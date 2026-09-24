# Tasks

## 已确认决策
- 主题选项：深色 / 浅色 / 跟随系统（`Appearance.Theme`，默认深色）。
- 全部随主题（含看图区背景/棋盘格/缩略图/标题栏）；看图 HUD 叠加层（加载/错误/底栏半透明遮罩）保持中性以保证可读性。
- 切换入口：设置窗「外观」下拉；改动即时生效并持久化。
- 跟随系统：`ApplySystemTheme()` + `SystemThemeWatcher.Watch(window, None)` 实时跟随。
- visualVerify=false。

## 1. Tests (RED)

- [x] 1.1 新增 `tests\...\Runtime\AppThemeStoreTests.cs`（默认 Dark、解析 Dark/Light/System 大小写、非法→Dark、往返）
- [x] 1.2 运行 `tools\test.ps1 -Build -Filter AppThemeStoreTests`，确认 RED

## 2. Implementation (GREEN)

- [x] 2.1 `Runtime\AppTheme.cs`：`enum AppTheme` + `AppThemeStore.Resolve/Load/Save`（键 `Appearance.Theme`）
- [x] 2.2 `Runtime\ThemeApplier.cs`：`Apply(AppTheme)` + `FollowSystem(Window, bool)`（Wpf.Ui）
- [x] 2.3 `App.xaml.cs`：启动应用主题；`ShowViewer` 去掉写死 Dark，改按配置，System 时跟随
- [x] 2.4 运行 `tools\test.ps1 -Build`，确认 GREEN

## 3. 设置窗

- [x] 3.1 `Views\SettingsWindow.xaml(.cs)`：新增「外观」段 + 主题下拉（深/浅/跟随系统），切换即应用+持久化+跟随

## 4. 主题画刷替换（硬编码颜色 → DynamicResource）

- [x] 4.1 `Views\SettingsWindow.xaml`、`Views\CopyToWindow.xaml`：窗口背景/标题栏/文字/选中色
- [x] 4.2 `Standalone\StandaloneViewerWindow.xaml`：窗口背景/标题栏/缩略图面板/棋盘格/选中色（HUD 遮罩保留）

## 5. 验证

- [x] 5.1 `tools\test.ps1 -Build` 全绿
- [x] 5.2 手动冒烟：切深/浅/跟随系统，窗口与看图区随之变化，重启保持

## 6. Review / Self-review

- [x] 6.1 轻量审查本变更 diff，修复 Critical/Important
- [x] 6.2 自审：逐条对照范围与 tasks

## 7. Commit

- [ ] 7.1 归档并提交（只 commit 不 push，待用户许可）
