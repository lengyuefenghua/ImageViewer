---
description: 根据已通过 grilling 的设计生成 tasks.md
---

使用 `task-plan` 技能生成 `docs/changes/<change>/tasks.md`；输入为 active change 的产物（full：proposal/design/specs；patch：已确认需求与状态）。

命令特有约束：

- 界面变更的三开关以用户明确确认为准（未确认先问，未确认按 `false`）；结论写入 change 的 `state.json`。
- 提交任务带 `[人工]` 标记：人工验收通过且用户明确许可后才执行，只 commit 不 push。
