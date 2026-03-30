# WindowResizer

[![WindowResizer](https://github.com/nirvanalinlei/WindowResizer/actions/workflows/WindowResizer.yml/badge.svg)](https://github.com/nirvanalinlei/WindowResizer/actions/workflows/WindowResizer.yml) [![GitHub all releases](https://img.shields.io/github/downloads/nirvanalinlei/WindowResizer/total)](https://github.com/nirvanalinlei/WindowResizer/releases) [![GitHub release (latest SemVer)](https://img.shields.io/github/v/release/nirvanalinlei/WindowResizer?sort=semver)](https://github.com/nirvanalinlei/WindowResizer/releases/latest)

WindowResizer saves and restores window positions with hotkeys, including full layout snapshots across multiple monitors and virtual desktops.

WindowResizer 用快捷键保存和恢复窗口位置，也支持跨多显示器与多虚拟桌面的整套布局快照。

## English

### Features
- Save and restore the foreground window.
- Use tray `Save All` / `Restore All` to capture and replay a full layout snapshot.
- Optional `Virtual Desktop Restore` will try to move matched windows back to their saved virtual desktops when Windows provides the required support.
- `Restore All` can include minimized windows.
- Snapshot matching is more resilient for dynamic titles, including numbered windows such as `Chart #1`.

### Download
- GitHub Releases: <https://github.com/nirvanalinlei/WindowResizer/releases/latest>
- Microsoft Store: <https://www.microsoft.com/store/apps/9NZ07CQ6WZMB>
- Runtime for packaged binaries: Windows x64 and .NET Framework 4.7.2 or later.

### Quick Start
1. Run `WindowResizer.exe`.
2. Use `Ctrl+Alt+S` to save the foreground window and `Ctrl+Alt+R` to restore it.
3. Open the settings window to change hotkeys and toggle `Resize by title`, `Auto Resize Delay`, `Include Minimized`, or `Virtual Desktop Restore`.
4. Use tray `Save All` to save the current layout snapshot. Use tray `Restore All` to replay the last saved snapshot.
5. For portable mode, keep `WindowResizer.config.json` beside `WindowResizer.exe`.

### Restore All Summary
- `Restored`: placement was applied successfully.
- `Move fallback`: the window could not be moved to the saved virtual desktop, so placement was still attempted on the current desktop.
- `Unmatched`: no live window matched a saved snapshot entry.
- `Failed`: a matching window was found, but Windows or the target app refused the placement change.

### CLI
Run `WindowResizer.CLI.exe resize -h`.

```powershell
WindowResizer.CLI.exe resize
WindowResizer.CLI.exe resize -p "notepad.exe"
WindowResizer.CLI.exe resize -p "notepad.exe" -t "notes" -v
```

When `--process` is omitted, the CLI resizes the foreground window. When `--process` is provided, `--title` works as a substring filter instead of a regex.

The CLI works without the tray app already running.

### Build
```powershell
dotnet restore
dotnet build WindowResizer.sln -c Release
pwsh .\installer\build.ps1 1.3.3
pwsh .\installer\build-cli.ps1 1.3.3
```

Release packaging also requires `nuget`, `7z`, and the local Squirrel tool restored from the `squirrel.windows` NuGet package.

## 中文

### 功能
- 保存和恢复当前前台窗口。
- 使用托盘 `Save All` / `Restore All` 保存并恢复整套布局快照。
- 可选 `Virtual Desktop Restore` 会在 Windows 提供相应支持时，尽量把匹配到的窗口移回保存时的虚拟桌面。
- `Restore All` 可选择包含最小化窗口。
- 布局快照的标题匹配对动态标题更稳，像 `Chart #1` 这类编号窗口也能更安全地区分。

### 下载
- GitHub Releases：<https://github.com/nirvanalinlei/WindowResizer/releases/latest>
- Microsoft Store：<https://www.microsoft.com/store/apps/9NZ07CQ6WZMB>
- 已发布程序的运行环境：Windows x64，.NET Framework 4.7.2 及以上。

### 快速开始
1. 运行 `WindowResizer.exe`。
2. 使用 `Ctrl+Alt+S` 保存当前前台窗口，使用 `Ctrl+Alt+R` 恢复当前前台窗口。
3. 在设置窗口中可以修改热键，并开启或关闭 `Resize by title`、`Auto Resize Delay`、`Include Minimized`、`Virtual Desktop Restore`。
4. 托盘 `Save All` 用于保存当前布局快照，`Restore All` 用于恢复最近一次保存的布局快照。
5. 便携模式下，把 `WindowResizer.config.json` 放在 `WindowResizer.exe` 同目录即可。

### Restore All 摘要说明
- `Restored`：成功应用了窗口位置和状态。
- `Move fallback`：无法把窗口移回保存时的虚拟桌面，但仍会尝试在当前桌面恢复位置和大小。
- `Unmatched`：没有找到与快照条目匹配的现存窗口。
- `Failed`：找到了匹配窗口，但 Windows 或目标应用拒绝应用位置变更。

### CLI
运行 `WindowResizer.CLI.exe resize -h`。

```powershell
WindowResizer.CLI.exe resize
WindowResizer.CLI.exe resize -p "notepad.exe"
WindowResizer.CLI.exe resize -p "notepad.exe" -t "notes" -v
```

不传 `--process` 时，CLI 只处理当前前台窗口。传入 `--process` 后，`--title` 才会生效，而且它是标题子串过滤，不是正则匹配。

CLI 可以独立运行，不要求托盘程序先启动。

### 构建
```powershell
dotnet restore
dotnet build WindowResizer.sln -c Release
pwsh .\installer\build.ps1 1.3.3
pwsh .\installer\build-cli.ps1 1.3.3
```

发布打包还依赖 `nuget`、`7z`，以及由 `squirrel.windows` NuGet 包还原到本地的 Squirrel 工具。
