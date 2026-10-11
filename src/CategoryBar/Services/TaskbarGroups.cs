using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Threading;
using CategoryBar.Interop;

namespace CategoryBar.Services;

/// <summary>
/// Categories as icons on the Windows taskbar (Settings.TaskbarIcons). Every category gets a shortcut with its
/// own application id in the TaskbarGroups folder next to the configuration, and that shortcut is pinned in
/// front of the user's other pins. Clicking or hovering the icon opens the category's menu above it and a
/// right click shows the category's applications as a Jump List. The icon is an .ico file of ours, redrawn
/// with a green dot while one of the category's applications is running.
/// </summary>
internal sealed class TaskbarGroups : IDisposable
{
    internal const string DividerArgument = "--divider";
    /// <summary>The id of this application's own windows on the taskbar; the installer gives the Start menu shortcut the same one.</summary>
    internal const string ProcessAppId = "velopack.CategoryBar";
    private const string OpenArgument = "--category", LaunchArgument = "--launch";
    private const string DividerId = IdPrefix + "Divider";
    /// <summary>Stands for the divider in the list of icons that have been pinned once.</summary>
    private static readonly Guid DividerKey = new("d1b1de70-0000-4000-8000-000000000001");
    private const string PipeName = "WindowsCategoryBar.TaskbarGroups", IdPrefix = "Chikanut.CategoryBar.Group.";
    private string DividerLink => Path.Combine(Folder, "Category Bar.lnk");

    private readonly Session session;
    private readonly Action<Category, PixelRect> open;
    private readonly Action settings;
    private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer hover = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer arrange = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private readonly ManualResetEventSlim stopped = new();
    private readonly Dictionary<Guid, string> published = [], jumpLists = [];
    private readonly FileSystemWatcher? watcher;
    private Dictionary<Guid, string> links = [];
    private volatile List<(string AppId, PixelRect Bounds)> buttons = [];
    private volatile bool enabled;
    private string? hovered, arranged;
    private int hoverTicks, arrangeTries;
    private bool arranging;

    /// <summary>The set of pinned category icons changed.</summary>
    internal event Action? StateChanged;
    internal string Folder { get; }
    private string IconFolder => Path.Combine(Folder, "Icons");

    internal TaskbarGroups(Session session, Action<Category, PixelRect> open, Action settings)
    {
        this.session = session; this.open = open; this.settings = settings;
        Folder = Path.Combine(Path.GetDirectoryName(session.Store.Path)!, "TaskbarGroups");
        hover.Tick += (_, _) => TrackHover();
        arrange.Tick += (_, _) => { arrange.Stop(); Arrange(); };
        session.Changed += Sync;
        session.Status.Changed += Sync;
        if (Directory.Exists(TaskbarPins.Folder))
        {
            watcher = new FileSystemWatcher(TaskbarPins.Folder, "*.lnk") { EnableRaisingEvents = true };
            void Changed(object sender, FileSystemEventArgs e) => dispatcher.BeginInvoke(() => { Sync(); StateChanged?.Invoke(); });
            watcher.Created += Changed; watcher.Deleted += Changed; watcher.Renamed += (s, e) => Changed(s, e);
        }
        Sync();
        new Thread(Listen) { IsBackground = true, Name = "Taskbar groups pipe" }.Start();
        // UI Automation must not run on the UI thread: the taskbar's answer can take hundreds of milliseconds.
        new Thread(Watch) { IsBackground = true, Name = "Taskbar groups buttons" }.Start();
    }

    private static string AppId(Category category) => IdPrefix + category.Id.ToString("N");

    // ---- State -----------------------------------------------------------------------------------

    /// <summary>The copies of category shortcuts in Windows' pinned folder, by category id.</summary>
    private static Dictionary<Guid, List<string>> PinnedLinks()
    {
        var result = new Dictionary<Guid, List<string>>();
        try
        {
            if (!Directory.Exists(TaskbarPins.Folder)) return result;
            foreach (var file in Directory.EnumerateFiles(TaskbarPins.Folder, "*.lnk"))
            {
                var arguments = Shortcuts.ReadLink(file).Arguments ?? "";
                if (!arguments.StartsWith(OpenArgument + " ", StringComparison.Ordinal) || !Guid.TryParse(arguments[(OpenArgument.Length + 1)..], out var id)) continue;
                if (!result.TryGetValue(id, out var files)) result[id] = files = [];
                files.Add(file);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        return result;
    }

    internal int PinnedCount => PinnedLinks().Keys.Count(id => session.Config.Categories.Any(c => c.Id == id));

    internal void OpenFolder()
    {
        try { Directory.CreateDirectory(Folder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", "\"" + Folder + "\""))?.Dispose(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { }
    }

    /// <summary>Pins every category again, including the ones the user unpinned by hand.</summary>
    internal void PinAll()
    {
        if (!enabled) return;
        Remember(pinned => pinned.Clear());
        RequestArrange();
    }

    private void Remember(Action<List<Guid>> change)
    {
        try { session.Editor.ChangeQuietly(() => change(session.Settings.TaskbarPinned)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
    }

    private void Sync()
    {
        var wanted = session.Settings.TaskbarIcons;
        if (wanted != enabled)
        {
            enabled = wanted; hover.IsEnabled = wanted; hovered = null; arranged = null;
            published.Clear(); jumpLists.Clear();
            if (!wanted) Retire();
        }
        if (enabled) Publish();
    }

    // ---- A second process started by a pinned icon or a Jump List entry --------------------------

    /// <summary>The request a pinned icon or a Jump List entry starts the program with: "--category id", "--launch id" or "--divider".</summary>
    private static string? Request(string[] arguments)
    {
        if (arguments.Contains(DividerArgument)) return DividerArgument;
        var index = Array.FindIndex(arguments, a => a is OpenArgument or LaunchArgument);
        return index < 0 || index + 1 >= arguments.Length ? null : arguments[index] + " " + arguments[index + 1];
    }

    /// <summary>Hands the request to the running instance. False when there is nothing to hand over or nobody to take it.</summary>
    internal static bool Forward(string[] arguments)
    {
        if (Request(arguments) is not { } request) return false;
        try
        {
            using var pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            pipe.Connect(500);
            // The click gave this process the right to take the foreground; pass it on.
            Native.AllowSetForegroundWindow(-1);
            using var writer = new StreamWriter(pipe);
            writer.WriteLine(request);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException) { return false; }
    }

    /// <summary>The same request, when the icon was clicked while the application was not running yet.</summary>
    internal void Handle(string[] arguments)
    {
        if (Request(arguments) is { } request) Receive(request);
    }

    private void Listen()
    {
        while (!stopped.IsSet)
        {
            try
            {
                using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.CurrentUserOnly);
                pipe.WaitForConnection();
                using var reader = new StreamReader(pipe);
                if (reader.ReadLine() is { } message) dispatcher.BeginInvoke(() => Receive(message));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException) { stopped.Wait(1000); }
        }
    }

    private void Receive(string message)
    {
        // The divider after the category icons doubles as the way into the settings.
        if (message == DividerArgument) { settings(); return; }
        var parts = message.Split(' ', 2);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return;
        if (parts[0] == OpenArgument)
        {
            if (session.Config.Categories.FirstOrDefault(c => c.Id == id) is { } category) open(category, AnchorFor(category));
            // The icon of a deleted category, or an icon left from the time the mode was on.
            else if (enabled) RequestArrange(); else Retire();
        }
        else if (parts[0] == LaunchArgument && session.Config.Categories.SelectMany(c => c.Shortcuts).FirstOrDefault(s => s.Id == id) is { } shortcut)
            session.Launch(shortcut);
    }

    /// <summary>The category's taskbar button, or the place of the pointer when the button is not known yet.</summary>
    private PixelRect AnchorFor(Category category)
    {
        var cursor = Native.Cursor();
        var id = AppId(category);
        var own = buttons.Where(b => b.AppId == id).ToList();
        if (own.Count > 0) return own.FirstOrDefault(b => b.Bounds.Contains(cursor.X, cursor.Y), own[0]).Bounds;
        foreach (var taskbar in Native.Taskbars())
            if (Native.GetWindowRect(taskbar, out var rect) && rect.Pixels.Contains(cursor.X, cursor.Y))
                return new(cursor.X - 22, rect.Top, cursor.X + 22, rect.Bottom);
        return new(cursor.X - 22, cursor.Y - 22, cursor.X + 22, cursor.Y + 22);
    }

    // ---- Hovering a pinned icon ------------------------------------------------------------------

    private void Watch()
    {
        var last = DateTime.MinValue;
        while (!stopped.Wait(500))
        {
            if (!enabled) { if (buttons.Count > 0) buttons = []; continue; }
            var cursor = Native.Cursor();
            var taskbars = Native.Taskbars();
            var over = taskbars.Any(t => Native.GetWindowRect(t, out var rect) && rect.Pixels.Contains(cursor.X, cursor.Y));
            // Buttons move when applications start and exit; look often only while the pointer is near them.
            if (!over && DateTime.UtcNow - last < TimeSpan.FromSeconds(4)) continue;
            last = DateTime.UtcNow;
            buttons = Scan(taskbars);
        }
    }

    private static List<(string, PixelRect)> Scan(List<IntPtr> taskbars)
    {
        var found = new List<(string, PixelRect)>();
        foreach (var handle in taskbars)
        {
            try
            {
                foreach (AutomationElement button in AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
                {
                    var id = button.Current.AutomationId;
                    if (!id.StartsWith("Appid: " + IdPrefix, StringComparison.Ordinal)) continue;
                    var bounds = button.Current.BoundingRectangle;
                    if (!bounds.IsEmpty) found.Add((id[7..], new((int)bounds.Left, (int)bounds.Top, (int)bounds.Right, (int)bounds.Bottom)));
                }
            }
            catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or TimeoutException or ArgumentException) { }
        }
        return found;
    }

    private void TrackHover()
    {
        var cursor = Native.Cursor();
        // A pressed button means a click or a drag of the icon; the click opens the menu by itself.
        var pressed = (Native.GetAsyncKeyState(1) & 0x8000) != 0 || (Native.GetAsyncKeyState(2) & 0x8000) != 0;
        var current = buttons;
        var over = pressed ? null : current.FirstOrDefault(b => b.Bounds.Contains(cursor.X, cursor.Y)).AppId;
        // The buttons keep their place on the screen while a fullscreen game or video covers the taskbar; what
        // counts is the window under the pointer, because the Windows key brings the taskbar up over a game.
        if (over is not null && !Native.TaskbarUnder(cursor)) over = null;
        if (over != hovered) { hovered = over; hoverTicks = 0; return; }
        if (over is null || ++hoverTicks != Math.Max(1, session.Settings.OpenDelayMs / 100)) return;
        if (session.Config.Categories.FirstOrDefault(c => AppId(c) == over) is { } category)
            open(category, current.First(b => b.AppId == over && b.Bounds.Contains(cursor.X, cursor.Y)).Bounds);
    }

    // ---- Shortcuts, icons and Jump Lists ---------------------------------------------------------

    /// <summary>What a category's icon is drawn from: an application's file, letters, or a symbol; and the tile colour.</summary>
    private static (string? Target, string? Glyph, string? Text, Color Color) IconOf(Category category)
    {
        if (CategoryIcons.TryText(category.Icon, out var text)) return (null, null, text, Theme.Accent);
        if (CategoryIcons.TryGlyph(category.Icon, out var glyph, out var color)) return (null, glyph, null, color);
        var shortcut = category.Icon.StartsWith("app:", StringComparison.Ordinal) && Guid.TryParse(category.Icon[4..], out var id)
            ? category.Shortcuts.FirstOrDefault(s => s.Id == id) : null;
        shortcut ??= category.Shortcuts.FirstOrDefault();
        if (shortcut is { IsSet: true }) shortcut = shortcut.Steps.FirstOrDefault();
        return shortcut is null || LaunchTarget.IsLink(shortcut.Target)
            ? (null, CategoryIcons.DefaultGlyph, null, CategoryIcons.ColorFor(category.Name)) : (shortcut.Target, null, null, default);
    }

    private static uint Hash(string text)
    {
        var hash = 2166136261;
        foreach (var c in text) hash = unchecked((hash ^ c) * 16777619);
        return hash;
    }

    private void Publish()
    {
        var executable = Environment.ProcessPath;
        if (executable is null) return;
        var pinned = PinnedLinks();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var files = new Dictionary<Guid, string>();
        foreach (var category in session.Config.Categories)
        {
            // Two categories may share a name, and a name may have nothing a file name can keep.
            var name = string.Concat(category.Name.Split(Path.GetInvalidFileNameChars())).Trim().TrimEnd('.');
            if (name.Length == 0) name = "Category";
            var unique = name;
            for (var suffix = 2; !names.Add(unique); suffix++) unique = name + " (" + suffix + ")";
            files[category.Id] = Path.Combine(Folder, unique + ".lnk");
        }
        links = files;
        var font = new FontFamily(session.Settings.FontName + ", Segoe UI");
        foreach (var category in session.Config.Categories)
        {
            var (target, glyph, text, color) = IconOf(category);
            var running = session.Settings.ShowRunningIndicators && session.Status.AnyRunning(category);
            var copies = pinned.GetValueOrDefault(category.Id) ?? [];
            var shortcuts = category.Shortcuts.ToList();
            var content = string.Join("|", shortcuts.Select(s => s.Id + s.Name + s.Target));
            var look = $"3|{category.Icon}|{target}|{glyph}|{color}|{running}" + (text is null ? "" : "|" + session.Settings.FontName);
            var icon = Path.Combine(IconFolder, $"{category.Id:N}-{Hash(look):x8}.ico");
            var signature = string.Join("|", executable, files[category.Id], icon, string.Join(";", copies), content);
            if (published.GetValueOrDefault(category.Id) == signature) continue;
            published[category.Id] = signature;
            var jumpList = jumpLists.GetValueOrDefault(category.Id) != content;
            jumpLists[category.Id] = content;

            void Write(ImageSource? image)
            {
                // A newer change to the same category is already on its way.
                if (!enabled || published.GetValueOrDefault(category.Id) != signature) return;
                try
                {
                    if (!File.Exists(icon))
                    {
                        Directory.CreateDirectory(IconFolder);
                        var symbol = image is null && text is null ? glyph ?? CategoryIcons.DefaultGlyph : null;
                        var tile = image is null && text is null && glyph is null ? CategoryIcons.ColorFor(category.Name) : color;
                        File.WriteAllBytes(icon, CategoryIcons.Ico(size => CategoryIcons.Render(size, image, symbol, tile, running, text, font)));
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return; }
                var file = files[category.Id];
                _ = ShellWorker.Run(() =>
                {
                    try
                    {
                        WriteLink(file, category, executable, icon);
                        foreach (var copy in copies) UpdatePinned(copy, executable, icon);
                        if (jumpList) WriteJumpList(category, shortcuts, executable);
                        foreach (var old in Directory.EnumerateFiles(IconFolder, category.Id.ToString("N") + "-*.ico"))
                            if (!old.Equals(icon, StringComparison.OrdinalIgnoreCase)) File.Delete(old);
                    }
                    catch (Exception ex) when (ex is COMException or InvalidCastException or IOException or UnauthorizedAccessException) { }
                    return true;
                });
            }
            if (target is not null) IconCache.Load(target, Write); else Write(null);
        }

        var current = files.Values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var divider = session.Settings.TaskbarDivider ? DividerLink : null;
        if (divider is not null) current.Add(divider);
        _ = ShellWorker.Run(() =>
        {
            try
            {
                if (divider is not null && !File.Exists(divider)) WriteDivider(divider, executable);
                if (Directory.Exists(Folder))
                    foreach (var file in Directory.EnumerateFiles(Folder, "*.lnk"))
                        if (!current.Contains(file)) File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return true;
        });
        // The pins follow the categories: a new one is pinned, a deleted one unpinned, the order is theirs.
        var order = string.Join(",", session.Config.Categories.Select(c => c.Id)) + (divider is null ? "" : "|");
        if (order != arranged) { arranged = order; RequestArrange(); }
    }

    private static object NewLink(string executable, string arguments, string title, string icon)
    {
        var instance = new ShellLinkObject();
        var link = (IShellLinkW)instance;
        link.SetPath(executable);
        link.SetArguments(arguments);
        link.SetDescription(title);
        link.SetIconLocation(icon, 0);
        return instance;
    }

    private static void WriteLink(string file, Category category, string executable, string icon)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var link = NewLink(executable, OpenArgument + " " + category.Id.ToString("N"), category.Name, icon);
        try
        {
            // The application id makes Windows treat every category as an application of its own.
            PropVariant.Set((IPropertyStore)link, PropertyKey.AppUserModelId, AppId(category));
            ((IPersistFile)link).Save(file, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    private void WriteDivider(string file, string executable)
    {
        var icon = Path.Combine(IconFolder, "divider-1.ico");
        Directory.CreateDirectory(IconFolder);
        if (!File.Exists(icon)) File.WriteAllBytes(icon, dispatcher.Invoke(() => CategoryIcons.Ico(CategoryIcons.RenderDivider)));
        var link = NewLink(executable, DividerArgument, L.T("Панель категорій — Налаштування"), icon);
        try
        {
            PropVariant.Set((IPropertyStore)link, PropertyKey.AppUserModelId, DividerId);
            ((IPersistFile)link).Save(file, true);
        }
        finally { Marshal.FinalReleaseComObject(link); }
    }

    /// <summary>
    /// Windows keeps its own copy of a pinned shortcut. Pointing that copy at a new icon file is what changes
    /// the picture on the taskbar; the file name differs every time, so no icon cache can return the old one.
    /// </summary>
    private static void UpdatePinned(string file, string executable, string icon)
    {
        var known = Shortcuts.ReadLink(file);
        if (string.Equals(known.IconPath, icon, StringComparison.OrdinalIgnoreCase) && string.Equals(known.Target, executable, StringComparison.OrdinalIgnoreCase)) return;
        var instance = new ShellLinkObject();
        try
        {
            if (((IPersistFile)instance).Load(file, 2) != 0) return; // STGM_READWRITE
            var link = (IShellLinkW)instance;
            link.SetPath(executable);
            link.SetIconLocation(icon, 0);
            if (((IPersistFile)instance).Save(file, true) != 0) return;
        }
        finally { Marshal.FinalReleaseComObject(instance); }
        TaskbarPins.Refresh(file);
    }

    private static void WriteJumpList(Category category, List<Shortcut> shortcuts, string executable)
    {
        var list = (ICustomDestinationList)new DestinationListObject();
        var tasks = (IObjectCollection)new ObjectCollectionObject();
        var links = new List<object>();
        try
        {
            list.SetAppID(AppId(category));
            var iid = typeof(IObjectArray).GUID;
            if (list.BeginList(out _, ref iid, out var removed) < 0) return;
            Marshal.FinalReleaseComObject(removed);
            foreach (var shortcut in shortcuts.Take(20))
            {
                var step = shortcut.IsSet ? shortcut.Steps.FirstOrDefault() : shortcut;
                var source = step is null ? default : Shortcuts.IsLinkFile(step.Target) && Shortcuts.ReadLink(step.Target) is { IconPath.Length: > 0 } read
                    ? (Path: read.IconPath, Index: read.IconIndex) : (Path: Shortcuts.Resolve(step).Target, Index: 0);
                var link = NewLink(executable, LaunchArgument + " " + shortcut.Id.ToString("N"), shortcut.Name, executable);
                if (source.Path is { Length: > 0 } && File.Exists(source.Path) && Path.GetExtension(source.Path).ToLowerInvariant() is ".exe" or ".ico" or ".dll")
                    ((IShellLinkW)link).SetIconLocation(source.Path, source.Index);
                links.Add(link);
                PropVariant.Set((IPropertyStore)link, PropertyKey.Title, shortcut.Name);
                tasks.AddObject(link);
            }
            if (links.Count > 0) list.AddUserTasks((IObjectArray)tasks);
            list.CommitList();
        }
        finally
        {
            foreach (var link in links) Marshal.FinalReleaseComObject(link);
            Marshal.FinalReleaseComObject(tasks);
            Marshal.FinalReleaseComObject(list);
        }
    }

    // ---- Pinning ---------------------------------------------------------------------------------

    private void RequestArrange() { arrangeTries = 0; arrange.Stop(); arrange.Start(); }

    /// <summary>
    /// Pins the categories that were never pinned, puts the category icons first in the order of the
    /// categories and unpins the icons of categories that no longer exist.
    /// </summary>
    private async void Arrange()
    {
        if (!enabled || arranging) { if (enabled) arrange.Start(); return; }
        arranging = true;
        try
        {
            var categories = session.Config.Categories.ToList();
            var known = session.Settings.TaskbarPinned.ToHashSet();
            var fresh = categories.Where(c => !known.Contains(c.Id) && links.ContainsKey(c.Id)).ToList();
            var add = fresh.ToDictionary(AppId, c => links[c.Id]);
            var divider = session.Settings.TaskbarDivider;
            if (divider && !known.Contains(DividerKey)) add[DividerId] = DividerLink;
            // A shortcut whose icon is still being drawn does not exist yet: come back for it.
            if (add.Values.Any(file => !File.Exists(file)) && arrangeTries++ < 12) { arrange.Start(); return; }
            var order = categories.Select(AppId).ToList();
            if (divider) order.Add(DividerId);
            var done = await ShellWorker.Run(() => enabled && TaskbarPins.Arrange(IdPrefix, order, add, DividerId));
            // Explorer drops an entry it does not accept, so the result is read back instead of assumed.
            await Task.Delay(1500);
            if (!enabled) return;
            var pinned = TaskbarPins.Pinned(IdPrefix);
            Remember(list =>
            {
                list.RemoveAll(id => id != DividerKey && categories.All(c => c.Id != id));
                list.AddRange(fresh.Where(c => pinned.Contains(AppId(c))).Select(c => c.Id));
                if (!divider) list.Remove(DividerKey);
                else if (pinned.Contains(DividerId) && !list.Contains(DividerKey)) list.Add(DividerKey);
            });
            await ShellWorker.Run(() => { MoveRunningBehind(); return true; });
            RemoveStrays(pinned);
            var failed = fresh.Where(c => !pinned.Contains(AppId(c))).ToList();
            if (!done || failed.Count > 0)
                session.Toast(L.T("Закріпіть значки категорій"), L.T("Перетягніть ярлики з папки на панель завдань Windows або клацніть ярлик правою кнопкою → «Показати додаткові параметри» → «Закріпити на панелі завдань». Закріплені значки стоять перед запущеними програмами, а їхній порядок змінюється перетягуванням."),
                    ToastKind.Warning, true, new ToastAction(L.T("Відкрити папку"), OpenFolder));
            StateChanged?.Invoke();
        }
        finally { arranging = false; }
    }

    /// <summary>
    /// Buttons of running applications that are not pinned and stand to the left of the category icons are
    /// moved to the end of the taskbar, keeping their order, so that the category icons come first.
    /// </summary>
    private static void MoveRunningBehind()
    {
        var all = new List<(string Id, string Name, double Left)>();
        try
        {
            var taskbar = Native.FindWindow("Shell_TrayWnd", null);
            if (taskbar == IntPtr.Zero) return;
            foreach (AutomationElement button in AutomationElement.FromHandle(taskbar).FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
            {
                var id = button.Current.AutomationId;
                if (id.StartsWith("Appid: ", StringComparison.Ordinal) && !button.Current.BoundingRectangle.IsEmpty)
                    all.Add((id[7..], button.Current.Name, button.Current.BoundingRectangle.Left));
            }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or TimeoutException or ArgumentException) { return; }
        var own = all.Where(b => b.Id.StartsWith(IdPrefix, StringComparison.Ordinal)).ToList();
        if (own.Count == 0) return;
        var edge = own.Max(b => b.Left);
        var pinned = TaskbarPins.AllIds();
        var ahead = all.Where(b => b.Left < edge && !pinned.Contains(b.Id)).OrderBy(b => b.Left).ToList();
        if (ahead.Count == 0) return;

        var windows = AppStatus.OpenWindows();
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Described(string path)
        {
            if (names.TryGetValue(path, out var known)) return known;
            var text = "";
            try { text = System.Diagnostics.FileVersionInfo.GetVersionInfo(path).FileDescription ?? ""; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
            return names[path] = text;
        }
        // The taskbar's id of a window is not always readable from outside, so the button is matched by what is known.
        bool Owns((string Id, string Name, double Left) button, OpenWindow window)
        {
            var file = Path.GetFileName(window.Path);
            if (window.AppId.Equals(button.Id, StringComparison.OrdinalIgnoreCase) || window.Path.Equals(button.Id, StringComparison.OrdinalIgnoreCase)) return true;
            if (button.Id.StartsWith('{') && button.Id.EndsWith("\\" + file, StringComparison.OrdinalIgnoreCase)) return true;
            if (button.Id == ProcessAppId) return window.Path.Equals(Environment.ProcessPath, StringComparison.OrdinalIgnoreCase);
            if (button.Id == "Microsoft.Windows.Explorer") return file.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase);
            if (window.AppId.Length > 0) return false;
            var described = Described(window.Path);
            return described.Length > 2 && button.Name.StartsWith(described, StringComparison.CurrentCultureIgnoreCase)
                || button.Id.EndsWith("." + Path.GetFileNameWithoutExtension(file), StringComparison.OrdinalIgnoreCase);
        }
        var taken = new HashSet<IntPtr>();
        var groups = ahead.Select(button => windows.Where(w => Owns(button, w) && taken.Add(w.Handle)).Select(w => w.Handle).Reverse().ToList()).Where(g => g.Count > 0).ToList();
        TaskbarPins.MoveToEnd(groups);
    }

    /// <summary>Deletes copies in the pinned folder that no pin points at any more.</summary>
    private static void RemoveStrays(HashSet<string> pinned)
    {
        foreach (var (id, files) in PinnedLinks())
        {
            if (pinned.Contains(IdPrefix + id.ToString("N"))) continue;
            foreach (var file in files)
                try { File.Delete(file); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        try
        {
            if (!pinned.Contains(DividerId) && Directory.Exists(TaskbarPins.Folder))
                foreach (var file in Directory.EnumerateFiles(TaskbarPins.Folder, "*.lnk"))
                    if (Shortcuts.ReadLink(file).Arguments == DividerArgument) File.Delete(file);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    /// <summary>The bar is back: the icons on the taskbar are no longer needed.</summary>
    private async void Retire()
    {
        buttons = [];
        arrange.Stop();
        Remember(list => list.Clear());
        var folder = Folder;
        await ShellWorker.Run(() =>
        {
            TaskbarPins.Arrange(IdPrefix, [], new Dictionary<string, string>());
            try
            {
                if (Directory.Exists(folder))
                    foreach (var file in Directory.EnumerateFiles(folder, "*.lnk")) File.Delete(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            return true;
        });
        await Task.Delay(1500);
        if (enabled) return;
        var pinned = TaskbarPins.Pinned(IdPrefix);
        RemoveStrays(pinned);
        StateChanged?.Invoke();
        if (pinned.Count > 0)
            session.Toast(L.T("Відкріпіть значки категорій"), L.T("Windows не дала прибрати їх автоматично. Клацніть значок правою кнопкою → «Відкріпити від панелі завдань»."), ToastKind.Warning, true);
    }

    /// <summary>
    /// The user closed the application: its icons leave the taskbar and are pinned again at the next start.
    /// The icons the user unpinned by hand stay remembered, so they do not come back.
    /// </summary>
    internal void Release()
    {
        if (!enabled) return;
        enabled = false; hover.Stop(); arrange.Stop(); buttons = [];
        var pinned = TaskbarPins.Pinned(IdPrefix);
        Remember(list => list.RemoveAll(id => pinned.Contains(id == DividerKey ? DividerId : IdPrefix + id.ToString("N"))));
        TaskbarPins.Arrange(IdPrefix, [], new Dictionary<string, string>());
        RemoveStrays(TaskbarPins.Pinned(IdPrefix));
    }

    public void Dispose()
    {
        stopped.Set();
        hover.Stop();
        arrange.Stop();
        watcher?.Dispose();
        session.Changed -= Sync;
        session.Status.Changed -= Sync;
    }
}
