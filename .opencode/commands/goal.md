---
description: 按最终目标持续推进 Lazy Flow，直到完成、暂停或达到安全上限
---

处理最终目标：`$ARGUMENTS`。

1. 参数必须非空；为空时询问用户提供明确的最终目标。
2. 先通过 skill 工具加载 `using-lazy-flow`，读取 `.opencode/lazy/state.json`、当前 active change（如有）和 `tasks.md`。
3. 调用 `lazy_goal_set`，将 `$ARGUMENTS` 作为 goal。此命令视为用户对该最终目标和已确认 `tasks.md` 范围的一次性实施授权。
4. 立刻开始当前 Lazy Flow 的下一步。不要在完成局部任务或一个 checkbox 后仅报告状态并停止：每个可验证步骤调用 `lazy_goal_progress`，由 Goal 引擎继续推进。
5. full 实现默认 inline，任务可并行或需隔离上下文时通过 `subagent-dev` 派发；行为任务必须使用 `tdd-cycle`。Goal 不得绕过分级、规格、TDD、SDD、验证、审查或收尾规则。
6. 需求不清、需要用户决策、验证失败、范围冲突、审查有重要发现，或收尾需要用户确认同步/归档时，调用 `lazy_goal_pause` 并报告阻塞原因。
7. 最终目标满足且有可验证证据时，调用 `lazy_goal_mark_done`。
