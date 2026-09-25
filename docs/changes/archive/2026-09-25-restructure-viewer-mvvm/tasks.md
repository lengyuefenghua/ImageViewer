# Tasks

> patch：解散 `Viewer` 目录按 MVVM 职责归位（Views/Runtime/Services）+ 测试目录扁平化 + 命名空间同步；纯结构重构，运行时行为不变，跳过 TDD。

## 1. 解散 Viewer 目录

- [x] 1.1 `git mv Viewer\ViewerWindow.xaml Viewer\ViewerWindow.xaml.cs Views\`
- [x] 1.2 `git mv Viewer\ViewerThumbnailListViewModel.cs Views\`
- [x] 1.3 `git mv Viewer\ViewerWindowStateStore.cs Viewer\CopySettingsStore.cs Runtime\`
- [x] 1.4 `git mv` 其余 9 个到 `Services\`：ViewerImageDirectoryScanner、SearchResultsProvider、ImageFileWhitelist、CommandLineImageArgument、ElevatedCommand、ImageCopyService、CopyFileName、CopyTarget、FolderPicker
- [x] 1.5 删除空目录 `Viewer\`（无日志点：纯文件移动，行为不变）

## 2. 测试目录扁平化与归位

- [x] 2.1 `git mv tests\ImageViewer.Tests\ImageViewer.Tests.csproj tests\ImageViewer.Tests.csproj`
- [x] 2.2 `git mv tests\ImageViewer.Tests\{Runtime,Services,Views}\ tests\`
- [x] 2.3 原 `tests\...\Viewer\` 8 个测试按被测类型归位：CopySettingsStoreTests/ViewerWindowStateStoreTests → `tests\Runtime`；CommandLineImageArgumentTests/CopyFileNameTests/ElevatedCommandTests/ImageCopyServiceTests/ViewerImageDirectoryScannerTests → `tests\Services`；ViewerThumbnailListViewModelTests → `tests\Views`
- [x] 2.4 删除残留 `tests\ImageViewer.Tests\`（含未跟踪 bin/obj 旧产物）

## 3. 命名空间与 using

- [x] 3.1 `ViewerWindow`、`ViewerThumbnailListViewModel` namespace → `ImageViewer.Views`
- [x] 3.2 `ViewerWindowStateStore`、`CopySettingsStore` namespace → `ImageViewer.Runtime`
- [x] 3.3 其余 9 个 namespace → `ImageViewer.Services`
- [x] 3.4 测试命名空间与目录对齐（`ImageViewer.Tests.Runtime/Services/Views`）
- [x] 3.5 各引用方 `using ImageViewer.Viewer;` 改为对应命名空间；`ViewerWindow.xaml` 的 `x:Class` → `ImageViewer.Views.ViewerWindow`
- [x] 3.6 运行 `tools\build.ps1` 编译通过，补齐缺失 using、无残留 `ImageViewer.Viewer`

## 4. 路径引用

- [x] 4.1 `ImageViewer.sln` 测试项目路径 → `tests\ImageViewer.Tests.csproj`
- [x] 4.2 `tests\ImageViewer.Tests.csproj` 的 `ProjectReference` → `..\ImageViewer.csproj`
- [x] 4.3 `tools\test.ps1` 测试项目路径 → `tests\ImageViewer.Tests.csproj`

## 5. 文档同步

- [x] 5.1 `README.md` 项目结构段：去掉 `Viewer/`，`tests/` 扁平
- [x] 5.2 `AGENTS.md` Architecture 段目录列表同步

## 6. 验证

- [x] 6.1 运行 `tools\build.ps1`（0 warning / 0 error）
- [x] 6.2 运行 `tools\test.ps1 -Build`（应 121/121）

## 7. Review

- [x] 7.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important

## 8. Smoke

- [x] 8.1 运行 `tools\run-app.ps1 -NoBuild`，用 `opencode_computer_wait_for` + `opencode_computer_screenshot` 确认正常启动（本变更不改 UI）

## 9. Self-review

- [x] 9.1 自审：`rg "ImageViewer\.Viewer"` 源码/测试零命中；源码目录为 Imaging/Runtime/Services/Views 四个；`tests\` 无中间层

## 10. Commit

- [x] 10.1 提交：`git add -A && git commit -m "refactor(restructure-viewer-mvvm): 按 MVVM 归位并扁平化测试目录"`（只 commit 不 push）
