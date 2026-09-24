---
description: 验证、审查、同步规格并归档当前 change
---

使用 `finish-change` 技能。

校验、规格同步和归档都通过读取和更新项目文件完成。

顺序：

1. 读取 active change 和 `tasks.md`。
2. full change：确认 `tasks.md` 中除提交任务外的任务全部完成且有新鲜验证证据；未达标不得归档或提交。
3. 运行项目测试和 tasks.md 中列出的验证命令。
4. 执行 `review-change`（full 双轴；patch 轻量）：spec compliance、TDD evidence、Lean 精简、风险检查；界面变更还需确认真实截图 + 已观察证据；完成前逐条对照 specs/tasks 自审。
5. 检查 delta specs 格式：`ADDED / MODIFIED / REMOVED / RENAMED Requirements`。
6. 如果用户确认归档，手动同步 delta specs 到 `docs/specs/`：
   - `ADDED` 添加 requirement。
   - `MODIFIED` 用完整 requirement block 替换同名 requirement。
   - `REMOVED` 删除同名 requirement，并保留归档中的 Reason/Migration。
   - `RENAMED` 按 `FROM:` / `TO:` 改名并保留内容。
   - 保留未触及内容。
7. 仅在存在真实、长期值得记录的取舍时，写/更新 `docs/adr/` 决策记录。
8. 移动 `docs/changes/<change>/` 到 `docs/changes/archive/YYYY-MM-DD-<change>/`。
9. 更新 `.opencode/lazy/state.json` 为 idle。
10. 若存在 active Lazy Goal（手动 `/goal`），最终目标满足时调用 `lazy_goal_mark_done`。
11. 完成自审后，执行 `tasks.md` 中的提交任务：`git add -- <该变更相关文件> && git commit -m "<type>(<change>): <中文摘要>"`；只 commit 不 push，不用工具代提交。

不要归档 failing、未审查或用户未确认的 work。任务未完成或自审未全绿时可以提示并询问，但不要擅自继续。
