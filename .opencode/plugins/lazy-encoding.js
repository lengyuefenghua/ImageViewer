// Lazy Encoding：Windows 下为 shell 命令注入 UTF-8 编码，修复中文乱码。
//
// 背景：中文 Windows 的 ACP/OEMCP 是 936（GBK）。opencode 用 -NoProfile 启动
// PowerShell，[Console]::OutputEncoding 保持在 gb2312，而 opencode 按 UTF-8 解码
// stdout；cmd 子进程也不会继承父进程的代码页。这里在命令执行前按 shell 注入
// UTF-8 设置，并通过 env 给子进程补上 Python 的 UTF-8 环境变量。
// 参考：https://github.com/anomalyco/opencode/issues/23636

const SHELLS = {
  // [Console] 管 PowerShell 自身的输出/输入编码；chcp 管控制台代码页，让 native
  // 子进程（git / python 等）继承 65001；$OutputEncoding 管到 native 进程的管道编码。
  // 前缀以 [Console] 开头，供幂等判定锚定命令开头。
  pwsh: {
    prefix:
      "[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false); " +
      "[Console]::InputEncoding = [Console]::OutputEncoding; " +
      "$OutputEncoding = [Console]::OutputEncoding; " +
      "chcp 65001 > $null;",
    separator: "\n",
    marker: "[Console]::OutputEncoding",
  },
  cmd: {
    prefix: "chcp 65001>nul",
    separator: " & ",
    marker: "chcp 65001",
  },
  bash: {
    prefix: "export LC_ALL=C.UTF-8; export LANG=C.UTF-8;",
    separator: "\n",
    marker: "export LC_ALL=",
  },
}

// 从 shell 路径/名称归一化类型；未知 shell 返回 null，宁可不注入也不写坏 fish 之类的命令。
export function detectShellKind(shell) {
  if (typeof shell !== "string" || !shell) return null
  const base = shell
    .replace(/\\/g, "/")
    .split("/")
    .pop()
    .toLowerCase()
    .replace(/\.(exe|cmd|bat)$/, "")
  if (base === "pwsh" || base === "powershell") return "pwsh"
  if (base === "cmd") return "cmd"
  if (["bash", "sh", "zsh", "dash", "ksh"].includes(base)) return "bash"
  return null
}

// opencode 可能在命令前拼 `set VAR="value" &&`，注入要保留它、并在它之后追加前缀。
export function stripSetPrefixes(command) {
  const match = command.match(/^((?:set\s+\w+="[^"]*"\s*&&\s*)+)/i)
  if (!match) return { prefixes: "", rest: command }
  return { prefixes: match[1], rest: command.slice(match[1].length) }
}

// 返回注入后的命令；已注入（锚定命令开头）或未知 shell 时返回 undefined。
export function injectUtf8Prefix(command, kind) {
  const spec = SHELLS[kind]
  if (!spec) return undefined
  const { prefixes, rest } = stripSetPrefixes(command)
  // 只看命令开头：正文里提到 marker（如 echo "$LC_ALL"）不能误判为已注入。
  if (rest.startsWith(spec.marker)) return undefined
  return `${prefixes}${spec.prefix}${spec.separator}${rest}`
}

export default {
  id: "lazy-encoding",
  async setup(ctx) {
    if (process.platform !== "win32") return
    // 宿主没有 shell 钩子域时降级为 no-op，不影响会话。
    if (!ctx?.shell || typeof ctx.shell.hook !== "function") return

    const registration = await ctx.shell.hook("create.before", (event) => {
      if (!event) return
      // Python 在 Windows 上默认按 ACP 编码管道输出，env 层对三种 shell 一致生效。
      if (event.env) {
        event.env.PYTHONIOENCODING = "utf-8"
        event.env.PYTHONUTF8 = "1"
      }
      // 空命令（如 TUI 交互终端）只补 env，不动命令内容。
      if (typeof event.command !== "string" || event.command.trim() === "") return
      const kind = detectShellKind(event.shell)
      if (!kind) return
      const injected = injectUtf8Prefix(event.command, kind)
      if (injected !== undefined) event.command = injected
    })

    return async () => {
      await registration?.dispose?.()
    }
  },
}
