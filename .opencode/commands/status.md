---
description: 查看 Lazy Flow 当前状态和恢复建议
---

读取以下文件并输出状态摘要：

1. `.opencode/lazy/state.json`
2. `docs/changes/*/state.json`
3. 活跃 change 的 `tasks.md` checkbox 状态
4. 活跃 change 的 `proposal.md`、`design.md`、`specs/**/spec.md` 是否存在
5. 全局状态与 active change 的 `level`、`phase` 是否一致
6. `.opencode/lazy/goal-state.json` 是否存在，以及其 goal、status、turns、最近进展和暂停原因
7. 该变更是否已提交（`git log` 中是否存在对应 change 的提交）

输出格式：

```text
Lazy Flow Status
- Initialized: yes/no
- Active change: <name or none>
- Level: tiny/patch/full
- Phase: idle/design/planning/implementing/finishing
- Lean: lite/full/ultra/off
- Artifacts: proposal/specs/design/tasks
- Tasks: done/total
- Next command: <suggestion>
- Lazy Goal: active/paused/completed/none
- Committed: yes/no
```

如果发现多个活跃 change，列出来并要求用户选择。不要擅自合并状态。

如果全局 `activeChange`、`level` 或 `phase` 与 active change 的状态不一致，明确报告不一致字段，并建议先修复状态再继续执行。
