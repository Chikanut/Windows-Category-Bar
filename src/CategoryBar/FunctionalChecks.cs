using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CategoryBar.Core;
namespace CategoryBar;
internal static class FunctionalChecks
{
    internal static async Task Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var checks = new List<string>();
        void Check(string label, bool success) { if (!success) throw new InvalidOperationException(label); checks.Add("PASS: " + label); File.WriteAllLines(Path.Combine(folder, "checks.txt"), checks); }
        var catalog = ShortcutCatalog.Discover();
        Check("Installed application catalog discovered", catalog.Count > 0);
        File.WriteAllText(Path.Combine(folder, "catalog.txt"), $"Total: {catalog.Count}; Store: {catalog.Count(s => LaunchTarget.IsShellApplication(s.Target))}");
        var command = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        var linkPath = Path.Combine(folder, "Launch check.lnk");
        object shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        object link = ((dynamic)shell).CreateShortcut(linkPath);
        try { ((dynamic)link).TargetPath = command; ((dynamic)link).Arguments = "/d /c cd > launch-output.txt"; ((dynamic)link).WorkingDirectory = folder; ((dynamic)link).WindowStyle = 7; ((dynamic)link).Save(); }
        finally { Marshal.FinalReleaseComObject(link); Marshal.FinalReleaseComObject(shell); }
        var resolved = ShortcutCatalog.Resolve(new Shortcut { Name = "Check", Target = linkPath });
        Check("LNK retains arguments and working directory", resolved.Target.Equals(command, StringComparison.OrdinalIgnoreCase) && resolved.Arguments.Contains("launch-output") && resolved.WorkingDirectory == folder);
        Process.Start(new ProcessStartInfo(linkPath) { UseShellExecute = true })?.Dispose();
        var output = Path.Combine(folder, "launch-output.txt");
        for (var i = 0; i < 30 && !File.Exists(output); i++) await Task.Delay(100);
        Check("Shell executes original LNK in its working directory", File.Exists(output) && File.ReadAllText(output).Trim().Equals(folder, StringComparison.OrdinalIgnoreCase));
        var configuration = new Configuration { Settings = new Settings { HasSeenWelcome = true } };
        var store = new ConfigurationStore(Path.Combine(folder, "config.json"));
        store.Save(configuration);
        var bar = new BarWindow(configuration, store); bar.Show();
        try
        {
            await Task.Delay(200);
            foreach (var size in new[] { 0, 16, 25 })
            {
                var category = new Category { Name = "Перевірка " + size, Shortcuts = Enumerable.Range(0, size).Select(i => new Shortcut { Name = "Програма " + i, Target = command }).ToList() };
                configuration.Categories.Add(category); bar.SaveAndRefresh();
                configuration.Settings.GridForLargeCategories = size >= 20;
                var anchor = new Button { Content = category.Name }; var host = new Window { Content = anchor, Width = 160, Height = 60, ShowInTaskbar = false, ShowActivated = false };
                host.Show(); var menu = new MenuWindow(category, configuration.Settings, anchor, bar);
                try { menu.Show(); menu.Position(NativeMethods.Observe().Monitor); menu.SetEditing(true); Check($"Menu with {size} shortcuts supports editing", menu.Editing && menu.ActualHeight > 0); menu.SetEditing(false); }
                finally { menu.Close(); host.Close(); }
            }
            configuration.Settings.AllMonitors = true; bar.SaveAndRefresh(); await Task.Delay(200);
            Check("Monitor synchronization runs", NativeMethods.Taskbars().Count > 0);
        }
        finally { bar.Close(); }
        var observation = NativeMethods.Observe();
        var monitor = NativeMethods.MonitorFromWindow(NativeMethods.FindWindow("Shell_TrayWnd", null), 2);
        NativeMethods.MonitorInfo Info() { var info = new NativeMethods.MonitorInfo { Size = Marshal.SizeOf<NativeMethods.MonitorInfo>() }; NativeMethods.GetMonitorInfo(monitor, ref info); return info; }
        var before = Info().Work;
        if (!observation.AutoHide)
        {
            using (var reservation = new AppBarReservation())
            {
                reservation.Update(observation.Monitor, 34, observation.Taskbar.Top); await Task.Delay(150);
                Check("AppBar reserves work area", Info().Work.Bottom <= before.Bottom - 34);
            }
            await Task.Delay(150); Check("AppBar releases work area on exit", Info().Work.Bottom == before.Bottom);
        }
        checks.Add("INFO: Actual auto-hide, fullscreen games, Explorer restart and mixed-monitor DPI still require integration validation.");
        File.WriteAllLines(Path.Combine(folder, "checks.txt"), checks);
    }
}
