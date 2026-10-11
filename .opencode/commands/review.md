---
description: 审查当前 change 或指定固定点以来的 diff
---

使用 `review-change` 技能审查 `$ARGUMENTS`：full 双轴完整审查；patch 轻量审查；tiny 不审查。

命令特有约束：

- 用户提供固定点、分支、tag 或 commit 时，按 `git diff <fixed-point>...HEAD` 审查；未提供且无 active change 时先问固定点或规格来源。
- 此命令只审查和报告；除非用户明确要求修复，否则不修改代码、任务或状态。
- 审查给出 `Passed` 且存在 active change 时，由技能把 `reviewed` 置为 `true`。
