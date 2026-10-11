using System.Windows.Interop;
using CategoryBar.Interop;

namespace CategoryBar.Views;

/// <summary>
/// Base for the bar, its menus and toasts: a transparent, always-on-top window that never takes focus
/// by itself and is positioned in physical pixels next to the Windows taskbar.
/// </summary>
internal abstract class OverlayWindow : Window
{
    private readonly Canvas canvas = new();
    protected IntPtr Handle { get; private set; }
    protected FrameworkElement Root { get; private set; } = new Grid();

    protected OverlayWindow()
    {
        WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = Brushes.Transparent;
        ShowInTaskbar = false; Topmost = true; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.Manual; Width = 10; Height = 10;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        this.Res(FontFamilyProperty, "Font");
        Content = canvas;
        Handle = new WindowInteropHelper(this).EnsureHandle();
        Native.SetExStyle(Handle, Native.ExToolWindow | Native.ExNoActivate);
        HwndSource.FromHwnd(Handle)?.AddHook(Hook);
        ApplyTextMode();
        Theme.Changed += ApplyTextMode;
        Closed += (_, _) => Theme.Changed -= ApplyTextMode;
        // No overlay of this application may appear in the picture another overlay shows behind itself.
        ScreenBackdrop.Exclude(Handle, true);
        Closed += (_, _) => ScreenBackdrop.Exclude(Handle, false);
    }

    // ---- Live backdrop ---------------------------------------------------------------------------
    // A theme may ask (".bar { backdrop: live }") for the picture of the screen behind a surface. The window
    // then publishes it as its own "Theme.Backdrop" brush, which the theme's material shader bends and tints.

    private readonly System.Windows.Threading.DispatcherTimer backdropClock = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private ScreenBackdrop.Target? backdrop;
    private FrameworkElement? glass;
    private string glassSelector = "";
    private System.Windows.Media.Imaging.WriteableBitmap? picture;
    private double? bleed;

    /// <summary>Names the surface that may show the screen behind it and the stylesheet selector that decides.</summary>
    protected void UseBackdrop(FrameworkElement surface, string selector)
    {
        glass = surface; glassSelector = selector;
        backdropClock.Tick += (_, _) => TrackBackdrop();
        IsVisibleChanged += (_, _) => SyncBackdrop();
        Theme.Changed += SyncBackdrop;
        Closed += (_, _) => { Theme.Changed -= SyncBackdrop; StopBackdrop(); };
    }

    /// <summary>For windows that rebuild their surface: the new element takes over the backdrop.</summary>
    protected void MoveBackdrop(FrameworkElement surface) { glass = surface; bleed = null; }

    private void SyncBackdrop()
    {
        bleed = null;
        var wanted = IsVisible && Theme.TransparencyEnabled && ScreenBackdrop.Available && TryFindResource(glassSelector + ".Backdrop") is true;
        if (!wanted) { StopBackdrop(); return; }
        if (backdrop is not null) return;
        backdrop = ScreenBackdrop.Attach(ShowBackdrop);
        backdropClock.Start();
        TrackBackdrop();
    }

    private void StopBackdrop()
    {
        backdropClock.Stop();
        if (backdrop is null) return;
        ScreenBackdrop.Detach(backdrop);
        backdrop = null; picture = null;
        Resources.Remove("Theme.Backdrop"); Resources.Remove("Theme.Backdrop.Enabled");
    }

    /// <summary>Keeps the pictured area on the surface as it moves, grows or plays a transition.</summary>
    private void TrackBackdrop()
    {
        if (backdrop is null || glass is null) return;
        if (!ScreenBackdrop.Available) { StopBackdrop(); return; }
        if (!glass.IsVisible || glass.ActualWidth < 1 || PresentationSource.FromVisual(glass) is null) { backdrop.Area = default; return; }
        // The shader works on the surface plus the transparent rim its effect adds around it.
        bleed ??= Effects(glass, 5).OfType<Theming.ThemeShaderEffect>().FirstOrDefault()?.Bleed ?? 0;
        var rim = (int)Math.Round(bleed.Value * Dpi * Theme.Scale);
        var area = ScreenRect(glass);
        backdrop.Blur = TryFindResource(glassSelector + ".BackdropBlur") is double blur ? blur * Dpi * Theme.Scale : 0;
        backdrop.Area = new(area.Left - rim, area.Top - rim, area.Right + rim, area.Bottom + rim);
    }

    private static IEnumerable<System.Windows.Media.Effects.Effect> Effects(DependencyObject root, int depth)
    {
        if (root is UIElement { Effect: { } effect }) yield return effect;
        if (depth == 0) yield break;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            foreach (var nested in Effects(VisualTreeHelper.GetChild(root, i), depth - 1)) yield return nested;
    }

    /// <summary>Called on the capture thread with a new frame.</summary>
    private void ShowBackdrop(byte[] pixels, int width, int height)
    {
        var target = backdrop;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, () =>
        {
            if (backdrop is null || backdrop != target) return;
            if (picture is null || picture.PixelWidth != width || picture.PixelHeight != height)
            {
                picture = new System.Windows.Media.Imaging.WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
                Resources["Theme.Backdrop"] = new ImageBrush(picture) { Stretch = Stretch.Fill };
            }
            picture.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
            // Changing a resource re-evaluates the whole window, so it is only done when the picture first arrives.
            if (Resources["Theme.Backdrop.Enabled"] is not 1d) Resources["Theme.Backdrop.Enabled"] = 1d;
        });
    }

    private void ApplyTextMode() =>
        TextOptions.SetTextFormattingMode(this, Theme.Scale == 1 ? TextFormattingMode.Display : TextFormattingMode.Ideal);

    /// <summary>The canvas lets the content take its natural size; the window is then sized to it.</summary>
    protected void SetRoot(FrameworkElement root)
    {
        Root = root.Res(LayoutTransformProperty, "Scale");
        canvas.Children.Clear();
        canvas.Children.Add(root);
    }

    protected double Dpi => Math.Max(96, Native.GetDpiForWindow(Handle)) / 96.0;

    protected (int Width, int Height) MeasurePixels()
    {
        // Children invalidated since the last pass only reach the root once the layout queue is flushed.
        Root.UpdateLayout();
        Root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return ((int)Math.Ceiling(Root.DesiredSize.Width * Dpi), (int)Math.Ceiling(Root.DesiredSize.Height * Dpi));
    }

    protected void Place(int x, int y, int width, int height) =>
        Native.SetWindowPos(Handle, Native.Topmost, x, y, width, height, Native.SwpNoActivate);

    internal PixelRect Bounds => Native.GetWindowRect(Handle, out var rect) ? rect.Pixels : default;

    internal bool ContainsCursor(int margin = 0)
    {
        if (!IsVisible) return false;
        var cursor = Native.Cursor(); var b = Bounds;
        return cursor.X >= b.Left - margin && cursor.X < b.Right + margin && cursor.Y >= b.Top - margin && cursor.Y < b.Bottom + margin;
    }

    /// <summary>Screen rectangle of an element inside this window, in physical pixels.</summary>
    internal PixelRect ScreenRect(FrameworkElement element)
    {
        var topLeft = element.PointToScreen(new Point(0, 0));
        var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));
        return new((int)topLeft.X, (int)topLeft.Y, (int)bottomRight.X, (int)bottomRight.Y);
    }

    private IntPtr previousForeground;
    /// <summary>True while focus is being handed back, so that losing it is not mistaken for a click elsewhere.</summary>
    protected bool ReleasingFocus { get; private set; }
    /// <summary>Whether the window still needs the keyboard after a context menu closes.</summary>
    protected virtual bool NeedsKeyboard => false;

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Clicking the bar or a menu must not take the keyboard away from the application the user works in.
        if (message == 0x21 /* WM_MOUSEACTIVATE */) { handled = true; return new IntPtr(3 /* MA_NOACTIVATE */); }
        return IntPtr.Zero;
    }

    /// <summary>Takes keyboard focus for typing or for a menu that must close on an outside click.</summary>
    internal void TakeFocus()
    {
        var foreground = Native.GetForegroundWindow();
        if (foreground != Handle && foreground != IntPtr.Zero)
        {
            _ = Native.GetWindowThreadProcessId(foreground, out var owner);
            if (owner != Environment.ProcessId) previousForeground = foreground;
        }
        Native.ForceForeground(Handle);
        Activate();
    }

    /// <summary>Gives the keyboard back to the application that had it before <see cref="TakeFocus"/>.</summary>
    internal void ReleaseFocus()
    {
        var previous = previousForeground;
        previousForeground = IntPtr.Zero;
        if (previous == IntPtr.Zero || Native.GetForegroundWindow() != Handle || !Native.IsWindow(previous)) return;
        ReleasingFocus = true;
        try { Native.SetForegroundWindow(previous); }
        finally { Dispatcher.BeginInvoke(() => ReleasingFocus = false); }
    }

    /// <summary>Puts the caret into a text box of this window, taking the keyboard from the active application.</summary>
    internal void FocusInput(TextBox box, bool selectAll = true)
    {
        TakeFocus();
        // Focus can only be given once the box is part of the laid-out tree and the window is active.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Input, () =>
        {
            UpdateLayout();
            box.Focus();
            Keyboard.Focus(box);
            if (selectAll) box.SelectAll();
        });
    }

    /// <summary>Opens a context menu from a window that normally never activates, so it closes on an outside click.</summary>
    internal void OpenMenu(ContextMenu menu, UIElement? target = null)
    {
        TakeFocus();
        menu.PlacementTarget = target ?? this;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.Closed += (_, _) => Dispatcher.BeginInvoke(() => { if (!NeedsKeyboard) ReleaseFocus(); });
        menu.IsOpen = true;
    }
}
