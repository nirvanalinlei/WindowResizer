using System;
using System.Runtime.InteropServices;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class Windows11_24H2ExplorerMoveApiTests
{
    [Theory]
    [InlineData(26200)]
    [InlineData(26201)]
    [InlineData(26250)]
    public void Api_DefaultBuildGating_TreatsValidatedBuildAsSupported(int build)
    {
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, build), new FakeComContext
        {
            IsAvailable = true,
        });

        Assert.True(api.IsAvailable);
        Assert.Null(api.GetUnavailableError());
    }

    [Theory]
    [InlineData(26199)]
    [InlineData(26900)]
    [InlineData(26999)]
    public void Api_DefaultBuildGating_TreatsUnsupportedBuildAsUnsupported(int build)
    {
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, build), new FakeComContext
        {
            IsAvailable = true,
        });

        Assert.False(api.IsAvailable);
        Assert.Equal($"Windows 11 24H2 Explorer move API is unsupported on build {build}.", api.GetUnavailableError());
    }

    [Theory]
    [InlineData(26199)]
    [InlineData(26900)]
    public void TryMoveWindowToDesktop_WhenBuildIsUnsupported_DoesNotCallContext(int build)
    {
        var context = new FakeComContext
        {
            IsAvailable = true,
            MoveResult = true,
        };
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, build), context);

        var result = api.TryMoveWindowToDesktop(new IntPtr(17), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal($"Windows 11 24H2 Explorer move API is unsupported on build {build}.", error);
        Assert.Equal(IntPtr.Zero, context.LastHandle);
    }

    [Fact]
    public void Api_ReportsAvailable_WhenDependenciesInitialize()
    {
        var api = new Windows11_24H2ExplorerMoveApi(new FakeComContext
        {
            IsAvailable = true,
        });

        Assert.True(api.IsAvailable);
        Assert.Null(api.GetUnavailableError());
    }

    [Fact]
    public void Api_ReportsUnavailableReason_WhenDependenciesFailToInitialize()
    {
        var api = new Windows11_24H2ExplorerMoveApi(new FakeComContext
        {
            IsAvailable = false,
            UnavailableError = "Virtual desktop COM services unavailable."
        });

        Assert.False(api.IsAvailable);
        Assert.Equal("Virtual desktop COM services unavailable.", api.GetUnavailableError());
    }

    [Fact]
    public void TryMoveWindowToDesktop_UsesComMovePath()
    {
        var expectedDesktopId = Guid.NewGuid();
        var context = new FakeComContext
        {
            IsAvailable = true,
            MoveResult = true,
        };
        var api = new Windows11_24H2ExplorerMoveApi(context);

        var result = api.TryMoveWindowToDesktop(new IntPtr(51), expectedDesktopId, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(new IntPtr(51), context.LastHandle);
        Assert.Equal(expectedDesktopId, context.LastDesktopId);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenDesktopLookupFails_ReturnsError()
    {
        var api = new Windows11_24H2ExplorerMoveApi(new FakeComContext
        {
            IsAvailable = true,
            MoveResult = false,
            MoveError = "Target virtual desktop was not found."
        });

        var result = api.TryMoveWindowToDesktop(new IntPtr(61), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Target virtual desktop was not found.", error);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenMoveFailsWithoutContextInvalidation_DoesNotRefreshContext()
    {
        var failedContext = new FakeComContext
        {
            IsAvailable = true,
            MoveResult = false,
            MoveError = "Target virtual desktop was not found."
        };
        var contexts = new SequenceComContextFactory(
            failedContext,
            new FakeComContext
            {
                IsAvailable = true,
                MoveResult = true,
            });
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, 26200), contexts.Create);

        var result = api.TryMoveWindowToDesktop(new IntPtr(63), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Target virtual desktop was not found.", error);
        Assert.Equal(1, contexts.CreateCallCount);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenComThrows_ReturnsExceptionMessage()
    {
        var api = new Windows11_24H2ExplorerMoveApi(new FakeComContext
        {
            IsAvailable = true,
            MoveException = new COMException("Explorer move failed.")
        });

        var result = api.TryMoveWindowToDesktop(new IntPtr(71), Guid.NewGuid(), out var error);

        Assert.False(result);
        Assert.Equal("Explorer move failed.", error);
    }

    [Fact]
    public void Api_WhenInitialContextIsUnavailable_RefreshesContextFactory()
    {
        var contexts = new SequenceComContextFactory(
            new FakeComContext
            {
                IsAvailable = false,
                UnavailableError = "Virtual desktop COM services unavailable."
            },
            new FakeComContext
            {
                IsAvailable = true,
            });
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, 26200), contexts.Create);

        Assert.True(api.IsAvailable);
        Assert.Null(api.GetUnavailableError());
        Assert.Equal(2, contexts.CreateCallCount);
    }

    [Fact]
    public void TryMoveWindowToDesktop_WhenExplorerContextNeedsRefresh_RetriesWithFreshContext()
    {
        var expectedDesktopId = Guid.NewGuid();
        var staleContext = new FakeComContext
        {
            IsAvailable = true,
            MoveResult = false,
            MoveError = "The object invoked has disconnected from its clients.",
            InvalidateOnMoveFailure = true,
        };
        var reboundContext = new FakeComContext
        {
            IsAvailable = true,
            MoveResult = true,
        };
        var contexts = new SequenceComContextFactory(staleContext, reboundContext);
        var api = new Windows11_24H2ExplorerMoveApi(new Version(10, 0, 26200), contexts.Create);

        var result = api.TryMoveWindowToDesktop(new IntPtr(81), expectedDesktopId, out var error);

        Assert.True(result);
        Assert.Null(error);
        Assert.Equal(2, contexts.CreateCallCount);
        Assert.Equal(new IntPtr(81), staleContext.LastHandle);
        Assert.Equal(new IntPtr(81), reboundContext.LastHandle);
        Assert.Equal(expectedDesktopId, reboundContext.LastDesktopId);
    }

    private sealed class FakeComContext : IWindows11_24H2VirtualDesktopComContext
    {
        public bool IsAvailable { get; set; }

        public string? UnavailableError { get; set; }

        public bool MoveResult { get; set; }

        public string? MoveError { get; set; }

        public Exception? MoveException { get; set; }

        public bool InvalidateOnMoveFailure { get; set; }

        public IntPtr LastHandle { get; private set; }

        public Guid LastDesktopId { get; private set; }

        public string? GetUnavailableError()
        {
            return UnavailableError;
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            if (MoveException is not null)
            {
                throw MoveException;
            }

            LastHandle = hWnd;
            LastDesktopId = desktopId;
            if (!MoveResult && InvalidateOnMoveFailure)
            {
                IsAvailable = false;
            }

            error = MoveError;
            return MoveResult;
        }
    }

    private sealed class SequenceComContextFactory
    {
        private readonly FakeComContext[] _contexts;
        private int _index;

        public SequenceComContextFactory(params FakeComContext[] contexts)
        {
            _contexts = contexts;
        }

        public int CreateCallCount { get; private set; }

        public IWindows11_24H2VirtualDesktopComContext Create()
        {
            CreateCallCount++;
            if (_contexts.Length == 0)
            {
                throw new InvalidOperationException("No test contexts configured.");
            }

            if (_index < _contexts.Length - 1)
            {
                return _contexts[_index++];
            }

            return _contexts[_contexts.Length - 1];
        }
    }
}
