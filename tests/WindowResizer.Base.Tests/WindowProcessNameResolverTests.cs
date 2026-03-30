using System;
using System.Diagnostics;
using WindowResizer.Base;
using Xunit;

namespace WindowResizer.Base.Tests;

public class WindowProcessNameResolverTests
{
    [Fact]
    public void TryGetProcessName_MainModuleAccessDenied_FallsBackToProcessNameExe()
    {
        using var process = Process.GetCurrentProcess();
        var onFailedCalls = 0;

        var available = WindowProcessNameResolver.TryGetProcessName(
            new IntPtr(11),
            _ => false,
            _ => process,
            _ => throw new InvalidOperationException("Access is denied."),
            _ => "admin-app",
            _ => false,
            (_, _) => onFailedCalls++,
            out var resolvedProcess,
            out var processName);

        Assert.True(available);
        Assert.Equal(process, resolvedProcess);
        Assert.Equal("admin-app.exe", processName);
        Assert.Equal(0, onFailedCalls);
    }

    [Fact]
    public void TryGetProcessName_FallbackAlreadyHasExeExtension_DoesNotAppendTwice()
    {
        using var process = Process.GetCurrentProcess();

        var available = WindowProcessNameResolver.TryGetProcessName(
            new IntPtr(12),
            _ => false,
            _ => process,
            _ => string.Empty,
            _ => "admin-app.exe",
            _ => false,
            null,
            out _,
            out var processName);

        Assert.True(available);
        Assert.Equal("admin-app.exe", processName);
    }

    [Fact]
    public void TryGetProcessName_InvisibleFallbackProcess_IsRejected()
    {
        using var process = Process.GetCurrentProcess();

        var available = WindowProcessNameResolver.TryGetProcessName(
            new IntPtr(13),
            _ => false,
            _ => process,
            _ => throw new InvalidOperationException("Access is denied."),
            _ => "hidden-app",
            processName => processName == "hidden-app.exe",
            null,
            out var resolvedProcess,
            out var processName);

        Assert.False(available);
        Assert.Null(resolvedProcess);
        Assert.Equal(string.Empty, processName);
    }

    [Fact]
    public void TryGetProcessName_FallbackProcessNameThrows_ReturnsFalseWithoutThrowing()
    {
        using var process = Process.GetCurrentProcess();

        var available = WindowProcessNameResolver.TryGetProcessName(
            new IntPtr(14),
            _ => false,
            _ => process,
            _ => throw new InvalidOperationException("Access is denied."),
            _ => throw new InvalidOperationException("Process exited."),
            _ => false,
            null,
            out var resolvedProcess,
            out var processName);

        Assert.False(available);
        Assert.Null(resolvedProcess);
        Assert.Equal(string.Empty, processName);
    }
}
