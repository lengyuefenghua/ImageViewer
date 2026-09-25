# Tasks

> patch：崩溃/卡死/内存风险加固。行为不变的部分无需 TDD；B2 复制异步化调整既有测试。

## 1. 崩溃加固

- [x] 1.1 `App.xaml.cs`：`PromptForDefaultViewerAsync` 整体包 try/catch（日志点：Error，上下文=首启引导），`OpenDefaultAppsSettings`/`Register` 失败不再冒泡
- [x] 1.2 `Views\ViewerWindow.xaml.cs`：`RequestAdjacentDirectory` 加 `isClosed` 短路，`await` 后与 `ApplyResultSet` 包 try/catch（日志点：Error）
- [x] 1.3 `App.xaml.cs ShowViewer`：`new ViewerWindow/Show` 包 try/catch（日志点：Error），失败 `Shutdown`
- [x] 1.4 `Services\FolderPicker.cs`：`GetResult` 后判 `result == null` 再调用（日志点：无，静默返回 null 已有）

## 2. 删除死代码

- [x] 2.1 删除 `Imaging\Viewport\ViewportDecoder.cs`、`ViewportImageDecoder.cs`、`ViewportRenderPipeline.cs` 与 `Imaging\Wic\JpegViewportDecoder.cs`、`PngViewportDecoder.cs`（无引用；保留 `ViewportState.cs`）
- [x] 2.2 运行 `tools\build.ps1` 确认删除后编译通过

## 3. UI 冻结加固

- [x] 3.1 `Services\SearchResultsProvider.cs`：加 `Stopwatch` 时间预算（300ms），超时回退（日志点：Debug，上下文=搜索结果读取超时）
- [x] 3.2 `Services\ImageCopyService.cs`：`Copy` → `CopyAsync`（`Task.Run` 复制，保留冲突/取消语义）；`Views\CopyToWindow.xaml.cs CopyRow` 改 `async`，复制中禁用按钮/提示
- [x] 3.3 `tests\Services\ImageCopyServiceTests.cs`：适配 `CopyAsync` 并运行聚焦测试
- [x] 3.4 `Views\ImageViewerViewModel.cs TrySampleRgb`：复用 `byte[4]` 缓冲，常见格式直接解析（无日志点：性能细节）

## 4. 启动/导航异步化

- [x] 4.1 `Views\ViewerWindow.xaml.cs`：`OnLoaded/LoadStartupPaths/LoadDirectoryContext/LoadExplicitList` 改 async，`ReadDimensions`+目录扫描入 `Task.Run`，结果回 UI（日志点：Error 捕获异常）
- [x] 4.2 `FindAdjacentDirectory` 目录扫描异步化；`Navigate` 调用点适配
- [x] 4.3 运行 `tools\build.ps1` + `tools\test.ps1 -Build` 确认无回归

## 5. 内存 / 资源

- [x] 5.1 `Views\ImageViewerViewModel.cs`：引入 `SemaphoreSlim(1,1)` 解码闸门 + generation，过期请求跳过（日志点：Debug，上下文=过期解码请求跳过）
- [x] 5.2 `Views\ViewerWindow.xaml.cs OnClosed`：`ThemeApplier.FollowSystem(this,false)`、`folderToastTimer?.Stop()`、`HwndSource.RemoveHook`（保存 hook 引用）

## 6. 验证

- [x] 6.1 运行 `tools\build.ps1`（0 warning / 0 error）
- [x] 6.2 运行 `tools\test.ps1 -Build`（应全绿）

## 7. Review

- [x] 7.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important

## 8. Self-review

- [x] 8.1 自审：逐条对照本 tasks 与修复清单，确认 A/B/C/D 全部落地、无遗漏

## 9. Commit

- [x] 9.1 提交：`git add -A && git commit -m "fix(harden-crash-freeze-memory): 加固崩溃/卡死/内存风险并删除死代码"`（只 commit 不 push；是否提交见 finish-change）
