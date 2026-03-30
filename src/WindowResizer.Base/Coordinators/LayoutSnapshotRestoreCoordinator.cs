using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using WindowResizer.Base.Abstractions;
using WindowResizer.Configuration;
using WindowResizer.Core.VirtualDesktop;

namespace WindowResizer.Base.Coordinators;

public sealed class LayoutSnapshotRestoreCoordinator
{
    private readonly IWindowContextService _windowContextService;
    private readonly IWindowPlacementService _windowPlacementService;
    private readonly IVirtualDesktopService _virtualDesktopService;

    public LayoutSnapshotRestoreCoordinator(
        IWindowContextService windowContextService,
        IWindowPlacementService windowPlacementService,
        IVirtualDesktopService virtualDesktopService)
    {
        _windowContextService = windowContextService;
        _windowPlacementService = windowPlacementService;
        _virtualDesktopService = virtualDesktopService;
    }

    public LayoutSnapshotRestoreResult RestoreAll(Config config, Action<Process, Exception>? onFailed)
    {
        var snapshotEntries = config.CurrentLayoutSnapshot?.Entries;
        if (snapshotEntries is null || snapshotEntries.Count == 0)
        {
            return new LayoutSnapshotRestoreResult(
                noSnapshot: true,
                restoredCount: 0,
                moveFallbackCount: 0,
                unmatchedCount: 0,
                failedCount: 0);
        }

        var candidateGroups = BuildCandidateGroupMap(BuildCandidates(config, onFailed));
        var restoredCount = 0;
        var moveFallbackCount = 0;
        var unmatchedCount = 0;
        var failedCount = 0;
        var moveFallbackEntries = new List<string>();
        var unmatchedEntries = new List<string>();
        var failedEntries = new List<string>();

        foreach (var snapshotGroup in snapshotEntries
                     .Where(entry => !ShouldSkipSnapshotEntry(entry))
                     .GroupBy(entry => CreateFallbackKey(entry.ProcessName, entry.WindowClassName), StringComparer.Ordinal)
                     .Select(group => group.OrderBy(entry => entry.CaptureOrder).ToList()))
        {
            var fallbackKey = CreateFallbackKey(snapshotGroup[0].ProcessName, snapshotGroup[0].WindowClassName);
            if (!candidateGroups.TryGetValue(fallbackKey, out var candidates))
            {
                unmatchedCount += snapshotGroup.Count;
                unmatchedEntries.AddRange(snapshotGroup.Select(entry => DescribeEntry(entry)));
                continue;
            }

            var assignments = CreateAssignments(snapshotGroup, candidates);
            if (assignments.Count == 0)
            {
                unmatchedCount += snapshotGroup.Count;
                unmatchedEntries.AddRange(snapshotGroup.Select(entry => DescribeEntry(entry)));
                continue;
            }

            if (assignments.Count < snapshotGroup.Count)
            {
                unmatchedCount += snapshotGroup.Count - assignments.Count;
                var matchedEntries = new HashSet<WindowLayoutSnapshotEntry>(assignments.Select(assignment => assignment.SnapshotEntry));
                unmatchedEntries.AddRange(snapshotGroup
                    .Where(entry => !matchedEntries.Contains(entry))
                    .Select(entry => DescribeEntry(entry)));
            }

            foreach (var assignment in assignments.OrderBy(assignment => assignment.SnapshotEntry.CaptureOrder))
            {
                var snapshotEntry = assignment.SnapshotEntry;
                var candidate = assignment.Candidate;
                var moveResult = TryMoveToSavedDesktop(config, candidate.Handle, snapshotEntry);
                if (moveResult.IsFallback)
                {
                    moveFallbackCount++;
                    moveFallbackEntries.Add(DescribeEntry(snapshotEntry, moveResult.ErrorMessage));
                }

                if (_windowPlacementService.RestorePlacement(candidate.Handle, ToWindowSize(snapshotEntry), out var placementErrorMessage))
                {
                    restoredCount++;
                }
                else
                {
                    failedCount++;
                    failedEntries.Add(DescribeEntry(snapshotEntry, placementErrorMessage));
                }
            }
        }

        return new LayoutSnapshotRestoreResult(
            noSnapshot: false,
            restoredCount,
            moveFallbackCount,
            unmatchedCount,
            failedCount,
            moveFallbackEntries,
            unmatchedEntries,
            failedEntries);
    }

    private List<WindowRuleContext> BuildCandidates(
        Config config,
        Action<Process, Exception>? onFailed)
    {
        var candidates = new List<WindowRuleContext>();
        foreach (var handle in _windowContextService.GetOpenWindows())
        {
            if (!config.RestoreAllIncludeMinimized
                && _windowContextService.GetWindowState(handle) == Common.Windows.WindowState.Minimized)
            {
                continue;
            }

            if (!_windowContextService.TryGetWindowContext(handle, onFailed, out var context))
            {
                continue;
            }

            if (ShouldSkipContext(context))
            {
                continue;
            }

            candidates.Add(context);
        }

        return candidates;
    }

    private static Dictionary<string, List<WindowRuleContext>> BuildCandidateGroupMap(IEnumerable<WindowRuleContext> candidates)
    {
        return candidates
            .GroupBy(candidate => CreateFallbackKey(candidate.ProcessName, candidate.WindowClassName), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    private static List<RestoreAssignment> CreateAssignments(
        IReadOnlyList<WindowLayoutSnapshotEntry> snapshotGroup,
        IReadOnlyList<WindowRuleContext> candidates)
    {
        if (snapshotGroup.Count == 0 || candidates.Count == 0)
        {
            return new List<RestoreAssignment>();
        }

        var assignments = new List<RestoreAssignment>();
        var remainingSnapshots = new List<WindowLayoutSnapshotEntry>();
        var remainingCandidates = new List<WindowRuleContext>(candidates);
        var exactBuckets = candidates
            .GroupBy(candidate => candidate.Title, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new Queue<WindowRuleContext>(group),
                StringComparer.Ordinal);

        foreach (var snapshotEntry in snapshotGroup)
        {
            if (exactBuckets.TryGetValue(snapshotEntry.ExactTitle, out var exactCandidates)
                && exactCandidates.Count > 0)
            {
                var candidate = exactCandidates.Dequeue();
                assignments.Add(new RestoreAssignment(snapshotEntry, candidate));
                remainingCandidates.Remove(candidate);
            }
            else
            {
                remainingSnapshots.Add(snapshotEntry);
            }
        }

        if (remainingSnapshots.Count == 0)
        {
            return assignments;
        }

        // After exact matches are consumed, only pair the leftovers when title similarity is uniquely informative.
        assignments.AddRange(MatchByUniqueTitleSimilarity(remainingSnapshots, remainingCandidates));
        return assignments;
    }

    private static IReadOnlyList<RestoreAssignment> MatchByUniqueTitleSimilarity(
        IReadOnlyList<WindowLayoutSnapshotEntry> snapshots,
        IReadOnlyList<WindowRuleContext> candidates)
    {
        if (snapshots.Count == 0 || candidates.Count == 0)
        {
            return Array.Empty<RestoreAssignment>();
        }

        var remainingSnapshots = snapshots.ToList();
        var remainingCandidates = candidates.ToList();
        var assignments = new List<RestoreAssignment>();

        while (remainingSnapshots.Count > 0 && remainingCandidates.Count > 0)
        {
            var pairs = FindMutualUniqueBestPairs(remainingSnapshots, remainingCandidates);
            if (pairs.Count == 0)
            {
                break;
            }

            assignments.AddRange(pairs);

            var matchedSnapshots = new HashSet<WindowLayoutSnapshotEntry>(pairs.Select(pair => pair.SnapshotEntry));
            var matchedCandidates = new HashSet<WindowRuleContext>(pairs.Select(pair => pair.Candidate));
            remainingSnapshots = remainingSnapshots.Where(snapshot => !matchedSnapshots.Contains(snapshot)).ToList();
            remainingCandidates = remainingCandidates.Where(candidate => !matchedCandidates.Contains(candidate)).ToList();
        }

        return assignments;
    }

    private static IReadOnlyList<RestoreAssignment> FindMutualUniqueBestPairs(
        IReadOnlyList<WindowLayoutSnapshotEntry> snapshots,
        IReadOnlyList<WindowRuleContext> candidates)
    {
        var scores = new Dictionary<(WindowLayoutSnapshotEntry Snapshot, WindowRuleContext Candidate), int>();
        foreach (var snapshot in snapshots)
        {
            foreach (var candidate in candidates)
            {
                var score = CalculateTitleSimilarity(snapshot.ExactTitle, candidate.Title);
                if (score > 0)
                {
                    scores[(snapshot, candidate)] = score;
                }
            }
        }

        if (scores.Count == 0)
        {
            return Array.Empty<RestoreAssignment>();
        }

        var bestCandidateBySnapshot = new Dictionary<WindowLayoutSnapshotEntry, WindowRuleContext>();
        foreach (var snapshot in snapshots)
        {
            var ranked = candidates
                .Select(candidate => new
                {
                    Candidate = candidate,
                    Score = scores.TryGetValue((snapshot, candidate), out var score) ? score : 0
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ToList();

            if (ranked.Count == 0)
            {
                continue;
            }

            if (ranked.Count == 1 || ranked[0].Score > ranked[1].Score)
            {
                bestCandidateBySnapshot[snapshot] = ranked[0].Candidate;
            }
        }

        var bestSnapshotByCandidate = new Dictionary<WindowRuleContext, WindowLayoutSnapshotEntry>();
        foreach (var candidate in candidates)
        {
            var ranked = snapshots
                .Select(snapshot => new
                {
                    Snapshot = snapshot,
                    Score = scores.TryGetValue((snapshot, candidate), out var score) ? score : 0
                })
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ToList();

            if (ranked.Count == 0)
            {
                continue;
            }

            if (ranked.Count == 1 || ranked[0].Score > ranked[1].Score)
            {
                bestSnapshotByCandidate[candidate] = ranked[0].Snapshot;
            }
        }

        return bestCandidateBySnapshot
            .Where(pair => bestSnapshotByCandidate.TryGetValue(pair.Value, out var snapshot)
                           && ReferenceEquals(snapshot, pair.Key))
            .Select(pair => new RestoreAssignment(pair.Key, pair.Value))
            .ToList();
    }

    private static int CalculateTitleSimilarity(string snapshotTitle, string candidateTitle)
    {
        var snapshotTokens = TokenizeTitle(snapshotTitle);
        var candidateTokens = TokenizeTitle(candidateTitle);
        if (snapshotTokens.Count == 0 || candidateTokens.Count == 0)
        {
            return 0;
        }

        var snapshotHashTokens = snapshotTokens.Where(IsHashNumberToken).ToList();
        var candidateHashTokens = candidateTokens.Where(IsHashNumberToken).ToList();
        if (snapshotHashTokens.Count > 0
            && candidateHashTokens.Count > 0
            && !snapshotHashTokens.Intersect(candidateHashTokens, StringComparer.Ordinal).Any())
        {
            return 0;
        }

        var overlap = snapshotTokens.Intersect(candidateTokens, StringComparer.Ordinal).Count();
        if (overlap == 0)
        {
            return 0;
        }

        return overlap * 100 - Math.Abs(snapshotTokens.Count - candidateTokens.Count);
    }

    private static HashSet<string> TokenizeTitle(string title)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(title))
        {
            return tokens;
        }

        var builder = new StringBuilder();
        var containsLetter = false;
        var isHashPrefixedToken = false;
        foreach (var ch in title)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
                containsLetter |= char.IsLetter(ch);
                continue;
            }

            CommitToken(tokens, builder, containsLetter, isHashPrefixedToken);
            containsLetter = false;
            isHashPrefixedToken = ch == '#';
        }

        CommitToken(tokens, builder, containsLetter, isHashPrefixedToken);
        return tokens;
    }

    private static void CommitToken(HashSet<string> tokens, StringBuilder builder, bool containsLetter, bool isHashPrefixedToken)
    {
        if (builder.Length == 0)
        {
            builder.Clear();
            return;
        }

        if (!containsLetter && !isHashPrefixedToken)
        {
            builder.Clear();
            return;
        }

        tokens.Add(isHashPrefixedToken ? $"#{builder}" : builder.ToString());
        builder.Clear();
    }

    private static bool IsHashNumberToken(string token)
    {
        return token.Length > 1 && token[0] == '#' && token.Skip(1).All(char.IsDigit);
    }

    private MoveAttemptResult TryMoveToSavedDesktop(Config config, IntPtr handle, WindowLayoutSnapshotEntry snapshotEntry)
    {
        if (!config.EnableVirtualDesktopRestore || string.IsNullOrWhiteSpace(snapshotEntry.SavedDesktopId))
        {
            return MoveAttemptResult.NotApplicable;
        }

        if (!Guid.TryParse(snapshotEntry.SavedDesktopId, out var desktopId))
        {
            return MoveAttemptResult.CreateFallback("Saved virtual desktop id is invalid.");
        }

        if (desktopId == Guid.Empty)
        {
            return MoveAttemptResult.NotApplicable;
        }

        if (VirtualDesktopReadHelper.TryGetDesktopIdWithRetry(_virtualDesktopService, handle, out var currentDesktopId)
            && currentDesktopId == desktopId)
        {
            return MoveAttemptResult.NotApplicable;
        }

        if (!_virtualDesktopService.CanMoveWindow)
        {
            return MoveAttemptResult.CreateFallback("Virtual desktop move capability unavailable.");
        }

        return _virtualDesktopService.TryMoveWindowToDesktop(handle, desktopId, out var error)
            ? MoveAttemptResult.NotApplicable
            : MoveAttemptResult.CreateFallback(string.IsNullOrWhiteSpace(error) ? "Unable to move window to saved virtual desktop." : error);
    }

    private static WindowSize ToWindowSize(WindowLayoutSnapshotEntry snapshotEntry)
    {
        return new WindowSize
        {
            Name = snapshotEntry.ProcessName,
            Title = snapshotEntry.ExactTitle,
            Rect = snapshotEntry.Rect,
            State = snapshotEntry.State,
            MaximizedPosition = snapshotEntry.MaximizedPosition,
            SavedDesktopId = snapshotEntry.SavedDesktopId
        };
    }

    private static string CreateFallbackKey(string processName, string windowClassName)
    {
        return $"{processName.ToUpperInvariant()}\u001F{windowClassName}";
    }

    private static string DescribeEntry(WindowLayoutSnapshotEntry snapshotEntry, string? detail = null)
    {
        var description = $"{snapshotEntry.ProcessName} :: {snapshotEntry.ExactTitle} :: {snapshotEntry.WindowClassName}";
        return string.IsNullOrWhiteSpace(detail) ? description : $"{description} [{detail}]";
    }

    private static bool ShouldSkipContext(WindowRuleContext context)
    {
        return string.Equals(context.WindowClassName, "TopLevelWindowForOverflowXamlIsland", StringComparison.Ordinal);
    }

    private static bool ShouldSkipSnapshotEntry(WindowLayoutSnapshotEntry entry)
    {
        return string.Equals(entry.WindowClassName, "TopLevelWindowForOverflowXamlIsland", StringComparison.Ordinal);
    }

    private readonly struct RestoreAssignment
    {
        public RestoreAssignment(WindowLayoutSnapshotEntry snapshotEntry, WindowRuleContext candidate)
        {
            SnapshotEntry = snapshotEntry;
            Candidate = candidate;
        }

        public WindowLayoutSnapshotEntry SnapshotEntry { get; }

        public WindowRuleContext Candidate { get; }
    }

    private readonly struct MoveAttemptResult
    {
        private MoveAttemptResult(bool fallback, string? errorMessage)
        {
            IsFallback = fallback;
            ErrorMessage = errorMessage;
        }

        public static MoveAttemptResult NotApplicable => new(false, null);

        public static MoveAttemptResult CreateFallback(string? errorMessage)
        {
            return new(true, errorMessage);
        }

        public bool IsFallback { get; }

        public string? ErrorMessage { get; }
    }
}
