---
description: 澄清 full 变更中会影响范围、行为、风险或验收方式的未知项
---

使用 `grill-me` 技能。

目标：只澄清会影响实现的未知项。需求明确时快速给出 `Passed`；不要为了走流程制造问题，也不要写实现代码。

第一步先统一核心术语（定义、范围、歧义点），术语确定后写入 `CONTEXT.md`，再澄清需求和边界。每轮结束时把本轮确认结论批量写入 `docs/changes/<change>/proposal.md` 的需求确认段（change id 由 `/start` 给定；未给定则推导一个并创建目录），不依赖上下文压缩后的记忆。

检查重点：业务目标/场景/流程/规则/异常/边界、范围和行为、数据或安全风险、YAGNI、验收方式。每轮结束重检核心需求/关键边界/相互矛盾，未知项清空即结束。涉及视觉界面时，先给**文字 UI 草图**并问用户是否使用视觉测试与验证（`visualVerify`），复杂界面再问是否产出 HTML demo（`designDemo`）；`visualVerify=true` 时再问是否需要视觉验收报告（`visualReport`）。结论记入全局 `.opencode/lazy/state.json`。

没有 active change 时只输出澄清结论；通过后由 `/start` 继续创建 full 产物。已有 full change 时，可审查已有产物并将全局和 change 的 `state.json` 更新为 `grilled = true`、`phase = "design"`。

如果不通过，输出必须修改的问题，不进入设计或规划。
