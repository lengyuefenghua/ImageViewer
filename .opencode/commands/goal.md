---
description: 按最终目标持续推进 Lazy Flow，直到完成、暂停或达到安全上限
---

处理最终目标：`$ARGUMENTS`。

1. 参数必须非空；为空时询问用户提供明确的最终目标。
2. 先通过 skill 工具加载 `using-lazy-flow`，读取 `.opencode/lazy/state.json`、当前 active change（如有）和 `tasks.md`。
3. 调用 `lazy_goal_set`，将 `$ARGUMENTS` 作为 goal。此命令视为用户对该最终目标和已确认 `tasks.md` 范围的一次性实施授权；授权不包含视觉验证与提交/推送。
4. 立刻开始当前 Lazy Flow 的下一步。不要在完成局部任务或一个 checkbox 后仅报告状态并停止：每个可验证步骤调用 `lazy_goal_progress`，由 Goal 引擎继续推进。
5. 继续遵守 `using-lazy-flow` 的分级、路由和全部纪律；Goal 不得绕过技能加载、验证、审查、收尾和权限门。
6. 权限门：未经用户明确许可不得调用 `lazy_computer_*`；人工验收通过前禁止提交；`git commit` 与 `git push` 均需用户明确许可。
7. 需求不清、需要用户决策、验证失败、范围冲突、审查有重要发现，或收尾需要用户确认同步/归档时，调用 `lazy_goal_pause` 并报告阻塞原因。
8. 最终目标满足（归档完成）且有可验证证据时，调用 `lazy_goal_mark_done`。
