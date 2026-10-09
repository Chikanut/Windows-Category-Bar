using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CategoryBar.Interop;

namespace CategoryBar.Services;

/// <summary>Loads application icons off the UI thread and keeps them for the lifetime of the process.</summary>
internal static class IconCache
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<Action<ImageSource?>>> Pending = new(StringComparer.OrdinalIgnoreCase);
    private static Guid imageFactoryId = typeof(IShellItemImageFactory).GUID;

    /// <summary>Icons are requested in physical pixels; 48 covers a 24-unit icon up to 200% scaling.</summary>
    internal const int Pixels = 48;

    internal static void Forget(string target) => Cache.TryRemove(target, out _);

    /// <summary>Calls back on the UI thread; synchronously when the icon is already cached.</summary>
    internal static void Load(string target, Action<ImageSource?> ready)
    {
        if (Cache.TryGetValue(target, out var cached)) { ready(cached); return; }
        lock (Pending)
        {
            if (Pending.TryGetValue(target, out var waiting)) { waiting.Add(ready); return; }
            Pending[target] = [ready];
        }
        var dispatcher = Dispatcher.CurrentDispatcher;
        ShellWorker.Run(() =>
        {
            ImageSource? image = null;
            try { image = Extract(target); } catch (Exception ex) when (ex is COMException or IOException or ArgumentException or UnauthorizedAccessException) { }
            Cache[target] = image;
            List<Action<ImageSource?>> callbacks;
            lock (Pending) { callbacks = Pending[target]; Pending.Remove(target); }
            dispatcher.BeginInvoke(() => { foreach (var callback in callbacks) callback(image); });
            return true;
        });
    }

    private static ImageSource? Extract(string target)
    {
        if (LaunchTarget.IsLink(target)) return null;
        var path = target;
        if (Shortcuts.IsLinkFile(target) && File.Exists(target))
        {
            var link = Shortcuts.ReadLink(target);
            if (link.IconPath is { Length: > 0 } && File.Exists(link.IconPath) && FromIconFile(link.IconPath, link.IconIndex) is { } custom) return custom;
            if (link.Target is { Length: > 0 } && (File.Exists(link.Target) || Directory.Exists(link.Target))) path = link.Target;
        }
        return FromShellItem(path) ?? FromFileInfo(path);
    }

    private static ImageSource? FromIconFile(string file, int index)
    {
        var icons = new IntPtr[1];
        if (Native.PrivateExtractIcons(file, index, Pixels, Pixels, icons, null, 1, 0) == 0 || icons[0] == IntPtr.Zero) return null;
        return FromIconHandle(icons[0]);
    }

    private static ImageSource? FromIconHandle(IntPtr icon)
    {
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally { Native.DestroyIcon(icon); }
    }

    private static ImageSource? FromFileInfo(string path)
    {
        var info = new Native.ShFileInfo();
        if (Native.SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<Native.ShFileInfo>(), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
        return FromIconHandle(info.Icon);
    }

    private static ImageSource? FromShellItem(string path)
    {
        if (Native.SHCreateItemFromParsingName(path, IntPtr.Zero, ref imageFactoryId, out var factory) != 0 || factory is null) return null;
        var bitmap = IntPtr.Zero;
        try
        {
            if (factory.GetImage(new Native.Size { Width = Pixels, Height = Pixels }, 0x4 /* SIIGBF_ICONONLY */, out bitmap) != 0) return null;
            // Read the pixels directly: the interop helper in WPF discards the alpha channel of shell bitmaps.
            var header = new Native.BitmapInfoHeader { Size = Marshal.SizeOf<Native.BitmapInfoHeader>(), Width = Pixels, Height = -Pixels, Planes = 1, BitCount = 32 };
            var pixels = new byte[Pixels * Pixels * 4];
            var dc = Native.GetDC(IntPtr.Zero);
            try { if (Native.GetDIBits(dc, bitmap, 0, Pixels, pixels, ref header, 0) == 0) return null; }
            finally { _ = Native.ReleaseDC(IntPtr.Zero, dc); }
            var hasAlpha = false;
            for (var i = 3; i < pixels.Length; i += 4) if (pixels[i] != 0) { hasAlpha = true; break; }
            var image = BitmapSource.Create(Pixels, Pixels, 96, 96, hasAlpha ? PixelFormats.Pbgra32 : PixelFormats.Bgr32, null, pixels, Pixels * 4);
            image.Freeze();
            return image;
        }
        finally
        {
            if (bitmap != IntPtr.Zero) Native.DeleteObject(bitmap);
            Marshal.FinalReleaseComObject(factory);
        }
    }
}
