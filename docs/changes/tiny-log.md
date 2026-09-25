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

## 2026-09-26 底栏日期显示到秒、文件大小提精度
- 改动：`Views\ImageViewerViewModel.cs` 的 `ImageModifiedDisplay` 格式 `yyyy-MM-dd HH:mm` → `yyyy-MM-dd HH:mm:ss`（同分钟内切图不再看起来「日期不变」）；`FormatSize` 的 KB/MB/GB 由 1 位小数改为 2 位（相邻图片的文件大小差异可见）。
- 验证：`tools\test.ps1 -Filter ImageViewerViewModelTests -Build` → 8/8 通过（新增 `SetFileInfo` 用例断言含秒格式、文件大小与属性通知）；实机截图底栏切图前后 `08:01:05`→`08:01:42`、`8.04 KB`→`116.68 KB` 均刷新。

## 2026-09-26 修复主题切换时设置页背景不跟随
- 根因：`ui:FluentWindow` 创建时 `WindowBackdrop.RemoveBackdrop` 把窗口 `Background` 从 DynamicResource 冻结成固定画刷；Wpf.Ui 切主题只重建 MainWindow 背景，设置窗/复制到窗不重建，出现「浅底浅字」；另 `ViewerWindow.OnClosed` 在句柄销毁后调 `SystemThemeWatcher.UnWatch` 抛 “Could not get window handle.”。
- 改动：`Runtime\ThemeApplier.cs` 订阅 `ApplicationThemeManager.Changed`，主题变化时对非主窗口调 `WindowBackgroundManager.UpdateBackground` 重建背景（Debug 日志：主题+窗口数）；`FollowSystem` 的 `UnWatch` 加异常防护；`Views\ViewerWindow.xaml.cs` 把 `FollowSystem(this,false)` 从 `OnClosed` 移到 `OnClosing`（句柄销毁前取消订阅，避免异常与静态 watcher 列表泄漏）。
- 验证：`tools\build.ps1` 0 warning / 0 error；`tools\test.ps1` 122/122 通过；启动+`CloseMainWindow` 关窗冒烟后日志 `Could not get window handle` 计数不变（10→10，不再产生新异常）；按用户要求 visualVerify=false，未做界面截图。
