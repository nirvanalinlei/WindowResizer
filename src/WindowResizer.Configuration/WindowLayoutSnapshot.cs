using System;
using System.Collections.Generic;

namespace WindowResizer.Configuration;

public class WindowLayoutSnapshot
{
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<WindowLayoutSnapshotEntry> Entries { get; set; } = new();
}
