---
description: 审查当前 change 或指定固定点以来的 diff
---

使用 `review-change` 技能审查 `$ARGUMENTS`：full 双轴完整审查；patch 轻量审查（4 条清单）。

1. 当前存在 active change 时，读取该 change 的 tasks、proposal、specs 和 design（如有）。
2. 用户提供固定点、分支、tag 或 commit 时，按 `review-change` 的固定点规则审查 `git diff <fixed-point>...HEAD`。
3. 用户未提供固定点且没有 active change 时，询问要审查的固定点或规格来源。
4. full 分开输出 Spec 轴和 Standards 轴的发现，并给出 `Passed` 或 `Needs changes`；patch 输出轻量审查结论。
5. 此命令只审查和报告；除非用户明确要求修复，否则不修改代码、任务或状态。
