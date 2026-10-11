using System.Runtime.InteropServices;
using System.Threading;

namespace CategoryBar.Interop;

/// <summary>
/// A live picture of the screen as it looks without the application's own overlay windows, for themes whose
/// surfaces bend or blur what lies behind them. The Magnification API renders the desktop with a list of
/// windows left out; a worker thread reads those pixels, softens them and hands each frame to its target.
/// Other programs' screenshots and recordings are not affected.
/// </summary>
internal static class ScreenBackdrop
{
    internal sealed class Target(Action<byte[], int, int> present)
    {
        /// <summary>The area to picture, in screen pixels; empty while there is nothing to draw.</summary>
        internal PixelRect Area;
        /// <summary>Blur radius in screen pixels.</summary>
        internal double Blur;
        internal readonly Action<byte[], int, int> Present = present;
        internal ulong Shown;
        /// <summary>Tick counts: when the next picture is due, and until when the desktop counts as moving.</summary>
        internal long Due, LivelyUntil;
    }

    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ImageHeader { public uint Width, Height; public Guid Format; public uint Stride, Offset; public UIntPtr Size; }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate bool Scaling(IntPtr window, IntPtr source, ImageHeader sourceHeader, IntPtr destination, ImageHeader destinationHeader, Bounds unclipped, Bounds clipped, IntPtr dirty);
    [DllImport("Magnification.dll")] private static extern bool MagInitialize();
    [DllImport("Magnification.dll")] private static extern bool MagSetWindowSource(IntPtr window, Bounds area);
    [DllImport("Magnification.dll")] private static extern bool MagSetImageScalingCallback(IntPtr window, Scaling callback);
    [DllImport("Magnification.dll")] private static extern bool MagSetWindowFilterList(IntPtr window, uint mode, int count, IntPtr[] windows);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateWindowEx(uint extended, string className, string? name, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr parameter);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);

    private static readonly object Gate = new();
    private static readonly List<Target> Targets = [];
    private static readonly HashSet<IntPtr> Excluded = [];
    private static bool excludedChanged = true, failed;
    private static Thread? worker;
    private static byte[] frame = [];
    private static int frameWidth, frameHeight, frameStride;

    /// <summary>False once the system has refused to deliver pictures; themes then fall back to plain translucency.</summary>
    internal static bool Available => !failed;

    /// <summary>Leaves a window of this application out of every picture, so a surface never shows itself.</summary>
    internal static void Exclude(IntPtr window, bool excluded)
    {
        lock (Gate) excludedChanged |= excluded ? Excluded.Add(window) : Excluded.Remove(window);
    }

    internal static Target Attach(Action<byte[], int, int> present)
    {
        var target = new Target(present);
        lock (Gate)
        {
            Targets.Add(target);
            if (worker is null && !failed)
            {
                worker = new Thread(Run) { IsBackground = true, Name = "Backdrop capture" };
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
            }
        }
        return target;
    }

    internal static void Detach(Target target) { lock (Gate) Targets.Remove(target); }

    private static void Run()
    {
        // The magnifier control lives in a hidden host window of this thread; it is only ever asked for pixels.
        var instance = GetModuleHandle(null);
        var host = MagInitialize() ? CreateWindowEx(0x00080080 /* layered tool window */, "Static", null, 0x80000000, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, instance, IntPtr.Zero) : IntPtr.Zero;
        var magnifier = host == IntPtr.Zero ? IntPtr.Zero : CreateWindowEx(0, "Magnifier", null, 0x50000000 /* visible child */, 0, 0, 0, 0, host, IntPtr.Zero, instance, IntPtr.Zero);
        Scaling receive = (_, source, header, _, _, _, _, _) =>
        {
            frameWidth = (int)header.Width; frameHeight = (int)header.Height; frameStride = (int)header.Stride;
            var length = frameStride * frameHeight;
            if (frame.Length < length) frame = new byte[length];
            Marshal.Copy(source + (int)header.Offset, frame, 0, length);
            return true;
        };
        if (magnifier == IntPtr.Zero || !MagSetImageScalingCallback(magnifier, receive)) { failed = true; return; }
        var misses = 0;
        while (true)
        {
            Target[] targets; IntPtr[]? filter = null;
            lock (Gate)
            {
                targets = Targets.Where(t => t.Area.Width > 0 && t.Area.Height > 0).ToArray();
                if (excludedChanged) { filter = [.. Excluded]; excludedChanged = false; }
            }
            if (filter is { Length: > 0 }) MagSetWindowFilterList(magnifier, 0 /* exclude */, filter.Length, filter);
            if (targets.Length == 0) { Thread.Sleep(80); continue; }
            var now = Environment.TickCount64;
            foreach (var target in targets)
            {
                if (now < target.Due) continue;
                var area = target.Area; var blur = target.Blur;
                // A still desktop is looked at a few times a second; anything that moves is followed smoothly.
                target.Due = now + (now < target.LivelyUntil ? 30 : 240);
                frameWidth = 0;
                Native.SetWindowPos(magnifier, IntPtr.Zero, area.Left, area.Top, area.Width, area.Height, Native.SwpNoActivate | Native.SwpNoZOrder);
                // Returns after the callback above has received this area of the composed desktop.
                if (!MagSetWindowSource(magnifier, new Bounds { Left = area.Left, Top = area.Top, Right = area.Right, Bottom = area.Bottom }) || frameWidth == 0)
                {
                    if (++misses > 40) { failed = true; return; }
                    continue;
                }
                misses = 0;
                // An unchanged desktop costs nothing further: no blur, no upload, no redraw.
                var stamp = Stamp(frame, frameStride * frameHeight) ^ (ulong)(blur * 16) ^ ((ulong)frameWidth << 40) ^ ((ulong)(uint)area.Left << 20) ^ (uint)area.Top;
                if (stamp == target.Shown) continue;
                target.Shown = stamp; target.LivelyUntil = now + 1200; target.Due = now + 30;
                var (pixels, width, height) = Soften(frame, frameWidth, frameHeight, frameStride, blur);
                target.Present(pixels, width, height);
            }
            Thread.Sleep(15);
        }
    }

    private static ulong Stamp(byte[] data, int length)
    {
        var hash = 14695981039346656037UL;
        var words = MemoryMarshal.Cast<byte, ulong>(data.AsSpan(0, length - length % 8));
        foreach (var word in words) hash = (hash ^ word) * 1099511628211UL;
        return hash;
    }

    /// <summary>Shrinks the picture and blurs it with three box passes; the result is a new opaque BGRA image.</summary>
    internal static (byte[] Pixels, int Width, int Height) Soften(byte[] source, int sourceWidth, int sourceHeight, int stride, double blur)
    {
        // A blurred picture carries no fine detail, so it is processed and uploaded at a fraction of its size.
        var step = blur >= 10 ? 4 : blur >= 3 ? 2 : 1;
        int width = Math.Max(1, sourceWidth / step), height = Math.Max(1, sourceHeight / step);
        var image = new int[width * height * 3];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                int b = 0, g = 0, r = 0;
                for (var dy = 0; dy < step; dy++)
                {
                    var at = (y * step + dy) * stride + x * step * 4;
                    for (var dx = 0; dx < step; dx++, at += 4) { b += source[at]; g += source[at + 1]; r += source[at + 2]; }
                }
                var to = (y * width + x) * 3; var count = step * step;
                image[to] = b / count; image[to + 1] = g / count; image[to + 2] = r / count;
            }
        var radius = (int)Math.Round(blur / step * .6);
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
        return (pixels, width, height);
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
