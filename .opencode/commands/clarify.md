---
description: 澄清 full 变更中会影响范围、行为、风险或验收方式的未知项
---

使用 `grill-me` 技能澄清 `$ARGUMENTS`；提问、维度与结束条件以技能为准。

命令特有约束：

- change id 由 `/start` 给定；未给定则推导 kebab-case id 并创建目录。
- 结论批量写入 `docs/changes/<change>/proposal.md` 的需求确认段，术语写入 `CONTEXT.md`。
- 界面变更：先给文字 UI 草图并问三开关（`visualVerify`/`visualReport`/`designDemo`）；未获用户明确许可禁止调用 `lazy_computer_*`。
- 没有 active change 时只输出澄清结论；通过后建议 `/start` 创建 full 产物。
- 不通过时列出必须修改的问题，不进入设计或规划。
