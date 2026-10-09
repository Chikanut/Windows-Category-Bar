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
        IsDark = settings.ThemeMode == "System" ? WindowsUsesDarkMode() : settings.ThemeMode == "Dark";
        var raw = settings.AccentMode == "System" ? WindowsAccent() : Parse(settings.AccentColor);
        Accent = Readable(raw, IsDark);
        Scale = settings.InterfaceScale;

        var tint = settings.TintSurfaces ? (IsDark ? .12 : .08) : 0;
        Color Surface(string dark, string light) => Mix(Parse(IsDark ? dark : light), raw, tint);
        var ink = IsDark ? Colors.White : Colors.Black;
        var translucent = (byte)Math.Round(settings.SurfaceOpacity * 255);
        Color Glass(Color c) => Color.FromArgb(translucent, c.R, c.G, c.B);

        var resources = Application.Current.Resources;
        void Brush(string key, Color color) { var brush = new SolidColorBrush(color); brush.Freeze(); resources[key] = brush; }

        Brush("Bar.Bg", Glass(Surface("#26262C", "#F4F4F7")));
        Brush("Menu.Bg", Glass(Surface("#24242A", "#FAFAFC")));
        Brush("Popup.Bg", Surface("#2C2C32", "#FFFFFF"));
        Brush("Window.Bg", Surface("#1B1B1E", "#F3F3F5"));
        Brush("Sidebar.Bg", Surface("#202024", "#E9E9EE"));
        Brush("Card.Bg", Surface("#26262B", "#FFFFFF"));
        Brush("Control.Bg", Surface("#2E2E34", "#FFFFFF"));
        Brush("Text", Parse(IsDark ? "#ECECEF" : "#1C1C1F"));
        Brush("Text.Muted", Parse(IsDark ? "#A6A6B0" : "#55555E"));
        Brush("Text.Faint", Parse(IsDark ? "#8E8E98" : "#777782"));
        Brush("Surface.Border", Alpha(ink, IsDark ? .10 : .13));
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

        double radius = settings.CornerRadius;
        resources["Radius"] = new CornerRadius(radius);
        resources["Radius.Small"] = new CornerRadius(Math.Min(radius, 6));
        resources["Radius.Top"] = new CornerRadius(Math.Min(radius, 9), Math.Min(radius, 9), 0, 0);
        resources["Font"] = new FontFamily(settings.FontName + ", Segoe UI Variable Text, Segoe UI");
        var scale = new ScaleTransform(settings.InterfaceScale, settings.InterfaceScale); scale.Freeze();
        resources["Scale"] = scale;
        var shadow = new DropShadowEffect { BlurRadius = 22, ShadowDepth = 5, Direction = 270, Color = Colors.Black, Opacity = IsDark ? .45 : .22 };
        shadow.Freeze();
        resources["Shadow"] = shadow;
        Changed?.Invoke();
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
