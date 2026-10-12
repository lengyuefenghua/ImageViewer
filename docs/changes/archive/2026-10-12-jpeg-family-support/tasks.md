# Tasks

## 1. Tests

- [x] 1.1 `tests/Services/FileAssociationServiceTests.cs`：在 `Register_writes_open_command_supported_types_candidates_and_app_declarations` 中新增 jpeg 家族断言——`.jpe/.jpeg/.jfif` 出现在 SupportedTypes、Classes OpenWithProgids 与 Capabilities\FileAssociations（后果：行为回归——已实测复现：缺 jpeg 家族注册会让应用从系统「照片查看器」类别选择列表消失）
- [x] 1.2 `tests/Services/ViewerImageDirectoryScannerTests.cs`：`Scan_lists_only_whitelisted_images_without_recursing` 中加入 `.jpeg` 文件并断言被收录（后果：行为回归——jpeg 家族文件不再被扫描/浏览）
- [x] 1.3 `tests/Services/BitmapSourceLoaderTests.cs`：为 `.jpe/.jpeg/.jfif` 增加真实 JPEG 内容的解码用例（写临时文件时按扩展名选择 JPEG 编解码），断言 `ReadDimensions`/`LoadForDisplay` 成功——证明解码器按内容识别、jpeg 家族可解，非只放行扩展名（预期先绿：解码支持不依赖本次放行）
- [x] 1.4 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests`、`tools\test.ps1 -Filter ViewerImageDirectoryScannerTests`、`tools\test.ps1 -Filter BitmapSourceLoaderTests`，确认 1.1/1.2 RED 因预期原因失败、1.3 绿（解码已支持）

## 2. Implementation

- [x] 2.1 `Services/ImageFileWhitelist.cs`：白名单加入 `.jpe/.jpeg/.jfif`（类注释同步）——无日志点：纯静态判定
- [x] 2.2 `Services/FileAssociationService.cs`：`extensions` 加入 `.jpe/.jpeg/.jfif`；`ApplicationDescription` 与类注释的格式文案改为 `jpg/jpeg/png/bmp`——日志点：无新增（既有 Register 日志已输出扩展名清单）
- [x] 2.3 文案与过滤器同步：`Views/ViewerWindow.xaml.cs` 打开对话框过滤串（含 `*.jpe;*.jpeg;*.jfif`）；`App.xaml.cs` 首启弹窗、`Views/SettingsWindow.xaml` 说明、`Services/ViewerAssociationService.cs` 两条返回消息、`README.md` 全部格式清单改为 `jpg/jpeg/png/bmp`——无测试：文案/过滤器
- [x] 2.4 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests`、`tools\test.ps1 -Filter ViewerImageDirectoryScannerTests`、`tools\test.ps1 -Filter BitmapSourceLoaderTests` 确认 GREEN

## 3. Review

- [x] 3.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build`

## 4. 端到端人工验收

- [x] 4.1 [人工] 用新构建的 `build\Debug\ImageViewer.exe`：设置 → 「取消关联」→「注册并重置默认选择」，然后在系统「照片查看器 → 选择应用」列表确认 ImageViewer 仍出现（验证由新代码写入的注册生效，而非探针遗留）；并随便打开一张 `.jpeg` 确认可正常查看

## 5. Self-review

- [x] 5.1 自审：对照本变更需求（jpeg 家族支持 + 注册覆盖，修复类别列表缺失）与 tasks.md 逐条确认无遗漏（本 change 无 spec 产物）

## 6. Commit

- [x] 6.1 [人工] 提交：`git add -- Services/ImageFileWhitelist.cs Services/FileAssociationService.cs Services/ViewerAssociationService.cs Views/ViewerWindow.xaml.cs Views/SettingsWindow.xaml App.xaml.cs README.md tests/Services/FileAssociationServiceTests.cs tests/Services/ViewerImageDirectoryScannerTests.cs tests/Services/BitmapSourceLoaderTests.cs docs/changes/jpeg-family-support .opencode/lazy/state.json && git commit -m "feat(association): 支持并注册 jpeg 家族扩展名"`（只 commit 不 push；人工验收通过且用户明确许可后才执行）
