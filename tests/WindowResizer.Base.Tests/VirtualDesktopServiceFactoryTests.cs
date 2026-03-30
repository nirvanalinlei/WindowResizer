using System;
using WindowResizer.Core.VirtualDesktop;
using Xunit;

namespace WindowResizer.Base.Tests;

public class VirtualDesktopServiceFactoryTests
{
    [Theory]
    [InlineData(10, 0, 19045)]
    [InlineData(10, 0, 22631)]
    [InlineData(10, 0, 26100)]
    [InlineData(10, 0, 26199)]
    [InlineData(10, 0, 26900)]
    [InlineData(10, 0, 26999)]
    public void Create_WhenRuntimeMoveIsNotEnabled_ReturnsWindowsVirtualDesktopService(int major, int minor, int build)
    {
        var service = VirtualDesktopServiceFactory.Create(
            new Version(major, minor, build),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.IsType<WindowsVirtualDesktopService>(service);
        Assert.True(service.CanReadDesktopId);
        Assert.False(service.CanMoveWindow);
    }

    [Theory]
    [InlineData(10, 0, 26200)]
    [InlineData(10, 0, 26201)]
    [InlineData(10, 0, 26250)]
    public void Create_ValidatedRuntimeMoveBuild_WithAvailableBackend_ReturnsExplorerVirtualDesktopService(int major, int minor, int build)
    {
        var service = VirtualDesktopServiceFactory.Create(
            new Version(major, minor, build),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.IsType<ExplorerVirtualDesktopService>(service);
        Assert.True(service.CanReadDesktopId);
        Assert.True(service.CanMoveWindow);
    }

    [Theory]
    [InlineData(10, 0, 26200)]
    [InlineData(10, 0, 26201)]
    [InlineData(10, 0, 26250)]
    public void Create_ValidatedRuntimeMoveBuild_WithUnavailableBackend_FallsBackToWindowsVirtualDesktopService(int major, int minor, int build)
    {
        var service = VirtualDesktopServiceFactory.Create(
            new Version(major, minor, build),
            new FakeExplorerMoveApiFactory(isAvailable: false).CreateBackend);

        Assert.IsType<WindowsVirtualDesktopService>(service);
        Assert.True(service.CanReadDesktopId);
        Assert.False(service.CanMoveWindow);
    }

    [Theory]
    [InlineData(10, 0, 19045)]
    [InlineData(10, 0, 22631)]
    [InlineData(10, 0, 26100)]
    public void Select_KnownSupportedWindowsBuild_ReturnsOfficialReadOnlyBackend(int major, int minor, int build)
    {
        var selection = VirtualDesktopServiceFactory.Select(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanReadDesktopId);
        Assert.False(selection.Service.CanMoveWindow);
    }

    [Theory]
    [InlineData(10, 0, 26200)]
    [InlineData(10, 0, 26999)]
    public void Select_ReadOnlyFamilyBuild_ReturnsOfficialReadOnlyBackend_AsUnknownBuild(int major, int minor, int build)
    {
        var selection = VirtualDesktopServiceFactory.Select(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanReadDesktopId);
        Assert.False(selection.Service.CanMoveWindow);
    }

    [Theory]
    [InlineData(10, 0, 19045)]
    [InlineData(10, 0, 22631)]
    [InlineData(10, 0, 26100)]
    [InlineData(10, 0, 26200)]
    public void SelectExplorerCandidate_KnownSupportedWindowsBuild_ReturnsExplorerCandidateBackend(int major, int minor, int build)
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectExplorerCandidate_Validated24H2MidServicingBuild_ReturnsExplorerCandidateBackend()
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(10, 0, 26250));

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectExplorerCandidate_BelowValidated24H2BoundaryBuild_RemainsOfficialReadOnly()
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(10, 0, 26199));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Theory]
    [InlineData(10, 0, 26999)]
    public void SelectExplorerCandidate_ReadOnlyFamilyBuild_RemainsOfficialReadOnlyUntilExplicitlyValidated(int major, int minor, int build)
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectExplorerCandidate_Unsupported24H2FamilyBoundaryBuild_RemainsOfficialReadOnly()
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(10, 0, 26900));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Theory]
    [InlineData(10, 0, 19045)]
    [InlineData(10, 0, 22631)]
    [InlineData(10, 0, 26100)]
    [InlineData(10, 0, 26200)]
    public void SelectExplorerCandidate_KnownSupportedWindowsBuild_RemainsConservativeUntilBackendIsWired(int major, int minor, int build)
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(major, minor, build));

        Assert.True(selection.Service.CanReadDesktopId);
        Assert.False(selection.Service.CanMoveWindow);
    }

    [Fact]
    public void SelectRuntime_ValidatedExplorerBuild_WithHealthyBackend_ReturnsExplorerCandidate()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26200),
            _ => new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi { IsAvailable = true, MoveResult = true }));

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanReadDesktopId);
        Assert.True(selection.Service.CanMoveWindow);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_ValidatedExplorerBuild_UsesExplorerCandidateWhenMoveApiIsAvailable()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26200),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanMoveWindow);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_Validated24H2ServicingBuild_UsesExplorerCandidateWhenMoveApiIsAvailable()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26201),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanMoveWindow);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_Validated24H2MidServicingBuild_UsesExplorerCandidateWhenMoveApiIsAvailable()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26250),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.ExplorerCandidate, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<ExplorerVirtualDesktopService>(selection.Service);
        Assert.True(selection.Service.CanMoveWindow);
    }

    [Fact]
    public void SelectRuntime_ValidatedExplorerBuild_WithUnavailableBackend_FallsBackToOfficialReadOnly()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26200),
            _ => new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi { IsAvailable = false }));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_ValidatedExplorerBuild_FallsBackToOfficialReadOnlyWhenMoveApiUnavailable()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26200),
            new FakeExplorerMoveApiFactory(isAvailable: false).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_Validated24H2ServicingBuild_FallsBackToOfficialReadOnlyWhenMoveApiUnavailable()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26201),
            new FakeExplorerMoveApiFactory(isAvailable: false).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.False(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_BelowValidated24H2BoundaryBuild_IgnoresHealthyBackend()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26199),
            _ => new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi { IsAvailable = true, MoveResult = true }));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_UnvalidatedReadOnlyFamilyBuild_IgnoresHealthyBackend()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26999),
            _ => new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi { IsAvailable = true, MoveResult = true }));

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_UnvalidatedReadOnlyFamilyBuild_IgnoresAvailableMoveApi()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26999),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Fact]
    public void SelectRuntime_DefaultPath_Unsupported24H2FamilyBoundaryBuild_DoesNotUseExplorerCandidate()
    {
        var selection = VirtualDesktopServiceFactory.SelectRuntime(
            new Version(10, 0, 26900),
            new FakeExplorerMoveApiFactory(isAvailable: true).CreateBackend);

        Assert.Equal(VirtualDesktopBackendKind.OfficialReadOnly, selection.BackendKind);
        Assert.True(selection.IsUnknownBuild);
        Assert.IsType<WindowsVirtualDesktopService>(selection.Service);
    }

    [Theory]
    [InlineData(10, 0, 10240)]
    [InlineData(10, 0, 17763)]
    [InlineData(10, 0, 22000)]
    [InlineData(10, 0, 30000)]
    [InlineData(6, 1, 7601)]
    [InlineData(6, 3, 9600)]
    public void Create_UnknownOrUnsupportedWindowsBuild_ReturnsNoOpVirtualDesktopService(int major, int minor, int build)
    {
        var service = VirtualDesktopServiceFactory.Create(new Version(major, minor, build));

        Assert.IsType<NoOpVirtualDesktopService>(service);
    }

    [Theory]
    [InlineData(10, 0, 10240, false)]
    [InlineData(10, 0, 17763, true)]
    [InlineData(10, 0, 22000, true)]
    [InlineData(10, 0, 30000, true)]
    [InlineData(6, 1, 7601, false)]
    [InlineData(6, 3, 9600, false)]
    public void Select_UnknownOrUnsupportedWindowsBuild_ReturnsNoOpBackend(int major, int minor, int build, bool isUnknownBuild)
    {
        var selection = VirtualDesktopServiceFactory.Select(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.NoOp, selection.BackendKind);
        Assert.Equal(isUnknownBuild, selection.IsUnknownBuild);
    }

    [Theory]
    [InlineData(10, 0, 17763, true)]
    [InlineData(10, 0, 30000, true)]
    [InlineData(6, 1, 7601, false)]
    public void SelectExplorerCandidate_UnknownOrUnsupportedWindowsBuild_ReturnsNoOpBackend(int major, int minor, int build, bool isUnknownBuild)
    {
        var selection = VirtualDesktopServiceFactory.SelectExplorerCandidate(new Version(major, minor, build));

        Assert.Equal(VirtualDesktopBackendKind.NoOp, selection.BackendKind);
        Assert.Equal(isUnknownBuild, selection.IsUnknownBuild);
    }

    [Fact]
    public void Create_CurrentProcessVersion_DoesNotThrow()
    {
        var service = VirtualDesktopServiceFactory.Create(Environment.OSVersion.Version);

        Assert.NotNull(service);
    }

    private sealed class FakeMoveApi : IExplorerVirtualDesktopMoveApi
    {
        public bool IsAvailable { get; set; }

        public bool MoveResult { get; set; }

        public string? GetUnavailableError()
        {
            return "Explorer move API unavailable.";
        }

        public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
        {
            error = null;
            return MoveResult;
        }
    }

    private sealed class FakeExplorerMoveApiFactory
    {
        private readonly bool _isAvailable;

        public FakeExplorerMoveApiFactory(bool isAvailable)
        {
            _isAvailable = isAvailable;
        }

        public IExplorerVirtualDesktopBackend CreateBackend(Version osVersion)
        {
            return new ExplorerMoveVirtualDesktopBackend(new FakeMoveApi
            {
                IsAvailable = _isAvailable,
                MoveResult = _isAvailable,
            });
        }
    }
}
