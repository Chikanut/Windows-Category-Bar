using System.Globalization;
using System.Windows.Media.Effects;

namespace CategoryBar;

/// <summary>
/// Turns the theme's stylesheet into the "&lt;selector&gt;.&lt;Property&gt;" resources that the shared templates and
/// authored templates read: geometry, typography, paints, shadows and the look of each interactive state.
/// </summary>
internal static class ThemeStyleResources
{
    internal static readonly string[] Selectors = ["window", "titlebar", "sidebar", "sidebar-item", "page", "card", "button", "button-primary", "input", "bar", "tab", "menu", "menu-item", "toast"];
    private static readonly string[] States = ["hover", "pressed", "focus", "selected", "disabled"];
    private static readonly DropShadowEffect NoShadow = Frozen(new DropShadowEffect { Opacity = 0, BlurRadius = 0, ShadowDepth = 0 });
    private static T Frozen<T>(T value) where T : Freezable { value.Freeze(); return value; }

    internal static void Apply(ResourceDictionary resources, ThemeStyleSheet css, double radiusScale = 1)
    {
        resources["Typography.Emphasis"] = FontWeights.SemiBold;
        // A primary button is a button first: whatever it does not state itself comes from ".button".
        string Source(string selector, string property) => selector == "button-primary" && !css.Has(selector, property) ? "button" : selector;
        double Number(string selector, string property, double fallback) => css.Number(Source(selector, property), property, fallback);
        Thickness Spacing(string selector, string property, double fallback)
        {
            var v = css.Spacing(Source(selector, property), property, fallback); return new Thickness(v[3], v[0], v[1], v[2]);
        }
        foreach (var selector in Selectors)
        {
            resources[selector + ".Radius"] = new CornerRadius(Math.Clamp(Number(selector, "border-radius", 6) * radiusScale, 0, 40));
            resources[selector + ".Border"] = new Thickness(Number(selector, "border-width", 0));
            resources[selector + ".Padding"] = Spacing(selector, "padding", 0);
            resources[selector + ".Margin"] = Spacing(selector, "margin", 0);
            resources[selector + ".Height"] = Number(selector, "height", selector == "titlebar" ? 40 : 36);
            resources[selector + ".MinHeight"] = Number(selector, "min-height", 30);
            resources[selector + ".FontSize"] = Number(selector, "font-size", 13);
            resources[selector + ".FontWeight"] = css.Value(Source(selector, "font-weight"), "font-weight", "normal") switch
            {
                "medium" => FontWeights.Medium, "semibold" => FontWeights.SemiBold, "bold" => FontWeights.Bold, _ => FontWeights.Normal
            };
        }
        var tabMargin = Spacing("tab", "margin", 0); var gap = css.Number("bar", "gap", 0) / 2;
        resources["tab.Margin"] = new Thickness(tabMargin.Left + gap, tabMargin.Top, tabMargin.Right + gap, tabMargin.Bottom);
        var rowMargin = Spacing("menu-item", "margin", 0); var rowGap = css.Number("menu", "gap", 0) / 2;
        resources["menu-item.Margin"] = new Thickness(rowMargin.Left, rowMargin.Top + rowGap, rowMargin.Right, rowMargin.Bottom + rowGap);
        resources["tab.Indicator"] = css.Value("tab", "indicator", "underline");
        resources["menu.Width"] = css.Number("menu", "width", 282);
        resources["sidebar.Width"] = css.Number("sidebar", "width", 208);
        resources["menu-item.IconSize"] = css.Number("menu-item", "icon-size", 24);
        resources["card.Margin"] = new Thickness(0, 0, 0, css.Number("card", "gap", 6));
        foreach (var selector in new[] { "tab", "menu-item", "card" }) resources[selector + ".Stagger"] = css.Number(selector, "stagger", 0);
        foreach (var selector in new[] { "bar", "menu", "toast" })
        {
            resources[selector + ".Backdrop"] = css.Value(selector, "backdrop", "none") == "live";
            resources[selector + ".BackdropBlur"] = css.Number(selector, "backdrop-blur", 12);
        }
        resources["Toggle.Radius"] = new CornerRadius(10);
        resources["Radius"] = resources["menu.Radius"];
        var small = new CornerRadius(Math.Min(10, ((CornerRadius)resources["button.Radius"]).TopLeft));
        resources["Radius.Small"] = small;
        resources["Radius.Top"] = new CornerRadius(small.TopLeft, small.TopLeft, 0, 0);
        resources["Theme.BorderThickness"] = resources["button.Border"];
        Paint(resources, css, Source);

        // Room around the overlays for their shadows; the bar's bottom edge keeps the theme's distance to the taskbar.
        var room = ThemePaint.Shadow(css.Value("bar", "box-shadow", "none")) is { } s ? Math.Ceiling(s.Blur + Math.Max(Math.Abs(s.X), Math.Abs(s.Y))) : 0;
        resources["bar.Frame"] = new Thickness(room, room, room, css.Number("bar", "offset", 0));
        resources["menu.Frame"] = new Thickness(24, 24, 24, css.Number("menu", "offset", 8));
    }

    private static void Paint(ResourceDictionary resources, ThemeStyleSheet css, Func<string, string, string> source)
    {
        Color ColorValue(string value)
        {
            if (value == "transparent") return Colors.Transparent;
            if (ThemePaint.Variable(value) is { } token)
                return (resources[token] as Brush ?? Application.Current.Resources[token] as Brush) switch
                {
                    SolidColorBrush solid => solid.Color, GradientBrush gradient => gradient.GradientStops[0].Color, _ => Colors.Transparent
                };
            static double N(string text) => double.Parse(text, CultureInfo.InvariantCulture);
            if (ThemePaint.Function(value, "rgba") is { } rgba) return Color.FromArgb((byte)Math.Round(N(rgba[3]) * 255), (byte)Math.Round(N(rgba[0])), (byte)Math.Round(N(rgba[1])), (byte)Math.Round(N(rgba[2])));
            if (ThemePaint.Function(value, "alpha") is { } alpha) { var c = ColorValue(alpha[0]); return Color.FromArgb((byte)Math.Round(c.A * N(alpha[1])), c.R, c.G, c.B); }
            if (ThemePaint.Function(value, "mix") is { } mix)
            {
                var (a, b, t) = (ColorValue(mix[0]), ColorValue(mix[1]), N(mix[2]));
                byte Blend(byte x, byte y) => (byte)Math.Round(x + (y - x) * t);
                return Color.FromArgb(Blend(a.A, b.A), Blend(a.R, b.R), Blend(a.G, b.G), Blend(a.B, b.B));
            }
            return Theme.Parse(value);
        }
        Brush Fill(string value)
        {
            GradientBrush? gradient = null; string[] stops = [];
            if (ThemePaint.Function(value, "linear-gradient") is { } linear)
            {
                var angle = double.Parse(linear[0][..^3], CultureInfo.InvariantCulture) * Math.PI / 180;
                var dx = Math.Sin(angle) / 2; var dy = -Math.Cos(angle) / 2;
                gradient = new LinearGradientBrush { StartPoint = new Point(.5 - dx, .5 - dy), EndPoint = new Point(.5 + dx, .5 + dy) }; stops = linear[1..];
            }
            else if (ThemePaint.Function(value, "radial-gradient") is { } radial) { gradient = new RadialGradientBrush(); stops = radial; }
            if (gradient is null) { var solid = new SolidColorBrush(ColorValue(value)); solid.Freeze(); return solid; }
            for (var i = 0; i < stops.Length; i++) gradient.GradientStops.Add(new GradientStop(ColorValue(stops[i]), (double)i / (stops.Length - 1)));
            gradient.Freeze(); return gradient;
        }
        DropShadowEffect Shadow(string value)
        {
            // A resource cannot be null (lookup would fall through to another shadow), so "none" is an invisible effect.
            if (ThemePaint.Shadow(value) is not { } shadow || SystemParameters.HighContrast) return NoShadow;
            var (x, y, blur, paint) = shadow; var color = ColorValue(paint);
            var effect = new DropShadowEffect { Color = Color.FromRgb(color.R, color.G, color.B), Opacity = color.A / 255.0, BlurRadius = blur, ShadowDepth = Math.Sqrt(x * x + y * y), Direction = Math.Atan2(-y, x) * 180 / Math.PI };
            effect.Freeze(); return effect;
        }
        string Value(string selector, string property, string fallback) => css.Value(source(selector, property), property, fallback);

        foreach (var selector in Selectors)
        {
            var background = selector switch
            {
                "tab" or "menu-item" or "sidebar-item" or "page" or "titlebar" => "transparent", "window" => "var(--window-bg)", "sidebar" => "var(--sidebar-bg)",
                "toast" => "var(--popup-bg)", "bar" => "var(--bar-bg)", "menu" => "var(--menu-bg)", "card" => "var(--card-bg)", "button-primary" => "var(--accent)", _ => "var(--control-bg)"
            };
            var color = selector == "button-primary" ? css.Value(selector, "color", "var(--accent-text)") : css.Value(selector, "color", "var(--text)");
            var border = Value(selector, "border-color", "var(--border)");
            var shadow = Value(selector, "box-shadow", "none");
            resources[selector + ".Background"] = Fill(css.Value(selector, "background", background));
            resources[selector + ".Foreground"] = Fill(color);
            resources[selector + ".BorderBrush"] = Fill(border);
            resources[selector + ".Overlay"] = Fill(Value(selector, "overlay", "transparent"));
            resources[selector + ".Rim"] = Fill(Value(selector, "rim-color", "transparent"));
            resources[selector + ".RimWidth"] = new Thickness(css.Number(source(selector, "rim-width"), "rim-width", 0));
            resources[selector + ".Opacity"] = css.Number(selector, "opacity", 1);
            resources[selector + ".Shadow"] = Shadow(shadow);
            foreach (var state in States)
            {
                var key = selector + ":" + state;
                resources[key + ".Background"] = Fill(css.Value(key, "background", state switch { "hover" => "var(--hover)", "pressed" => "var(--pressed)", "selected" => "var(--accent-soft)", _ => "transparent" }));
                resources[key + ".BorderBrush"] = Fill(css.Value(key, "border-color", state == "focus" ? "var(--accent)" : border));
                resources[key + ".Foreground"] = Fill(css.Value(key, "color", color));
                resources[key + ".Opacity"] = css.Number(key, "opacity", state == "disabled" ? .45 : 1);
                resources[key + ".Shadow"] = Shadow(css.Value(key, "box-shadow", shadow));
            }
        }
        resources["sidebar-item.Icon"] = Fill(css.Value("sidebar-item", "icon-color", "var(--text-muted)"));
        resources["sidebar-item:selected.Icon"] = Fill(css.Value("sidebar-item:selected", "icon-color", "var(--accent)"));
        // Code that paints with a palette token directly follows the surface the theme actually drew.
        if (!SystemParameters.HighContrast)
            foreach (var (selector, token) in new[] { ("window", "Window.Bg"), ("sidebar", "Sidebar.Bg"), ("bar", "Bar.Bg"), ("menu", "Menu.Bg"), ("card", "Card.Bg"), ("input", "Control.Bg") })
                if (css.Has(selector, "background")) resources[token] = resources[selector + ".Background"];
    }
}
