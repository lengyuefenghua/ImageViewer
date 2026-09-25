# Tiny 变更记录

> 三行式：标题（日期 + 摘要）、改动、验证。最新在下。

## 2026-09-25 设置窗注册后自动打开系统「默认应用」页
- 改动：`Views\SettingsWindow.xaml.cs` 的 `RegisterClick` 在 `associations.IsRegistered` 为真时调用系统「默认应用设置」，并抽出共用的 `OpenDefaultAppsSettings()` 私有方法（与 `OpenDefaultAppsClick` 复用）。
- 验证：`tools\test.ps1 -Build` → 96/96 通过；按用户要求未做 UIA 界面验证。

## 2026-09-25 设置窗新增「关于」并提升版本至 1.0.0
- 改动：`Views\SettingsWindow.xaml(.cs)` 增加「关于」段（开发者：系统集成部；版本动态取程序集版本）；`ImageViewer.csproj` 的 `<Version>` 0.1.0 → 1.0.0。
- 验证：`tools\test.ps1 -Build` → 116/116 通过；Release 重建成功（界面文案类，未做 UIA 验证）。

## 2026-09-25 底栏信息条颜色跟随主题
- 改动：`Views\ViewerWindow.xaml` 底栏 `Border` 背景 `#B3000000` → `{DynamicResource SolidBackgroundFillColorSecondaryBrush}`，文字 `White`/分隔符 `#66FFFFFF` → `TextFillColorPrimaryBrush`/`TextFillColorTertiaryBrush`；浅色模式下不再显示深色条。加载中/错误遮罩保持原样。
- 验证：`tools\build.ps1` 0 warning / 0 error；按用户要求未做视觉验证（visualVerify=false）。
