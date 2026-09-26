# Tasks

## 1. Tests

- [x] 1.1 在 `tools/test-startup.ps1` 增加黑盒回归检查：单张图片启动时，首图解码完成日志应早于目录扫描启动；测试 seam：真实 Release 隔离副本、唯一图片文件名对应的应用日志。
- [x] 1.2 运行 `tools\test-startup.ps1`，确认旧路径日志显示首图解码晚于目录扫描，建立预期 RED（探索阶段曾短暂得到假 GREEN，已修正日志切片与关联并重跑）。

## 2. Implementation

- [x] 2.1 单张图片启动时先开始首图加载，再异步准备同目录/同级目录导航；搜索结果 COM 探测也延后到首图解码后；保持连续浏览及错误提示行为不变。日志点：现有“查看器已打开”Warn、扫描开始/完成 Debug、首图解码完成 Info 保留；目录扫描失败 Warn，尺寸读取降级 Debug。
- [x] 2.2 运行 `tools\test-startup.ps1` 并确认 GREEN；用隔离 Release 副本对生成图片启动测 5 次（热启动窗口 356–391 ms，首图解码 467–491 ms）；未截图（用户选择 visualVerify=false），因此仅确认解码完成日志，不声称验证了真实像素显示或相对旧版的端到端提速。空白窗口不在本次启动顺序改动范围。
- [x] 2.3 构造阶段启动首图尺寸后台预读并在 Loaded 后复用结果；日志点：预读开始/完成与窗口 Loaded 均为 Debug，不记录路径；尺寸读取失败继续 Debug 降级。
- [x] 2.4 运行 `tools\test-startup.ps1` 并确认窗口构造期间开始了后台尺寸预读，且首图解码仍在目录扫描之前完成。

## 3. Review

- [x] 3.1 加载 `review-change` 对照任务与 diff 做 patch 轻量审查；修正单图启动顺序并保留搜索结果/目录导航语义；失败/缺失图片会清除首图待完成状态。
- [x] 3.2 运行最终验证：`tools\test.ps1 -Build` → 124/124 通过；`tools\test-startup.ps1` → GREEN，尺寸预读 23:30:10.2906，Loaded 23:30:10.3346，首图解码 23:30:10.4147，目录扫描 23:30:10.7182；新加构造期预读后再次运行同脚本，GREEN，`git diff --check` 无空白错误。

## 4. Self-review

- [x] 4.1 自审单图、搜索结果列表、多实例及导航回填路径；尺寸预读在窗口构造阶段启动且后台完成，Loaded 后复用任务结果，异常按既有 Debug 降级；首图解码后再做搜索结果 COM 探测与目录/同级枚举。日志点：图片打开 Warn、主图解码失败 Error、首图解码 Info、尺寸预读开始/完成与 Loaded Debug、目录扫描开始/完成 Debug、扫描失败 Warn、尺寸读取降级 Debug。

## 5. Commit

- [ ] 5.1 用户确认完成收尾后提交本变更：`git add -- Services/ViewerImageDirectoryScanner.cs Views/ImageViewerViewModel.cs Views/ViewerWindow.xaml.cs tools/test-startup.ps1 docs/changes/startup-speed .opencode/lazy/state.json && git commit -m "perf(startup): 优化单图启动加载顺序"`（只 commit 不 push；不包含原有未跟踪的 `package.json`/`package-lock.json`）。
