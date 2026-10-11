using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CategoryBar.Views;

/// <summary>A single notification card above the right end of the taskbar, with optional actions.</summary>
internal sealed class ToastWindow : OverlayWindow
{
    private const int ShadowMargin = 20;
    private readonly DispatcherTimer life = new();
    private Border progress = new();
    private bool sticky;

    internal ToastWindow()
    {
        life.Tick += (_, _) => Dismiss();
        MouseEnter += (_, _) => { life.Stop(); progress.BeginAnimation(WidthProperty, null); progress.Width = 340; };
        MouseLeave += (_, _) => StartLife();
    }

    internal void Show(ToastRequest request, PixelRect monitor, int bottom)
    {
        sticky = request.Sticky;
        var (glyph, tone) = request.Kind switch
        {
            ToastKind.Success => (Icons.Check, "Success"), ToastKind.Warning => (Icons.Warning, "Warning"),
            ToastKind.Error => (Icons.Close, "Danger"), _ => (Icons.Info, "Accent")
        };
        var badge = new Grid { Width = 28, Height = 28, VerticalAlignment = VerticalAlignment.Top };
        badge.Children.Add(new Border { CornerRadius = new CornerRadius(14), Opacity = .18 }.Res(Border.BackgroundProperty, tone));
        badge.Children.Add(Ui.Icon(glyph, tone, 14, 1.8));

        var text = new StackPanel { Margin = new Thickness(12, 0, 8, 0) };
        text.Children.Add(Ui.Text(request.Title, 14, "Text", bold: true, wrap: true));
        if (request.Body.Length > 0)
        {
            var body = Ui.Text(request.Body, 12.5, "Text.Muted", wrap: true);
            body.Margin = new Thickness(0, 3, 0, 0); body.LineHeight = 18;
            text.Children.Add(body);
        }
        if (request.Actions.Length > 0)
        {
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var action in request.Actions)
            {
                var button = Ui.Button(action.Label, () => { Dismiss(); action.Run(); });
                button.MinHeight = 26; button.FontSize = 12; button.Padding = new Thickness(10, 0, 10, 0); button.Margin = new Thickness(0, 0, 8, 0);
                button.Focusable = false;
                actions.Children.Add(button);
            }
            text.Children.Add(actions);
        }
        var close = Ui.IconButton(Icons.Close, L.T("Закрити"), Dismiss, "Text.Faint");
        close.Width = close.Height = close.MinHeight = 22; close.VerticalAlignment = VerticalAlignment.Top; close.Margin = new Thickness(0, -3, -4, 0);

        var layout = new Grid { Margin = new Thickness(14, 12, 14, 12) };
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.Children.Add(badge); layout.Children.Add(text.At(1)); layout.Children.Add(close.At(2));

        progress = new Border { Height = 2, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom, CornerRadius = new CornerRadius(1) }.Res(Border.BackgroundProperty, "Accent");
        var card = new Grid { Width = 340, Margin = new Thickness(ShadowMargin) };
        card.Children.Add(new Border().Res(Border.BackgroundProperty, "Popup.Bg").Res(Border.CornerRadiusProperty, "Radius").Res(EffectProperty, "Shadow"));
        var surface = new ContentControl { Content = new Grid { Children = { layout, progress } } }.Res(StyleProperty, "Theme.Toast");
        card.Children.Add(surface);
        progress.Visibility = sticky ? Visibility.Collapsed : Visibility.Visible;
        progress.Margin = new Thickness(10, 0, 10, 0);
        SetRoot(card);

        var (width, height) = MeasurePixels();
        var inset = (int)((ShadowMargin - 14) * Dpi * Theme.Scale);
        Place(monitor.Right - width + inset, bottom - height + inset, width, height);
        if (!IsVisible) Show();
        Ui.FadeIn(card, 0, 10, 180, "Theme.Toast.Enter");
        StartLife();
    }

    private void StartLife()
    {
        life.Stop();
        if (sticky) return;
        life.Interval = TimeSpan.FromSeconds(4.5);
        life.Start();
        progress.BeginAnimation(WidthProperty, new DoubleAnimation(320, 0, life.Interval));
    }

    internal void Dismiss()
    {
        life.Stop();
        if (!IsVisible) return;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(140));
        fade.Completed += (_, _) => { if (Root.Opacity < .05) Hide(); };
        Root.BeginAnimation(OpacityProperty, fade);
    }
}
