using System;
using System.Diagnostics;
using WindowResizer.Base.Abstractions;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;

namespace WindowResizer.Base.Coordinators;

public sealed class RuleSaveCoordinator
{
    private readonly IWindowContextService _windowContextService;
    private readonly IWindowPlacementService _windowPlacementService;
    private readonly IConfigurationStore _configurationStore;
    private readonly IVirtualDesktopService _virtualDesktopService;
    private readonly WindowRuleMatcher _ruleMatcher;
    private readonly WindowRuleUpdater _ruleUpdater;

    public RuleSaveCoordinator(
        IWindowContextService windowContextService,
        IWindowPlacementService windowPlacementService,
        IConfigurationStore configurationStore,
        IVirtualDesktopService virtualDesktopService,
        WindowRuleMatcher ruleMatcher,
        WindowRuleUpdater ruleUpdater)
    {
        _windowContextService = windowContextService;
        _windowPlacementService = windowPlacementService;
        _configurationStore = configurationStore;
        _virtualDesktopService = virtualDesktopService;
        _ruleMatcher = ruleMatcher;
        _ruleUpdater = ruleUpdater;
    }

    public void SaveWindow(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string>? onSuccess = null,
        bool allowVirtualDesktopCapture = true)
    {
        if (!_windowContextService.TryGetWindowContext(handle, onFailed, out var context))
        {
            return;
        }

        var match = _ruleMatcher.Match(config, context.ProcessName, context.Title);
        var placement = _windowPlacementService.GetPlacement(handle);
        var savedDesktopId = ResolveSavedDesktopId(handle, config, context.Title, allowVirtualDesktopCapture);

        _ruleUpdater.Upsert(config, match, context.ProcessName, context.Title, placement, savedDesktopId);
        _configurationStore.Save();
        onSuccess?.Invoke(context.ProcessName);
    }

    private string? ResolveSavedDesktopId(IntPtr handle, Config config, string title, bool allowVirtualDesktopCapture)
    {
        if (!allowVirtualDesktopCapture || !config.EnableVirtualDesktopRestore || !_ruleMatcher.HasLiteralWindowTitle(title))
        {
            return null;
        }

        if (!VirtualDesktopReadHelper.TryGetDesktopIdWithRetry(_virtualDesktopService, handle, out var desktopId))
        {
            return null;
        }

        return desktopId.ToString("D");
    }
}
