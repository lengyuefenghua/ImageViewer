import fs from "fs"
import os from "os"
import path from "path"
import { spawn } from "child_process"

// Lazy Flow 桌面通知。
// OpenCode V2 原生通知走终端 OSC 序列，Windows Terminal 不渲染成系统横幅；
// 本插件在服务端直接弹 Windows 原生 Toast，作为原生通知的补充。
//
// 后端选择：
//   - Windows: powershell（Windows PowerShell 5.1，唯一支持 WinRT Toast）
//   - WSL:     Windows PowerShell 5.1 绝对路径 → powershell.exe → notify-send
//   - Linux:   notify-send
//
// 注：PowerShell 7（pwsh）不支持 WinRT Toast，因此不用于通知；pwsh 只适合当终端。
//
// 覆盖通知：done / error / permission / question（default 为兜底文案）。
// 子会话仅“完成”不通知；权限和提问仍会通知，避免子代理被卡住而无人处理。
// 关闭方式：
//   - 环境变量 LAZY_NOTIFY=0（全关）
//   - cli.json 的 attention.notifications: false（全关）

const PS5_WSL = "/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"

function fileExists(candidate) {
  try {
    return fs.existsSync(candidate)
  } catch {
    return false
  }
}

// 归一化目录，便于跨实例比较（Windows 忽略大小写、去掉末尾斜杠）。
function normalizeDirectory(directory) {
  if (typeof directory !== "string" || directory === "") return null
  let normalized = path.resolve(directory)
  if (process.platform === "win32") normalized = normalized.toLowerCase()
  return normalized.replace(/[\\/]+$/, "")
}

function isWsl() {
  if (process.platform !== "linux") return false
  if (process.env.WSL_DISTRO_NAME || process.env.WSL_INTEROP) return true
  try {
    return fs.readFileSync("/proc/version", "utf8").toLowerCase().includes("microsoft")
  } catch {
    return false
  }
}

// 纯函数，便于测试：返回按优先级排序的后端候选 [{ type, command }]。
export function resolveCandidates(options = {}) {
  const platform = options.platform ?? process.platform
  const wsl = options.wsl ?? isWsl()
  const exists = options.exists ?? fileExists

  if (platform === "win32") return [{ type: "powershell", command: "powershell" }]

  if (platform === "linux" && wsl) {
    const candidates = []
    if (exists(PS5_WSL)) candidates.push({ type: "powershell", command: PS5_WSL })
    candidates.push({ type: "powershell", command: "powershell.exe" })
    candidates.push({ type: "notify-send", command: "notify-send" })
    return candidates
  }

  if (platform === "linux") return [{ type: "notify-send", command: "notify-send" }]
  return []
}

function cliNotificationsDisabled() {
  const candidates = []
  if (process.env.XDG_CONFIG_HOME) candidates.push(path.join(process.env.XDG_CONFIG_HOME, "opencode", "cli.json"))
  candidates.push(path.join(os.homedir(), ".config", "opencode", "cli.json"))
  for (const file of candidates) {
    try {
      const config = JSON.parse(fs.readFileSync(file, "utf8"))
      return config?.attention?.notifications === false
    } catch {}
  }
  return false
}

function powerShellString(value) {
  return String(value ?? "").replace(/'/g, "''")
}

function toastScript(title, message) {
  return `
$ErrorActionPreference='Stop'
New-Item -Path 'HKCU:\\SOFTWARE\\Classes\\AppUserModelId\\opencode-notify' -Force -ErrorAction SilentlyContinue | Out-Null
New-ItemProperty -Path 'HKCU:\\SOFTWARE\\Classes\\AppUserModelId\\opencode-notify' -Name 'DisplayName' -Value 'OpenCode' -PropertyType String -Force -ErrorAction SilentlyContinue | Out-Null
New-ItemProperty -Path 'HKCU:\\SOFTWARE\\Classes\\AppUserModelId\\opencode-notify' -Name 'ShowInSettings' -Value 1 -PropertyType DWord -Force -ErrorAction SilentlyContinue | Out-Null
try {
  [Windows.UI.Notifications.ToastNotificationManager,Windows.UI.Notifications,ContentType=WindowsRuntime] > $null
  [Windows.Data.Xml.Dom.XmlDocument,Windows.Data.Xml.Dom.XmlDocument,ContentType=WindowsRuntime] > $null
  $toast=[Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent('ToastText02')
  $toast.SelectSingleNode('//text[@id="1"]').InnerText='${powerShellString(title)}'
  $toast.SelectSingleNode('//text[@id="2"]').InnerText='${powerShellString(message)}'
  [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('{1AC14E77-02E7-4E5D-B744-2EB1AE5198B7}\\WindowsPowerShell\\v1.0\\powershell.exe').Show($toast)
  exit 0
} catch {
  # PowerShell 7 无 WinRT 支持；非 0 退出让上层回退到 Windows PowerShell 5.1。
  exit 1
}
`.trim()
}

function toastArguments(candidate, title, message) {
  if (candidate.type === "powershell") {
    return ["-NoProfile", "-NonInteractive", "-Command", toastScript(title, message)]
  }
  return ["--app-name=OpenCode", title, message]
}

function tryCandidate(candidates, index, title, message) {
  if (index >= candidates.length) return
  const candidate = candidates[index]
  let settled = false
  const next = () => {
    if (settled) return
    settled = true
    tryCandidate(candidates, index + 1, title, message)
  }
  try {
    const child = spawn(candidate.command, toastArguments(candidate, title, message), { windowsHide: true, stdio: "ignore" })
    const timer = setTimeout(() => child.kill(), 15000)
    child.once("error", next)
    child.on("exit", (code) => {
      clearTimeout(timer)
      // 退出码非 0（例如 pwsh7 不支持 WinRT）时回退到下一个后端。
      if (code !== 0) next()
    })
  } catch {
    next()
  }
}

function showToast(title, message) {
  if (process.env.LAZY_NOTIFY === "0" || cliNotificationsDisabled()) return
  const candidates = resolveCandidates()
  if (candidates.length === 0) return
  tryCandidate(candidates, 0, title, message)
}

// 通知类型对应的文案前缀。
const LABELS = {
  done: "任务完成",
  error: "任务失败",
  permission: "需要确认",
  question: "需要回答",
  default: "OpenCode",
}

// notifier(title, message) 可注入，便于测试断言。
export function createPlugin(notifier = showToast) {
  return {
    id: "lazy-notify",

    async setup(ctx) {
      const location = normalizeDirectory(ctx.location?.directory)
      const project = ctx.location?.directory ? path.basename(ctx.location.directory) : ""
      const childSessions = new Set()
      const sessionDirs = new Map()

      const notify = (kind, detail) => {
        const suffix = project ? ` · ${project}` : ""
        const label = LABELS[kind] || LABELS.default
        notifier(`OpenCode${suffix}`, detail ? `${label}：${detail}` : label)
      }

      // 共享后台服务会为每个打开过的项目各加载一份插件实例，各自订阅全局事件流。
      // 这里只处理属于本插件 location 的会话，避免多实例重复弹通知。
      const belongsHere = (dir) => location !== null && dir !== null && dir === location
      const resolveDir = async (sessionID) => {
        const known = sessionDirs.get(sessionID)
        if (known !== undefined) return known
        try {
          const session = await ctx.session.get({ sessionID })
          const dir = normalizeDirectory(session?.location?.directory)
          if (dir) sessionDirs.set(sessionID, dir)
          return dir
        } catch {
          return null
        }
      }

      const handleEvent = async (event) => {
        const type = event?.type
        const data = event?.data || {}

        if (type === "session.created") {
          if (data.parentID) childSessions.add(data.sessionID)
          const dir = normalizeDirectory(data.location?.directory)
          if (dir) sessionDirs.set(data.sessionID, dir)
          return
        }
        if (type === "session.deleted") {
          childSessions.delete(data.sessionID)
          sessionDirs.delete(data.sessionID)
          return
        }
        if (type === "session.execution.interrupted") return

        const sessionID = type === "form.created" ? data.form?.sessionID : data.sessionID
        if (typeof sessionID !== "string") return

        const isChild = childSessions.has(sessionID)

        if (type === "permission.asked") {
          if (!belongsHere(await resolveDir(sessionID))) return
          notify("permission", data.action || "权限请求")
          return
        }

        if (type === "form.created") {
          if (!belongsHere(await resolveDir(sessionID))) return
          notify("question", data.form?.title || "需要你回答")
          return
        }

        if (type !== "session.execution.succeeded" && type !== "session.execution.failed") return

        // 子会话完成不通知。
        if (type === "session.execution.succeeded" && isChild) return

        // 已知属于其他项目则直接跳过，避免无谓的 session.get。
        const known = sessionDirs.get(sessionID)
        if (known !== undefined && !belongsHere(known)) return

        let title = ""
        let dir = known
        try {
          const session = await ctx.session.get({ sessionID })
          title = session?.title || ""
          dir = normalizeDirectory(session?.location?.directory) ?? dir
          if (dir) sessionDirs.set(sessionID, dir)
        } catch {}
        if (!belongsHere(dir)) return

        if (type === "session.execution.failed") {
          notify("error", data.error?.message || title || "执行失败")
          return
        }

        notify("done", title || "执行完成")
      }

      const controller = new AbortController()
      void (async () => {
        try {
          for await (const event of ctx.event.subscribe({ signal: controller.signal })) {
            await handleEvent(event)
          }
        } catch {}
      })()

      return () => controller.abort()
    },
  }
}

export default createPlugin()
