using System;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class UnavailableExplorerVirtualDesktopBackend : IExplorerVirtualDesktopBackend
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
        error = "Explorer virtual desktop backend is unavailable.";
        return false;
    }
}
