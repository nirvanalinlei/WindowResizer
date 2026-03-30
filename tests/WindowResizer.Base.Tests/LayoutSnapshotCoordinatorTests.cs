using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using WindowResizer.Base;
using WindowResizer.Base.Abstractions;
using WindowResizer.Base.Coordinators;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class LayoutSnapshotCoordinatorTests
{
    [Fact]
    public void SaveAll_CreatesSnapshotEntries_AndDoesNotMutateWindowRules()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "existing.exe",
            Title = "*",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Maximized,
            SavedDesktopId = "stale-rule"
        });

        var windowsBefore = JsonConvert.SerializeObject(config.WindowSizes.ToList());
        var firstDesktopId = Guid.NewGuid();
        var secondDesktopId = Guid.NewGuid();
        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(101), "notepad.exe", "a.txt", "Notepad", WindowState.Normal),
            new WindowDescriptor(new IntPtr(102), "code.exe", "repo", "Chrome_WidgetWin_1", WindowState.Normal, WindowVisibilityState.Cloaked),
            new WindowDescriptor(new IntPtr(103), "skip.exe", "ignored", "Skip", WindowState.Minimized));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(101)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(1, 2)
        };
        placementService.Placements[new IntPtr(102)] = new WindowPlacement
        {
            Rect = new Rect(30, 40, 130, 140),
            WindowState = WindowState.Maximized,
            MaximizedPosition = new Point(3, 4)
        };

        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(101)] = firstDesktopId,
                [new IntPtr(102)] = secondDesktopId
            }
        };
        var configurationStore = new FakeConfigurationStore();
        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            configurationStore,
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.True(configurationStore.SaveCalled);
        Assert.Equal(windowsBefore, JsonConvert.SerializeObject(config.WindowSizes.ToList()));
        Assert.NotNull(config.CurrentLayoutSnapshot);
        Assert.Equal(2, config.CurrentLayoutSnapshot!.Entries.Count);
        Assert.Equal(2, result.SavedWindowCount);
        Assert.Equal(2, result.DesktopCount);
        Assert.Equal(1, result.CloakedWindowCount);

        var firstEntry = config.CurrentLayoutSnapshot.Entries[0];
        Assert.Equal("notepad.exe", firstEntry.ProcessName);
        Assert.Equal("a.txt", firstEntry.ExactTitle);
        Assert.Equal("Notepad", firstEntry.WindowClassName);
        Assert.Equal(firstDesktopId.ToString("D"), firstEntry.SavedDesktopId);
        Assert.Equal(1, firstEntry.CaptureOrder);

        var secondEntry = config.CurrentLayoutSnapshot.Entries[1];
        Assert.Equal("code.exe", secondEntry.ProcessName);
        Assert.Equal("repo", secondEntry.ExactTitle);
        Assert.Equal("Chrome_WidgetWin_1", secondEntry.WindowClassName);
        Assert.Equal(secondDesktopId.ToString("D"), secondEntry.SavedDesktopId);
        Assert.Equal(2, secondEntry.CaptureOrder);
    }

    [Fact]
    public void SaveAll_DisabledVirtualDesktopRestore_DoesNotPersistDesktopIds()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = false;

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(111), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(111)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(111)] = Guid.NewGuid()
            }
        };
        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Null(config.CurrentLayoutSnapshot.Entries[0].SavedDesktopId);
        Assert.Equal(1, result.SavedWindowCount);
        Assert.Equal(0, result.DesktopCount);
        Assert.Equal(0, result.CloakedWindowCount);
    }

    [Fact]
    public void SaveAll_ReadCapabilityDisabled_DoesNotPersistDesktopIds()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(112), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(112)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = false,
            CanMoveWindow = true
        };
        virtualDesktopService.WindowDesktopIds[new IntPtr(112)] = Guid.NewGuid();

        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Null(config.CurrentLayoutSnapshot.Entries[0].SavedDesktopId);
        Assert.Equal(0, result.DesktopCount);
    }

    [Fact]
    public void SaveAll_EmptyDesktopId_DoesNotPersistDesktopIds()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(114), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(114)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(114)] = Guid.Empty
            }
        };

        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Null(config.CurrentLayoutSnapshot.Entries[0].SavedDesktopId);
        Assert.Equal(0, result.DesktopCount);
    }

    [Fact]
    public void SaveAll_SkipsTransientOverflowWindow()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(115), "Explorer.EXE", "系统托盘溢出窗口。", "TopLevelWindowForOverflowXamlIsland", WindowState.Normal),
            new WindowDescriptor(new IntPtr(116), "notepad.exe", "note.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(116)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        placementService.Placements[new IntPtr(115)] = new WindowPlacement
        {
            Rect = new Rect(0, 0, 20, 20),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(116)] = Guid.NewGuid()
            }
        };

        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.Equal(1, result.SavedWindowCount);
        var entry = Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Equal("notepad.exe", entry.ProcessName);
        Assert.DoesNotContain(config.CurrentLayoutSnapshot.Entries, item => item.WindowClassName == "TopLevelWindowForOverflowXamlIsland");
    }

    [Fact]
    public void SaveAll_PlacementReadFailure_SkipsWindowAndContinuesBatch()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var desktopId = Guid.NewGuid();
        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(117), "broken.exe", "broken", "BrokenHost", WindowState.Normal),
            new WindowDescriptor(new IntPtr(118), "notepad.exe", "note.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.ThrowOnGetPlacement[new IntPtr(117)] = new InvalidOperationException("Placement unavailable.");
        placementService.Placements[new IntPtr(118)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(118)] = desktopId
            }
        };
        var configurationStore = new FakeConfigurationStore();
        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            configurationStore,
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.True(configurationStore.SaveCalled);
        Assert.Equal(1, result.SavedWindowCount);
        Assert.Equal(1, result.DesktopCount);
        var entry = Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Equal("notepad.exe", entry.ProcessName);
    }

    [Fact]
    public void SaveAll_TransientDesktopReadFailure_RetriesAndPersistsDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var desktopId = Guid.NewGuid();
        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(113), "notepad.exe", "retry.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(113)] = new WindowPlacement
        {
            Rect = new Rect(10, 20, 110, 120),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true
        };
        virtualDesktopService.WindowDesktopIdResponses[new IntPtr(113)] = new Queue<Guid?>(new Guid?[] { null, desktopId });

        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        var result = coordinator.SaveAll(config, null);

        Assert.Equal(1, result.SavedWindowCount);
        Assert.Equal(1, result.DesktopCount);
        Assert.Equal(desktopId.ToString("D"), Assert.Single(config.CurrentLayoutSnapshot!.Entries).SavedDesktopId);
        Assert.Equal(2, virtualDesktopService.GetDesktopIdCallCount[new IntPtr(113)]);
    }

    [Fact]
    public void RestoreAll_UsesSnapshotEntries_WhenRulesMissing()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        var desktopId = Guid.NewGuid();
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "a.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = desktopId.ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Maximized,
                    MaximizedPosition = new Point(7, 8),
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(121), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        var moveRequest = Assert.Single(virtualDesktopService.MoveRequests);
        Assert.Equal(new IntPtr(121), moveRequest.Handle);
        Assert.Equal(desktopId, moveRequest.DesktopId);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(new IntPtr(121), placementService.RestoredWindows[0].Handle);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.NoSnapshot);
    }

    [Fact]
    public void RestoreAll_WithoutSnapshot_IsNoOp()
    {
        var config = Config.NewConfig("default");
        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(131), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(placementService.RestoredWindows);
        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.True(result.NoSnapshot);
        Assert.Equal(0, result.RestoredCount);
    }

    [Fact]
    public void RestoreAll_SkipsMinimizedWindows_WhenIncludeMinimizedDisabled()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.RestoreAllIncludeMinimized = false;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "a.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(141), "notepad.exe", "a.txt", "Notepad", WindowState.Minimized));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(placementService.RestoredWindows);
        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Equal(0, result.RestoredCount);
    }

    [Fact]
    public void RestoreAll_InvalidDesktopId_FallsBackToPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "a.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = "bad-guid",
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(151), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(1, result.MoveFallbackCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void RestoreAll_EmptyDesktopId_SkipsMoveAndRestoresPlacement()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "empty-guid.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.Empty.ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(156), "notepad.exe", "empty-guid.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
    }

    [Fact]
    public void RestoreAll_MoveCapabilityDisabled_FallsBackToPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "a.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(152), "notepad.exe", "a.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = false,
            MoveResult = true
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(1, result.MoveFallbackCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void RestoreAll_WindowAlreadyOnSavedDesktop_DoesNotCountMoveFallback()
    {
        var desktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "already-there.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = desktopId.ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(153), "notepad.exe", "already-there.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = false,
            WindowDesktopIds =
            {
                [new IntPtr(153)] = desktopId
            }
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void RestoreAll_WindowAlreadyOnSavedDesktop_DoesNotCountMoveFallback_WhenMoveCapabilityDisabled()
    {
        var desktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "already-there-but-no-move.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = desktopId.ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(1531), "notepad.exe", "already-there-but-no-move.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = false,
            MoveResult = false,
            WindowDesktopIds =
            {
                [new IntPtr(1531)] = desktopId
            }
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
        Assert.Empty(result.MoveFallbackEntries);
    }

    [Fact]
    public void RestoreAll_TransientDesktopReadFailure_ButAlreadyOnSavedDesktop_DoesNotCountMoveFallback()
    {
        var desktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "retry-current.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = desktopId.ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(154), "notepad.exe", "retry-current.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = false
        };
        virtualDesktopService.WindowDesktopIdResponses[new IntPtr(154)] = new Queue<Guid?>(new Guid?[] { null, desktopId });

        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(virtualDesktopService.MoveRequests);
        Assert.Single(placementService.RestoredWindows);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
        Assert.Equal(2, virtualDesktopService.GetDesktopIdCallCount[new IntPtr(154)]);
    }

    [Fact]
    public void RestoreAll_DuplicateWindows_PairsByCaptureOrderAndEnumerationOrder()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "shared.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "shared.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    MaximizedPosition = new Point(9, 10),
                    CaptureOrder = 2
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(202), "notepad.exe", "shared.txt", "Notepad", WindowState.Normal),
            new WindowDescriptor(new IntPtr(201), "notepad.exe", "shared.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(2, placementService.RestoredWindows.Count);

        Assert.Equal(new IntPtr(202), placementService.RestoredWindows[0].Handle);
        Assert.Equal(new Rect(10, 20, 110, 120), placementService.RestoredWindows[0].WindowSize.Rect);
        Assert.Equal(WindowState.Normal, placementService.RestoredWindows[0].WindowSize.State);

        Assert.Equal(new IntPtr(201), placementService.RestoredWindows[1].Handle);
        Assert.Equal(new Rect(30, 40, 130, 140), placementService.RestoredWindows[1].WindowSize.Rect);
        Assert.Equal(WindowState.Maximized, placementService.RestoredWindows[1].WindowSize.State);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_MatchesUniqueBestCandidateWithinProcessAndClass()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Sierra Chart EURUSD 2026-03-30 00:50:26 Connecting",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(211), "SierraChart_64.exe", "Sierra Chart EURUSD 2026-03-30 00:55:44 Downloading", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(212), "SierraChart_64.exe", "Trade Service Log", "Afx:0000000140000000:30", WindowState.Normal),
            new WindowDescriptor(new IntPtr(213), "WindowsTerminal.exe", "Sierra Chart EURUSD 2026-03-30 00:55:44 Downloading", "Afx:0000000140000000:20", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(0, result.FailedCount);
        var restored = Assert.Single(placementService.RestoredWindows);
        Assert.Equal(new IntPtr(211), restored.Handle);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_PrefersExactTitleOverFuzzyCandidate()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chart Values for Tools",
                    WindowClassName = "Afx:0000000140000000:820",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(221), "SierraChart_64.exe", "#1 EURUSD - Chart Values For Tools", "Afx:0000000140000000:820", WindowState.Normal),
            new WindowDescriptor(new IntPtr(222), "SierraChart_64.exe", "Chart Values for Tools", "Afx:0000000140000000:820", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        var restored = Assert.Single(placementService.RestoredWindows);
        Assert.Equal(new IntPtr(222), restored.Handle);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_DoesNotMatchAmbiguousCandidates()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chart Values for Tools",
                    WindowClassName = "Afx:0000000140000000:820",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(231), "SierraChart_64.exe", "#1 EURUSD - Chart Values For Tools", "Afx:0000000140000000:820", WindowState.Normal),
            new WindowDescriptor(new IntPtr(232), "SierraChart_64.exe", "#2 GBPUSD - Chart Values For Tools", "Afx:0000000140000000:820", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Empty(placementService.RestoredWindows);
        Assert.Equal(0, result.RestoredCount);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Contains("SierraChart_64.exe :: Chart Values for Tools :: Afx:0000000140000000:820", result.UnmatchedEntries);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_UsesRemainingCandidateAfterExactMatchWithinSameProcessAndClass()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook A",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook B 2026-03-30 00:50:26 Connecting",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    CaptureOrder = 2
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(241), "SierraChart_64.exe", "Chartbook A", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(242), "SierraChart_64.exe", "Chartbook B 2026-03-30 00:55:44 Downloading", "Afx:0000000140000000:20", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(new IntPtr(241), placementService.RestoredWindows[0].Handle);
        Assert.Equal(new IntPtr(242), placementService.RestoredWindows[1].Handle);
    }

    [Fact]
    public void RestoreAll_ExactTitleMatching_RemainsStableAcrossMultipleTitleGroupsWithinSameProcessAndClass()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook A",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook B",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    CaptureOrder = 2
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook A",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(50, 60, 150, 160),
                    State = WindowState.Normal,
                    CaptureOrder = 3
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Chartbook B",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(70, 80, 170, 180),
                    State = WindowState.Maximized,
                    CaptureOrder = 4
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(245), "SierraChart_64.exe", "Chartbook B", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(246), "SierraChart_64.exe", "Chartbook A", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(247), "SierraChart_64.exe", "Chartbook B", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(248), "SierraChart_64.exe", "Chartbook A", "Afx:0000000140000000:20", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(4, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(new IntPtr(246), placementService.RestoredWindows[0].Handle);
        Assert.Equal(new IntPtr(245), placementService.RestoredWindows[1].Handle);
        Assert.Equal(new IntPtr(248), placementService.RestoredWindows[2].Handle);
        Assert.Equal(new IntPtr(247), placementService.RestoredWindows[3].Handle);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_PairsMultipleCandidatesByUniqueTitleSimilarity()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Sierra Chart EURUSD 2026-03-30 00:50:26 Connecting",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "Sierra Chart GBPUSD 2026-03-30 00:50:26 Connecting",
                    WindowClassName = "Afx:0000000140000000:20",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    CaptureOrder = 2
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(251), "SierraChart_64.exe", "Sierra Chart GBPUSD 2026-03-30 00:55:44 Downloading", "Afx:0000000140000000:20", WindowState.Normal),
            new WindowDescriptor(new IntPtr(252), "SierraChart_64.exe", "Sierra Chart EURUSD 2026-03-30 00:55:44 Downloading", "Afx:0000000140000000:20", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(new IntPtr(252), placementService.RestoredWindows[0].Handle);
        Assert.Equal(new IntPtr(251), placementService.RestoredWindows[1].Handle);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_UsesHashNumberTokenToDisambiguateOtherwiseIdenticalTitles()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "#1 USD-X[M] 30 Sec Connecting",
                    WindowClassName = "SCDW_FloatingChart",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "#5 USD-X[M] 30 Sec Connecting",
                    WindowClassName = "SCDW_FloatingChart",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    CaptureOrder = 2
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(271), "SierraChart_64.exe", "#5 USD-X[M] 30 Sec Downloading", "SCDW_FloatingChart", WindowState.Normal),
            new WindowDescriptor(new IntPtr(272), "SierraChart_64.exe", "#1 USD-X[M] 30 Sec Downloading", "SCDW_FloatingChart", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(new IntPtr(272), placementService.RestoredWindows[0].Handle);
        Assert.Equal(new IntPtr(271), placementService.RestoredWindows[1].Handle);
    }

    [Fact]
    public void RestoreAll_DynamicTitleFallback_DoesNotMatchSingleCandidateWithDifferentHashNumberToken()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "SierraChart_64.exe",
                    ExactTitle = "#1 USD-X[M] 30 Sec Connecting",
                    WindowClassName = "SCDW_FloatingChart",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(273), "SierraChart_64.exe", "#5 USD-X[M] 30 Sec Downloading", "SCDW_FloatingChart", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(0, result.RestoredCount);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Empty(placementService.RestoredWindows);
        Assert.Contains("SierraChart_64.exe :: #1 USD-X[M] 30 Sec Connecting :: SCDW_FloatingChart", result.UnmatchedEntries);
    }

    [Fact]
    public void RestoreAll_IgnoresTransientOverflowSnapshotEntries()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "Explorer.EXE",
                    ExactTitle = "系统托盘溢出窗口。",
                    WindowClassName = "TopLevelWindowForOverflowXamlIsland",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(0, 0, 20, 20),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "real.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 2
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(261), "Explorer.EXE", "系统托盘溢出窗口。", "TopLevelWindowForOverflowXamlIsland", WindowState.Normal),
            new WindowDescriptor(new IntPtr(262), "notepad.exe", "real.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(new IntPtr(262), Assert.Single(placementService.RestoredWindows).Handle);
    }

    [Fact]
    public void RestoreAll_PartialBatchFailures_ReportAccurateCounts()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "first.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(10, 20, 110, 120),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "code.exe",
                    ExactTitle = "second.txt",
                    WindowClassName = "Chrome_WidgetWin_1",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 130, 140),
                    State = WindowState.Maximized,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "missing.exe",
                    ExactTitle = "third.txt",
                    WindowClassName = "Missing",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(50, 60, 150, 160),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(301), "notepad.exe", "first.txt", "Notepad", WindowState.Normal),
            new WindowDescriptor(new IntPtr(302), "code.exe", "second.txt", "Chrome_WidgetWin_1", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.RestoreResults[new IntPtr(302)] = false;
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true,
            FailedMoveHandles =
            {
                new IntPtr(301)
            }
        };
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, virtualDesktopService.MoveRequests.Count);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(1, result.MoveFallbackCount);
        Assert.Equal(1, result.UnmatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Contains("notepad.exe :: first.txt :: Notepad [Move failed]", result.MoveFallbackEntries);
        Assert.Contains("missing.exe :: third.txt :: Missing", result.UnmatchedEntries);
        Assert.Contains("code.exe :: second.txt :: Chrome_WidgetWin_1 [Unable to restore placement.]", result.FailedEntries);
    }

    [Fact]
    public void RestoreAll_ElevatedMoveFailure_RecordsMoveFallbackReason_AndContinuesPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "admin-app.exe",
                    ExactTitle = "Admin Window",
                    WindowClassName = "AdminHost",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(100, 120, 420, 360),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "safe.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(30, 40, 330, 240),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(501), "admin-app.exe", "Admin Window", "AdminHost", WindowState.Normal),
            new WindowDescriptor(new IntPtr(502), "notepad.exe", "safe.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        virtualDesktopService.MoveErrors[new IntPtr(501)] = "Access is denied.";

        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, result.RestoredCount);
        Assert.Equal(1, result.MoveFallbackCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Contains("admin-app.exe :: Admin Window :: AdminHost [Access is denied.]", result.MoveFallbackEntries);
        Assert.Equal(2, placementService.RestoredWindows.Count);
    }

    [Fact]
    public void RestoreAll_ElevatedPlacementFailureAfterSuccessfulMove_RecordsFailure_AndContinuesBatch()
    {
        var savedDesktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "admin-app.exe",
                    ExactTitle = "Admin Window",
                    WindowClassName = "AdminHost",
                    SavedDesktopId = savedDesktopId.ToString("D"),
                    Rect = new Rect(100, 120, 420, 360),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                },
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "safe.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = savedDesktopId.ToString("D"),
                    Rect = new Rect(30, 40, 330, 240),
                    State = WindowState.Normal,
                    CaptureOrder = 1
                }
            }
        };

        var disturbedDesktopId = Guid.NewGuid();
        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(601), "admin-app.exe", "Admin Window", "AdminHost", WindowState.Normal),
            new WindowDescriptor(new IntPtr(602), "notepad.exe", "safe.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.RestoreResults[new IntPtr(601)] = false;
        placementService.RestoreErrors[new IntPtr(601)] = "Access is denied.";

        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true,
            WindowDesktopIds =
            {
                [new IntPtr(601)] = disturbedDesktopId,
                [new IntPtr(602)] = disturbedDesktopId
            }
        };

        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            virtualDesktopService);

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(2, virtualDesktopService.MoveRequests.Count);
        Assert.Equal(1, result.RestoredCount);
        Assert.Equal(0, result.MoveFallbackCount);
        Assert.Equal(0, result.UnmatchedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Empty(result.MoveFallbackEntries);
        Assert.Single(result.FailedEntries);
        Assert.Contains("admin-app.exe :: Admin Window :: AdminHost [Access is denied.]", result.FailedEntries);
        Assert.DoesNotContain("notepad.exe :: safe.txt :: Notepad", result.FailedEntries);
        Assert.Equal(2, placementService.RestoredWindows.Count);
        Assert.Contains(virtualDesktopService.MoveRequests, request => request.Handle == new IntPtr(601) && request.DesktopId == savedDesktopId);
        Assert.Contains(virtualDesktopService.MoveRequests, request => request.Handle == new IntPtr(602) && request.DesktopId == savedDesktopId);
    }

    [Fact]
    public void SaveAll_PreservesNegativeScreenCoordinatesInSnapshotEntries()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(401), "notepad.exe", "left-screen.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        placementService.Placements[new IntPtr(401)] = new WindowPlacement
        {
            Rect = new Rect(-1700, -940, -1380, -720),
            WindowState = WindowState.Maximized,
            MaximizedPosition = new Point(-8, -16)
        };
        var virtualDesktopService = new FakeBatchVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            WindowDesktopIds =
            {
                [new IntPtr(401)] = Guid.NewGuid()
            }
        };
        var coordinator = new LayoutSnapshotSaveCoordinator(
            contextService,
            placementService,
            new FakeConfigurationStore(),
            virtualDesktopService);

        coordinator.SaveAll(config, null);

        var entry = Assert.Single(config.CurrentLayoutSnapshot!.Entries);
        Assert.Equal(new Rect(-1700, -940, -1380, -720), entry.Rect);
        Assert.Equal(new Point(-8, -16), entry.MaximizedPosition);
    }

    [Fact]
    public void RestoreAll_PreservesNegativeScreenCoordinatesInPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.CurrentLayoutSnapshot = new WindowLayoutSnapshot
        {
            Entries = new List<WindowLayoutSnapshotEntry>
            {
                new()
                {
                    ProcessName = "notepad.exe",
                    ExactTitle = "left-screen.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(-1700, -940, -1380, -720),
                    State = WindowState.Maximized,
                    MaximizedPosition = new Point(-8, -16),
                    CaptureOrder = 1
                }
            }
        };

        var contextService = new FakeBatchWindowContextService(
            new WindowDescriptor(new IntPtr(402), "notepad.exe", "left-screen.txt", "Notepad", WindowState.Normal));
        var placementService = new FakeBatchWindowPlacementService();
        var coordinator = new LayoutSnapshotRestoreCoordinator(
            contextService,
            placementService,
            new FakeBatchVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                MoveResult = true
            });

        var result = coordinator.RestoreAll(config, null);

        Assert.Equal(1, result.RestoredCount);
        var restored = Assert.Single(placementService.RestoredWindows);
        Assert.Equal(new Rect(-1700, -940, -1380, -720), restored.WindowSize.Rect);
        Assert.Equal(new Point(-8, -16), restored.WindowSize.MaximizedPosition);
    }

    private sealed class FakeBatchWindowContextService : IWindowContextService
    {
        private readonly List<WindowDescriptor> _windows;

        public FakeBatchWindowContextService(params WindowDescriptor[] windows)
        {
            _windows = windows.ToList();
        }

        public IReadOnlyList<IntPtr> GetOpenWindows()
        {
            return _windows.Select(window => window.Handle).ToList();
        }

        public WindowState GetWindowState(IntPtr handle)
        {
            return _windows.First(window => window.Handle == handle).State;
        }

        public WindowVisibilityState GetWindowVisibilityState(IntPtr handle)
        {
            return _windows.First(window => window.Handle == handle).VisibilityState;
        }

        public bool TryGetWindowContext(IntPtr handle, Action<Process, Exception>? onFailed, out WindowRuleContext context)
        {
            var descriptor = _windows.FirstOrDefault(window => window.Handle == handle);
            if (descriptor is null)
            {
                context = null!;
                return false;
            }

            context = new WindowRuleContext(
                descriptor.Handle,
                descriptor.ProcessName,
                descriptor.Title,
                descriptor.WindowClassName);
            return true;
        }
    }

    private sealed class FakeBatchWindowPlacementService : IWindowPlacementService
    {
        public Dictionary<IntPtr, WindowPlacement> Placements { get; } = new();

        public Dictionary<IntPtr, Exception> ThrowOnGetPlacement { get; } = new();

        public Dictionary<IntPtr, bool> RestoreResults { get; } = new();

        public Dictionary<IntPtr, string> RestoreErrors { get; } = new();

        public List<RestoredPlacement> RestoredWindows { get; } = new();

        public WindowPlacement GetPlacement(IntPtr handle)
        {
            if (ThrowOnGetPlacement.TryGetValue(handle, out var exception))
            {
                throw exception;
            }

            return Placements[handle];
        }

        public bool RestorePlacement(IntPtr handle, WindowSize windowSize, out string? errorMessage)
        {
            RestoredWindows.Add(new RestoredPlacement(handle, windowSize));
            var restored = !RestoreResults.TryGetValue(handle, out var result) || result;
            errorMessage = restored
                ? null
                : (RestoreErrors.TryGetValue(handle, out var restoreError) ? restoreError : "Unable to restore placement.");
            return restored;
        }
    }

    private sealed class FakeBatchVirtualDesktopService : IVirtualDesktopService
    {
        public bool CanReadDesktopId { get; set; }

        public bool CanMoveWindow { get; set; }

        public bool MoveResult { get; set; } = true;

        public HashSet<IntPtr> FailedMoveHandles { get; } = new();

        public Dictionary<IntPtr, Guid> WindowDesktopIds { get; } = new();

        public Dictionary<IntPtr, Queue<Guid?>> WindowDesktopIdResponses { get; } = new();

        public Dictionary<IntPtr, int> GetDesktopIdCallCount { get; } = new();

        public Dictionary<IntPtr, string> MoveErrors { get; } = new();

        public List<MoveRequest> MoveRequests { get; } = new();

        public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
        {
            if (!GetDesktopIdCallCount.ContainsKey(hWnd))
            {
                GetDesktopIdCallCount[hWnd] = 0;
            }

            GetDesktopIdCallCount[hWnd]++;
            if (!CanReadDesktopId)
            {
                desktopId = Guid.Empty;
                return false;
            }

            if (WindowDesktopIdResponses.TryGetValue(hWnd, out var responses) && responses.Count > 0)
            {
                var next = responses.Dequeue();
                if (next.HasValue)
                {
                    desktopId = next.Value;
                    return true;
                }

                desktopId = Guid.Empty;
                return false;
            }

            return WindowDesktopIds.TryGetValue(hWnd, out desktopId);
        }

        public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
        {
            isOnCurrentDesktop = true;
            return true;
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            if (!CanMoveWindow)
            {
                error = "Virtual desktop move capability unavailable.";
                return false;
            }

            MoveRequests.Add(new MoveRequest(hWnd, desktopId));
            var moved = MoveResult && !FailedMoveHandles.Contains(hWnd) && !MoveErrors.ContainsKey(hWnd);
            error = moved ? null : (MoveErrors.TryGetValue(hWnd, out var moveError) ? moveError : "Move failed");
            return moved;
        }
    }

    private sealed class FakeConfigurationStore : IConfigurationStore
    {
        public bool SaveCalled { get; private set; }

        public void Save()
        {
            SaveCalled = true;
        }
    }

    private sealed class WindowDescriptor
    {
        public WindowDescriptor(
            IntPtr handle,
            string processName,
            string title,
            string windowClassName,
            WindowState state,
            WindowVisibilityState visibilityState = WindowVisibilityState.Visible)
        {
            Handle = handle;
            ProcessName = processName;
            Title = title;
            WindowClassName = windowClassName;
            State = state;
            VisibilityState = visibilityState;
        }

        public IntPtr Handle { get; }

        public string ProcessName { get; }

        public string Title { get; }

        public string WindowClassName { get; }

        public WindowState State { get; }

        public WindowVisibilityState VisibilityState { get; }
    }

    private sealed class RestoredPlacement
    {
        public RestoredPlacement(IntPtr handle, WindowSize windowSize)
        {
            Handle = handle;
            WindowSize = windowSize;
        }

        public IntPtr Handle { get; }

        public WindowSize WindowSize { get; }
    }

    private sealed class MoveRequest
    {
        public MoveRequest(IntPtr handle, Guid desktopId)
        {
            Handle = handle;
            DesktopId = desktopId;
        }

        public IntPtr Handle { get; }

        public Guid DesktopId { get; }
    }
}
