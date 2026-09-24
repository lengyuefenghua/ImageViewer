# ImageViewer

独立、完全自包含的 Windows 图片查看器：以单个图片路径为命令行参数打开一个轻量看图窗口，可多开、关闭窗口即退出，不依赖任何规则/索引/外部服务。

## 主要能力

- **命令行看图**：`ImageViewer.exe "<图片路径>"` 直接打开 jpg/png/bmp。
- **同目录浏览**：扫描图片所在目录的直接子项（非递归，白名单 jpg/png/bmp），按资源管理器式自然顺序排列，←/→ 翻页。
- **视口交互**：适配窗口、指针中心滚轮缩放、拖拽平移、双击在适应↔100% 之间切换。
- **缩略图列表**：右侧纯内存缩略图（不落盘），当前项高亮、点击切换、可折叠。
- **底栏信息**：尺寸×位深、序号/总数、缩放%、文件大小/解码内存、修改时间、指针坐标与 RGB。
- **全屏与窗口记忆**：F11 与标题栏双击全屏，记住窗口大小/位置/最大化状态。
- **文件关联**：右键菜单可把本程序注册为 jpg/png/bmp 的打开候选（机器级 HKLM 需管理员授权）并引导系统默认应用设置。

## 系统要求

- Windows 10 (1803+) 或 Windows 11，x64
- .NET Framework 4.7.2

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET Framework 4.7.2（x64） |
| UI | WPF + [WPF UI](https://github.com/lepoco/wpfui)（`Wpf.Ui` 4.2.0） |
| 日志 | NLog |
| 测试 | xUnit |

## 项目结构

```
src/
  ImageViewer.App   查看器窗口、看图视图模型、视口解码、文件关联、日志
tests/
  ImageViewer.App.Tests
tools/              构建 / 测试 / 运行 / 发布脚本
.github/workflows/  tag 触发的发布工作流
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

窗口状态与日志写入 `%AppData%\ImageViewer\`（`Configuration\viewer-window.json`、`Logs\`），与其它程序隔离。
