using System.ComponentModel;
using CategoryBar.Services;

namespace CategoryBar;

internal enum ToastKind { Success, Info, Warning, Error }
internal sealed record ToastAction(string Label, Action Run);
internal sealed record ToastRequest(string Title, string Body, ToastKind Kind = ToastKind.Success, bool Sticky = false, params ToastAction[] Actions);

/// <summary>
/// The running application's state: configuration, persistence with undo, application status and the
/// notifications that windows subscribe to. All members are used from the UI thread.
/// </summary>
internal sealed class Session
{
    internal Configuration Config { get; }
    internal ConfigurationStore Store { get; }
    internal ConfigurationEditor Editor { get; }
    internal AppStatus Status { get; }
    internal Settings Settings => Config.Settings;

    /// <summary>Categories, shortcuts or settings changed; views rebuild from <see cref="Config"/>.</summary>
    internal event Action? Changed;
    internal event Action<ToastRequest>? ToastRequested;

    internal Session(Configuration config, ConfigurationStore store)
    {
        Config = config; Store = store;
        Editor = new ConfigurationEditor(config, store.Save);
        Editor.Changed += () => Changed?.Invoke();
        // The applications inside a set are tracked one by one; the set's own state is derived from them.
        Status = new AppStatus(() => Config.Categories.SelectMany(c => c.Shortcuts).SelectMany(s => s.IsSet ? s.Steps : [s]));
        AdoptLinkFiles();
    }

    /// <summary>Shortcuts added by earlier versions still point at files on the desktop; take a copy of those.</summary>
    private void AdoptLinkFiles()
    {
        var changed = false;
        foreach (var shortcut in Config.Categories.SelectMany(c => c.Shortcuts))
            changed |= Shortcuts.KeepOwnCopy(shortcut, Store.Path);
        if (!changed) return;
        try { Editor.ChangeQuietly(() => { }); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }

    internal void Toast(string title, string body = "", ToastKind kind = ToastKind.Success, bool sticky = false, params ToastAction[] actions)
        => ToastRequested?.Invoke(new(title, body, kind, sticky || kind == ToastKind.Error, actions));

    /// <summary>Applies and saves a structural change. A title produces a toast with an undo action.</summary>
    internal bool Change(Action change, string? title = null, string body = "")
    {
        try { Editor.Change(change); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        {
            Toast(L.T("Не вдалося зберегти зміну"), ex.Message, ToastKind.Error);
            return false;
        }
        Status.Refresh();
        if (title is not null) Toast(title, body, ToastKind.Success, false, new ToastAction(L.T("Скасувати##undo"), Undo));
        return true;
    }

    internal void Undo()
    {
        if (!Editor.CanUndo) return;
        try { Editor.Undo(); Status.Refresh(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Toast(L.T("Не вдалося скасувати"), ex.Message, ToastKind.Error); }
    }

    /// <summary>Applies a settings tweak immediately; it does not replace the undo step.</summary>
    internal void ChangeSettings(Action<Settings> change)
    {
        try { Editor.ChangeQuietly(() => change(Config.Settings)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Toast(L.T("Не вдалося зберегти налаштування"), ex.Message, ToastKind.Error); }
        Theme.Apply(Config.Settings);
        Changed?.Invoke();
    }

    internal Category? AddCategory(string? name = null)
    {
        var category = new Category { Name = name ?? Ui.FreshCategoryName(Config.Categories) };
        return Change(() => Config.Categories.Add(category)) ? category : null;
    }

    /// <summary>Adds shortcuts that the category does not already contain. Returns how many were added.</summary>
    internal int AddShortcuts(Category category, IEnumerable<Shortcut> shortcuts, int index = -1)
    {
        var known = category.Shortcuts.Select(Shortcuts.Identity).ToHashSet();
        var fresh = shortcuts.Where(s => known.Add(Shortcuts.Identity(s))).ToList();
        if (fresh.Count == 0) { Toast(L.T("Уже в категорії"), L.F("Ці ярлики вже є в «{0}».", category.Name), ToastKind.Info); return 0; }
        foreach (var shortcut in fresh) Shortcuts.KeepOwnCopy(shortcut, Store.Path);
        var title = fresh.Count == 1 ? L.F("Додано «{0}»", fresh[0].Name) : L.N(fresh.Count, "Додано {0} ярлик|Додано {0} ярлики|Додано {0} ярликів");
        Change(() => category.Shortcuts.InsertRange(index < 0 ? category.Shortcuts.Count : Math.Min(index, category.Shortcuts.Count), fresh), title, L.F("До категорії «{0}».", category.Name));
        return fresh.Count;
    }

    internal int AddPaths(Category category, IEnumerable<string> paths)
    {
        var all = paths.ToList();
        var shortcuts = all.Select(Shortcuts.FromPath).OfType<Shortcut>().ToList();
        var added = shortcuts.Count > 0 ? AddShortcuts(category, shortcuts) : 0;
        if (shortcuts.Count < all.Count)
            Toast(L.T("Непідтримуваний файл"), L.F("Додати можна .exe, .lnk, .url, папки та вебпосилання. Пропущено: {0}.", all.Count - shortcuts.Count), ToastKind.Warning);
        return added;
    }

    internal void RemoveShortcut(Category category, Shortcut shortcut) =>
        Change(() => category.Shortcuts.Remove(shortcut), L.F("Прибрано «{0}»", shortcut.Name), L.T("Сама програма залишилась на комп’ютері."));

    internal Category? CategoryOf(Shortcut shortcut) => Config.Categories.FirstOrDefault(c => c.Shortcuts.Contains(shortcut));

    /// <summary>Starts the shortcut, or switches to its window when it is already running.</summary>
    internal async void Launch(Shortcut shortcut, bool asAdministrator = false, bool newInstance = false)
    {
        if (shortcut.IsSet) { LaunchSet(shortcut, newInstance); return; }
        if (Status.IsBroken(shortcut) || !Shortcuts.Exists(shortcut))
        {
            var category = CategoryOf(shortcut);
            Toast(L.T("Ярлик не працює"), L.F("Файл для «{0}» не знайдено. Можливо, програму видалено або переміщено.", shortcut.Name), ToastKind.Warning, true,
                new ToastAction(L.T("Вказати файл…"), () => Views.Dialogs.Relocate(this, shortcut)),
                new ToastAction(L.T("Прибрати"), () => { if (category is not null) RemoveShortcut(category, shortcut); }));
            return;
        }
        if (!asAdministrator && !newInstance && Settings.ActivateRunning && Status.Activate(shortcut)) return;
        Status.MarkLaunching(shortcut);
        try { await Shortcuts.Launch(shortcut, asAdministrator); }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Toast(L.F("Не вдалося запустити «{0}»", shortcut.Name), ex.Message, ToastKind.Error);
        }
    }

    /// <summary>
    /// Starts every application of the set that is not running yet, then puts the windows where the set
    /// remembers them. Applications that are already open are only moved.
    /// </summary>
    private async void LaunchSet(Shortcut set, bool restart)
    {
        var steps = set.Steps.ToList();
        var failed = steps.Where(s => !Shortcuts.Exists(s)).ToList();
        Status.MarkLaunching(set);
        foreach (var step in steps.Except(failed).ToList())
        {
            if (!restart && Status.IsRunning(step)) continue;
            try { await Shortcuts.Launch(step, false); }
            catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException) { failed.Add(step); }
        }
        if (failed.Count > 0)
            Toast(L.F("Набір «{0}» запущено не повністю", set.Name), L.F("Не вдалося запустити: {0}.", string.Join(", ", failed.Select(s => s.Name))), ToastKind.Warning);
        await AppSets.Arrange(steps.Except(failed));
    }

    /// <summary>Remembers where the set's open windows are now.</summary>
    internal void CaptureLayout(Shortcut set)
    {
        var captured = set.Steps.Select(step => (Step: step, Placement: AppSets.Capture(Status.WindowOf(step)))).Where(p => p.Placement is not null).ToList();
        if (captured.Count == 0)
        {
            Toast(L.T("Немає відкритих вікон"), L.T("Запустіть застосунки набору, розставте вікна й спробуйте ще раз."), ToastKind.Info);
            return;
        }
        Change(() => { foreach (var (step, placement) in captured) step.Placement = placement; },
            L.T("Розташування вікон запам’ятано"), L.N(captured.Count, "{0} вікно у наборі «{1}».|{0} вікна у наборі «{1}».|{0} вікон у наборі «{1}».", set.Name));
    }

    /// <summary>Replaces the set with its applications as separate shortcuts.</summary>
    internal void Ungroup(Category category, Shortcut set)
    {
        var index = category.Shortcuts.IndexOf(set);
        if (index < 0) return;
        Change(() =>
        {
            category.Shortcuts.RemoveAt(index);
            var known = category.Shortcuts.Select(Shortcuts.Identity).ToHashSet();
            var steps = set.Steps.Where(s => known.Add(Shortcuts.Identity(s))).ToList();
            foreach (var step in steps) step.Placement = null;
            category.Shortcuts.InsertRange(index, steps);
        }, L.F("Набір «{0}» розібрано", set.Name), L.T("Його застосунки залишились у категорії окремими ярликами."));
    }
}
