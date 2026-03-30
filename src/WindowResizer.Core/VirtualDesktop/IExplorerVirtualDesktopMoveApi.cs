using System;

namespace WindowResizer.Core.VirtualDesktop;

public interface IExplorerVirtualDesktopMoveApi
{
    bool IsAvailable { get; }

    string? GetUnavailableError();

    bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error);
}
