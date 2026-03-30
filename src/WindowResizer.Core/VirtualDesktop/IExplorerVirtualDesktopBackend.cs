using System;

namespace WindowResizer.Core.VirtualDesktop;

public interface IExplorerVirtualDesktopBackend
{
    bool CanReadDesktopId { get; }

    bool CanMoveWindow { get; }

    bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId);

    bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop);

    bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error);
}
