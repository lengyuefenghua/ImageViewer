---
description: 初始化当前项目并生成紧凑 AGENTS.md
---

初始化当前项目。不要生成或修改 `.opencode/skills/*`、`.opencode/commands/*`、`.opencode/plugins/*`；这些是模板自带工具包。

目标：创建或更新根目录 `AGENTS.md`，让未来 OpenCode 会话快速掌握本仓库最容易猜错的事实。每一行都必须回答：没有这句，agent 是否很可能踩坑？如果不是，就删掉。

## 1. 调查项目

优先读取最高价值来源：

1. `README*`、根 manifest、workspace config、lockfile。
2. build、test、lint、formatter、typecheck、codegen 配置。
3. CI workflow、pre-commit、task runner 配置。
4. 现有 instruction files：`AGENTS.md`、`CLAUDE.md`、`.cursor/rules/`、`.cursorrules`、`.github/copilot-instructions.md`。
5. repo-local OpenCode config：`opencode.json`、`opencode.jsonc`、`.opencode/opencode.json`。

可执行来源优先于 prose。若文档和脚本冲突，信脚本和配置，只保留可验证内容。

如果架构仍不清楚，再读少量代表性源码：入口文件、路由/wiring、package 边界、应用启动点。不要随机读叶子文件。

只在仓库无法回答关键问题时问用户，一次短问题批次即可。

## 2. 创建目录和状态

确保存在：

- `docs/specs/`
- `docs/changes/`
- `docs/changes/tiny-log.md`（预建，追加式三行记录）
- `docs/adr/`（类别子目录按需惰性创建）
- `.opencode/lazy/`

`docs/changes/tiny-log.md` 不存在时创建，内容：

```markdown
# Tiny 变更记录

> 三行式：标题（日期 + 摘要）、改动、验证。最新在下。
```

`CONTEXT.md`（术语表）不预建，首次术语确定时由 `grill-me` 惰性创建。

确保根目录 `.gitignore` 包含 `docs/changes/**/evidence/`（视觉验收证据本地保留、不提交），缺失则追加。

创建或更新 `.opencode/lazy/state.json`：

```json
{
  "initialized": true,
  "activeChange": null,
  "level": "tiny",
  "phase": "idle",
  "leanMode": "full"
}
```

保留已有字段，缺失字段补齐。

## 3. 生成或更新 AGENTS.md

使用 `.opencode/lazy/AGENTS.template.md` 作为结构参考，但不要机械复制。根 `AGENTS.md` 应该短、项目特定、可验证。

如果 `AGENTS.md` 已存在：

- 改进原文件，不要盲目重写。
- 保留已验证有用的项目约定。
- 删除泛泛建议、过时声明、不可验证猜测。
- 与当前脚本、配置、CI 对齐。

优先写这些高信号事实：

- Exact commands：install、dev、build、lint、typecheck、test。
- Focused verification：单测、单包、单文件、单命令怎么跑。
- Required order：例如 `lint -> typecheck -> test`，仅当仓库确实要求。
- Architecture：真实入口、主要目录边界、包/应用职责。
- Tooling quirks：codegen、generated files、migrations、env loading、dev server、deploy flow。
- Testing quirks：fixtures、required services、snapshots、昂贵/易 flaky 的 suite。
- Existing instruction files 中仍然重要的约束。
- Lazy Flow 最小提醒：开发请求先加载 `using-lazy-flow`；`tiny`（文案/样式/小修复/局部优化，可跨文件不改契约，重要代码必须聚焦测试，三行记录到 `docs/changes/tiny-log.md`）/ `patch` / `full`（仅共享契约/架构级/迁移/高不确定）；行为变化优先 TDD，full 实现默认 inline、按需派发子代理，完成声明前 fresh verification，推送前只跑最小相关命令；`/goal <最终目标>` 可在已确认范围内持续推进，并在阻塞时暂停；界面/前端/GUI 变更必须用自动化操作工具 `opencode_computer_*` 做闭环视觉实际验证（实际操作 + 截图观察）。
- 仅在存在真实且长期值得记录的取舍时，写 `docs/adr/` 决策记录。

排除：

- 泛泛工程建议。
- 长教程或完整文件树。
- 显而易见的语言/框架默认规范。
- 未验证猜测。
- 应该留在 skills 或命令里的完整流程说明。

建议结构：

```markdown
# Repository Instructions

## Commands
- Install: <exact command>
- Dev: <exact command>
- Build: <exact command>
- Lint: <exact command>
- Typecheck: <exact command>
- Test: <exact command>
- Single test: <exact command/pattern>

## Architecture
- <entrypoints and package boundaries agents would not infer quickly>

## Workflow
- Load `using-lazy-flow` before development work, then use Lazy Flow levels: tiny / patch / full. Tiny = copy/style/small fixes/local optimizations (may span files, no contract changes); focused tests required on critical code; append a 3-line entry to `docs/changes/tiny-log.md`.
- For behavior changes, prefer TDD unless the task is explicitly tiny.
- For full changes, implement inline by default; dispatch subagents only for parallel or context-isolated tasks; the controller verifies and reviews the result.
- For UI/frontend/GUI changes, do closed-loop visual verification with the automation tools `opencode_computer_*` (operate + screenshot): launch the app, drive it, screenshot the changed UI (returned as an image attachment), observe it, compare against spec, fix and re-capture until it matches; never claim done without real screenshot evidence.
- An active `/goal <final objective>` authorizes work only within confirmed scope; log each verified step and pause for decisions or failures.
- Before claiming done/fixed/passing, run the relevant verification command in the current turn and read output.
- Write an ADR only for a real, long-lived decision trade-off.
- Log at key paths (external boundaries, errors/retries, state transitions, lifecycle) with correct levels; comment the non-obvious (intent/invariants/boundaries/counter-intuitive branches/public contracts) in Chinese; never narrate execution steps or log secrets.

## 代码注释
- 注释用中文；默认不写，但关键处必须有注释（非显然意图/不变量/边界/反直觉分支/公共契约）；禁止执行过程叙述、复述代码、变更史；平凡代码不加注释。

## 日志
- 日志是行为变更的交付物：所有行为变更必须评估并列出日志点（事件 + 级别 + 上下文）；涉及外部边界/异常降级重试/状态转换/用户可见结果的必须落地，确实不适用的写明原因；用级别控制输出内容。缺日志视为未完成。豁免：很小的一次性验证/临时项目按项目整体豁免，需一句话说明。
- 优先用仓库已有 logger；日志文案随仓库惯例（通常英文）。
- 级别：TRACE/DEBUG（开发排障）< INFO（启动/配置/关键业务节点）< WARN（潜在问题/重试/降级）< ERROR（当前操作失败）< FATAL（无法继续运行）。
- 记录点：外部边界（I/O/网络/DB/文件/子进程）的调用与失败、异常/降级/重试/回退、关键状态转换、启动与关闭；每条带定位上下文。
- 禁止：敏感数据（密钥/token/PII）、循环刷屏、无信息占位日志。

## 文档层级
- 每个事实只有一个家：决策 → `docs/adr/`；行为契约 → `docs/specs/`；术语 → `CONTEXT.md`；需求确认 → `docs/changes/<change>/proposal.md` 的需求确认段；变更过程 → `docs/changes/`；tiny 记录 → `docs/changes/tiny-log.md`；常设规则 → 本文件。
- 别处引用用相对路径链接，不粘贴副本。写当前状态，不写变更史。

## Gotchas
- <repo-specific traps only>
```

Omit empty sections.

## 4. 输出结果

输出：

- 读取了哪些高价值来源。
- 识别到的核心命令。
- `AGENTS.md` 是创建还是更新。
- 当前 Lazy Flow 状态：`idle`。
- 下一步建议：直接描述需求，或 `/start <change-name>` 开始变更。

如果复制或修改了 `.opencode/`，提醒用户重启 OpenCode 后配置才会完整生效。
