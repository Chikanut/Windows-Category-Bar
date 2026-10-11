using System.Globalization;

namespace CategoryBar.Theming;

internal static class ThemeParameterResources
{
    internal static IReadOnlyList<string> Apply(ResourceDictionary resources, ThemeDefinition theme, Settings settings)
    {
        var keys = new List<string>();
        void Put(string key, object value) { resources[key] = value; keys.Add(key); }
        foreach (var parameter in theme.Parameters)
        {
            var value = parameter.Resolve(settings, theme.Id);
            var key = "Theme.Parameter." + parameter.Id;
            switch (parameter.Type)
            {
                case "Number": Put(key, double.Parse(value, CultureInfo.InvariantCulture)); break;
                case "Toggle":
                    Put(key, value == "true");
                    Put(key + ".Visibility", value == "true" ? Visibility.Visible : Visibility.Collapsed);
                    break;
                case "Color":
                    var color = Theme.Parse(value); var brush = new SolidColorBrush(color); brush.Freeze();
                    Put(key, brush); Put(key + ".Color", color);
                    break;
                case "Choice": Put(key, value); break;
            }
        }
        return keys;
    }
}
