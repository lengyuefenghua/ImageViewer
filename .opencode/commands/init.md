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
- Lazy Flow 最小提醒：以 `.opencode/lazy/AGENTS.template.md` 的 Workflow 段为准（含分级、测试默认不写、权限门：未经用户明确许可禁用 `lazy_computer_*`、人工验收前禁止提交）。
- 仅在存在真实且长期值得记录的取舍时，写 `docs/adr/` 决策记录。

排除：

- 泛泛工程建议。
- 长教程或完整文件树。
- 显而易见的语言/框架默认规范。
- 未验证猜测。
- 应该留在 skills 或命令里的完整流程说明。

Omit empty sections.

## 4. 输出结果

输出：

- 读取了哪些高价值来源。
- 识别到的核心命令。
- `AGENTS.md` 是创建还是更新。
- 当前 Lazy Flow 状态：`idle`。
- 下一步建议：直接描述需求，或 `/start <change-name>` 开始变更。

如果复制或修改了 `.opencode/`，提醒用户重启 OpenCode 后配置才会完整生效。
