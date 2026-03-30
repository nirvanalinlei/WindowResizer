using System;
using System.Linq;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;

namespace WindowResizer.Base;

public sealed class WindowRuleUpdater
{
    private readonly WindowRuleMatcher _matcher;

    public WindowRuleUpdater(WindowRuleMatcher matcher)
    {
        _matcher = matcher;
    }

    public void Upsert(Config config, MatchWindowSize match, string processName, string? title, WindowPlacement placement, string? savedDesktopId)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return;
        }

        if (!config.EnableResizeByTitle)
        {
            if (match.NoMatch || match.WildcardMatch is null)
            {
                InsertOrder(config, CreateWindowSize(processName, "*", placement, null));
            }
            else
            {
                UpdateWindowSize(match.WildcardMatch, placement, null);
            }

            return;
        }

        if (match.NoMatch)
        {
            InsertOrder(config, CreateWindowSize(processName, "*", placement, null));

            if (!string.IsNullOrWhiteSpace(title))
            {
                InsertOrder(config, CreateWindowSize(processName, title!, placement, savedDesktopId));
            }

            return;
        }

        if (match.FullMatch != null)
        {
            UpdateWindowSize(match.FullMatch, placement, savedDesktopId);
        }
        else if (!string.IsNullOrWhiteSpace(title))
        {
            var delay = match.All.FirstOrDefault(i => i is { AutoResizeDelay: > 0 })?.AutoResizeDelay ?? 0;
            var entry = CreateWindowSize(processName, title!, placement, savedDesktopId);
            entry.AutoResizeDelay = delay;
            InsertOrder(config, entry);
        }

        if (match.SuffixMatch != null)
        {
            UpdateWindowSize(match.SuffixMatch, placement, null);
        }

        if (match.PrefixMatch != null)
        {
            UpdateWindowSize(match.PrefixMatch, placement, null);
        }

        if (match.WildcardMatch != null)
        {
            UpdateWindowSize(match.WildcardMatch, placement, null);
        }
        else
        {
            InsertOrder(config, CreateWindowSize(processName, "*", placement, null));
        }
    }

    private WindowSize CreateWindowSize(string processName, string title, WindowPlacement placement, string? savedDesktopId)
    {
        return new WindowSize
        {
            Name = processName,
            Title = title,
            Rect = placement.Rect,
            State = placement.WindowState,
            MaximizedPosition = placement.MaximizedPosition,
            SavedDesktopId = NormalizeSavedDesktopId(title, savedDesktopId)
        };
    }

    private void UpdateWindowSize(WindowSize windowSize, WindowPlacement placement, string? savedDesktopId)
    {
        windowSize.Rect = placement.Rect;
        windowSize.State = placement.WindowState;
        windowSize.MaximizedPosition = placement.MaximizedPosition;
        windowSize.SavedDesktopId = NormalizeSavedDesktopId(windowSize.Title, savedDesktopId);
    }

    private string? NormalizeSavedDesktopId(string? title, string? savedDesktopId)
    {
        return _matcher.IsExactRuleTitle(title) ? savedDesktopId : null;
    }

    private static void InsertOrder(Config config, WindowSize item)
    {
        var list = config.WindowSizes;
        var backing = list.ToList();
        backing.Add(item);
        var index = backing.OrderBy(l => l.Name).ThenBy(l => l.Title).ToList().IndexOf(item);
        list.Insert(index, item);
    }
}
