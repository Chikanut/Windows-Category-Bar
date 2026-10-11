using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace CategoryBar;

/// <summary>
/// Turns the appearance settings and the current Windows colours into application-wide resources.
/// Every surface refers to them dynamically, so a change is visible immediately in all open windows.
/// </summary>
internal static class Theme
{
    internal static bool IsDark { get; private set; } = true;
    internal static Color Accent { get; private set; } = Parse("#8EA8FF");
    internal static double Scale { get; private set; } = 1;
    internal static bool TransparencyEnabled { get; private set; }
    internal static ThemeStore Library { get; private set; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsCategoryBar", "Themes"));
    internal static ThemeDefinition Current { get; private set; } = BuiltInThemes.All[0];
    internal static ThemeStyleSheet StyleSheet { get; private set; } = ThemeStyleSheet.Parse("");
    private static ResourceDictionary? templates;
    private static ResourceDictionary? authoredResources;
    private static readonly HashSet<object> authoredKeys = [];
    private static IReadOnlyList<string> parameterKeys = [];
    internal static void Initialize(string configPath)
    {
        Library = new ThemeStore(Path.Combine(Path.GetDirectoryName(configPath)!, "Themes"));
        Library.Load();
        foreach (var theme in Library.Custom.ToArray())
        {
            try { _ = Theming.ThemeResourceLoader.Load(theme); }
            catch (InvalidDataException ex) { Library.LoadErrors.Add(theme.Name + ": " + ex.Message); Library.Custom.Remove(theme); }
        }
    }
    internal static event Action? Changed;

    internal static readonly string[] AccentPresets =
        ["#8EA8FF", "#5B8DEF", "#3FB8AF", "#5CC98A", "#E2B93B", "#F2A541", "#F0616D", "#E667AF", "#B48CF2", "#9AA0A6"];

    internal static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);
    internal static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    /// <summary>Follows the Windows mode used by the taskbar, since the bar sits on top of it.</summary>
    internal static bool WindowsUsesDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("SystemUsesLightTheme") is not int light || light == 0;
    }

    internal static Color WindowsAccent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM");
        if (key?.GetValue("AccentColor") is int abgr)
            return Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF));
        var glass = SystemParameters.WindowGlassColor;
        return Color.FromRgb(glass.R, glass.G, glass.B);
    }

    internal static void Apply(Settings settings)
    {
        var requested = Library.Resolve(settings.ThemeId);
        var authored = SystemParameters.HighContrast ? new ResourceDictionary() : Theming.ThemeResourceLoader.Load(requested);
        Theming.ThemeMotion.FinishAll();
        foreach (var key in authoredKeys) Application.Current.Resources.Remove(key);
        authoredKeys.Clear();
        IsDark = settings.ThemeMode == "System" ? WindowsUsesDarkMode() : settings.ThemeMode == "Dark";
        Current = Library.Resolve(settings.ThemeId);
        StyleSheet = ThemeStyleSheet.Parse(SystemParameters.HighContrast ? BuiltInThemes.All[0].StyleSheet : Current.StyleSheet);
        var palette = IsDark ? Current.Dark : Current.Light;
        var raw = settings.AccentMode == "System" ? WindowsAccent() : Parse(settings.AccentMode == "Theme" ? palette["Accent"] : settings.AccentColor);
        Accent = Readable(raw, IsDark);
        Scale = settings.InterfaceScale;

        var tint = settings.TintSurfaces ? (IsDark ? .12 : .08) : 0;
        Color Surface(string key) => Mix(Parse(palette[key]), raw, tint);
        var ink = IsDark ? Colors.White : Colors.Black;
        using var personalization = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        var transparency = !SystemParameters.HighContrast && personalization?.GetValue("EnableTransparency") is not 0;
        TransparencyEnabled = transparency;
        var translucent = (byte)Math.Round((transparency ? settings.SurfaceOpacity : 1) * 255);
        Color Glass(Color c) => Color.FromArgb(translucent, c.R, c.G, c.B);

        var resources = Application.Current.Resources;
        foreach (var key in parameterKeys) resources.Remove(key);
        parameterKeys = Theming.ThemeParameterResources.Apply(resources, Current, settings);
        void Brush(string key, Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); resources[key] = brush; }

        foreach (var key in ThemeDefinition.Tokens.Where(k => k != "Accent"))
            Brush(key, key.EndsWith(".Bg", StringComparison.Ordinal) ? Surface(key) : Parse(palette[key]));
        Brush("Bar.Bg", Glass(Surface("Bar.Bg")));
        Brush("Menu.Bg", Glass(Surface("Menu.Bg")));
        if (Current.Backdrop != "None" && transparency) Brush("Window.Bg", Glass(Surface("Window.Bg")));
        if (Current.Material == "Glass" && transparency)
        {
            foreach (var key in new[] { "Bar.Bg", "Menu.Bg" })
            {
                var color = Surface(key);
                var top = Mix(color, Colors.White, IsDark ? .16 : .45);
                var brush = new LinearGradientBrush(key == "Card.Bg" ? top : Glass(top), key == "Card.Bg" ? color : Glass(color), 90);
                brush.Freeze(); resources[key] = brush;
            }
        }
        Brush("Divider", Alpha(ink, IsDark ? .07 : .08));
        Brush("Hover", Alpha(ink, IsDark ? .07 : .06));
        Brush("Pressed", Alpha(ink, IsDark ? .12 : .10));
        Brush("Selected", Alpha(ink, IsDark ? .09 : .07));
        Brush("Accent", Accent);
        Brush("Accent.Soft", Alpha(Accent, IsDark ? .20 : .16));
        Brush("Accent.Text", Luminance(Accent) > .45 ? Parse("#14141A") : Colors.White);
        Brush("Danger", Parse(IsDark ? "#F0616D" : "#C42B3A"));
        Brush("Warning", Parse(IsDark ? "#F2A541" : "#B36B00"));
        Brush("Success", Parse(IsDark ? "#5CC98A" : "#1F8A4C"));
        Brush("Tooltip.Bg", Parse(IsDark ? "#F2F2F4" : "#2A2A30"));
        Brush("Tooltip.Fg", Parse(IsDark ? "#1C1C1F" : "#F2F2F4"));
        Brush("Glass.Rim", SystemParameters.HighContrast ? Colors.Transparent : Alpha(Colors.White, IsDark ? .18 : .65));
        var sheen = new LinearGradientBrush(SystemParameters.HighContrast ? Colors.Transparent : Alpha(Colors.White, IsDark ? .16 : .45), Colors.Transparent, 90);
        sheen.Freeze(); resources["Glass.Sheen"] = sheen;
        if (SystemParameters.HighContrast)
        {
            foreach (var key in ThemeDefinition.Tokens.Where(k => k.EndsWith(".Bg", StringComparison.Ordinal))) Brush(key, SystemColors.WindowColor);
            foreach (var key in new[] { "Text", "Text.Muted", "Text.Faint", "Surface.Border", "Divider" }) Brush(key, SystemColors.WindowTextColor);
            foreach (var key in new[] { "Accent", "Accent.Soft", "Selected", "Hover", "Pressed" }) Brush(key, SystemColors.HighlightColor);
            Brush("Accent.Text", SystemColors.HighlightTextColor);
        }

        double radius = settings.CornerRadius;
        resources["Radius"] = new CornerRadius(radius);
        resources["Radius.Small"] = new CornerRadius(Math.Min(radius, radius >= 14 ? 12 : 6));
        resources["Theme.BarBorderThickness"] = new Thickness(Current.BorderWidth, Current.BorderWidth, Current.BorderWidth, 0);
        resources["Radius.Top"] = new CornerRadius(Math.Min(radius, 9), Math.Min(radius, 9), 0, 0);
        resources["Theme.BorderThickness"] = new Thickness(Current.BorderWidth);
        resources["Font"] = new FontFamily(settings.FontName + ", Segoe UI Variable Text, Segoe UI");
        var scale = new ScaleTransform(settings.InterfaceScale, settings.InterfaceScale); scale.Freeze();
        resources["Scale"] = scale;
        var shadow = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Direction = 270, Color = Colors.Black, Opacity = IsDark ? .45 : .22 };
        shadow.Freeze();
        resources["Shadow"] = shadow;
        ThemeStyleResources.Apply(resources, StyleSheet, Current.CornerRadius == 0 ? 1 : (double)settings.CornerRadius / Current.CornerRadius);
        // Each skin provides actual control templates, not just palette substitutions.
        if (templates is not null) resources.MergedDictionaries.Remove(templates);
        if (authoredResources is not null) resources.MergedDictionaries.Remove(authoredResources);
        templates = ThemeStyleResources.Templates(StyleSheet.Skin);
        resources.MergedDictionaries.Add(templates);
        authoredResources = authored;
        resources.MergedDictionaries.Add(authoredResources);
        foreach (var key in authoredResources.Keys.Cast<object>().ToArray()) { resources[key] = authoredResources[key]; authoredKeys.Add(key); }
        Changed?.Invoke();
    }

    internal static void Select(Settings settings, ThemeDefinition theme)
    {
        settings.ThemeId = theme.Id;
        settings.AccentMode = "Theme";
        settings.TintSurfaces = false;
        settings.CornerRadius = theme.CornerRadius;
        settings.SurfaceOpacity = theme.SurfaceOpacity;
        settings.FontName = theme.FontName;
    }

    internal static ThemeDefinition Capture(Settings settings)
    {
        var copy = Current.Copy();
        foreach (var parameter in copy.Parameters) parameter.Default = parameter.Resolve(settings, Current.Id);
        copy.Id = "custom-" + Guid.NewGuid().ToString("N");
        copy.StyleSheet = StyleSheet.WithGeometry(Current.CornerRadius == 0 ? 1 : (double)settings.CornerRadius / Current.CornerRadius);
        copy.CornerRadius = settings.CornerRadius; copy.SurfaceOpacity = settings.SurfaceOpacity; copy.FontName = settings.FontName;
        foreach (var (dark, palette) in new[] { (true, copy.Dark), (false, copy.Light) })
        {
            var raw = settings.AccentMode == "System" ? WindowsAccent() : Parse(settings.AccentMode == "Theme" ? palette["Accent"] : settings.AccentColor);
            if (settings.TintSurfaces)
                foreach (var key in ThemeDefinition.Tokens.Where(k => k.EndsWith(".Bg", StringComparison.Ordinal))) palette[key] = Hex(Mix(Parse(palette[key]), raw, dark ? .12 : .08));
            palette["Accent"] = Hex(raw);
        }
        return copy;
    }

    /// <summary>Raises <see cref="Changed"/>-driven refreshes when Windows switches mode or accent colour.</summary>
    internal static void FollowWindows(Func<Settings> settings)
    {
        var pending = false;
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle) || pending) return;
            pending = true;
            Application.Current.Dispatcher.BeginInvoke(async () =>
            {
                await Task.Delay(250); // Windows sends a burst of notifications for one change.
                pending = false;
                Apply(settings());
            });
        };
    }

    private static Color Alpha(Color c, double opacity) => Color.FromArgb((byte)Math.Round(opacity * 255), c.R, c.G, c.B);
    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R + (b.R - a.R) * amount), (byte)(a.G + (b.G - a.G) * amount), (byte)(a.B + (b.B - a.B) * amount));
    private static double Luminance(Color c)
    {
        static double Channel(byte v) { var s = v / 255.0; return s <= .03928 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
        return .2126 * Channel(c.R) + .7152 * Channel(c.G) + .0722 * Channel(c.B);
    }

    /// <summary>Keeps any accent legible: lighter on dark surfaces, darker on light ones.</summary>
    private static Color Readable(Color accent, bool dark)
    {
        for (var i = 0; i < 12; i++)
        {
            var luminance = Luminance(accent);
            if (dark ? luminance >= .30 : luminance <= .22) break;
            accent = Mix(accent, dark ? Colors.White : Colors.Black, .12);
        }
        return accent;
    }
}
