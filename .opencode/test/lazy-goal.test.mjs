import assert from "node:assert/strict"
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises"
import path from "node:path"
import os from "node:os"
import test from "node:test"
import lazyGoal from "../plugins/lazy-goal.js"
import lazyReminder from "../plugins/lazy-reminder.js"

const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds))

function context(root, sessionID = "goal-test") {
  return {
    sessionID,
    messageID: "message-test",
    agent: "build",
    directory: root,
    worktree: root,
    abort: new AbortController().signal,
    metadata() {},
    ask: async () => {},
  }
}

async function setupFullChange(root, change = "demo") {
  await mkdir(path.join(root, ".opencode", "lazy"), { recursive: true })
  await writeFile(
    path.join(root, ".opencode", "lazy", "state.json"),
    JSON.stringify({ initialized: true, activeChange: change, level: "full", phase: "implementing", leanMode: "full" }),
  )
  const specDir = path.join(root, "docs", "changes", change, "specs", "auth")
  await mkdir(specDir, { recursive: true })
  await writeFile(path.join(specDir, "spec.md"), ["## ADDED Requirements", "", "### Requirement: 登录", "#### Scenario: 成功"].join("\n"))
}

async function setOwner(root, change, sessionID) {
  await writeFile(
    path.join(root, ".opencode", "lazy", "reminder-owner.json"),
    JSON.stringify({ changeId: change, sessionID, updatedAt: new Date().toISOString() }),
  )
}

test("Lazy Goal serializes continuation and persists progress through pause, resume, and stop", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-goal-"))
  const ctx = context(root)
  let hooks
  let prompts = 0
  let activePrompts = 0
  let maximumConcurrentPrompts = 0
  const client = {
    session: {
      prompt: async () => {
        prompts += 1
        activePrompts += 1
        maximumConcurrentPrompts = Math.max(maximumConcurrentPrompts, activePrompts)
        if (prompts === 1) {
          await hooks.tool.lazy_goal_progress.execute({ note: "完成第一个可验证步骤", verification: "mock check" }, ctx)
          await hooks.tool.lazy_goal_pause.execute({ reason: "等待测试恢复" }, ctx)
        }
        await sleep(20)
        activePrompts -= 1
      },
    },
  }

  try {
    hooks = await lazyGoal({ directory: root, client })
    const names = [
      "lazy_goal_set",
      "lazy_goal_progress",
      "lazy_goal_mark_done",
      "lazy_goal_pause",
      "lazy_goal_resume",
      "lazy_goal_status",
      "lazy_goal_abort",
    ]
    assert.ok(names.every((name) => hooks.tool[name]))

    await hooks.tool.lazy_goal_set.execute({ goal: "完成长任务", max_turns: 5 }, ctx)
    await Promise.all(Array.from({ length: 5 }, () => hooks.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })))
    await sleep(350)

    const paused = JSON.parse(await hooks.tool.lazy_goal_status.execute({}, ctx))
    assert.equal(prompts, 1)
    assert.equal(maximumConcurrentPrompts, 1)
    assert.equal(paused.status, "paused")
    assert.equal(paused.progress.length, 1)
    assert.equal(paused.noProgressTurns, 0)

    await hooks.tool.lazy_goal_resume.execute({}, ctx)
    await sleep(350)
    assert.equal(prompts, 2)

    await hooks.tool.lazy_goal_abort.execute({}, ctx)
    await assert.rejects(readFile(path.join(root, ".opencode", "lazy", "goal-state.json"), "utf8"))
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("Lazy Goal pauses an active persisted goal after OpenCode restarts", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-goal-restart-"))
  const ctx = context(root, "goal-restart")
  const client = { session: { prompt: async () => {} } }

  try {
    const first = await lazyGoal({ directory: root, client })
    await first.tool.lazy_goal_set.execute({ goal: "重启后需要人工恢复" }, ctx)

    await lazyGoal({ directory: root, client })
    const persisted = JSON.parse(await readFile(path.join(root, ".opencode", "lazy", "goal-state.json"), "utf8"))
    assert.equal(persisted.status, "paused")
    assert.match(persisted.pausedReason, /goal-resume/)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("full change does not auto-start a goal", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-no-autogoal-"))
  const ctx = context(root, "controller")
  const client = { session: { prompt: async () => {} } }

  try {
    await setupFullChange(root)
    const hooks = await lazyGoal({ directory: root, client })
    assert.equal(hooks["tool.execute.before"], undefined)
    assert.equal(hooks.tool.lazy_goal_set !== undefined, true)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge prompts up to 3 times for unfinished full change", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-"))
  const ctx = context(root, "nudge-session")
  let prompts = 0
  const client = { session: { prompt: async () => { prompts += 1 } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n- [x] 1.2 done\n")
    await setOwner(root, "demo", ctx.sessionID)
    const reminder = await lazyReminder({ directory: root, client })
    for (let index = 0; index < 6; index++) {
      await reminder.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })
      await sleep(400)
    }
    assert.equal(prompts, 3)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("aborting a session suppresses idle nudges", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-abort-"))
  const ctx = context(root, "abort-session")
  let prompts = 0
  const client = { session: { prompt: async () => { prompts += 1 } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    const reminder = await lazyReminder({ directory: root, client })
    await reminder.event({ event: { type: "session.error", properties: { sessionID: ctx.sessionID, error: { name: "MessageAbortedError" } } } })
    for (let index = 0; index < 3; index++) {
      await reminder.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })
      await sleep(400)
    }
    assert.equal(prompts, 0)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is suppressed when tasks.md has a manual confirmation gate", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-gate-"))
  const ctx = context(root, "gate-session")
  let prompts = 0
  const client = { session: { prompt: async () => { prompts += 1 } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n- [ ] 1.2 自验收通过后停下等待用户手动确认\n")
    const reminder = await lazyReminder({ directory: root, client })
    for (let index = 0; index < 3; index++) {
      await reminder.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })
      await sleep(400)
    }
    assert.equal(prompts, 0)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is suppressed when an unfinished task is marked [人工]", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-human-"))
  const ctx = context(root, "human-session")
  let prompts = 0
  const client = { session: { prompt: async () => { prompts += 1 } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 [人工] 停下等待用户确认\n")
    const reminder = await lazyReminder({ directory: root, client })
    for (let index = 0; index < 3; index++) {
      await reminder.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })
      await sleep(400)
    }
    assert.equal(prompts, 0)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("manual goal completes without any audit gate", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-manual-goal-"))
  const ctx = context(root, "manual-controller")
  let hooks
  const client = {
    session: {
      prompt: async () => {
        await hooks.tool.lazy_goal_progress.execute({ note: "step done", verification: "mock" }, ctx)
        await hooks.tool.lazy_goal_mark_done.execute({ evidence: "done", verification: "mock check" }, ctx)
      },
    },
  }

  try {
    hooks = await lazyGoal({ directory: root, client })
    await hooks.tool.lazy_goal_set.execute({ goal: "完成目标" }, ctx)
    await sleep(900)
    const final = JSON.parse(await hooks.tool.lazy_goal_status.execute({}, ctx))
    assert.equal(final.status, "completed")
    assert.equal(final.completion.verification, "mock check")
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is skipped for subagent (child) sessions", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-child-"))
  const ctx = context(root, "child-session")
  let prompts = 0
  const client = { session: { prompt: async () => { prompts += 1 } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    const reminder = await lazyReminder({ directory: root, client })
    await reminder.event({ event: { type: "session.created", properties: { info: { id: ctx.sessionID, parentID: "parent-1" } } } })
    for (let index = 0; index < 3; index++) {
      await reminder.event({ event: { type: "session.idle", properties: { sessionID: ctx.sessionID } } })
      await sleep(400)
    }
    assert.equal(prompts, 0)
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge only reaches the owning session", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-owner-"))
  const owner = context(root, "owner-session")
  const other = context(root, "other-session")
  const prompts = []
  const client = { session: { prompt: async ({ path }) => { prompts.push(path.id) } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    await setOwner(root, "demo", owner.sessionID)
    const reminder = await lazyReminder({ directory: root, client })

    await reminder.event({ event: { type: "session.idle", properties: { sessionID: other.sessionID } } })
    await sleep(400)
    assert.equal(prompts.length, 0)

    await reminder.event({ event: { type: "session.idle", properties: { sessionID: owner.sessionID } } })
    await sleep(400)
    assert.deepEqual(prompts, [owner.sessionID])
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("source edit claims flow ownership for the editing session", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-claim-"))
  const editor = context(root, "editor-session")
  const other = context(root, "other-session")
  const prompts = []
  const client = { session: { prompt: async ({ path }) => { prompts.push(path.id) } } }

  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    const reminder = await lazyReminder({ directory: root, client })

    await reminder["tool.execute.before"]({ tool: "edit", sessionID: editor.sessionID }, { args: { filePath: "src/app.js" } })

    await reminder.event({ event: { type: "session.idle", properties: { sessionID: other.sessionID } } })
    await sleep(400)
    assert.equal(prompts.length, 0)

    await reminder.event({ event: { type: "session.idle", properties: { sessionID: editor.sessionID } } })
    await sleep(400)
    assert.deepEqual(prompts, [editor.sessionID])
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})

test("Lean mode resolves new leanMode and legacy ponytailMode, and off disables the guard", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-mode-"))
  const ctx = context(root, "mode-session")
  const client = { session: { prompt: async () => {} } }
  const statePath = path.join(root, ".opencode", "lazy", "state.json")

  try {
    await mkdir(path.join(root, ".opencode", "lazy"), { recursive: true })
    const reminder = await lazyReminder({ directory: root, client })

    await writeFile(statePath, JSON.stringify({ initialized: true, level: "tiny", phase: "idle", ponytailMode: "full" }))
    const legacy = { system: [] }
    await reminder["experimental.chat.system.transform"]({ sessionID: ctx.sessionID }, legacy)
    assert.ok(legacy.system.some((text) => text.includes("lean-check")))

    await writeFile(statePath, JSON.stringify({ initialized: true, level: "tiny", phase: "idle", leanMode: "off" }))
    const off = { system: [] }
    await reminder["experimental.chat.system.transform"]({ sessionID: ctx.sessionID }, off)
    assert.ok(!off.system.some((text) => text.includes("lean-check")))
  } finally {
    await rm(root, { recursive: true, force: true })
  }
})



