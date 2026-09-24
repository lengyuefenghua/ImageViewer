# Repository Instructions

## Current Baseline
- ImageViewer 是一个独立、完全自包含的 Windows 图片查看器（WPF，.NET Framework 4.7.2，x64），以单个图片路径为命令行参数打开看图窗口，可多开、关窗即退。
- 本仓库不引用 ImageDataViewer 的任何项目、程序集或数据：所有看图、解码、日志与序列化实现都自带于此仓库；运行时数据写入 `%AppData%\ImageViewer\`。
- 用 `tools\build.ps1` 构建、`tools\test.ps1 [-Filter <class-or-method>]` 跑测试（默认 `--no-build`，先构建）、`tools\run-app.ps1` 启动、`tools\release.ps1 [-Tag vX.Y.Z] [-CreateTag]` 发布（打包 `dist\ImageViewer-<version>-win-x64.zip`）。
- 应用运行时会锁住 `build\<Configuration>`，脚本会先停止 `ImageViewer` 进程再构建。

## Architecture
- `src\ImageViewer.App`：唯一应用项目。`Standalone`（查看器窗口/目录扫描/缩略图/窗口状态/参数解析）、`Services`（解码、文件关联）、`Imaging`（视口解码管线）、`ViewModels`（看图视图模型）、`Diagnostics`/`Runtime`（日志与路径）。
- 图片枚举不走 Everything：查看器只扫描图片所在目录的直接子项（jpg/png/bmp，非递归，资源管理器式自然排序）。
- 所有 UI 控件使用 WPF UI（`Wpf.Ui`）自带控件及其隐式样式。
- 查 WPF UI 控件 API 直接读本地源码 `C:\Users\wtg\Desktop\agent\wpfui-4.3.0`，不要凭记忆猜属性名。

## Workflow
- Lazy Flow 分级：`tiny` / `patch` / `full`。行为变更优先 TDD。
- 宣称完成前，在当前轮内运行相关验证并读取输出。
- 变更归档需要 ADR（full 必做；patch 有真实取舍才写）；tiny 免。

## Logging
- 所有代码变更需在关键位置加日志埋点：外部边界、异常、状态转换、生命周期、公共契约。
- 级别：异常/失败 = `Error`/`Fatal`；关键路径 = `Warn`；诊断 = `Info`/`Debug`。
- 关键处（非显然意图、不变量、边界、反直觉分支）保留中文注释。
