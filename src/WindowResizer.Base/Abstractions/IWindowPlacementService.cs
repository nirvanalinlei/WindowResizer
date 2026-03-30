using System;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;

namespace WindowResizer.Base.Abstractions;

public interface IWindowPlacementService
{
    WindowPlacement GetPlacement(IntPtr handle);

    bool RestorePlacement(IntPtr handle, WindowSize windowSize, out string? errorMessage);
}
