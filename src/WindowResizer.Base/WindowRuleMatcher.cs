using System;
using System.Collections.Generic;
using System.Linq;
using WindowResizer.Configuration;

namespace WindowResizer.Base;

public sealed class WindowRuleMatcher
{
    public MatchWindowSize Match(Config config, string processName, string? title, bool onlyAuto = false)
    {
        var windows = config.WindowSizes
            .Where(w => w.Name.Equals(processName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!config.EnableResizeByTitle)
        {
            windows = windows.Where(w => w.Title.Equals("*")).ToList();

            if (onlyAuto)
            {
                windows = windows.Where(w => w.AutoResize).ToList();
            }

            return new MatchWindowSize
            {
                WildcardMatch = windows.FirstOrDefault()
            };
        }

        if (onlyAuto)
        {
            windows = windows.Where(w => w.AutoResize).ToList();
        }

        title ??= "*";
        if (string.IsNullOrEmpty(title))
        {
            title = "*";
        }

        return new MatchWindowSize
        {
            FullMatch = windows.FirstOrDefault(w => w.Title == title),
            PrefixMatch = windows.FirstOrDefault(w =>
                w.Title.StartsWith("*", StringComparison.Ordinal)
                && w.Title.Length > 1
                && title.EndsWith(w.Title.TrimStart('*'), StringComparison.Ordinal)),
            SuffixMatch = windows.FirstOrDefault(w =>
                w.Title.EndsWith("*", StringComparison.Ordinal)
                && w.Title.Length > 1
                && title.StartsWith(w.Title.TrimEnd('*'), StringComparison.Ordinal)),
            WildcardMatch = windows.FirstOrDefault(w => w.Title.Equals("*"))
        };
    }

    public WindowSize? SelectPreferredMatch(MatchWindowSize match)
    {
        return match.FullMatch ?? match.PrefixMatch ?? match.SuffixMatch ?? match.WildcardMatch;
    }

    public bool IsExactTitleRule(WindowSize windowSize)
    {
        return !string.IsNullOrWhiteSpace(windowSize.Title)
               && !windowSize.Title.Equals("*", StringComparison.Ordinal)
               && !windowSize.Title.StartsWith("*", StringComparison.Ordinal)
               && !windowSize.Title.EndsWith("*", StringComparison.Ordinal);
    }

    public bool IsExactRuleTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var exactTitle = title!;
        return !exactTitle.Equals("*", StringComparison.Ordinal)
               && !exactTitle.StartsWith("*", StringComparison.Ordinal)
               && !exactTitle.EndsWith("*", StringComparison.Ordinal);
    }

    public bool HasLiteralWindowTitle(string? title)
    {
        return !string.IsNullOrWhiteSpace(title);
    }
}
