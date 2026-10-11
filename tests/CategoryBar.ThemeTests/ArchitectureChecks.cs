using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Theming;
using CategoryBar.Views;

internal static class ArchitectureChecks
{
    private const string Header = "<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:fx=\"clr-namespace:CategoryBar.Theming;assembly=CategoryBar\">";
    internal static void Run(Application app, string folder, string configPath)
    {
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
        var workspaceLayouts = new HashSet<string>();
        foreach (var theme in BuiltInThemes.All)
        {
            var exported = ThemeFiles.Read(Path.Combine("Themes", "Examples", theme.Id + ThemeFiles.Extension));
            Check(exported.StyleResources.ReplaceLineEndings("\n") == theme.StyleResources.ReplaceLineEndings("\n") && exported.StyleSheet.ReplaceLineEndings("\n") == theme.StyleSheet.ReplaceLineEndings("\n") && exported.Shaders.SequenceEqual(theme.Shaders), theme.Id + " example matches shipped layout, motion and shaders");
            foreach (var dark in new[] { true, false })
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
            {
                var config = new Configuration(); Theme.Select(config.Settings, theme); config.Settings.ThemeMode = dark ? "Dark" : "Light"; config.Settings.InterfaceScale = scale;
                Theme.Apply(config.Settings);
                workspaceLayouts.Add(XamlWriter.Save(app.Resources["Theme.WorkspaceTemplate"]));
                var window = new SettingsWindow(new Session(config, new ConfigurationStore(configPath)));
                window.Navigate("Appearance");
                var root = (FrameworkElement)window.Content;
                var width = 900 * scale; var height = 640 * scale;
                root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
                var texts = Descendants(root).OfType<TextBlock>().ToArray();
                if (texts.Length < 15 || texts.Any(t => !double.IsFinite(t.ActualWidth))) throw new Exception("Missing or invalid gallery content at " + scale);
                if (scale == 1)
                {
                    var bitmap = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(Path.Combine(folder, theme.Id + (dark ? "-dark" : "-light") + "-gallery.png")); encoder.Save(stream);
                }
                window.Close();
            }
        }
        Check(workspaceLayouts.Count == 4, "four authored workspace compositions; gallery renders at 100/125/150/200%, light and dark");

        Theme.Apply(new Settings());
        var tab = new ContentControl { Content = "Category", Width = 160, Height = 40 }.Res(FrameworkElement.StyleProperty, "Theme.Tab");
        tab.ApplyTemplate(); tab.Measure(new Size(160, 40)); tab.Arrange(new Rect(0, 0, 160, 40)); Pump(20);
        var indicator = (Border)tab.Template.FindName("indicator", tab);
        Check(indicator.Opacity == 0, "idle tab does not show a selected/running indicator");
        ThemeVisual.SetIsSelected(tab, true); Pump(20);
        Check(indicator.Opacity == 1, "semantic selection drives template indicator without code painting");
        ThemeVisual.SetIsSelected(tab, false); ThemeVisual.SetIsRunning(tab, true); Pump(20);
        Check(indicator.Opacity > 0 && indicator.Width == 10, "running state remains distinct from selection");

        var testTheme = BuiltInThemes.All[0].Copy(); testTheme.Id = "architecture-test";
        testTheme.StyleResources = Header + "<SolidColorBrush x:Key=\"Test.Sentinel\" Color=\"Red\"/><ControlTemplate x:Key=\"Theme.MaterialTemplate\" TargetType=\"ContentControl\"><Ellipse Fill=\"{TemplateBinding Background}\"/></ControlTemplate><Style x:Key=\"Theme.Text.Heading\" TargetType=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"27\"/></Style></ResourceDictionary>";
        Theme.Library.Remove(testTheme.Id); Theme.Library.Install(ThemeFiles.Parse(ThemeFiles.Serialize(testTheme)));
        var settings = new Settings(); Theme.Select(settings, testTheme); Theme.Apply(settings);
        var heading = Ui.Text("Live heading", 22);
        var card = new ContentControl { Content = "Foreground stays readable" }.Res(FrameworkElement.StyleProperty, "Theme.Card");
        var liveWindow = new Window { Content = new StackPanel { Children = { heading, card } } };
        card.ApplyTemplate(); card.Measure(new Size(240, 80)); card.Arrange(new Rect(0, 0, 240, 80));
        Check(Descendants(card).OfType<System.Windows.Shapes.Ellipse>().Any() && heading.FontSize == 27, "portable material replaces geometry and typography through public contracts");
        Theme.Apply(new Settings()); card.ApplyTemplate(); card.Measure(new Size(240, 80)); card.Arrange(new Rect(0, 0, 240, 80)); card.UpdateLayout(); Pump(20);
        Check(!app.Resources.Contains("Test.Sentinel") && heading.FontSize == 22 && !Descendants(card).OfType<System.Windows.Shapes.Ellipse>().Any(), "switching away restores existing controls and removes authored resources");
        liveWindow.Close();
        Theme.Library.Remove(testTheme.Id);

        bool Reject(string markup)
        {
            testTheme.StyleResources = Header + markup + "</ResourceDictionary>";
            try { ThemeResourceLoader.Load(testTheme); return false; } catch (InvalidDataException) { return true; }
        }
        Check(Reject("<ControlTemplate x:Key=\"Theme.WindowTemplate\" TargetType=\"Button\"><Border/></ControlTemplate>"), "wrong template target rejected before installation");
        Check(Reject("<Storyboard x:Key=\"Theme.Menu.Exit\" RepeatBehavior=\"Forever\"><DoubleAnimation Storyboard.TargetProperty=\"Opacity\" To=\"0\" Duration=\"0:0:1\"/></Storyboard>"), "infinite lifecycle transition rejected before installation");

        if (ThemeMotion.Enabled)
        {
            var host = new Border(); var stale = 0; var finished = 0;
            ThemeMotion.Play(host, "Theme.Menu.Exit", () => stale++, exit: true);
            ThemeMotion.Play(host, "Theme.Menu.Enter", () => finished++);
            Pump(400);
            Check(stale == 0 && finished == 1 && host.Opacity == 1 && !host.HasAnimatedProperties, "reopening cancels stale exit callback and releases completed clocks");
            ThemeMotion.Play(host, "Theme.Menu.Exit", () => stale++, exit: true);
            host.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent)); Pump(200);
            Check(stale == 0 && !host.HasAnimatedProperties, "unload cancels transitions without stale callbacks");
        }
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i); yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }
    private static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
}
