using System.Text;
using WindowResizer.Base.Coordinators;

namespace WindowResizer.Base;

public static class LayoutSnapshotOperationFormatter
{
    public static string FormatSaveSummary(LayoutSnapshotSaveResult result)
    {
        if (result.SavedWindowCount == 0)
        {
            return "No eligible windows captured.";
        }

        var message = $"Saved {result.SavedWindowCount} windows across {System.Math.Max(result.DesktopCount, 1)} desktops.";
        if (result.CloakedWindowCount > 0)
        {
            message += $" Cloaked {result.CloakedWindowCount}.";
        }

        return message;
    }

    public static string FormatRestoreSummary(LayoutSnapshotRestoreResult result)
    {
        return $"Restored {result.RestoredCount}. Move fallback {result.MoveFallbackCount}. Unmatched {result.UnmatchedCount}. Failed {result.FailedCount}.";
    }

    public static string FormatSaveLog(LayoutSnapshotSaveResult result)
    {
        return $"SaveAll snapshot saved: windows={result.SavedWindowCount}, desktops={result.DesktopCount}, cloaked={result.CloakedWindowCount}.";
    }

    public static string FormatRestoreLog(LayoutSnapshotRestoreResult result)
    {
        var message = new StringBuilder(
            $"RestoreAll completed: restored={result.RestoredCount}, moveFallback={result.MoveFallbackCount}, unmatched={result.UnmatchedCount}, failed={result.FailedCount}.");
        AppendEntries(message, "Move fallback", result.MoveFallbackEntries);
        AppendEntries(message, "Unmatched", result.UnmatchedEntries);
        AppendEntries(message, "Placement failed", result.FailedEntries);
        return message.ToString();
    }

    private static void AppendEntries(StringBuilder message, string title, System.Collections.Generic.IReadOnlyList<string> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        message.Append(' ');
        message.Append(title);
        message.Append(": ");
        message.Append(string.Join(" | ", entries));
        message.Append('.');
    }

}
