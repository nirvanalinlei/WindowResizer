using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindowResizer.Base.Abstractions;
using WindowResizer.Common.Windows;
using WindowResizer.Core.WindowControl;

namespace WindowResizer.Base.Services;

public sealed class WindowContextService : IWindowContextService
{
    public IReadOnlyList<IntPtr> GetOpenWindows()
    {
        return Resizer.GetOpenWindows();
    }

    public WindowState GetWindowState(IntPtr handle)
    {
        return Resizer.GetWindowState(handle);
    }

    public WindowVisibilityState GetWindowVisibilityState(IntPtr handle)
    {
        return Resizer.GetWindowVisibilityState(handle);
    }

    public bool TryGetWindowContext(IntPtr handle, Action<Process, Exception>? onFailed, out WindowRuleContext context)
    {
        context = null!;
        if (!WindowProcessNameResolver.TryGetProcessName(
                handle,
                Resizer.IsChildWindow,
                Resizer.GetRealProcess,
                process => process.MainModule?.ModuleName ?? string.Empty,
                process => process.ProcessName,
                Resizer.IsInvisibleProcess,
                onFailed,
                out _,
                out var processName))
        {
            return false;
        }

        context = new WindowRuleContext(
            handle,
            processName,
            Resizer.GetWindowTitle(handle) ?? string.Empty,
            Resizer.GetWindowClassName(handle) ?? string.Empty);
        return true;
    }
}
