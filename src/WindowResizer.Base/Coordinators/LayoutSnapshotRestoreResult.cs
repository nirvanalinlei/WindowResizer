using System.Collections.Generic;
using System.Linq;

namespace WindowResizer.Base.Coordinators;

public sealed class LayoutSnapshotRestoreResult
{
    public LayoutSnapshotRestoreResult(
        bool noSnapshot,
        int restoredCount,
        int moveFallbackCount,
        int unmatchedCount,
        int failedCount,
        IEnumerable<string>? moveFallbackEntries = null,
        IEnumerable<string>? unmatchedEntries = null,
        IEnumerable<string>? failedEntries = null)
    {
        NoSnapshot = noSnapshot;
        RestoredCount = restoredCount;
        MoveFallbackCount = moveFallbackCount;
        UnmatchedCount = unmatchedCount;
        FailedCount = failedCount;
        MoveFallbackEntries = (moveFallbackEntries ?? Enumerable.Empty<string>()).ToList();
        UnmatchedEntries = (unmatchedEntries ?? Enumerable.Empty<string>()).ToList();
        FailedEntries = (failedEntries ?? Enumerable.Empty<string>()).ToList();
    }

    public bool NoSnapshot { get; }

    public int RestoredCount { get; }

    public int MoveFallbackCount { get; }

    public int UnmatchedCount { get; }

    public int FailedCount { get; }

    public IReadOnlyList<string> MoveFallbackEntries { get; }

    public IReadOnlyList<string> UnmatchedEntries { get; }

    public IReadOnlyList<string> FailedEntries { get; }
}
