import fs from "fs"
import path from "path"
import { fileURLToPath } from "url"

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const projectRoot = path.resolve(__dirname, "..", "..")
const sessions = new Map()

function readJson(file, fallback) {
  try {
    return JSON.parse(fs.readFileSync(file, "utf8"))
  } catch {
    return fallback
  }
}

function readMode(file) {
  const statePath = file || path.join(projectRoot, ".opencode", "lazy", "state.json")
  const state = readJson(statePath, {})
  const mode = state.leanMode ?? state.ponytailMode
  if (["lite", "full", "ultra", "off"].includes(mode)) return mode
  return "full"
}

function readGoal(file) {
  return readJson(file, null)
}

function ownerPath(root) {
  return path.join(root, ".opencode", "lazy", "reminder-owner.json")
}

function readOwner(root) {
  return readJson(ownerPath(root), null)
}

function writeOwner(root, record) {
  const file = ownerPath(root)
  fs.mkdirSync(path.dirname(file), { recursive: true })
  const temporary = `${file}.${process.pid}.tmp`
  fs.writeFileSync(temporary, JSON.stringify(record, null, 2), "utf8")
  fs.renameSync(temporary, file)
}

function changeIdFromPath(filePath) {
  if (typeof filePath !== "string") return null
  const match = filePath.replaceAll("\\", "/").match(/docs\/changes\/([^/]+)\//)
  return match ? match[1] : null
}

const FLOW_GUARD = `Lazy Flow 守卫：涉及新增、修改、修复、重构、调试、测试、审查、设计、依赖选型，或会改变项目代码、配置、文档、测试、行为的请求时，在探索、设计或修改前必须调用 skill 工具加载 using-lazy-flow，并按其分级和路由继续；进入后续阶段前必须加载对应技能，不得只凭技能名称或记忆执行。full 变更先加载 grill-me、spec-design、task-plan；实现默认 inline，任务可并行或需隔离上下文时按 subagent-dev 派发。行为变更必须加载 tdd-cycle。界面/前端/GUI 变更必须加载 ui-visual-verify，用 opencode_computer_* 实际操作应用并观察改动后的真实界面截图，无截图证据不得声明完成。技能已由 OpenCode 注册，不需要文件搜索。纯问答和无开发动作的任务不适用本守卫。`

function leanGuard(mode) {
  if (mode === "off") return null
  const strength = mode === "ultra"
    ? "Lean ultra：实现、修复、重构、依赖选型和审查前加载 lean-check；优先删除、复用和原生能力，拒绝投机抽象。"
    : "Lean：实现、修复、重构、依赖选型和审查前加载 lean-check；保持最小正确 diff，优先复用和原生能力。"
  return strength
}

function isSourcePath(filePath) {
  if (!filePath || typeof filePath !== "string") return false
  const normalized = filePath.replaceAll("\\", "/")
  if (normalized.includes(".opencode/")) return false
  if (normalized.includes("docs/changes/")) return false
  if (normalized.endsWith("AGENTS.md")) return false
  if (normalized.includes("/test/") || normalized.includes("/tests/") || normalized.includes("__tests__")) return false
  return /\.(js|jsx|ts|tsx|mjs|cjs|py|go|rs|java|kt|swift|php|rb|cs|cpp|c|h|hpp|vue|svelte)$/.test(normalized)
}

function loadedSkills(sessionID) {
  if (!sessions.has(sessionID)) sessions.set(sessionID, new Set())
  return sessions.get(sessionID)
}

function countTasks(file) {
  try {
    const lines = fs.readFileSync(file, "utf8").split(/\r?\n/)
    const isCommit = (line) => /提交|commit/i.test(line)
    const isManualGate = (line) => line.includes("[人工]") || /停下.*确认|等待用户|手动确认|手动验收|用户复核|等待复核|复核后/.test(line)
    const uncheckedLines = lines.filter((line) => /^\s*-\s*\[ \]/.test(line))
    const unchecked = uncheckedLines.filter((line) => !isCommit(line)).length
    const done = lines.filter((line) => /^\s*-\s*\[[xX]\]/.test(line)).length
    const manualGate = uncheckedLines.some(isManualGate)
    return { unchecked, done, manualGate }
  } catch {
    return null
  }
}

export default async ({ client, directory, project } = {}) => {
  const root = directory || project?.root || projectRoot
  const localStatePath = path.join(root, ".opencode", "lazy", "state.json")
  const nudges = new Map()
  const childSessions = new Set()

  const claim = (changeId, sessionID) => {
    if (!changeId || typeof sessionID !== "string") return
    writeOwner(root, { changeId, sessionID, updatedAt: new Date().toISOString() })
  }

  const toast = async (message) => {
    try {
      await client?.tui?.showToast?.({ body: { title: "Lazy Flow", message, variant: "info" } })
    } catch {}
  }

  return {
    "experimental.chat.system.transform": async (input, output) => {
      const mode = readMode(localStatePath)
      output.system.push(FLOW_GUARD)
      const guard = leanGuard(mode)
      if (guard) output.system.push(guard)
      const goal = readGoal(path.join(root, ".opencode", "lazy", "goal-state.json"))
      if (goal?.status === "active" && (!input.sessionID || input.sessionID === goal.sessionID)) {
        output.system.push(`Lazy Goal 正在执行：${goal.goal}。不要在局部步骤完成后仅报告并停止；每个可验证步骤调用 lazy_goal_progress。需求不清、验证失败、需要用户决策或归档确认时调用 lazy_goal_pause。只有最终目标满足并有验证证据时调用 lazy_goal_mark_done。`)
      }
    },

    event: async ({ event }) => {
      if (event?.type === "session.created" || event?.type === "session.updated") {
        const info = event.properties?.info
        if (info?.id) {
          if (info.parentID) childSessions.add(info.id)
          else childSessions.delete(info.id)
        }
        return
      }
      if (event?.type === "session.deleted") {
        const info = event.properties?.info
        if (info?.id) childSessions.delete(info.id)
        return
      }
      if (event?.type === "session.error") {
        const abortedSession = event.properties?.sessionID
        if (typeof abortedSession === "string" && event.properties?.error?.name === "MessageAbortedError") {
          const state = nudges.get(abortedSession) || { count: 0, lastUnchecked: null, pending: false }
          state.suppressed = true
          state.pending = false
          nudges.set(abortedSession, state)
        }
        return
      }
      if (event?.type !== "session.idle") return
      const sessionID = event.properties?.sessionID
      if (typeof sessionID !== "string") return
      if (childSessions.has(sessionID)) return
      const flow = readJson(localStatePath, {})
      if (flow.level !== "full" || !flow.activeChange) {
        nudges.delete(sessionID)
        return
      }
      const owner = readOwner(root)
      if (!owner || owner.changeId !== flow.activeChange || owner.sessionID !== sessionID) {
        nudges.delete(sessionID)
        return
      }
      const tasks = countTasks(path.join(root, "docs", "changes", flow.activeChange, "tasks.md"))
      if (!tasks || tasks.unchecked === 0 || tasks.manualGate) {
        nudges.delete(sessionID)
        return
      }
      const state = nudges.get(sessionID) || { count: 0, lastUnchecked: null, pending: false, suppressed: false }
      if (state.changeId && state.changeId !== flow.activeChange) {
        state.suppressed = false
        state.count = 0
        state.lastUnchecked = null
      }
      state.changeId = flow.activeChange
      if (state.lastUnchecked !== null && tasks.unchecked < state.lastUnchecked) state.count = 0
      state.lastUnchecked = tasks.unchecked
      nudges.set(sessionID, state)
      if (state.suppressed || state.pending || state.count >= 3) return
      state.pending = true
      state.count += 1
      const nudgeText = `[Lazy Flow] 空闲核对（第 ${state.count}/3 次）：docs/changes/${flow.activeChange}/tasks.md 还有 ${tasks.unchecked} 条未完成。请逐条确认是否已完整实现（不是最小实现/偷懒）；未完成的继续做，全部完成请明确说明已完成，不要只报告局部进度。`
      setTimeout(() => {
        state.pending = false
        void client?.session?.prompt?.({ path: { id: sessionID }, body: { parts: [{ type: "text", text: nudgeText }] } }).catch(() => {})
      }, 300)
    },

    "tool.execute.before": async (input, output) => {
      const toolName = input?.tool
      const args = output?.args || {}
      const sessionID = input?.sessionID
      const skills = loadedSkills(sessionID)

      if (toolName === "skill" && typeof args.name === "string") {
        skills.add(args.name)
        if (args.name === "using-lazy-flow") {
          const flow = readJson(localStatePath, {})
          if (flow.activeChange) claim(flow.activeChange, sessionID)
        }
        return
      }

      if (!["edit", "write", "patch"].includes(toolName)) return

      const filePath = args.filePath || args.path || args.file || args.target
      const changeId = changeIdFromPath(filePath)
      if (changeId) {
        claim(changeId, sessionID)
      } else if (isSourcePath(filePath)) {
        const flow = readJson(localStatePath, {})
        if (flow.activeChange) claim(flow.activeChange, sessionID)
      }

      if (!isSourcePath(filePath)) return

      const state = readJson(localStatePath, {})
      const mode = readMode(localStatePath)
      const goal = readGoal(path.join(root, ".opencode", "lazy", "goal-state.json"))
      const reminders = []
      if (!skills.has("using-lazy-flow")) {
        reminders.push("开发操作前尚未加载 using-lazy-flow，请先完成分级和路由")
      }
      if (["patch", "full"].includes(state.level) && !state.activeChange) {
        reminders.push("当前是 patch/full 变更但尚未设置 activeChange，请先建立或恢复 change")
      }
      if (mode !== "off" && !skills.has("lean-check")) {
        reminders.push("源码编辑前尚未加载 lean-check，请先执行最小实现检查")
      }
      if (["design", "planning"].includes(state.phase)) {
        reminders.push(`当前阶段是 ${state.phase}，除非用户明确要求直接实现，请先完成当前工作流阶段`)
      }
      if (goal?.status === "active" && input.sessionID === goal.sessionID) {
        reminders.push("存在 active Lazy Goal，完成可验证步骤后记录 lazy_goal_progress")
      }
      if (reminders.length > 0) await toast(reminders.join("；"))
    },
  }
}
