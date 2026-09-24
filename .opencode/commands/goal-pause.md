---
description: 暂停当前项目的 Lazy Goal，并保留可恢复状态
---

调用 `lazy_goal_pause`。使用 `$ARGUMENTS` 作为暂停原因；没有提供原因时，使用“用户请求暂停”。报告已暂停及后续用 `/goal-resume` 恢复。
