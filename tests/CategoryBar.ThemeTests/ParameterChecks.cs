using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Theming;
using CategoryBar.Views;

internal static class ParameterChecks
{
    internal static void Run(Application app, string folder)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var glass = BuiltInThemes.All.Single(t => t.Id == "glass");
        var settings = new Settings(); Theme.Select(settings, glass);
        glass.Parameters.Single(p => p.Id == "specular").Set(settings, glass.Id, "0.3");
        glass.Parameters.Single(p => p.Id == "motion").Set(settings, glass.Id, "false");
        glass.Parameters.Single(p => p.Id == "rim-color").Set(settings, glass.Id, "#D04080");
        glass.Parameters.Single(p => p.Id == "roundness").Set(settings, glass.Id, "10");
        Theme.Apply(settings);
        var material = new ContentControl { Background = Brushes.SlateGray, Tag = new CornerRadius(12), Width = 240, Height = 80 }
            .Res(Control.TemplateProperty, "Theme.MaterialTemplate");
        var window = new Window { Content = material };
        material.ApplyTemplate(); material.Measure(new Size(240, 80)); material.Arrange(new Rect(0, 0, 240, 80)); material.UpdateLayout();
        var face = Descendants(material).OfType<Border>().First(border => border.Effect is ThemeShaderEffect);
        Check(face.Effect is ThemeShaderEffect { P0: .3, P2: 240, P3: 80 }, "theme number and element size drive live GPU shader constants");
        Check((Visibility)app.Resources["Theme.Parameter.motion.Visibility"] == Visibility.Collapsed
            && ((SolidColorBrush)app.Resources["Theme.Parameter.rim-color"]).Color == Theme.Parse("#D04080"), "toggle and color parameters produce typed resources");
        Check(((CornerRadius)app.Resources["tab.Radius"]).TopLeft == 10 && ((CornerRadius)app.Resources["bar.Radius"]).TopLeft == 10, "a parameter used by the stylesheet reshapes the bar and its tabs");
        var silent = new Border(); var completedAtOnce = false;
        ThemeMotion.Play(silent, "Theme.Menu.Enter", () => completedAtOnce = true);
        Check(completedAtOnce && !silent.HasAnimatedProperties, "the theme's motion toggle switches lifecycle transitions off");
        var exported = ThemeFiles.Parse(ThemeFiles.Serialize(Theme.Capture(settings)));
        Check(exported.Parameters.Single(p => p.Id == "specular").Default == "0.3" && exported.Parameters.Single(p => p.Id == "motion").Default == "false"
            && exported.Sheet().Number("tab", "border-radius", 0) == 10, "export captures current per-theme tuning as portable defaults");
        Theme.Select(settings, BuiltInThemes.All[0]); Theme.Apply(settings);
        Check(!app.Resources.Contains("Theme.Parameter.specular"), "switching themes removes previous parameter resources");
        Theme.Select(settings, glass); Theme.Apply(settings);
        Check((double)app.Resources["Theme.Parameter.specular"] == .3, "returning to a theme restores its independent tuning");
        settings.ThemeParameters.Remove(glass.Id); Theme.Apply(settings);
        Check((double)app.Resources["Theme.Parameter.specular"] == .2, "reset restores author defaults");
        material.ApplyTemplate(); material.UpdateLayout();
        Check(app.Resources["bar.Backdrop"] is true && (double)app.Resources["menu.BackdropBlur"] == 10 && (double)app.Resources["Theme.Backdrop.Enabled"] == 0
            && Descendants(material).OfType<Border>().First(border => border.Effect is ThemeShaderEffect).Effect is ThemeShaderEffect { Auxiliary: ImageBrush, P5: 0 }, "glass asks for a backdrop and stays plain glass until a window supplies one");
        glass.Parameters.Single(p => p.Id == "backdrop").Set(settings, glass.Id, "none"); Theme.Apply(settings);
        Check(app.Resources["bar.Backdrop"] is false, "the user can switch the backdrop off");
        settings.ThemeParameters.Remove(glass.Id); Theme.Apply(settings);
        var flat = new byte[40 * 20 * 4]; for (var i = 0; i < flat.Length; i += 4) { flat[i] = 200; flat[i + 1] = 100; flat[i + 2] = 50; flat[i + 3] = 255; }
        flat[4 * (40 * 10 + 20)] = 0;
        var (soft, softWidth, softHeight) = CategoryBar.Interop.ScreenBackdrop.SoftenAndShrink(flat, 40, 20, 160, 12);
        Check(softWidth == 10 && softHeight == 5 && soft.Length == 200 && soft[1] == 100 && soft[2] == 50 && soft[0] is > 190 and < 200 && soft[3] == 255, "backdrop pictures are shrunk and blurred without darkening their edges");
        window.Close();

        var expressive = BuiltInThemes.All.Single(t => t.Id == "expressive");
        Theme.Select(settings, expressive);
        expressive.Parameters.Single(p => p.Id == "navigation").Set(settings, expressive.Id, "left"); Theme.Apply(settings);
        var workspace = new ThemeSurface { Header = "Navigation", Content = "Page" }.Res(FrameworkElement.StyleProperty, "Theme.Workspace");
        workspace.ApplyTemplate();
        Check(ReferenceEquals(workspace.Template, app.Resources["Expressive.RailWorkspace"]), "choice parameter switches the workspace composition");
        if (ThemeMotion.Enabled)
        {
            expressive.Parameters.Single(p => p.Id == "motion-style").Set(settings, expressive.Id, "smooth"); Theme.Apply(settings);
            var smooth = new Border(); ThemeMotion.Play(smooth, "Theme.Menu.Enter");
            Check(ThemeTransform.GetScaleY(smooth) == 1 && smooth.HasAnimatedProperties, "motion-style picks the theme's alternative transition");
            ThemeMotion.Stop(smooth);
        }
        var brutal = BuiltInThemes.All.Single(t => t.Id == "brutal");
        Theme.Select(settings, brutal); settings.ThemeMode = "Light";
        brutal.Parameters.Single(p => p.Id == "shadow").Set(settings, brutal.Id, "6");
        brutal.Parameters.Single(p => p.Id == "shadow-tone").Set(settings, brutal.Id, "accent-fill"); Theme.Apply(settings);
        var hard = (System.Windows.Media.Effects.DropShadowEffect)app.Resources["card.Shadow"];
        Check(hard.BlurRadius == 0 && Math.Abs(hard.ShadowDepth - Math.Sqrt(72)) < .001 && hard.Color == ((SolidColorBrush)app.Resources["Accent.Fill"]).Color, "number and choice parameters compose a hard shadow in the stylesheet");
        Check(((Thickness)app.Resources["bar.Frame"]).Bottom == 6 && ((Thickness)app.Resources["menu.Frame"]).Bottom == 14, "layout rules place the bar above the taskbar and the menu above the bar");
        Theme.Select(settings, glass); settings.ThemeMode = "Dark"; Theme.Apply(settings);
        var config = new Configuration { Settings = settings };
        var configPath = Path.Combine(folder, "parameter-ui-config.json");
        var editor = new SettingsWindow(new Session(config, new ConfigurationStore(configPath)));
        editor.Navigate("Appearance");
        var root = (FrameworkElement)editor.Content;
        root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout();
        var slider = Descendants(root).OfType<Slider>().Single(control => System.Windows.Automation.AutomationProperties.GetName(control) == L.T("Світло на краях"));
        slider.Value = .4;
        slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Check(Math.Abs((double)app.Resources["Theme.Parameter.specular"] - .4) < .00001
            && Math.Abs(double.Parse(new ConfigurationStore(configPath).Load().Settings.ThemeParameters[glass.Id]["specular"], System.Globalization.CultureInfo.InvariantCulture) - .4) < .00001,
            "generated slider applies and persists authored parameter through the real settings flow");
        root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout();
        var scroller = Descendants(root).OfType<ScrollViewer>().First(view => view.ScrollableHeight > 400);
        scroller.ScrollToVerticalOffset(520); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(900, 640, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using (var stream = File.Create(Path.Combine(folder, "glass-parameters.png"))) encoder.Save(stream);
        editor.Close();
        Theme.Apply(new Settings());
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
}
