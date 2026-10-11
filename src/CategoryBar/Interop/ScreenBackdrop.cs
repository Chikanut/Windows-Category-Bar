using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;

namespace CategoryBar.Interop;

/// <summary>
/// What a glass surface shows behind itself: the desktop wallpaper of its monitor, laid out the way Windows
/// lays it out and blurred. It is prepared once per wallpaper, monitor and blur, so glass costs nothing while
/// it is on screen. Like the system's Mica, it shows the wallpaper even where a window covers the desktop.
/// </summary>
internal static class ScreenBackdrop
{
    [ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitor);
        [return: MarshalAs(UnmanagedType.LPWStr)] string GetMonitorDevicePathAt(uint index);
        uint GetMonitorDevicePathCount();
        Native.Rect GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitor);
        void SetBackgroundColor(uint color);
        uint GetBackgroundColor();
        void SetPosition(int position);
        int GetPosition();
    }
    [ComImport, Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")] private class DesktopWallpaper;

    private static readonly Dictionary<string, BitmapSource> Prepared = [];

    /// <summary>The blurred desktop of a monitor, or null when Windows cannot tell what its wallpaper is.</summary>
    internal static BitmapSource? For(PixelRect monitor, double blur)
    {
        if (monitor.Width <= 0 || monitor.Height <= 0) return null;
        try
        {
            var (path, position, background) = Wallpaper(monitor);
            var file = path.Length > 0 && File.Exists(path) ? new FileInfo(path) : null;
            // Less blur keeps more detail, so the picture is kept larger.
            var shrink = blur < 4 ? 2 : blur < 12 ? 4 : 8;
            var key = string.Join('|', path, file?.LastWriteTimeUtc.Ticks, file?.Length, position, background, monitor, Math.Round(blur), shrink);
            if (Prepared.TryGetValue(key, out var ready)) return ready;
            if (Prepared.Count > 12) Prepared.Clear();

            int width = Math.Max(1, monitor.Width / shrink), height = Math.Max(1, monitor.Height / shrink);
            var visual = new DrawingVisual();
            using (var drawing = visual.RenderOpen())
            {
                drawing.DrawRectangle(new SolidColorBrush(background), null, new System.Windows.Rect(0, 0, width, height));
                if (file is not null)
                {
                    // Only the header is read here: centring and tiling need the picture's real size.
                    var original = BitmapFrame.Create(new Uri(file.FullName), BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                    Lay(drawing, Load(file.FullName, Math.Min(original.PixelWidth, width * 2)), position, width, height,
                        new Size((double)original.PixelWidth / shrink, (double)original.PixelHeight / shrink));
                }
            }
            var desktop = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            desktop.Render(visual);
            var raw = new byte[width * height * 4];
            desktop.CopyPixels(raw, width * 4, 0);
            var pixels = Soften(raw, width, height, width * 4, blur / shrink);
            var picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgr32, null, pixels, width * 4);
            picture.Freeze();
            return Prepared[key] = picture;
        }
        // Any failure here only means plain translucent glass instead of a picture.
        catch (Exception ex) when (ex is COMException or IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException or ArgumentException or InvalidCastException or FileFormatException)
        {
            return null;
        }
    }

    private static (string Path, int Position, Color Background) Wallpaper(PixelRect monitor)
    {
        var desktop = (IDesktopWallpaper)new DesktopWallpaper();
        try
        {
            // Each monitor may carry its own picture.
            string? device = null;
            for (uint i = 0, count = desktop.GetMonitorDevicePathCount(); i < count; i++)
            {
                var candidate = desktop.GetMonitorDevicePathAt(i);
                try { if (desktop.GetMonitorRECT(candidate).Pixels == monitor) { device = candidate; break; } }
                catch (COMException) { /* a detached monitor is still listed */ }
            }
            var color = desktop.GetBackgroundColor();
            return (desktop.GetWallpaper(device) ?? "", desktop.GetPosition(), Color.FromRgb((byte)color, (byte)(color >> 8), (byte)(color >> 16)));
        }
        finally { Marshal.ReleaseComObject(desktop); }
    }

    private static BitmapSource Load(string path, int width)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;   // the file is not kept open
        image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
        image.DecodePixelWidth = width;
        image.UriSource = new Uri(path);
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>Draws the wallpaper as Windows positions it: centre, tile, stretch, fit, fill or span.</summary>
    private static void Lay(DrawingContext drawing, BitmapSource image, int position, double width, double height, Size natural)
    {
        double ratio = (double)image.PixelWidth / image.PixelHeight, screen = width / height;
        System.Windows.Rect Centered(double w, double h) => new((width - w) / 2, (height - h) / 2, w, h);
        drawing.PushClip(new RectangleGeometry(new System.Windows.Rect(0, 0, width, height)));
        switch (position)
        {
            case 0: drawing.DrawImage(image, Centered(natural.Width, natural.Height)); break;
            case 1:
                var tile = new ImageBrush(image) { TileMode = TileMode.Tile, ViewportUnits = BrushMappingMode.Absolute, Viewport = new System.Windows.Rect(0, 0, natural.Width, natural.Height) };
                drawing.DrawRectangle(tile, null, new System.Windows.Rect(0, 0, width, height));
                break;
            case 2: drawing.DrawImage(image, new System.Windows.Rect(0, 0, width, height)); break;
            case 3: drawing.DrawImage(image, ratio > screen ? Centered(width, width / ratio) : Centered(height * ratio, height)); break;
            default: drawing.DrawImage(image, ratio > screen ? Centered(height * ratio, height) : Centered(width, width / ratio)); break;
        }
        drawing.Pop();
    }

    /// <summary>Shrinks the picture and blurs it with three box passes; the result is a new opaque BGRA image.</summary>
    internal static (byte[] Pixels, int Width, int Height) SoftenAndShrink(byte[] source, int sourceWidth, int sourceHeight, int stride, double blur)
    {
        var step = blur >= 10 ? 4 : blur >= 3 ? 2 : 1;
        int width = Math.Max(1, sourceWidth / step), height = Math.Max(1, sourceHeight / step);
        var small = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0;
                for (var dy = 0; dy < step; dy++)
                {
                    var at = (y * step + dy) * stride + x * step * 4;
                    for (var dx = 0; dx < step; dx++, at += 4) { b += source[at]; g += source[at + 1]; r += source[at + 2]; }
                }
                var to = (y * width + x) * 4; var count = step * step;
                small[to] = (byte)(b / count); small[to + 1] = (byte)(g / count); small[to + 2] = (byte)(r / count); small[to + 3] = 255;
            }
        return (Soften(small, width, height, width * 4, blur / step), width, height);
    }

    /// <summary>Blurs a BGRA picture with three box passes of about <paramref name="blur"/> pixels; the result is opaque.</summary>
    internal static byte[] Soften(byte[] source, int width, int height, int stride, double blur)
    {
        var image = new int[width * height * 3];
        for (var y = 0; y < height; y++)
            for (int x = 0, at = y * stride, to = y * width * 3; x < width; x++, at += 4, to += 3)
            {
                image[to] = source[at]; image[to + 1] = source[at + 1]; image[to + 2] = source[at + 2];
            }
        var radius = (int)Math.Round(blur * .6);
        if (radius > 0)
        {
            var scratch = new int[image.Length];
            for (var pass = 0; pass < 3; pass++)
            {
                Box(image, scratch, width, height, radius, horizontal: true);
                Box(scratch, image, width, height, radius, horizontal: false);
            }
        }
        var pixels = new byte[width * height * 4];
        for (int i = 0, o = 0; i < image.Length; i += 3, o += 4)
        {
            pixels[o] = (byte)image[i]; pixels[o + 1] = (byte)image[i + 1]; pixels[o + 2] = (byte)image[i + 2]; pixels[o + 3] = 255;
        }
        return pixels;
    }

    private static void Box(int[] from, int[] to, int width, int height, int radius, bool horizontal)
    {
        var (lines, length, along, across) = horizontal ? (height, width, 3, width * 3) : (width, height, width * 3, 3);
        var window = radius * 2 + 1;
        for (var line = 0; line < lines; line++)
        {
            var start = line * across;
            for (var channel = 0; channel < 3; channel++)
            {
                // The edge pixel repeats beyond the border, so the blur does not darken towards the rim.
                var sum = 0;
                for (var i = -radius; i <= radius; i++) sum += from[start + Math.Clamp(i, 0, length - 1) * along + channel];
                for (var i = 0; i < length; i++)
                {
                    to[start + i * along + channel] = sum / window;
                    sum += from[start + Math.Min(i + radius + 1, length - 1) * along + channel] - from[start + Math.Max(i - radius, 0) * along + channel];
                }
            }
        }
    }
}
