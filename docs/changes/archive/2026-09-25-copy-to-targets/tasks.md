# Tasks

## 已确认决策
- 看图窗口快捷键（默认 `Space`，设置窗可改）打开「复制到」窗口；右键菜单加「复制到…」。
- 目标列表每行：序号 + 目录路径(+浏览) + 后缀 + [复制]；点击行直接复制，[复制] 按钮同效。
- 右上角工具条：新增 / 删除 / 上移 / 下移（作用于当前选中行）。
- 底部全局：时间戳格式（含「无」= 不追加，全局控制开关与格式）+ 同名冲突（询问/覆盖/跳过，全局）。
- `Esc` 或标题栏 X 关闭；无单独关闭按钮。
- 命名：`主干` +（时间戳? `_格式`）+ 后缀（原样）+ 扩展名。
- 配置存 exe 旁 `ImageViewer.exe.config` 的 appSettings 索引键；后缀每行独立，时间戳/冲突全局。
- visualVerify=false。

## 1. Tests (RED)

- [x] 1.1 新增 `tests\...\Standalone\CopyFileNameTests.cs`（命名规则）
- [x] 1.2 新增 `tests\...\Standalone\CopySettingsStoreTests.cs`（默认值 + 增删/顺序/全局项往返）
- [x] 1.3 新增 `tests\...\Standalone\ImageCopyServiceTests.cs`（复制/覆盖/跳过/取消/建目录）
- [x] 1.4 运行 `tools\test.ps1 -Build -Filter CopyFileNameTests`，确认 RED

## 2. Implementation (GREEN)

- [x] 2.1 `Standalone\CopyTarget.cs`：`CopyTarget`、`CopyConflict`、`CopyDecision`、`CopyResult`、`CopySettings`
- [x] 2.2 `Standalone\CopyFileName.cs`：纯命名函数（非法格式→不追加）
- [x] 2.3 `Standalone\CopySettingsStore.cs`：`Load/Save`（索引键，含 Order/Suffix/Format/Conflict/Shortcut）
- [x] 2.4 `Standalone\ImageCopyService.cs`：复制 + 冲突策略 + 日志
- [x] 2.5 运行 `tools\test.ps1 -Build`，确认 GREEN

## 3. UI

- [x] 3.1 `Views\CopyToWindow.xaml(.cs)`：列表 + 工具条 + 全局下拉 + 浏览 + 点行复制 + Esc 关闭
- [x] 3.2 `ImageViewer.csproj` 加 `System.Windows.Forms` 引用（FolderBrowserDialog）
- [x] 3.3 `Standalone\StandaloneViewerWindow.xaml.cs`：快捷键拦截 + 右键「复制到…」+ 打开窗口
- [x] 3.4 `Views\SettingsWindow.xaml(.cs)`：新增「复制」段 + 快捷键按键录制

## 4. 验证

- [x] 4.1 `tools\test.ps1 -Build` 全绿
- [x] 4.2 手动冒烟：设置快捷键、添加目标、点行复制、无图片时提示、Esc 关闭

## 5. Review / Self-review

- [x] 5.1 轻量审查本变更 diff，修复 Critical/Important
- [x] 5.2 自审：逐条对照范围与 tasks，确认无遗漏

## 6. Commit

- [ ] 6.1 提交（只 commit 不 push，待用户许可）
