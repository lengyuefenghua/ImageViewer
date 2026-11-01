# ImageViewer

独立、完全自包含的 Windows 图片查看器：以单个图片路径为命令行参数打开一个轻量看图窗口，可多开、关闭窗口即退出，不依赖任何规则/索引/外部服务。

## 主要能力

- **命令行看图**：`ImageViewer.exe "<图片路径>"` 直接打开 jpg/jpeg/png/bmp；双击 exe（无参数）打开空白窗口，右键菜单「打开图片...」选择文件。
- **同目录浏览**：扫描图片所在目录的直接子项（非递归，白名单 jpg/jpeg/png/bmp），按资源管理器式自然顺序排列，←/→ 翻页。
- **跨目录浏览**：到当前目录首/尾时**询问**是否切到相邻的同级（有图片的）目录继续浏览；到端不循环。
- **多图/搜索结果浏览**：多选图片或搜索结果后用 ImageViewer 打开、拖拽到窗口、或 Ctrl+V 粘贴，即可按选择顺序连续左右浏览。
- **视口交互**：适配窗口、指针中心滚轮缩放、拖拽平移、双击在适应↔100% 之间切换。
- **缩略图列表**：右侧纯内存缩略图（不落盘），当前项高亮、点击切换、可折叠。
- **底栏信息**：尺寸×位深、序号/总数、缩放%、文件大小/解码内存、修改时间、指针坐标与 RGB。
- **全屏与窗口记忆**：F11 与标题栏双击全屏，记住窗口大小/位置/最大化状态。
- **主题**：设置窗口可切换深色 / 浅色 / 跟随系统，随改随生效并记忆。
- **复制到目标**：默认空格键（可在「设置」里改键）打开「复制到」窗口，按行管理目标目录与文件名后缀，可选时间戳，点行或「复制」即把当前图片复制过去。
- **文件关联**：首次运行会提示是否设为默认图片查看器；也可在右键菜单「设置...」窗口中注册/取消 jpg/jpeg/png/bmp 的打开候选（机器级 HKLM 需管理员授权）并打开系统默认应用设置。

## 系统要求

- Windows 10 (1803+) 或 Windows 11，x64
- .NET Framework 4.7.2

## 技术栈

| 层 | 选型 |
|---|---|
| 运行时 | .NET Framework 4.7.2（x64） |
| UI | WPF + [WPF UI](https://github.com/lepoco/wpfui)（`Wpf.Ui` 4.2.0） |
| 配置与日志 | 用户配置 `%AppData%\ImageViewer\settings.config`；内置文件日志写入 `%AppData%\ImageViewer\Logs\`，无第三方依赖 |
| 测试 | xUnit |

## 项目结构

```
ImageViewer.csproj        应用项目（位于仓库根）
App.xaml / App.xaml.cs    应用入口、首启引导
Services/                 解码、文件关联、浏览扫描、复制
Imaging/                  视口解码管线
Views/                    窗口与视图模型
Runtime/                  日志（抽象 + 实现）、路径、配置
tests/                    单元测试
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

版本号来自 git tag，构建 Release x64 并输出 ZIP 与 Inno Setup 安装器：`dist\ImageViewer-<version>-win-x64.zip`、`dist\ImageViewer-<version>-win-x64-setup.exe`。安装程序始终以管理员权限为这台电脑的所有用户安装到 `%ProgramFiles%\ImageViewer`；固定安装身份和不含版本号的目录支持后续版本原位升级。用户配置和日志分别位于 `%AppData%\ImageViewer\settings.config` 与 `%AppData%\ImageViewer\Logs\`；旧版 exe 旁 `ImageViewer.exe.config` 会在首次启动时复制迁移。安装与卸载均可静默运行：

```powershell
# 静默安装；需要管理员权限。
& .\ImageViewer-<version>-win-x64-setup.exe /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /SP-

# 静默卸载：从所有用户安装项取得实际路径；需要管理员权限。
$uninstallKey = '{E4A60292-839A-4C2B-B811-C22BBD3AA634}_is1'
$uninstall = Get-ItemProperty "HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\$uninstallKey"
$uninstaller = $uninstall.UninstallString.Trim('"')
& $uninstaller /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
```

安装器默认勾选“注册 ImageViewer 为图片打开方式”，安装后将 jpg/jpe/jpeg/jfif/png/bmp 注册为 ImageViewer 的打开候选并显示在系统“默认应用”列表中；Windows 10/11 不允许安装器可靠地强制修改默认应用，仍需用户在系统设置中确认默认图片查看器。卸载不会清除用户配置与 `%AppData%\ImageViewer\Logs\`。推送到远程的 tag（`v*`）会触发 `.github/workflows/release.yml` 自动发布 ZIP 与安装器。

## 数据目录

用户配置（窗口大小/位置/最大化、首启偏好）写入 `%AppData%\ImageViewer\settings.config`；旧版本 exe 同目录的 `ImageViewer.exe.config` 会在首次启动时复制迁移，原文件保留。日志写入 `%AppData%\ImageViewer\Logs\`。最低日志级别可在设置窗口调整（保存为配置文件的 `Logging.MinimumLevel`）。
