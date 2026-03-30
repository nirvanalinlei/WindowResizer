using System;
using System.Runtime.InteropServices;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class WindowsVirtualDesktopService : IVirtualDesktopService
{
    private static readonly Guid VirtualDesktopManagerClassId = new("AA509086-5CA9-4C25-8F95-589D3C07B48A");

    private readonly IVirtualDesktopManagerNative? _manager;

    public WindowsVirtualDesktopService()
    {
        if (Environment.OSVersion.Version < new Version(10, 0))
        {
            return;
        }

        try
        {
            var type = Type.GetTypeFromCLSID(VirtualDesktopManagerClassId, throwOnError: false);
            if (type is null)
            {
                return;
            }

            _manager = Activator.CreateInstance(type) as IVirtualDesktopManagerNative;
        }
        catch (COMException)
        {
        }
        catch (InvalidCastException)
        {
        }
    }

    public bool CanReadDesktopId => _manager is not null;

    public bool CanMoveWindow => false;

    public bool TryGetWindowDesktopId(IntPtr hWnd, out Guid desktopId)
    {
        desktopId = Guid.Empty;
        if (_manager is null)
        {
            return false;
        }

        try
        {
            return _manager.GetWindowDesktopId(hWnd, out desktopId) >= 0;
        }
        catch (COMException)
        {
            desktopId = Guid.Empty;
            return false;
        }
        catch (InvalidCastException)
        {
            desktopId = Guid.Empty;
            return false;
        }
    }

    public bool TryIsWindowOnCurrentDesktop(IntPtr hWnd, out bool isOnCurrentDesktop)
    {
        isOnCurrentDesktop = true;
        if (_manager is null)
        {
            return false;
        }

        try
        {
            return _manager.IsWindowOnCurrentVirtualDesktop(hWnd, out isOnCurrentDesktop) >= 0;
        }
        catch (COMException)
        {
            isOnCurrentDesktop = true;
            return false;
        }
        catch (InvalidCastException)
        {
            isOnCurrentDesktop = true;
            return false;
        }
    }

    public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
    {
        error = null;
        if (_manager is null)
        {
            error = "Virtual desktop support is unavailable.";
            return false;
        }

        int result;
        try
        {
            result = _manager.MoveWindowToDesktop(hWnd, ref desktopId);
        }
        catch (COMException e)
        {
            error = e.Message;
            return false;
        }
        catch (InvalidCastException e)
        {
            error = e.Message;
            return false;
        }

        if (result >= 0)
        {
            return true;
        }

        error = $"MoveWindowToDesktop failed with HRESULT 0x{result:X8}.";
        return false;
    }

    [ComImport]
    [Guid("A5CD92FF-29BE-454C-8D04-D82879FB3F1B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IVirtualDesktopManagerNative
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop(IntPtr topLevelWindow, [MarshalAs(UnmanagedType.Bool)] out bool onCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId(IntPtr topLevelWindow, out Guid desktopId);

        [PreserveSig]
        int MoveWindowToDesktop(IntPtr topLevelWindow, ref Guid desktopId);
    }
}
