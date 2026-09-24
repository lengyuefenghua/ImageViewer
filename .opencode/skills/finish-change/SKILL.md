---
name: finish-change
description: 实现完成后使用：验证、审查、安全同步 delta specs 到 docs/specs、写决策记录、归档 change，并重置 Lazy Flow 状态。
license: MIT
---

# 收尾变更

核心规则：

```text
没有新鲜验证证据，不得声明完成
```

在当前回合运行命令、读取输出，再报告真实状态。

## 入口检查

1. 从 `.opencode/lazy/state.json` 或 `docs/changes/*/state.json` 识别 active change。
2. 存在多个 active change 时，让用户选择。
3. 读 `tasks.md`、`proposal.md`、`specs/**/spec.md`、`design.md`（如有）和 change 的 `state.json`。
4. 统计未完成的 `- [ ]` checkbox。
5. 同时把全局和 change 的 `state.json` phase 设为 `finishing`。

## 验证门

归档或声明成功之前：

1. 运行 `tasks.md` 和仓库脚本中的相关验证命令。**只跑 tasks.md 列出的最小相关命令，禁止惯性跑全量套件**；除非变更跨库到无法界定范围。
2. 读取输出和退出状态。
3. 执行 `review-change`：full 做双轴完整审查，patch 做轻量审查。
4. 验证失败、审查有重要发现或任务未完成时，报告真实状态并先询问，再继续；存在 active Lazy Goal 时调用 `lazy_goal_pause` 记录阻塞原因。
5. 用户要推送/提交 PR 时：检查 git 状态和远程分支，不重复运行已通过的检查；远程有 CI 时报告 `gh pr checks` 结果，报告保持真实（pending 就是 pending）。
6. 归档前用 `git diff` 浏览改动文件清单，确认每个改动文件的验证已被覆盖；未覆盖的说明原因或补验证。
7. **界面变更**：必须有改动后的真实截图（`opencode_computer_screenshot`）并观察过，对照 `specs/**/spec.md` 的视觉要求；缺失或未观察不得归档、提交或声明完成。无法启动界面时暂停并报告，不要假装完成。
8. **自审**（full/patch，收尾门唯一执行点；tiny 不适用）：完成前必须逐条对照 `specs/**/spec.md` 的 requirement/scenario 与 `tasks.md`，确认完整实现、无遗漏；有缺口先修复。复杂 diff 可派只读子代理辅助，但不强制。
9. 存在未完成的 `[人工]` 任务时：停下等用户确认，不自行归档或提交。

## 同步决策

用户尚未批准归档/同步时，给出选项：

- 同步 specs 并归档。
- 保持 change 激活。
- 只同步 specs。
- 仅当 change 没有行为/spec 影响时，才允许不同步 specs 直接归档。

不要静默归档。存在 active Lazy Goal 时，用户尚未批准同步或归档属于暂停条件：调用 `lazy_goal_pause`，不得自动继续。

## 手动 Delta 同步安全规则

对每个 `docs/changes/<change>/specs/<capability>/spec.md`：

1. 读 delta spec。
2. 读主 spec：`docs/specs/<capability>/spec.md`。
3. 主 spec 缺失时，仅当 delta 只有 `ADDED` 且没有 `MODIFIED` 或 `RENAMED` 操作，才创建骨架。
4. 解析章节：
   - `## ADDED Requirements`
   - `## MODIFIED Requirements`
   - `## REMOVED Requirements`
   - `## RENAMED Requirements`
5. 写入前检测冲突：
   - 同一 requirement 出现在多个章节。
   - 同一章节内 requirement 名重复。
   - `ADDED` 在主 spec 中已存在。
   - `MODIFIED` 目标在主 spec 中缺失。
   - `REMOVED` 目标在主 spec 中缺失。
   - `RENAMED` 的 `FROM` 缺失或 `TO` 已存在。
   - 改名后的 requirement 在 `MODIFIED` 里用了旧名字。
6. 有冲突就停下问，不写部分同步。
7. 写入前按 capability 给出摘要：added、modified、removed、renamed。
8. 除非用户已明确批准同步，否则写入前请求确认。

## 主 Spec 骨架

创建新主 spec 时使用：

```markdown
# <capability> Specification

## Purpose
<基于 proposal 的一句话>

## Requirements
```

不要 `TBD` 或占位符文本。

## 应用操作

按此顺序应用：

1. `RENAMED`：把 requirement 标题从 `FROM` 改为 `TO`，保留正文。
2. `REMOVED`：删除完整匹配的 requirement 块。Reason/Migration 留在归档的 delta 中。
3. `MODIFIED`：用提供的完整更新块替换完整匹配的 requirement 块。
4. `ADDED`：追加新的 requirement 块。

保留未触及的 purpose 文本、注释、requirement 块和既有顺序。

写完后重读主 spec 并检查：

- 每个生效 requirement 使用 `### Requirement:`。
- 每个生效 requirement 至少有一个 `#### Scenario:`。
- 没有引入 `<...>`、`TODO`、`TBD` 之类的占位符。
- 摘要计数与写入结果一致。

## 决策记录

仅在存在真实、长期值得记录的取舍时写 `docs/adr/` 下的 ADR。没有取舍时不创建 ADR。

写入规则：

- 选择类别目录（architecture/process/feature/bug-fix/simplification/testing），文件名 `yyyy-mm-dd-slug.md`。
- 新 ADR 使旧决策过时时，旧 ADR 的 Status 改为 `superseded by ../<class>/<新文件名>.md`。
- ADR 使用 `Status: accepted` frontmatter、简短标题，以及说明背景、结论和理由的一至三句话；存在被拒方案时追加 `## Considered Options`。

归档前自查：

- [ ] 类别目录正确、日期文件名
- [ ] 有被拒备选时 Considered Options 非空，每条写清为什么输
- [ ] 旧 ADR 的 superseded 标记已加
- [ ] 无推理痕迹、无变更史叙事（只写当前决策与理由）

## 归档安全

归档路径：

```text
docs/changes/archive/YYYY-MM-DD-<change>/
```

移动之前：

- 确认归档目标不存在。
- 确认没有必需的同步/写入失败。
- 保留 change 文件夹内的全部产物。
- 没有用户明确指示，不得丢弃失败或未审查的工作。

移动后更新 `.opencode/lazy/state.json`：

```json
{
  "initialized": true,
  "activeChange": null,
  "level": "tiny",
  "phase": "idle",
  "leanMode": "<保持原模式>"
}
```

归档 change 的 `state.json`（如存在）更新 `archived = true`。

## 最终回复

报告：

- 运行的验证命令和结果。
- 审查结论。
- 界面变更：截图证据与观察结论。
- Specs 是否已同步。
- 归档路径或保持激活的原因。
- 剩余风险。

存在 active Lazy Goal 时，只有最终目标已满足且上述验证、审查、同步、归档和提交均已完成，才能调用 `lazy_goal_mark_done`，附上最终验证证据。若最终目标尚未满足，调用 `lazy_goal_progress` 记录完成的收尾步骤，让 Goal 继续。

full change 归档前必须确认 `tasks.md` 中**除提交任务外**的任务全部完成且有新鲜验证证据；未达标不得归档或提交。归档并把 `.opencode/lazy/state.json` 置 idle 后，先完成自审（见验证门第 8 条），再执行 `tasks.md` 中的提交任务：`git add -- <该变更相关文件> && git commit -m "<type>(<change>): <中文摘要>"`；只 commit 不 push，不用工具代提交。
