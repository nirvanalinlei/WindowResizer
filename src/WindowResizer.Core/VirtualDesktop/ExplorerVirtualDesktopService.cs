using System;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class ExplorerVirtualDesktopService : IVirtualDesktopService
{
    private readonly IVirtualDesktopService _readFallback;
    private readonly IExplorerVirtualDesktopBackend _backend;

    public ExplorerVirtualDesktopService()
        : this(new NoOpVirtualDesktopService(), new UnavailableExplorerVirtualDesktopBackend())
    {
    }

    public ExplorerVirtualDesktopService(IExplorerVirtualDesktopBackend backend)
        : this(new NoOpVirtualDesktopService(), backend)
    {
    }

    public ExplorerVirtualDesktopService(IVirtualDesktopService readFallback, IExplorerVirtualDesktopBackend backend)
    {
        _readFallback = readFallback ?? throw new ArgumentNullException(nameof(readFallback));
        _backend = backend ?? throw new ArgumentNullException(nameof(backend));
    }

    public bool CanReadDesktopId => _backend.CanReadDesktopId || _readFallback.CanReadDesktopId;

    public bool CanMoveWindow => _backend.CanMoveWindow;

    public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
    {
        if (!_backend.CanReadDesktopId)
        {
            return _readFallback.TryGetWindowDesktopId(hWnd, out desktopId);
        }

        return _backend.TryGetWindowDesktopId(hWnd, out desktopId);
    }

    public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
    {
        if (!_backend.CanReadDesktopId)
        {
            return _readFallback.TryIsWindowOnCurrentDesktop(hWnd, out isOnCurrentDesktop);
        }

        return _backend.TryIsWindowOnCurrentDesktop(hWnd, out isOnCurrentDesktop);
    }

    public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
    {
        return _backend.TryMoveWindowToDesktop(hWnd, desktopId, out error);
    }
}
