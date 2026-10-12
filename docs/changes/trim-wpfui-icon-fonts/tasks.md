# Tasks

变更级别：patch。范围：发布期裁剪 `Wpf.Ui.dll` 中未使用的两个 Fluent 图标字体，缩小发布体积；不改应用运行时代码、XAML、用户配置。`visualVerify=false`、`designDemo=false`、不写 ADR。

## 1. 剥离工具

- [x] 1.1 新增 `tools\WpfUiFontStripper\WpfUiFontStripper.csproj`（net8.0 console，`Mono.Cecil` PackageReference，`PrivateAssets=all`）
- [x] 1.2 新增 `tools\WpfUiFontStripper\Program.cs`：读入指定 `Wpf.Ui.dll`，从内嵌资源 `Wpf.Ui.g.resources` 删除 `resources/fonts/fluentsystemicons-regular.ttf` 与 `resources/fonts/fluentsystemicons-filled.ttf`，原样保留其余条目后写回；stdout 打印前后字节数与被删条目；缺失任一预期条目时打印 stderr 并以非零码退出（无测试：构建期工具，靠结构校验 + 应用启动冒烟验证）
- [x] 1.3 修改 `ImageViewer.csproj`：追加 `<Compile/Page/ApplicationDefinition/None Remove="tools\**" />`，避免工具源码被应用的 SDK 默认 glob 吸入
- [x] 1.4 运行 `tools\build.ps1 -Configuration Release` 产出原始产物
- [x] 1.5 运行 `dotnet run --project tools\WpfUiFontStripper -c Release -- build\Release\Wpf.Ui.dll`，确认打印体积报告、退出码 0、DLL 显著变小

## 2. 接入发布脚本

- [x] 2.1 修改 `tools\release.ps1`：在拷贝到暂存目录后、`Compress-Archive` 前，对 `$PublishDirectory\Wpf.Ui.dll` 运行 stripper；非零退出即 `throw`（日志点：stdout 打印裁剪前后体积）
- [x] 2.2 运行 `tools\release.ps1 -Tag v1.1.6 -OutputDirectory "$env:TEMP\iv-trim-check"`，确认 zip 生成且体积约 0.6MB

## 3. 验证

- [x] 3.1 结构校验：用 `ResourceReader` 枚举补丁后 DLL 的 `Wpf.Ui.g.resources`，确认两个 `*.ttf` 已消失且 BAML 条目仍为 107
- [x] 3.2 运行时冒烟：把补丁后的 `Wpf.Ui.dll` 放入 `build\Release`，用测试图启动 `ImageViewer.exe`，确认进程存活 ≥3s 且 `%AppData%\ImageViewer\Logs\` 当日日志无 `Error`/`Fatal`
- [x] 3.3 运行 `tools\test.ps1 -Build`，确认既有测试全绿

## 4. Review

- [x] 4.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 4.2 最终验证：`tools\test.ps1 -Build` + `tools\release.ps1 -Tag v1.1.6 -OutputDirectory "$env:TEMP\iv-trim-check"` 体积达标

## 5. Self-review

- [x] 5.1 自审：逐条对照本变更目标，确认剥离生效、无残留字体、无遗漏

## 6. Commit

- [x] 6.1 [人工] 提交：`git add -- ImageViewer.csproj tools/WpfUiFontStripper tools/release.ps1 docs/changes/trim-wpfui-icon-fonts && git commit -m "build(trim-wpfui-icon-fonts): 发布期裁剪 Wpf.Ui 未使用的图标字体"`（只 commit 不 push）
