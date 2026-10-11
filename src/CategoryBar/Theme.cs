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
    internal static Color WindowBaseColor { get; private set; }
    internal static ThemeStore Library { get; private set; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsCategoryBar", "Themes"));
    internal static ThemeDefinition Current { get; private set; } = BuiltInThemes.All[0];
    internal static ThemeStyleSheet StyleSheet { get; private set; } = ThemeStyleSheet.Parse("");
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
    // A shader sampler has to be an image; one clear pixel stands in until a window has a real picture.
    private static readonly ImageBrush NoBackdrop = Blank();
    private static ImageBrush Blank()
    {
        var brush = new ImageBrush(System.Windows.Media.Imaging.BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, new byte[4], 4));
        brush.Freeze(); return brush;
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
        var contrast = SystemParameters.HighContrast;
        // High contrast keeps the plain composition and the system colours instead of authored decoration.
        var authored = contrast ? new ResourceDictionary() : Theming.ThemeResourceLoader.Load(requested);
        Theming.ThemeMotion.FinishAll();
        var resources = Application.Current.Resources;
        foreach (var key in authoredKeys) resources.Remove(key);
        authoredKeys.Clear();
        foreach (var key in parameterKeys) resources.Remove(key);
        IsDark = settings.ThemeMode == "System" ? WindowsUsesDarkMode() : settings.ThemeMode == "Dark";
        Current = requested;
        Scale = settings.InterfaceScale;
        using (var personalization = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
            TransparencyEnabled = !contrast && personalization?.GetValue("EnableTransparency") is not 0;
        (StyleSheet, Accent, WindowBaseColor, parameterKeys) = Fill(resources, Current, settings, IsDark);

        // Overlay windows replace these with the blurred wallpaper behind them when the theme asks for one.
        resources["Theme.Backdrop"] = NoBackdrop; resources["Theme.Backdrop.Enabled"] = 0d;
        resources["Font"] = new FontFamily(settings.FontName + ", Segoe UI Variable Text, Segoe UI");
        var scale = new ScaleTransform(settings.InterfaceScale, settings.InterfaceScale); scale.Freeze();
        resources["Scale"] = scale;
        if (authoredResources is not null) resources.MergedDictionaries.Remove(authoredResources);
        authoredResources = authored;
        resources.MergedDictionaries.Add(authoredResources);
        foreach (var key in authoredResources.Keys.Cast<object>().ToArray()) { resources[key] = authoredResources[key]; authoredKeys.Add(key); }
        Changed?.Invoke();
    }

    /// <summary>
    /// Writes everything a theme's templates read into <paramref name="resources"/>: palette, accent, parameters and
    /// the stylesheet's resources. The application uses it for itself, and the gallery for each isolated preview.
    /// </summary>
    internal static (ThemeStyleSheet Sheet, Color Accent, Color WindowBase, IReadOnlyList<string> ParameterKeys) Fill(ResourceDictionary resources, ThemeDefinition theme, Settings settings, bool dark)
    {
        var contrast = SystemParameters.HighContrast;
        var overridden = settings.ThemeColors.TryGetValue(theme.Id, out var colors)
            ? colors.Keys.Where(k => k.StartsWith(dark ? "Dark:" : "Light:", StringComparison.Ordinal)).Select(k => k[(k.IndexOf(':') + 1)..]).ToArray() : [];
        var sheet = contrast ? BuiltInThemes.All[0].Sheet() : theme.Sheet(settings, ThemeStyleSheet.SurfaceColorRules(overridden));
        var palette = Palette(settings, theme, dark);
        var raw = settings.AccentMode == "System" ? WindowsAccent() : Parse(settings.AccentMode == "Theme" ? palette["Accent"] : settings.AccentColor);
        var accent = Readable(raw, dark);

        var tint = settings.TintSurfaces ? (dark ? .12 : .08) : 0;
        Color Surface(string key) => Mix(Parse(palette[key]), raw, tint);
        var ink = dark ? Colors.White : Colors.Black;
        var translucent = (byte)Math.Round((TransparencyEnabled ? settings.SurfaceOpacity : 1) * 255);
        Color Glass(Color c) => Color.FromArgb(translucent, c.R, c.G, c.B);

        var parameters = Theming.ThemeParameterResources.Apply(resources, theme, settings);
        void Brush(string key, Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); resources[key] = brush; }

        foreach (var key in ThemeDefinition.Tokens.Where(k => k != "Accent"))
            Brush(key, key.EndsWith(".Bg", StringComparison.Ordinal) ? Surface(key) : Parse(palette[key]));
        // The bar and its menus are see-through overlays; ordinary windows only when a system backdrop shows behind them.
        Brush("Bar.Bg", Glass(Surface("Bar.Bg")));
        Brush("Menu.Bg", Glass(Surface("Menu.Bg")));
        if (theme.Backdrop != "None" && TransparencyEnabled) Brush("Window.Bg", Glass(Surface("Window.Bg")));
        Brush("Divider", Alpha(ink, dark ? .07 : .08));
        Brush("Hover", Alpha(ink, dark ? .07 : .06));
        Brush("Pressed", Alpha(ink, dark ? .12 : .10));
        Brush("Selected", Alpha(ink, dark ? .09 : .07));
        Brush("Accent", accent);
        Brush("Accent.Soft", Alpha(accent, dark ? .20 : .16));
        Brush("Accent.Text", Luminance(accent) > .45 ? Parse("#14141A") : Colors.White);
        // The accent exactly as chosen, for themes that fill shapes with it instead of colouring text.
        Brush("Accent.Fill", raw);
        Brush("Accent.FillText", Luminance(raw) > .45 ? Parse("#14141A") : Colors.White);
        // Plain colours for gradient stops and glows, which cannot take a brush.
        resources["Accent.Color"] = accent; resources["Accent.Fill.Color"] = raw;
        resources["Text.Color"] = Parse(palette["Text"]); resources["Surface.Border.Color"] = Parse(palette["Surface.Border"]);
        Brush("Danger", Parse(dark ? "#F0616D" : "#C42B3A"));
        Brush("Warning", Parse(dark ? "#F2A541" : "#B36B00"));
        Brush("Success", Parse(dark ? "#5CC98A" : "#1F8A4C"));
        Brush("Tooltip.Bg", Parse(dark ? "#F2F2F4" : "#2A2A30"));
        Brush("Tooltip.Fg", Parse(dark ? "#1C1C1F" : "#F2F2F4"));
        if (contrast)
        {
            foreach (var key in ThemeDefinition.Tokens.Where(k => k.EndsWith(".Bg", StringComparison.Ordinal))) Brush(key, SystemColors.WindowColor);
            foreach (var key in new[] { "Text", "Text.Muted", "Text.Faint", "Surface.Border", "Divider" }) Brush(key, SystemColors.WindowTextColor);
            foreach (var key in new[] { "Accent", "Accent.Soft", "Accent.Fill", "Selected", "Hover", "Pressed" }) Brush(key, SystemColors.HighlightColor);
            Brush("Accent.Text", SystemColors.HighlightTextColor); Brush("Accent.FillText", SystemColors.HighlightTextColor);
        }
        var shadow = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Direction = 270, Color = Colors.Black, Opacity = dark ? .45 : .22 };
        shadow.Freeze();
        resources["Shadow"] = shadow;
        ThemeStyleResources.Apply(resources, sheet, RadiusScale(settings, theme));
        return (sheet, accent, Surface("Window.Bg"), parameters);
    }

    /// <summary>The user's corner setting relative to the theme's own, applied to every radius of its stylesheet.</summary>
    internal static double RadiusScale(Settings settings, ThemeDefinition theme) =>
        theme.RadiusScale * (theme.CornerRadius == 0 ? 1 : (double)settings.CornerRadius / theme.CornerRadius);

    internal static Dictionary<string, string> Palette(Settings settings, ThemeDefinition theme, bool dark)
    {
        var palette = new Dictionary<string, string>(dark ? theme.Dark : theme.Light);
        if (settings.ThemeColors.TryGetValue(theme.Id, out var overrides))
            foreach (var token in ThemeDefinition.Tokens)
                if (overrides.TryGetValue((dark ? "Dark:" : "Light:") + token, out var color) && Settings.IsHexColor(color)) palette[token] = color;
        return palette;
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

    /// <summary>The current theme with the user's tuning written in as its new defaults, ready to share.</summary>
    internal static ThemeDefinition Capture(Settings settings)
    {
        var copy = Current.Copy();
        foreach (var parameter in copy.Parameters) parameter.Default = parameter.Resolve(settings, Current.Id);
        copy.Id = "custom-" + Guid.NewGuid().ToString("N");
        // The stylesheet keeps its parameters; overridden surface colours are appended as rules that win the cascade.
        if (settings.ThemeColors.TryGetValue(Current.Id, out var colors) && ThemeStyleSheet.SurfaceColorRules(colors.Keys.Select(key => key[(key.IndexOf(':') + 1)..])) is { Length: > 0 } rules)
            copy.StyleSheet += "\n" + rules;
        copy.RadiusScale = Math.Clamp(RadiusScale(settings, Current), 0, 4);
        copy.CornerRadius = settings.CornerRadius; copy.SurfaceOpacity = settings.SurfaceOpacity; copy.FontName = settings.FontName;
        foreach (var (dark, palette) in new[] { (true, copy.Dark), (false, copy.Light) })
        {
            foreach (var (key, value) in Palette(settings, Current, dark)) palette[key] = value;
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
            // Desktop: a new wallpaper changes what glass surfaces show behind themselves.
            if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color or UserPreferenceCategory.VisualStyle or UserPreferenceCategory.Desktop) || pending) return;
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
