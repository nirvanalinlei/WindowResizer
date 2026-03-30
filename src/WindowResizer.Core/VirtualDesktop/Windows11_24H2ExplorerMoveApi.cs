using System;
using System.Runtime.InteropServices;

namespace WindowResizer.Core.VirtualDesktop;

public sealed class Windows11_24H2ExplorerMoveApi : IExplorerVirtualDesktopMoveApi
{
    private readonly Func<IWindows11_24H2VirtualDesktopComContext> _contextFactory;
    private readonly string? _unsupportedBuildError;
    private IWindows11_24H2VirtualDesktopComContext _context;

    public Windows11_24H2ExplorerMoveApi()
        : this(Environment.OSVersion.Version)
    {
    }

    public Windows11_24H2ExplorerMoveApi(Version osVersion)
        : this(osVersion, () => new Windows11_24H2VirtualDesktopComContext(osVersion))
    {
    }

    public Windows11_24H2ExplorerMoveApi(Version osVersion, IWindows11_24H2VirtualDesktopComContext context)
        : this(osVersion, () => context)
    {
    }

    public Windows11_24H2ExplorerMoveApi(Version osVersion, Func<IWindows11_24H2VirtualDesktopComContext> contextFactory)
    {
        if (osVersion is null)
        {
            throw new ArgumentNullException(nameof(osVersion));
        }

        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _context = CreateContext();
        if (!SupportsBuild(osVersion))
        {
            _unsupportedBuildError = Windows11_24H2ExplorerMoveBuildPolicy.BuildUnsupportedMessage(osVersion);
        }
    }

    public Windows11_24H2ExplorerMoveApi(IWindows11_24H2VirtualDesktopComContext context)
        : this(() => context)
    {
    }

    private Windows11_24H2ExplorerMoveApi(Func<IWindows11_24H2VirtualDesktopComContext> contextFactory)
    {
        _contextFactory = contextFactory ?? throw new ArgumentNullException(nameof(contextFactory));
        _context = CreateContext();
    }

    public bool IsAvailable => _unsupportedBuildError is null && EnsureAvailableContext().IsAvailable;

    public string? GetUnavailableError()
    {
        return _unsupportedBuildError ?? EnsureAvailableContext().GetUnavailableError();
    }

    public bool TryMoveWindowToDesktop(IntPtr hWnd, Guid desktopId, out string? error)
    {
        var context = EnsureAvailableContext();
        if (_unsupportedBuildError is not null || !context.IsAvailable)
        {
            error = GetUnavailableError() ?? "Virtual desktop COM services unavailable.";
            return false;
        }

        if (TryMoveWindowToDesktop(context, hWnd, desktopId, out error))
        {
            return true;
        }

        if (context.IsAvailable)
        {
            return false;
        }

        if (!TryRefreshContext())
        {
            error ??= GetUnavailableError() ?? "Virtual desktop COM services unavailable.";
            return false;
        }

        context = _context;
        if (!context.IsAvailable)
        {
            error ??= GetUnavailableError() ?? "Virtual desktop COM services unavailable.";
            return false;
        }

        return TryMoveWindowToDesktop(context, hWnd, desktopId, out error);
    }

    private IWindows11_24H2VirtualDesktopComContext EnsureAvailableContext()
    {
        if (_unsupportedBuildError is null && !_context.IsAvailable)
        {
            TryRefreshContext();
        }

        return _context;
    }

    private bool TryRefreshContext()
    {
        var refreshedContext = CreateContext();
        _context = refreshedContext;
        return _context.IsAvailable;
    }

    private IWindows11_24H2VirtualDesktopComContext CreateContext()
    {
        return _contextFactory() ?? throw new InvalidOperationException("Virtual desktop COM context factory returned null.");
    }

    private static bool TryMoveWindowToDesktop(
        IWindows11_24H2VirtualDesktopComContext context,
        IntPtr hWnd,
        Guid desktopId,
        out string? error)
    {
        try
        {
            return context.TryMoveWindowToDesktop(hWnd, desktopId, out error);
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
    }

    private static bool SupportsBuild(Version osVersion)
    {
        return Windows11_24H2ExplorerMoveBuildPolicy.Supports(osVersion);
    }
}
