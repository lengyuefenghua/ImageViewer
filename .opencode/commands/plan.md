---
description: 根据已通过 grilling 的设计生成 tasks.md
---

使用 `task-plan` 技能。

输入：当前 active change 的产物。full 使用 `proposal.md`、`design.md`（如有）、`specs/**/spec.md`；patch 使用已确认的需求和变更状态。

输出：`docs/changes/<change>/tasks.md`。

任务要求：

- 主任务必须是 checkbox：`- [ ] 1.1 Task description`。
- 每个实现任务是贯穿相关层的 tracer-bullet 垂直切片，可独立验证；按依赖排列，有前置依赖标注 `（blocked by <编号>）`。
- 每个任务写清文件路径、做什么、验证方式。
- 行为变化任务包含 TDD 检查点。
- full 的实现任务默认 inline；需要并行或隔离上下文的任务标注由 `subagent-dev` 派发。
- **仅当变更涉及视觉界面**：读取**澄清阶段**确定的 `visualVerify`/`designDemo`（未确定则回到 `/clarify`），据此生成任务：
  1. `visualVerify=true` → 加视觉验证任务；`false` → 不加，且本变更不得使用 `opencode_computer_*`。
  2. `designDemo=true` → 加 demo 任务：产出 `docs/changes/<change>/design-demo.html`（单文件、内联 CSS/JS、示例数据、基本交互）并请用户验收。
  非界面变更不生成任何视觉/demo 任务。
- 完成前包含 review 和 finish 检查点。

完成后同步更新全局和 change 的 `state.json`：`phase = "planning"`、`planned = true`。
