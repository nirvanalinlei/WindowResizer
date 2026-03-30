using System;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class ExplorerMoveVirtualDesktopBackend : IExplorerVirtualDesktopBackend
{
    private readonly IExplorerVirtualDesktopMoveApi _moveApi;

    public ExplorerMoveVirtualDesktopBackend(IExplorerVirtualDesktopMoveApi moveApi)
    {
        _moveApi = moveApi ?? throw new ArgumentNullException(nameof(moveApi));
    }

    public bool CanReadDesktopId => false;

    public bool CanMoveWindow => _moveApi.IsAvailable;

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
        if (!_moveApi.IsAvailable)
        {
            error = _moveApi.GetUnavailableError() ?? "Explorer move API unavailable.";
            return false;
        }

        return _moveApi.TryMoveWindowToDesktop(hWnd, desktopId, out error);
    }
}
