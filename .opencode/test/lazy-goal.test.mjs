import assert from "node:assert/strict"
import { mkdtemp, mkdir, readFile, rm, writeFile } from "node:fs/promises"
import path from "node:path"
import os from "node:os"
import test from "node:test"
import lazyGoal from "../plugins/lazy-goal.js"
import lazyReminder from "../plugins/lazy-reminder.js"

const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds))

// 事件流：测试手动 push，插件通过 ctx.event.subscribe 消费。
function eventStream() {
  const buffer = []
  const waiters = []
  let done = false

  const finish = () => {
    done = true
    while (waiters.length) waiters.shift()({ value: undefined, done: true })
  }

  const iterator = {
    next() {
      if (buffer.length) return Promise.resolve({ value: buffer.shift(), done: false })
      if (done) return Promise.resolve({ value: undefined, done: true })
      return new Promise((resolve) => waiters.push(resolve))
    },
    return() {
      finish()
      return Promise.resolve({ value: undefined, done: true })
    },
  }

  return {
    push(value) {
      if (waiters.length) waiters.shift()({ value, done: false })
      else buffer.push(value)
    },
    finish,
    subscribe({ signal } = {}) {
      if (signal) signal.addEventListener("abort", finish)
      return { [Symbol.asyncIterator]: () => iterator }
    },
  }
}

// mock V2 插件上下文，暴露注册结果供断言。
function makeCtx(root) {
  const tools = new Map()
  const contextHooks = []
  const toolBeforeHooks = []
  const prompts = []
  const stream = eventStream()

  const registration = { dispose: async () => {} }

  const ctx = {
    location: { directory: root },
    tool: {
      async transform(callback) {
        const editor = {
          add(definition) {
            tools.set(definition.name, definition)
          },
          namespace() {},
        }
        callback(editor)
        return registration
      },
      async hook(name, callback) {
        toolBeforeHooks.push({ name, callback })
        return registration
      },
    },
    session: {
      async hook(name, callback) {
        contextHooks.push({ name, callback })
        return registration
      },
      async prompt(input) {
        prompts.push(input)
      },
    },
    event: {
      subscribe: stream.subscribe,
    },
  }

  return { ctx, tools, contextHooks, toolBeforeHooks, prompts, stream }
}

function toolContext(sessionID) {
  return { sessionID, agent: "build", messageID: "message-test", id: "call-test" }
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
  const harness = makeCtx(root)
  const sessionID = "goal-test"
  const context = toolContext(sessionID)
  let prompts = 0
  let activePrompts = 0
  let maximumConcurrentPrompts = 0

  harness.ctx.session.prompt = async () => {
    prompts += 1
    activePrompts += 1
    maximumConcurrentPrompts = Math.max(maximumConcurrentPrompts, activePrompts)
    if (prompts === 1) {
      await harness.tools.get("lazy_goal_progress").execute({ note: "完成第一个可验证步骤", verification: "mock check" }, context)
      await harness.tools.get("lazy_goal_pause").execute({ reason: "等待测试恢复" }, context)
    }
    await sleep(20)
    activePrompts -= 1
  }

  let cleanup
  try {
    cleanup = await lazyGoal.setup(harness.ctx)

    const names = [
      "lazy_goal_set",
      "lazy_goal_progress",
      "lazy_goal_mark_done",
      "lazy_goal_pause",
      "lazy_goal_resume",
      "lazy_goal_status",
      "lazy_goal_abort",
    ]
    assert.ok(names.every((name) => harness.tools.has(name)))

    await harness.tools.get("lazy_goal_set").execute({ goal: "完成长任务", max_turns: 5 }, context)
    for (let index = 0; index < 5; index++) harness.stream.push({ type: "session.idle", data: { sessionID } })
    await sleep(350)

    const paused = JSON.parse((await harness.tools.get("lazy_goal_status").execute({}, context)).content)
    assert.equal(prompts, 1)
    assert.equal(maximumConcurrentPrompts, 1)
    assert.equal(paused.status, "paused")
    assert.equal(paused.progress.length, 1)
    assert.equal(paused.noProgressTurns, 0)

    await harness.tools.get("lazy_goal_resume").execute({}, context)
    await sleep(350)
    assert.equal(prompts, 2)

    await harness.tools.get("lazy_goal_abort").execute({}, context)
    await assert.rejects(readFile(path.join(root, ".opencode", "lazy", "goal-state.json"), "utf8"))
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("Lazy Goal pauses an active persisted goal after OpenCode restarts", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-goal-restart-"))
  const first = makeCtx(root)
  const second = makeCtx(root)
  const context = toolContext("goal-restart")

  let cleanupFirst
  let cleanupSecond
  try {
    cleanupFirst = await lazyGoal.setup(first.ctx)
    await first.tools.get("lazy_goal_set").execute({ goal: "重启后需要人工恢复" }, context)

    cleanupSecond = await lazyGoal.setup(second.ctx)
    const persisted = JSON.parse(await readFile(path.join(root, ".opencode", "lazy", "goal-state.json"), "utf8"))
    assert.equal(persisted.status, "paused")
    assert.match(persisted.pausedReason, /goal-resume/)
  } finally {
    await cleanupFirst?.()
    await cleanupSecond?.()
    first.stream.finish()
    second.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("full change does not auto-start a goal", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-no-autogoal-"))
  const harness = makeCtx(root)

  let cleanup
  try {
    await setupFullChange(root)
    cleanup = await lazyGoal.setup(harness.ctx)
    assert.equal(harness.tools.has("lazy_goal_set"), true)
    await assert.rejects(readFile(path.join(root, ".opencode", "lazy", "goal-state.json"), "utf8"))
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge prompts up to 3 times for unfinished full change", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-"))
  const harness = makeCtx(root)
  const sessionID = "nudge-session"

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n- [x] 1.2 done\n")
    await setOwner(root, "demo", sessionID)
    cleanup = await lazyReminder.setup(harness.ctx)
    for (let index = 0; index < 6; index++) {
      harness.stream.push({ type: "session.idle", data: { sessionID } })
      await sleep(400)
    }
    assert.equal(harness.prompts.length, 3)
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("aborting a session suppresses idle nudges", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-abort-"))
  const harness = makeCtx(root)
  const sessionID = "abort-session"

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    cleanup = await lazyReminder.setup(harness.ctx)
    harness.stream.push({ type: "session.execution.interrupted", data: { sessionID, reason: "user" } })
    for (let index = 0; index < 3; index++) {
      harness.stream.push({ type: "session.idle", data: { sessionID } })
      await sleep(400)
    }
    assert.equal(harness.prompts.length, 0)
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is suppressed when tasks.md has a manual confirmation gate", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-gate-"))
  const harness = makeCtx(root)
  const sessionID = "gate-session"

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n- [ ] 1.2 自验收通过后停下等待用户手动确认\n")
    cleanup = await lazyReminder.setup(harness.ctx)
    for (let index = 0; index < 3; index++) {
      harness.stream.push({ type: "session.idle", data: { sessionID } })
      await sleep(400)
    }
    assert.equal(harness.prompts.length, 0)
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is suppressed when an unfinished task is marked [人工]", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-human-"))
  const harness = makeCtx(root)
  const sessionID = "human-session"

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 [人工] 停下等待用户确认\n")
    cleanup = await lazyReminder.setup(harness.ctx)
    for (let index = 0; index < 3; index++) {
      harness.stream.push({ type: "session.idle", data: { sessionID } })
      await sleep(400)
    }
    assert.equal(harness.prompts.length, 0)
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("manual goal completes without any audit gate", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-manual-goal-"))
  const harness = makeCtx(root)
  const sessionID = "manual-controller"
  const context = toolContext(sessionID)

  harness.ctx.session.prompt = async () => {
    await harness.tools.get("lazy_goal_progress").execute({ note: "step done", verification: "mock" }, context)
    await harness.tools.get("lazy_goal_mark_done").execute({ evidence: "done", verification: "mock check" }, context)
  }

  let cleanup
  try {
    cleanup = await lazyGoal.setup(harness.ctx)
    await harness.tools.get("lazy_goal_set").execute({ goal: "完成目标" }, context)
    await sleep(600)
    const final = JSON.parse((await harness.tools.get("lazy_goal_status").execute({}, context)).content)
    assert.equal(final.status, "completed")
    assert.equal(final.completion.verification, "mock check")
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge is skipped for subagent (child) sessions", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-child-"))
  const harness = makeCtx(root)
  const sessionID = "child-session"

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    cleanup = await lazyReminder.setup(harness.ctx)
    harness.stream.push({ type: "session.created", data: { sessionID, parentID: "parent-1" } })
    for (let index = 0; index < 3; index++) {
      harness.stream.push({ type: "session.idle", data: { sessionID } })
      await sleep(400)
    }
    assert.equal(harness.prompts.length, 0)
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("idle nudge only reaches the owning session", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-owner-"))
  const owner = "owner-session"
  const other = "other-session"
  const harness = makeCtx(root)

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    await setOwner(root, "demo", owner)
    cleanup = await lazyReminder.setup(harness.ctx)

    harness.stream.push({ type: "session.idle", data: { sessionID: other } })
    await sleep(400)
    assert.equal(harness.prompts.length, 0)

    harness.stream.push({ type: "session.idle", data: { sessionID: owner } })
    await sleep(400)
    assert.deepEqual(harness.prompts.map((prompt) => prompt.sessionID), [owner])
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("source edit claims flow ownership for the editing session", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-nudge-claim-"))
  const editor = "editor-session"
  const other = "other-session"
  const harness = makeCtx(root)

  let cleanup
  try {
    await setupFullChange(root)
    await writeFile(path.join(root, "docs", "changes", "demo", "tasks.md"), "- [ ] 1.1 todo\n")
    cleanup = await lazyReminder.setup(harness.ctx)

    await harness.toolBeforeHooks[0].callback({ tool: "edit", input: { filePath: "src/app.js" }, sessionID: editor })

    harness.stream.push({ type: "session.idle", data: { sessionID: other } })
    await sleep(400)
    assert.equal(harness.prompts.length, 0)

    harness.stream.push({ type: "session.idle", data: { sessionID: editor } })
    await sleep(400)
    assert.deepEqual(harness.prompts.map((prompt) => prompt.sessionID), [editor])
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})

test("Lean mode resolves new leanMode and legacy ponytailMode, and off disables the guard", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "lazy-mode-"))
  const harness = makeCtx(root)
  const sessionID = "mode-session"
  const statePath = path.join(root, ".opencode", "lazy", "state.json")

  let cleanup
  try {
    await mkdir(path.join(root, ".opencode", "lazy"), { recursive: true })
    cleanup = await lazyReminder.setup(harness.ctx)

    await writeFile(statePath, JSON.stringify({ initialized: true, level: "tiny", phase: "idle", ponytailMode: "full" }))
    const legacy = { sessionID, system: [] }
    await harness.contextHooks[0].callback(legacy)
    assert.ok(legacy.system.some((part) => part.text.includes("lean-check")))

    await writeFile(statePath, JSON.stringify({ initialized: true, level: "tiny", phase: "idle", leanMode: "off" }))
    const off = { sessionID, system: [] }
    await harness.contextHooks[0].callback(off)
    assert.ok(!off.system.some((part) => part.text.includes("lean-check")))
  } finally {
    await cleanup?.()
    harness.stream.finish()
    await rm(root, { recursive: true, force: true })
  }
})
