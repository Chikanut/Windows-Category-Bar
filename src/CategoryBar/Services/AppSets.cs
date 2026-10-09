using System.Runtime.InteropServices;
using CategoryBar.Interop;

namespace CategoryBar.Services;

/// <summary>Remembering and restoring window positions for the applications of a set.</summary>
internal static class AppSets
{
    /// <summary>The window's current rectangle, or null for a minimized window, which has none to remember.</summary>
    internal static WindowPlacement? Capture(IntPtr window)
    {
        if (window == IntPtr.Zero || !Native.IsWindow(window) || Native.IsIconic(window) || !Native.GetWindowRect(window, out var rect)) return null;
        var (width, height) = (rect.Right - rect.Left, rect.Bottom - rect.Top);
        if (width <= 0 || height <= 0) return null;
        return new WindowPlacement { Left = rect.Left, Top = rect.Top, Width = width, Height = height, Maximized = Native.IsZoomed(window) };
    }

    /// <summary>Requests are posted, so a busy or elevated application cannot stall the bar.</summary>
    private static void Apply(IntPtr window, WindowPlacement placement)
    {
        if (!Native.IsWindow(window)) return;
        if (Native.IsIconic(window) || Native.IsZoomed(window)) Native.ShowWindowAsync(window, 1); // SW_SHOWNORMAL
        Native.SetWindowPos(window, IntPtr.Zero, placement.Left, placement.Top, placement.Width, placement.Height,
            Native.SwpNoZOrder | Native.SwpNoActivate | Native.SwpAsync);
        // A maximized window is first moved to its rectangle so that it maximizes on the right monitor.
        if (placement.Maximized) Native.ShowWindowAsync(window, 3); // SW_MAXIMIZE
    }

    /// <summary>
    /// Waits for the applications' windows and puts each where it was remembered. A window that replaces an
    /// earlier one (a splash screen, a launcher) is placed again.
    /// </summary>
    internal static async Task Arrange(IEnumerable<Shortcut> steps)
    {
        var items = steps.Where(s => s.Placement is not null).ToList();
        if (items.Count == 0) return;
        var placed = new Dictionary<Guid, IntPtr>();
        var deadline = DateTime.UtcNow.AddSeconds(30);
        var settledAt = DateTime.MaxValue;
        try
        {
            while (DateTime.UtcNow < deadline && DateTime.UtcNow < settledAt)
            {
                var (running, _) = await ShellWorker.Run(() => AppStatus.Inspect(items));
                foreach (var step in items)
                {
                    if (!running.TryGetValue(step.Id, out var window) || placed.TryGetValue(step.Id, out var known) && known == window) continue;
                    Apply(window, step.Placement!);
                    placed[step.Id] = window;
                    settledAt = DateTime.MaxValue;
                }
                if (placed.Count == items.Count && settledAt == DateTime.MaxValue) settledAt = DateTime.UtcNow.AddSeconds(4);
                await Task.Delay(500);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or COMException) { }
    }
}
