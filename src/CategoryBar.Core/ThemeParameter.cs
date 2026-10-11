using System.Globalization;

namespace CategoryBar.Core;

/// <summary>Portable author-defined settings. Values are invariant strings in configuration and exports.</summary>
public sealed class ThemeParameter
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public string Description { get; set; } = "";
    public string Type { get; set; } = "Number";
    public string Default { get; set; } = "0";
    public double Minimum { get; set; }
    public double Maximum { get; set; } = 1;
    public double Step { get; set; } = .05;
    public Dictionary<string, string> Options { get; set; } = [];

    public void Validate()
    {
        if (!ThemeDefinition.IsValidId(Id) || string.IsNullOrWhiteSpace(Label) || Label.Length > 80
            || Description is null || Description.Length > 300 || Options is null || Options.Count > 16
            || Options.Any(p => !ThemeDefinition.IsValidId(p.Key) || string.IsNullOrWhiteSpace(p.Value) || p.Value.Length > 80)
            || Type is not ("Number" or "Toggle" or "Color" or "Choice")
            || !double.IsFinite(Minimum) || !double.IsFinite(Maximum) || !double.IsFinite(Step)
            || Minimum < -10000 || Maximum > 10000 || Minimum >= Maximum || Step <= 0 || Step > Maximum - Minimum
            || Type == "Choice" && Options.Count < 2 || !Accepts(Default))
            throw new InvalidDataException("Invalid theme parameter: " + Id);
    }

    public bool Accepts(string? value) => value is { Length: > 0 and <= 128 } && Type switch
    {
        "Number" => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) && number >= Minimum && number <= Maximum,
        "Toggle" => value is "true" or "false",
        "Color" => Settings.IsHexColor(value),
        "Choice" => Options.ContainsKey(value),
        _ => false
    };

    public string Resolve(Settings settings, string themeId) => settings.ThemeParameters.TryGetValue(themeId, out var values)
        && values.TryGetValue(Id, out var value) && Accepts(value) ? value : Default;

    public void Set(Settings settings, string themeId, string value)
    {
        if (!Accepts(value)) throw new InvalidDataException("Invalid value for theme parameter: " + Id);
        if (!settings.ThemeParameters.TryGetValue(themeId, out var values)) settings.ThemeParameters[themeId] = values = [];
        values[Id] = value;
    }
}
