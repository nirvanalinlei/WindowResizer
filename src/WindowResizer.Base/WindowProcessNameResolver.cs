using System;
using System.Diagnostics;

namespace WindowResizer.Base;

internal static class WindowProcessNameResolver
{
    public static bool TryGetProcessName(
        IntPtr handle,
        Func<IntPtr, bool> isChildWindow,
        Func<IntPtr, Process?> getRealProcess,
        Func<Process, string> getModuleName,
        Func<Process, string> getProcessName,
        Func<string, bool> isInvisibleProcess,
        Action<Process, Exception>? onFailed,
        out Process? process,
        out string processName)
    {
        process = null;
        processName = string.Empty;

        if (isChildWindow(handle))
        {
            return false;
        }

        process = getRealProcess(handle);
        if (process is null)
        {
            return false;
        }

        try
        {
            processName = getModuleName(process) ?? string.Empty;
        }
        catch
        {
        }

        if (string.IsNullOrWhiteSpace(processName))
        {
            try
            {
                processName = NormalizeProcessName(getProcessName(process));
            }
            catch (Exception e)
            {
                onFailed?.Invoke(process, e);
                process = null;
                processName = string.Empty;
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(processName) || isInvisibleProcess(processName))
        {
            process = null;
            processName = string.Empty;
            return false;
        }

        return true;
    }

    private static string NormalizeProcessName(string? processName)
    {
        if (string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        return processName!.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : processName + ".exe";
    }
}
