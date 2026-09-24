# Tiny 变更记录

> 三行式：标题（日期 + 摘要）、改动、验证。最新在下。

## 2026-09-25 设置窗注册后自动打开系统「默认应用」页
- 改动：`Views\SettingsWindow.xaml.cs` 的 `RegisterClick` 在 `associations.IsRegistered` 为真时调用系统「默认应用设置」，并抽出共用的 `OpenDefaultAppsSettings()` 私有方法（与 `OpenDefaultAppsClick` 复用）。
- 验证：`tools\test.ps1 -Build` → 96/96 通过；按用户要求未做 UIA 界面验证。
