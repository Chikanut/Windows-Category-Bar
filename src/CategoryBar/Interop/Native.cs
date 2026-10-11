using System.Runtime.InteropServices;
using System.Text;

namespace CategoryBar.Interop;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential)] internal struct Rect
    {
        public int Left, Top, Right, Bottom;
        public readonly PixelRect Pixels => new(Left, Top, Right, Bottom);
    }
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] internal struct AppBarData
    {
        public uint Size; public IntPtr Window; public uint Callback, Edge; public Rect Rect; public IntPtr Param;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct NotifyIconData
    {
        public int Size; public IntPtr Window; public uint Id, Flags, CallbackMessage; public IntPtr Icon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Tip;
        public uint State, StateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Info;
        public uint TimeoutOrVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string InfoTitle;
        public uint InfoFlags; public Guid Item; public IntPtr BalloonIcon;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] internal struct ShFileInfo
    {
        public IntPtr Icon; public int IconIndex; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfoHeader
    {
        public int Size, Width, Height; public short Planes, BitCount; public int Compression, SizeImage, XPels, YPels, ClrUsed, ClrImportant;
    }

    internal const int GwlExStyle = -20;
    internal const long ExToolWindow = 0x80, ExNoActivate = 0x08000000, ExTopmost = 0x8;
    internal static readonly IntPtr Topmost = new(-1);
    internal const uint SwpNoActivate = 0x10, SwpNoMove = 0x2, SwpNoSize = 0x1, SwpNoZOrder = 0x4, SwpShowWindow = 0x40, SwpHideWindow = 0x80, SwpAsync = 0x4000;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string? className, string? title);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool IsZoomed(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll")] internal static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetWindowText(IntPtr window, StringBuilder text, int length);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] internal static extern bool AllowSetForegroundWindow(int processId);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr MonitorFromPoint(Point point, uint flags);
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
    [DllImport("user32.dll")] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[]? ids, uint count, uint flags);
    internal delegate bool EnumWindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool EnumWindows(EnumWindowCallback callback, IntPtr parameter);
    [DllImport("user32.dll")] internal static extern bool AttachThreadInput(uint thread, uint attachTo, bool attach);
    [DllImport("user32.dll")] internal static extern bool BringWindowToTop(IntPtr window);
    [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr window);
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr window, IntPtr dc);

    [DllImport("shell32.dll")] internal static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr ILCreateFromPathW(string path);
    [DllImport("shell32.dll")] internal static extern void ILFree(IntPtr list);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern bool Shell_NotifyIcon(uint message, ref NotifyIconData data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] internal static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

    [DllImport("dwmapi.dll")] internal static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("kernel32.dll")] internal static extern IntPtr OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern int GetApplicationUserModelId(IntPtr process, ref int length, StringBuilder id);

    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr handle);
    [DllImport("gdi32.dll")] internal static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);

    internal static string ClassName(IntPtr window)
    {
        var name = new StringBuilder(256);
        _ = GetClassName(window, name, name.Capacity);
        return name.ToString();
    }

    internal static Point Cursor() { GetCursorPos(out var p); return p; }

    /// <summary>The bounds of the monitor nearest to a screen point.</summary>
    internal static PixelRect MonitorAt(int x, int y)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        return GetMonitorInfo(MonitorFromPoint(new Point { X = x, Y = y }, 2), ref info) ? info.Monitor.Pixels : default;
    }

    internal static void SetExStyle(IntPtr window, long add, long remove = 0)
    {
        var style = GetWindowLongPtr(window, GwlExStyle).ToInt64();
        SetWindowLongPtr(window, GwlExStyle, new IntPtr((style | add) & ~remove));
    }

    /// <summary>
    /// Gives keyboard focus to one of our windows. The bar never activates on a click, so Windows does not
    /// consider it entitled to the foreground; sharing input state with the current foreground thread is.
    /// </summary>
    internal static void ForceForeground(IntPtr window)
    {
        var foreground = GetForegroundWindow();
        if (foreground == window) return;
        var current = GetCurrentThreadId();
        var other = foreground == IntPtr.Zero ? 0 : GetWindowThreadProcessId(foreground, out _);
        var attached = other != 0 && other != current && AttachThreadInput(current, other, true);
        try { BringWindowToTop(window); SetForegroundWindow(window); SetFocus(window); }
        finally { if (attached) AttachThreadInput(current, other, false); }
    }

    /// <summary>Dark title bar and rounded corners for regular (non-layered) windows.</summary>
    internal static void StyleFrame(IntPtr window, bool dark, bool rounded = true)
    {
        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(window, 20, ref value, sizeof(int)); // DWMWA_USE_IMMERSIVE_DARK_MODE
        var round = rounded ? 2 : 1;
        _ = DwmSetWindowAttribute(window, 33, ref round, sizeof(int)); // DWMWA_WINDOW_CORNER_PREFERENCE = ROUND
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FrameMargins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref FrameMargins margins);

    internal static bool ApplyBackdrop(IntPtr window, string backdrop)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return false;
        var type = backdrop switch { "Acrylic" => 3, "Mica" => 2, _ => 1 };
        var enabled = DwmSetWindowAttribute(window, 38, ref type, sizeof(int)) >= 0 && type != 1;
        var margins = enabled ? new FrameMargins { Left = -1, Right = -1, Top = -1, Bottom = -1 } : new FrameMargins();
        if (DwmExtendFrameIntoClientArea(window, ref margins) < 0) return false;
        return enabled;
    }

    internal static List<IntPtr> Taskbars()
    {
        var result = new List<IntPtr>();
        EnumWindows((window, _) =>
        {
            if (ClassName(window) is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") result.Add(window);
            return true;
        }, IntPtr.Zero);
        return result;
    }

    private static bool IsAbove(IntPtr first, IntPtr second)
    {
        // Traverse the actual z-order, rather than treating WS_EX_TOPMOST as visibility.
        for (var current = GetTopWindow(IntPtr.Zero); current != IntPtr.Zero; current = GetWindow(current, 2))
        {
            if (current == first) return true;
            if (current == second) return false;
        }
        return false;
    }

    internal static TaskbarObservation Observe(IntPtr taskbar)
    {
        if (!IsWindow(taskbar) || !GetWindowRect(taskbar, out var bar)) return default;
        var monitor = MonitorFromWindow(taskbar, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return default;
        _ = DwmGetWindowAttribute(taskbar, 14, out var cloaked, sizeof(int));
        var foreground = GetForegroundWindow();
        var desktop = foreground == IntPtr.Zero || ClassName(foreground) is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
        var fullscreen = !desktop && MonitorFromWindow(foreground, 2) == monitor
            && GetWindowRect(foreground, out var fg) && fg.Left <= info.Monitor.Left && fg.Top <= info.Monitor.Top
            && fg.Right >= info.Monitor.Right && fg.Bottom >= info.Monitor.Bottom;
        // The z-order walk is only needed to tell a covered taskbar from one revealed over a fullscreen window.
        return new(true, IsWindowVisible(taskbar), cloaked != 0, false, bar.Pixels, info.Monitor.Pixels,
            fullscreen, !fullscreen || foreground == taskbar || IsAbove(taskbar, foreground));
    }
}

[ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellItemImageFactory
{
    [PreserveSig] int GetImage(Native.Size size, int flags, out IntPtr bitmap);
}

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLinkObject { }

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    [PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int length, IntPtr findData, uint flags);
    [PreserveSig] int GetIDList(out IntPtr list);
    [PreserveSig] int SetIDList(IntPtr list);
    [PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int length);
    [PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
    [PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory, int length);
    [PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
    [PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments, int length);
    [PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
    [PreserveSig] int GetHotkey(out short hotkey);
    [PreserveSig] int SetHotkey(short hotkey);
    [PreserveSig] int GetShowCmd(out int command);
    [PreserveSig] int SetShowCmd(int command);
    [PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int length, out int index);
    [PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
    [PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
    [PreserveSig] int Resolve(IntPtr window, uint flags);
    [PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
}

[ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-C000-000000000046")]
internal interface IPersistFile
{
    [PreserveSig] int GetClassID(out Guid classId);
    [PreserveSig] int IsDirty();
    [PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string file, uint mode);
    [PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string file, bool remember);
    [PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
    [PreserveSig] int GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string file);
}
