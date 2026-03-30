using System;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class ExplorerMoveVirtualDesktopBackendTests
{
    [Fact]
    public void Backend_ExposesMoveCapability_WhenMoveApiIsAvailable()
    {
        var backend = new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi
        {
            IsAvailable = true,
        });

        Assert.False(backend.CanReadDesktopId);
        Assert.True(backend.CanMoveWindow);
    }

    [Fact]
    public void ReadOperations_AreUnsupported()
    {
        var backend = new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi
        {
            IsAvailable = true,
        });

        var desktopIdResult = backend.TryGetWindowDesktopId(new IntPtr(11), out var desktopId);
        var isOnCurrentResult = backend.TryIsWindowOnCurrentDesktop(new IntPtr(11), out var isOnCurrentDesktop);

        Assert.False(desktopIdResult);
        Assert.Equal(Guid.Empty, desktopId);
        Assert.False(isOnCurrentResult);
        Assert.True(isOnCurrentDesktop);
    }

    [Fact]
    public void TryMoveWindowToDesktop_DelegatesToMoveApi()
    {
        var expectedDesktopId = Guid.NewGuid();
        var api = new FakeMoveApi
        {
            IsAvailable = true,
            MoveResult = true,
        };
        var backend = new ExplorerMoveVirtualDesktopBackend(api);

        var result = backend.TryMoveWindowToDesktop(new IntPtr(21), expectedDesktopId, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(new IntPtr(21), api.LastHandle);
        Assert.Equal(expectedDesktopId, api.LastDesktopId);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenMoveApiUnavailable_ReturnsUnavailableError()
    {
        var backend = new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi
        {
            IsAvailable = false,
            UnavailableError = "Explorer move API unavailable."
        });

        var result = backend.TryMoveWindowToDesktop(new IntPtr(31), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Explorer move API unavailable.", error);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenMoveApiFails_ReturnsMoveError()
    {
        var backend = new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi
        {
            IsAvailable = true,
            MoveResult = false,
            MoveError = "Target virtual desktop was not found."
        });

        var result = backend.TryMoveWindowToDesktop(new IntPtr(41), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Target virtual desktop was not found.", error);
    }

    private sealed class FakeMoveApi : IExplorerVirtualDesktopMoveApi
    {
        public bool IsAvailable { get; set; }

        public bool MoveResult { get; set; }

        public string? MoveError { get; set; }

        public string? UnavailableError { get; set; }

        public IntPtr LastHandle { get; private set; }

        public Guid LastDesktopId { get; private set; }

        public string? GetUnavailableError()
        {
            return UnavailableError;
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            LastHandle = hWnd;
            LastDesktopId = desktopId;
            error = MoveError;
            return MoveResult;
        }
    }
}
