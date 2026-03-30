namespace WindowResizer.Core.VirtualDesktop;

public sealed class VirtualDesktopServiceSelection
{
    public VirtualDesktopServiceSelection(
        IVirtualDesktopService service,
        VirtualDesktopBackendKind backendKind,
        bool isUnknownBuild)
    {
        Service = service;
        BackendKind = backendKind;
        IsUnknownBuild = isUnknownBuild;
    }

    public IVirtualDesktopService Service { get; }

    public VirtualDesktopBackendKind BackendKind { get; }

    public bool IsUnknownBuild { get; }
}
