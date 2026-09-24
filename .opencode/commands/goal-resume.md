---
description: 恢复当前项目已暂停的 Lazy Goal
---

先调用 `lazy_goal_status` 检查暂停原因。仅当用户已处理或明确允许继续时，调用 `lazy_goal_resume`。恢复后由 Goal 引擎推进下一步。
