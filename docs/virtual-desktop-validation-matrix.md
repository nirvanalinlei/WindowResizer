# 多虚拟桌面验证矩阵

## 当前门控合同

- `SelectRuntime()` 只在 `26200 <= build < 26900` 且 move backend 可用时启用 runtime move。
- `SelectRuntime()` 在 validated build 上即使 backend 初始化失败，也必须保持 `OfficialReadOnly + IsUnknownBuild = false`。
- `SelectExplorerCandidate()` 保留历史 candidate build：`19045`、`22631`、`26100`，并覆盖 validated runtime 区间。
- `Windows11_24H2ExplorerMoveApi` 与 `Windows11_24H2VirtualDesktopComContext` 统一复用 `Windows11_24H2ExplorerMoveBuildPolicy`。

## 自动验证命令

```powershell
dotnet test tests\WindowResizer.Base.Tests\WindowResizer.Base.Tests.csproj
dotnet build tests\WindowResizer.Base.Tests\WindowResizer.Base.Tests.csproj -c Debug
dotnet build .\.codex-temp\WindowProbeApp\WindowProbeApp.csproj -c Debug
dotnet build src\WindowResizer\WindowResizer.csproj -c Debug -f net47
dotnet build src\WindowResizer.CLI\WindowResizer.CLI.csproj -c Debug -f net472
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_runtime_restore.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_multimon_restore.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_explorer_restart_restore.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_fallback_restore.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_move_probe.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_collect_cross_machine_evidence.ps1
```

说明：真实 smoke/probe 必须使用 `powershell.exe`。`pwsh` 当前会在 24H2 Explorer COM 路径上遇到 `IInspectable` marshalling 问题，不能作为可靠联调入口。六个脚本都会直接加载 `src\WindowResizer.*\bin\Debug\net48\*.dll`，并依赖 `.codex-temp\WindowProbeApp\bin\Debug\net48\WindowProbeApp.exe`。脚本会根据自身所在的 `.codex-temp` 目录自动推导仓库根目录，因此可以在任意 checkout 路径执行。`wr_vd_explorer_restart_restore.ps1` 会主动重启 `explorer.exe`，属于受控会话级验证；`wr_vd_elevated_fallback_restore.ps1` 与 `wr_vd_elevated_move_probe.ps1` 都会弹出 UAC，需手动允许后继续；`wr_vd_collect_cross_machine_evidence.ps1` 会顺序运行这两个提权脚本并把证据落盘，通常会触发两次 UAC。前者验证提权恢复容错，后者只验证 direct move 本身；即使子脚本失败，包装脚本也会优先保留 raw output、状态 JSON 和 `summary.txt`。Explorer restart 脚本失败时会按 shell ready 状态尝试自恢复。

## 构建与测试状态

| 项目 | 结果 | 说明 |
| --- | --- | --- |
| `Base.Tests` | 通过 `130/130` | 包含 runtime/candidate/API/COM gating、读重试、Explorer 重启重试、提权失败可观测性、受限进程名回退、resolver 契约回归、skip-move、提权窗口 placement 失败回归与跨屏回归测试 |
| 跨屏回归测试 | 通过 | `RuleCoordinatorTests` 与 `LayoutSnapshotCoordinatorTests` 负坐标副屏场景 |
| GUI Debug build | 通过 | `src\WindowResizer\WindowResizer.csproj -c Debug -f net47` |
| CLI Debug build | 通过 | `src\WindowResizer.CLI\WindowResizer.CLI.csproj -c Debug -f net472` |
| 真实跨桌面 smoke | 通过 | `RestoredCount=2`、`MoveFallbackCount=0`、`FailedCount=0` |
| 真实多显示器 smoke | 通过 | 负坐标副屏恢复到原屏幕与原桌面 |
| Explorer 重启后 smoke | 通过 | `RestoredCount=2`、`MoveFallbackCount=0`、`FailedCount=0` |
| 提权恢复容错 smoke | 已覆盖路径 B | 已执行真实 UAC smoke，当前观察值为 `SavedWindowCount=3`、`RestoredCount=2`、`MoveFallbackCount=0`、`FailedCount=1`；该结果命中 `PlacementFailureAfterMove` 可接受路径，`MoveFallback` 实机路径仍待补 |

## 版本矩阵

| Build | 默认 runtime 行为 | 自动化状态 | 实机状态 | 备注 |
| --- | --- | --- | --- | --- |
| `19045` | read-only / candidate only | 已覆盖 | 待补 | 不默认启用 move |
| `22631` | read-only / candidate only | 已覆盖 | 待补 | 不默认启用 move |
| `26100` | read-only / candidate only | 已覆盖 | 待补 | 不默认启用 move |
| `26199` | read-only | 已覆盖 | 不适用 | 下边界外显式阻断 |
| `26200` | runtime move | 已覆盖 | 已通过 | 当前真实验证基线 |
| `26201` | runtime move | 已覆盖 | 待补 | servicing build |
| `26250` | runtime move | 已覆盖 | 待补 | mid-servicing build |
| `26900` | read-only | 已覆盖 | 不适用 | 上边界外显式阻断 |
| `26999` | read-only | 已覆盖 | 不适用 | 继续阻断 runtime move |

## 当前已归档证据

- 本机环境：Windows 11 24H2 `10.0.26200.0`
- 真实 smoke 输出核心结果：
  - `SaveResult.SavedWindowCount = 2`
  - `SaveResult.DesktopCount = 2`
  - `RestoreResult.RestoredCount = 2`
  - `RestoreResult.MoveFallbackCount = 0`
  - `RestoreResult.FailedCount = 0`
- 真实多显示器 smoke 输出核心结果：
  - 目标副屏：`DISPLAY3`，边界 `{X=-1715,Y=-16,Width=1707,Height=960}`
  - 干扰后窗口被移动到主屏 `{X=0,Y=0,Width=1707,Height=960}`
  - 恢复后窗口回到副屏负坐标矩形 `Left=-1635, Top=64, Right=-1315, Bottom=284`
  - `RestoreResult.RestoredCount = 2`
  - `RestoreResult.MoveFallbackCount = 0`
  - `RestoreResult.FailedCount = 0`
- 当前已确认场景：`Save All / Restore All` 跨虚拟桌面保存、跨桌面恢复、非当前桌面窗口移动、位置恢复。
- 当前已确认场景补充：跨显示器 + 跨虚拟桌面恢复，且恢复后回到原副屏坐标。
- 当前已确认场景补充：Explorer 重启后首次 `Restore All` 仍成功，且 `MoveFallbackCount = 0`。
- Explorer 重启 smoke 输出核心结果：
  - `ExplorerProcessCountBefore = 1`
  - `ExplorerProcessCountAfter = 2`
  - `RestoreResult.RestoredCount = 2`
  - `RestoreResult.MoveFallbackCount = 0`
  - `RestoreResult.FailedCount = 0`
- 提权恢复容错 smoke 输出核心结果：
  - `SaveResult.SavedWindowCount = 3`
  - `RestoreResult.RestoredCount = 2`
  - `RestoreResult.MoveFallbackCount = 0`
  - `RestoreResult.UnmatchedCount = 0`
  - `RestoreResult.FailedCount = 1`
  - `AdminReturnedToSavedDesktop = true`
  - `AdminPlacementRestored = false`
  - 已归档为 `PlacementFailureAfterMove` 路径
- 提权 direct move probe 输出核心结果：
  - 本机环境：Windows 11 24H2 `10.0.26200.0`
  - `NormalObservedPath = BothMovesSucceeded`
  - `AdminObservedPath = BothMovesSucceeded`
  - 管理员窗口在本机上可从受扰动桌面直接移回保存桌面，也可再移回受扰动桌面
  - 结论：本机未复现真实 `MoveFallback`，当前 `MoveFallback` 缺口属于跨机器待补证据，而不是本机未完成实验

## 待补实机验证

- `26201` 与 `26250` 的真实窗口 smoke
- 提权窗口 `MoveFallback` 实机路径：本机 `26200` 已补 direct move probe，但未复现；需在其他机器/权限拓扑上继续留证

跨机补证请使用 [virtual-desktop-cross-machine-evidence-template.md](/D:/Projects/WindowResizer/docs/virtual-desktop-cross-machine-evidence-template.md)。
