---
description: 执行当前 active change 的 tasks.md
---

使用 `execute-change` 技能。

根据变更层级选择：

- patch：inline TDD。
- full：默认 inline 实现；任务相互独立且不触碰相同文件、或需要隔离上下文时，加载 `subagent-dev` 派发。完成前确认 `tasks.md` 无未完成项，全部完成才收尾。

执行要求：

1. 行为变化不要跳过 RED test。
2. 先确认 RED 因预期原因失败，再写生产代码；派发子代理时由子代理完成此循环。
3. 每个任务完成后更新 `tasks.md` checkbox。
4. 派发的子代理返回后，主控读取 diff、独立重跑验证，再更新 `tasks.md` 和两个 `state.json`。
5. full 实现完成后进入 review/finish；完整实现自审由收尾门统一执行（见 `finish-change`）。
6. 界面任务：勾选前加载 `ui-visual-verify`，用自动化操作工具 `opencode_computer_*` 实际操作应用并截取改动后的真实界面、观察截图对照 `specs/**/spec.md`；不符则修正重截，直到符合。
7. 不做 tasks.md 外的顺手优化或重构。
