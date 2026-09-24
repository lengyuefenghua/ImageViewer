import fs from "fs"
import path from "path"
import { fileURLToPath } from "url"
import { tool } from "@opencode-ai/plugin"

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const templateRoot = path.resolve(__dirname, "..", "..")
const runtimes = new Map()

const DEFAULT_MAX_TURNS = 50
const DEFAULT_MAX_NO_PROGRESS_TURNS = 3
const LOCK_STALE_MS = 60_000

function readJson(file, fallback) {
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"))
  } catch {
    return fallback
  }
}

function lazyState(root) {
  return readJson(path.join(root, ".opencode", "lazy", "state.json"), {})
}

function goalStatePath(root) {
  return path.join(root, ".opencode", "lazy", "goal-state.json")
}

function lockPath(root) {
  return path.join(root, ".opencode", "lazy", "goal-run.lock")
}

function readGoal(root) {
  return readJson(goalStatePath(root), null)
}

function writeGoal(root, state) {
  const file = goalStatePath(root)
  fs.mkdirSync(path.dirname(file), { recursive: true })
  const temporary = `${file}.${process.pid}.tmp`
  fs.writeFileSync(temporary, JSON.stringify(state, null, 2), "utf8")
  fs.renameSync(temporary, file)
}

function deleteGoal(root) {
  try {
    fs.rmSync(goalStatePath(root), { force: true })
  } catch {}
}

function now() {
  return new Date().toISOString()
}

function asPositiveInt(value, fallback) {
  return typeof value === "number" && Number.isInteger(value) && value > 0 ? value : fallback
}

function clearRuntime(sessionID) {
  const runtime = runtimes.get(sessionID)
  if (!runtime) return
  if (runtime.timer) clearTimeout(runtime.timer)
  runtimes.delete(sessionID)
}

function acquireRunLock(root, sessionID) {
  const file = lockPath(root)
  fs.mkdirSync(path.dirname(file), { recursive: true })

  try {
    const current = fs.statSync(file)
    if (Date.now() - current.mtimeMs > LOCK_STALE_MS) fs.rmSync(file, { force: true })
  } catch {}

  try {
    fs.writeFileSync(file, JSON.stringify({ pid: process.pid, sessionID, startedAt: now() }), { flag: "wx" })
  } catch {
    return null
  }

  const heartbeat = setInterval(() => {
    try {
      const time = new Date()
      fs.utimesSync(file, time, time)
    } catch {}
  }, 5_000)

  return () => {
    clearInterval(heartbeat)
    try {
      fs.rmSync(file, { force: true })
    } catch {}
  }
}

function ownerState(root, sessionID) {
  const state = readGoal(root)
  if (!state) return { state: null, error: "当前项目没有 Lazy Goal。先调用 lazy_goal_set。" }
  if (state.sessionID !== sessionID) {
    return { state: null, error: "当前 Lazy Goal 属于另一个会话。只有创建它的主控会话可以推进、暂停或完成该 Goal。" }
  }
  return { state, error: null }
}

function updateFlowSnapshot(root, state) {
  const flow = lazyState(root)
  state.activeChange = flow.activeChange ?? null
  state.phase = flow.phase ?? "idle"
  state.level = flow.level ?? "tiny"
}

function buildTurnPrompt(root, state) {
  updateFlowSnapshot(root, state)
  writeGoal(root, state)
  const nextHint = state.activeChange
    ? `当前 active change 是 ${state.activeChange}，阶段为 ${state.phase}，级别为 ${state.level}。`
    : "当前没有 active change；先根据最终目标加载 using-lazy-flow 并决定正确起点。"

  return `[Lazy Goal] 最终目标：${state.goal}

${nextHint}
本次 /goal 已对最终目标及已确认范围授予一次性实施授权。读取 .opencode/lazy/state.json、当前 change 的状态和产物后，只推进一个依赖已满足的下一步。

规则：
- 必须继续遵守 Lazy Flow、技能加载、TDD、验证和状态机；Goal 不替代这些规则。
- full 实现默认 inline；任务可并行或需隔离上下文时通过 subagent-dev 派发子代理，主控负责验证、审查和勾选任务。
- 行为任务必须先加载 tdd-cycle，完成 RED 后再做 GREEN。
- 每完成一个可验证步骤，立即调用 lazy_goal_progress，写明完成内容和验证证据。
- 最终目标满足（含归档与提交）时才调用 lazy_goal_mark_done 并提供最终验证证据。
- 需求不清、需要用户决策、验证失败、任务范围不明确、发现冲突、审查有重要发现，或 finish 需要用户确认同步/归档时，调用 lazy_goal_pause 说明原因；不要猜测或越权继续。
- 不要仅报告某个局部任务完成后停止。`
}

function scheduleContinue(root, client, sessionID, delay = 250) {
  const runtime = runtimes.get(sessionID)
  if (!runtime || runtime.running || runtime.pending) return
  const state = readGoal(root)
  if (!state || state.sessionID !== sessionID || state.status !== "active") return

  runtime.pending = true
  runtime.timer = setTimeout(() => {
    runtime.timer = null
    runtime.pending = false
    void continueGoal(root, client, sessionID)
  }, delay)
}

async function continueGoal(root, client, sessionID) {
  const runtime = runtimes.get(sessionID)
  const state = readGoal(root)
  if (!runtime || runtime.running || !state || state.sessionID !== sessionID || state.status !== "active") return

  if (state.turns >= state.maxTurns) {
    state.status = "paused"
    state.pausedReason = `达到 /goal 设置的最大自动轮次 (${state.maxTurns})。`
    state.updatedAt = now()
    writeGoal(root, state)
    return
  }

  const releaseLock = acquireRunLock(root, sessionID)
  if (!releaseLock) {
    scheduleContinue(root, client, sessionID, 2_000)
    return
  }

  runtime.running = true
  runtime.progressed = false
  state.turns += 1
  state.updatedAt = now()
  writeGoal(root, state)

  try {
    await client?.session?.prompt?.({
      path: { id: sessionID },
      body: { parts: [{ type: "text", text: buildTurnPrompt(root, state) }] },
    })
  } catch {}
  finally {
    releaseLock()
    runtime.running = false
  }

  const latest = readGoal(root)
  if (!latest || latest.sessionID !== sessionID || latest.status !== "active") return

  if (runtime.progressed) {
    latest.noProgressTurns = 0
  } else {
    latest.noProgressTurns = (latest.noProgressTurns ?? 0) + 1
  }
  latest.updatedAt = now()

  if (latest.noProgressTurns >= latest.maxNoProgressTurns) {
    latest.status = "paused"
    latest.pausedReason = `连续 ${latest.noProgressTurns} 轮没有记录可验证进展。`
  }
  writeGoal(root, latest)

  if (latest.status === "active") scheduleContinue(root, client, sessionID)
}

function createGoalState(sessionID, flow, options) {
  return {
    version: 1,
    sessionID,
    goal: options.goal,
    startedAt: now(),
    updatedAt: now(),
    activeChange: flow.activeChange ?? null,
    phase: flow.phase ?? "idle",
    level: flow.level ?? "tiny",
    authorized: true,
    status: "active",
    turns: 0,
    maxTurns: asPositiveInt(options.maxTurns, DEFAULT_MAX_TURNS),
    noProgressTurns: 0,
    maxNoProgressTurns: asPositiveInt(options.maxNoProgressTurns, DEFAULT_MAX_NO_PROGRESS_TURNS),
    progress: [],
    pausedReason: null,
    completion: null,
  }
}

export default async ({ client, directory, project } = {}) => {
  const root = directory || project?.root || templateRoot
  const persistedGoal = readGoal(root)
  if (persistedGoal?.status === "active") {
    persistedGoal.status = "paused"
    persistedGoal.pausedReason = "OpenCode 已重启；使用 /goal-resume 重新接管并继续。"
    persistedGoal.updatedAt = now()
    writeGoal(root, persistedGoal)
  }

  return {
    event: async ({ event }) => {
      if (event?.type !== "session.idle") return
      const sessionID = event.properties?.sessionID
      if (typeof sessionID === "string") scheduleContinue(root, client, sessionID)
    },

    tool: {
      lazy_goal_set: tool({
        description: "显式启动当前项目的 Lazy Goal 连续执行器，仅用于用户主动发起的 /goal 最终目标。它会按 Lazy Flow 持续推进，直到完成、暂停或达到安全上限。",
        args: {
          goal: tool.schema.string().min(1),
          max_turns: tool.schema.number().int().positive().optional(),
          max_no_progress_turns: tool.schema.number().int().positive().optional(),
        },
        async execute(args, context) {
          const existing = readGoal(root)
          if (existing?.status === "active" && existing.sessionID !== context.sessionID) {
            return "当前项目已有另一个会话正在执行 Lazy Goal。请先由其主控会话暂停或停止。"
          }

          const flow = lazyState(root)
          if (existing?.sessionID) clearRuntime(existing.sessionID)
          const state = createGoalState(context.sessionID, flow, {
            goal: args.goal.trim(),
            maxTurns: args.max_turns,
            maxNoProgressTurns: args.max_no_progress_turns,
          })
          writeGoal(root, state)
          runtimes.set(context.sessionID, { running: false, pending: false, timer: null, progressed: false })
          scheduleContinue(root, client, context.sessionID)
          return `Lazy Goal 已启动：${state.goal}。将在当前 Lazy Flow 状态下持续推进，最多 ${state.maxTurns} 轮；连续 ${state.maxNoProgressTurns} 轮无显式进展会暂停。`
        },
      }),

      lazy_goal_progress: tool({
        description: "记录当前 Lazy Goal 的一个可验证进展。完成后引擎会继续下一个步骤。",
        args: {
          note: tool.schema.string().min(1),
          verification: tool.schema.string().optional(),
        },
        async execute(args, context) {
          const { state, error } = ownerState(root, context.sessionID)
          if (error) return error
          if (state.status !== "active") return `Lazy Goal 当前状态为 ${state.status}，不能记录进展。`

          updateFlowSnapshot(root, state)
          state.progress.push({
            at: now(),
            note: args.note.trim(),
            verification: args.verification?.trim() || null,
          })
          state.progress = state.progress.slice(-50)
          state.noProgressTurns = 0
          state.updatedAt = now()
          writeGoal(root, state)
          const runtime = runtimes.get(context.sessionID)
          if (runtime) runtime.progressed = true
          return "Lazy Goal 进展已记录。完成当前回合后将继续检查下一个步骤。"
        },
      }),

      lazy_goal_mark_done: tool({
        description: "仅在最终 Lazy Goal 已满足时调用。调用后停止自动续跑。",
        args: {
          evidence: tool.schema.string().min(1),
          verification: tool.schema.string().min(1),
        },
        async execute(args, context) {
          const { state, error } = ownerState(root, context.sessionID)
          if (error) return error

          state.status = "completed"
          state.completion = { at: now(), evidence: args.evidence.trim(), verification: args.verification.trim() }
          state.pausedReason = null
          state.updatedAt = now()
          writeGoal(root, state)
          clearRuntime(context.sessionID)
          return "Lazy Goal 已完成并停止续跑。"
        },
      }),

      lazy_goal_pause: tool({
        description: "暂停当前 Lazy Goal 并保留状态。需求不清、需要用户决策、验证失败或需要同步/归档确认时调用。",
        args: { reason: tool.schema.string().min(1) },
        async execute(args, context) {
          const { state, error } = ownerState(root, context.sessionID)
          if (error) return error
          state.status = "paused"
          state.pausedReason = args.reason.trim()
          state.updatedAt = now()
          writeGoal(root, state)
          clearRuntime(context.sessionID)
          return `Lazy Goal 已暂停：${state.pausedReason}`
        },
      }),

      lazy_goal_resume: tool({
        description: "恢复当前项目已暂停的 Lazy Goal。仅在用户已处理暂停原因后调用。",
        args: {},
        async execute(_args, context) {
          const state = readGoal(root)
          if (!state) return "当前项目没有可恢复的 Lazy Goal。"
          if (state.status !== "paused") return `Lazy Goal 当前状态为 ${state.status}，不能恢复。`

          clearRuntime(state.sessionID)
          state.sessionID = context.sessionID
          state.status = "active"
          state.pausedReason = null
          state.updatedAt = now()
          updateFlowSnapshot(root, state)
          writeGoal(root, state)
          runtimes.set(context.sessionID, { running: false, pending: false, timer: null, progressed: false })
          scheduleContinue(root, client, context.sessionID)
          return "Lazy Goal 已恢复，将继续推进下一个步骤。"
        },
      }),

      lazy_goal_status: tool({
        description: "查看当前项目 Lazy Goal 的目标、状态、轮次、最近进展和暂停或完成证据。",
        args: {},
        async execute() {
          const state = readGoal(root)
          return state ? JSON.stringify(state, null, 2) : "当前项目没有 Lazy Goal。"
        },
      }),

      lazy_goal_abort: tool({
        description: "停止并删除当前项目的 Lazy Goal 状态。仅在用户明确要求停止时调用。",
        args: {},
        async execute(_args, context) {
          const { state, error } = ownerState(root, context.sessionID)
          if (error) return error
          clearRuntime(state.sessionID)
          deleteGoal(root)
          try {
            fs.rmSync(lockPath(root), { force: true })
          } catch {}
          return "Lazy Goal 已停止并删除本地状态。"
        },
      }),
    },
  }
}
