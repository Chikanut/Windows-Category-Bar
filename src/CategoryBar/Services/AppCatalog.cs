using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using CategoryBar.Interop;

namespace CategoryBar.Services;

/// <summary>An application the user can add, with the category the classifier suggests for it.</summary>
internal sealed record CatalogEntry(string Name, string Target, string Source, string SuggestedCategory);

/// <summary>Finds installed applications and the ones pinned to the Windows taskbar.</summary>
internal static class AppCatalog
{
    private static Task<List<CatalogEntry>>? installed;
    private static string PinnedFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");

    /// <summary>Start menu and Microsoft Store applications. Scanned once and reused.</summary>
    internal static Task<List<CatalogEntry>> Installed(bool refresh = false)
    {
        if (installed is null || refresh || installed.IsFaulted) installed = ShellWorker.Run(ScanInstalled);
        return installed;
    }

    internal static async Task<List<CatalogEntry>> Pinned()
    {
        var apps = await Installed();
        // UI Automation must not run on the shell worker's STA: it needs a thread that is not pumping the taskbar's client.
        return await Task.Run(() => ScanPinned(apps));
    }

    private static List<CatalogEntry> ScanInstalled()
    {
        var result = new List<CatalogEntry>();
        foreach (var root in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
            ScanFolder(Environment.GetFolderPath(root), result);
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is not null)
        {
            object? shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
                items = ((dynamic)folder!).Items();
                foreach (dynamic item in (dynamic)items!)
                {
                    try
                    {
                        string name = item.Name, id = item.Path;
                        // Packaged applications only; classic programs are already covered by their Start menu shortcuts.
                        if (name.Length > 0 && id.Contains('!')) result.Add(Entry(name, Shortcuts.AppsFolder + id, "Microsoft Store"));
                    }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            finally
            {
                foreach (var value in new[] { items, folder, shell })
                    if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
        }
        return result.DistinctBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void ScanFolder(string root, List<CatalogEntry> result)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var path in Directory.EnumerateFiles(root, "*.lnk"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                var lower = name.ToLowerInvariant();
                if (lower.Contains("uninstall") || lower.Contains("видал") || lower.Contains("readme") || lower.Contains("license")
                    || lower.EndsWith(" help") || lower.Contains("довідка") || lower.Contains("documentation") || lower.Contains("release notes")) continue;
                result.Add(Entry(name, path, "Меню «Пуск»"));
            }
            foreach (var folder in Directory.EnumerateDirectories(root))
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0) ScanFolder(folder, result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    private static CatalogEntry Entry(string name, string target, string source)
    {
        var resolved = target;
        var description = "";
        if (Shortcuts.IsLinkFile(target))
        {
            var link = Shortcuts.ReadLink(target);
            if (link.Target is { Length: > 0 }) resolved = link.Target + " " + link.Arguments;
            try
            {
                if (link.Target is { Length: > 0 } exe && exe.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(exe))
                    description = FileVersionInfo.GetVersionInfo(exe).FileDescription ?? "";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new(name, target, source, ApplicationClassifier.Classify(name, resolved, description).Category);
    }

    /// <summary>
    /// Pinned classic programs have a shortcut in the user's pinned folder. Store applications do not, so the
    /// taskbar buttons are read through UI Automation: their identifier carries the application id and their
    /// order is the order on the taskbar.
    /// </summary>
    private static List<CatalogEntry> ScanPinned(List<CatalogEntry> apps)
    {
        var links = new List<CatalogEntry>();
        if (Directory.Exists(PinnedFolder))
            foreach (var file in Directory.EnumerateFiles(PinnedFolder, "*.lnk"))
                links.Add(Entry(Path.GetFileNameWithoutExtension(file), file, "Панель завдань"));

        var buttons = new List<(string Name, string AppId)>();
        try
        {
            var handle = Native.FindWindow("Shell_TrayWnd", null);
            if (handle != IntPtr.Zero)
                foreach (AutomationElement button in AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)))
                {
                    var id = button.Current.AutomationId;
                    if (id.StartsWith("Appid: ", StringComparison.Ordinal)) buttons.Add((button.Current.Name, id[7..]));
                }
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or COMException or InvalidOperationException or TimeoutException) { }

        // A pinned, not running program is announced as "<name><suffix>" ("Signal pinned"). Learn the localized
        // suffix from a program whose pin is certain, then use it to recognise pinned Store applications.
        string? pinnedSuffix = null;
        foreach (var (name, _) in buttons)
        {
            var link = links.FirstOrDefault(l => name.StartsWith(l.Name, StringComparison.CurrentCultureIgnoreCase));
            if (link is null) continue;
            var rest = name[link.Name.Length..];
            if (rest.Length > 1 && !rest.Any(char.IsDigit)) { pinnedSuffix = rest; break; }
        }

        var result = new List<CatalogEntry>();
        foreach (var (name, appId) in buttons)
        {
            var link = links.Where(l => name.StartsWith(l.Name, StringComparison.CurrentCultureIgnoreCase)).MaxBy(l => l.Name.Length);
            if (link is not null) { if (!result.Contains(link)) result.Add(link); continue; }
            if (!appId.Contains('!')) continue;
            var app = apps.FirstOrDefault(a => a.Target.Equals(Shortcuts.AppsFolder + appId, StringComparison.OrdinalIgnoreCase));
            var pinned = pinnedSuffix is not null ? name.EndsWith(pinnedSuffix, StringComparison.CurrentCulture) : !name.Any(char.IsDigit);
            if (app is not null && pinned && !result.Contains(app)) result.Add(app with { Source = "Панель завдань" });
        }
        result.AddRange(links.Where(l => !result.Contains(l)));
        return result;
    }

    /// <summary>
    /// Copies a shortcut out of Windows' pinned folder so that it keeps working after the user unpins it.
    /// </summary>
    internal static Shortcut ToShortcut(CatalogEntry entry, string configPath)
    {
        var shortcut = new Shortcut { Name = entry.Name, Target = entry.Target };
        if (entry.Target.StartsWith(PinnedFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            var folder = Path.Combine(Path.GetDirectoryName(configPath)!, "ImportedShortcuts");
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, shortcut.Id + ".lnk");
            File.Copy(entry.Target, copy, true);
            shortcut.Target = copy;
        }
        return shortcut;
    }
}
