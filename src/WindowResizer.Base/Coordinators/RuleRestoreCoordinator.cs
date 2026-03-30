using System;
using System.Diagnostics;
using WindowResizer.Base.Abstractions;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;

namespace WindowResizer.Base.Coordinators;

public sealed class RuleRestoreCoordinator
{
    private readonly IWindowContextService _windowContextService;
    private readonly IWindowPlacementService _windowPlacementService;
    private readonly IVirtualDesktopService _virtualDesktopService;
    private readonly WindowRuleMatcher _ruleMatcher;

    public RuleRestoreCoordinator(
        IWindowContextService windowContextService,
        IWindowPlacementService windowPlacementService,
        IVirtualDesktopService virtualDesktopService,
        WindowRuleMatcher ruleMatcher)
    {
        _windowContextService = windowContextService;
        _windowPlacementService = windowPlacementService;
        _virtualDesktopService = virtualDesktopService;
        _ruleMatcher = ruleMatcher;
    }

    public WindowRestoreResult RestoreWindow(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string, string>? onConfigNoMatch,
        bool onlyAuto = false,
        bool allowVirtualDesktopMove = true)
    {
        if (!_windowContextService.TryGetWindowContext(handle, onFailed, out var context))
        {
            return new WindowRestoreResult(
                matchedConfig: false,
                placementRestored: false,
                matchedRuleTitle: null,
                virtualDesktopMoveStatus: VirtualDesktopMoveStatus.NotApplicable,
                errorMessage: "Window unavailable.",
                virtualDesktopMoveErrorMessage: null);
        }

        return RestoreWindow(context, config, onConfigNoMatch, onlyAuto, allowVirtualDesktopMove);
    }

    public WindowRestoreResult RestoreWindow(
        WindowRuleContext context,
        Config config,
        Action<string, string>? onConfigNoMatch,
        bool onlyAuto = false,
        bool allowVirtualDesktopMove = true)
    {
        var match = _ruleMatcher.Match(config, context.ProcessName, context.Title, onlyAuto);
        var targetWindow = _ruleMatcher.SelectPreferredMatch(match);
        if (targetWindow is null)
        {
            onConfigNoMatch?.Invoke(context.ProcessName, context.Title);
            return new WindowRestoreResult(
                matchedConfig: false,
                placementRestored: false,
                matchedRuleTitle: null,
                virtualDesktopMoveStatus: VirtualDesktopMoveStatus.NotApplicable,
                errorMessage: "No saved settings.",
                virtualDesktopMoveErrorMessage: null);
        }

        var moveResult = MoveToSavedDesktop(context.Handle, config, targetWindow, allowVirtualDesktopMove);
        var restored = _windowPlacementService.RestorePlacement(context.Handle, targetWindow, out var placementErrorMessage);
        return new WindowRestoreResult(
            matchedConfig: true,
            placementRestored: restored,
            matchedRuleTitle: targetWindow.Title,
            virtualDesktopMoveStatus: moveResult.Status,
            errorMessage: restored ? null : (string.IsNullOrWhiteSpace(placementErrorMessage) ? "Unable to restore placement." : placementErrorMessage),
            virtualDesktopMoveErrorMessage: moveResult.ErrorMessage);
    }

    private DesktopMoveResult MoveToSavedDesktop(IntPtr handle, Config config, WindowSize targetWindow, bool allowVirtualDesktopMove)
    {
        if (!allowVirtualDesktopMove
            || !config.EnableVirtualDesktopRestore
            || !_ruleMatcher.IsExactTitleRule(targetWindow)
            || string.IsNullOrWhiteSpace(targetWindow.SavedDesktopId))
        {
            return new DesktopMoveResult(VirtualDesktopMoveStatus.NotApplicable, null);
        }

        if (!_virtualDesktopService.CanMoveWindow)
        {
            return new DesktopMoveResult(VirtualDesktopMoveStatus.Fallback, "Virtual desktop move capability unavailable.");
        }

        if (!Guid.TryParse(targetWindow.SavedDesktopId, out var desktopId))
        {
            return new DesktopMoveResult(VirtualDesktopMoveStatus.Fallback, "Saved virtual desktop id is invalid.");
        }

        if (VirtualDesktopReadHelper.TryGetDesktopIdWithRetry(_virtualDesktopService, handle, out var currentDesktopId)
            && currentDesktopId == desktopId)
        {
            return new DesktopMoveResult(VirtualDesktopMoveStatus.NotApplicable, null);
        }

        return _virtualDesktopService.TryMoveWindowToDesktop(handle, desktopId, out var error)
            ? new DesktopMoveResult(VirtualDesktopMoveStatus.Moved, null)
            : new DesktopMoveResult(
                VirtualDesktopMoveStatus.Fallback,
                string.IsNullOrWhiteSpace(error) ? "Unable to move window to saved virtual desktop." : error);
    }

    private readonly struct DesktopMoveResult
    {
        public DesktopMoveResult(VirtualDesktopMoveStatus status, string? errorMessage)
        {
            Status = status;
            ErrorMessage = errorMessage;
        }

        public VirtualDesktopMoveStatus Status { get; }

        public string? ErrorMessage { get; }
    }
}
