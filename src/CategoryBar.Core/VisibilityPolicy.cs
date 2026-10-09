namespace CategoryBar.Core;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

public readonly record struct TaskbarObservation(bool Exists, bool Visible, bool Cloaked, bool AutoHide,
    PixelRect Taskbar, PixelRect Monitor, bool FullscreenForeground, bool TaskbarAboveForeground);

public static class VisibilityPolicy
{
    public static bool ShouldShow(TaskbarObservation o)
    {
        if (!o.Exists || !o.Visible || o.Cloaked || o.Taskbar.Width <= 0 || o.Taskbar.Height <= 0) return false;
        var visibleHeight = Math.Min(o.Taskbar.Bottom, o.Monitor.Bottom) - Math.Max(o.Taskbar.Top, o.Monitor.Top);
        // Hidden auto-hide taskbars often leave a 1–2 physical pixel activation strip.
        if (visibleHeight <= 2) return false;
        if (o.FullscreenForeground && !o.TaskbarAboveForeground) return false;
        return true;
    }
}
