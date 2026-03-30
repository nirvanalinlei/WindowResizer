# 多虚拟桌面手工验证清单

## 当前结论

- Windows 11 24H2 `10.0.26200.0` 已完成真实窗口 smoke 验证。
- `Save All / Restore All` 的跨虚拟桌面保存与恢复已通过。
- 默认 runtime move 只在 `26200 <= build < 26900` 且 backend 可用时启用。
- 自动 smoke 必须使用 `powershell.exe`；`pwsh` 当前会触发 `IInspectable` marshalling 问题。
- 多显示器 + 多虚拟桌面 smoke 已在负坐标副屏环境通过。

## 自动联调

1. 运行单元测试：
   `dotnet test tests\WindowResizer.Base.Tests\WindowResizer.Base.Tests.csproj`
2. 构建 smoke 依赖的 `net48` 库和探针程序：
   `dotnet build tests\WindowResizer.Base.Tests\WindowResizer.Base.Tests.csproj -c Debug`
   `dotnet build .\.codex-temp\WindowProbeApp\WindowProbeApp.csproj -c Debug`
3. 构建 GUI：
   `dotnet build src\WindowResizer\WindowResizer.csproj -c Debug -f net47`
4. 构建 CLI：
   `dotnet build src\WindowResizer.CLI\WindowResizer.CLI.csproj -c Debug -f net472`
5. 运行真实跨桌面 smoke：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_runtime_restore.ps1`
6. 运行真实多显示器跨桌面 smoke：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_multimon_restore.ps1`
7. 如需验证 Explorer 重启后的恢复重试，运行：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_explorer_restart_restore.ps1`
8. 如需验证提权窗口跨桌面恢复容错与整批不中断，运行：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_fallback_restore.ps1`
9. 如需单独探测管理员窗口 direct move 能否真实失败，运行：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_elevated_move_probe.ps1`
10. 如需一次性收集跨机证据目录和最短回填块，运行：
   `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\.codex-temp\wr_vd_collect_cross_machine_evidence.ps1`
11. 预期结果：前三个脚本的 JSON 中都应满足 `RestoreResult.RestoredCount = 2`、`MoveFallbackCount = 0`、`FailedCount = 0`；提权恢复脚本应满足 `Contract.AcceptedSatisfied = true`，且前后两个普通窗口都恢复成功。可接受路径 A 为 `ObservedPath = MoveFallback`，即 `MoveFallbackCount = 1` 且管理员窗口停留在受扰动桌面；可接受路径 B 为 `ObservedPath = PlacementFailureAfterMove`，即管理员窗口先回到保存桌面，但 placement 恢复失败并计入 `FailedEntries`。direct move probe 则用于补 direct move 证据，重点看 `NormalObservedPath` 与 `AdminObservedPath` 是否为 `BothMovesSucceeded`，或是否出现真实 move 失败路径。包装脚本会把 `probe-output.txt`、`restore-output.txt`、`summary.txt` 和每一步的 `*-status.json` 一起落盘；子脚本只要输出了可解析 JSON，也会同步落盘 `probe.json` / `restore.json`。

说明：六个脚本都会直接加载 `src\WindowResizer.*\bin\Debug\net48\*.dll`，并依赖 `.codex-temp\WindowProbeApp\bin\Debug\net48\WindowProbeApp.exe`。脚本会根据自身所在的 `.codex-temp` 目录自动推导仓库根目录，因此可以在任意 checkout 路径执行，但不要把单个脚本脱离仓库目录单独拷走。其中 `wr_vd_explorer_restart_restore.ps1` 会主动重启当前用户会话中的 `explorer.exe`，执行前应关闭无关窗口，并把这一步记录为受控会话级 smoke。`wr_vd_elevated_fallback_restore.ps1`、`wr_vd_elevated_move_probe.ps1` 与 `wr_vd_collect_cross_machine_evidence.ps1` 都会弹出 UAC 确认框，用于启动管理员 `WindowProbeApp`；只有手动点击“允许”后脚本才会继续。前两者分别验证提权恢复容错与 direct move，包装脚本则按顺序运行二者并落盘证据目录，通常会触发两次 UAC；即使子脚本失败，也会优先保留 raw output 和状态文件。Explorer restart 脚本失败时也会优先尝试把 shell 恢复到 taskbar ready 状态，而不是只检查 `explorer.exe` 进程是否存在。

## 环境矩阵

- Windows 11 24H2 `26200`：已完成测试、构建、真实 smoke。
- Windows 11 24H2 servicing builds，例如 `26201`、`26250`：已有自动化边界测试，待实机验证。
- Windows 10 `19045`、Windows 11 `22631`、Windows 11 `26100`：当前仅保留 read-only / candidate 选择语义，不默认启用 runtime move。
- 多显示器 + 多虚拟桌面：负坐标副屏 smoke 已通过，更复杂拓扑仍建议按下面的手工用例补全记录。

## 预设

1. 在设置页开启 `Virtual Desktop Restore`。
2. 准备至少 3 个虚拟桌面。
3. 如需验证跨屏恢复，准备第二块显示器并确认扩展桌面已启用。
4. 准备以下窗口：
   - `notepad.exe` 唯一窗口
   - 两个同标题的 `notepad.exe` 窗口
   - 一个最小化窗口
   - 一个管理员权限窗口

## 用例

### 0. 设置页快照状态

1. 打开设置页，记录当前 `Last layout snapshot` 文案。
2. 执行一次 `Save All`。
3. 观察状态文案是否更新为最新时间和窗口数。
4. 切换到一个没有执行过 `Save All` 的 profile。
5. 预期：新 profile 显示 `not saved yet`；切回原 profile 后恢复原快照状态。

### 1. 单窗口跨桌面恢复

1. 在桌面 A 打开唯一标题窗口并执行 `Save`。
2. 把窗口移到桌面 B。
3. 执行 `Restore`。
4. 预期：窗口先回到保存时桌面，再恢复位置和状态。

### 2. `Save All / Restore All`

1. 在桌面 A、B、C 分别摆放多个窗口。
2. 执行 `Save All`。
3. 打乱窗口位置、状态和桌面。
4. 执行 `Restore All`。
5. 预期：已匹配窗口按快照恢复；toast 显示恢复、回退、未匹配和失败统计。

### 3. 重复窗口 best-effort

1. 打开两个同进程、同标题、同类名窗口。
2. 让两个窗口保存时使用不同位置。
3. 执行 `Save All` 后再打乱位置。
4. 执行 `Restore All`。
5. 预期：两个窗口按稳定顺序恢复，不要求恢复到原始实例身份。

### 4. 最小化窗口语义

1. 先在窗口非最小化时执行 `Save All`。
2. 将其中一个已保存窗口最小化。
3. 在 `RestoreAllIncludeMinimized = false` 下执行 `Restore All`。
4. 再切换到 `true` 重试。
5. 预期：关闭时最小化窗口跳过；开启时该已保存窗口可参与恢复。

### 5. 无快照与失败回退

1. 切到未执行过 `Save All` 的 profile。
2. 执行 `Restore All`。
3. 预期：no-op，并提示当前 profile 没有保存的布局快照。

### 6. 提权与跨桌面恢复容错

1. 对管理员窗口执行 `Restore All`。
2. 观察日志、toast 和最终位置。
3. 预期：提权窗口前后的普通窗口都继续恢复，且整批恢复不会被管理员窗口中断。
4. 可接受路径 A：`Contract.ObservedPath = MoveFallback`；`MoveFallbackCount = 1`；管理员窗口保持在受扰动后的桌面；`MoveFallbackEntries` 包含带原因的管理员条目。
5. 可接受路径 B：`Contract.ObservedPath = PlacementFailureAfterMove`；`MoveFallbackCount = 0`；管理员窗口先回到保存桌面，但 placement 恢复失败；此时 `RestoredCount = 2`、`FailedCount = 1`，且管理员窗口必须计入 `FailedEntries`。
6. 当前本机已实测命中路径 B。

### 6A. 提权窗口 direct move 探针

1. 运行 `wr_vd_elevated_move_probe.ps1`。
2. 允许 UAC，等待 JSON 输出。
3. 预期：`Contract.AcceptedSatisfied = true`，至少普通窗口控制组要能完成“受扰动桌面 -> 保存桌面 -> 受扰动桌面”的 direct move 往返。
4. 读取 `AdminObservedPath` 判断管理员窗口 direct move 行为；若为 `BothMovesSucceeded`，表示本机未复现真实 `MoveFallback`；若出现 `NonCurrentToCurrentMoveFailed` 或 `CurrentToOtherMoveFailed`，则应把该机归档为真实 `MoveFallback` 候选环境。
5. `NormalCurrentDesktopSignalConsistent` 与 `AdminCurrentDesktopSignalConsistent` 目前只作为诊断字段，不作为 gate；结论应以 desktop id 和 `ObservedPath` 为准。

### 7. CLI `resize --verbose`

1. 执行 `WindowResizer.CLI resize --verbose`。
2. 预期：输出包含规则标题、桌面移动状态、是否恢复成功和错误信息。

### 8. 跨显示器 + 跨桌面恢复

1. 将窗口保存到副屏的桌面 B。
2. 把窗口挪回主屏或其他桌面。
3. 执行 `Restore` 或 `Restore All`。
4. 预期：窗口先回到保存时桌面，再回到保存时屏幕坐标。

### 9. Explorer 重启后的恢复重试

1. 保持 `Virtual Desktop Restore` 开启，并准备两个位于不同虚拟桌面的测试窗口。
2. 执行 `Save All`，再把其中一个窗口挪到错误桌面和错误位置。
3. 受控重启 `explorer.exe`。
4. 在不重建应用配置的前提下执行 `Restore All`。
5. 预期：窗口仍能回到保存时桌面和位置；结果统计保持 `MoveFallbackCount = 0`。

## 记录项

- Windows 版本和 build 号
- 显示器拓扑
- 虚拟桌面数量
- 使用的 shell，必须明确记录 `powershell.exe` 还是 `pwsh`
- 是否存在 `cloaked` 窗口
- 失败窗口类型与错误现象

如需在其他机器上继续补 `MoveFallback` 证据，优先按 [virtual-desktop-cross-machine-evidence-template.md](/D:/Projects/WindowResizer/docs/virtual-desktop-cross-machine-evidence-template.md) 执行并回填。
