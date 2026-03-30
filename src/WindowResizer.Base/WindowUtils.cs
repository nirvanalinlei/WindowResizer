using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using WindowResizer.Base.Coordinators;
using WindowResizer.Base.Services;
using WindowResizer.Common.Shortcuts;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;
using WindowResizer.Core.WindowControl;

namespace WindowResizer.Base;

public static class WindowUtils
{
    public static WindowRestoreResult ResizeWindow(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string, string>? onConfigNoMatch,
        bool onlyAuto = false,
        bool allowVirtualDesktopMove = true)
    {
        return CreateRuleRestoreCoordinator().RestoreWindow(
            handle,
            config,
            onFailed,
            onConfigNoMatch,
            onlyAuto,
            allowVirtualDesktopMove);
    }

    public static void AutoRestoreWindow(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string, string>? onConfigNoMatch)
    {
        CreateAutoRestoreCoordinator().RestoreWindow(handle, config, onFailed, onConfigNoMatch);
    }

    public static bool ResizeAllWindow(Config profile, Action<string>? onError)
    {
        var result = RestoreWindowLayoutSnapshot(profile, null);
        if (result.NoSnapshot)
        {
            onError?.Invoke("Current profile has no saved layout snapshot.");
        }

        return true;
    }

    public static void UpdateOrSaveWindowSize(
        IntPtr handle,
        Config config,
        Action<Process, Exception>? onFailed,
        Action<string>? onSuccess = null,
        bool allowVirtualDesktopCapture = true)
    {
        CreateRuleSaveCoordinator().SaveWindow(handle, config, onFailed, onSuccess, allowVirtualDesktopCapture);
    }

    public static LayoutSnapshotSaveResult SaveWindowLayoutSnapshot(
        Config config,
        Action<Process, Exception>? onFailed)
    {
        return CreateLayoutSnapshotSaveCoordinator().SaveAll(config, onFailed);
    }

    public static LayoutSnapshotRestoreResult RestoreWindowLayoutSnapshot(
        Config config,
        Action<Process, Exception>? onFailed)
    {
        return CreateLayoutSnapshotRestoreCoordinator().RestoreAll(config, onFailed);
    }

    public static bool IsProcessAvailable(IntPtr handle, out string processName, Action<Process, Exception>? onFailed)
    {
        return WindowProcessNameResolver.TryGetProcessName(
            handle,
            Resizer.IsChildWindow,
            Resizer.GetRealProcess,
            process => process.MainModule?.ModuleName ?? string.Empty,
            process => process.ProcessName,
            Resizer.IsInvisibleProcess,
            onFailed,
            out _,
            out processName);
    }

    public static Hotkeys? GetKeys(HotkeysType type) =>
        ConfigFactory.Current.GetKeys(type);

    private static RuleSaveCoordinator CreateRuleSaveCoordinator()
    {
        var matcher = new WindowRuleMatcher();
        return new RuleSaveCoordinator(
            new WindowContextService(),
            new WindowPlacementService(),
            new ConfigurationStore(),
            CreateVirtualDesktopService(),
            matcher,
            new WindowRuleUpdater(matcher));
    }

    private static RuleRestoreCoordinator CreateRuleRestoreCoordinator()
    {
        var matcher = new WindowRuleMatcher();
        return new RuleRestoreCoordinator(
            new WindowContextService(),
            new WindowPlacementService(),
            CreateVirtualDesktopService(),
            matcher);
    }

    private static AutoRestoreCoordinator CreateAutoRestoreCoordinator()
    {
        var matcher = new WindowRuleMatcher();
        return new AutoRestoreCoordinator(
            new WindowContextService(),
            new WindowWaitService(),
            new RuleRestoreCoordinator(
                new WindowContextService(),
                new WindowPlacementService(),
                CreateVirtualDesktopService(),
                matcher));
    }

    private static LayoutSnapshotSaveCoordinator CreateLayoutSnapshotSaveCoordinator()
    {
        return new LayoutSnapshotSaveCoordinator(
            new WindowContextService(),
            new WindowPlacementService(),
            new ConfigurationStore(),
            CreateVirtualDesktopService());
    }

    private static LayoutSnapshotRestoreCoordinator CreateLayoutSnapshotRestoreCoordinator()
    {
        return new LayoutSnapshotRestoreCoordinator(
            new WindowContextService(),
            new WindowPlacementService(),
            CreateVirtualDesktopService());
    }

    private static IVirtualDesktopService CreateVirtualDesktopService()
    {
        return VirtualDesktopServiceFactory.Create();
    }
}
