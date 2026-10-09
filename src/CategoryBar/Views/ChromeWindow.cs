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
    internal Grid TitleBar { get; } = new() { Height = 40 };

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
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 40, CornerRadius = new CornerRadius(0), GlassFrameThickness = new Thickness(1), ResizeBorderThickness = new Thickness(0), UseAeroCaptionButtons = false });

        var caption = Ui.Text(title, 12.5, "Text.Muted"); caption.Margin = new Thickness(14, 0, 0, 0);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 6, 0) };
        if (canMinimize) buttons.Children.Add(CaptionButton(Icons.Minimize, "Згорнути", () => WindowState = WindowState.Minimized));
        buttons.Children.Add(CaptionButton(Icons.Close, "Закрити", Close));
        TitleBar.Children.Add(caption); TitleBar.Children.Add(buttons);

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        layout.RowDefinitions.Add(new RowDefinition());
        layout.Children.Add(TitleBar); layout.Children.Add(Body.At(0, 1));
        layout.SetResourceReference(LayoutTransformProperty, "Scale");
        Content = new Border { BorderThickness = new Thickness(1), Child = layout }.Res(Border.BorderBrushProperty, "Surface.Border");

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
        if (handle != IntPtr.Zero) Native.StyleFrame(handle, Theme.IsDark);
    }

    /// <summary>Brings an already open window to the user instead of opening a second copy.</summary>
    internal void Surface()
    {
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Show();
        Activate();
    }
}
