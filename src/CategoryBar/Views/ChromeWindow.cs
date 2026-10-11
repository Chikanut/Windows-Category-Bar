using System.Windows.Interop;
using System.Windows.Shell;
using CategoryBar.Interop;

namespace CategoryBar.Views;

/// <summary>
/// A regular window with its own title bar drawn in the application theme. The window background is
/// themed from the first frame, so nothing light shows through behind the content.
/// </summary>
internal class ChromeWindow : Window
{
    private readonly double baseWidth, baseHeight;
    internal Grid Body { get; } = new();
    internal Theming.ThemeSurface TitleBar { get; } = new Theming.ThemeSurface().Res(StyleProperty, "Theme.Caption");

    internal ChromeWindow(string title, double width, double height, bool canMinimize = false)
    {
        baseWidth = width; baseHeight = height;
        Title = title;
        WindowStyle = WindowStyle.None;
        ResizeMode = canMinimize ? ResizeMode.CanMinimize : ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        UseLayoutRounding = true; SnapsToDevicePixels = true;
        this.Res(BackgroundProperty, "Window.Bg").Res(ForegroundProperty, "Text").Res(FontFamilyProperty, "Font");
        FontSize = 13;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(1), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });
        TitleBar.Background = Brushes.Transparent;
        // The header is a movable template slot, so drag hit testing follows its actual position.
        TitleBar.MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) { DragMove(); e.Handled = true; } };

        var caption = Ui.Text(title, 12.5, "Text.Muted");
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        if (canMinimize) buttons.Children.Add(CaptionButton(Icons.Minimize, L.T("Згорнути"), () => WindowState = WindowState.Minimized));
        buttons.Children.Add(CaptionButton(Icons.Close, L.T("Закрити"), Close));
        TitleBar.Header = caption; TitleBar.Content = buttons;

        var shell = new Theming.ThemeSurface { Header = TitleBar, Content = Body }.Res(StyleProperty, "Theme.Window");
        shell.SetResourceReference(LayoutTransformProperty, "Scale");
        Content = shell;
        Loaded += (_, _) => Theming.ThemeMotion.Play(shell, "Theme.Window.Enter", offset: 4);

        ApplyTheme();
        Theme.Changed += ApplyTheme;
        Closed += (_, _) => Theme.Changed -= ApplyTheme;
        SourceInitialized += (_, _) => ApplyTheme();
        PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape && !e.Handled && CloseOnEscape) { e.Handled = true; Close(); } };
    }

    protected virtual bool CloseOnEscape => true;

    private Button CaptionButton(string icon, string tip, Action click)
    {
        var button = Ui.IconButton(icon, tip, click);
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    private void ApplyTheme()
    {
        Width = baseWidth * Theme.Scale; Height = baseHeight * Theme.Scale;
        TextOptions.SetTextFormattingMode(this, Theme.Scale == 1 ? TextFormattingMode.Display : TextFormattingMode.Ideal);
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            Native.StyleFrame(handle, Theme.IsDark, Theme.StyleSheet.Number("window", "border-radius", 8) > 0);
            var backdrop = Native.ApplyBackdrop(handle, Theme.TransparencyEnabled ? Theme.Current.Backdrop : "None");
            if (HwndSource.FromHwnd(handle)?.CompositionTarget is { } target)
                target.BackgroundColor = backdrop ? Colors.Transparent : Theme.WindowBaseColor;
            if (backdrop) Background = Brushes.Transparent;
            else this.Res(BackgroundProperty, "Window.Bg");
        }
    }

    /// <summary>Brings an already open window to the user instead of opening a second copy.</summary>
    internal void Surface()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }
}
