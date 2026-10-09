using System.Text;
using System.Windows.Threading;
using CategoryBar.Interop;

namespace CategoryBar.Services;

/// <summary>
/// Tracks which shortcuts are running, missing or being started. Inspection happens on the shell worker;
/// <see cref="Changed"/> is raised on the UI thread only when something actually changed.
/// </summary>
internal readonly record struct OpenWindow(IntPtr Handle, string Path, string AppId);

internal sealed class AppStatus
{
    private readonly Func<IEnumerable<Shortcut>> shortcuts;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Dictionary<Guid, DateTime> launching = [];
    private Dictionary<Guid, IntPtr> running = [];
    private HashSet<Guid> broken = [];
    private bool busy;

    internal event Action? Changed;
    internal bool Enabled { get => timer.IsEnabled; set { if (value == timer.IsEnabled) return; timer.IsEnabled = value; if (value) Refresh(); } }

    internal AppStatus(Func<IEnumerable<Shortcut>> shortcuts)
    {
        this.shortcuts = shortcuts;
        timer.Tick += (_, _) => Refresh();
    }

    /// <summary>A set is running when all of its applications are, and broken when any of them is missing.</summary>
    internal bool IsRunning(Shortcut shortcut) => shortcut.IsSet ? shortcut.Steps.All(IsRunning) : running.ContainsKey(shortcut.Id);
    internal bool IsBroken(Shortcut shortcut) => shortcut.IsSet ? shortcut.Steps.Any(IsBroken) : broken.Contains(shortcut.Id);
    internal IntPtr WindowOf(Shortcut shortcut) => running.TryGetValue(shortcut.Id, out var window) && Native.IsWindow(window) ? window : IntPtr.Zero;
    internal bool IsLaunching(Shortcut shortcut) => launching.ContainsKey(shortcut.Id);
    internal bool AnyRunning(Category category) => category.Shortcuts.Any(IsRunning);

    internal void MarkLaunching(Shortcut shortcut)
    {
        launching[shortcut.Id] = DateTime.UtcNow.AddSeconds(6);
        Changed?.Invoke();
        // Look for the new window sooner than the regular cadence.
        var quick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        var attempts = 0;
        quick.Tick += (_, _) => { Refresh(); if (++attempts >= 8 || !launching.ContainsKey(shortcut.Id)) quick.Stop(); };
        quick.Start();
    }

    /// <summary>Brings the running application's window to the front. Returns false when it has none.</summary>
    internal bool Activate(Shortcut shortcut)
    {
        if (!running.TryGetValue(shortcut.Id, out var window) || !Native.IsWindow(window)) return false;
        if (Native.IsIconic(window)) Native.ShowWindow(window, 9); // SW_RESTORE
        return Native.SetForegroundWindow(window);
    }

    internal async void Refresh()
    {
        if (busy) return;
        busy = true;
        try
        {
            var items = shortcuts().Select(s => new Shortcut { Id = s.Id, Name = s.Name, Target = s.Target, Arguments = s.Arguments, WorkingDirectory = s.WorkingDirectory }).ToList();
            var (nowRunning, nowBroken) = await ShellWorker.Run(() => Inspect(items));
            var now = DateTime.UtcNow;
            var finished = launching.Where(p => p.Value < now || nowRunning.ContainsKey(p.Key)).Select(p => p.Key).ToList();
            foreach (var id in finished) launching.Remove(id);
            var changed = finished.Count > 0 || !nowBroken.SetEquals(broken) || nowRunning.Count != running.Count
                || nowRunning.Any(p => !running.TryGetValue(p.Key, out var old) || old != p.Value);
            running = nowRunning; broken = nowBroken;
            if (changed) Changed?.Invoke();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException) { }
        finally { busy = false; }
    }

    /// <summary>Top-level application windows, front to back, with the program that owns each.</summary>
    internal static List<OpenWindow> OpenWindows()
    {
        var result = new List<OpenWindow>();
        var processes = new Dictionary<uint, (string Path, string AppId)>();
        var text = new StringBuilder(1024);

        (string Path, string AppId) Describe(uint id)
        {
            if (processes.TryGetValue(id, out var known)) return known;
            var described = ("", "");
            var process = Native.OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, id);
            if (process != IntPtr.Zero)
            {
                try
                {
                    var length = text.Capacity; text.Clear();
                    var path = Native.QueryFullProcessImageName(process, 0, text, ref length) ? text.ToString() : "";
                    length = text.Capacity; text.Clear();
                    var appId = Native.GetApplicationUserModelId(process, ref length, text) == 0 ? text.ToString() : "";
                    described = (path, appId);
                }
                finally { Native.CloseHandle(process); }
            }
            return processes[id] = described;
        }

        Native.EnumWindows((window, lParam) =>
        {
            if (!Native.IsWindowVisible(window) || Native.GetWindow(window, 4 /* GW_OWNER */) != IntPtr.Zero) return true;
            if ((Native.GetWindowLongPtr(window, Native.GwlExStyle).ToInt64() & Native.ExToolWindow) != 0) return true;
            var className = Native.ClassName(window);
            if (className is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
            var owner = window;
            if (className == "ApplicationFrameWindow")
            {
                // Store applications live in a frame host; a cloaked frame is a suspended application.
                _ = Native.DwmGetWindowAttribute(window, 14, out var cloaked, sizeof(int));
                owner = Native.FindWindowEx(window, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
                if (cloaked != 0 || owner == IntPtr.Zero) return true;
            }
            _ = Native.GetWindowThreadProcessId(owner, out var processId);
            var (path, appId) = Describe(processId);
            if (path.Length == 0) return true;
            // Explorer always runs as the shell; only its folder windows count as a running application.
            if (path.EndsWith("\\explorer.exe", StringComparison.OrdinalIgnoreCase) && className != "CabinetWClass") return true;
            result.Add(new(window, path, appId));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    internal static (Dictionary<Guid, IntPtr>, HashSet<Guid>) Inspect(List<Shortcut> items)
    {
        var byPath = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        var byName = new Dictionary<string, (string Path, IntPtr Window)>(StringComparer.OrdinalIgnoreCase);
        var byAppId = new Dictionary<string, IntPtr>(StringComparer.OrdinalIgnoreCase);
        foreach (var open in OpenWindows())
        {
            byPath.TryAdd(open.Path, open.Handle);
            byName.TryAdd(Path.GetFileName(open.Path), (open.Path, open.Handle));
            if (open.AppId.Length > 0) byAppId.TryAdd(open.AppId, open.Handle);
        }

        var running = new Dictionary<Guid, IntPtr>();
        var broken = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (!Shortcuts.Exists(item)) { broken.Add(item.Id); continue; }
            if (LaunchTarget.IsLink(item.Target)) continue;
            var window = IntPtr.Zero;
            if (LaunchTarget.IsShellApplication(item.Target))
            {
                var id = item.Target[Shortcuts.AppsFolder.Length..];
                if (!byAppId.TryGetValue(id, out window) && id.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    && byName.TryGetValue(Path.GetFileName(id), out var named)) window = named.Window;
            }
            else
            {
                var (target, arguments, _) = Shortcuts.Resolve(item);
                // The File Explorer pin is a namespace shortcut without a file target.
                if (target == item.Target && Shortcuts.IsLinkFile(target) && item.Name is "File Explorer" or "Провідник" or "Проводник"
                    or "Explorer" or "Datei-Explorer" or "Explorador de archivos" or "Explorateur de fichiers" or "Explorador de Arquivos" or "文件资源管理器" or "エクスプローラー")
                    target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
                if (!byPath.TryGetValue(target, out window))
                {
                    // Squirrel-style launchers: "Update.exe --processStart App.exe".
                    var marker = arguments.IndexOf("--processStart", StringComparison.OrdinalIgnoreCase);
                    var name = marker >= 0 ? arguments[(marker + 14)..].Trim().Trim('"').Split('"', ' ')[0] : Path.GetFileName(target);
                    // Stub launchers start the real program from a versioned folder next to them.
                    var folder = Path.GetDirectoryName(target);
                    if (name.Length > 0 && folder is { Length: > 3 } && byName.TryGetValue(name, out var named)
                        && named.Path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) window = named.Window;
                }
            }
            if (window != IntPtr.Zero) running[item.Id] = window;
        }
        return (running, broken);
    }
}
