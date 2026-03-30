using System;
using System.Threading;
using WindowResizer.Core.VirtualDesktop;

namespace WindowResizer.Base.Coordinators;

internal static class VirtualDesktopReadHelper
{
    private const int MaxAttempts = 3;
    private const int DelayMilliseconds = 100;

    public static bool TryGetDesktopIdWithRetry(IVirtualDesktopService service, IntPtr handle, out Guid desktopId)
    {
        if (service is null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        desktopId = Guid.Empty;
        if (!service.CanReadDesktopId)
        {
            return false;
        }

        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            if (service.TryGetWindowDesktopId(handle, out desktopId))
            {
                return true;
            }

            if (attempt < MaxAttempts - 1)
            {
                Thread.Sleep(DelayMilliseconds);
            }
        }

        desktopId = Guid.Empty;
        return false;
    }
}
