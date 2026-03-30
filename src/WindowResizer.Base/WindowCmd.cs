using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using WindowResizer.Base.Coordinators;
using WindowResizer.Configuration;
using WindowResizer.Core.WindowControl;
using static WindowResizer.Base.WindowUtils;

namespace WindowResizer.Base;

public static class WindowCmd
{
    public static bool Resize(string? configPath, string? profileName, string? process, string? title,
        Action<string>? onError = null,
        Action<List<TargetWindow>>? onDebug = null)
    {
        var profile = LoadConfig(configPath, profileName, onError);
        if (profile is null)
        {
            return false;
        }

        var windows = Resizer.GetOpenWindows();
        windows.Reverse();

        var targets = new List<TargetWindow>();

        foreach (var handler in windows)
        {
            if (!IsProcessAvailable(handler, out string processName, null))
            {
                continue;
            }

            var t = Resizer.GetWindowTitle(handler);

            targets.Add(new TargetWindow(handler, processName, t));
        }

        bool resizeAllProcesses = string.IsNullOrEmpty(process);

        if (!resizeAllProcesses)
        {
            targets = targets.Where(i => i.ProcessName.Equals(process, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (!string.IsNullOrEmpty(title))
        {
            var regex = new Regex(title);
            targets = targets.Where(i => !string.IsNullOrEmpty(i.Title) && regex.IsMatch(i.Title!)).ToList();
        }

        foreach (var tp in targets)
        {
            var restoreResult = ResizeWindow(tp.Handle, profile, (p, e) =>
            {
                tp.Result = "Elevated privileges may be required.";
                if (!resizeAllProcesses)
                {
                    onError?.Invoke($"Unable to resize process <{p}>, elevated privileges may be required.");
                }
            }, (p, t) =>
            {
                var message = $"No saved settings.";
                tp.Result = message;
                if (!resizeAllProcesses)
                {
                    onError?.Invoke($"No saved settings for <{p} :: {t}>.");
                }
            });

            tp.MatchedRuleTitle = restoreResult.MatchedRuleTitle;
            tp.Restored = restoreResult.PlacementRestored;
            tp.VirtualDesktopMoveStatus = restoreResult.VirtualDesktopMoveStatus;
            if (string.IsNullOrWhiteSpace(tp.Result))
            {
                tp.Result = CombineMessages(restoreResult.VirtualDesktopMoveErrorMessage, restoreResult.ErrorMessage);
            }
        }

        onDebug?.Invoke(targets);

        return true;
    }

    public class TargetWindow
    {
        public TargetWindow(IntPtr handle, string processName, string? title)
        {
            Handle = handle;
            ProcessName = processName;
            Title = title;
        }

        public IntPtr Handle { get; }

        public string ProcessName { get; }

        public string? Title { get; }

        public string? MatchedRuleTitle { get; set; }

        public bool Restored { get; set; }

        public VirtualDesktopMoveStatus VirtualDesktopMoveStatus { get; set; } = VirtualDesktopMoveStatus.NotApplicable;

        public string Result { get; set; } = string.Empty;
    }

    private static string CombineMessages(string? desktopMoveError, string? restoreError)
    {
        if (string.IsNullOrWhiteSpace(desktopMoveError))
        {
            return restoreError ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(restoreError))
        {
            return desktopMoveError!;
        }

        return $"{desktopMoveError}; {restoreError}";
    }

    private static Config? LoadConfig(string? configPath, string? profileName, Action<string>? onError)
    {
        if (!ConfigUtils.Load(configPath, onError))
        {
            return null;
        }

        if (string.IsNullOrEmpty(profileName))
        {
            return ConfigFactory.Current;
        }

        var p = ConfigFactory.Profiles.Configs.FirstOrDefault(i =>
            i.ProfileName.Equals(profileName, StringComparison.OrdinalIgnoreCase));
        if (p is null)
        {
            onError?.Invoke($"Profile <{profileName}> not exists");
        }

        return p;
    }
}
