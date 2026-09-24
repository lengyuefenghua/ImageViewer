# ImageViewer

独立、完全自包含的 Windows 图片查看器：以单个图片路径为命令行参数打开一个轻量看图窗口，可多开、关闭窗口即退出，不依赖任何规则/索引/外部服务。

## 主要能力

- **命令行看图**：`ImageViewer.exe "<图片路径>"` 直接打开 jpg/png/bmp；双击 exe（无参数）打开空白窗口，右键菜单「打开图片...」选择文件。
- **同目录浏览**：扫描图片所在目录的直接子项（非递归，白名单 jpg/png/bmp），按资源管理器式自然顺序排列，←/→ 翻页。
- **视口交互**：适配窗口、指针中心滚轮缩放、拖拽平移、双击在适应↔100% 之间切换。
- **缩略图列表**：右侧纯内存缩略图（不落盘），当前项高亮、点击切换、可折叠。
- **底栏信息**：尺寸×位深、序号/总数、缩放%、文件大小/解码内存、修改时间、指针坐标与 RGB。
- **全屏与窗口记忆**：F11 与标题栏双击全屏，记住窗口大小/位置/最大化状态。
- **文件关联**：首次运行会提示是否设为默认图片查看器；也可在右键菜单「设置...」窗口中注册/取消 jpg/png/bmp 的打开候选（机器级 HKLM 需管理员授权）并打开系统默认应用设置。

## 系统要求

- Windows 10 (1803+) 或 Windows 11，x64
- .NET Framework 4.7.2

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET Framework 4.7.2（x64） |
| UI | WPF + [WPF UI](https://github.com/lepoco/wpfui)（`Wpf.Ui` 4.2.0） |
| 日志 | 内置文件日志（`%AppData%\ImageViewer\Logs\`，无第三方依赖） |
| 测试 | xUnit |

## 项目结构

```
ImageViewer.csproj        应用项目（位于仓库根）
App.xaml / App.xaml.cs    应用入口、首启引导
Standalone/               查看器窗口、目录扫描、缩略图、窗口状态、命令行参数
Services/                 解码、文件关联
Imaging/                  视口解码管线
ViewModels/ Views/        视图模型与设置窗口
Runtime/ Configuration/   日志、路径、配置
Diagnostics/              日志抽象
tests/ImageViewer.Tests   单元测试
tools/                    构建 / 测试 / 运行 / 发布脚本
.github/workflows/        tag 触发的发布工作流
```

## 构建与运行

优先使用 `tools\` 下的脚本（应用运行时会锁住 `build\<Configuration>`）：

```powershell
tools\build.ps1          # 构建 Debug
tools\run-app.ps1        # 构建并启动（-NoBuild 跳过构建）
```

构建产物输出到 `build\Debug\ImageViewer.exe`。

## 测试

```powershell
tools\test.ps1 -Build -Filter <class-or-method>   # 聚焦测试
tools\test.ps1                                    # 全量测试
```

## 发布打包

版本号来自 git tag，构建 Release x64 并输出 `dist\ImageViewer-<version>-win-x64.zip`。

```powershell
tools\release.ps1 [-Tag vX.Y.Z] [-CreateTag]
```

推送到远程的 tag（`v*`）会触发 `.github/workflows/release.yml` 自动发布。

## 数据目录

用户配置（窗口大小/位置/最大化、首启偏好）写入 exe 同目录的 `ImageViewer.exe.config`；日志写入 `%AppData%\ImageViewer\Logs\`。最低日志级别可在设置窗口调整（保存为配置文件的 `Logging.MinimumLevel`）。
