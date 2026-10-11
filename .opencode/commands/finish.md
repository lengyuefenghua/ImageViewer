---
description: 验证、审查、同步规格并归档当前 change
---

使用 `finish-change` 技能收尾 active change；验证门、自审、delta 同步、归档和提交的完整规则以该技能为准。

命令特有约束：

- `tasks.md` 除提交任务外未全部完成时，不得归档。
- 界面变更：仅当用户明确许可 `visualVerify=true` 时有截图证据要求；未获许可禁止调用 `lazy_computer_*`。
- **提交门**：人工验收通过前禁止提交；`git commit` 与 `git push` 都必须获得用户明确许可（commit 许可不等于 push 许可）。提交任务在用户验收并许可后执行，只 commit 不 push。
- 不归档 failing、未审查或用户未确认的工作。
