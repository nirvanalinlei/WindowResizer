# Changelog

All notable changes to this project will be documented in this file.

本文件记录项目的重要变更。

## [Unreleased] / [未发布]

### Added / 新增
- Added tray `Save All` / `Restore All` support to save and restore a full layout snapshot. 新增托盘 `Save All` / `Restore All`，可保存并恢复整套布局快照。
- Added optional `Virtual Desktop Restore` with best-effort cross-desktop window moves when Windows support is available. 新增可选 `Virtual Desktop Restore`，在 Windows 提供相应支持时，尽量执行跨虚拟桌面的窗口移动。
- Added layout snapshot status text in the settings window so the last saved snapshot is visible at a glance. 在设置窗口中新增布局快照状态文本，可直接看到最近一次保存的快照状态。
- Added richer restore summaries for `Restored`, `Move fallback`, `Unmatched`, and `Failed`. 为 `Restored`、`Move fallback`、`Unmatched`、`Failed` 增加更完整的恢复结果摘要。

### Changed / 变更
- Improved snapshot matching for dynamic window titles, including numbered titles such as `Chart #1`. 改进动态窗口标题匹配，对 `Chart #1` 这类带编号标题的识别更稳。
- Improved multi-monitor restore reliability for layouts that use negative screen coordinates. 改进多显示器场景下带负坐标布局的恢复可靠性。
- Skipped transient non-user windows such as `TopLevelWindowForOverflowXamlIsland` during snapshot save and restore. 在快照保存和恢复时跳过 `TopLevelWindowForOverflowXamlIsland` 这类临时的非用户窗口。
- Ignored empty virtual desktop identifiers when saving snapshots to avoid invalid restore targets. 保存快照时忽略空的虚拟桌面标识，避免产生无效恢复目标。
- Kept `Restore All` batch execution resilient when some windows cannot be moved or restored. 即使部分窗口无法移动或恢复，`Restore All` 也会继续执行整批恢复。

### Fixed / 修复
- Fixed fallback accounting so windows already on the target virtual desktop are not reported as move fallbacks. 修复 fallback 统计，已经位于目标虚拟桌面的窗口不会再被误报为 move fallback。
- Fixed snapshot save behavior so a single placement-read failure does not abort the full `Save All` operation. 修复快照保存流程，单个窗口读取 placement 失败不会中断整个 `Save All`。
- Preserved detailed placement failure reasons in logs and failed-entry diagnostics. 保留更详细的 placement 失败原因到日志和 failed-entry 诊断信息中。
