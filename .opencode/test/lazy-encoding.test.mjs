import assert from "node:assert/strict"
import { spawnSync } from "node:child_process"
import test from "node:test"
import lazyEncoding, { detectShellKind, injectUtf8Prefix } from "../plugins/lazy-encoding.js"

const WINDOWS = process.platform === "win32"

function makeCtx() {
  const hooks = []
  const ctx = {
    shell: {
      async hook(name, callback) {
        hooks.push({ name, callback })
        return { dispose: async () => {} }
      },
    },
  }
  return { ctx, hooks }
}

// 临时改写 process.platform，让 win32 守卫可测；结束恢复原描述符。
async function withPlatform(value, callback) {
  const descriptor = Object.getOwnPropertyDescriptor(process, "platform")
  Object.defineProperty(process, "platform", { value, configurable: true })
  try {
    return await callback()
  } finally {
    Object.defineProperty(process, "platform", descriptor)
  }
}

test("detectShellKind 识别常见 shell 路径", () => {
  assert.equal(detectShellKind("C:\\Program Files\\PowerShell\\7\\pwsh.exe"), "pwsh")
  assert.equal(detectShellKind("C:\\Windows\\System32\\WindowsPowerShell\\v1.0\\powershell.exe"), "pwsh")
  assert.equal(detectShellKind("powershell"), "pwsh")
  assert.equal(detectShellKind("C:\\Windows\\System32\\cmd.exe"), "cmd")
  assert.equal(detectShellKind("/usr/bin/bash"), "bash")
  assert.equal(detectShellKind("zsh"), "bash")
  assert.equal(detectShellKind("fish"), null)
  assert.equal(detectShellKind(""), null)
  assert.equal(detectShellKind(undefined), null)
})

test("injectUtf8Prefix 为各 shell 注入 UTF-8 前缀", () => {
  const pwsh = injectUtf8Prefix("Write-Output '中文'", "pwsh")
  assert.ok(pwsh.startsWith("[Console]::OutputEncoding ="))
  assert.ok(pwsh.includes("chcp 65001 > $null;"))
  assert.ok(pwsh.endsWith("\nWrite-Output '中文'"))

  assert.equal(injectUtf8Prefix("echo 中文", "cmd"), "chcp 65001>nul & echo 中文")
  assert.equal(injectUtf8Prefix("echo 中文", "bash"), "export LC_ALL=C.UTF-8; export LANG=C.UTF-8;\necho 中文")
})

test("injectUtf8Prefix 幂等：已注入的命令不再套前缀", () => {
  assert.equal(injectUtf8Prefix(injectUtf8Prefix("echo 中文", "bash"), "bash"), undefined)
  assert.equal(injectUtf8Prefix(injectUtf8Prefix("echo 中文", "cmd"), "cmd"), undefined)
  assert.equal(injectUtf8Prefix(injectUtf8Prefix("Write-Output 1", "pwsh"), "pwsh"), undefined)
})

test("injectUtf8Prefix 保留 set 前缀，且正文提到 marker 不误判", () => {
  assert.equal(injectUtf8Prefix('set FOO="bar" && echo 中文', "cmd"), 'set FOO="bar" && chcp 65001>nul & echo 中文')
  assert.ok(injectUtf8Prefix('echo "$LC_ALL"', "bash").startsWith("export LC_ALL="))
})

test("setup 在 win32 注册 create.before 并改写命令与环境变量", async () => {
  await withPlatform("win32", async () => {
    const { ctx, hooks } = makeCtx()
    const cleanup = await lazyEncoding.setup(ctx)
    assert.equal(hooks.length, 1)
    assert.equal(hooks[0].name, "create.before")

    const event = { command: "echo 中文", cwd: "C:\\proj", timeout: 0, shell: "C:\\Windows\\System32\\cmd.exe", env: {} }
    hooks[0].callback(event)
    assert.equal(event.command, "chcp 65001>nul & echo 中文")
    assert.equal(event.env.PYTHONIOENCODING, "utf-8")
    assert.equal(event.env.PYTHONUTF8, "1")

    assert.equal(typeof cleanup, "function")
    await cleanup()
  })
})

test("空命令只补环境变量，不改写命令", async () => {
  await withPlatform("win32", async () => {
    const { ctx, hooks } = makeCtx()
    await lazyEncoding.setup(ctx)
    const event = { command: "   ", cwd: "C:\\proj", timeout: 0, shell: "pwsh.exe", env: {} }
    hooks[0].callback(event)
    assert.equal(event.command, "   ")
    assert.equal(event.env.PYTHONUTF8, "1")
  })
})

test("非 win32 不注册 hook，缺少 shell 域时降级为 no-op", async () => {
  await withPlatform("linux", async () => {
    const { ctx, hooks } = makeCtx()
    await lazyEncoding.setup(ctx)
    assert.equal(hooks.length, 0)
  })
  await withPlatform("win32", async () => {
    await assert.doesNotReject(lazyEncoding.setup({}))
  })
})

// 等价验证：真实 spawn PowerShell 5.1，模拟 opencode「读 stdout 按 UTF-8 解码」的管线。
test("注入后 PowerShell 输出按 UTF-8 解码正确", { skip: !WINDOWS }, () => {
  const command = injectUtf8Prefix("Write-Output '中文测试你好'", "pwsh")
  const result = spawnSync("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command], {
    stdio: ["ignore", "pipe", "pipe"],
  })
  assert.equal(result.status, 0)
  assert.equal(result.stderr.toString("utf8"), "")
  assert.equal(result.stdout.toString("utf8").trim(), "中文测试你好")
})

test("注入后 cmd 输出按 UTF-8 解码正确", { skip: !WINDOWS }, () => {
  const command = injectUtf8Prefix("echo 中文测试你好", "cmd")
  const result = spawnSync("cmd.exe", ["/d", "/s", "/c", command], { stdio: ["ignore", "pipe", "pipe"] })
  assert.equal(result.status, 0)
  assert.equal(result.stdout.toString("utf8").trim(), "中文测试你好")
})
