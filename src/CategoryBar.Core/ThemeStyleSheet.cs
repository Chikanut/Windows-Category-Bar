using System.Globalization;
using System.Text.RegularExpressions;

namespace CategoryBar.Core;

/// <summary>The supported native CSS dialect. Values map to WPF layout resources, never to code.</summary>
public sealed class ThemeStyleSheet
{
    private readonly Dictionary<string, string> values = [];
    public string Skin => values.GetValueOrDefault("theme.skin", "fluent");
    public string Value(string selector, string property, string fallback) => values.GetValueOrDefault(selector + "." + property, fallback);
    public double Number(string selector, string property, double fallback) => double.TryParse(Value(selector, property, "").Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture, out var value) ? value : fallback;
    public double[] Spacing(string selector, string property, double fallback)
    {
        var parts = Value(selector, property, fallback.ToString(CultureInfo.InvariantCulture)).Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture)).ToArray();
        // CSS top/right/bottom/left, later converted to WPF left/top/right/bottom.
        return parts.Length switch { 1 => [parts[0], parts[0], parts[0], parts[0]], 2 => [parts[0], parts[1], parts[0], parts[1]], _ => parts };
    }

    public static ThemeStyleSheet Parse(string css) => Parse(css, inherit: true);
    public string WithGeometry(double radiusScale = 1, double? borderWidth = null, double? zeroRadius = null)
    {
        var copy = new Dictionary<string, string>(values);
        foreach (var key in copy.Keys.ToArray())
        {
            if (key.EndsWith(".border-radius", StringComparison.Ordinal))
            {
                var radius = double.Parse(copy[key].Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture);
                copy[key] = Math.Clamp(radius == 0 && zeroRadius.HasValue ? zeroRadius.Value : radius * radiusScale, 0, 32).ToString("0.###", CultureInfo.InvariantCulture) + "px";
            }
            if (borderWidth.HasValue && key.EndsWith(".border-width", StringComparison.Ordinal)) copy[key] = borderWidth.Value.ToString(CultureInfo.InvariantCulture) + "px";
        }
        return string.Join("\n", copy.GroupBy(v => v.Key[..v.Key.LastIndexOf('.')]).Select(group =>
            "." + group.Key + " { " + string.Join(" ", group.Select(v => v.Key[(v.Key.LastIndexOf('.') + 1)..] + ": " + v.Value + ";")) + " }"));
    }
    private static ThemeStyleSheet Parse(string css, bool inherit)
    {
        if (css is null || css.Length > 12000) throw Invalid();
        var sheet = new ThemeStyleSheet();
        css = Regex.Replace(css, @"/\*[\s\S]*?\*/", "");
        var blocks = Regex.Matches(css, @"\.([a-z-]+)(?::(hover|pressed|focus|selected|disabled))?\s*\{([^{}]*)\}");
        if (!string.IsNullOrWhiteSpace(Regex.Replace(css, @"\.([a-z-]+)(?::(hover|pressed|focus|selected|disabled))?\s*\{([^{}]*)\}", ""))) throw Invalid();
        foreach (Match block in blocks)
        {
            var element = block.Groups[1].Value;
            var state = block.Groups[2].Value;
            var selector = element + (state.Length == 0 ? "" : ":" + state);
            if (element is not ("theme" or "window" or "sidebar" or "bar" or "tab" or "menu" or "menu-item" or "button" or "button-primary" or "input" or "card" or "sidebar-item" or "titlebar" or "page" or "toast")) throw Invalid();
            if (state.Length != 0 && element is not ("button" or "button-primary" or "input" or "tab" or "menu-item" or "sidebar-item")) throw Invalid();
            foreach (var declaration in block.Groups[3].Value.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = declaration.Split(':', 2, StringSplitOptions.TrimEntries);
                if (pair.Length != 2 || !sheet.values.TryAdd(selector + "." + pair[0], pair[1])) throw Invalid();
                var property = pair[0]; var value = pair[1];
                if (selector == "theme") { if (property != "skin" || value is not ("fluent" or "glass" or "expressive" or "midnight" or "studio")) throw Invalid(); continue; }
                if (property is "background" or "overlay") { if (!ThemePaint.IsBrush(value)) throw Invalid(); continue; }
                if (property is "color" or "border-color" or "rim-color") { if (!ThemePaint.IsColor(value)) throw Invalid(); continue; }
                if (property == "box-shadow") { if (value != "none" && ThemePaint.Shadow(value) is null) throw Invalid(); continue; }
                if (state.Length != 0 && property != "opacity") throw Invalid();
                if (selector == "tab" && property == "indicator") { if (value is not ("underline" or "dot" or "none" or "rail")) throw Invalid(); continue; }
                if (selector == "tab" && property == "selected-background") { if (value is not ("accent-soft" or "selected" or "control")) throw Invalid(); continue; }
                if (property == "font-weight" && selector is "button" or "tab" or "sidebar-item" or "button-primary" or "input") { if (value is not ("normal" or "semibold" or "bold")) throw Invalid(); continue; }
                var max = property switch
                {
                    "height" when selector is "titlebar" or "bar" or "tab" or "menu-item" or "sidebar-item" => 64,
                    "min-height" when selector is "button" or "input" => 56,
                    "width" when selector == "menu" => 420,
                    "border-radius" => 32,
                    "border-width" => 3,
                    "rim-width" => 3,
                    "opacity" => 1,
                    "padding" => 32,
                    "gap" when selector is "bar" or "menu" or "card" => 20,
                    "margin" when selector is "tab" or "menu-item" or "sidebar-item" => 12,
                    "font-size" when selector is "button" or "tab" or "input" or "button-primary" => 18,
                    "icon-size" when selector == "menu-item" => 40,
                    _ => -1
                };
                var min = property switch { "height" => 20, "min-height" => 24, "width" => 220, "font-size" => 10, "icon-size" => 16, _ => 0 };
                var numbers = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (max < 0 || ((property is "padding" or "margin") ? numbers.Length is not (1 or 2 or 4) : numbers.Length != 1)) throw Invalid();
                foreach (var number in numbers)
                    if (!Regex.IsMatch(number, @"^\d+(\.\d+)?(px)?$") || !double.TryParse(number.Replace("px", "", StringComparison.Ordinal), CultureInfo.InvariantCulture, out var parsed) || !double.IsFinite(parsed) || parsed < min || parsed > max) throw Invalid();
            }
        }
        if (inherit)
        {
            var defaults = Parse(BuiltIn(sheet.Skin), inherit: false);
            foreach (var (key, value) in defaults.values) sheet.values.TryAdd(key, value);
        }
        return sheet;
    }

    private static InvalidDataException Invalid() => new(L.T("Некоректні властивості теми."));
    internal static string BuiltIn(string id)
    {
        using var stream = typeof(ThemeStyleSheet).Assembly.GetManifestResourceStream("CategoryBar.Core.ThemeStyles." + id + ".css")!;
        using var reader = new StreamReader(stream); return reader.ReadToEnd();
    }
}
