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
        glass.Parameters.Single(p => p.Id == "refraction").Set(settings, glass.Id, "0.06");
        glass.Parameters.Single(p => p.Id == "show-rim").Set(settings, glass.Id, "false");
        glass.Parameters.Single(p => p.Id == "rim-color").Set(settings, glass.Id, "#D04080");
        Theme.Apply(settings);
        var material = new ContentControl { Background = Brushes.SlateGray, Tag = new CornerRadius(12), Width = 240, Height = 80 }
            .Res(Control.TemplateProperty, "Theme.MaterialTemplate");
        var window = new Window { Content = material };
        material.ApplyTemplate(); material.Measure(new Size(240, 80)); material.Arrange(new Rect(0, 0, 240, 80)); material.UpdateLayout();
        var face = (Border)material.Template.FindName("liquid", material);
        Check(face.Effect is ThemeShaderEffect { P0: .06 }, "theme number drives a live GPU shader constant");
        Check((Visibility)app.Resources["Theme.Parameter.show-rim.Visibility"] == Visibility.Collapsed
            && ((SolidColorBrush)app.Resources["Theme.Parameter.rim-color"]).Color == Theme.Parse("#D04080"), "toggle and color parameters produce typed resources");
        var exported = ThemeFiles.Parse(ThemeFiles.Serialize(Theme.Capture(settings)));
        Check(exported.Parameters.Single(p => p.Id == "refraction").Default == "0.06" && exported.Parameters.Single(p => p.Id == "show-rim").Default == "false", "export captures current per-theme tuning as portable defaults");
        Theme.Select(settings, BuiltInThemes.All[0]); Theme.Apply(settings);
        Check(!app.Resources.Contains("Theme.Parameter.refraction"), "switching themes removes previous parameter resources");
        Theme.Select(settings, glass); Theme.Apply(settings);
        Check((double)app.Resources["Theme.Parameter.refraction"] == .06, "returning to a theme restores its independent tuning");
        settings.ThemeParameters.Remove(glass.Id); Theme.Apply(settings);
        Check((double)app.Resources["Theme.Parameter.refraction"] == .025, "reset restores author defaults");
        window.Close();

        var expressive = BuiltInThemes.All.Single(t => t.Id == "expressive");
        Theme.Select(settings, expressive);
        expressive.Parameters.Single(p => p.Id == "navigation").Set(settings, expressive.Id, "left"); Theme.Apply(settings);
        var workspace = new ThemeSurface { Header = "Navigation", Content = "Page" }.Res(FrameworkElement.StyleProperty, "Theme.Workspace");
        workspace.ApplyTemplate();
        Check(ReferenceEquals(workspace.Template, app.Resources["Expressive.SidebarWorkspace"]), "choice parameter switches the workspace composition");
        Theme.Select(settings, glass); settings.ThemeMode = "Dark"; Theme.Apply(settings);
        var config = new Configuration { Settings = settings };
        var configPath = Path.Combine(folder, "parameter-ui-config.json");
        var editor = new SettingsWindow(new Session(config, new ConfigurationStore(configPath)));
        editor.Navigate("Appearance");
        var root = (FrameworkElement)editor.Content;
        root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout();
        var slider = Descendants(root).OfType<Slider>().Single(control => System.Windows.Automation.AutomationProperties.GetName(control) == L.T("Викривлення відблиску"));
        slider.Value = .055;
        slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.LostMouseCaptureEvent });
        Check(Math.Abs((double)app.Resources["Theme.Parameter.refraction"] - .055) < .00001
            && Math.Abs(double.Parse(new ConfigurationStore(configPath).Load().Settings.ThemeParameters[glass.Id]["refraction"], System.Globalization.CultureInfo.InvariantCulture) - .055) < .00001,
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
