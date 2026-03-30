using System;

namespace WindowResizer.Base;

public sealed class WindowRuleContext
{
    public WindowRuleContext(IntPtr handle, string processName, string title, string windowClassName)
    {
        Handle = handle;
        ProcessName = processName;
        Title = title;
        WindowClassName = windowClassName;
    }

    public IntPtr Handle { get; }

    public string ProcessName { get; }

    public string Title { get; }

    public string WindowClassName { get; }
}
