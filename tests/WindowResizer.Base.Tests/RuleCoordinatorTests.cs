using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using WindowResizer.Base;
using WindowResizer.Base.Abstractions;
using WindowResizer.Base.Coordinators;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class RuleCoordinatorTests
{
    [Fact]
    public void SaveWindow_ExactTitleRule_PersistsSavedDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var desktopId = Guid.NewGuid();
        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(11), "notepad.exe", "note.txt", "Notepad"),
            new FakeVirtualDesktopService
            {
                DesktopId = desktopId,
                CanReadDesktopId = true,
                CanMoveWindow = true,
                GetDesktopIdResult = true
            },
            out var configurationStore);

        coordinator.SaveWindow(new IntPtr(11), config, null);

        Assert.True(configurationStore.SaveCalled);
        Assert.Equal(2, config.WindowSizes.Count);
        Assert.Null(config.WindowSizes[0].SavedDesktopId);
        Assert.Equal(desktopId.ToString("D"), config.WindowSizes[1].SavedDesktopId);
    }

    [Fact]
    public void SaveWindow_DisabledCapture_DoesNotPersistSavedDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(21), "notepad.exe", "note.txt", "Notepad"),
            new FakeVirtualDesktopService
            {
                DesktopId = Guid.NewGuid(),
                CanReadDesktopId = true,
                CanMoveWindow = true,
                GetDesktopIdResult = true
            },
            out _);

        coordinator.SaveWindow(new IntPtr(21), config, null, allowVirtualDesktopCapture: false);

        Assert.All(config.WindowSizes, windowSize => Assert.Null(windowSize.SavedDesktopId));
    }

    [Fact]
    public void SaveWindow_ReadCapabilityDisabled_DoesNotPersistSavedDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(25), "notepad.exe", "note.txt", "Notepad"),
            new FakeVirtualDesktopService
            {
                DesktopId = Guid.NewGuid(),
                CanReadDesktopId = false,
                CanMoveWindow = true,
                GetDesktopIdResult = true
            },
            out _);

        coordinator.SaveWindow(new IntPtr(25), config, null);

        Assert.All(config.WindowSizes, windowSize => Assert.Null(windowSize.SavedDesktopId));
    }

    [Fact]
    public void SaveWindow_TransientDesktopReadFailure_RetriesAndPersistsSavedDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var desktopId = Guid.NewGuid();
        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true
        };
        virtualDesktopService.DesktopIdResponses.Enqueue(null);
        virtualDesktopService.DesktopIdResponses.Enqueue(desktopId);

        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(26), "notepad.exe", "retry.txt", "Notepad"),
            virtualDesktopService,
            out _);

        coordinator.SaveWindow(new IntPtr(26), config, null);

        var savedRule = Assert.Single(config.WindowSizes, windowSize => windowSize.Title == "retry.txt");
        Assert.Equal(desktopId.ToString("D"), savedRule.SavedDesktopId);
        Assert.Equal(2, virtualDesktopService.GetDesktopIdCalls);
    }

    [Fact]
    public void RestoreWindow_ExactTitleRule_MovesWindowToSavedDesktop()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(31), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(31), config, null, null);

        Assert.Equal(1, virtualDesktopService.MoveCalls);
        Assert.Equal(1, placementService.RestoreCalls);
        Assert.True(result.MatchedConfig);
        Assert.True(result.PlacementRestored);
        Assert.Equal("note.txt", result.MatchedRuleTitle);
        Assert.Equal(VirtualDesktopMoveStatus.Moved, result.VirtualDesktopMoveStatus);
        Assert.Null(result.VirtualDesktopMoveErrorMessage);
    }

    [Fact]
    public void RestoreWindow_MoveCapabilityDisabled_FallsBackToPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = false,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(32), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(32), config, null, null);

        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(1, placementService.RestoreCalls);
        Assert.True(result.PlacementRestored);
        Assert.Equal(VirtualDesktopMoveStatus.Fallback, result.VirtualDesktopMoveStatus);
        Assert.Equal("Virtual desktop move capability unavailable.", result.VirtualDesktopMoveErrorMessage);
    }

    [Fact]
    public void RestoreWindow_AlreadyOnSavedDesktop_SkipsMoveAndStillRestoresPlacement()
    {
        var desktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "already-there.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = desktopId.ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            DesktopId = desktopId,
            GetDesktopIdResult = true,
            MoveResult = false
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(321), "notepad.exe", "already-there.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(321), config, null, null);

        Assert.True(result.PlacementRestored);
        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(VirtualDesktopMoveStatus.NotApplicable, result.VirtualDesktopMoveStatus);
        Assert.Null(result.VirtualDesktopMoveErrorMessage);
    }

    [Fact]
    public void RestoreWindow_TransientDesktopReadFailure_ButAlreadyOnSavedDesktop_SkipsMove()
    {
        var desktopId = Guid.NewGuid();
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "retry-current.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = desktopId.ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = false
        };
        virtualDesktopService.DesktopIdResponses.Enqueue(null);
        virtualDesktopService.DesktopIdResponses.Enqueue(desktopId);

        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(322), "notepad.exe", "retry-current.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(322), config, null, null);

        Assert.True(result.PlacementRestored);
        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(2, virtualDesktopService.GetDesktopIdCalls);
        Assert.Equal(VirtualDesktopMoveStatus.NotApplicable, result.VirtualDesktopMoveStatus);
        Assert.Null(result.VirtualDesktopMoveErrorMessage);
    }

    [Fact]
    public void SaveWindow_SharedRules_DoNotPersistSavedDesktopId()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "*",
            SavedDesktopId = "stale-wildcard"
        });
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "*suffix",
            SavedDesktopId = "stale-prefix"
        });
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "prefix*",
            SavedDesktopId = "stale-suffix"
        });

        var desktopId = Guid.NewGuid();
        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(35), "notepad.exe", "prefixsuffix", "Notepad"),
            new FakeVirtualDesktopService
            {
                DesktopId = desktopId,
                CanReadDesktopId = true,
                CanMoveWindow = true,
                GetDesktopIdResult = true
            },
            out _);

        coordinator.SaveWindow(new IntPtr(35), config, null);

        Assert.All(config.WindowSizes.Where(windowSize =>
                windowSize.Title == "*" || windowSize.Title == "*suffix" || windowSize.Title == "prefix*"),
            windowSize => Assert.Null(windowSize.SavedDesktopId));
        Assert.Contains(config.WindowSizes, windowSize => windowSize.Title == "prefixsuffix" && windowSize.SavedDesktopId == desktopId.ToString("D"));
    }

    [Fact]
    public void RestoreWindow_SharedRule_DoesNotMoveWindowAcrossVirtualDesktops()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "*",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(36), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(36), config, null, null);

        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(1, placementService.RestoreCalls);
        Assert.Equal(VirtualDesktopMoveStatus.NotApplicable, result.VirtualDesktopMoveStatus);
    }

    [Fact]
    public void AutoRestore_DoesNotMoveWindowAcrossVirtualDesktops()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D"),
            AutoResize = true
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var restoreCoordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(41), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());
        var autoCoordinator = new AutoRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(41), "notepad.exe", "note.txt", "Notepad")),
            new FakeWindowWaitService(),
            restoreCoordinator);

        autoCoordinator.RestoreWindow(new IntPtr(41), config, null, null);

        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(1, placementService.RestoreCalls);
    }

    [Fact]
    public void RestoreWindow_InvalidDesktopId_FallsBackToPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = "bad-guid"
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(37), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(37), config, null, null);

        Assert.Equal(0, virtualDesktopService.MoveCalls);
        Assert.Equal(1, placementService.RestoreCalls);
        Assert.True(result.PlacementRestored);
        Assert.Equal(VirtualDesktopMoveStatus.Fallback, result.VirtualDesktopMoveStatus);
        Assert.Equal("Saved virtual desktop id is invalid.", result.VirtualDesktopMoveErrorMessage);
    }

    [Fact]
    public void RestoreWindow_NoMatchingRule_ReturnsNoMatchResult()
    {
        var config = Config.NewConfig("default");
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(38), "notepad.exe", "note.txt", "Notepad")),
            placementService,
            new FakeVirtualDesktopService(),
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(38), config, null, null);

        Assert.False(result.MatchedConfig);
        Assert.False(result.PlacementRestored);
        Assert.Equal("No saved settings.", result.ErrorMessage);
        Assert.Equal(0, placementService.RestoreCalls);
        Assert.Equal(VirtualDesktopMoveStatus.NotApplicable, result.VirtualDesktopMoveStatus);
    }

    [Fact]
    public void RestoreWindow_MoveFailure_ReturnsDesktopMoveErrorMessage()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            Rect = new Rect(1, 2, 3, 4),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = false
        };
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(39), "notepad.exe", "note.txt", "Notepad")),
            new FakeWindowPlacementService(),
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(39), config, null, null);

        Assert.Equal(VirtualDesktopMoveStatus.Fallback, result.VirtualDesktopMoveStatus);
        Assert.Equal("Move failed", result.VirtualDesktopMoveErrorMessage);
        Assert.True(result.PlacementRestored);
    }

    [Fact]
    public void RestoreWindow_PlacementFailure_ReturnsPlacementErrorMessage()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "admin-app.exe",
            Title = "Admin Window",
            Rect = new Rect(10, 20, 300, 220),
            State = WindowState.Normal,
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var placementService = new FakeWindowPlacementService
        {
            RestoreResult = false,
            RestoreErrorMessage = "Access is denied."
        };
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(40), "admin-app.exe", "Admin Window", "AdminHost")),
            placementService,
            new FakeVirtualDesktopService
            {
                CanReadDesktopId = true,
                CanMoveWindow = true,
                GetDesktopIdResult = true,
                DesktopId = Guid.NewGuid(),
                MoveResult = true
            },
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(40), config, null, null);

        Assert.False(result.PlacementRestored);
        Assert.Equal("Access is denied.", result.ErrorMessage);
    }

    [Fact]
    public void SaveWindow_PreservesNegativeScreenCoordinatesInExactTitleRule()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;

        var desktopId = Guid.NewGuid();
        var placementService = new FakeWindowPlacementService
        {
            PlacementToReturn = new WindowPlacement
            {
                Rect = new Rect(-1700, -940, -1380, -720),
                WindowState = WindowState.Normal,
                MaximizedPosition = new Point(-8, -16)
            }
        };
        var coordinator = CreateRuleSaveCoordinator(
            new WindowRuleContext(new IntPtr(51), "notepad.exe", "left-monitor.txt", "Notepad"),
            placementService,
            new FakeVirtualDesktopService
            {
                DesktopId = desktopId,
                CanReadDesktopId = true,
                CanMoveWindow = true,
                GetDesktopIdResult = true
            },
            out _);

        coordinator.SaveWindow(new IntPtr(51), config, null);

        var savedRule = Assert.Single(config.WindowSizes, windowSize => windowSize.Title == "left-monitor.txt");
        Assert.Equal(new Rect(-1700, -940, -1380, -720), savedRule.Rect);
        Assert.Equal(new Point(-8, -16), savedRule.MaximizedPosition);
        Assert.Equal(desktopId.ToString("D"), savedRule.SavedDesktopId);
    }

    [Fact]
    public void RestoreWindow_PreservesNegativeScreenCoordinatesInPlacementRestore()
    {
        var config = Config.NewConfig("default");
        config.EnableVirtualDesktopRestore = true;
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "left-monitor.txt",
            Rect = new Rect(-1700, -940, -1380, -720),
            State = WindowState.Maximized,
            MaximizedPosition = new Point(-8, -16),
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var virtualDesktopService = new FakeVirtualDesktopService
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
            MoveResult = true
        };
        var placementService = new FakeWindowPlacementService();
        var coordinator = new RuleRestoreCoordinator(
            new FakeWindowContextService(new WindowRuleContext(new IntPtr(52), "notepad.exe", "left-monitor.txt", "Notepad")),
            placementService,
            virtualDesktopService,
            new WindowRuleMatcher());

        var result = coordinator.RestoreWindow(new IntPtr(52), config, null, null);

        Assert.True(result.PlacementRestored);
        Assert.Equal(1, placementService.RestoreCalls);
        Assert.Equal(new Rect(-1700, -940, -1380, -720), Assert.Single(placementService.RestoredWindowSizes).Rect);
        Assert.Equal(new Point(-8, -16), placementService.RestoredWindowSizes[0].MaximizedPosition);
    }

    [Fact]
    public void Config_Defaults_VirtualDesktopRestoreDisabled_AndSnapshotNull()
    {
        var config = Config.NewConfig("default");

        Assert.False(config.EnableVirtualDesktopRestore);
        Assert.Null(config.CurrentLayoutSnapshot);
    }

    [Fact]
    public void LoadOldConfig_MissingVirtualDesktopFields_UsesDefaults()
    {
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "{\"DisableInFullScreen\":true,\"CheckUpdate\":false,\"WindowSizes\":[]}");

            var method = typeof(ConfigFactory).GetMethod("LoadOldConfig", BindingFlags.NonPublic | BindingFlags.Static);
            var migrated = (Config?)method?.Invoke(null, new object[] { tempFile });

            Assert.NotNull(migrated);
            Assert.False(migrated!.EnableVirtualDesktopRestore);
            Assert.Null(migrated.CurrentLayoutSnapshot);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Config_RoundTrips_NewVirtualDesktopFields()
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
                    ExactTitle = "note.txt",
                    WindowClassName = "Notepad",
                    SavedDesktopId = Guid.NewGuid().ToString("D"),
                    Rect = new Rect(1, 2, 3, 4),
                    State = WindowState.Maximized,
                    MaximizedPosition = new Point(4, 5),
                    CaptureOrder = 1
                }
            }
        };
        config.WindowSizes.Add(new WindowSize
        {
            Name = "notepad.exe",
            Title = "note.txt",
            SavedDesktopId = Guid.NewGuid().ToString("D")
        });

        var json = JsonConvert.SerializeObject(config);
        var roundTripped = JsonConvert.DeserializeObject<Config>(json);

        Assert.NotNull(roundTripped);
        Assert.True(roundTripped!.EnableVirtualDesktopRestore);
        Assert.NotNull(roundTripped.CurrentLayoutSnapshot);
        Assert.Single(roundTripped.CurrentLayoutSnapshot!.Entries);
        Assert.NotNull(roundTripped.WindowSizes[0].SavedDesktopId);
    }

    private static RuleSaveCoordinator CreateRuleSaveCoordinator(
        WindowRuleContext context,
        FakeWindowPlacementService placementService,
        FakeVirtualDesktopService virtualDesktopService,
        out FakeConfigurationStore configurationStore)
    {
        configurationStore = new FakeConfigurationStore();
        var matcher = new WindowRuleMatcher();
        return new RuleSaveCoordinator(
            new FakeWindowContextService(context),
            placementService,
            configurationStore,
            virtualDesktopService,
            matcher,
            new WindowRuleUpdater(matcher));
    }

    private static RuleSaveCoordinator CreateRuleSaveCoordinator(
        WindowRuleContext context,
        FakeVirtualDesktopService virtualDesktopService,
        out FakeConfigurationStore configurationStore)
    {
        return CreateRuleSaveCoordinator(
            context,
            new FakeWindowPlacementService(),
            virtualDesktopService,
            out configurationStore);
    }

    private sealed class FakeWindowContextService : IWindowContextService
    {
        private readonly WindowRuleContext _context;

        public FakeWindowContextService(WindowRuleContext context)
        {
            _context = context;
        }

        public IReadOnlyList<IntPtr> GetOpenWindows()
        {
            return new[] { _context.Handle };
        }

        public WindowState GetWindowState(IntPtr handle)
        {
            return WindowState.Normal;
        }

        public WindowVisibilityState GetWindowVisibilityState(IntPtr handle)
        {
            return WindowVisibilityState.Visible;
        }

        public bool TryGetWindowContext(IntPtr handle, Action<Process, Exception>? onFailed, out WindowRuleContext context)
        {
            context = _context;
            return true;
        }
    }

    private sealed class FakeWindowPlacementService : IWindowPlacementService
    {
        public WindowPlacement PlacementToReturn { get; set; } = new()
        {
            Rect = new Rect(10, 20, 200, 220),
            WindowState = WindowState.Normal,
            MaximizedPosition = new Point(0, 0)
        };

        public bool RestoreResult { get; set; } = true;

        public string? RestoreErrorMessage { get; set; }

        public int RestoreCalls { get; private set; }

        public List<WindowSize> RestoredWindowSizes { get; } = new();

        public WindowPlacement GetPlacement(IntPtr handle)
        {
            return PlacementToReturn;
        }

        public bool RestorePlacement(IntPtr handle, WindowSize windowSize, out string? errorMessage)
        {
            RestoreCalls++;
            RestoredWindowSizes.Add(windowSize);
            errorMessage = RestoreResult ? null : (RestoreErrorMessage ?? "Unable to restore placement.");
            return RestoreResult;
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

    private sealed class FakeWindowWaitService : IWindowWaitService
    {
        public void Sleep(int millisecondsDelay)
        {
        }
    }

    private sealed class FakeVirtualDesktopService : IVirtualDesktopService
    {
        public bool CanReadDesktopId { get; set; }

        public bool CanMoveWindow { get; set; }

        public Guid DesktopId { get; set; }

        public bool GetDesktopIdResult { get; set; }

        public bool MoveResult { get; set; }

        public Queue<Guid?> DesktopIdResponses { get; } = new();

        public int MoveCalls { get; private set; }

        public int GetDesktopIdCalls { get; private set; }

        public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
        {
            GetDesktopIdCalls++;
            if (!CanReadDesktopId)
            {
                desktopId = Guid.Empty;
                return false;
            }

            if (DesktopIdResponses.Count > 0)
            {
                var next = DesktopIdResponses.Dequeue();
                if (next.HasValue)
                {
                    desktopId = next.Value;
                    return true;
                }

                desktopId = Guid.Empty;
                return false;
            }

            desktopId = DesktopId;
            return GetDesktopIdResult;
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

            MoveCalls++;
            error = MoveResult ? null : "Move failed";
            return MoveResult;
        }
    }
}
