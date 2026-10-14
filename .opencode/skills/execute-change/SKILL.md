---
name: execute-change
description: 用于安全执行 tasks.md：选取下一个待办任务，按 tdd-cycle 取舍测试（或按需子代理），更新进度，遇到阻塞即停。
license: MIT
---

# 执行变更

一次一个 checkbox 地执行 active change 的 `tasks.md`。

## 入口检查

1. 从 `.opencode/lazy/state.json` 识别 active change，有歧义就问。
2. 读 `tasks.md`、`proposal.md`、`specs/**/spec.md`，以及 `design.md`（如有）。
3. 同时把全局和 change 的 `state.json` phase 设为 `implementing`。
4. 除非用户指定任务，否则选第一个未完成的 checkbox。
5. 修改前确认许可：列出待办任务摘要（涉及文件、验证命令），向用户请求一次确认；仅在用户明确确认后开始修改，除非用户已明确要求执行（如运行了 `/apply`，它只授权执行**已规划任务**），或存在 active Lazy Goal 且该 Goal 已授予当前已确认范围的一次性实施授权。执行中出现新需求或范围变更时，按「执行中的新需求 / 范围变更」处理，不因 `/apply` 或 Goal 授权而跳过。

## 模式选择

- tiny：直接修改，不用本技能。
- patch：inline 实现，测试按 `tdd-cycle` 取舍。
- full：默认 inline 实现；任务独立可并行、或需要隔离上下文时加载 `subagent-dev` 派发子代理。

## 单任务循环

1. 复述任务和对应 spec scenario，并列出本任务的日志点（事件/级别/上下文；无则写明原因）。
2. 行为变更先加载 `tdd-cycle` 判断是否值得测试（默认不写）：命中漏测后果闸的在生产代码前做 RED，其余写明「无测试：<原因>」。
3. full 派发子代理时按 `subagent-dev` 执行（其提示词已含测试/视觉取舍），主控不重复整套流程。
4. 运行 `tasks.md` 中的聚焦验证命令，并读取输出。
5. 派发子代理的 full 任务由主控读取 diff、独立重跑验证并审查规格符合性和 Lean 最小性。
6. 只有读到新证据后才勾选 checkbox。勾选前逐点确认第 1 步列出的日志点已落地（缺则补），并确认关键处有注释（对照 AGENTS.md 日志/注释规范）。**界面任务**且用户明确许可 `visualVerify=true` 时，还必须在勾选前加载 `ui-visual-verify` 完成闭环；未获许可不得调用 `lazy_computer_*`。
7. active Lazy Goal 存在时，调用 `lazy_goal_progress` 记录本任务的验证证据，让 Goal 引擎继续下一个待办 checkbox；没有 Goal 时，进入下一个待办 checkbox，或停下来报告状态。
8. full 实现任务全部完成后进入 review/finish；完整实现自审由收尾门统一执行（见 `finish-change`）。

## 执行中的新需求 / 范围变更

执行期间（含 `/apply` 与 build agent）出现以下任一情况，**禁止直接改代码**：

- 用户提出新的需求改动；
- 实现中暴露需要调整方案或超出已确认范围（spec/design/tasks 未覆盖）。

必须先停下并给出简短方案：改什么、涉及哪些文件、对现有产物/任务的影响、验证方式；必要时先更新 `tasks.md`（或 proposal/spec/design），经用户明确确认后再动代码。因新需求改变范围时，先把它作为新任务或新变更落入 `tasks.md` 再执行。

**例外**：仅当用户明确声明本变更期间不再逐次确认时，把 change 的 `state.json` 置 `confirmWaived=true`，同一变更内可连续实施而不再逐次确认。该豁免不覆盖提交/推送门与 `lazy_computer_*` 权限门，且仅在当前变更有效，归档或新变更后失效。

## 失败处理

- RED 意外通过：先修测试再实现。
- RED 因错误原因失败：先修 setup/测试。
- GREEN 失败：改实现，不改测试，除非测试被证明是错的。
- 命令缺失或不明确：从仓库配置推导；仍不确定就问。
- 任务与用户改动冲突：停下来问。
- 实现中发现 spec/design 有错：停下来先更新产物再继续。
- 存在 active Lazy Goal 且遇到以上任何阻塞：调用 `lazy_goal_pause`，写明原因；不要让 Goal 盲目继续。

## 范围规则

- 只做待办 checkbox 任务；用户改范围或出现新需求时按「执行中的新需求 / 范围变更」先方案后改，禁止直接改。
- 只改必须改的：不“顺手改进”相邻代码、注释或格式；不重构没坏的东西；即使你更想换个写法也沿用现有风格。
- 无关的死代码只指出、不删除；但自己这次改动造成的孤儿（未用的 import/变量/函数）要清掉。
- 必需的日志与关键注释不是“多余”，不得当冗余删除；反过来也不得为凑数加占位日志/叙述注释。
- 不要因为一条命令通过就勾选多个任务。
- 没有新验证证据不得声明完成。
- 界面变更：获用户明确许可（`visualVerify=true`）时，没有“真实截图 + 已观察”证据不得声明完成；未获许可不得调用 `lazy_computer_*`。
- 遇到带 `[人工]` 标记的任务：停下等用户，不自行推进。

## 进度更新

每完成一个任务：

- 更新 `tasks.md` 的 checkbox。
- 在回复中记录简明证据：运行的命令和结果。
- 实现任务全部完成前，state 保持 `implementing`。
