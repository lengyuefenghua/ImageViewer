# Repository Instructions

## Current Baseline
- ImageViewer 是独立、完全自包含的 Windows 图片查看器（WPF，.NET Framework 4.7.2，x64）：以单个图片路径为命令行参数打开看图窗口，可多开，关闭窗口即退出。
- 本仓库不引用 ImageDataViewer 的任何项目、程序集或数据；日志写入 `%AppData%\ImageViewer\Logs\`，用户配置（窗口状态/首启偏好）写入 exe 同目录的 `ImageViewer.exe.config`。

## Commands
- Build: `tools\build.ps1 [-Configuration Debug|Release]` → 产物 `build\<Configuration>\ImageViewer.exe`
- Run: `tools\run-app.ps1 [-NoBuild]`（先停旧实例、构建、再启动）
- Test (all): `tools\test.ps1`（默认 `--no-build`，直接跑已构建产物）
- Test (build first): `tools\test.ps1 -Build`
- Single test: `tools\test.ps1 -Filter <class-or-method> [-Build]`
- Release: `tools\release.ps1 [-Tag vX.Y.Z] [-CreateTag]` → `dist\ImageViewer-<version>-win-x64.zip`
- 无独立 lint/typecheck；编译检查即 `dotnet build`（已由 `tools\build.ps1` 包装）

## Architecture
- 应用项目 `ImageViewer.csproj` 位于仓库根，源码平铺在同级目录：`Standalone`（查看器窗口/目录扫描/缩略图/窗口状态/命令行参数）、`Services`（解码、文件关联）、`Imaging`（视口解码管线）、`Views`（窗口与视图模型）、`Runtime`（日志抽象与实现、路径、配置）；测试在 `tests\ImageViewer.Tests`。
- 入口 `App.xaml.cs` 的 `OnStartup`：先处理首启「设为默认查看器」引导，再按命令行参数打开 `StandaloneViewerWindow`（无参数则打开空白窗口）。
- 图片枚举只扫描图片所在目录的直接子项（白名单 jpg/png/bmp，非递归，资源管理器式自然排序），不走 Everything/索引：`Standalone\ViewerImageDirectoryScanner.cs`。
- 全部 UI 使用 WPF UI（`Wpf.Ui`）自带控件及其隐式样式；查控件 API 读本地源码 `C:\Users\wtg\Desktop\agent\wpfui-4.3.0`，不要凭记忆猜属性名。
- CI：推送 `v*` tag 触发 `.github/workflows/release.yml`，版本号解析自 git tag。

## Workflow
- 开发请求先通过 skill 工具加载 `using-lazy-flow` 分级：`tiny`（文案/样式/小修复/局部优化，可跨文件不改契约；重要代码必须聚焦测试，不审查；三行记录到 `docs/changes/tiny-log.md`）/ `patch` / `full`（仅共享契约/架构级重组/数据迁移/高不确定，双轴审查）。行为变更优先 TDD。
- 实现、修复、重构、依赖选型与审查前加载 `lean-check`（`leanMode` 非 off 时）：保持最小正确 diff，优先复用与原生能力。
- full 变更实现默认 inline，仅任务可并行或需隔离上下文时按 `subagent-dev` 派发子代理；主控负责验证、审查与收尾。
- 界面/GUI 变更必须用自动化操作工具 `opencode_computer_*` 实际操作并截图，闭环对照规格；无真实截图证据不得声明完成。
- 声明完成前在当前轮运行相关验证命令并读取输出；推送前只跑本次改动相关的最小命令，禁止惯性全量。
- 仅当存在真实、长期值得记录的取舍时才写 `docs/adr/` 决策记录。

## 日志
- 所有行为变更必须评估并列出日志点（事件 + 级别 + 上下文）；外部边界、异常/降级/重试、状态转换、启动关闭、用户可见结果必须落地。优先用仓库已有 logger（`Diagnostics.Sink` / `AppLogging`，底层内置 `FileLogSink`，写 `%AppData%\ImageViewer\Logs\yyyy-MM-dd.log`），文案随仓库惯例（英文）。缺日志视为未完成。
- 级别：异常/失败 = `Error`/`Fatal`；关键路径 = `Warn`；诊断 = `Info`/`Debug`。禁止敏感数据、循环刷屏、无信息占位日志。
- 最低级别可在设置窗口切换，持久化在 `ImageViewer.exe.config` 的 `Logging.MinimumLevel`；缺失/非法回落 `Error`。

## 代码注释
- 注释用中文；默认不写，但关键处必须有注释（非显然意图/不变量/边界/反直觉分支/公共契约）；禁止执行过程叙述、复述代码、变更史。

## 文档层级
- 每个事实只有一个家：决策 → `docs/adr/`；行为契约 → `docs/specs/`；术语 → `CONTEXT.md`；需求确认 → `docs/changes/<change>/proposal.md`；变更过程 → `docs/changes/`；tiny 记录 → `docs/changes/tiny-log.md`；常设规则 → 本文件。别处引用用相对路径，不粘贴副本。

## Gotchas
- 构建产物在仓库根 `build\<Configuration>\`（csproj 覆写了 `OutputPath`），不是 `bin\`。
- 运行中的 ImageViewer 会锁住 `build\<Configuration>`；所有脚本构建前会先停止 `ImageViewer` 进程。
- `tools\test.ps1` 默认**不**构建（`--no-build`）；改完代码要跑测试必须加 `-Build`，否则测的是旧产物。
- 测试项目 `ImageViewer.Tests` 通过 `InternalsVisibleTo("ImageViewer.Tests")` 访问 `ImageViewer` 的 internal 成员。
- 应用 csproj 在仓库根，SDK 默认 glob 会递归吸入 `tests\**`，已在 `ImageViewer.csproj` 用 `Compile/Page/ApplicationDefinition/None Remove="tests\**"` 排除，勿删。
- 用户配置写在 exe 旁 `ImageViewer.exe.config`；csproj 设了 `GenerateSupportedRuntime=false` 以免构建覆盖用户配置，勿改回。
