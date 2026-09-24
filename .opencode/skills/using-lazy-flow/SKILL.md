---
name: using-lazy-flow
description: 新增、修改、修复、重构、调试、测试、审查、设计或依赖选型请求开始时使用：先分级 tiny/patch/full，再路由到对应的 Lazy Flow 技能。
license: MIT
---

# 使用 Lazy Flow

任何开发请求开始时先分级，再行动。技能名称和摘要不能替代技能正文：完成分级后，进入每项后续纪律前必须调用 skill 工具加载对应技能。

规格、变更、任务、归档历史用 `docs/` 下的本地 markdown 文件管理。

技能已由 OpenCode 注册；不需要通过文件搜索寻找技能或 Lean 文件。

存在 active Lazy Goal（`.opencode/lazy/goal-state.json` 的 `status = "active"`）时，读取其最终目标和当前进展。完成局部步骤后必须记录 `lazy_goal_progress`，由 Goal 继续推进；只有最终目标满足且有验证证据时才能调用 `lazy_goal_mark_done`。Goal 不得绕过分级、技能加载、TDD、SDD、验证、审查、归档或用户决策门。**不要自行调用 `lazy_goal_set` 为 full 变更启动 Goal；Goal 模式只由用户显式执行 `/goal` 触发。**

full 变更**不自动续跑**：不要因完成局部任务就停止。`tasks.md` 仍有未完成 checkbox 就继续；全部完成且有新鲜验证证据才进入收尾。若收到空闲核对提示，按同一标准自查。归档后执行 `tasks.md` 中的提交任务（`git commit`，中文说明，只 commit 不 push，不用工具代提交）。

full/patch 变更**完成前必须自审**（由 `finish-change` 收尾门统一执行）：逐条对照 `specs/**/spec.md` 的 requirement/scenario 与 `tasks.md`，确认完整实现（不是最小实现/偷懒）、无遗漏；有缺口先修复再重审。tiny 不做审查。

所有级别（tiny/patch/full）都必须遵守仓库 `AGENTS.md` 的日志与注释规范：日志是所有行为变更的交付物——必须评估并列出日志点（事件 + 级别 + 上下文），涉及外部边界/异常降级重试/状态转换/用户可见结果的必须落地，确实不适用的写明原因，用级别控制输出内容；关键处（非显然意图/不变量/边界/反直觉分支/公共契约）有中文注释。缺失视为缺陷，full/patch 审查按 Important 处理。豁免：很小的一次性验证/临时项目按项目整体豁免，需一句话说明。

界面/前端/GUI 变更（页面、组件、样式、布局、响应式、窗口、对话框、自绘/画布）：在**澄清阶段**给出**文字 UI 草图**（布局/区块/组件/交互要点；简单界面即文字草图，复杂界面可另出单文件 HTML demo），并问用户是否使用视觉测试与验证（`visualVerify`）；复杂界面再问是否产出 HTML 设计效果 demo（`designDemo`）。`visualVerify=false` 时本变更**不得使用** `opencode_computer_*`；`designDemo=true` 时产出单文件 `docs/changes/<change>/design-demo.html`（内联 CSS/JS、示例数据、基本交互）；`visualVerify=true` 时加载 `ui-visual-verify`，用自动化操作工具 `opencode_computer_*` 实际操作应用并观察改动后的真实界面截图，闭环比对规格直到实际渲染符合要求。没有真实截图证据，不得声明完成、提交或归档。

## 分级

### tiny

适用于文案、注释、配置值、样式、小 bug 修复、局部优化和低风险小调整。可以跨文件（如同时改几处文案/样式/配置值），但不得改变跨文件的接口/契约；改动小到能一眼审完、根因明确。根因不明或需要系统诊断的 bug 走 `debug-fix` 加 patch/full。

做最小安全修改，不创建 change 产物、不做审查。验证：有测试跑聚焦测试；改动涉及**重要代码**（关键路径：I/O/网络/DB/文件、错误/降级/重试、状态转换、启动关闭；公共契约；数据处理）时**必须**做聚焦测试或等价验证，不得跳过。

完成后在 `docs/changes/tiny-log.md` 追加三行记录（标题/改动/验证）。

### patch

适用于单功能/子系统内的功能开发、需要完整 TDD 循环的行为变更、较大 bug 修复、单模块重构与单模块鉴权/安全/数据模型调整。跨 UI/逻辑/测试层的垂直切片是常态，不算跨模块。

默认只写执行清单 `docs/changes/<change>/tasks.md` 和 `state.json`；`note.md` 默认不写，用户要求恢复记录时才写。

创建轻量 change 后进入 `planning`：`{"level":"patch","phase":"planning","planned":true,"reviewed":false,"archived":false}`。同时写入 `.opencode/lazy/state.json` 的 `activeChange`、`level` 和 `phase`。

优先 TDD。范围保持紧凑。除非行为不清晰、或外部可见程度高到需要契约，否则不要创建完整 proposal/spec/design。完成后走轻量审查（见 `review-change`）。

### full

仅当命中以下任一硬条件才使用（其他情况尽量走 patch，保持轻快）：

- **共享契约变更**：多个功能/子系统共用的接口、数据结构、文件格式、协议要改，且会迫使其他模块跟着改。
- **架构级重组**：模块边界或组织方式变化。
- **数据/格式迁移**：需兼容或转换既有数据。
- **高不确定性**：读代码和文档后仍有关键未知项，且需多轮用户决策才能定方向。

不确定时**从轻判 patch**；开工后发现真跨共享契约，允许升级到 full；不允许为保险直接判 full。

不算 full 的反例：改多个文件 ≠ 跨模块；垂直切片 ≠ 跨模块；能问清楚 ≠ 需求不清晰；有技术选择 ≠ 有设计取舍。

流程：

```text
grill-me -> spec-design -> task-plan -> execute-change -> review-change -> finish-change
```

完整产物：

```text
docs/changes/<change>/
├── proposal.md (含需求确认段，grill-me 落盘)
├── design.md (仅有真实技术取舍时)
├── specs/<capability>/spec.md
├── tasks.md
└── state.json
```

full 变更的实现默认由主控 inline 完成。任务独立且可并行、或需要隔离上下文时，通过 `subagent-dev` 派发子代理；派发后由主控读取 diff、独立验证、审查和收尾，被委托的子代理直接完成单一任务，不再继续派发。

## 路由

- `tiny`：实现、修复、优化或依赖选型时，若 Lean 模式不是 `off`，加载 `lean-check`；不创建 change、不做审查；按 tiny 的验证要求做聚焦验证，完成后追加 `docs/changes/tiny-log.md` 三行记录。
- `patch`：先加载 `task-plan`，再加载 `execute-change`；行为任务在实现前加载 `tdd-cycle`；完成后加载 `review-change`（轻量审查）和 `finish-change`。
- `full`：先加载 `grill-me`（先统一核心术语写入 `CONTEXT.md`，需求确认结论落盘 `proposal.md` 的 `## 需求确认` 段），通过后加载 `spec-design`、`task-plan`、`execute-change`；行为任务加载 `tdd-cycle`；任务可并行或需隔离上下文时加载 `subagent-dev`；完成后加载 `review-change`（双轴审查）和 `finish-change`，归档并执行 `tasks.md` 的提交任务。
- Bug/失败/回归：根因不明或难以复现时，在修复前先加载 `debug-fix`；小 bug 修复直接按分级路径走。
- 界面/前端/GUI 变更：实现后加载 `ui-visual-verify`，用自动化操作工具 `opencode_computer_*` 实际操作应用并观察改动后的真实界面截图，完成闭环视觉实际验证，无证据不得声明完成。

不确定时，推荐最轻的一档，并用一小段话解释原因。
