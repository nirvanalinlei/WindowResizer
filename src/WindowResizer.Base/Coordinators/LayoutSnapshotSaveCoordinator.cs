using System;
using System.Collections.Generic;
using System.Diagnostics;
using WindowResizer.Base.Abstractions;
using WindowResizer.Common.Exceptions;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;

namespace WindowResizer.Base.Coordinators;

public sealed class LayoutSnapshotSaveCoordinator
{
    private readonly IWindowContextService _windowContextService;
    private readonly IWindowPlacementService _windowPlacementService;
    private readonly IConfigurationStore _configurationStore;
    private readonly IVirtualDesktopService _virtualDesktopService;

    public LayoutSnapshotSaveCoordinator(
        IWindowContextService windowContextService,
        IWindowPlacementService windowPlacementService,
        IConfigurationStore configurationStore,
        IVirtualDesktopService virtualDesktopService)
    {
        _windowContextService = windowContextService;
        _windowPlacementService = windowPlacementService;
        _configurationStore = configurationStore;
        _virtualDesktopService = virtualDesktopService;
    }

    public LayoutSnapshotSaveResult SaveAll(Config config, Action<Process, Exception>? onFailed)
    {
        var snapshot = new WindowLayoutSnapshot();
        var desktopIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var captureOrder = 0;
        var cloakedWindowCount = 0;

        foreach (var handle in _windowContextService.GetOpenWindows())
        {
            if (_windowContextService.GetWindowState(handle) == WindowState.Minimized)
            {
                continue;
            }

            if (!_windowContextService.TryGetWindowContext(handle, onFailed, out var context))
            {
                continue;
            }

            if (ShouldSkipContext(context))
            {
                continue;
            }

            if (_windowContextService.GetWindowVisibilityState(handle) == WindowVisibilityState.Cloaked)
            {
                cloakedWindowCount++;
            }

            WindowPlacement placement;
            try
            {
                placement = _windowPlacementService.GetPlacement(handle);
            }
            catch (Exception exception) when (CanSkipPlacementFailure(handle, exception))
            {
                continue;
            }

            var savedDesktopId = ResolveSavedDesktopId(config, handle);
            if (!string.IsNullOrWhiteSpace(savedDesktopId))
            {
                desktopIds.Add(savedDesktopId!);
            }

            snapshot.Entries.Add(new WindowLayoutSnapshotEntry
            {
                ProcessName = context.ProcessName,
                ExactTitle = context.Title,
                WindowClassName = context.WindowClassName,
                SavedDesktopId = savedDesktopId,
                Rect = placement.Rect,
                State = placement.WindowState,
                MaximizedPosition = placement.MaximizedPosition,
                CaptureOrder = ++captureOrder
            });
        }

        config.CurrentLayoutSnapshot = snapshot;
        _configurationStore.Save();
        return new LayoutSnapshotSaveResult(snapshot.Entries.Count, desktopIds.Count, cloakedWindowCount);
    }

    private string? ResolveSavedDesktopId(Config config, IntPtr handle)
    {
        if (!config.EnableVirtualDesktopRestore)
        {
            return null;
        }

        return VirtualDesktopReadHelper.TryGetDesktopIdWithRetry(_virtualDesktopService, handle, out var desktopId)
               && desktopId != Guid.Empty
            ? desktopId.ToString("D")
            : null;
    }

    private static bool ShouldSkipContext(WindowRuleContext context)
    {
        return string.Equals(context.WindowClassName, "TopLevelWindowForOverflowXamlIsland", StringComparison.Ordinal);
    }

    private static bool CanSkipPlacementFailure(IntPtr handle, Exception exception)
    {
        return handle != IntPtr.Zero
               && (exception is WindowResizerException || exception is InvalidOperationException);
    }
}
