---
name: spec-design
description: 用于完整变更在动生产代码之前：澄清意图、选定 change id，并在 docs/changes 下产出完整的 proposal/spec/design 产物。
license: MIT
---

# 规格设计

完整变更在动生产代码之前使用。

直接在 `docs/changes/<change>/` 下创建和编辑产物文件。

## 入口检查

1. 如果还没分级，先加载 `using-lazy-flow`。
2. 确认这确实是 `full`。如果是 `tiny` 或 `patch`，推荐更轻的路径。
3. 确定 change id：
   - 优先使用命令参数。
   - 否则从请求的变更推导一个简短的 kebab-case id。
   - 如果有多种合理解释，问一次。
4. 检查 `docs/changes/<change>/` 是否已存在。
   - 已存在则先读已有产物再更新，不要盲目覆盖。
   - 如果属于不同意图，停下来问。
5. 读 `docs/changes/<change>/proposal.md` 的 `## 需求确认` 段（grill-me 落盘）和仓库根 `CONTEXT.md`（术语表，如有）；产物使用其中的术语，并保留需求确认段。

## 流程

1. 探索相关代码、测试、已有 `docs/specs/`、`CONTEXT.md` 和项目约定。
2. 仅当歧义会改变范围、行为或风险时才问澄清问题。
3. 存在真实设计取舍时对比方案，推荐最省力的安全方案。
4. 创建或更新 `proposal.md`、`specs/<capability>/spec.md`、可选的 `design.md` 和 `state.json`。
5. 停下前自查产物。
6. 产出后即停止，建议运行 `/plan`；不要规划或实现。

## 禁止占位符

最终产物不得包含未解决的占位符，如 `<title>`、`<path>`、`<behavior>`、`TODO`、`TBD`、`lorem` 或示例文本。信息未知但重要时，问用户，或在 `design.md` 里写成显式开放问题。

## proposal.md

必需章节（保留 `grill-me` 落盘的 `## 需求确认` 段）：

```markdown
# Proposal: 清晰的人类可读标题

## 需求确认
<grill-me 落盘的确认结论，保留不覆盖>

## Why
用 1-2 个短段落说明问题或机会。

## What Changes
- 具体的行为或能力变更。
- 破坏性变更标记 **BREAKING**。

## Capabilities
### New Capabilities
- kebab-case-name

### Modified Capabilities
- existing-kebab-case-name

## Impact
- Data: 迁移/存储变更，或 "none"
- Risks: 具体风险，或 "low"
```

不要凭空发明 capability。列 Modified Capabilities 前先检查 `docs/specs/`。

## specs/<capability>/spec.md

规格文件只定义可观察行为。它们是行为契约，不是实现计划。

每个 capability 一个 delta spec：

```text
docs/changes/<change>/specs/<capability>/spec.md
```

允许的章节：

```markdown
## ADDED Requirements
## MODIFIED Requirements
## REMOVED Requirements
## RENAMED Requirements
```

规则：

- 每个生效 requirement 使用 `### Requirement: 名称`。
- 每个生效 requirement 至少有一个 `#### Scenario: 名称`。
- Scenario 标题必须恰好四个井号。
- 规范性行为用 SHALL/MUST。
- `ADDED` 用于新行为。
- `MODIFIED` 必须包含从 `### Requirement:` 到所有 scenario 的完整更新块。
- `REMOVED` 必须包含 `**Reason**:` 和 `**Migration**:`。
- `RENAMED` 使用 `FROM:` 和 `TO:`，仅用于改名。
- 改名后的行为变更放在 `MODIFIED`，使用新名字。
- 不要提及内部类/函数名、库选型或逐行实现。

对修改的 capability，先读当前主 spec 并复制原 requirement 块再编辑。不要写不完整的 modified 块。

## design.md

存在架构、依赖、迁移、数据模型、安全、性能或跨模块取舍时创建 `design.md`。

创建时必需章节：

```markdown
# Design: 清晰的人类可读标题

## Context
## Goals / Non-Goals
## Decisions
## Risks / Trade-offs
## Migration Plan
## Open Questions
```

简单完整变更的 `design.md` 可以很短，但必须解释关键取舍以及为什么不需要更重的设计。

界面变更时，在 `design.md` 增加 `## UI 草图` 段：用文字给出布局、区块、组件与交互要点（简单界面到文字草图即可，不必建文件）。

## design-demo.html

当本变更是**复杂界面**变更且用户确认 `designDemo=true` 时，产出 `docs/changes/<change>/design-demo.html`：

- 单文件：HTML + 内联 CSS + 内联 JS，无外部依赖、无框架、无构建。
- 带示例数据，展示设计效果与基本交互（点击/切换/状态变化等），供用户做基本功能验收。
- 不追求生产级实现，只表达设计意图；范围保持最小。

简单界面不产出 HTML，用 `design.md` 的 `## UI 草图` 即可。

## state.json

创建或更新：

```json
{
  "level": "full",
  "phase": "design",
  "grilled": true,
  "planned": false,
  "reviewed": false,
  "archived": false,
  "visualVerify": true,
  "designDemo": true
}
```

界面变更时，把澄清阶段确定的 `visualVerify`/`designDemo` 记录进 change 的 `state.json`（并同步进全局 `.opencode/lazy/state.json`）。

同时更新 `.opencode/lazy/state.json`：`activeChange`、`level = "full"`、`phase = "design"`。

## 自查门

停下前检查：

- 无未解决占位符。
- Proposal 的 capabilities 与 spec 文件一致。
- 每个生效 requirement 都有 scenario。
- Spec 写的是行为，不是实现。
- `MODIFIED` 块完整。
- 范围最小，没有明显 YAGNI 的字段或 API。
- 术语与 `CONTEXT.md` 一致；引入的新领域概念已回写 `CONTEXT.md`。
- **冲突检查**：把 `proposal.md` 的需求确认段/spec 与 `CONTEXT.md`、`AGENTS.md`、`docs/adr/`、代码规范、架构规范、测试规范对照；发现冲突先列出并解决，解决不了就停下来问用户。带着冲突不进入规划、不写代码。
