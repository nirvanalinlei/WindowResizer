using System;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class NoOpVirtualDesktopService : IVirtualDesktopService
{
    public bool CanReadDesktopId => false;

    public bool CanMoveWindow => false;

    public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
    {
        desktopId = Guid.Empty;
        return false;
    }

    public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
    {
        isOnCurrentDesktop = true;
        return false;
    }

    public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
    {
        error = "Virtual desktop support is unavailable.";
        return false;
    }
}
