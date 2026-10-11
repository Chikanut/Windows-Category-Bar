using System.Globalization;
using System.Windows.Media.Effects;

namespace CategoryBar;

internal static class ThemePaintResources
{
    internal static void Apply(ResourceDictionary resources, ThemeStyleSheet css)
    {
        Color ColorValue(string value)
        {
            if (value == "transparent") return Colors.Transparent;
            if (ThemePaint.Variable(value) is { } token)
            {
                var brush = resources[token] as Brush ?? Application.Current.Resources[token] as Brush;
                return brush switch { SolidColorBrush solid => solid.Color, GradientBrush gradient => gradient.GradientStops[0].Color, _ => Colors.Transparent };
            }
            if (!value.StartsWith("rgba(", StringComparison.Ordinal)) return Theme.Parse(value);
            var numbers = ThemePaint.Arguments(value[5..^1]).Select(v => double.Parse(v, CultureInfo.InvariantCulture)).ToArray();
            return Color.FromArgb((byte)Math.Round(numbers[3] * 255), (byte)Math.Round(numbers[0]), (byte)Math.Round(numbers[1]), (byte)Math.Round(numbers[2]));
        }
        Brush Paint(string value)
        {
            if (!value.StartsWith("linear-gradient(", StringComparison.Ordinal)) return new SolidColorBrush(ColorValue(value));
            var args = ThemePaint.Arguments(value[16..^1]);
            var angle = double.Parse(args[0][..^3], CultureInfo.InvariantCulture) * Math.PI / 180;
            var dx = Math.Sin(angle) / 2; var dy = -Math.Cos(angle) / 2;
            var brush = new LinearGradientBrush { StartPoint = new Point(.5 - dx, .5 - dy), EndPoint = new Point(.5 + dx, .5 + dy) };
            for (var i = 1; i < args.Length; i++) brush.GradientStops.Add(new GradientStop(ColorValue(args[i]), (double)(i - 1) / (args.Length - 2)));
            brush.Freeze(); return brush;
        }
        void StorePaint(string selector, string property, string suffix, string fallback) => resources[selector + "." + suffix] = Paint(css.Value(selector, property, fallback));
        foreach (var (selector, fallback) in new[] { ("window", "window-bg"), ("sidebar", "sidebar-bg"), ("titlebar", "sidebar-bg"), ("page", "window-bg"), ("toast", "popup-bg"), ("bar", "bar-bg"), ("menu", "menu-bg"), ("menu-item", "control-bg"), ("tab", "control-bg"), ("button", "control-bg"), ("button-primary", "accent"), ("input", "control-bg"), ("card", "card-bg"), ("sidebar-item", "control-bg") })
        {
            StorePaint(selector, "background", "Background", selector is "tab" or "menu-item" or "sidebar-item" ? "transparent" : "var(--" + fallback + ")");
            StorePaint(selector, "color", "Foreground", selector == "button-primary" ? "var(--accent-text)" : "var(--text)");
            StorePaint(selector, "border-color", "BorderBrush", "var(--border)");
            StorePaint(selector, "overlay", "Overlay", "transparent");
            StorePaint(selector, "rim-color", "Rim", "transparent");
            resources[selector + ".RimWidth"] = new Thickness(css.Number(selector, "rim-width", 0));
            resources[selector + ".Opacity"] = css.Number(selector, "opacity", 1);
            resources[selector + ".Shadow"] = Effect(css.Value(selector, "box-shadow", "none"));
            foreach (var state in new[] { "hover", "pressed", "focus", "selected", "disabled" })
            {
                var key = selector + ":" + state;
                StorePaint(key, "background", "Background", state switch { "hover" => "var(--hover)", "pressed" => "var(--pressed)", "selected" => "var(--accent-soft)", _ => "transparent" });
                StorePaint(key, "border-color", "BorderBrush", state == "focus" ? "var(--accent)" : css.Value(selector, "border-color", "var(--border)"));
                StorePaint(key, "color", "Foreground", css.Value(selector, "color", selector == "button-primary" ? "var(--accent-text)" : "var(--text)"));
                resources[key + ".Opacity"] = css.Number(key, "opacity", state == "disabled" ? .45 : 1);
                resources[key + ".Shadow"] = Effect(css.Value(key, "box-shadow", css.Value(selector, "box-shadow", "none")));
            }
        }
        if (!SystemParameters.HighContrast)
            foreach (var (selector, token) in new[] { ("window", "Window.Bg"), ("sidebar", "Sidebar.Bg"), ("bar", "Bar.Bg"), ("menu", "Menu.Bg"), ("card", "Card.Bg"), ("input", "Control.Bg") })
                if (css.Value(selector, "background", "").Length != 0) resources[token] = resources[selector + ".Background"];

        DropShadowEffect Effect(string value)
        {
            var shadow = ThemePaint.Shadow(value);
            if (shadow is null || SystemParameters.HighContrast) return new DropShadowEffect { Opacity = 0 };
            var (x, y, blur, paint) = shadow.Value; var color = ColorValue(paint);
            var effect = new DropShadowEffect { Color = Color.FromRgb(color.R, color.G, color.B), Opacity = color.A / 255.0, BlurRadius = blur, ShadowDepth = Math.Sqrt(x * x + y * y), Direction = Math.Atan2(-y, x) * 180 / Math.PI };
            effect.Freeze(); return effect;
        }
    }
}
