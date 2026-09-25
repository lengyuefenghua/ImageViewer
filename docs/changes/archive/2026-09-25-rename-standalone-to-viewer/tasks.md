# Tasks

> patch：消除 Standalone 迁移命名（目录/命名空间/类名/文案），运行时行为不变，跳过 TDD（无行为变更）。

## 1. 目录与命名空间

- [x] 1.1 `git mv Standalone Viewer`
- [x] 1.2 `git mv tests\ImageViewer.Tests\Standalone tests\ImageViewer.Tests\Viewer`
- [x] 1.3 源码 `namespace ImageViewer.Standalone` → `ImageViewer.Viewer`、`using ImageViewer.Standalone;` → `using ImageViewer.Viewer;`
- [x] 1.4 测试 `namespace ImageViewer.Tests.Standalone` → `ImageViewer.Tests.Viewer`

## 2. 类名与文件名

- [x] 2.1 `git mv Viewer\StandaloneViewerWindow.xaml Viewer\ViewerWindow.xaml`、`.cs` 同理；`StandaloneViewerWindow` → `ViewerWindow`（含 xaml `x:Class` 前缀）
- [x] 2.2 `git mv Viewer\StandaloneImageFiles.cs Viewer\ImageFileWhitelist.cs`；`StandaloneImageFiles` → `ImageFileWhitelist`（5 处调用）
- [x] 2.3 `App.xaml.cs` 引用 `new ViewerWindow(...)`

## 3. 迁移文案清理

- [x] 3.1 `Viewer\ViewerWindow.xaml.cs` 日志「独立查看器」→「查看器」
- [x] 3.2 注释去旧架构措辞：`ViewerWindow`、`ViewerImageDirectoryScanner`、`ImageFileWhitelist`、`CommandLineImageArgument`、`ViewerWindowStateStore`、`ViewerThumbnailListViewModel`、`Views\ImageViewerViewModel`（规则 / Everything / 主程序 / 运行缓存 / 复判 / 业务数据）
- [x] 3.3 `App.xaml.cs` 日志「启动模式判定：独立查看器」→「启动模式判定：查看器」

## 4. 文档同步

- [x] 4.1 `README.md` 项目结构 `Standalone/` → `Viewer/`
- [x] 4.2 `AGENTS.md` 目录列表/入口/扫描路径：`Standalone` → `Viewer`、`StandaloneViewerWindow` → `ViewerWindow`

## 5. 构建与测试

- [x] 5.1 运行 `tools\build.ps1` 编译通过，确认无旧命名残留
- [x] 5.2 运行 `tools\test.ps1 -Build`（应全绿）

## 6. Review

- [x] 6.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important

## 7. Smoke

- [x] 7.1 运行 `tools\run-app.ps1 -NoBuild`，用 `opencode_computer_wait_for` + `opencode_computer_screenshot` 确认正常启动（本变更不改 UI）

## 8. Self-review

- [x] 8.1 自审：`rg "Standalone"` 在源码/测试零命中（历史归档除外），命名空间/类名/目录已统一

## 9. Commit

- [x] 9.1 提交：`git add -A && git commit -m "refactor(rename-standalone-to-viewer): 消除 Standalone 迁移命名"`（只 commit 不 push）
