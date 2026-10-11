import { spawn } from "node:child_process"
import { readFileSync, rmSync } from "node:fs"
import path from "node:path"
import { fileURLToPath } from "node:url"

// Lazy Computer：Windows 自动化操作工具（OpenCode V2 工具 API）。
// 工具名统一为 lazy_computer_*，由 setup 通过 ctx.tool.transform 注册。
// 后端是常驻 PowerShell 宿主 + 内嵌 C#（P/Invoke / UIA / GDI+），零 npm 依赖。
// 宿主脚本沿用 .opencode/tools/computer/，本插件只负责工具定义与宿主客户端。

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const hostScript = path.join(__dirname, "..", "tools", "computer", "lazy-computer-host.ps1")

// 常驻 PowerShell 宿主客户端：JSON 行协议、串行队列、崩溃自动重启。
class PsHost {
  constructor() {
    this.proc = null
    this.buffer = ""
    this.pending = new Map()
    this.nextId = 1
    this.ready = null
    this.queue = Promise.resolve()
  }

  ensure() {
    if (this.ready) return this.ready
    this.ready = new Promise((resolve, reject) => {
      const proc = spawn(
        "powershell",
        ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Sta", "-File", hostScript],
        { stdio: ["pipe", "pipe", "pipe"], windowsHide: true },
      )
      this.proc = proc
      proc.stdout.setEncoding("utf8")
      let settled = false
      proc.stdout.on("data", (chunk) => {
        this.buffer += chunk
        let index = this.buffer.indexOf("\n")
        while (index >= 0) {
          const line = this.buffer.slice(0, index).replace(/\r$/, "")
          this.buffer = this.buffer.slice(index + 1)
          if (line.trim()) this.dispatch(line, () => { settled = true; resolve() })
          index = this.buffer.indexOf("\n")
        }
      })
      proc.stderr.setEncoding("utf8")
      proc.stderr.on("data", () => {})
      proc.on("error", (error) => {
        if (!settled) { settled = true; reject(error) }
        this.failAll(error)
      })
      proc.on("exit", () => {
        this.proc = null
        this.ready = null
        const error = new Error("PowerShell 宿主已退出")
        this.failAll(error)
        if (!settled) { settled = true; reject(error) }
      })
    })
    return this.ready
  }

  dispatch(line, onReady) {
    let message
    try {
      message = JSON.parse(line)
    } catch {
      return
    }
    if (message.ready) { onReady(); return }
    if (typeof message.id === "number" && this.pending.has(message.id)) {
      const handler = this.pending.get(message.id)
      this.pending.delete(message.id)
      if (message.ok) handler.resolve(message.result === undefined ? null : message.result)
      else handler.reject(new Error(message.error || "宿主返回未知错误"))
    }
  }

  failAll(error) {
    for (const handler of this.pending.values()) handler.reject(error)
    this.pending.clear()
  }

  // 停止宿主并释放管道，供测试收尾时让进程自然退出。
  stop() {
    const proc = this.proc
    this.proc = null
    this.ready = null
    this.failAll(new Error("PowerShell 宿主已停止"))
    if (proc) {
      try {
        proc.kill()
      } catch {}
    }
  }

  call(action, params) {
    const run = async () => {
      await this.ensure()
      const proc = this.proc
      if (!proc) throw new Error("PowerShell 宿主未启动")
      const id = this.nextId++
      return await new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
          this.pending.delete(id)
          reject(new Error(`宿主调用超时: ${action}`))
        }, 30000)
        this.pending.set(id, {
          resolve: (value) => { clearTimeout(timer); resolve(value) },
          reject: (error) => { clearTimeout(timer); reject(error) },
        })
        proc.stdin.write(JSON.stringify({ id, action, params }) + "\n")
      })
    }
    // 串行队列：任何一次调用失败都不阻塞后续调用。
    const result = this.queue.then(run, run)
    this.queue = result.catch(() => {})
    return result
  }
}

const host = new PsHost()

function asArray(value) {
  if (Array.isArray(value)) return value
  if (value === null || value === undefined) return []
  return [value]
}

// 把截图临时文件转成 V2 工具附件（file content，uri 为内联 data URL）。
function imageAttachment(filePath, keep = false) {
  const base64 = readFileSync(filePath).toString("base64")
  if (!keep) {
    try {
      rmSync(filePath, { force: true })
    } catch {}
  }
  return {
    type: "file",
    uri: `data:image/jpeg;base64,${base64}`,
    mime: "image/jpeg",
    name: path.basename(filePath),
  }
}

function resolveShotPath(worktree, args) {
  if (args.path) {
    return path.isAbsolute(args.path) ? args.path : path.join(worktree, args.path)
  }
  if (!args.save) return null
  const stamp = new Date().toISOString().replace(/[:.]/g, "-")
  const name = `${stamp}-${args.label || "shot"}.jpg`
  let change = null
  try {
    const state = JSON.parse(readFileSync(path.join(worktree, ".opencode", "lazy", "state.json"), "utf8"))
    change = state.activeChange ?? null
  } catch {}
  if (change) return path.join(worktree, "docs", "changes", change, "evidence", name)
  return path.join(worktree, ".opencode", "lazy", "evidence", name)
}

function describeWindow(info) {
  if (!info) return ""
  return `窗口: ${info.title} [${info.process}] hwnd=${info.hwnd} rect=(${info.x},${info.y},${info.width}x${info.height}) client=${info.clientWidth}x${info.clientHeight}`
}

// 目标定位参数：可复用到多数工具。
const targetProps = {
  hwnd: { type: "integer", description: "窗口句柄，优先于其它定位参数" },
  process: { type: "string", description: "进程名，如 notepad、msedge" },
  title: { type: "string", description: "窗口标题子串" },
  class: { type: "string", description: "窗口类名子串" },
  pid: { type: "integer", description: "进程 PID" },
}

const regionProps = {
  type: "object",
  properties: {
    x: { type: "number" },
    y: { type: "number" },
    width: { type: "number" },
    height: { type: "number" },
  },
  required: ["x", "y", "width", "height"],
  additionalProperties: false,
}

// 批量 do 的单步 schema：所有 op 的参数字段取并集。
const stepProperties = {
  op: { type: "string", description: "focus/screenshot/type/key/mouse/scroll/window/clipboard/wait/windows/resolve/postmessage/controls/control/wait_for/verify" },
  hwnd: { type: "integer" },
  process: { type: "string" },
  title: { type: "string" },
  class: { type: "string" },
  class_name: { type: "string" },
  pid: { type: "integer" },
  id: { type: "string" },
  index: { type: "integer" },
  value: { type: "string" },
  max: { type: "integer" },
  condition: { type: "string", enum: ["window", "control", "file"] },
  check: { type: "string", enum: ["window", "file", "value", "title"] },
  expected: { type: "string" },
  path: { type: "string" },
  timeout_ms: { type: "integer" },
  interval_ms: { type: "integer" },
  verify: { type: "boolean" },
  x: { type: "number" },
  y: { type: "number" },
  to_x: { type: "number" },
  to_y: { type: "number" },
  width: { type: "number" },
  height: { type: "number" },
  region: regionProps,
  display: { type: "integer" },
  space: { type: "string", enum: ["client", "window", "screen"] },
  client: { type: "boolean" },
  keep_restored: { type: "boolean" },
  save: { type: "boolean" },
  label: { type: "string" },
  mode: { type: "string", enum: ["auto", "printwindow", "screen", "foreground", "postmessage"] },
  action: { type: "string" },
  button: { type: "string", enum: ["left", "right", "middle"] },
  count: { type: "integer" },
  direction: { type: "string", enum: ["up", "down"] },
  amount: { type: "number" },
  combo: { type: "string" },
  text: { type: "string" },
  duration_ms: { type: "integer" },
  message: { anyOf: [{ type: "string" }, { type: "number" }] },
  wparam: { type: "number" },
  lparam: { type: "number" },
}

async function runStep(step, worktree) {
  const op = String(step.op).toLowerCase()
  if (op === "wait") {
    const ms = step.duration_ms ?? 500
    await new Promise((resolve) => setTimeout(resolve, ms))
    return { text: `等待 ${ms}ms` }
  }
  if (op === "screenshot") {
    const outPath = resolveShotPath(worktree, step)
    const result = await host.call("screenshot", { ...step, ...(outPath ? { out_path: outPath } : {}) })
    const saved = Boolean(outPath)
    return {
      text: saved ? `截图 ${result.width}x${result.height} mode=${result.mode} 已保存: ${result.path}` : `截图 ${result.width}x${result.height} mode=${result.mode}（已内联 JPEG）`,
      attachment: imageAttachment(result.path, saved),
    }
  }
  if (op === "focus") return { text: await host.call("window", { ...step, action: "focus" }) }
  if (op === "window") return { text: await host.call("window", step) }
  if (op === "type") return { text: await host.call("key", step) }
  if (op === "key") return { text: await host.call("key", step) }
  if (op === "mouse") return { text: await host.call("mouse", step) }
  if (op === "scroll") return { text: await host.call("mouse", { ...step, action: "scroll" }) }
  if (op === "clipboard") return { text: await host.call("clipboard", step) }
  if (op === "windows") return { text: JSON.stringify(await host.call("windows", step)) }
  if (op === "resolve") return { text: JSON.stringify(await host.call("resolve", step)) }
  if (op === "postmessage") return { text: await host.call("postmessage", step) }
  if (op === "controls") return { text: JSON.stringify(await host.call("controls", step)) }
  if (op === "control") return { text: await host.call("control", step) }
  if (op === "wait_for") return { text: JSON.stringify(await host.call("wait_for", step)) }
  if (op === "verify") return { text: JSON.stringify(await host.call("verify", step)) }
  throw new Error(`未知 op: ${step.op}`)
}

// V2 工具定义（JSON Schema）。execute 返回 Tool.Result：{ content }。
function toolDefinitions(worktree) {
  return [
    {
      name: "lazy_computer_windows",
      description: "列出可见顶层窗口（hwnd、标题、类名、pid、进程、位置尺寸、是否前台/最小化）。可用 query 过滤；多数操作可直接用 process/title 定位，无需先调用本工具。",
      input: {
        type: "object",
        properties: {
          query: { type: "string", description: "按标题/类名/进程名子串过滤" },
          include_hidden: { type: "boolean", description: "包含隐藏窗口" },
        },
        additionalProperties: false,
      },
      async execute(args) {
        const list = asArray(await host.call("windows", args))
        return { content: JSON.stringify(list, null, 2) }
      },
    },
    {
      name: "lazy_computer_displays",
      description: "列出显示器（id、主屏、位置尺寸）与 DPI 缩放。截图 display 参数即用这里的 id。",
      input: { type: "object", properties: {}, additionalProperties: false },
      async execute() {
        return { content: JSON.stringify(await host.call("displays", {}), null, 2) }
      },
    },
    {
      name: "lazy_computer_screenshot",
      description: "截取窗口/窗口内区域/显示器，返回 JPEG 图片。支持后台截图：用 PrintWindow 捕获被遮挡的窗口，不抢焦点；窗口最小化时会自动静默还原→截图→再最小化（不抢焦点），可用 keep_restored=true 保持还原状态。用 process/title/hwnd 直接定位窗口；client=true 或 space=client 截客户区；region 相对客户区(默认)或窗口。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          display: { type: "integer", description: "显示器 id（不给窗口时截该显示器）" },
          region: regionProps,
          space: { type: "string", enum: ["client", "window", "screen"], description: "区域坐标系，默认 client" },
          client: { type: "boolean", description: "截客户区而非整窗" },
          mode: { type: "string", enum: ["auto", "printwindow", "screen"], description: "auto=后台 PrintWindow 优先，失败才回退前台" },
          keep_restored: { type: "boolean", description: "目标原本最小化时，截图后保持还原而不重新最小化" },
          save: { type: "boolean", description: "保存截图到项目（默认 docs/changes/<change>/evidence/，无 active change 时 .opencode/lazy/evidence/）" },
          path: { type: "string", description: "显式保存路径（相对项目根），隐含保存" },
          label: { type: "string", description: "保存文件名标签" },
        },
        additionalProperties: false,
      },
      async execute(args) {
        const outPath = resolveShotPath(worktree, args)
        const callArgs = { ...args, ...(outPath ? { out_path: outPath } : {}) }
        const result = await host.call("screenshot", callArgs)
        const saved = Boolean(outPath)
        const header = saved
          ? `截图成功 ${result.width}x${result.height} mode=${result.mode} scale=${result.scale}\n已保存: ${result.path}`
          : `截图成功 ${result.width}x${result.height} mode=${result.mode} scale=${result.scale}（已内联 JPEG，临时文件未保留）`
        const windowLine = describeWindow(result.window)
        return { content: [{ type: "text", text: windowLine ? `${header}\n${windowLine}` : header }, imageAttachment(result.path, saved)] }
      },
    },
    {
      name: "lazy_computer_window",
      description: "窗口管理：focus/restore/minimize/maximize/close/move/resize。用 process/title/hwnd 定位。focus 会真正激活窗口（AttachThreadInput + SetForegroundWindow + SwitchToThisWindow），可用于后续前台键鼠。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          action: { type: "string", enum: ["focus", "restore", "minimize", "maximize", "close", "move", "resize"] },
          x: { type: "number", description: "move 的 X" },
          y: { type: "number", description: "move 的 Y" },
          width: { type: "number", description: "resize 宽度" },
          height: { type: "number", description: "resize 高度" },
        },
        required: ["action"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("window", args) }
      },
    },
    {
      name: "lazy_computer_mouse",
      description: "鼠标操作：move/click/down/up/drag/scroll。mode=postmessage(后台发消息，不抢焦点) 或 foreground(会真正激活目标窗口后真实鼠标，可点快捷键场景)。有窗口时坐标默认相对客户区，可用 space 切换；未指定 mode 时：有窗口走 postmessage，否则 foreground。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          mode: { type: "string", enum: ["foreground", "postmessage"] },
          action: { type: "string", enum: ["move", "click", "down", "up", "drag", "scroll"] },
          x: { type: "number" },
          y: { type: "number" },
          to_x: { type: "number", description: "drag 终点 X" },
          to_y: { type: "number", description: "drag 终点 Y" },
          button: { type: "string", enum: ["left", "right", "middle"] },
          count: { type: "integer", description: "click 次数" },
          direction: { type: "string", enum: ["up", "down"], description: "scroll 方向" },
          amount: { type: "number", description: "scroll 格数" },
          space: { type: "string", enum: ["client", "screen"] },
        },
        required: ["action"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("mouse", args) }
      },
    },
    {
      name: "lazy_computer_key",
      description: "键盘操作：combo 组合键(如 ctrl+shift+s) 或 text 文本。mode=postmessage(后台，自动定向到输入控件，不抢焦点) 或 foreground(会真正激活目标窗口后 SendInput，可发快捷键)。有窗口时用 process/title/hwnd 定位；未指定 mode 时：有窗口走 postmessage，否则 foreground。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          mode: { type: "string", enum: ["foreground", "postmessage"] },
          action: { type: "string", enum: ["press", "down", "up"] },
          combo: { type: "string", description: "组合键，如 ctrl+c" },
          text: { type: "string", description: "要输入的文本" },
          duration_ms: { type: "integer", description: "按住时长" },
        },
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("key", args) }
      },
    },
    {
      name: "lazy_computer_clipboard",
      description: "剪贴板读写：action=get 读取，action=set 写入 text。",
      input: {
        type: "object",
        properties: {
          action: { type: "string", enum: ["get", "set"] },
          text: { type: "string" },
        },
        required: ["action"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("clipboard", args) }
      },
    },
    {
      name: "lazy_computer_postmessage",
      description: "向窗口发送原始 PostMessage（高级）。message 可用名称(WM_CLOSE/WM_KEYDOWN/WM_CHAR/WM_LBUTTONDOWN 等)或数值；wparam/lparam 为整数。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          message: { anyOf: [{ type: "string" }, { type: "number" }] },
          wparam: { type: "number" },
          lparam: { type: "number" },
        },
        required: ["message"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("postmessage", args) }
      },
    },
    {
      name: "lazy_computer_controls",
      description: "列出窗口的 UI Automation 控件树（type/name/id/class/hwnd/rect/enabled）。用于定位按钮、输入框、菜单项等控件，再用 lazy_computer_control 操作。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          max: { type: "integer", description: "最多返回多少个控件，默认 200" },
        },
        additionalProperties: false,
      },
      async execute(args) {
        const list = asArray(await host.call("controls", args))
        return { content: JSON.stringify(list, null, 2) }
      },
    },
    {
      name: "lazy_computer_control",
      description: "操作指定控件（UI Automation，后台可靠不抢焦点）：按 name/id/class_name/type/index 定位，action 取 invoke/click/setvalue/getvalue/focus/toggle/select/expand/collapse/info。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          name: { type: "string", description: "控件名称" },
          id: { type: "string", description: "AutomationId" },
          class_name: { type: "string", description: "控件类名" },
          type: { type: "string", description: "控件类型，如 button/edit/document/checkbox/menuitem/tabitem" },
          index: { type: "integer", description: "多个匹配时的序号，默认 0" },
          action: { type: "string", enum: ["invoke", "click", "setvalue", "getvalue", "focus", "toggle", "select", "expand", "collapse", "info"] },
          value: { type: "string", description: "setvalue 要写入的值" },
        },
        required: ["action"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: await host.call("control", args) }
      },
    },
    {
      name: "lazy_computer_wait_for",
      description: "等待条件满足：condition=window(窗口出现)/control(控件出现)/file(文件存在)。带 timeout_ms（默认 10000）与 interval_ms（默认 250）。返回 {found, elapsed_ms, detail}。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          condition: { type: "string", enum: ["window", "control", "file"] },
          name: { type: "string" },
          id: { type: "string" },
          class_name: { type: "string" },
          type: { type: "string" },
          index: { type: "integer" },
          path: { type: "string" },
          timeout_ms: { type: "integer" },
          interval_ms: { type: "integer" },
        },
        required: ["condition"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: JSON.stringify(await host.call("wait_for", args), null, 2) }
      },
    },
    {
      name: "lazy_computer_verify",
      description: "操作后校验：check=window(窗口存在)/file(文件存在)/value(控件值匹配)/title(窗口标题匹配)。返回 {ok, actual, expected}。key/control setvalue 默认已自带校验，此工具用于独立确认。",
      input: {
        type: "object",
        properties: {
          ...targetProps,
          check: { type: "string", enum: ["window", "file", "value", "title"] },
          name: { type: "string" },
          id: { type: "string" },
          class_name: { type: "string" },
          type: { type: "string" },
          index: { type: "integer" },
          path: { type: "string" },
          expected: { type: "string" },
          mode: { type: "string", enum: ["contains", "equals"] },
        },
        required: ["check"],
        additionalProperties: false,
      },
      async execute(args) {
        return { content: JSON.stringify(await host.call("verify", args), null, 2) }
      },
    },
    {
      name: "lazy_computer_do",
      description: "一次调用按顺序执行多步电脑操作，减少交互次数。每步 {op, ...}：op 为 focus/screenshot/type/key/mouse/scroll/window/clipboard/wait/wait_for/verify/windows/resolve/postmessage/controls/control；screenshot 步骤返回图片附件。默认出错继续，stop_on_error=true 则中断。",
      input: {
        type: "object",
        properties: {
          steps: {
            type: "array",
            minItems: 1,
            items: {
              type: "object",
              properties: stepProperties,
              required: ["op"],
              additionalProperties: false,
            },
          },
          stop_on_error: { type: "boolean" },
        },
        required: ["steps"],
        additionalProperties: false,
      },
      async execute(args) {
        const lines = []
        const attachments = []
        for (let index = 0; index < args.steps.length; index++) {
          const step = args.steps[index]
          try {
            const result = await runStep(step, worktree)
            lines.push(`#${index + 1} ${step.op}: ${result.text}`)
            if (result.attachment) attachments.push(result.attachment)
          } catch (error) {
            lines.push(`#${index + 1} ${step.op}: ERROR ${error?.message ?? error}`)
            if (args.stop_on_error) break
          }
        }
        return { content: [{ type: "text", text: lines.join("\n") }, ...attachments] }
      },
    },
  ]
}

export default {
  id: "lazy-computer",
  async setup(ctx) {
    // 工具执行上下文不含 worktree；在 setup 时捕获本项目目录，供截图落盘路径使用。
    const worktree = ctx.location?.directory || process.cwd()
    await ctx.tool.transform((editor) => {
      for (const definition of toolDefinitions(worktree)) editor.add(definition)
    })
  },
}

// 仅测试用：暴露宿主实例以便收尾时释放 PowerShell 子进程。
export { host }
