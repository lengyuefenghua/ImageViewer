---
name: ui-visual-verify
description: 涉及界面/前端/GUI/窗口渲染/布局/样式的变更，在声明完成前使用：用自动化操作工具（opencode_computer_*，窗口/键鼠/UIA/截图）实际操作应用并截图观察，闭环比对规格，反复修正直到实际渲染符合要求。
license: MIT
---

# 界面视觉实际验证

界面变更不能只靠单测或 DOM 断言。必须用**自动化操作工具实际运行并操作应用**，再**截图观察**，闭环到真实渲染符合规格。

用 `opencode_computer_*` 做**自动化操作**（窗口枚举/管理、键鼠输入、PostMessage、UIA 控件操作、等待、结果校验），截图只是其中用于观察的一环。不要只用截图，也不要用截图代替操作与校验。

## 何时必须使用

- **仅当本变更 `visualVerify=true` 时使用**；`false` 时本技能不适用，本变更不得使用 `opencode_computer_*`。
- 前端页面/组件、样式、布局、响应式、主题
- GUI 应用、窗口/对话框、自绘/画布
- 用户要求“看效果/截图确认/视觉验证”

tiny 的纯文案改动可豁免，但要说明理由。若 `designDemo=true`，以 `docs/changes/<change>/design-demo.html` 的设计效果为参照；否则以 `design.md` 的 `## UI 草图` 为参照。

## 硬性规则

1. 没有**实际观察过**改动后的真实界面截图之前，不得声明界面完成。
2. 截图必须是**改动后**实际运行的结果，不是设计稿、不是旧图。
3. 必须覆盖 spec/design 里要求的状态（默认/空/加载/错误/禁用/响应式等）。
4. 发现不符 → 修正 → 重新截图，直到符合；不允许“看起来差不多”。
5. 证据写进 tasks.md/回复：截图路径 + 观察到的内容 + 对应哪条 requirement。

## 工具（自动化操作工具，非仅截图）

用 `opencode_computer_*`：

- `opencode_computer_windows` / `opencode_computer_wait_for`：找到/等待目标窗口或控件
- `opencode_computer_screenshot`：截窗口/客户区/区域（后台 PrintWindow，不抢焦点），**直接返回图片附件**供观察
- `opencode_computer_controls` / `opencode_computer_control`：定位并操作控件（点击、输入、展开菜单）
- `opencode_computer_key` / `opencode_computer_mouse`：需要真实键鼠时（前台会抢焦点）
- `opencode_computer_verify`：读回控件值/标题/文件做客观校验
- `opencode_computer_do`：把「操作→等待→截图」合成一次调用

直接观察截图附件；界面未显示图片或需要重看时才用 `read` 兜底。

## 闭环流程

1. **启动**：按仓库方式启动应用（dev server / 打开应用/窗口）；用 `wait_for` 等就绪。
2. **到达目标界面**：用 `control`/`key`/`mouse` 导航到要验证的界面/状态。
3. **截图**：`opencode_computer_screenshot`（窗口或区域），拿到图片。
4. **观察**：查看截图附件（必要时用 `read` 兜底），逐条对照 spec/design 的视觉要求（布局、文案、颜色、状态，无报错/错位/溢出）。
5. **判定**：符合 → 记录证据；不符 → 定位原因（代码/样式/数据）→ 修正 → 回到步骤 3。
6. **状态覆盖**：对 spec 要求的状态重复 2–5。
7. **交互验证**（如要求）：用 `control`/`key`/`mouse` 触发交互，再用 `verify`/`screenshot` 确认结果状态。

## 与 Lazy Flow 的衔接

- patch/full：`task-plan` 必须为界面变更加入“视觉验证”任务，写明确切的工具调用与截图判据。
- `execute-change`：界面任务勾选前必须完成上述闭环。
- `finish-change`：界面变更的验证门包含“改动后真实截图 + 已观察”证据；缺失不得归档/提交。
- full：派发子代理完成界面任务后，主控必须**独立再截一次**确认，不能只信子代理的文字报告。
- 证据不足或无法启动界面时，暂停（Goal 下调用 `lazy_goal_pause`）并报告，不要假装完成。

## 视觉验收报告（仅当 `visualReport=true`）

只有变更涉及视觉界面、且规划阶段用户确认需要验收报告时才做：

- 截图用 `opencode_computer_screenshot` 的 `save`/`path` 存到 `docs/changes/<change>/evidence/`。
- 在同目录写 `visual-report.md`：每个状态一张截图（相对路径嵌入）+ 观察结论 + 对应 requirement/scenario。
- 证据目录 `docs/changes/**/evidence/` 已加入 `.gitignore`，仅本地验收用、不提交。
- `visualReport=false` 或非界面变更时：不做报告；界面变更仍执行上面的闭环观察，但截图不落盘。

## 常见坑

- 后台窗口截图用 PrintWindow；部分 GPU/DirectX 窗口可能黑屏，用 `mode=screen` 回退（会抢焦点）。
- 最小化窗口无法直接截图：先静默还原（不抢焦点）再截。
- 坐标：鼠标默认客户区，截图默认整窗；用 `client=true` 对齐。
- 浏览器界面：定位浏览器窗口进程/标题后截图；必要时先导航到目标 URL。
