---
description: 澄清 full 变更中会影响范围、行为、风险或验收方式的未知项
---

使用 `grill-me` 技能澄清 `$ARGUMENTS`；提问、维度与结束条件以技能为准。

命令特有约束：

- change id 由 `/start` 给定；未给定则推导 kebab-case id 并创建目录。
- 结论批量写入 `docs/changes/<change>/change.md` 的 `## 需求卡` 段，术语写入 `CONTEXT.md`；结束时只呈现需求卡并停下等用户确认（停点 1）。
- 界面变更：**必须给可视设计稿**（文字 UI 草图是底线；复杂界面再问是否升级为 `designDemo` HTML demo，不给 HTML 也必须给草图），并问 `visualVerify`/`visualReport`；未获用户明确许可禁止调用 `lazy_computer_*`；tiny 纯样式微调豁免。
- 没有 active change 时只输出澄清结论；确认后由 `spec-design` 继续产出协作卡与流程契约。
- 不通过时列出必须修改的问题，不进入设计或规划。
