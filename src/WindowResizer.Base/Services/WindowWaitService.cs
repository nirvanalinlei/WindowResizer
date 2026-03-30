using System.Threading;
using WindowResizer.Base.Abstractions;

namespace WindowResizer.Base.Services;

public sealed class WindowWaitService : IWindowWaitService
{
    public void Sleep(int millisecondsDelay)
    {
        Thread.Sleep(millisecondsDelay);
    }
}
