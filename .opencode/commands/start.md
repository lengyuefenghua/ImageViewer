---
description: 开始一个变更：按 tiny、patch 或 full 分级并进入对应工作流
---

先加载 `using-lazy-flow` 分级，再处理 `$ARGUMENTS`。

要求：

1. tiny：说明理由并直接最小修改，不创建 change；完成后在 `docs/changes/tiny-log.md` 追加三行记录（标题/改动/验证）。
2. patch：创建 `docs/changes/<change>/tasks.md` 和 `state.json`，同时设置全局 `activeChange`、`level = "patch"`、`phase = "planning"`；然后停止，建议 `/apply`。
3. full：先加载 `grill-me`，先统一核心术语（写入 `CONTEXT.md`）再澄清影响范围、行为、风险或验收的未知项，结论落盘到 `docs/changes/$ARGUMENTS/proposal.md` 的需求确认段；通过后加载 `spec-design`。
4. full 的 `spec-design` 在需求确认段基础上补齐 `proposal.md` 其余章节、`specs/<capability>/spec.md`、按需 `design.md` 和 `state.json`，同时设置全局 `activeChange`、`level = "full"`、`phase = "design"`。
5. full 产物完成后停止，建议 `/plan`。

所有产物直接创建在 change 目录中。
