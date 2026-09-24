# Tasks

## 已确认决策
- 同级文件夹切换：到当前目录首/尾时切到相邻的"有图片"同级目录；两端停止，不循环。
- 多图输入（多命令行参数 / 拖拽 / 粘贴）→ 连续浏览，保持传入顺序；显式列表不扫目录、不做同级切换。
- 尝试直读资源管理器搜索结果（Shell.Application，best-effort，独立可移除）。
- visualVerify=false。

## 1. Tests (RED)

- [x] 1.1 `ViewerImageDirectoryScannerTests`：新增同级目录枚举用例（含自身、自然排序、父目录为空时仅自身、无子目录）
- [x] 1.2 `CommandLineImageArgumentTests`：新增 `TryResolveAll` 用例（多路径保序、跳过无效、空白/空返回空）
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter ViewerImageDirectoryScannerTests`，确认 RED

## 2. Implementation (GREEN)

- [x] 2.1 `ViewerImageDirectoryScanner` 增加 `EnumerateSiblingDirectories(directory)`（自然排序，含自身）
- [x] 2.2 `CommandLineImageArgument` 增加 `TryResolveAll(args, out paths)`（保序、过滤无效）
- [x] 2.3 `StandaloneViewerWindow`：同级目录切换（边界时重扫相邻目录）+ 显式列表模式 + `AllowDrop`/`Drop` + `Ctrl+V` 粘贴
- [x] 2.4 `App.xaml.cs`：`ContinueStartup` 用 `TryResolveAll`，`ShowViewer` 接受多路径
- [x] 2.5 新增 `SearchResultsProvider`（Shell.Application 直读搜索结果），单路径启动时先尝试
- [x] 2.6 运行 `tools\test.ps1 -Build`，确认 GREEN

## 3. 验证

- [x] 3.1 `tools\test.ps1 -Build` 全绿
- [x] 3.2 启动冒烟（无参数/单张/多张均正常启动）；同级切换与拖拽/粘贴未做自动化手测（visualVerify=false）；搜索直读为尝试项，未实测

## 4. 文档

- [x] 4.1 `README.md` 补充同级文件夹切换、多图/拖拽/粘贴浏览

## 5. Review / Self-review

- [x] 5.1 轻量审查本变更 diff，修复 Critical/Important
- [x] 5.2 自审：逐条对照范围与 tasks

## 6. Commit

- [ ] 6.1 归档并提交（只 commit 不 push，待用户许可）
