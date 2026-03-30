using WindowResizer.Configuration;

namespace WindowResizer.Base;

public static class LayoutSnapshotStatusFormatter
{
    public static string Format(WindowLayoutSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return "Last layout snapshot: not saved yet.";
        }

        var timestamp = snapshot.CapturedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
        var count = snapshot.Entries.Count;
        var unit = count == 1 ? "window" : "windows";
        return $"Last layout snapshot: {timestamp} ({count} {unit}).";
    }
}
