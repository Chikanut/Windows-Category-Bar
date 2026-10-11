using System.Globalization;
using System.Text.RegularExpressions;

namespace CategoryBar.Core;

/// <summary>
/// The supported native CSS dialect. Values map to WPF layout resources, never to code. Later rules override
/// earlier ones, and <c>var(--p-id)</c> reads one of the theme's own parameters.
/// </summary>
public sealed class ThemeStyleSheet
{
    public const int MaximumLength = 16000;
    private static readonly string[] Elements = ["theme", "window", "titlebar", "sidebar", "sidebar-item", "page", "card", "button", "button-primary", "input", "bar", "tab", "menu", "menu-item", "toast"];
    private static readonly string[] Stateful = ["button", "button-primary", "input", "tab", "menu-item", "sidebar-item"];
    private static readonly string[] States = ["hover", "pressed", "focus", "selected", "disabled"];
    private static readonly string[] Typographic = ["button", "button-primary", "input", "tab", "menu-item", "sidebar-item"];
    private const string Block = @"\.([a-z-]+)(?::([a-z]+))?\s*\{([^{}]*)\}";

    private readonly Dictionary<string, string> values = [];
    /// <summary>Kept for themes written for the first format; every theme now uses the one shared renderer.</summary>
    public string Skin => values.GetValueOrDefault("theme.skin", "studio");
    public string Value(string selector, string property, string fallback) => values.GetValueOrDefault(selector + "." + property, fallback);
    public bool Has(string selector, string property) => values.ContainsKey(selector + "." + property);
    public double Number(string selector, string property, double fallback) => TryNumber(Value(selector, property, ""), out var value) ? value : fallback;
    public double[] Spacing(string selector, string property, double fallback)
    {
        var parts = Value(selector, property, fallback.ToString(CultureInfo.InvariantCulture)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => TryNumber(p, out var number) ? number : fallback).ToArray();
        // CSS top/right/bottom/left, later converted to WPF left/top/right/bottom.
        return parts.Length switch { 1 => [parts[0], parts[0], parts[0], parts[0]], 2 => [parts[0], parts[1], parts[0], parts[1]], _ => parts };
    }

    private static bool TryNumber(string text, out double value)
    {
        text = text.EndsWith("px", StringComparison.Ordinal) || text.EndsWith("ms", StringComparison.Ordinal) ? text[..^2] : text;
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);
    }

    /// <summary>Rules that make the listed palette tokens win over any background the theme painted itself.</summary>
    public static string SurfaceColorRules(IEnumerable<string> tokens) => string.Join("\n", tokens.Distinct().SelectMany(token => token switch
    {
        "Window.Bg" => new[] { ("window", "window-bg") }, "Sidebar.Bg" => [("sidebar", "sidebar-bg"), ("titlebar", "sidebar-bg")],
        "Bar.Bg" => [("bar", "bar-bg")], "Menu.Bg" => [("menu", "menu-bg")], "Card.Bg" => [("card", "card-bg")],
        "Control.Bg" => [("button", "control-bg"), ("input", "control-bg")], _ => []
    }).Select(rule => "." + rule.Item1 + " { background: var(--" + rule.Item2 + "); }"));

    public static ThemeStyleSheet Parse(string css, IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (css is null || css.Length > MaximumLength) throw Invalid("stylesheet is too large");
        var sheet = new ThemeStyleSheet();
        sheet.Read(BuiltIn("studio"), null);
        sheet.Read(css, parameters ?? new Dictionary<string, string>());
        return sheet;
    }

    private void Read(string css, IReadOnlyDictionary<string, string>? parameters)
    {
        css = Regex.Replace(css, @"/\*[\s\S]*?\*/", "");
        if (parameters is not null)
            css = Regex.Replace(css, @"var\(--p-([a-z0-9-]+)\)", match => parameters.TryGetValue(match.Groups[1].Value, out var value) ? value : throw Invalid("unknown parameter " + match.Groups[1].Value));
        if (!string.IsNullOrWhiteSpace(Regex.Replace(css, Block, ""))) throw Invalid("text outside of rules");
        foreach (Match block in Regex.Matches(css, Block))
        {
            var element = block.Groups[1].Value; var state = block.Groups[2].Value;
            if (!Elements.Contains(element)) throw Invalid("unknown selector ." + element);
            if (state.Length != 0 && (!States.Contains(state) || !Stateful.Contains(element))) throw Invalid("unsupported state ." + element + ":" + state);
            var selector = element + (state.Length == 0 ? "" : ":" + state);
            foreach (var declaration in block.Groups[3].Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = declaration.Split(':', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !Accepts(element, state, pair[0], pair[1])) throw Invalid("." + selector + " { " + declaration + " }");
                values[selector + "." + pair[0]] = pair[1];
            }
        }
    }

    private static bool Accepts(string element, string state, string property, string value)
    {
        if (element == "theme") return property == "skin" && value is "fluent" or "glass" or "expressive" or "midnight" or "studio";
        switch (property)
        {
            case "background" or "overlay": return ThemePaint.IsBrush(value);
            case "color" or "border-color" or "rim-color": return ThemePaint.IsColor(value);
            case "icon-color": return element == "sidebar-item" && ThemePaint.IsColor(value);
            case "box-shadow": return value == "none" || ThemePaint.Shadow(value) is not null;
            case "opacity": return Range(value, 0, 1);
        }
        if (state.Length != 0) return false;
        return property switch
        {
            "indicator" => element == "tab" && value is "underline" or "dot" or "none" or "rail" or "pill",
            "selected-background" => element == "tab" && value is "accent-soft" or "selected" or "control",
            "font-weight" => Typographic.Contains(element) && value is "normal" or "medium" or "semibold" or "bold",
            "font-size" => Typographic.Contains(element) && Range(value, 10, 20),
            "height" => element is "titlebar" or "bar" or "tab" or "menu-item" or "sidebar-item" && Range(value, 20, 72),
            "min-height" => element is "button" or "button-primary" or "input" && Range(value, 24, 56),
            "width" => element == "menu" && Range(value, 220, 420) || element == "sidebar" && Range(value, 160, 320),
            "border-radius" => Range(value, 0, 40),
            "border-width" or "rim-width" => Range(value, 0, 4),
            "padding" => Box(value, 40),
            "margin" => element is "tab" or "menu-item" or "sidebar-item" or "sidebar" or "page" && Box(value, 24),
            "gap" => element is "bar" or "menu" or "card" && Range(value, 0, 24),
            "icon-size" => element == "menu-item" && Range(value, 16, 40),
            // Distance between the bar and the taskbar, and between an open menu and the bar.
            "offset" => element is "bar" or "menu" && Range(value, 0, 24),
            // "wallpaper" gives the surface the desktop wallpaper behind it, blurred by backdrop-blur pixels,
            // which the theme's material shader may bend and tint (the "Theme.Backdrop" brush). "live" is its old name.
            "backdrop" => element is "bar" or "menu" or "toast" && value is "none" or "wallpaper" or "live",
            "backdrop-blur" => element is "bar" or "menu" or "toast" && Range(value, 0, 40),
            // Delay between neighbours when tabs, menu rows or cards play their Enter transition.
            "stagger" => element is "tab" or "menu-item" or "card" && Range(value, 0, 120),
            _ => false
        };
    }

    private static bool Range(string value, double min, double max) =>
        Regex.IsMatch(value, @"^\d+(\.\d+)?(px|ms)?$") && TryNumber(value, out var number) && number >= min && number <= max;
    private static bool Box(string value, double max)
    {
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length is 1 or 2 or 4 && parts.All(p => Range(p, 0, max));
    }

    private static InvalidDataException Invalid(string detail) => new(L.T("Некоректні властивості теми.") + " (" + detail + ")");
    private static string BuiltIn(string id)
    {
        using var stream = typeof(ThemeStyleSheet).Assembly.GetManifestResourceStream("ThemeStyles/" + id + ".css")!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
