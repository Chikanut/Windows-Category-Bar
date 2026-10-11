using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Views;
using CategoryBar.Theming;
using System.Windows.Media.Animation;
using System.Windows.Threading;

internal static class Program
{
    private sealed class RenderApp : App
    {
        // Pump animation clocks without starting the production app, tray or configuration flow.
        protected override void OnStartup(StartupEventArgs e) { }
    }
    [STAThread]
    private static void Main(string[] args)
    {
        Velopack.VelopackApp.Build().Run();
        var app = new RenderApp { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        foreach (var file in new[] { "Controls", "ThemeControls", "ThemeSurfaces" })
            app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CategoryBar;component/Themes/" + file + ".xaml", UriKind.Relative) });
        var folder = Path.GetFullPath(Path.Combine(".local", "theme-renders")); Directory.CreateDirectory(folder);
        var configPath = Path.Combine(folder, "config.json"); Theme.Initialize(configPath);
        typeof(App).GetProperty("Updates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(app, new CategoryBar.Services.AppUpdates(new Session(new Configuration(), new ConfigurationStore(configPath))));
        if (args.Contains("--lab"))
        {
            Lab.Run(args.Where(a => !a.StartsWith("--")).ToArray());
            if (args.Contains("--showcase")) Lab.Showcase();
            app.Shutdown(); return;
        }
        if (args.Contains("--export-examples"))
        {
            // The shareable single-file form of every built-in theme, kept next to the hand-written examples.
            foreach (var theme in BuiltInThemes.All) ThemeFiles.Write(Path.Combine("Themes", "Examples", theme.Id + ThemeFiles.Extension), theme);
            app.Shutdown(); return;
        }
        MiniatureDragChecks.Run(configPath);
        InteractionChecks.Colors(configPath);
        foreach (var id in new[] { "architecture-test", "native-css-check", "aurora-motion" }) Theme.Library.Remove(id);
        var templateIdentities = new HashSet<ControlTemplate>();
        foreach (var theme in BuiltInThemes.All)
        foreach (var dark in new[] { true, false })
        {
            var config = new Configuration(); Theme.Select(config.Settings, theme); config.Settings.ThemeMode = dark ? "Dark" : "Light";
            Theme.Apply(config.Settings);
            InteractionChecks.Run(config.Settings, configPath);
            var button = Ui.Button("Preview", () => { }); button.ApplyTemplate();
            if (button.Template != app.Resources["Theme.ButtonTemplate"] || button.MinHeight != Theme.StyleSheet.Number("button", "min-height", 30))
                throw new Exception(theme.Name + " did not apply its component stylesheet.");
            if (dark) templateIdentities.Add((ControlTemplate)app.Resources["Theme.TabTemplate"]);
            var session = new Session(config, new ConfigurationStore(configPath));
            var window = new SettingsWindow(session); window.Navigate("General");
            var root = (FrameworkElement)window.Content;
            root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout();
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle((Brush)app.Resources["Window.Bg"], null, new Rect(0, 0, 900, 640));
            var bitmap = new RenderTargetBitmap(900, 640, 96, 96, PixelFormats.Pbgra32); bitmap.Render(background); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var stream = File.Create(Path.Combine(folder, theme.Id + (dark ? "-dark" : "-light") + ".png"))) encoder.Save(stream);
            window.Close();
            Console.WriteLine("PASS: native " + theme.Id + (dark ? " dark" : " light") + " templates, layout and render");
        }
        if (templateIdentities.Count != 4 || BuiltInThemes.All.Select(t => t.StyleSheet).Distinct().Count() != 4) throw new Exception("Theme styles are not independently loaded.");
        Console.WriteLine("PASS: four independent file-defined designs, each with its own tab composition");
        var custom = BuiltInThemes.All[1].Copy(); custom.Id = "native-css-check";
        custom.StyleSheet = ".theme { skin: glass; } .button { min-height: 50px; border-radius: 0px; } .card { border-radius: var(--p-roundness); }";
        var customPath = Path.Combine(folder, "custom.json"); ThemeFiles.Write(customPath, custom);
        Theme.Library.Remove(custom.Id); Theme.Library.Install(ThemeFiles.Read(customPath));
        var customSettings = new Settings(); Theme.Select(customSettings, custom); Theme.Apply(customSettings);
        var customButton = Ui.Button("Custom", () => { }); customButton.ApplyTemplate();
        if (customButton.MinHeight != 50 || ((CornerRadius)app.Resources["button.Radius"]).TopLeft != 0) throw new Exception("Imported custom stylesheet did not change native geometry.");
        customSettings.CornerRadius = 8; Theme.Apply(customSettings);
        var captured = Theme.Capture(customSettings); Theme.Select(customSettings, captured);
        Theme.Library.Install(captured); Theme.Apply(customSettings);
        if (captured.RadiusScale != .5 || !captured.StyleSheet.Contains("var(--p-roundness)") || ((CornerRadius)app.Resources["card.Radius"]).TopLeft != 12.5) throw new Exception("Export lost the user's corner override or the stylesheet's parameters.");
        Theme.Library.Remove(captured.Id);
        Theme.Library.Remove(custom.Id);
        Console.WriteLine("PASS: imported custom CSS changes controls and exported overrides survive");
        var aurora = ThemeFiles.Read(Path.Combine("Themes", "Examples", "aurora-motion", "theme.categorybar-theme.json"));
        Theme.Library.Remove(aurora.Id); Theme.Library.Install(aurora);
        var auroraSettings = new Settings(); Theme.Select(auroraSettings, aurora); Theme.Apply(auroraSettings);
        var animatedButton = Ui.Button("Motion", () => { }); animatedButton.ApplyTemplate();
        if (animatedButton.Template.Triggers.OfType<Trigger>().All(t => t.EnterActions.Count == 0)) throw new Exception("Authored hover transitions are missing.");
        var shaderCard = new ContentControl { Content = "Shader", Width = 240, Height = 100 }.Res(FrameworkElement.StyleProperty, "Theme.Card");
        shaderCard.ApplyTemplate(); shaderCard.Measure(new Size(240, 100)); shaderCard.Arrange(new Rect(0, 0, 240, 100));
        var surface = (Border)shaderCard.Template.FindName("surface", shaderCard);
        if (surface.Effect is not ThemeShaderEffect effect || effect.Shader.Length == 0 || effect.P1 != .6) throw new Exception("File shader was not applied to the native surface.");
        shaderCard.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
        var enteringMenu = new Border(); Ui.FadeIn(enteringMenu, transition: "Theme.Menu.Enter");
        var dispatcherFrame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) => { timer.Stop(); dispatcherFrame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(dispatcherFrame);
        if (((ThemeShaderEffect)surface.Effect).P0 <= 0) throw new Exception("Shader parameter animation did not run.");
        if (SystemParameters.ClientAreaAnimation && enteringMenu.Opacity < .99) throw new Exception("File menu transition did not complete.");
        var shaderBitmap = new RenderTargetBitmap(240, 100, 96, 96, PixelFormats.Pbgra32); shaderBitmap.Render(shaderCard);
        var shaderEncoder = new PngBitmapEncoder(); shaderEncoder.Frames.Add(BitmapFrame.Create(shaderBitmap));
        using (var stream = File.Create(Path.Combine(folder, "aurora-shader.png"))) shaderEncoder.Save(stream);
        if (app.Resources["Theme.Menu.Enter"] is not Storyboard) throw new Exception("File menu transition was not loaded.");
        var previousTemplate = app.Resources["Theme.CardTemplate"];
        Theme.Library.Remove(aurora.Id); Theme.Apply(new Settings());
        if (ReferenceEquals(previousTemplate, app.Resources["Theme.CardTemplate"])) throw new Exception("Previous theme resources leaked into the next theme.");
        Console.WriteLine("PASS: authored templates, transitions, compiled GPU shader and animated parameters");
        ArchitectureChecks.Run(app, folder, configPath);
        ParameterChecks.Run(app, folder);
        app.Shutdown();
    }
}
