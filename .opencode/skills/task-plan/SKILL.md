---
name: task-plan
description: patch 或 full 变更进入实施前使用：产出具体的 tasks.md，包含 checkbox 任务、TDD 步骤、验证命令和审查门。
license: MIT
---

# 任务规划

根据 active change 创建 `docs/changes/<change>/tasks.md`。full 使用 proposal/spec/design；patch 使用已确认的需求和变更状态。

## 入口检查

1. 从 `.opencode/lazy/state.json` 识别 active change，有歧义就问。
2. full：读 `proposal.md`、`specs/**/spec.md`，以及 `design.md`（如有）；patch：读 `tasks.md`（如已存在）和用户已确认的变更范围。
3. full：确认 `state.json` 的 `grilled = true`；patch 不需要 grilling。
4. 检查仓库脚本/配置，找到确切的验证命令。

## 禁止占位符

最终 `tasks.md` 不得包含未解决的占位符，如 `<path>`、`<command>`、`<behavior>`、`TODO`、`TBD` 或复制的示例文本。无法确定确切命令或路径时，停下来问，或加一个带具体行动的发现任务。

## 必需格式

用 checkbox 任务作为进度来源。每个实现任务必须小到能在一个聚焦步骤内完成并验证。

```markdown
# Tasks

## 1. Tests

- [ ] 1.1 在 `exact/path` 为指定行为添加失败测试
- [ ] 1.2 运行 `exact command` 并确认 RED 因 `expected reason` 失败

## 2. Implementation

- [ ] 2.1 修改 `exact/path` 实现指定行为（日志点：<事件/级别/上下文>，或「无日志点：<原因>」）
- [ ] 2.2 运行 `exact command` 并确认 GREEN 通过

## 3. Review

- [ ] 3.1 加载 `review-change` 审查本变更 diff（full：Spec 轴 + Standards 轴；patch：轻量审查），修复 Critical/Important
- [ ] 3.2 运行最终验证：`exact command`

## 4. UI design demo (仅 designDemo=true)

- [ ] 4.1 产出/完善 `docs/changes/<change>/design-demo.html`（单文件、内联 CSS/JS、示例数据、基本交互；无框架/无构建）并请用户验收设计效果与基本功能

## 5. Visual verification (仅界面变更且 visualVerify=true)

- [ ] 5.1 启动应用并用 `opencode_computer_wait_for` 等到窗口/控件就绪
- [ ] 5.2 `opencode_computer_screenshot` 截取改动后的界面（返回图片附件），观察截图并对照 `specs/**/spec.md` 的视觉要求
- [ ] 5.3 覆盖要求的状态（默认/空/错误/响应式），不符则修正后重截
- [ ] 5.4（仅当 `visualReport=true`）用 `save`/`path` 把截图存到 `docs/changes/<change>/evidence/`，并写 `visual-report.md`（嵌图 + 观察 + requirement 映射）

## 6. Self-review (完成前，full/patch)

- [ ] 6.1 自审：逐条对照 `specs/**/spec.md` 的 requirement/scenario 与 `tasks.md`，确认完整实现（不是最小实现/偷懒）、无遗漏；有缺口先修复再重审

## 7. Commit (仅当仓库是 git 仓库)

- [ ] 7.1 提交：`git add -- <该变更相关文件> && git commit -m "<type>(<change>): <中文摘要>"`（只 commit 不 push）
```

示例只作形状参考。所有文本都要替换成仓库相关动作。跨层变更按垂直切片组织任务（每个任务贯穿相关层、可独立验证），不要按层横向堆叠。需要人工介入/确认的任务在任务文本里带 `[人工]` 标记。

## 规划规则

- 每个实现任务是一个 **tracer-bullet 垂直切片**：切一条贯穿相关层（schema/API/UI/测试）的窄而完整的路径，能独立 demo 或验证，且小到塞得进一个全新上下文窗口。
- 任务按依赖排序；有前置依赖时在任务文本写 `（blocked by <编号>）`，按前沿推进（blockers 全部完成的先做）。
- **宽重构**（一次机械改动波及全库、无法逐片保持绿色）走 expand–contract：先新增与旧并存 → 分批迁移（每批一个任务，blocked by 展开）→ 旧形式无调用方后删除（blocked by 所有迁移批）。
- 已知时写明要创建/修改的确切文件。
- 每个可测任务写明确切的验证命令，并写明 seam（测试打在哪个公共接口）；seams 在规划阶段与用户确认一次。
- 行为变更要包含 RED 和 GREEN 任务，使用同一个聚焦命令。
- 完成前必须包含一次 `review-change` 审查任务（full：Spec 轴 + Standards 轴；patch：轻量审查），修复 Critical/Important。
- full 的实现任务默认 inline；需要并行或隔离上下文的任务标明由 `subagent-dev` 派发。
- **仅界面变更**：先判断是否涉及视觉界面（前端页面/组件、样式、布局、响应式、窗口/对话框、自绘/画布）。
  - 不涉及 → 不加任何视觉验证/demo 任务。
  - 涉及 → **读取澄清阶段确定的 `visualVerify`/`designDemo`（未确定则回到 `/clarify`）**，据此生成任务：
    - `visualVerify=true` → 加视觉验证任务（格式第 5 节）；`false` → 不加，且本变更不得使用 `opencode_computer_*`。
    - `designDemo=true` → 加 demo 任务（格式第 4 节）：产出 `docs/changes/<change>/design-demo.html` 并请用户验收。
- 当 `visualVerify=true` 时，视觉验证任务要写清启动方式、`opencode_computer_*` 的截图调用、以及对照 spec 的判据；full 由主控独立再截一次确认（派发子代理时不能只信子代理截图）。
- **所有行为任务**都要评估并列出日志点（事件 + 级别 + 上下文）作为验收点，用级别控制输出内容；确实不适用的写明「无日志点：<原因>」。关键注释作为验收点。
- 任务属于文档/配置/生成物、跳过 TDD 的，在任务文本里说明原因。
- **需要人工介入、协同、手动验收或等待用户确认的任务，任务文本必须带 `[人工]` 标记**（如 `- [ ] 3.8 [人工] 停下等待用户确认`）；执行到该任务时停下等用户，不自行推进。
- **必须**在 `tasks.md` 末尾加自审 + 提交任务（见格式第 6、7 节）：完成前逐条对照 specs/tasks 自审，无缺口后再执行 `git add -- <该变更相关文件> && git commit -m "<type>(<change>): <中文摘要>"`；只 commit 不 push，不用工具代提交。
- 除非 spec/design 要求，否则不要加大范围重构或清理任务。

## 收尾

写完 `tasks.md` 后：

- 重读并删除占位符。
- full：确保每个 spec scenario 至少有一个任务或验证路径。
- 更新 change 的 `state.json`：`phase = "planning"`、`planned = true`。
- 更新 `.opencode/lazy/state.json`：`phase = "planning"`。
