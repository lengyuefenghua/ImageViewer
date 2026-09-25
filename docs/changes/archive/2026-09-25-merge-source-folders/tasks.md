# Tasks

> patch：纯结构重构（移动目录 + 统一命名空间），运行时行为不变，跳过 TDD（无行为变更）。

## 1. 移动文件与目录

- [x] 1.1 `git mv Configuration\AppSettingsFile.cs Runtime\AppSettingsFile.cs`
- [x] 1.2 `git mv` 4 个 Diagnostics 文件到 `Runtime\`：`Diagnostics.cs`、`ILogSink.cs`、`LogSeverity.cs`、`NullLogSink.cs`
- [x] 1.3 `git mv ViewModels\BindableViewModel.cs Views\BindableViewModel.cs`；`git mv ViewModels\Viewer\ImageViewerViewModel.cs Views\ImageViewerViewModel.cs`
- [x] 1.4 `git mv tests\ImageViewer.Tests\Viewer tests\ImageViewer.Tests\Views`
- [x] 1.5 删除空目录 `Configuration\`、`Diagnostics\`、`ViewModels\`（含 `ViewModels\Viewer\`），确认无残留（无日志点：纯文件移动，行为不变）

## 2. 统一命名空间

- [x] 2.1 `Runtime\Diagnostics.cs`、`ILogSink.cs`、`LogSeverity.cs`、`NullLogSink.cs`：`namespace ImageViewer.Core.Diagnostics` → `ImageViewer.Runtime`
- [x] 2.2 `Runtime\AppSettingsFile.cs`：`namespace ImageViewer.Configuration` → `ImageViewer.Runtime`
- [x] 2.3 `Views\BindableViewModel.cs`、`Views\ImageViewerViewModel.cs`：`namespace ImageViewer.ViewModels[.Viewer]` → `ImageViewer.Views`
- [x] 2.4 `tests\ImageViewer.Tests\Views\ImageViewerViewModelTests.cs`：`namespace ImageViewer.Tests.Viewer` → `ImageViewer.Tests.Views`（无日志点：命名空间重命名，行为不变）

## 3. 同步 using 引用

- [x] 3.1 全局 `using ImageViewer.Core.Diagnostics;` → `using ImageViewer.Runtime;`；`ImageViewer.Runtime` 命名空间文件内删除自引用、同文件重复 using 去重
- [x] 3.2 全局 `using ImageViewer.Configuration;` → `using ImageViewer.Runtime;`；同命名空间内删除自引用
- [x] 3.3 `using ImageViewer.ViewModels;` / `using ImageViewer.ViewModels.Viewer;` → `using ImageViewer.Views;`；`ImageViewer.Views` 命名空间文件内删除自引用
- [x] 3.4 运行 `tools\build.ps1` 编译通过，确认无遗漏 using/旧命名空间

## 4. 文档同步

- [x] 4.1 更新 `README.md`「项目结构」段：8→5 目录，去掉 Configuration/Diagnostics/ViewModels
- [x] 4.2 更新 `AGENTS.md` Architecture 段目录列表，并删除 Gotchas 中「Diagnostics 命名空间不一致」条目

## 5. Review

- [ ] 5.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [ ] 5.2 运行最终验证：`tools\build.ps1` + `tools\test.ps1 -Build`（应全绿）

## 6. Smoke

- [ ] 6.1 运行 `tools\run-app.ps1 -NoBuild`，用 `opencode_computer_wait_for` 等窗口就绪并 `opencode_computer_screenshot` 截图，确认应用正常启动（非界面规格比对：本变更不改 UI）

## 7. Self-review

- [ ] 7.1 自审：逐条对照本变更范围与 `tasks.md`，确认无残留旧命名空间、空目录、旧 using（`rg "ImageViewer\.(Core\.Diagnostics|Configuration|ViewModels)"` 无命中）

## 8. Commit

- [ ] 8.1 提交：`git add -- Configuration Diagnostics ViewModels Views Runtime tests README.md AGENTS.md docs/changes/2026-09-25-merge-source-folders` 加实际移动结果后 `git commit -m "refactor(merge-source-folders): 合并源码目录并统一命名空间"`（只 commit 不 push）
