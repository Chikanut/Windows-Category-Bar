using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using CategoryBar.Core;

namespace CategoryBar;

public partial class App : Application
{
    private Mutex? instance;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--render-settings"))
        {
            System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            var index = Array.IndexOf(e.Args, "--render-settings");
            if (index + 1 >= e.Args.Length) { Shutdown(2); return; }
            var page = index + 2 < e.Args.Length ? e.Args[index + 2] : "Panel";
            if (page is not ("Panel" or "General" or "Categories" or "About")) { Shutdown(2); return; }
            try { SettingsPreview.RenderAndCheck(e.Args[index + 1], page); Shutdown(); }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(e.Args[index + 1], "failure.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }
        if (e.Args.Contains("--self-test"))
        {
            var index = Array.IndexOf(e.Args, "--self-test");
            if (index + 1 >= e.Args.Length) { Shutdown(2); return; }
            var folder = Path.GetFullPath(e.Args[index + 1]);
            Dispatcher.BeginInvoke(async () =>
            {
                try { await FunctionalChecks.Run(folder); Shutdown(); }
                catch (Exception ex) { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder, "failure.txt"), ex.ToString()); Shutdown(1); }
            });
            return;
        }
        if (e.Args.Contains("--probe"))
        {
            var index = Array.IndexOf(e.Args, "--probe");
            if (index + 1 >= e.Args.Length) { Shutdown(2); return; }
            var observation = NativeMethods.Observe();
            File.WriteAllText(e.Args[index + 1], JsonSerializer.Serialize(new
            {
                Time = DateTimeOffset.Now,
                Observation = observation,
                ShouldShow = VisibilityPolicy.ShouldShow(observation)
            }, new JsonSerializerOptions { WriteIndented = true }));
            Shutdown();
            return;
        }
        instance = new Mutex(true, "Local\\WindowsCategoryBar", out var created);
        if (!created) { Shutdown(); return; }
        var configArg = Array.IndexOf(e.Args, "--config");
        var path = configArg >= 0 && configArg + 1 < e.Args.Length ? e.Args[configArg + 1]
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsCategoryBar", "config.json");
        try
        {
            var store = new ConfigurationStore(path);
            var config = store.Load();
            var bar = new BarWindow(config, store);
            MainWindow = bar;
            bar.Show();
            if (store.RecoveryMessage is { } message) bar.Notify("Налаштування відновлено", message);
            if (e.Args.Contains("--smoke"))
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) => { timer.Stop(); Shutdown(); };
                timer.Start();
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            MessageBox.Show(ex.Message, "Панель категорій", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        instance?.Dispose();
        base.OnExit(e);
    }
}
