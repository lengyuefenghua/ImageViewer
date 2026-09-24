# Tasks

## 1. 移动与重命名

- [x] 1.1 `git mv` 应用文件平铺到根：`App.xaml`、`App.xaml.cs`、`ImageViewer.ico`、`ImageViewer.App.csproj`→`ImageViewer.csproj`，以及 `Configuration Diagnostics Imaging Runtime Services Standalone ViewModels Views` 8 个源码目录
- [x] 1.2 `git mv src\ImageViewer.sln ImageViewer.sln`
- [x] 1.3 `git mv tests\ImageViewer.App.Tests tests\ImageViewer.Tests`，csproj 重命名为 `ImageViewer.Tests.csproj`
- [x] 1.4 删除残留 `src\`（含未跟踪 `bin\obj`）

## 2. 引用与命名更新

- [x] 2.1 全局替换 `.cs`/`.xaml`：先 `ImageViewer.App.Tests`→`ImageViewer.Tests`，再 `ImageViewer.App`→`ImageViewer`（无日志点：纯重命名，运行时行为不变）
- [x] 2.2 `ImageViewer.csproj`：`RootNamespace`→`ImageViewer`、`InternalsVisibleTo`→`ImageViewer.Tests`、`OutputPath`→`build\$(Configuration)\`，并加 `tests\**` 的 Compile/Page/ApplicationDefinition/None 排除
- [x] 2.3 `tests\ImageViewer.Tests\ImageViewer.Tests.csproj`：`RootNamespace`→`ImageViewer.Tests`、`ProjectReference`→`..\..\ImageViewer.csproj`
- [x] 2.4 `ImageViewer.sln`：项目名与路径更新为根布局
- [x] 2.5 `tools\build.ps1`、`tools\release.ps1` 的 `src\ImageViewer.sln`→`ImageViewer.sln`；`tools\test.ps1` 测试 csproj 路径
- [x] 2.6 `README.md`、`AGENTS.md` 更新项目结构与命名空间说明

## 3. 验证

- [x] 3.1 运行 `tools\build.ps1`，确认成功且产物为 `build\Debug\ImageViewer.exe`
- [x] 3.2 运行 `tools\test.ps1 -Build`，确认全绿（证明 tests 未被吸入应用项目、InternalsVisibleTo 生效）
- [x] 3.3 运行 `tools\run-app.ps1 -NoBuild` 冒烟启动
- [x] 3.4 `git status` 确认识别为 rename、`src\` 消失

## 4. Review

- [x] 4.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 4.2 最终验证：`tools\build.ps1` + `tools\test.ps1 -Build`

## 5. Self-review

- [x] 5.1 自审：逐条对照本变更范围与 `tasks.md`，确认无遗漏、无残留旧命名

## 6. Commit

- [ ] 6.1 提交：`git add -- <该变更相关文件> && git commit -m "refactor(flatten-and-rename-project): 应用文件平铺到根并去掉 .App 命名"`（只 commit 不 push）
