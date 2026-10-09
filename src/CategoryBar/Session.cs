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
        Status = new AppStatus(() => Config.Categories.SelectMany(c => c.Shortcuts));
        AdoptLinkFiles();
    }

    /// <summary>Shortcuts added by earlier versions still point at files on the desktop; take a copy of those.</summary>
    private void AdoptLinkFiles()
    {
        var changed = false;
        foreach (var shortcut in Config.Categories.SelectMany(c => c.Shortcuts))
        {
            var target = shortcut.Target;
            Shortcuts.KeepOwnCopy(shortcut, Store.Path);
            changed |= shortcut.Target != target;
        }
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
            Toast("Не вдалося зберегти зміну", ex.Message, ToastKind.Error);
            return false;
        }
        Status.Refresh();
        if (title is not null) Toast(title, body, ToastKind.Success, false, new ToastAction("Скасувати", Undo));
        return true;
    }

    internal void Undo()
    {
        if (!Editor.CanUndo) return;
        try { Editor.Undo(); Status.Refresh(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Toast("Не вдалося скасувати", ex.Message, ToastKind.Error); }
    }

    /// <summary>Applies a settings tweak immediately; it does not replace the undo step.</summary>
    internal void ChangeSettings(Action<Settings> change)
    {
        try { Editor.ChangeQuietly(() => change(Config.Settings)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { Toast("Не вдалося зберегти налаштування", ex.Message, ToastKind.Error); }
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
        if (fresh.Count == 0) { Toast("Уже в категорії", "Ці ярлики вже є в «" + category.Name + "».", ToastKind.Info); return 0; }
        foreach (var shortcut in fresh) Shortcuts.KeepOwnCopy(shortcut, Store.Path);
        var title = fresh.Count == 1 ? "Додано «" + fresh[0].Name + "»" : "Додано " + Ui.Plural(fresh.Count, "ярлик", "ярлики", "ярликів");
        Change(() => category.Shortcuts.InsertRange(index < 0 ? category.Shortcuts.Count : Math.Min(index, category.Shortcuts.Count), fresh), title, "До категорії «" + category.Name + "».");
        return fresh.Count;
    }

    internal int AddPaths(Category category, IEnumerable<string> paths)
    {
        var all = paths.ToList();
        var shortcuts = all.Select(Shortcuts.FromPath).OfType<Shortcut>().ToList();
        var added = shortcuts.Count > 0 ? AddShortcuts(category, shortcuts) : 0;
        if (shortcuts.Count < all.Count)
            Toast("Непідтримуваний файл", "Додати можна .exe, .lnk, .url, папки та вебпосилання. Пропущено: " + (all.Count - shortcuts.Count) + ".", ToastKind.Warning);
        return added;
    }

    internal void RemoveShortcut(Category category, Shortcut shortcut) =>
        Change(() => category.Shortcuts.Remove(shortcut), "Прибрано «" + shortcut.Name + "»", "Сама програма залишилась на комп’ютері.");

    internal Category? CategoryOf(Shortcut shortcut) => Config.Categories.FirstOrDefault(c => c.Shortcuts.Contains(shortcut));

    /// <summary>Starts the shortcut, or switches to its window when it is already running.</summary>
    internal async void Launch(Shortcut shortcut, bool asAdministrator = false, bool newInstance = false)
    {
        if (Status.IsBroken(shortcut) || !Shortcuts.Exists(shortcut))
        {
            var category = CategoryOf(shortcut);
            Toast("Ярлик не працює", "Файл для «" + shortcut.Name + "» не знайдено. Можливо, програму видалено або переміщено.", ToastKind.Warning, true,
                new ToastAction("Вказати файл…", () => Views.Dialogs.Relocate(this, shortcut)),
                new ToastAction("Прибрати", () => { if (category is not null) RemoveShortcut(category, shortcut); }));
            return;
        }
        if (!asAdministrator && !newInstance && Settings.ActivateRunning && Status.Activate(shortcut)) return;
        Status.MarkLaunching(shortcut);
        try { await Shortcuts.Launch(shortcut, asAdministrator); }
        catch (Exception ex) when (ex is Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            Toast("Не вдалося запустити «" + shortcut.Name + "»", ex.Message, ToastKind.Error);
        }
    }
}
