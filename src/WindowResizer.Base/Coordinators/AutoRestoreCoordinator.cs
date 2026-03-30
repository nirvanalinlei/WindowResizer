using System;
using System.Diagnostics;
using System.Linq;
using WindowResizer.Base.Abstractions;
using WindowResizer.Configuration;

namespace WindowResizer.Base.Coordinators;

public sealed class AutoRestoreCoordinator
{
    private readonly IWindowContextService _windowContextService;
    private readonly IWindowWaitService _windowWaitService;
    private readonly RuleRestoreCoordinator _ruleRestoreCoordinator;

    public AutoRestoreCoordinator(
        IWindowContextService windowContextService,
        IWindowWaitService windowWaitService,
        RuleRestoreCoordinator ruleRestoreCoordinator)
    {
        _windowContextService = windowContextService;
        _windowWaitService = windowWaitService;
        _ruleRestoreCoordinator = ruleRestoreCoordinator;
    }

    public void RestoreWindow(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string, string>? onConfigNoMatch)
    {
        if (!_windowContextService.TryGetWindowContext(handle, onFailed, out var context))
        {
            return;
        }

        if (config.EnableAutoResizeDelay)
        {
            var autoWindow = config.WindowSizes.FirstOrDefault(w =>
                w.Name.Equals(context.ProcessName, StringComparison.OrdinalIgnoreCase));
            if (autoWindow is null)
            {
                return;
            }

            if (autoWindow.AutoResizeDelay > 0)
            {
                _windowWaitService.Sleep(autoWindow.AutoResizeDelay);
            }
        }

        _ruleRestoreCoordinator.RestoreWindow(context, config, onConfigNoMatch, onlyAuto: true, allowVirtualDesktopMove: false);
    }
}
