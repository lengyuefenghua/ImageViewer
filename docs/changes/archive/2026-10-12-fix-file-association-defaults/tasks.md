# Tasks

## 1. Tests

- [x] 1.1 在 `tests/Services/FileAssociationServiceTests.cs` 调整既有测试为「去劫持」预期并新增：
  - `Register_leaves_the_system_progid_default_verb_untouched`（后果：破坏系统运行——注册不得再改写系统 ProgID 的 shell 默认动词，也不得写 Backup）
  - `Register_restores_a_legacy_hijacked_default_verb`（后果：破坏系统运行——历史劫持残留（shell 默认动词指向本程序）必须被幂等修复）
  - `ResetStaleUserChoices_removes_legacy_values_and_foreign_user_choice`（后果：数据损坏——重置必须清掉 `Application`/`Progid` 值与外部 `UserChoice`）
  - `ResetStaleUserChoices_keeps_our_own_user_choice`（后果：数据损坏——不得清除用户已选定本程序的默认）
  - 把 `Register_points_the_system_progid_default_verb_to_ours_and_backs_up`、`Register_is_idempotent_and_keeps_the_first_backup`、`Unregister_restores_a_valid_backed_up_default_verb`、`Unregister_clears_the_default_when_the_backup_targets_a_missing_program` 改为预置历史状态的等价形式（还原/清理断言保留）
- [x] 1.2 在 `tests/Services/ViewerAssociationServiceTests.cs` 新增：
  - `Register_resets_stale_user_choice_after_successful_registration`（后果：数据损坏——注册成功后旧默认必须被重置，否则功能静默失效）
  - `Register_does_not_reset_user_choice_when_elevation_is_cancelled`（负向守卫：前置失败不得清除默认；当前实现无重置时先通过，接线错误会红）
- [x] 1.3 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` 和 `tools\test.ps1 -Filter ViewerAssociationServiceTests`，确认 RED 因预期原因失败

## 2. Implementation

- [x] 2.1 `Services/FileAssociationService.cs`：
  - `IsDefaultViewer(string executablePath = null)` 同时接受 `ImageViewer.Image` 与 `Applications\<exe 文件名>`（大小写不敏感；缺省取当前进程 exe）——无测试：检测口径修正无灾难后果，既有测试继续覆盖主形
  - `Register` 删除系统 ProgID 动词劫持写入（verb 键、shell 默认值、Backup 记录）；提取 `RestoreHijackedDefaultVerb`（默认动词指向本程序时还原/清除 + 删除自家动词键），`Register` 幂等修复历史残留、`Unregister` 复用同一实现——日志点：Info 修复残留（systemProgId）
  - 新增 `ResetStaleUserChoices(string executablePath)` 返回 `UserChoiceResetResult(Cleared, Failed)`：逐扩展名删除 `FileExts\<ext>` 的 `Application`/`Progid` 值；`UserChoice` 指向外部 ProgID 时删子键、指向自家时保留；单项失败计入 Failed——日志点：Info/Warn 汇总计数、Warn 单项失败（扩展名 + 异常）
- [x] 2.2 `Services/ViewerAssociationService.cs`：`Register()` 仅在机器级注册成功后调用 `ResetStaleUserChoices`，返回文案含重置结果与失败降级提示；`ShouldPromptForDefaultViewer` 改调 `IsDefaultViewer(executablePath())`——日志点：计数日志由 2.1 的 `ResetStaleUserChoices` 提供，此处不重复记录
- [x] 2.3 文案同步：`App.xaml.cs` 首启弹窗说明加入「清除旧的图片默认应用选择（下次打开会重新询问）」；`Views/SettingsWindow.xaml` 说明文案与注册按钮文案（改「注册并重置默认选择」）同步——无测试：文案
- [x] 2.4 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` 与 `tools\test.ps1 -Filter ViewerAssociationServiceTests` 确认 GREEN

## 3. Review

- [x] 3.1 加载 `review-change` 轻量审查本变更 diff，修复 Critical/Important
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build`

## 4. Self-review

- [x] 4.1 自审：对照本变更三项需求（默认检测、重置集成、去劫持）与本 tasks.md 逐条确认无遗漏（本 change 无 spec 产物）

## 5. Commit

- [x] 5.1 [人工] 提交：`git add -- Services/FileAssociationService.cs Services/ViewerAssociationService.cs App.xaml.cs Views/SettingsWindow.xaml tests/Services/FileAssociationServiceTests.cs tests/Services/ViewerAssociationServiceTests.cs docs/changes/fix-file-association-defaults .opencode/lazy/state.json && git commit -m "fix(file-association): 修正默认检测并重置旧默认选择"`（只 commit 不 push；人工验收通过且用户明确许可后才执行）
