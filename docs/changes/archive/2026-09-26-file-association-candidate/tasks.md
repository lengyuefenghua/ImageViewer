# Tasks

变更：图片文件的「注册」在写好候选注册后，把系统 ProgID（`jpegfile`/`pngfile`/`Paint.Picture`）的 `shell` 默认动词指向本程序的 `ImageViewerOpen`，使 ImageViewer 出现在 Windows「打开方式 / 默认应用」候选列表（与 MassiGra 同款机制）。**不写 `UserChoice`**（哈希保护项），因此不会自动成为默认——用户在系统设置里手动选一次后才真正成为默认。

取消注册时健壮回退：仅当默认动词仍是本程序时，还原备份动词（备份动词指向的程序仍存在才还原，否则清除该默认值），并删除本程序动词，避免遗留指向已删程序的死动词弹窗。

范围约束：仍注册到 HKLM（需 UAC）；保留候选注册（`Applications`/`RegisteredApplications`/`Capabilities`/`OpenWithProgids`）。覆盖类型仅 jpg/png/bmp。

## 1. Tests (RED)

- [x] 1.1 `tests\Services\FileAssociationServiceTests.cs`：删除断言「Register 不改系统默认动词」的用例。
- [x] 1.2 新增 `Register_points_the_system_progid_default_verb_to_ours_and_backs_up`：注册后系统 ProgID 的 `shell` 默认=`ImageViewerOpen`、写入该动词命令、备份原动词。
- [x] 1.3 新增 `Register_is_idempotent_and_keeps_the_first_backup`。
- [x] 1.4 新增 `Unregister_restores_a_valid_backed_up_default_verb`：备份动词目标存在时还原。
- [x] 1.5 新增 `Unregister_clears_the_default_when_the_backup_targets_a_missing_program`：备份动词指向缺失程序时清除默认值。
- [x] 1.6 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests`，确认新用例在旧实现下 RED（实测 2 例失败：缺失程序清除、移除本程序默认动词）。

## 2. Implementation (GREEN)

- [x] 2.1 `Services\FileAssociationService.cs` 的 `Register`：加回「在系统 ProgID 下写 `ImageViewerOpen` 动词 + 备份原默认动词 + 将 `shell` 默认值指向本程序」；不写 `UserChoice`（日志点：沿用注册成功 `Warn`）。
- [x] 2.2 同文件 `Unregister`：仅当默认动词为本程序时回退——备份动词目标存在则还原，否则清除默认值；删除本程序动词；恢复 `backupPath` 字段（日志点：回退命中 `Info`）。
- [x] 2.3 新增 `CommandTargetsExistingProgram` / `VerbTargetsExistingProgram` / `ReadBackedUpVerb` 辅助（仅对绝对路径判存在性；相对/内置命令保守视为有效）。
- [x] 2.4 运行 `tools\test.ps1 -Build -Filter FileAssociationServiceTests` → 16/16 GREEN。
- [x] 2.5 运行 `tools\test.ps1 -Build` → 124/124 通过。

## 3. Review

- [x] 3.1 轻量审查本变更 diff，修复 Critical/Important。
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build`。

## 4. 人工验收（OS shell 行为，无法单测）

- [ ] 4.1 [人工] 构建后点「注册」→ ImageViewer 出现在 Windows 设置「选择应用」/ 右键「打开方式」候选列表；双击默认不变（不自动成为默认）。
- [ ] 4.2 [人工] 在系统设置里手动选一次 ImageViewer → 双击图片直接打开。
- [ ] 4.3 [人工] 点「取消注册」→ 不再遗留指向已删程序（ImageDataViewer）的死默认动词弹窗；原先的有效默认（若存在）被还原。

## 5. Self-review

- [x] 5.1 自审：逐条对照本次范围与 tasks，确认无遗漏、无多余改动。

## 6. Commit

- [ ] 6.1 提交：`git add -- Services/FileAssociationService.cs tests/Services/FileAssociationServiceTests.cs docs/changes/2026-09-26-file-association-candidate && git commit -m "feat(file-association): 注册后进入候选列表，不自动设默认并健壮回退"`（只 commit 不 push）。
