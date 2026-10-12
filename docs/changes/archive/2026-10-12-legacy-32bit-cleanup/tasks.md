# Tasks

## 1. Tests

- [x] 1.1 `tests/Services/FileAssociationServiceTests.cs` 新增 `Register_cleans_a_legacy_32bit_registration`：注入 32 位测试根并预置旧注册（`RegisteredApplications` 值、`ImageViewer\Capabilities`、`Classes\ImageViewer.Image`、`Classes\Applications\ImageViewer.exe`、`.jpg\OpenWithProgids` 值），`Register` 后断言全部删除且无关值保留（后果：数据损坏/破坏系统运行——清理写错路径会删错键或漏删旧注册留下悬挂项）
- [x] 1.2 `tests/Services/FileAssociationServiceTests.cs` 新增 `Unregister_cleans_a_legacy_32bit_registration`：预置 32 位 `RegisteredApplications` 值，`Unregister` 后断言删除（后果：破坏系统运行——取消关联必须把机器级 32 位残留一并清干净）
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests`，确认 RED 因预期原因失败（残留未被清理）

## 2. Implementation

- [x] 2.1 `Services/FileAssociationService.cs`：构造函数增加可选参数 `legacy32Root`（32 位视图机器根；缺省且 `root` 为 HKLM 时自动用 `RegistryView.Registry32` 解析，其余情形跳过以保证测试隔离）；新增 `CleanupLegacy32BitRegistration`（清 `Software\RegisteredApplications` 的 `ImageViewer` 值、`Software\ImageViewer` 树、`Software\Classes\{ProgId, Applications\<exe>}` 树、各扩展名 `OpenWithProgids` 的 `ImageViewer.Image` 值），`Register` 与 `Unregister` 均调用——日志点：Info 仅在确有清理时输出、Warn 清理失败（含异常）
- [x] 2.2 `AGENTS.md` 第 19 行白名单描述改为 `jpg/jpeg/png/bmp`——无测试：文档文案
- [x] 2.3 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` 确认 GREEN

## 3. Review

- [x] 3.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build`

## 4. 端到端人工验收

- [x] 4.1 [人工] 用新构建的 `build\Debug\ImageViewer.exe`：设置 → 「注册并重置默认选择」一次，确认注册正常、日志无「清理 32 位」失败记录（历史残留已手工清除，自动清理路径由 1.x 测试覆盖）

## 5. Self-review

- [x] 5.1 自审：对照本变更两项遗留（32 位清理、白名单文案）与 tasks.md 逐条确认无遗漏（本 change 无 spec 产物）

## 6. Commit

- [ ] 6.1 [人工] 提交：`git add -- Services/FileAssociationService.cs AGENTS.md tests/Services/FileAssociationServiceTests.cs docs/changes/legacy-32bit-cleanup && git commit -m "fix(association): 清理 32 位视图旧注册残留"`（含 AGENTS.md 的术语修正与白名单两行文档同步；只 commit 不 push；人工验收通过且用户明确许可后才执行）
