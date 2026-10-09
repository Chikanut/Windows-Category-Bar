using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace CategoryBar.Interop;

/// <summary>Registers an application desktop toolbar so maximized windows leave room for the bar.</summary>
internal sealed class AppBarReservation : IDisposable
{
    private readonly HwndSource source;
    private readonly uint callback;
    private bool dirty = true;
    private bool disposed;
    private PixelRect lastMonitor;
    private int lastHeight, lastBottom;

    internal AppBarReservation()
    {
        callback = Native.RegisterWindowMessage("WindowsCategoryBar.AppBarCallback");
        source = new HwndSource(new HwndSourceParameters("CategoryBar.ReservedArea")
        {
            WindowStyle = unchecked((int)0x80000000), ExtendedWindowStyle = 0x080000A0,
            Width = 1, Height = 1, PositionX = 0, PositionY = 0
        });
        source.AddHook((IntPtr _, int message, IntPtr _, IntPtr _, ref bool _) =>
        {
            if (message == callback) dirty = true;
            return IntPtr.Zero;
        });
        var data = Data();
        Native.SHAppBarMessage(0, ref data); // ABM_NEW
    }

    private Native.AppBarData Data() => new()
    {
        Size = (uint)Marshal.SizeOf<Native.AppBarData>(), Window = source.Handle, Callback = callback, Edge = 3
    };

    internal void Update(PixelRect monitor, int height, int taskbarTop)
    {
        if (!dirty && monitor == lastMonitor && lastHeight == height && lastBottom == taskbarTop) return;
        var data = Data();
        data.Rect = new Native.Rect { Left = monitor.Left, Top = monitor.Top, Right = monitor.Right, Bottom = monitor.Bottom };
        Native.SHAppBarMessage(2, ref data); // ABM_QUERYPOS
        data.Rect.Bottom = Math.Min(data.Rect.Bottom, taskbarTop);
        data.Rect.Top = data.Rect.Bottom - height;
        Native.SHAppBarMessage(3, ref data); // ABM_SETPOS
        Native.SetWindowPos(source.Handle, IntPtr.Zero, data.Rect.Left, data.Rect.Top, data.Rect.Right - data.Rect.Left, height, 0x0014);
        lastMonitor = monitor; lastHeight = height; lastBottom = taskbarTop; dirty = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        var data = Data();
        Native.SHAppBarMessage(1, ref data); // ABM_REMOVE
        source.Dispose();
    }
}
