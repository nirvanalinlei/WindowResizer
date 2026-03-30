using System;
using System.Runtime.InteropServices;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class Windows11_24H2VirtualDesktopComContext : IWindows11_24H2VirtualDesktopComContext
{
    private static readonly Guid ImmersiveShellClassId = new("C2F03A33-21F5-47FA-B4BB-156362A2F239");
    private static readonly Guid VirtualDesktopManagerInternalServiceId = new("C5E0CDCA-7B6E-41B2-9FC4-D93975CC467B");

    private readonly IVirtualDesktopManagerInternal? _manager;
    private readonly IApplicationViewCollection? _viewCollection;
    private readonly string? _unavailableError;
    private bool _isInvalidated;

    public Windows11_24H2VirtualDesktopComContext()
        : this(Environment.OSVersion.Version)
    {
    }

    public Windows11_24H2VirtualDesktopComContext(Version osVersion)
    {
        if (osVersion is null)
        {
            throw new ArgumentNullException(nameof(osVersion));
        }

        if (!Windows11_24H2ExplorerMoveBuildPolicy.Supports(osVersion))
        {
            _unavailableError = Windows11_24H2ExplorerMoveBuildPolicy.BuildUnsupportedMessage(osVersion);
            return;
        }

        try
        {
            var shellType = Type.GetTypeFromCLSID(ImmersiveShellClassId, throwOnError: false);
            if (shellType is null)
            {
                _unavailableError = "ImmersiveShell COM class unavailable.";
                return;
            }

            var shell = (IServiceProvider10)Activator.CreateInstance(shellType)!;

            var internalInterfaceId = typeof(IVirtualDesktopManagerInternal).GUID;
            var internalServiceId = VirtualDesktopManagerInternalServiceId;
            _manager = (IVirtualDesktopManagerInternal)shell.QueryService(
                ref internalServiceId,
                ref internalInterfaceId);

            var viewCollectionId = typeof(IApplicationViewCollection).GUID;
            _viewCollection = (IApplicationViewCollection)shell.QueryService(
                ref viewCollectionId,
                ref viewCollectionId);

            if (_manager is null || _viewCollection is null)
            {
                _unavailableError = "Virtual desktop COM services unavailable.";
            }
        }
        catch (COMException e)
        {
            _unavailableError = e.Message;
        }
        catch (InvalidCastException e)
        {
            _unavailableError = e.Message;
        }
    }

    public bool IsAvailable => !_isInvalidated && _manager is not null && _viewCollection is not null;

    public string? GetUnavailableError()
    {
        return _unavailableError;
    }

    public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
    {
        if (!IsAvailable)
        {
            error = GetUnavailableError() ?? "Virtual desktop COM services unavailable.";
            return false;
        }

        if (hWnd == IntPtr.Zero)
        {
            error = "Window handle unavailable.";
            return false;
        }

        try
        {
            _viewCollection!.GetViewForHwnd(hWnd, out var view);
            if (view is null)
            {
                error = "Window view unavailable.";
                return false;
            }

            var desktop = _manager!.FindDesktop(ref desktopId);
            if (desktop is null)
            {
                error = "Target virtual desktop was not found.";
                return false;
            }

            _manager.MoveViewToDesktop(view, desktop);
            error = null;
            return true;
        }
        catch (COMException e)
        {
            _isInvalidated = true;
            error = e.Message;
            return false;
        }
        catch (InvalidCastException e)
        {
            _isInvalidated = true;
            error = e.Message;
            return false;
        }
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("6D5140C1-7436-11CE-8034-00AA006009FA")]
    private interface IServiceProvider10
    {
        [return: MarshalAs(UnmanagedType.IUnknown)]
        object QueryService(ref Guid service, ref Guid riid);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIInspectable)]
    [Guid("372E1D3B-38D3-42E4-A15B-8AB2B178F513")]
    private interface IApplicationView
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("1841C6D7-4F9D-42C0-AF41-8747538F10E5")]
    private interface IApplicationViewCollection
    {
        int GetViews(out IObjectArray array);

        int GetViewsByZOrder(out IObjectArray array);

        int GetViewsByAppUserModelId(string id, out IObjectArray array);

        int GetViewForHwnd(IntPtr hwnd, out IApplicationView view);

        int GetViewForApplication(object application, out IApplicationView view);

        int GetViewForAppUserModelId(string id, out IApplicationView view);

        int GetViewInFocus(out IntPtr view);

        int Unknown1(out IntPtr view);

        void RefreshCollection();

        int RegisterForApplicationViewChanges(object listener, out int cookie);

        int UnregisterForApplicationViewChanges(int cookie);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("92CA9DCD-5622-4BBA-A805-5E9F541BD8C9")]
    private interface IObjectArray
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("3F07F4BE-B107-441A-AF0F-39D82529072C")]
    private interface IVirtualDesktop
    {
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("53F5CA0B-158F-4124-900C-057158060B27")]
    private interface IVirtualDesktopManagerInternal
    {
        int GetCount();

        void MoveViewToDesktop(IApplicationView view, IVirtualDesktop desktop);

        bool CanViewMoveDesktops(IApplicationView view);

        IVirtualDesktop GetCurrentDesktop();

        void GetDesktops(out IObjectArray desktops);

        [PreserveSig]
        int GetAdjacentDesktop(IVirtualDesktop from, int direction, out IVirtualDesktop desktop);

        void SwitchDesktop(IVirtualDesktop desktop);

        void SwitchDesktopAndMoveForegroundView(IVirtualDesktop desktop);

        IVirtualDesktop CreateDesktop();

        void MoveDesktop(IVirtualDesktop desktop, int nIndex);

        void RemoveDesktop(IVirtualDesktop desktop, IVirtualDesktop fallback);

        IVirtualDesktop FindDesktop(ref Guid desktopId);
    }
}
