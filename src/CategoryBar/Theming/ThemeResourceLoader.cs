using System.Windows.Markup;
using System.Windows.Media.Effects;
using System.Xml;
using System.Xml.Linq;
using System.Security.Cryptography;
using System.Windows.Media.Animation;

namespace CategoryBar.Theming;

internal static class ThemeResourceLoader
{
    internal static ResourceDictionary Load(ThemeDefinition theme)
    {
        theme.Validate();
        try
        {
            var shaderNames = new Dictionary<string, string>();
            foreach (var (id, encoded) in theme.Shaders)
            {
                var bytes = Convert.FromBase64String(encoded);
                var key = Convert.ToHexString(SHA256.HashData(bytes));
                if (!ThemeShaderEffect.Available.ContainsKey(key))
                {
                    using var stream = new MemoryStream(bytes);
                    var shader = new PixelShader(); shader.SetStreamSource(stream); shader.Freeze(); ThemeShaderEffect.Available.Add(key, shader);
                }
                shaderNames.Add(id, key);
            }
            if (theme.StyleResources.Length == 0) return new ResourceDictionary();
            var markup = XDocument.Parse(theme.StyleResources);
            foreach (var effect in markup.Descendants(XName.Get("ThemeShaderEffect", ThemeMarkup.Effects)))
            {
                var shader = effect.Attribute("Shader");
                if (shader is null || !shaderNames.TryGetValue(shader.Value, out var resolved)) throw new InvalidDataException("Shader is not included in this theme.");
                shader.Value = resolved;
            }
            using var reader = XmlReader.Create(new StringReader(markup.ToString()), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            var result = (ResourceDictionary)XamlReader.Load(reader);
            // Force deferred resources to materialize now, before persisting or applying a theme.
            foreach (var key in result.Keys.Cast<object>().ToArray())
            {
                var value = result[key];
                if (key is not string name) continue;
                if (name.StartsWith("Theme.Parameter.", StringComparison.Ordinal))
                    throw new InvalidDataException("Theme.Parameter resources are generated from Parameters and cannot be overridden by markup.");
                var target = name switch
                {
                    "Theme.WindowTemplate" or "Theme.WorkspaceTemplate" or "Theme.SettingTemplate" or "Theme.SettingStackedTemplate" or "Theme.CaptionTemplate" => typeof(ThemeSurface),
                    "Theme.ButtonTemplate" or "Theme.PrimaryButtonTemplate" or "Navigation.Template" or "Button.IconTemplate" => typeof(Button),
                    "Theme.InputTemplate" => typeof(TextBox),
                    "Theme.MaterialTemplate" or "Theme.CardTemplate" or "Theme.BarTemplate" or "Theme.TabTemplate" or "Theme.MenuTemplate" or "Theme.MenuItemTemplate"
                        or "Theme.SidebarTemplate" or "Theme.TitleBarTemplate" or "Theme.PageTemplate" or "Theme.ToastTemplate" => typeof(ContentControl),
                    _ => null
                };
                if (target is not null && (value is not ControlTemplate template || !template.TargetType.IsAssignableFrom(target)))
                    throw new InvalidDataException(name + " must be a ControlTemplate for " + target.Name + ".");
                if (ThemeDefinition.Tokens.Contains(name) && value is not Brush)
                    throw new InvalidDataException(name + " must be a Brush.");
                if (name.StartsWith("Theme.", StringComparison.Ordinal) && (name.EndsWith(".Enter", StringComparison.Ordinal) || name.EndsWith(".Exit", StringComparison.Ordinal) || name.EndsWith(".Switch", StringComparison.Ordinal) || name.EndsWith(".Expand", StringComparison.Ordinal) || name.EndsWith(".Collapse", StringComparison.Ordinal)))
                {
                    if (value is not Storyboard transition) throw new InvalidDataException(name + " must be a Storyboard.");
                    ValidateTransition(transition);
                }
            }
            if (result.Contains("Theme.ButtonTemplate") && !result.Contains("Theme.PrimaryButtonTemplate")) result["Theme.PrimaryButtonTemplate"] = result["Theme.ButtonTemplate"];
            return result;
        }
        catch (Exception ex) when (ex is XamlParseException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("Cannot load theme resources: " + ex.Message, ex); }
    }

    private static void ValidateTransition(Timeline timeline)
    {
        if (timeline.RepeatBehavior == RepeatBehavior.Forever || timeline.Duration == Duration.Forever
            || timeline.RepeatBehavior.HasCount && timeline.RepeatBehavior.Count > 4
            || timeline.RepeatBehavior.HasDuration && timeline.RepeatBehavior.Duration.TotalSeconds > 3
            || timeline.Duration.HasTimeSpan && timeline.Duration.TimeSpan.TotalSeconds > 3
            || timeline.BeginTime is { TotalSeconds: > 3 })
            throw new InvalidDataException("Lifecycle transitions must be finite and short (at most 3 seconds).");
        if (timeline is TimelineGroup group) foreach (var child in group.Children) ValidateTransition(child);
    }
}
