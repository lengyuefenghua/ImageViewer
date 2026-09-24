import assert from "node:assert/strict"
import test, { after } from "node:test"
import * as computer from "../tools/opencode_computer.ts"

after(() => process.exit(0))

test("computer tools are exported", () => {
  const names = ["windows", "displays", "screenshot", "window", "mouse", "key", "clipboard", "postmessage", "controls", "control", "wait_for", "verify", "do"]
  for (const name of names) assert.ok(computer[name], `missing tool: ${name}`)
})

test("displays returns screens", async () => {
  const result = JSON.parse(await computer.displays.execute({}, {}))
  assert.ok(Array.isArray(result.screens))
  assert.ok(result.screens.length > 0)
})

test("windows with no match returns an empty array", async () => {
  const result = JSON.parse(await computer.windows.execute({ query: "zzz-no-such-window-xyz" }, {}))
  assert.ok(Array.isArray(result))
  assert.equal(result.length, 0)
})

test("windows with a single match stays an array", async () => {
  const result = JSON.parse(await computer.windows.execute({ query: "Program Manager" }, {}))
  assert.ok(Array.isArray(result))
  assert.ok(result.length >= 1)
})

test("wait_for file found and timeout", async () => {
  const found = JSON.parse(await computer.wait_for.execute({ condition: "file", path: "C:\\Windows\\System32\\notepad.exe", timeout_ms: 3000 }, {}))
  assert.equal(found.found, true)
  const missing = JSON.parse(await computer.wait_for.execute({ condition: "file", path: "C:\\nope\\nope.txt", timeout_ms: 500 }, {}))
  assert.equal(missing.found, false)
})

test("verify file exists and missing", async () => {
  const ok = JSON.parse(await computer.verify.execute({ check: "file", path: "C:\\Windows\\System32\\notepad.exe" }, {}))
  assert.equal(ok.ok, true)
  const no = JSON.parse(await computer.verify.execute({ check: "file", path: "C:\\nope\\nope.txt" }, {}))
  assert.equal(no.ok, false)
})

test("key text and control setvalue auto-verify on notepad", async () => {
  const { spawn, execSync } = await import("node:child_process")
  const kill = () => { try { execSync("taskkill /IM notepad.exe /F", { stdio: "ignore" }) } catch {} }
  kill()
  spawn("notepad", [], { detached: true, stdio: "ignore" }).unref()
  try {
    const wf = JSON.parse(await computer.wait_for.execute({ condition: "window", process: "notepad", timeout_ms: 8000 }, {}))
    assert.equal(wf.found, true)
    const list = JSON.parse(await computer.windows.execute({ query: "notepad" }, {}))
    assert.ok(Array.isArray(list) && list.length >= 1)
    const hwnd = list[0].hwnd

    const typed = await computer.key.execute({ hwnd, text: "auto-verify" }, {})
    assert.match(typed, /\[verified\]/)

    const set = await computer.control.execute({ hwnd, type: "document", action: "setvalue", value: "set-verify" }, {})
    assert.match(set, /\[verified\]/)
  } finally {
    kill()
  }
})

test("screenshot saves to path when requested", async () => {
  const os = await import("node:os")
  const path = await import("node:path")
  const fs = await import("node:fs")
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "cu-evidence-"))
  try {
    const rel = "docs/changes/demo/evidence/shot.jpg"
    const result = await computer.screenshot.execute({ display: 0, path: rel }, { worktree: root })
    const saved = path.join(root, rel)
    assert.ok(fs.existsSync(saved), "saved file should exist")
    assert.match(result.output, /已保存/)
    assert.ok(result.attachments[0].url.startsWith("data:image/jpeg;base64,"))
  } finally {
    fs.rmSync(root, { recursive: true, force: true })
  }
})

test("screenshot does not persist by default", async () => {
  const os = await import("node:os")
  const path = await import("node:path")
  const fs = await import("node:fs")
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "cu-default-"))
  try {
    const result = await computer.screenshot.execute({ display: 0 }, { worktree: root })
    assert.ok(!fs.existsSync(path.join(root, "docs")), "no project files should be created by default")
    assert.ok(!fs.existsSync(path.join(root, ".opencode")), "no lazy evidence should be created by default")
    assert.ok(result.attachments[0].url.startsWith("data:image/jpeg;base64,"))
  } finally {
    fs.rmSync(root, { recursive: true, force: true })
  }
})
