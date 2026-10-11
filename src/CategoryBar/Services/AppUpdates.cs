using System.Reflection;
using Velopack;
using Velopack.Sources;

namespace CategoryBar.Services;

/// <summary>Stable public GitHub releases; downloads and restarts only after a user action.</summary>
internal sealed class AppUpdates
{
    internal const string Repository = "https://github.com/Chikanut/Windows-Category-Bar";
    private readonly UpdateManager manager = new(new GithubSource(Repository, null, false));
    private readonly Session session;
    private UpdateInfo? available;
    internal event Action? Changed;
    internal bool Busy { get; private set; }
    internal bool IsInstalled => manager.IsInstalled;
    internal bool HasUpdate => available is not null;
    internal static string Version => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "?";
    internal string Status { get; private set; } = L.F("Поточна версія: {0}", Version);

    internal AppUpdates(Session session) => this.session = session;

    private void SetStatus(string text) { Status = text; Changed?.Invoke(); }

    internal async Task Check(bool manual)
    {
        if (Busy) return;
        if (!IsInstalled)
        {
            SetStatus(L.T("Для оновлень установіть додаток через Setup.exe з GitHub Releases."));
            return;
        }
        Busy = true;
        SetStatus(L.T("Перевіряємо оновлення…"));
        try
        {
            available = await manager.CheckForUpdatesAsync();
            SetStatus(available is null ? L.F("Версія {0} актуальна.", Version) : L.F("Доступна версія {0}.", available.TargetFullRelease.Version));
            if (available is not null)
                session.Toast(L.T("Доступне оновлення"), Status, ToastKind.Info, true,
                    new ToastAction(L.T("Переглянути"), () => App.Instance.ShowSettings("General")));
        }
        catch (Exception ex)
        {
            Log(ex);
            SetStatus(L.T("Не вдалося перевірити оновлення. Спробуйте пізніше."));
            if (manual) session.Toast(L.T("Оновлення"), Status, ToastKind.Warning);
        }
        finally { Busy = false; Changed?.Invoke(); }
    }

    internal async Task Install()
    {
        if (Busy || available is null) return;
        var update = available;
        Busy = true;
        SetStatus(L.T("Завантажуємо оновлення…"));
        try
        {
            await manager.DownloadUpdatesAsync(update, progress => App.Instance.Dispatcher.BeginInvoke(() =>
                SetStatus(L.F("Завантажуємо оновлення: {0}%", progress))));
            // Persist first, then let the updater wait while WPF releases the tray, appbars and mutex.
            session.Store.Save(session.Config);
            manager.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: false, restart: true,
                restartArgs: App.Instance.UpdateRestartArguments);
            App.Instance.Quit(restarting: true);
        }
        catch (Exception ex)
        {
            Log(ex);
            SetStatus(L.T("Не вдалося встановити оновлення. Спробуйте ще раз."));
            session.Toast(L.T("Оновлення"), Status, ToastKind.Error);
        }
        finally { Busy = false; Changed?.Invoke(); }
    }

    private void Log(Exception exception)
    {
        try { File.AppendAllText(Path.Combine(Path.GetDirectoryName(session.Store.Path)!, "errors.log"), DateTime.Now + " Update: " + exception + Environment.NewLine); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
