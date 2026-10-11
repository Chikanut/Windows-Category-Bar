using System.Globalization;
using System.Text.RegularExpressions;

namespace CategoryBar.Core;

/// <summary>Portable paint syntax shared by the importer and the native renderer.</summary>
public static class ThemePaint
{
    public static readonly IReadOnlyDictionary<string, string> Variables = new Dictionary<string, string>
    {
        ["bar-bg"] = "Bar.Bg", ["menu-bg"] = "Menu.Bg", ["popup-bg"] = "Popup.Bg", ["window-bg"] = "Window.Bg",
        ["sidebar-bg"] = "Sidebar.Bg", ["card-bg"] = "Card.Bg", ["control-bg"] = "Control.Bg", ["text"] = "Text",
        ["text-muted"] = "Text.Muted", ["text-faint"] = "Text.Faint", ["border"] = "Surface.Border", ["accent"] = "Accent",
        ["accent-soft"] = "Accent.Soft", ["accent-text"] = "Accent.Text", ["accent-fill"] = "Accent.Fill", ["accent-fill-text"] = "Accent.FillText", ["hover"] = "Hover", ["pressed"] = "Pressed", ["selected"] = "Selected",
        ["danger"] = "Danger", ["warning"] = "Warning", ["success"] = "Success"
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

    /// <summary>The arguments of <c>name(…)</c>, or null when the value is not that function.</summary>
    public static string[]? Function(string value, string name) =>
        value.StartsWith(name + "(", StringComparison.Ordinal) && value.EndsWith(')') ? Arguments(value[(name.Length + 1)..^1]) : null;

    /// <summary>transparent, #RRGGBB, var(--token), rgba(r,g,b,a), alpha(color, a) and mix(color, color, amount).</summary>
    public static bool IsColor(string value) => IsColor(value, 0);
    private static bool IsColor(string value, int depth)
    {
        if (depth > 4) return false;
        if (value == "transparent" || Settings.IsHexColor(value) || Variable(value) is not null) return true;
        if (Function(value, "rgba") is { Length: 4 } rgba) return rgba.Take(3).All(v => Number(v, 0, 255)) && Number(rgba[3], 0, 1);
        if (Function(value, "alpha") is { Length: 2 } alpha) return IsColor(alpha[0], depth + 1) && Number(alpha[1], 0, 1);
        if (Function(value, "mix") is { Length: 3 } mix) return IsColor(mix[0], depth + 1) && IsColor(mix[1], depth + 1) && Number(mix[2], 0, 1);
        return false;
    }

    /// <summary>A colour, linear-gradient(angle, 2–5 colours) or radial-gradient(2–4 colours, centre to edge).</summary>
    public static bool IsBrush(string value)
    {
        if (IsColor(value)) return true;
        if (Function(value, "linear-gradient") is { Length: >= 3 and <= 6 } linear)
            return linear[0].EndsWith("deg", StringComparison.Ordinal) && Number(linear[0][..^3], -360, 360) && linear.Skip(1).All(IsColor);
        return Function(value, "radial-gradient") is { Length: >= 2 and <= 4 } radial && radial.All(IsColor);
    }

    public static (double X, double Y, double Blur, string Color)? Shadow(string value)
    {
        var match = Regex.Match(value, @"^(-?\d+(?:\.\d+)?)(?:px)?\s+(-?\d+(?:\.\d+)?)(?:px)?\s+(\d+(?:\.\d+)?)(?:px)?\s+(.+)$");
        if (!match.Success || !Number(match.Groups[1].Value, -24, 24) || !Number(match.Groups[2].Value, -24, 24) || !Number(match.Groups[3].Value, 0, 64) || !IsColor(match.Groups[4].Value)) return null;
        return (double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), match.Groups[4].Value);
    }
    private static bool Number(string value, double min, double max) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= min && number <= max;
}
