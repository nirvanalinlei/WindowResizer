using System;

namespace WindowResizer.Core.VirtualDesktop;

public interface IWindows11_24H2VirtualDesktopComContext
{
    bool IsAvailable { get; }

    string? GetUnavailableError();

    bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error);
}
