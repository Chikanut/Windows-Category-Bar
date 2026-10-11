using System.Windows.Media.Animation;
using System.Windows.Shapes;
using CategoryBar.Services;
using IconPath = System.Windows.Shapes.Path;

namespace CategoryBar;

/// <summary>Line icons drawn on a 16×16 grid, matching the design reference.</summary>
internal static class Icons
{
    internal const string Plus = "M8 3v10M3 8h10";
    internal const string Search = "M11.5 7a4.5 4.5 0 1 1-9 0a4.5 4.5 0 0 1 9 0zM10.5 10.5L14 14";
    internal const string Warning = "M8 2L15 14H1zM8 6.5v3.2M8 11.6v.3";
    internal const string Shield = "M8 1.5L13.5 3.5v4c0 3.2-2.3 5.6-5.5 7c-3.2-1.4-5.5-3.8-5.5-7v-4z";
    internal const string ChevronUp = "M4 10l4-4l4 4";
    internal const string ChevronDown = "M4 6l4 4l4-4";
    internal const string ChevronRight = "M6 4l4 4l-4 4";
    internal const string ChevronLeft = "M10 4l-4 4l4 4";
    internal const string Close = "M4 4l8 8M12 4l-8 8";
    internal const string Check = "M3 8.5l3.2 3L13 5";
    internal const string Pencil = "M3 13l.6-2.8L10.9 2.9a1.2 1.2 0 0 1 1.7 0l.5.5a1.2 1.2 0 0 1 0 1.7L5.8 12.4zM9.8 4l2.2 2.2";
    internal const string Sliders = "M2 5h6M12 5h2M2 11h2M8 11h6M10 3v4M6 9v4";
    internal const string Trash = "M3 4.5h10M6.5 4.5V3h3v1.5M4.5 4.5l.6 8.5h5.8l.6-8.5M7 7v4M9 7v4";
    internal const string Folder = "M2 4.5a1 1 0 0 1 1-1h3l1.5 1.5H13a1 1 0 0 1 1 1V12a1 1 0 0 1-1 1H3a1 1 0 0 1-1-1z";
    internal const string Globe = "M14 8A6 6 0 1 1 2 8a6 6 0 0 1 12 0zM2 8h12M8 2c2 2 2 10 0 12M8 2c-2 2-2 10 0 12";
    internal const string Undo = "M4 7h6a3 3 0 0 1 0 6H7M4 7l3-3M4 7l3 3";
    internal const string Play = "M5 3.5l7 4.5l-7 4.5z";
    internal const string Apps = "M3 3h4v4H3zM9 3h4v4H9zM3 9h4v4H3zM9 9h4v4H9z";
    internal const string Palette = "M8 2a6 6 0 1 0 0 12c1 0 1.5-.7 1.2-1.5c-.4-1 .3-2 1.4-2H12a2 2 0 0 0 2-2c0-3.6-2.7-6.5-6-6.5zM5 8h.01M6.5 5.2h.01M9.8 5h.01";
    internal const string Info = "M14 8A6 6 0 1 1 2 8a6 6 0 0 1 12 0zM8 7.2v3.6M8 5v.3";
    internal const string Layout = "M2 3.5h12v9H2zM2 10h12";
    internal const string Home = "M2.5 7.5L8 3l5.5 4.5V13h-4V9.5h-3V13h-4z";
    internal const string Import = "M3 11.5h10v2H3zM8 2v6.5M5.5 6L8 8.5L10.5 6";
    internal const string External = "M9 3h4v4M13 3L7.5 8.5M11 9v3.5H3.5V5H7";
    internal const string Move = "M3 8h10M10 5l3 3l-3 3";
    internal const string Sun = "M8 5a3 3 0 1 0 0 6a3 3 0 0 0 0-6zM8 1.5v1.5M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1 1M11.6 11.6l1 1M3.4 12.6l1-1M11.6 4.4l1-1";
    internal const string Moon = "M13 9.5A5.5 5.5 0 0 1 6.5 3a5.5 5.5 0 1 0 6.5 6.5z";
    internal const string Sparkle = "M8 2l1.5 4.5L14 8l-4.5 1.5L8 14l-1.5-4.5L2 8l4.5-1.5z";
    internal const string Square = "M3 3h10v10H3z";
    internal const string Grip = "M6 4h.01M10 4h.01M6 8h.01M10 8h.01M6 12h.01M10 12h.01";
    internal const string More = "M3.5 8h.01M8 8h.01M12.5 8h.01";
    internal const string Windows = "M2 2h5.5v5.5H2zM8.5 2H14v5.5H8.5zM2 8.5h5.5V14H2zM8.5 8.5H14V14H8.5z";
    internal const string Minimize = "M4 8h8";
}

internal static class Ui
{
    private static readonly string[] TileColors = ["#5B8DEF", "#3FB8AF", "#E2793B", "#B48CF2", "#5CC98A", "#E667AF", "#D9A441", "#6C7BD9", "#4FA7D9", "#C96B6B"];

    internal static T Res<T>(this T element, DependencyProperty property, string key) where T : FrameworkElement
    {
        element.SetResourceReference(property, key);
        return element;
    }

    internal static T At<T>(this T element, int column = 0, int row = 0) where T : UIElement
    {
        Grid.SetColumn(element, column); Grid.SetRow(element, row);
        return element;
    }

    internal static FrameworkElement Icon(string data, string brush = "Text.Muted", double size = 16, double thickness = 1.5)
    {
        var path = new IconPath
        {
            Data = Geometry.Parse(data), Width = 16, Height = 16, StrokeThickness = data == Icons.Grip || data == Icons.More ? 2 : thickness,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round, StrokeLineJoin = PenLineJoin.Round,
            IsHitTestVisible = false, SnapsToDevicePixels = false
        };
        // "inherit" follows the text colour of whatever the icon sits in, so templates can recolour it by state.
        if (brush == "inherit") path.SetBinding(Shape.StrokeProperty, new System.Windows.Data.Binding { RelativeSource = System.Windows.Data.RelativeSource.Self, Path = new PropertyPath(System.Windows.Documents.TextElement.ForegroundProperty) });
        else path.SetResourceReference(Shape.StrokeProperty, brush);
        if (data == Icons.Windows) { path.StrokeThickness = 0; path.SetResourceReference(Shape.FillProperty, brush); }
        if (size == 16) return path;
        return new Viewbox { Width = size, Height = size, Child = path, IsHitTestVisible = false };
    }

    internal static TextBlock Text(string text, double size = 13, string brush = "Text", bool bold = false, bool wrap = false)
    {
        var role = size >= 28 ? "Display" : size >= 20 ? "Heading" : size >= 14 ? "Title" : size <= 11 ? "Caption" : size < 13 ? "Small" : "Body";
        var label = new TextBlock
        {
            Text = text, TextTrimming = wrap ? TextTrimming.None : TextTrimming.CharacterEllipsis,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, VerticalAlignment = VerticalAlignment.Center
        }.Res(FrameworkElement.StyleProperty, "Theme.Text." + role);
        if (brush != "inherit") label.SetResourceReference(TextBlock.ForegroundProperty, brush);
        if (bold) label.SetResourceReference(TextBlock.FontWeightProperty, "Typography.Emphasis");
        return label;
    }

    internal static Button Button(string text, Action click, string? style = "Button.Secondary", string? icon = null)
    {
        object content = text;
        if (icon is not null)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = Icon(icon, style is "Button.Primary" ? "Accent.Text" : "Text.Muted"); glyph.Margin = new Thickness(0, 0, 8, 0);
            row.Children.Add(glyph);
            row.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center });
            content = row;
        }
        var button = new Button { Content = content };
        if (style is not null) button.SetResourceReference(FrameworkElement.StyleProperty, style);
        button.Click += (_, _) => click();
        return button;
    }

    internal static Button IconButton(string icon, string tip, Action click, string brush = "Text.Muted")
    {
        var button = new Button { Content = Icon(icon, brush), ToolTip = tip }.Res(FrameworkElement.StyleProperty, "Button.Icon");
        // An inheriting icon button takes the text colour of the surface it sits on, whatever the theme paints there.
        if (brush == "inherit") button.SetBinding(Control.ForegroundProperty, new System.Windows.Data.Binding("Foreground") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(Control), 1) });
        System.Windows.Automation.AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => click();
        return button;
    }

    internal static MenuItem Item(string header, Action click, string? icon = null, bool danger = false, bool enabled = true, string? gesture = null)
    {
        var item = new MenuItem { Header = header, IsEnabled = enabled, InputGestureText = gesture ?? "" };
        if (icon is not null) item.Icon = Icon(icon, danger ? "Danger" : "Text.Muted");
        if (danger) item.Tag = "danger";
        item.Click += (_, _) => click();
        return item;
    }

    /// <summary>
    /// The application's real icon, with a coloured letter tile as the placeholder while it loads
    /// and as the fallback for web links and missing files.
    /// </summary>
    internal static FrameworkElement AppIcon(string name, string target, double size = 24)
    {
        var letter = name.FirstOrDefault(char.IsLetterOrDigit);
        var color = TileColors[(int)((uint)StableHash(name) % TileColors.Length)];
        var tile = new Border
        {
            CornerRadius = new CornerRadius(size / 4), Background = new SolidColorBrush(Theme.Parse(color)),
            Child = new TextBlock
            {
                Text = letter == default ? "•" : char.ToUpper(letter).ToString(), Foreground = Brushes.White, FontSize = size / 2, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center
            }
        };
        var image = new Image { Stretch = Stretch.Uniform };
        RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
        var host = new Grid { Width = size, Height = size, IsHitTestVisible = false };
        host.Children.Add(tile); host.Children.Add(image);
        IconCache.Load(target, source =>
        {
            if (source is null) return;
            image.Source = source; tile.Visibility = Visibility.Hidden;
        });
        return host;
    }

    /// <summary>A set is drawn as up to four of its applications' icons in one tile.</summary>
    internal static FrameworkElement ShortcutIcon(Shortcut shortcut, double size = 24)
    {
        if (!shortcut.IsSet) return AppIcon(shortcut.Name, shortcut.Target, size);
        var shown = shortcut.Steps.Take(4).ToList();
        if (shown.Count == 1) return AppIcon(shown[0].Name, shown[0].Target, size);
        var cell = Math.Floor(size / 2);
        var tiles = new System.Windows.Controls.Primitives.UniformGrid { Columns = 2, Width = cell * 2, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        foreach (var step in shown)
        {
            var icon = AppIcon(step.Name, step.Target, cell - 1);
            icon.Margin = new Thickness(.5);
            tiles.Children.Add(icon);
        }
        return new Grid { Width = size, Height = size, IsHitTestVisible = false, Children = { tiles } };
    }

    private static int StableHash(string text)
    {
        var hash = 17;
        foreach (var c in text) hash = unchecked(hash * 31 + c);
        return hash;
    }

    internal static FrameworkElement Spinner(double size = 12)
    {
        var arc = new IconPath
        {
            Data = Geometry.Parse("M8 2a6 6 0 0 1 6 6"), Width = 16, Height = 16, StrokeThickness = 2,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round,
            RenderTransformOrigin = new Point(.5, .5), RenderTransform = new RotateTransform()
        }.Res(Shape.StrokeProperty, "Accent");
        arc.Loaded += (_, _) => arc.RenderTransform.BeginAnimation(RotateTransform.AngleProperty,
            new DoubleAnimation(0, 360, TimeSpan.FromSeconds(.8)) { RepeatBehavior = RepeatBehavior.Forever });
        return new Viewbox { Width = size, Height = size, Child = arc, IsHitTestVisible = false };
    }

    internal static void FadeIn(UIElement element, double from = 0, double offset = 6, int milliseconds = 130, string? transition = null)
    {
        if (element is FrameworkElement host)
            Theming.ThemeMotion.Play(host, transition ?? "Theme.Content.Enter", offset: offset, milliseconds: milliseconds);
    }

    /// <summary>Names offered when a new category is created, skipping ones already in use.</summary>
    internal static string FreshCategoryName(IEnumerable<Category> existing)
    {
        var names = existing.Select(c => c.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(L.T("Нова категорія"))) return L.T("Нова категорія");
        for (var i = 2; ; i++) if (!names.Contains(L.F("Нова категорія {0}", i))) return L.F("Нова категорія {0}", i);
    }
}
