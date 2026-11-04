# Tasks

> patch 变更：修复 `CleanupLegacy32BitRegistration` 误删跨视图共享的 `HKLM\Software\Classes` 文件关联键，导致安装包与软件内注册双双无效；并让安装包在用户上下文清除旧默认选择（`UserChoice`），完成页提供「打开默认应用设置」选项。

## 1. Tests

- [x] 1.1 `tests/Services/FileAssociationServiceTests.cs`：改写 `Register_cleans_a_legacy_32bit_registration`，注入 32 位测试根并预置旧注册（断言前确认预置键存在），`Register` 后断言：32 位根下 `Software\ImageViewer` 树被清除；32 位根下 `Software\RegisteredApplications` 的 `ImageViewer` 值与无关值**均保留**；32 位根下 `Classes\ImageViewer.Image`、`Classes\Applications\ImageViewer.exe`、`Classes\.jpg\OpenWithProgids` 的 ProgId 值**必须保留**；`Assert.True(service.IsRegistered(exePath))`（后果：数据损坏/破坏系统运行——清理写错键会删掉跨视图共享的注册，用户侧表现为注册完全无效）
- [x] 1.2 `tests/Services/FileAssociationServiceTests.cs`：改写 `Unregister_cleans_a_legacy_32bit_registration`，删除原先「32 位根下 `Software\RegisteredApplications` 的 ImageViewer 值被删除」的断言；改为预置 32 位根下 `Software\ImageViewer\Capabilities` 树并在 `Unregister` 后断言该树被清除，同时断言 32 位根下 `Software\RegisteredApplications` 的 ImageViewer 值与 `Classes\ImageViewer.Image` **保留**（取消关联只清真正重定向的旧残留，不碰共享键）
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` 确认 RED 因「Classes 下的注册被 32 位清理删除」失败

## 2. Implementation

- [x] 2.1 `Services/FileAssociationService.cs` 的 `CleanupLegacy32BitRegistration`：删除对 `Software\Classes` 的一切删除分支（ProgID 树、`Applications\<exe>` 树、各扩展名 `OpenWithProgids` 值）与共享键 `Software\RegisteredApplications` 的 32 位删除，只保留真正被 WOW64 重定向的 `Software\ImageViewer` 树清理；方法改为无参并同步更新 `Register`/`Unregister` 两处调用，`DeleteSubKeyIfExists` 保留；加中文注释说明「Classes 文件关联键 32/64 视图共享，不可在 32 位视图删除」（日志点：确有清理时保留原 `Info` 文案「已清理 32 位视图的旧注册残留」；清理异常 `Warn` 保留；注册成功 `Warn` / 失败 `Error` 不变）
- [x] 2.2 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` 确认 GREEN
- [x] 2.3 运行 `tools\test.ps1 -Build` 确认全量测试无回归
- [x] 2.7 新增 `Services\UserChoiceHash.cs`（`internal static`）：移植 PS-SFTA（MIT）的 UserChoice Hash 算法（自定义 MD5 混合、本地时间截到分钟的 FILETIME 十六进制、从 shell32.dll 读取 “User Choice set via Windows User Experience {GUID}” 并回退到已知 GUID），供写入默认程序使用；中文注释注明来源与不变量
- [x] 2.8 `Services\FileAssociationService.cs` 新增 `SetDefaultViewer(string executablePath)`：仅 HKCU 根实例执行；每个扩展名 `RegDeleteKey(HKCU, FileExts\<ext>\UserChoice)` → 写 `Hash`/`ProgId` → 写 `ApplicationAssociationToasts` → 最后 `SHChangeNotify`，逐项回读校验并返回成功/失败计数（日志点：`Info` 成功项数、`Warn` 单项失败、`Error` 整体异常）
- [x] 2.9 `Services\ViewerAssociationService.cs` 注册流程改为调用 `SetDefaultViewer` 并返回准确文案（全部成功=已设为默认；部分失败=提示被拦截、去系统默认应用选择），新增 `IsDefaultViewer` 只读属性
- [x] 2.10 `App.xaml.cs` 用户级命令分支改为 `SetDefaultViewer`（替代 `ResetStaleUserChoices`）；首启引导与 `Views\SettingsWindow.xaml.cs` 仅在未成为默认时打开「默认应用」页
- [x] 2.11 `tests\Services\UserChoiceHashTests.cs`：用实测向量 `baseInfo=.jpgs-1-5-21-...-1001imageviewer.image01dd6c12c74e6e00user choice set via windows user experience {d18b6dd5-...}` → `eg1nwCkvu5U=` 断言算法（后果：静默错误结果——算错一位 Hash，Windows 会把默认重置）；`tests\Services\FileAssociationServiceTests.cs` 增加 `SetDefaultViewer` 写入 UserChoice 的用例（后果：破坏系统运行——写错键或漏写 Hash）
- [x] 2.12 运行 `tools\test.ps1 -Build` 并重新打包安装包
- [x] 2.13 `tools\ImageViewer.iss`：把两条 `[Run]` 注册项改为 `[Code]` 的 `CurStepChanged(ssPostInstall)`——机器级用 `Exec`、用户级用 `ExecAsOriginalUser`（静默安装没有向导步骤，`runasoriginaluser` 不可靠；`ExecAsOriginalUser` 两种模式都能拿到原始用户令牌），完成页设置页入口保留 `skipifsilent`，静默安装不弹页面
- [x] 2.14 `Views\ViewerWindow.xaml.cs`：首次 `ContentRendered` 后补救前台激活——`Activate` → `SetForegroundWindow` → 150 ms 间隔最多重试 3 次，最后一次用 `Topmost` 闪烁兜底；`Activated` 事件一到即停止重试，关闭时清理定时器（日志点：首次激活失败 `Info`、最终仍非前台 `Warn` 并带窗口句柄；无测试：Win32 前台焦点行为无法在单测稳定复现）

- [x] 2.4 `App.xaml.cs` 的 `RunAssociationCommand`（`ElevatedAction.RegisterUserCandidates` 分支）：注册用户级候选后追加 `ResetStaleUserChoices(executablePath)`，让安装包在用户上下文清除旧 `UserChoice`，否则装完打开图片仍走旧程序；属于接线，无测试：`ResetStaleUserChoices` 行为已由 `ResetStaleUserChoices_removes_legacy_values_and_foreign_user_choice` / `ResetStaleUserChoices_keeps_our_own_user_choice` 覆盖（日志点沿用服务内 `Info`/`Warn`，含清除数与失败数）
- [x] 2.5 `tools\ImageViewer.iss`：`[Run]` 增加一条 postinstall 项 `Filename: "ms-settings:defaultapps"`（`Description` 说明用它把 ImageViewer 设为图片默认程序，`Flags: postinstall shellexec nowait skipifsilent`，`Tasks: fileassociations`），安装结束页默认勾选，无测试：安装器 UI 文案与开关
- [x] 2.6 运行 `tools\test.ps1 -Build`，重新执行 `tools\release.ps1` 产出含本次改动的 1.2.1 安装包

## 3. Review

- [x] 3.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build -Filter FileAssociationServiceTests`

## 4. Manual acceptance

- [x] 4.0 版本提升：`ImageViewer.csproj` 的 `<Version>` 1.2.0 → 1.2.4；`tools\release.ps1 -Tag v1.2.4`（构建期临时 tag，产物完成后删除，待验收与提交后再正式打 tag）产出 `dist\ImageViewer-1.2.4-win-x64-setup.exe`；已实测静默安装六个扩展名默认选择全部生效

- [x] 4.1 [人工] 用户重装 `dist\ImageViewer-1.2.4-win-x64-setup.exe` 并勾选「注册 ImageViewer 为图片打开方式」，确认：装完双击 jpg/png 直接由 ImageViewer 打开；多次双击启动时窗口都在最前台；`reg query HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.jpg\UserChoice` 显示 `ProgId=ImageViewer.Image` 且 `Hash` 非空

## 5. Self-review

- [x] 5.1 自审：对照已确认方案（根因、改动范围、验证方式）与 `tasks.md` 逐条确认无遗漏，修复后重审

## 6. Commit

- [x] 6.1 [人工] 提交：`git add -- App.xaml.cs ImageViewer.csproj Services/FileAssociationService.cs Services/UserChoiceHash.cs Services/ViewerAssociationService.cs Views/SettingsWindow.xaml.cs tests/Services/FileAssociationServiceTests.cs tests/Services/UserChoiceHashTests.cs tests/Services/ViewerAssociationServiceTests.cs tools/ImageViewer.iss docs/changes/2026-11-04-association-shared-classes-cleanup && git commit -m "fix(association): 修复注册自删并直接写入默认程序"`（只 commit 不 push；push 需单独许可）
