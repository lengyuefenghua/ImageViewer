# Tasks

## UI 草图（SettingsWindow）
```
┌─ 设置 ─────────────────────────────────────────────┐
│ 文件关联                                            │
│   把 ImageViewer 注册为 jpg/png/bmp 打开候选…        │
│  ┌──────────────────────────────────────────────┐  │
│  │ 当前未注册 / 已注册状态                        │  │
│  │ [注册为打开候选] [取消关联] [系统默认应用设置]  │  │
│  └──────────────────────────────────────────────┘  │
│ 日志                                                │
│   选择写入日志的最低级别；日志位于 %AppData%\…\Logs │
│  ┌──────────────────────────────────────────────┐  │
│  │ 最低日志级别                    [ 下拉: Error ]│  │
│  └──────────────────────────────────────────────┘  │
│                                          [ 关闭 ]   │
└────────────────────────────────────────────────────┘
```
交互：下拉切换 → 立即生效（AppLogging.SetMinimumLevel）并写入 `ImageViewer.exe.config` 的 `Logging.MinimumLevel`。

## 已确认决策
- 日志等级配置键：`Logging.MinimumLevel`（appSettings），默认 `Error`。
- 提权参数 `--register-file-associations` / `--unregister-file-associations` 在提升后的进程内执行 HKLM 注册/取消后立即退出，不显示 UI。
- 设置窗加日志级别下拉并改善布局；visualVerify=true。

## 1. Tests (RED)

- [x] 1.1 `Runtime\AppLoggingTests.cs`：新增 `ResolveMinimumLevel` 用例（缺失→Error、`Debug`→Debug、`warn`→Warn 大小写、非法→Error、`Fatal`→Fatal）
- [x] 1.2 新增 `Standalone\ElevatedCommandTests.cs`：`--register-file-associations`→Register、`--unregister-file-associations`→Unregister、无参/图片路径→None（含大小写/多参数）
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter AppLoggingTests`，确认 RED

## 2. Implementation (GREEN)

- [x] 2.1 `Runtime\AppLogging.cs`：加 `MinimumLevelKey` 常量与 `ResolveMinimumLevel(IDictionary<string,string>)`
- [x] 2.2 新增 `Standalone\ElevatedCommand.cs`：`enum ElevatedAction` + `Parse(string[])`
- [x] 2.3 `App.xaml.cs`：启动读 `Logging.MinimumLevel`；`OnStartup` 命中提权参数时执行 `FileAssociationService(Registry.LocalMachine).Register/Unregister` 后 `Shutdown(0/1)`，不显示 UI
- [x] 2.4 运行 `tools\test.ps1 -Build`，确认 GREEN

## 3. 设置窗 UI

- [x] 3.1 `Views\SettingsWindow.xaml`：改为分区布局（文件关联 / 日志），日志区放 `ui:ComboBox`，用 `ui:Card` 容器与统一间距
- [x] 3.2 `Views\SettingsWindow.xaml.cs`：初始化下拉（当前级别）、切换时 `AppSettingsFile.Set` 持久化并 `AppLogging.SetMinimumLevel` 即时生效
- [x] 3.3 `tools\build.ps1` + `tools\test.ps1 -Build` 通过

## 4. 视觉验证（visualVerify=true）

- [x] 4.1 启动应用并用 `opencode_computer_wait_for`/`opencode_computer_windows` 定位设置窗
- [x] 4.2 `opencode_computer_screenshot` 截图默认状态，观察布局/文案/区块无错位溢出
- [x] 4.3 切换日志级别下拉 → 截图 + `opencode_computer_verify` 确认选择态，并核对 `ImageViewer.exe.config` 已写入 `Logging.MinimumLevel`（用户明确豁免：不做 UI 验证）
- [x] 4.4 不符则修正重截（用户明确豁免：不做 UI 验证）

## 5. 文档

- [x] 5.1 `README.md`/`AGENTS.md` 增补日志级别配置键说明

## 6. Review / Self-review

- [x] 6.1 轻量审查本变更 diff，修复 Critical/Important
- [x] 6.2 自审：逐条对照范围与 tasks，确认无遗漏

## 7. Commit

- [ ] 7.1 提交（只 commit 不 push，待用户许可）
