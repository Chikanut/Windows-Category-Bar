using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;
using CategoryBar.Core;

namespace CategoryBar;

internal static class NativeMethods
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelRect Pixels => new(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo
    {
        public int Size; public Rect Monitor, Work; public uint Flags;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData
    {
        public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Rect; public IntPtr Param;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] internal static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] internal static extern IntPtr GetTopWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] internal static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint RegisterWindowMessage(string name);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    private delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);

    internal static List<IntPtr> Taskbars()
    {
        var result = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            var name = new StringBuilder(256); GetClassName(window, name, name.Capacity);
            if (name.ToString() is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") result.Add(window);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    internal static bool IsAbove(IntPtr first, IntPtr second)
    {
        // Traverse the actual z-order, rather than treating WS_EX_TOPMOST as visibility.
        for (var current = GetTopWindow(IntPtr.Zero); current != IntPtr.Zero; current = GetWindow(current, 2))
        {
            if (current == first) return true;
            if (current == second) return false;
        }
        return false;
    }

    internal static TaskbarObservation Observe(IntPtr taskbar = default)
    {
        if (taskbar == IntPtr.Zero) taskbar = FindWindow("Shell_TrayWnd", null);
        if (!IsWindow(taskbar) || !GetWindowRect(taskbar, out var bar)) return default;
        var monitor = MonitorFromWindow(taskbar, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return default;
        _ = DwmGetWindowAttribute(taskbar, 14, out var cloaked, sizeof(int));
        var data = new AppBarData { Size = (uint)Marshal.SizeOf<AppBarData>() };
        var autoHide = (SHAppBarMessage(4, ref data).ToUInt64() & 1) != 0;
        var foreground = GetForegroundWindow();
        var className = new StringBuilder(256);
        _ = GetClassName(foreground, className, className.Capacity);
        var desktop = className.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        var fullscreen = !desktop && foreground != IntPtr.Zero && MonitorFromWindow(foreground, 2) == monitor
            && GetWindowRect(foreground, out var fg) && fg.Left <= info.Monitor.Left && fg.Top <= info.Monitor.Top
            && fg.Right >= info.Monitor.Right && fg.Bottom >= info.Monitor.Bottom;
        return new(true, IsWindowVisible(taskbar), cloaked != 0, autoHide, bar.Pixels, info.Monitor.Pixels,
            fullscreen, foreground == taskbar || IsAbove(taskbar, foreground));
    }
}
