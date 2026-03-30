using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindowResizer.Common.Windows;

namespace WindowResizer.Base.Abstractions;

public interface IWindowContextService
{
    IReadOnlyList<IntPtr> GetOpenWindows();

    WindowState GetWindowState(IntPtr handle);

    WindowVisibilityState GetWindowVisibilityState(IntPtr handle);

    bool TryGetWindowContext(IntPtr handle, Action<Process, Exception>? onFailed, out WindowRuleContext context);
}
