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
                case "Number":
                    var number = double.Parse(value, CultureInfo.InvariantCulture);
                    Put(key, number);
                    // The same number in the shapes that layout properties expect.
                    if (number >= 0) { Put(key + ".Thickness", new Thickness(number)); Put(key + ".CornerRadius", new CornerRadius(number)); Put(key + ".Length", new GridLength(number)); }
                    break;
                case "Toggle":
                    Put(key, value == "true");
                    Put(key + ".Visibility", value == "true" ? Visibility.Visible : Visibility.Collapsed);
                    Put(key + ".Opacity", value == "true" ? 1d : 0d);
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
