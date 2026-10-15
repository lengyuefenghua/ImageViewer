---
description: 根据已确认的协作卡与流程契约生成 tasks.md
---

使用 `task-plan` 技能生成 `docs/changes/<change>/tasks.md`；输入为 active change 的产物（full：`change.md` 的协作卡与流程契约 + specs；patch：已确认需求与状态）。

命令特有约束：

- full：协作卡与流程契约必须已经用户确认（停点 2）；未见确认记录时先呈现卡并等待，不生成 `tasks.md`。
- 界面变更：必须落一份可视设计稿（`designDemo=true` 用 `design-demo.html`，否则文字 UI 草图写入 `change.md`/`tasks.md`）；三开关以用户明确确认为准（未确认先问，未确认按 `false`），结论写入 change 的 `state.json`；tiny 纯样式微调豁免。
- 提交任务带 `[人工]` 标记：人工验收通过且用户明确许可后才执行，只 commit 不 push。
