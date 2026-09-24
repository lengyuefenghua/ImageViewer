import { tool } from "@opencode-ai/plugin"
import { spawn, type ChildProcessWithoutNullStreams } from "node:child_process"
import { readFileSync, rmSync } from "node:fs"
import path from "node:path"
import { fileURLToPath } from "node:url"

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const hostScript = path.join(__dirname, "computer", "opencode-computer-host.ps1")

class PsHost {
  private proc: ChildProcessWithoutNullStreams | null = null
  private buffer = ""
  private pending = new Map<number, { resolve: (value: any) => void; reject: (error: Error) => void }>()
  private nextId = 1
  private ready: Promise<void> | null = null
  private queue: Promise<unknown> = Promise.resolve()

  private ensure(): Promise<void> {
    if (this.ready) return this.ready
    this.ready = new Promise<void>((resolve, reject) => {
      const proc = spawn(
        "powershell",
        ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-Sta", "-File", hostScript],
        { stdio: ["pipe", "pipe", "pipe"], windowsHide: true },
      )
      this.proc = proc
      proc.stdout.setEncoding("utf8")
      let settled = false
      proc.stdout.on("data", (chunk: string) => {
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

  private dispatch(line: string, onReady: () => void) {
    let message: any
    try {
      message = JSON.parse(line)
    } catch {
      return
    }
    if (message.ready) { onReady(); return }
    if (typeof message.id === "number" && this.pending.has(message.id)) {
      const handler = this.pending.get(message.id)!
      this.pending.delete(message.id)
      if (message.ok) handler.resolve(message.result === undefined ? null : message.result)
      else handler.reject(new Error(message.error || "宿主返回未知错误"))
    }
  }

  private failAll(error: Error) {
    for (const handler of this.pending.values()) handler.reject(error)
    this.pending.clear()
  }

  call(action: string, params: Record<string, any>): Promise<any> {
    const run = async () => {
      await this.ensure()
      const proc = this.proc
      if (!proc) throw new Error("PowerShell 宿主未启动")
      const id = this.nextId++
      return await new Promise<any>((resolve, reject) => {
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
    const result = this.queue.then(run, run)
    this.queue = result.catch(() => {})
    return result
  }
}

const host = new PsHost()

function asArray(value: unknown): unknown[] {
  if (Array.isArray(value)) return value
  if (value === null || value === undefined) return []
  return [value]
}

function imageAttachment(filePath: string, keep = false) {
  const base64 = readFileSync(filePath).toString("base64")
  if (!keep) {
    try {
      rmSync(filePath, { force: true })
    } catch {}
  }
  return {
    type: "file" as const,
    mime: "image/jpeg",
    url: `data:image/jpeg;base64,${base64}`,
    filename: path.basename(filePath),
  }
}

function resolveShotPath(worktree: string, args: { path?: string; save?: boolean; label?: string }): string | null {
  if (args.path) {
    return path.isAbsolute(args.path) ? args.path : path.join(worktree, args.path)
  }
  if (!args.save) return null
  const stamp = new Date().toISOString().replace(/[:.]/g, "-")
  const name = `${stamp}-${args.label || "shot"}.jpg`
  let change: string | null = null
  try {
    const state = JSON.parse(readFileSync(path.join(worktree, ".opencode", "lazy", "state.json"), "utf8"))
    change = state.activeChange ?? null
  } catch {}
  if (change) return path.join(worktree, "docs", "changes", change, "evidence", name)
  return path.join(worktree, ".opencode", "lazy", "evidence", name)
}

function describeWindow(info: any): string {
  if (!info) return ""
  return `窗口: ${info.title} [${info.process}] hwnd=${info.hwnd} rect=(${info.x},${info.y},${info.width}x${info.height}) client=${info.clientWidth}x${info.clientHeight}`
}

const targetArgs = {
  hwnd: tool.schema.number().int().optional().describe("窗口句柄，优先于其它定位参数"),
  process: tool.schema.string().optional().describe("进程名，如 notepad、msedge"),
  title: tool.schema.string().optional().describe("窗口标题子串"),
  class: tool.schema.string().optional().describe("窗口类名子串"),
  pid: tool.schema.number().int().optional().describe("进程 PID"),
}

export const windows = tool({
  description: "列出可见顶层窗口（hwnd、标题、类名、pid、进程、位置尺寸、是否前台/最小化）。可用 query 过滤；多数操作可直接用 process/title 定位，无需先调用本工具。",
  args: {
    query: tool.schema.string().optional().describe("按标题/类名/进程名子串过滤"),
    include_hidden: tool.schema.boolean().optional().describe("包含隐藏窗口"),
  },
  async execute(args) {
    const list = asArray(await host.call("windows", args))
    return JSON.stringify(list, null, 2)
  },
})

export const displays = tool({
  description: "列出显示器（id、主屏、位置尺寸）与 DPI 缩放。截图 display 参数即用这里的 id。",
  args: {},
  async execute() {
    const info = await host.call("displays", {})
    return JSON.stringify(info, null, 2)
  },
})

export const screenshot = tool({
  description: "截取窗口/窗口内区域/显示器，返回 JPEG 图片。支持后台截图：用 PrintWindow 捕获被遮挡的窗口，不抢焦点；窗口最小化时会自动静默还原→截图→再最小化（不抢焦点），可用 keep_restored=true 保持还原状态。用 process/title/hwnd 直接定位窗口；client=true 或 space=client 截客户区；region 相对客户区(默认)或窗口。",
  args: {
    ...targetArgs,
    display: tool.schema.number().int().optional().describe("显示器 id（不给窗口时截该显示器）"),
    region: tool.schema.object({
      x: tool.schema.number(),
      y: tool.schema.number(),
      width: tool.schema.number(),
      height: tool.schema.number(),
    }).optional().describe("裁剪区域"),
    space: tool.schema.enum(["client", "window", "screen"]).optional().describe("区域坐标系，默认 client"),
    client: tool.schema.boolean().optional().describe("截客户区而非整窗"),
    mode: tool.schema.enum(["auto", "printwindow", "screen"]).optional().describe("auto=后台 PrintWindow 优先，失败才回退前台"),
    keep_restored: tool.schema.boolean().optional().describe("目标原本最小化时，截图后保持还原而不重新最小化"),
    save: tool.schema.boolean().optional().describe("保存截图到项目（默认 docs/changes/<change>/evidence/，无 active change 时 .opencode/lazy/evidence/）"),
    path: tool.schema.string().optional().describe("显式保存路径（相对项目根），隐含保存"),
    label: tool.schema.string().optional().describe("保存文件名标签"),
  },
  async execute(args, context) {
    const outPath = resolveShotPath(context.worktree, args)
    const callArgs = { ...args, ...(outPath ? { out_path: outPath } : {}) }
    const result = await host.call("screenshot", callArgs)
    const saved = Boolean(outPath)
    const header = saved
      ? `截图成功 ${result.width}x${result.height} mode=${result.mode} scale=${result.scale}\n已保存: ${result.path}`
      : `截图成功 ${result.width}x${result.height} mode=${result.mode} scale=${result.scale}（已内联 JPEG，临时文件未保留）`
    const windowLine = describeWindow(result.window)
    return {
      output: windowLine ? `${header}\n${windowLine}` : header,
      attachments: [imageAttachment(result.path, saved)],
    }
  },
})

export const window = tool({
  description: "窗口管理：focus/restore/minimize/maximize/close/move/resize。用 process/title/hwnd 定位。focus 会真正激活窗口（AttachThreadInput + SetForegroundWindow + SwitchToThisWindow），可用于后续前台键鼠。",
  args: {
    ...targetArgs,
    action: tool.schema.enum(["focus", "restore", "minimize", "maximize", "close", "move", "resize"]),
    x: tool.schema.number().optional().describe("move 的 X"),
    y: tool.schema.number().optional().describe("move 的 Y"),
    width: tool.schema.number().optional().describe("resize 宽度"),
    height: tool.schema.number().optional().describe("resize 高度"),
  },
  async execute(args) {
    return await host.call("window", args)
  },
})

export const mouse = tool({
  description: "鼠标操作：move/click/down/up/drag/scroll。mode=postmessage(后台发消息，不抢焦点) 或 foreground(会真正激活目标窗口后真实鼠标，可点快捷键场景)。有窗口时坐标默认相对客户区，可用 space 切换；未指定 mode 时：有窗口走 postmessage，否则 foreground。",
  args: {
    ...targetArgs,
    mode: tool.schema.enum(["foreground", "postmessage"]).optional(),
    action: tool.schema.enum(["move", "click", "down", "up", "drag", "scroll"]),
    x: tool.schema.number().optional(),
    y: tool.schema.number().optional(),
    to_x: tool.schema.number().optional().describe("drag 终点 X"),
    to_y: tool.schema.number().optional().describe("drag 终点 Y"),
    button: tool.schema.enum(["left", "right", "middle"]).optional(),
    count: tool.schema.number().int().optional().describe("click 次数"),
    direction: tool.schema.enum(["up", "down"]).optional().describe("scroll 方向"),
    amount: tool.schema.number().optional().describe("scroll 格数"),
    space: tool.schema.enum(["client", "screen"]).optional(),
  },
  async execute(args) {
    return await host.call("mouse", args)
  },
})

export const key = tool({
  description: "键盘操作：combo 组合键(如 ctrl+shift+s) 或 text 文本。mode=postmessage(后台，自动定向到输入控件，不抢焦点) 或 foreground(会真正激活目标窗口后 SendInput，可发快捷键)。有窗口时用 process/title/hwnd 定位；未指定 mode 时：有窗口走 postmessage，否则 foreground。",
  args: {
    ...targetArgs,
    mode: tool.schema.enum(["foreground", "postmessage"]).optional(),
    action: tool.schema.enum(["press", "down", "up"]).optional(),
    combo: tool.schema.string().optional().describe("组合键，如 ctrl+c"),
    text: tool.schema.string().optional().describe("要输入的文本"),
    duration_ms: tool.schema.number().int().optional().describe("按住时长"),
  },
  async execute(args) {
    return await host.call("key", args)
  },
})

export const clipboard = tool({
  description: "剪贴板读写：action=get 读取，action=set 写入 text。",
  args: {
    action: tool.schema.enum(["get", "set"]),
    text: tool.schema.string().optional(),
  },
  async execute(args) {
    return await host.call("clipboard", args)
  },
})

export const postmessage = tool({
  description: "向窗口发送原始 PostMessage（高级）。message 可用名称(WM_CLOSE/WM_KEYDOWN/WM_CHAR/WM_LBUTTONDOWN 等)或数值；wparam/lparam 为整数。",
  args: {
    ...targetArgs,
    message: tool.schema.union([tool.schema.string(), tool.schema.number()]),
    wparam: tool.schema.number().optional(),
    lparam: tool.schema.number().optional(),
  },
  async execute(args) {
    return await host.call("postmessage", args)
  },
})

export const controls = tool({
  description: "列出窗口的 UI Automation 控件树（type/name/id/class/hwnd/rect/enabled）。用于定位按钮、输入框、菜单项等控件，再用 computer_control 操作。",
  args: {
    ...targetArgs,
    max: tool.schema.number().int().optional().describe("最多返回多少个控件，默认 200"),
  },
  async execute(args) {
    const list = asArray(await host.call("controls", args))
    return JSON.stringify(list, null, 2)
  },
})

export const control = tool({
  description: "操作指定控件（UI Automation，后台可靠不抢焦点）：按 name/id/class_name/type/index 定位，action 取 invoke/click/setvalue/getvalue/focus/toggle/select/expand/collapse/info。",
  args: {
    ...targetArgs,
    name: tool.schema.string().optional().describe("控件名称"),
    id: tool.schema.string().optional().describe("AutomationId"),
    class_name: tool.schema.string().optional().describe("控件类名"),
    type: tool.schema.string().optional().describe("控件类型，如 button/edit/document/checkbox/menuitem/tabitem"),
    index: tool.schema.number().int().optional().describe("多个匹配时的序号，默认 0"),
    action: tool.schema.enum(["invoke", "click", "setvalue", "getvalue", "focus", "toggle", "select", "expand", "collapse", "info"]),
    value: tool.schema.string().optional().describe("setvalue 要写入的值"),
  },
  async execute(args) {
    return await host.call("control", args)
  },
})

export const wait_for = tool({
  description: "等待条件满足：condition=window(窗口出现)/control(控件出现)/file(文件存在)。带 timeout_ms（默认 10000）与 interval_ms（默认 250）。返回 {found, elapsed_ms, detail}。",
  args: {
    ...targetArgs,
    condition: tool.schema.enum(["window", "control", "file"]),
    name: tool.schema.string().optional(),
    id: tool.schema.string().optional(),
    class_name: tool.schema.string().optional(),
    type: tool.schema.string().optional(),
    index: tool.schema.number().int().optional(),
    path: tool.schema.string().optional(),
    timeout_ms: tool.schema.number().int().optional(),
    interval_ms: tool.schema.number().int().optional(),
  },
  async execute(args) {
    const result = await host.call("wait_for", args)
    return JSON.stringify(result, null, 2)
  },
})

export const verify = tool({
  description: "操作后校验：check=window(窗口存在)/file(文件存在)/value(控件值匹配)/title(窗口标题匹配)。返回 {ok, actual, expected}。key/control setvalue 默认已自带校验，此工具用于独立确认。",
  args: {
    ...targetArgs,
    check: tool.schema.enum(["window", "file", "value", "title"]),
    name: tool.schema.string().optional(),
    id: tool.schema.string().optional(),
    class_name: tool.schema.string().optional(),
    type: tool.schema.string().optional(),
    index: tool.schema.number().int().optional(),
    path: tool.schema.string().optional(),
    expected: tool.schema.string().optional(),
    mode: tool.schema.enum(["contains", "equals"]).optional(),
  },
  async execute(args) {
    const result = await host.call("verify", args)
    return JSON.stringify(result, null, 2)
  },
})

const stepSchema = tool.schema.object({
  op: tool.schema.string().describe("focus/screenshot/type/key/mouse/scroll/window/clipboard/wait/windows/resolve/postmessage"),
  hwnd: tool.schema.number().int().optional(),
  process: tool.schema.string().optional(),
  title: tool.schema.string().optional(),
  class: tool.schema.string().optional(),
  class_name: tool.schema.string().optional(),
  pid: tool.schema.number().int().optional(),
  id: tool.schema.string().optional(),
  index: tool.schema.number().int().optional(),
  value: tool.schema.string().optional(),
  max: tool.schema.number().int().optional(),
  condition: tool.schema.enum(["window", "control", "file"]).optional(),
  check: tool.schema.enum(["window", "file", "value", "title"]).optional(),
  expected: tool.schema.string().optional(),
  path: tool.schema.string().optional(),
  timeout_ms: tool.schema.number().int().optional(),
  interval_ms: tool.schema.number().int().optional(),
  verify: tool.schema.boolean().optional(),
  x: tool.schema.number().optional(),
  y: tool.schema.number().optional(),
  to_x: tool.schema.number().optional(),
  to_y: tool.schema.number().optional(),
  width: tool.schema.number().optional(),
  height: tool.schema.number().optional(),
  region: tool.schema.object({
    x: tool.schema.number(),
    y: tool.schema.number(),
    width: tool.schema.number(),
    height: tool.schema.number(),
  }).optional(),
  display: tool.schema.number().int().optional(),
  space: tool.schema.enum(["client", "window", "screen"]).optional(),
  client: tool.schema.boolean().optional(),
  keep_restored: tool.schema.boolean().optional(),
  save: tool.schema.boolean().optional(),
  path: tool.schema.string().optional(),
  label: tool.schema.string().optional(),
  mode: tool.schema.enum(["auto", "printwindow", "screen", "foreground", "postmessage"]).optional(),
  action: tool.schema.string().optional(),
  button: tool.schema.enum(["left", "right", "middle"]).optional(),
  count: tool.schema.number().int().optional(),
  direction: tool.schema.enum(["up", "down"]).optional(),
  amount: tool.schema.number().optional(),
  combo: tool.schema.string().optional(),
  text: tool.schema.string().optional(),
  duration_ms: tool.schema.number().int().optional(),
  message: tool.schema.union([tool.schema.string(), tool.schema.number()]).optional(),
  wparam: tool.schema.number().optional(),
  lparam: tool.schema.number().optional(),
})

async function runStep(step: Record<string, any>, worktree: string): Promise<{ text: string; attachment?: ReturnType<typeof imageAttachment> }> {
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

const batch = tool({
  description: "一次调用按顺序执行多步电脑操作，减少交互次数。每步 {op, ...}：op 为 focus/screenshot/type/key/mouse/scroll/window/clipboard/wait/wait_for/verify/windows/resolve/postmessage/controls/control；screenshot 步骤返回图片附件。默认出错继续，stop_on_error=true 则中断。",
  args: {
    steps: tool.schema.array(stepSchema).min(1),
    stop_on_error: tool.schema.boolean().optional(),
  },
  async execute(args, context) {
    const lines: string[] = []
    const attachments: ReturnType<typeof imageAttachment>[] = []
    for (let index = 0; index < args.steps.length; index++) {
      const step = args.steps[index]
      try {
        const result = await runStep(step, context.worktree)
        lines.push(`#${index + 1} ${step.op}: ${result.text}`)
        if (result.attachment) attachments.push(result.attachment)
      } catch (error: any) {
        lines.push(`#${index + 1} ${step.op}: ERROR ${error?.message ?? error}`)
        if (args.stop_on_error) break
      }
    }
    return { output: lines.join("\n"), attachments }
  },
})

export { batch as "do" }
