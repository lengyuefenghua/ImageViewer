# Tasks

> 设计基线：Inno Setup（固定 AppId 与安装位置实现原位升级）；保留现有 ZIP，新增 x64 setup EXE；仅以管理员权限为所有用户安装到 Program Files；安装目录不含版本号。

## 1. Implementation

- [x] 1.1 更新安装器向导 Demo，仅展示管理员权限的所有用户安装、固定 Program Files 目录与确认摘要；静态脚本解析及残留范围选项检查通过；无测试：设计稿由用户人工验收。
- [x] 1.6 安装器强制管理员权限，仅为所有用户安装到 `%ProgramFiles%\ImageViewer`；已移除权限模式覆盖、当前用户目录回调及相关文档；PowerShell 脚本解析和安装器配置检查通过；无测试：安装脚本配置由真实 Release 编译验证。
- [x] 1.3 扩展 `tools\release.ps1`，从现有发布暂存目录构建 ZIP 与 `ImageViewer-<版本>-win-x64-setup.exe`，安装器缺失/失败时明确报错；无测试：打包脚本变更，使用真实 Release 包验证产物。
- [x] 1.4 固定 CI 使用的 Inno Setup 版本，并将 ZIP 与安装器一并附加到 GitHub Release；无测试：CI 配置变更，由工作流实际发布验证。
- [x] 1.7 将运行时设置文件迁移到 `%AppData%\ImageViewer\settings.config`，首次启动复制 exe 旁旧 `ImageViewer.exe.config`（如目标已存在则保留新设置），原文件保留；日志继续写入 AppData；路径测试 3/3 通过；本机安装后注入合法旧配置并启动应用，确认迁移值正确且旧文件保留。
- [x] 1.8 在安装器增加默认勾选的“注册 ImageViewer 为图片打开方式”任务；管理员步骤写 HKLM 关联声明，`runasoriginaluser` 步骤写当前用户 FileExts 打开候选，注册 jpg/jpeg/png/bmp 但不强制系统默认应用；README 和设计稿已同步，Inno Setup 6.7.3 Release 编译通过，关联命令聚焦测试 5/5 通过。`UninstallRun` 不支持 `runasoriginaluser`，因此不在卸载时冒险错误清理用户候选。

## 2. Verification

- [x] 2.1 运行 `tools\release.ps1 -Tag v1.2.0 -OutputDirectory "$env:TEMP\iv-installer-check-final"`，验证 ZIP 与 setup EXE 版本一致且 ZIP 内容/命名未回归。
- [ ] 2.2 [人工] 在管理员环境验证默认安装到 `%ProgramFiles%\ImageViewer`、原位升级、启动、旧 exe.config 到 AppData 的迁移、默认勾选的图片打开方式注册（`FileExts\OpenWithProgids` 中出现 `ImageViewer.Image`）、静默卸载与 AppData 设置保留。根因修复后，新构建的 `--register-user-file-association-candidates` 命令退出码为 0，且六种扩展名候选均存在；需要重新运行最新安装包才能使已安装旧版本获得该步骤。
- [ ] 2.3 [人工] 验收 `docs/changes/2026-10-21-windows-installer/design-demo.html` 与真实 Inno Setup 向导；用户明确自行进行 UI 验证，不调用 UI 自动化。

## 3. Review

- [ ] 3.1 加载 `review-change` 轻量审查本变更，修复 Critical/Important；新增默认文件关联注册任务后需要复审。
- [x] 3.2 运行最终验证：`tools\release.ps1 -Tag v1.2.0` 成功，Release 构建 0 警告/错误；Inno Setup 6.7.3 编译成功，ZIP 与 setup EXE 均生成到 `dist`。最近一次重建确认仅所有用户配置，ZIP 645,045 bytes，setup EXE 2,706,381 bytes。

## 4. Self-review

- [x] 4.1 自审：固定 AppId、双包发布和静默开关已实现；当前用户范围/迁移/卸载保留已实测，管理员所有用户范围及交互 UAC/向导仍为明确验收缺口。未提交或推送。

## 5. Commit

- [ ] 5.1 [人工] 人工验收通过且获得明确许可后提交本变更相关文件（只 commit 不 push；push 需单独许可）。
