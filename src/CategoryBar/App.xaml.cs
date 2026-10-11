using System.Text.Json;
using System.Threading;
using System.Windows.Threading;
using CategoryBar.Interop;
using CategoryBar.Views;

namespace CategoryBar;

public partial class App : Application
{
    private readonly List<BarWindow> bars = [];
    private readonly DispatcherTimer monitorTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private Mutex? instance;
    private Session? session;
    private Tray? tray;
    private SettingsWindow? settings;
    private OnboardingWindow? onboarding;
    private bool reporting;
    private string[] arguments = [];

    internal static App Instance => (App)Current;
    /// <summary>Set when the application was started with --config; autostart then keeps using that file.</summary>
    internal string? CustomConfigPath { get; private set; }
    internal Services.AppUpdates Updates { get; private set; } = null!;
    internal string[] UpdateRestartArguments => CustomConfigPath is null ? ["--settings"] : ["--config", CustomConfigPath, "--settings"];
    private readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromSeconds(15) };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        arguments = e.Args;
        L.Use("System");
        var configArgument = Array.IndexOf(e.Args, "--config");
        if (configArgument >= 0 && configArgument + 1 < e.Args.Length) CustomConfigPath = Path.GetFullPath(e.Args[configArgument + 1]);
        var path = CustomConfigPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WindowsCategoryBar", "config.json");

        // One instance per configuration file.
        instance = new Mutex(true, "Local\\WindowsCategoryBar-" + (uint)path.ToUpperInvariant().Aggregate(17, (hash, c) => unchecked(hash * 31 + c)), out var created);
        if (!created && !e.Args.Contains("--diagnose")) { Shutdown(); return; }

        var log = Path.Combine(Path.GetDirectoryName(path)!, "errors.log");
        DispatcherUnhandledException += (_, error) =>
        {
            // A failure in one interaction must not take the bar down; record it and tell the user.
            error.Handled = true;
            try { Directory.CreateDirectory(Path.GetDirectoryName(log)!); File.AppendAllText(log, DateTime.Now + " " + error.Exception + Environment.NewLine + Environment.NewLine); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            if (reporting) return;
            reporting = true;
            try { session?.Toast(L.T("Сталася помилка"), L.F("{0} Подробиці записано в errors.log.", error.Exception.Message), ToastKind.Error); }
            finally { reporting = false; }
        };

        ConfigurationStore store;
        Configuration config;
        try
        {
            store = new ConfigurationStore(path);
            config = store.Load();
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException)
        {
            MessageBox.Show(ex.Message, L.T("Панель категорій"), MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        var diagnose = Array.IndexOf(e.Args, "--diagnose");
        if (diagnose >= 0 && diagnose + 1 < e.Args.Length)
        {
            // Writes what the status tracker sees for every shortcut; used to investigate missing indicators.
            var all = config.Categories.SelectMany(c => c.Shortcuts).SelectMany(s => s.IsSet ? s.Steps : [s]).ToList();
            var (running, broken) = Services.AppStatus.Inspect(all);
            File.WriteAllLines(e.Args[diagnose + 1], all.Select(s => $"{s.Name} | {(running.ContainsKey(s.Id) ? "running" : broken.Contains(s.Id) ? "broken" : "idle")} | {Services.Shortcuts.Resolve(s)}"));
            Shutdown();
            return;
        }

        L.Use(config.Settings.Language);
        Theme.Apply(config.Settings);
        session = new Session(config, store);
        Updates = new Services.AppUpdates(session);
        if (Updates.IsInstalled && StartupRegistration.IsEnabled)
        {
            try { StartupRegistration.SetEnabled(true, CustomConfigPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { session.Toast(L.T("Не вдалося змінити автозапуск"), ex.Message, ToastKind.Warning); }
        }
        updateTimer.Tick += async (_, _) => { updateTimer.Stop(); await Updates.Check(manual: false); };
        if (Updates.IsInstalled) updateTimer.Start();
        Theme.FollowWindows(() => session.Settings);
        session.ToastRequested += request => { if (bars.FirstOrDefault(b => b.IsPrimary)?.ShowToast(request) != true) tray?.Balloon(request.Title, request.Body); };
        session.Changed += SyncBars;
        session.Status.Enabled = true;
        monitorTimer.Tick += (_, _) => SyncBars();

        tray = new Tray { MenuFactory = TrayMenu };
        tray.Clicked += () => ShowSettings();

        if (config.Settings.OnboardingCompleted) StartBars();
        else ShowOnboarding();
        if (e.Args.Contains("--settings") && config.Settings.OnboardingCompleted) ShowSettings();
        if (store.RecoveryMessage is { } recovery) session.Toast(L.T("Налаштування відновлено"), recovery, ToastKind.Warning);
    }

    private void StartBars()
    {
        if (session is null || bars.Count > 0) return;
        AddBar(new BarWindow(session));
        monitorTimer.Start();
        SyncBars();
    }

    private void AddBar(BarWindow bar)
    {
        bars.Add(bar);
        bar.Closed += (_, _) => bars.Remove(bar);
    }

    /// <summary>Keeps one bar per secondary taskbar while "all monitors" is on.</summary>
    private void SyncBars()
    {
        if (session is null || bars.Count == 0) return;
        var wanted = session.Settings.AllMonitors
            ? Native.Taskbars().Where(t => Native.ClassName(t) == "Shell_SecondaryTrayWnd").ToList() : [];
        foreach (var bar in bars.Where(b => !b.IsPrimary && !wanted.Contains(b.BoundTaskbar)).ToList()) bar.Close();
        foreach (var taskbar in wanted.Where(t => bars.All(b => b.BoundTaskbar != t)))
            AddBar(new BarWindow(session, taskbar) { HiddenByUser = bars[0].HiddenByUser });
    }

    private ContextMenu TrayMenu()
    {
        var menu = new ContextMenu();
        var hidden = bars.Count > 0 && bars[0].HiddenByUser;
        menu.Items.Add(Ui.Item(L.T("Налаштування…"), () => ShowSettings(), Icons.Sliders));
        menu.Items.Add(Ui.Item(L.T("Упорядкувати категорії…"), () => ShowSettings("Categories"), Icons.Apps));
        menu.Items.Add(Ui.Item(L.T("Перевірити оновлення"), () => { ShowSettings("General"); _ = Updates.Check(manual: true); }, Icons.Import));
        menu.Items.Add(new Separator());
        menu.Items.Add(hidden ? Ui.Item(L.T("Показати панель"), ShowBars, Icons.ChevronUp) : Ui.Item(L.T("Сховати панель до перезапуску"), HideBars, Icons.ChevronDown));
        var startup = new MenuItem { Header = L.T("Запускати з Windows"), IsChecked = StartupRegistration.IsEnabled };
        startup.Click += (_, _) =>
        {
            try { StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled, CustomConfigPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { session?.Toast(L.T("Не вдалося змінити автозапуск"), ex.Message, ToastKind.Error); }
        };
        menu.Items.Add(startup);
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Item(L.T("Вийти"), Quit, Icons.Close));
        return menu;
    }

    internal void HideBars() { foreach (var bar in bars) bar.HiddenByUser = true; }
    internal void ShowBars() { foreach (var bar in bars) bar.HiddenByUser = false; }

    internal void ShowSettings(string? page = null)
    {
        if (session is null) return;
        if (onboarding is not null) { onboarding.Surface(); return; }
        if (settings is null)
        {
            settings = new SettingsWindow(session);
            settings.Closed += (_, _) => settings = null;
        }
        if (page is not null) settings.Navigate(page);
        settings.Surface();
    }

    internal void ShowOnboarding()
    {
        if (session is null) return;
        if (onboarding is not null) { onboarding.Surface(); return; }
        settings?.Close();
        var window = onboarding = new OnboardingWindow(session);
        window.Closed += (_, _) =>
        {
            onboarding = null;
            // Closing the wizard early still counts as a decision: start with what there is.
            if (!session.Settings.OnboardingCompleted) session.ChangeSettings(s => s.OnboardingCompleted = true);
            StartBars();
            if (window.Finished)
                session.Toast(L.T("Панель готова"), L.T("Наведіть курсор на категорію над панеллю завдань. Перенесені значки тепер можна відкріпити: клацніть значок правою кнопкою → «Відкріпити від панелі завдань»."), ToastKind.Success, true);
        };
        window.Surface();
    }

    internal void Quit()
    {
        monitorTimer.Stop();
        updateTimer.Stop();
        foreach (var bar in bars.ToList()) bar.Close(); // Releases any reserved screen area.
        settings?.Close();
        onboarding?.Close();
        tray?.Dispose();
        tray = null;
        Shutdown();
    }

    /// <summary>
    /// Starts the application again and reopens the settings. Used after the language changes: every window,
    /// menu and cached list is built from texts in the language chosen at startup.
    /// </summary>
    internal void Restart()
    {
        var executable = Environment.ProcessPath;
        if (executable is null) return;
        var info = new System.Diagnostics.ProcessStartInfo(executable);
        foreach (var argument in arguments.Where(a => a != "--settings")) info.ArgumentList.Add(argument);
        info.ArgumentList.Add("--settings");
        // The new process must be able to take the single-instance lock.
        try { instance?.ReleaseMutex(); } catch (ApplicationException) { }
        instance?.Dispose();
        instance = null;
        System.Diagnostics.Process.Start(info)?.Dispose();
        Quit();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        tray?.Dispose();
        instance?.Dispose();
        base.OnExit(e);
    }
}
