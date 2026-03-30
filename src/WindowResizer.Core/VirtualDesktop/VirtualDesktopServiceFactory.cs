using System;

namespace WindowResizer.Core.VirtualDesktop;

public static class VirtualDesktopServiceFactory
{
    private static readonly int[] LegacyExplorerCandidateBuilds =
    {
        19045,
        22631,
        26100,
    };

    private static readonly int[] KnownReadOnlyBuilds =
    {
        19045,
        22631,
        26100,
    };

    public static IVirtualDesktopService Create()
    {
        return SelectRuntime(Environment.OSVersion.Version).Service;
    }

    public static IVirtualDesktopService Create(Version osVersion)
    {
        return SelectRuntime(osVersion).Service;
    }

    public static IVirtualDesktopService Create(
        Version osVersion,
        Func<Version, IExplorerVirtualDesktopBackend> backendFactory)
    {
        return SelectRuntime(osVersion, backendFactory).Service;
    }

    public static VirtualDesktopServiceSelection SelectRuntime(Version osVersion)
    {
        return SelectRuntime(osVersion, CreateRuntimeBackend);
    }

    public static VirtualDesktopServiceSelection SelectRuntime(
        Version osVersion,
        Func<Version, IExplorerVirtualDesktopBackend> backendFactory)
    {
        if (backendFactory is null)
        {
            throw new ArgumentNullException(nameof(backendFactory));
        }

        var baseSelection = Select(osVersion);
        if (baseSelection.BackendKind != VirtualDesktopBackendKind.OfficialReadOnly
            || !IsValidatedRuntimeExplorerBuild(osVersion))
        {
            return baseSelection;
        }

        var validatedReadOnlySelection = new VirtualDesktopServiceSelection(
            baseSelection.Service,
            VirtualDesktopBackendKind.OfficialReadOnly,
            isUnknownBuild: false);

        var backend = backendFactory(osVersion) ?? new UnavailableExplorerVirtualDesktopBackend();
        if (!backend.CanMoveWindow)
        {
            return validatedReadOnlySelection;
        }

        return new VirtualDesktopServiceSelection(
            new ExplorerVirtualDesktopService(baseSelection.Service, backend),
            VirtualDesktopBackendKind.ExplorerCandidate,
            isUnknownBuild: false);
    }

    private static IExplorerVirtualDesktopBackend CreateRuntimeBackend(Version osVersion)
    {
        if (IsValidatedRuntimeExplorerBuild(osVersion))
        {
            return new ExplorerMoveVirtualDesktopBackend(new Windows11_24H2ExplorerMoveApi(osVersion));
        }

        return new UnavailableExplorerVirtualDesktopBackend();
    }

    private static bool IsValidatedRuntimeExplorerBuild(Version osVersion)
    {
        return Windows11_24H2ExplorerMoveBuildPolicy.Supports(osVersion);
    }

    public static VirtualDesktopServiceSelection SelectExplorerCandidate(Version osVersion)
    {
        var baseSelection = Select(osVersion);
        if (baseSelection.BackendKind != VirtualDesktopBackendKind.OfficialReadOnly
            || !IsExplorerCandidatePreviewBuild(osVersion))
        {
            return baseSelection;
        }

        return new VirtualDesktopServiceSelection(
            // ExplorerCandidate marks a routed capability probe, not guaranteed move support.
            new ExplorerVirtualDesktopService(
                new WindowsVirtualDesktopService(),
                new UnavailableExplorerVirtualDesktopBackend()),
            VirtualDesktopBackendKind.ExplorerCandidate,
            isUnknownBuild: false);
    }

    public static VirtualDesktopServiceSelection Select(Version osVersion)
    {
        var firstKnownReadOnlyBuild = KnownReadOnlyBuilds[0];

        if (osVersion.Major < 10)
        {
            return new VirtualDesktopServiceSelection(
                new NoOpVirtualDesktopService(),
                VirtualDesktopBackendKind.NoOp,
                isUnknownBuild: false);
        }

        if (osVersion.Build < firstKnownReadOnlyBuild)
        {
            return new VirtualDesktopServiceSelection(
                new NoOpVirtualDesktopService(),
                VirtualDesktopBackendKind.NoOp,
                isUnknownBuild: osVersion.Build >= 17763);
        }

        if (Array.IndexOf(KnownReadOnlyBuilds, osVersion.Build) >= 0)
        {
            return new VirtualDesktopServiceSelection(
                new WindowsVirtualDesktopService(),
                VirtualDesktopBackendKind.OfficialReadOnly,
                isUnknownBuild: false);
        }

        var lastKnownReadOnlyBuild = KnownReadOnlyBuilds[KnownReadOnlyBuilds.Length - 1];
        if (osVersion.Build > lastKnownReadOnlyBuild && osVersion.Build < 27000)
        {
            return new VirtualDesktopServiceSelection(
                new WindowsVirtualDesktopService(),
                VirtualDesktopBackendKind.OfficialReadOnly,
                isUnknownBuild: true);
        }

        return new VirtualDesktopServiceSelection(
            new NoOpVirtualDesktopService(),
            VirtualDesktopBackendKind.NoOp,
            isUnknownBuild: true);
    }

    private static bool IsExplorerCandidatePreviewBuild(Version osVersion)
    {
        return Array.IndexOf(LegacyExplorerCandidateBuilds, osVersion.Build) >= 0
            || IsValidatedRuntimeExplorerBuild(osVersion);
    }
}
