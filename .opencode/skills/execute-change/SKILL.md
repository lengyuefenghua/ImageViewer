---
name: execute-change
description: 用于安全执行 tasks.md：选取下一个待办任务，采用 TDD（或按需子代理），更新进度，遇到阻塞即停。
license: MIT
---

# 执行变更

一次一个 checkbox 地执行 active change 的 `tasks.md`。

## 入口检查

1. 从 `.opencode/lazy/state.json` 识别 active change，有歧义就问。
2. 读 `tasks.md`、`proposal.md`、`specs/**/spec.md`，以及 `design.md`（如有）。
3. 同时把全局和 change 的 `state.json` phase 设为 `implementing`。
4. 除非用户指定任务，否则选第一个未完成的 checkbox。
5. 修改前确认许可：列出待办任务摘要（涉及文件、验证命令），向用户请求一次确认；仅在用户明确确认后开始修改，除非用户已明确要求执行（如运行了 /apply），或存在 active Lazy Goal 且该 Goal 已授予当前已确认范围的一次性实施授权。

## 模式选择

- tiny：直接修改，不用本技能。
- patch：inline TDD 通常足够。
- full：默认 inline 实现。任务独立可并行、或需要隔离上下文时，加载 `subagent-dev` 派发子代理。

## 单任务循环

1. 复述任务和对应 spec scenario，并列出本任务的日志点（事件/级别/上下文；无则写明原因）。
2. patch 任务 inline 执行；行为变更先加载 `tdd-cycle`，在生产代码前做 RED。
3. full 默认 inline；派发子代理时按 `subagent-dev` 执行，子代理行为变更先加载 `tdd-cycle`。
4. 运行 `tasks.md` 中的聚焦验证命令，并读取输出。
5. 派发子代理的 full 任务由主控读取 diff、独立重跑验证并审查规格符合性和 Lean 最小性。
6. 只有读到新证据后才勾选 checkbox。勾选前逐点确认第 1 步列出的日志点已落地（缺则补），并确认关键处有注释（对照 AGENTS.md 日志/注释规范）。**界面任务**还必须在勾选前加载 `ui-visual-verify`：启动应用、截取改动后的真实界面、观察截图并对照 spec，不符则修正后重截，直到符合。
7. active Lazy Goal 存在时，调用 `lazy_goal_progress` 记录本任务的验证证据，让 Goal 引擎继续下一个待办 checkbox；没有 Goal 时，进入下一个待办 checkbox，或停下来报告状态。
8. full 实现任务全部完成后进入 review/finish；完整实现自审由收尾门统一执行（见 `finish-change`）。

## 失败处理

- RED 意外通过：先修测试再实现。
- RED 因错误原因失败：先修 setup/测试。
- GREEN 失败：改实现，不改测试，除非测试被证明是错的。
- 命令缺失或不明确：从仓库配置推导；仍不确定就问。
- 任务与用户改动冲突：停下来问。
- 实现中发现 spec/design 有错：停下来先更新产物再继续。
- 存在 active Lazy Goal 且遇到以上任何阻塞：调用 `lazy_goal_pause`，写明原因；不要让 Goal 盲目继续。

## 范围规则

- 只做待办 checkbox 任务，除非用户改范围。
- 不捆绑无关清理。
- 必需的日志与关键注释不是“多余”，不得当冗余删除；反过来也不得为凑数加占位日志/叙述注释。
- 不要因为一条命令通过就勾选多个任务。
- 没有新验证证据不得声明完成。
- 界面变更没有“真实截图 + 已观察”证据不得声明完成。
- 遇到带 `[人工]` 标记的任务：停下等用户，不自行推进。

## 进度更新

每完成一个任务：

- 更新 `tasks.md` 的 checkbox。
- 在回复中记录简明证据：运行的命令和结果。
- 实现任务全部完成前，state 保持 `implementing`。
