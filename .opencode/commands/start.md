---
description: 开始一个变更：按 tiny、patch 或 full 分级并进入对应工作流
---

先加载 `using-lazy-flow` 分级，再按路由处理 `$ARGUMENTS`：

- tiny：直接最小修改，不创建 change；完成后往 `docs/changes/tiny-log.md` 追加三行记录。
- patch：创建 `docs/changes/<change>/tasks.md` 和 `state.json`，设置全局 `activeChange`、`level = "patch"`、`phase = "planning"`；停止，建议 `/apply`。
- full：加载 `grill-me` 澄清（术语写入 `CONTEXT.md`，需求卡落盘 `change.md`），停点 1 等用户确认；确认后加载 `spec-design` 补齐协作卡、流程契约、`specs/<capability>/spec.md` 和 `state.json`，停点 2 等用户确认；确认后停止，建议 `/plan`。设置全局 `activeChange`、`level = "full"`、`phase = "design"`。

产出直接创建在 change 目录中。权限门：未经用户明确许可不得调用 `lazy_computer_*`；人工验收通过前禁止提交。
