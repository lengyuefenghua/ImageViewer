# Tasks

## 已确认决策
- 窗口状态 + 首启偏好统一存入 exe 旁标准 `ImageViewer.exe.config` 的 `<appSettings>`，用 .NET 内置 `ConfigurationManager`（不新增第三方依赖）。
- 停止构建生成 `.exe.config`（`GenerateSupportedRuntime=false` + `AutoGenerateBindingRedirects=false`），并移除改名 target，避免构建覆盖用户配置。
- 旧 `%AppData%\ImageViewer\Configuration\` 下的 JSON 不迁移、忽略；日志仍写 `%AppData%\ImageViewer\Logs\`。
- exe 目录不可写时：忽略 + Debug 日志，本次会话用默认值。

## 1. Tests (RED)

- [x] 1.1 `ViewerWindowStateStoreTests`：path 扩展名改 `.config`，corrupt 用例改非法 config 内容，新增「偏好已写入后保存窗口状态不破坏偏好」
- [x] 1.2 `ViewerPreferencesStoreTests`：path 改 `.config`，新增「窗口状态已写入后保存偏好不破坏窗口状态」
- [x] 1.3 `ImageViewerPathsTests`：断言 `ImageViewerPaths.ConfigFilePath` 指向 exe 目录下 `ImageViewer.exe.config`，移除 `ConfigurationDirectory` 断言
- [x] 1.4 运行 `tools\test.ps1 -Build -Filter ViewerWindowStateStoreTests`，确认 RED（`ConfigFilePath`/新签名不存在或断言失败）

## 2. Implementation (GREEN)

- [x] 2.1 新增 `Configuration\AppSettingsFile.cs`：基于 `ConfigurationManager.OpenMappedExeConfiguration` 读写指定路径 config 的 appSettings（静态锁串行化读改写；无日志点，失败由调用方按 Debug 记录）
- [x] 2.2 重写 `Standalone\ViewerWindowStateStore.cs`：用 appSettings 键 `Window.Width/Height/Left/Top/IsMaximized`，保留 `IsOnScreen`
- [x] 2.3 重写 `Runtime\ViewerPreferencesStore.cs`：用键 `Preferences.DefaultViewerPromptDismissed`
- [x] 2.4 `Runtime\ImageViewerPaths.cs`：移除 `ConfigurationDirectory`，新增 `ConfigFilePath`（exe 目录 + `ImageViewer.exe.config`），保留 `LogsDirectory`
- [x] 2.5 更新调用点：`Standalone\StandaloneViewerWindow.xaml.cs`、`Services\ViewerAssociationService.cs` 改用 `ImageViewerPaths.ConfigFilePath`
- [x] 2.6 `ImageViewer.csproj`：移除 `RenameAppConfig` target；加 `GenerateSupportedRuntime=false`、`AutoGenerateBindingRedirects=false`；加 `System.Configuration` 引用
- [x] 2.7 删除 `Configuration\AtomicJsonFile.cs` 与 `Services\JsonSerialization.cs`（无剩余引用）；同步清理 csproj/tests csproj 中因此不再需要的引用
- [x] 2.8 运行 `tools\test.ps1 -Build`，确认 GREEN

## 3. 验证

- [x] 3.1 清 `build\Release` 后 `tools\build.ps1 -Configuration Release`，确认产物无 `.exe.config`、无 pdb
- [x] 3.2 启动应用并触发窗口关闭/首启偏好写入，确认 exe 旁生成 `ImageViewer.exe.config` 且含 `appSettings` 键，日志仍写 `%AppData%\ImageViewer\Logs\`
- [x] 3.3 运行 `tools\test.ps1 -Build` 全绿

## 4. 文档

- [x] 4.1 `README.md`「数据目录」段更新为：配置在 exe 旁 `ImageViewer.exe.config`，日志在 `%AppData%\ImageViewer\Logs\`
- [x] 4.2 `AGENTS.md` Current Baseline 同步

## 5. Review / Self-review

- [x] 5.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 5.2 自审：逐条对照本变更范围与 tasks，确认无遗漏、无 JSON/AtomicJsonFile 残留

## 6. Commit

- [ ] 6.1 提交：`git add -- <该变更相关文件> && git commit -m "refactor(config): 配置统一存入 exe 旁 ImageViewer.exe.config（内置 ConfigurationManager）"`（只 commit 不 push）
