using System;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class ExplorerVirtualDesktopServiceTests
{
    [Fact]
    public void Service_ExposesFallbackReadCapability_WhenBackendCannotRead()
    {
        var readOnlyService = new FakeReadOnlyService
        {
            CanReadDesktopId = true,
        };
        var backend = new FakeExplorerBackend();
        var service = new ExplorerVirtualDesktopService(readOnlyService, backend);

        Assert.True(service.CanReadDesktopId);
        Assert.False(service.CanMoveWindow);
    }

    [Fact]
    public void Service_ExposesBackendCapabilities()
    {
        var backend = new FakeExplorerBackend
        {
            CanReadDesktopId = true,
            CanMoveWindow = true,
        };
        var service = new ExplorerVirtualDesktopService(backend);

        Assert.True(service.CanReadDesktopId);
        Assert.True(service.CanMoveWindow);
    }

    [Fact]
    public void TryGetWindowDesktopId_DelegatesToBackend()
    {
        var expected = Guid.NewGuid();
        var backend = new FakeExplorerBackend
        {
            CanReadDesktopId = true,
            DesktopId = expected,
            GetDesktopIdResult = true,
        };
        var service = new ExplorerVirtualDesktopService(backend);

        var result = service.TryGetWindowDesktopId(new IntPtr(11), out var actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryGetWindowDesktopId_WhenBackendCannotRead_FallsBackToReadOnlyService()
    {
        var expected = Guid.NewGuid();
        var readOnlyService = new FakeReadOnlyService
        {
            CanReadDesktopId = true,
            DesktopId = expected,
            GetDesktopIdResult = true,
        };
        var backend = new FakeExplorerBackend();
        var service = new ExplorerVirtualDesktopService(readOnlyService, backend);

        var result = service.TryGetWindowDesktopId(new IntPtr(12), out var actual);

        Assert.True(result);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryMoveWindowToDesktop_DelegatesToBackend()
    {
        var expected = Guid.NewGuid();
        var backend = new FakeExplorerBackend
        {
            CanMoveWindow = true,
            MoveResult = true,
        };
        var service = new ExplorerVirtualDesktopService(backend);

        var result = service.TryMoveWindowToDesktop(new IntPtr(21), expected, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(expected, backend.LastMovedDesktopId);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenBackendUnavailable_ReturnsBackendError()
    {
        var backend = new FakeExplorerBackend
        {
            CanMoveWindow = false,
            MoveResult = false,
            MoveError = "Explorer backend unavailable."
        };
        var service = new ExplorerVirtualDesktopService(backend);

        var result = service.TryMoveWindowToDesktop(new IntPtr(31), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Explorer backend unavailable.", error);
    }

    [Fact]
    public void TryIsWindowOnCurrentDesktop_DelegatesToBackend()
    {
        var backend = new FakeExplorerBackend
        {
            CanReadDesktopId = true,
            IsOnCurrentDesktop = false,
            IsOnCurrentDesktopResult = true,
        };
        var service = new ExplorerVirtualDesktopService(backend);

        var result = service.TryIsWindowOnCurrentDesktop(new IntPtr(41), out var isOnCurrentDesktop);

        Assert.True(result);
        Assert.False(isOnCurrentDesktop);
    }

    [Fact]
    public void TryIsWindowOnCurrentDesktop_WhenBackendCannotRead_FallsBackToReadOnlyService()
    {
        var readOnlyService = new FakeReadOnlyService
        {
            IsOnCurrentDesktop = false,
            IsOnCurrentDesktopResult = true,
        };
        var backend = new FakeExplorerBackend();
        var service = new ExplorerVirtualDesktopService(readOnlyService, backend);

        var result = service.TryIsWindowOnCurrentDesktop(new IntPtr(42), out var isOnCurrentDesktop);

        Assert.True(result);
        Assert.False(isOnCurrentDesktop);
    }

    private sealed class FakeReadOnlyService : IVirtualDesktopService
    {
        public bool CanReadDesktopId { get; set; }

        public bool CanMoveWindow => false;

        public Guid DesktopId { get; set; }

        public bool GetDesktopIdResult { get; set; }

        public bool IsOnCurrentDesktop { get; set; } = true;

        public bool IsOnCurrentDesktopResult { get; set; }

        public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
        {
            desktopId = DesktopId;
            return GetDesktopIdResult;
        }

        public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
        {
            isOnCurrentDesktop = IsOnCurrentDesktop;
            return IsOnCurrentDesktopResult;
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            error = "Read-only fallback cannot move windows.";
            return false;
        }
    }

    private sealed class FakeExplorerBackend : IExplorerVirtualDesktopBackend
    {
        public bool CanReadDesktopId { get; set; }

        public bool CanMoveWindow { get; set; }

        public Guid DesktopId { get; set; }

        public bool GetDesktopIdResult { get; set; }

        public bool MoveResult { get; set; }

        public string? MoveError { get; set; }

        public bool IsOnCurrentDesktop { get; set; }

        public bool IsOnCurrentDesktopResult { get; set; }

        public Guid LastMovedDesktopId { get; private set; }

        public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
        {
            desktopId = DesktopId;
            return GetDesktopIdResult;
        }

        public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
        {
            isOnCurrentDesktop = IsOnCurrentDesktop;
            return IsOnCurrentDesktopResult;
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            LastMovedDesktopId = desktopId;
            error = MoveError;
            return MoveResult;
        }
    }
}
