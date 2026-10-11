using System.Globalization;
using System.Text.RegularExpressions;

namespace CategoryBar.Core;

/// <summary>Portable paint syntax shared by the editor, importer and native renderer.</summary>
public static class ThemePaint
{
    public static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["bar-bg"] = "Bar.Bg", ["menu-bg"] = "Menu.Bg", ["popup-bg"] = "Popup.Bg", ["window-bg"] = "Window.Bg",
        ["sidebar-bg"] = "Sidebar.Bg", ["card-bg"] = "Card.Bg", ["control-bg"] = "Control.Bg", ["text"] = "Text",
        ["text-muted"] = "Text.Muted", ["text-faint"] = "Text.Faint", ["border"] = "Surface.Border", ["accent"] = "Accent",
        ["accent-soft"] = "Accent.Soft", ["accent-text"] = "Accent.Text", ["hover"] = "Hover", ["pressed"] = "Pressed", ["selected"] = "Selected"
    };
    public static string? Variable(string value) => Regex.Match(value, @"^var\(--([a-z-]+)\)$") is { Success: true } match && Variables.TryGetValue(match.Groups[1].Value, out var token) ? token : null;
    public static string[] Arguments(string value)
    {
        var parts = new List<string>(); var depth = 0; var start = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '(') depth++;
            if (value[i] == ')') depth--;
            if (value[i] == ',' && depth == 0) { parts.Add(value[start..i].Trim()); start = i + 1; }
        }
        parts.Add(value[start..].Trim()); return parts.ToArray();
    }
    public static bool IsColor(string value)
    {
        if (value == "transparent" || Settings.IsHexColor(value) || Variable(value) is not null) return true;
        if (!value.StartsWith("rgba(", StringComparison.Ordinal) || !value.EndsWith(')')) return false;
        var args = Arguments(value[5..^1]);
        return args.Length == 4 && args.Take(3).All(v => Number(v, 0, 255)) && Number(args[3], 0, 1);
    }
    public static bool IsBrush(string value)
    {
        if (IsColor(value)) return true;
        if (!value.StartsWith("linear-gradient(", StringComparison.Ordinal) || !value.EndsWith(')')) return false;
        var args = Arguments(value[16..^1]);
        return args.Length is 3 or 4 && args[0].EndsWith("deg", StringComparison.Ordinal) && Number(args[0][..^3], -360, 360) && args.Skip(1).All(IsColor);
    }
    public static (double X, double Y, double Blur, string Color)? Shadow(string value)
    {
        var match = Regex.Match(value, @"^(-?\d+(?:\.\d+)?)(?:px)?\s+(-?\d+(?:\.\d+)?)(?:px)?\s+(\d+(?:\.\d+)?)(?:px)?\s+(.+)$");
        if (!match.Success || !Number(match.Groups[1].Value, -24, 24) || !Number(match.Groups[2].Value, -24, 24) || !Number(match.Groups[3].Value, 0, 64) || !IsColor(match.Groups[4].Value)) return null;
        return (double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), match.Groups[4].Value);
    }
    private static bool Number(string value, double min, double max) => double.TryParse(value, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= min && number <= max;
}
