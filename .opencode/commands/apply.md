---
description: 执行当前 active change 的 tasks.md
---

使用 `execute-change` 技能执行 active change 的 `tasks.md`；单任务循环、测试取舍、日志/注释、子代理派发和失败处理以该技能为准。

命令特有约束：

- 一次一个 checkbox，读到新证据才勾选。
- 不做 `tasks.md` 外的顺手优化或重构。
- 执行中出现新需求或范围变更：先出方案并获用户明确确认再改码，禁止直接改；仅当用户明确声明本变更期间不再确认（`confirmWaived=true`）才可连续实施。
- 权限门：未经用户明确许可禁止调用 `lazy_computer_*`；人工验收通过前禁止提交。
