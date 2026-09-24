# Tasks

## 1. Tests (TDD RED)

- [x] 1.1 `git mv tests\ImageViewer.Tests\Runtime\NLogLogSinkTests.cs tests\ImageViewer.Tests\Runtime\FileLogSinkTests.cs`，把 `NLogLogSink` 全部改为 `FileLogSink`
- [x] 1.2 `tests\ImageViewer.Tests\Runtime\AppLoggingTests.cs` 断言改为 `IsType<FileLogSink>`
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter FileLogSinkTests`，确认 RED 因 `FileLogSink` 不存在（CS0246）而失败

## 2. Implementation (GREEN)

- [x] 2.1 新增 `Runtime\FileLogSink.cs`（实现 `ILogSink`：每日 `yyyy-MM-dd.log`、`yyyy-MM-dd HH:mm:ss.ffff|级别|logger|message[|异常]`、级别过滤、`lock` 并发安全、`CreateDirectory`、失败吞掉）
- [x] 2.2 `Runtime\AppLogging.cs` 改用 `FileLogSink` 并更新注释；`git rm Runtime\NLogLogSink.cs`；`Diagnostics\ILogSink.cs` 注释去掉 NLog 措辞
- [x] 2.3 `ImageViewer.csproj` 移除 `NLog` 包引用
- [x] 2.4 运行 `tools\test.ps1 -Build -Filter FileLogSinkTests`，确认 GREEN

## 3. 输出精简

- [x] 3.1 `ImageViewer.csproj` 新增 Release-only `<DebugType>none</DebugType>` + `<DebugSymbols>false</DebugSymbols>`
- [x] 3.2 `ImageViewer.csproj` 新增 `AfterTargets="Build"` 的 target，把 `ImageViewer.exe.config`→`ImageViewer.config`
- [x] 3.3 运行 `tools\build.ps1 -Configuration Release`，列 `build\Release` 确认 8 文件、无 pdb、无 NLog.dll、有 `ImageViewer.config`

## 4. 文档

- [x] 4.1 `README.md` 技术栈表「NLog」改为内置文件日志
- [x] 4.2 `AGENTS.md` 日志段去掉 NLog 措辞，改述 `Diagnostics.Sink`/`AppLogging` + 内置 `FileLogSink`

## 5. 验证

- [x] 5.1 运行 `tools\test.ps1 -Build` 全绿
- [x] 5.2 运行 `tools\run-app.ps1 -NoBuild` 冒烟，并确认 `%AppData%\ImageViewer\Logs\` 写入当日日志

## 6. Review

- [x] 6.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 6.2 最终验证：`tools\build.ps1 -Configuration Release` + `tools\test.ps1 -Build`

## 7. Self-review

- [x] 7.1 自审：逐条对照本变更范围与 `tasks.md`，确认无遗漏、无 NLog 残留

## 8. Commit

- [ ] 8.1 提交：`git add -- <该变更相关文件> && git commit -m "refactor(trim-output-and-drop-nlog): 去 NLog 改内置文件日志并精简 Release 产物"`（只 commit 不 push）
