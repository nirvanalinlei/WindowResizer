using System;
using WindowResizer.Common.Exceptions;
using WindowResizer.Base.Abstractions;
using WindowResizer.Common.Windows;
using WindowResizer.Configuration;
using WindowResizer.Core.WindowControl;

namespace WindowResizer.Base.Services;

public sealed class WindowPlacementService : IWindowPlacementService
{
    public WindowPlacement GetPlacement(IntPtr handle)
    {
        return Resizer.GetPlacement(handle);
    }

    public bool RestorePlacement(IntPtr handle, WindowSize windowSize, out string? errorMessage)
    {
        try
        {
            errorMessage = null;
            return Resizer.SetPlacement(handle, windowSize.Rect, windowSize.MaximizedPosition, windowSize.State);
        }
        catch (WindowResizerException exception)
        {
            errorMessage = exception.Message;
            return false;
        }
    }
}
