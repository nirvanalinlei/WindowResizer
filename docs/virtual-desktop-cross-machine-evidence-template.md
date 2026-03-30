# 多虚拟桌面跨机留证模板

## 适用目标

- 补 `wr_vd_elevated_fallback_restore.ps1` 的真实 `MoveFallback` 路径证据。
- 区分“direct move 可行，但 placement restore 失败”和“direct move 本身失败”。
- 统一记录不同机器、权限拓扑和 Explorer 状态下的可复现结果。

## 执行顺序

1. 记录环境信息。
2. 运行 `wr_vd_elevated_move_probe.ps1`，先判断 direct move 是否可行。
3. 运行 `wr_vd_elevated_fallback_restore.ps1`，再观察恢复容错路径。
4. 把结果回填到本文件或验证矩阵。

说明：这些脚本会根据自身所在的 `.codex-temp` 目录自动推导仓库根目录，因此可以在任意 checkout 路径执行；不要把单个脚本脱离仓库目录单独拷走。`wr_vd_collect_cross_machine_evidence.ps1` 在子脚本失败时也会保留 `probe-output.txt`、`restore-output.txt`、`summary.txt` 和每一步的 `*-status.json`，便于异常机型留证。

## 环境信息

- 机器标识：
- Windows 版本与 build：
- 是否为主力机 / 虚拟机 / 远程桌面：
- 显示器拓扑：
- 虚拟桌面数量：
- `explorer.exe` 是否刚重启过：
- 是否存在额外安全软件、窗口管理器、录屏/远控工具：
- shell：必须记录 `powershell.exe`

## Probe 记录

命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_move_probe.ps1
```

记录项：

- `Environment.OsVersion`:
- `Environment.OriginalDesktopId`:
- `Environment.DisturbedDesktopId`:
- `Contract.AcceptedSatisfied`:
- `Contract.NormalObservedPath`:
- `Contract.AdminObservedPath`:
- `Contract.NormalCurrentDesktopSignalConsistent`:
- `Contract.AdminCurrentDesktopSignalConsistent`:

判读规则：

- `AdminObservedPath = BothMovesSucceeded`：本机管理员窗口 direct move 可行，未复现真实 `MoveFallback`。
- `AdminObservedPath = NonCurrentToCurrentMoveFailed`：管理员窗口从受扰动桌面移回保存桌面失败，优先归档为 `MoveFallback` 候选环境。
- `AdminObservedPath = CurrentToOtherMoveFailed`：管理员窗口从保存桌面移回受扰动桌面失败，说明 direct move 仍存在方向性异常，也应归档。
- 若出现 `NonCurrentToCurrentDesktopMismatch`、`CurrentToOtherDesktopMismatch` 或其他未枚举 `ObservedPath`，也必须保留完整 JSON，并按“未知路径”单独归档。
- `NormalCurrentDesktopSignalConsistent` / `AdminCurrentDesktopSignalConsistent` 只是诊断字段，不作为结论 gate。

## Restore Smoke 记录

命令：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_fallback_restore.ps1
```

记录项：

- `SaveResult.SavedWindowCount`:
- `RestoreResult.RestoredCount`:
- `RestoreResult.MoveFallbackCount`:
- `RestoreResult.UnmatchedCount`:
- `RestoreResult.FailedCount`:
- `Contract.AcceptedSatisfied`:
- `Contract.StrictMoveFallbackSatisfied`:
- `Contract.PlacementFailureAfterMoveSatisfied`:
- `Contract.ObservedPath`:
- `Contract.AdminReturnedToSavedDesktop`:
- `Contract.AdminPlacementRestored`:
- `Contract.AdminFailedRecorded`:

判读规则：

- `ObservedPath = MoveFallback`：命中真实 `MoveFallback` 路径，应优先归档该机。
- `ObservedPath = PlacementFailureAfterMove`：direct move 已发生，但 placement restore 失败。
- `AcceptedSatisfied = false`：说明该机出现了未归档的新路径，需保留完整 JSON 和环境信息。

## 回填摘要

- 机器结论：
- direct move 结论：
- restore 容错结论：
- 是否命中真实 `MoveFallback`：
- 是否建议补充该机为长期验证节点：
- 附加现象：

## 最短回填块

```text
机器：
Windows build：
显示器拓扑：
虚拟桌面数量：

Probe:
- AcceptedSatisfied=
- NormalObservedPath=
- AdminObservedPath=

Restore Smoke:
- AcceptedSatisfied=
- ObservedPath=
- RestoredCount=
- MoveFallbackCount=
- FailedCount=

结论：
```

建议填写方式：

- `Probe` 直接抄 `wr_vd_elevated_move_probe.ps1` 的 `Contract` 字段。
- `Restore Smoke` 直接抄 `wr_vd_elevated_fallback_restore.ps1` 的 `Contract` 与 `RestoreResult` 字段。
- `结论` 只写一句，例如“本机未复现真实 MoveFallback，命中 PlacementFailureAfterMove”或“本机命中真实 MoveFallback，建议纳入长期验证节点”。
