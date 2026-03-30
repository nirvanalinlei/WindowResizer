using WindowResizer.Common.Windows;

namespace WindowResizer.Configuration;

public class WindowLayoutSnapshotEntry
{
    public string ProcessName { get; set; } = string.Empty;

    public string ExactTitle { get; set; } = string.Empty;

    public string WindowClassName { get; set; } = string.Empty;

    public string? SavedDesktopId { get; set; }

    public Rect Rect { get; set; }

    public WindowState State { get; set; } = WindowState.Normal;

    public Point MaximizedPosition { get; set; } = new(0, 0);

    public int CaptureOrder { get; set; }
}
