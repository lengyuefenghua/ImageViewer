import assert from "node:assert/strict"
import test from "node:test"

import lazyNotify, { createPlugin, resolveCandidates } from "../plugins/lazy-notify.js"

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

const sleep = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds))
const DEMO = "C:\\proj\\demo"
const OTHER = "C:\\proj\\other"

function harness() {
  const stream = eventStream()
  const calls = []
  const sessionGets = []
  const ctx = {
    location: { directory: DEMO },
    session: {
      async get({ sessionID }) {
        sessionGets.push(sessionID)
        return { id: sessionID, title: `会话-${sessionID}`, location: { directory: sessionID.startsWith("foreign") ? OTHER : DEMO } }
      },
    },
    event: { subscribe: stream.subscribe },
  }
  const plugin = createPlugin((title, message) => calls.push({ title, message }))
  return { stream, calls, sessionGets, ctx, plugin }
}

const created = (sessionID, directory, parentID) => ({ type: "session.created", data: { sessionID, parentID, location: { directory } } })

test("lazy-notify exports a V2 definition", () => {
  assert.equal(lazyNotify.id, "lazy-notify")
  assert.equal(typeof lazyNotify.setup, "function")
})

test("lazy-notify maps notifications, skips child completion, and ignores other locations", async () => {
  const { stream, calls, sessionGets, ctx, plugin } = harness()
  const cleanup = await plugin.setup(ctx)

  stream.push(created("parent-1", DEMO))
  stream.push(created("child-1", DEMO, "parent-1"))
  stream.push(created("foreign-1", OTHER))
  stream.push({ type: "session.execution.succeeded", data: { sessionID: "parent-1" } })
  stream.push({ type: "session.execution.succeeded", data: { sessionID: "child-1" } })
  stream.push({ type: "session.execution.succeeded", data: { sessionID: "foreign-1" } })
  stream.push({ type: "session.execution.failed", data: { sessionID: "parent-1", error: { message: "boom" } } })
  stream.push({ type: "permission.asked", data: { sessionID: "parent-1", action: "bash" } })
  stream.push({ type: "form.created", data: { form: { sessionID: "parent-1", title: "继续吗" } } })
  stream.push({ type: "permission.asked", data: { sessionID: "child-1", action: "edit" } })
  stream.push({ type: "form.created", data: { form: { sessionID: "child-1", title: "子问题" } } })
  await sleep(100)

  assert.deepEqual(calls, [
    { title: "OpenCode · demo", message: "任务完成：会话-parent-1" },
    { title: "OpenCode · demo", message: "任务失败：boom" },
    { title: "OpenCode · demo", message: "需要确认：bash" },
    { title: "OpenCode · demo", message: "需要回答：继续吗" },
    { title: "OpenCode · demo", message: "需要确认：edit" },
    { title: "OpenCode · demo", message: "需要回答：子问题" },
  ])
  assert.deepEqual(sessionGets, ["parent-1", "parent-1"])

  await cleanup()
  stream.finish()
})

test("notifications for unknown sessions are resolved and filtered by their location", async () => {
  const { stream, calls, ctx, plugin } = harness()
  const cleanup = await plugin.setup(ctx)

  // 未知会话 -> 通过 session.get 解析 location
  stream.push({ type: "form.created", data: { form: { sessionID: "parent-9", title: "是否继续" } } })
  stream.push({ type: "form.created", data: { form: { sessionID: "foreign-9", title: "别的项目" } } })
  stream.push({ type: "session.execution.succeeded", data: { sessionID: "foreign-9" } })
  await sleep(80)

  assert.deepEqual(calls, [{ title: "OpenCode · demo", message: "需要回答：是否继续" }])

  await cleanup()
  stream.finish()
})

test("resolveCandidates uses Windows PowerShell 5.1 and falls back to notify-send on WSL", () => {
  const none = () => false
  const only = (set) => (candidate) => set.has(candidate)

  assert.deepEqual(resolveCandidates({ platform: "win32" }), [{ type: "powershell", command: "powershell" }])

  const ps5 = "/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe"
  assert.deepEqual(resolveCandidates({ platform: "linux", wsl: true, exists: only(new Set([ps5])) }), [
    { type: "powershell", command: ps5 },
    { type: "powershell", command: "powershell.exe" },
    { type: "notify-send", command: "notify-send" },
  ])

  assert.deepEqual(resolveCandidates({ platform: "linux", wsl: true, exists: none }), [
    { type: "powershell", command: "powershell.exe" },
    { type: "notify-send", command: "notify-send" },
  ])

  assert.deepEqual(resolveCandidates({ platform: "linux", wsl: false, exists: none }), [
    { type: "notify-send", command: "notify-send" },
  ])
})
