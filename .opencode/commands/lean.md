---
description: 切换 Lean 模式：lite/full/ultra/off
---

将 Lean 模式切换为 `$ARGUMENTS`。

可选值：

- `lite`：正常做，但指出更懒方案。
- `full`：默认，强制遵循决策阶梯。
- `ultra`：极端 YAGNI，删除优先。
- `off`：关闭 Lean 提醒和检查；Lazy Flow、TDD、验证及项目规则仍然生效。

更新 `.opencode/lazy/state.json` 的 `leanMode`。如果参数不是 `lite/full/ultra/off`，询问用户。
