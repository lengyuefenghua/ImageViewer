import assert from "node:assert/strict"
import test, { after } from "node:test"
import os from "node:os"
import path from "node:path"
import fs from "node:fs"
import lazyComputer, { host } from "../plugins/lazy-computer.js"

// 收尾时关闭常驻 PowerShell 宿主，让测试进程自然退出。
after(() => host.stop())

// mock OpenCode V2 插件上下文，暴露 setup 注册的工具供断言。
function makeCtx(root) {
  const tools = new Map()
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
        return { dispose: async () => {} }
      },
    },
  }
  return { ctx, tools }
}

async function setupTools(root) {
  const harness = makeCtx(root)
  await lazyComputer.setup(harness.ctx)
  return harness.tools
}

function textContent(result) {
  if (typeof result.content === "string") return result.content
  return result.content.find((part) => part.type === "text")?.text ?? ""
}

function fileContent(result) {
  return result.content.find((part) => part.type === "file")
}

test("lazy computer tools are registered", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const names = [
    "lazy_computer_windows",
    "lazy_computer_displays",
    "lazy_computer_screenshot",
    "lazy_computer_window",
    "lazy_computer_mouse",
    "lazy_computer_key",
    "lazy_computer_clipboard",
    "lazy_computer_postmessage",
    "lazy_computer_controls",
    "lazy_computer_control",
    "lazy_computer_wait_for",
    "lazy_computer_verify",
    "lazy_computer_do",
  ]
  for (const name of names) assert.ok(tools.has(name), `missing tool: ${name}`)
})

test("displays returns screens", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const result = JSON.parse(textContent(await tools.get("lazy_computer_displays").execute({}, {})))
  assert.ok(Array.isArray(result.screens))
  assert.ok(result.screens.length > 0)
})

test("windows with no match returns an empty array", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const result = JSON.parse(textContent(await tools.get("lazy_computer_windows").execute({ query: "zzz-no-such-window-xyz" }, {})))
  assert.ok(Array.isArray(result))
  assert.equal(result.length, 0)
})

test("windows with a single match stays an array", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const result = JSON.parse(textContent(await tools.get("lazy_computer_windows").execute({ query: "Program Manager" }, {})))
  assert.ok(Array.isArray(result))
  assert.ok(result.length >= 1)
})

test("wait_for file found and timeout", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const found = JSON.parse(textContent(await tools.get("lazy_computer_wait_for").execute({ condition: "file", path: "C:\\Windows\\System32\\notepad.exe", timeout_ms: 3000 }, {})))
  assert.equal(found.found, true)
  const missing = JSON.parse(textContent(await tools.get("lazy_computer_wait_for").execute({ condition: "file", path: "C:\\nope\\nope.txt", timeout_ms: 500 }, {})))
  assert.equal(missing.found, false)
})

test("verify file exists and missing", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const ok = JSON.parse(textContent(await tools.get("lazy_computer_verify").execute({ check: "file", path: "C:\\Windows\\System32\\notepad.exe" }, {})))
  assert.equal(ok.ok, true)
  const no = JSON.parse(textContent(await tools.get("lazy_computer_verify").execute({ check: "file", path: "C:\\nope\\nope.txt" }, {})))
  assert.equal(no.ok, false)
})

test("key text and control setvalue auto-verify on notepad", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const { spawn, execSync } = await import("node:child_process")
  const kill = () => { try { execSync("taskkill /IM notepad.exe /F", { stdio: "ignore" }) } catch {} }
  kill()
  spawn("notepad", [], { detached: true, stdio: "ignore" }).unref()
  try {
    const wf = JSON.parse(textContent(await tools.get("lazy_computer_wait_for").execute({ condition: "window", process: "notepad", timeout_ms: 8000 }, {})))
    assert.equal(wf.found, true)
    const list = JSON.parse(textContent(await tools.get("lazy_computer_windows").execute({ query: "notepad" }, {})))
    assert.ok(Array.isArray(list) && list.length >= 1)
    const hwnd = list[0].hwnd

    const typed = textContent(await tools.get("lazy_computer_key").execute({ hwnd, text: "auto-verify" }, {}))
    assert.match(typed, /\[verified\]/)

    const set = textContent(await tools.get("lazy_computer_control").execute({ hwnd, type: "document", action: "setvalue", value: "set-verify" }, {}))
    assert.match(set, /\[verified\]/)
  } finally {
    kill()
  }
})

test("screenshot saves to path when requested", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const rel = "docs/changes/demo/evidence/shot.jpg"
  const result = await tools.get("lazy_computer_screenshot").execute({ display: 0, path: rel }, {})
  const saved = path.join(root, rel)
  assert.ok(fs.existsSync(saved), "saved file should exist")
  assert.match(textContent(result), /已保存/)
  assert.ok(fileContent(result).uri.startsWith("data:image/jpeg;base64,"))
})

test("screenshot does not persist by default", async () => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "lazy-computer-"))
  const tools = await setupTools(root)
  const result = await tools.get("lazy_computer_screenshot").execute({ display: 0 }, {})
  assert.ok(!fs.existsSync(path.join(root, "docs")), "no project files should be created by default")
  assert.ok(!fs.existsSync(path.join(root, ".opencode")), "no lazy evidence should be created by default")
  assert.ok(fileContent(result).uri.startsWith("data:image/jpeg;base64,"))
})
