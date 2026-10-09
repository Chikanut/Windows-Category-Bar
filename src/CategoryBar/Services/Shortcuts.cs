using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CategoryBar.Interop;

namespace CategoryBar.Services;

internal readonly record struct ResolvedLink(string Target, string Arguments, string WorkingDirectory, string IconPath, int IconIndex);

/// <summary>Reading, identifying and launching shortcut targets.</summary>
internal static class Shortcuts
{
    internal const string AppsFolder = "shell:AppsFolder\\";
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, ResolvedLink Link)> Links = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsLinkFile(string target) => target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase);

    /// <summary>Reads a .lnk file. Results are cached until the file changes.</summary>
    internal static ResolvedLink ReadLink(string path)
    {
        DateTime stamp;
        try { stamp = File.GetLastWriteTimeUtc(path); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return default; }
        if (Links.TryGetValue(path, out var cached) && cached.Stamp == stamp) return cached.Link;
        var result = default(ResolvedLink);
        object? instance = null;
        try
        {
            instance = new ShellLinkObject();
            if (((IPersistFile)instance).Load(path, 0) == 0)
            {
                var link = (IShellLinkW)instance;
                var buffer = new StringBuilder(1024);
                string Read(Func<StringBuilder, int> call) { buffer.Clear(); return call(buffer) >= 0 ? Environment.ExpandEnvironmentVariables(buffer.ToString()) : ""; }
                var target = Read(b => link.GetPath(b, b.Capacity, IntPtr.Zero, 0));
                var arguments = Read(b => link.GetArguments(b, b.Capacity));
                var directory = Read(b => link.GetWorkingDirectory(b, b.Capacity));
                var index = 0;
                var icon = Read(b => link.GetIconLocation(b, b.Capacity, out index));
                result = new(target, arguments, directory, icon, index);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException) { }
        finally { if (instance is not null) Marshal.FinalReleaseComObject(instance); }
        Links[path] = (stamp, result);
        return result;
    }

    /// <summary>The file that is ultimately started, with the shortcut's own overrides applied.</summary>
    internal static (string Target, string Arguments, string WorkingDirectory) Resolve(Shortcut shortcut)
    {
        if (!IsLinkFile(shortcut.Target) || !File.Exists(shortcut.Target))
            return (shortcut.Target, shortcut.Arguments, shortcut.WorkingDirectory);
        var link = ReadLink(shortcut.Target);
        return (link.Target is { Length: > 0 } ? link.Target : shortcut.Target,
            shortcut.Arguments.Length > 0 ? shortcut.Arguments : link.Arguments ?? "",
            shortcut.WorkingDirectory.Length > 0 ? shortcut.WorkingDirectory : link.WorkingDirectory ?? "");
    }

    /// <summary>Two shortcuts with the same identity start the same thing.</summary>
    internal static string Identity(Shortcut shortcut)
    {
        if (shortcut.IsSet) return "set:" + shortcut.Id;
        var (target, arguments, _) = Resolve(shortcut);
        // Namespace shortcuts (File Explorer, Control Panel) have no file target; the file name identifies them.
        if (target == shortcut.Target && IsLinkFile(target)) target = Path.GetFileNameWithoutExtension(target);
        return target.ToUpperInvariant() + "|" + arguments;
    }

    internal static bool Exists(Shortcut shortcut)
    {
        if (shortcut.IsSet) return shortcut.Steps.All(Exists);
        if (LaunchTarget.IsShellApplication(shortcut.Target) || LaunchTarget.IsLink(shortcut.Target)) return true;
        return File.Exists(shortcut.Target) || Directory.Exists(shortcut.Target);
    }

    /// <summary>Text shown under a name in pickers and property sheets.</summary>
    internal static string Describe(string target)
    {
        if (LaunchTarget.IsShellApplication(target)) return L.T("Застосунок Windows");
        if (LaunchTarget.IsLink(target)) return target;
        if (Directory.Exists(target)) return L.F("Папка · {0}", target);
        return target;
    }

    /// <summary>Starts the shortcut. Runs on a worker thread because the shell can block for seconds.</summary>
    internal static Task Launch(Shortcut shortcut, bool asAdministrator) => Task.Run(() =>
    {
        var admin = asAdministrator || shortcut.RunAsAdministrator;
        ProcessStartInfo info;
        if (LaunchTarget.IsShellApplication(shortcut.Target))
            info = new("explorer.exe", "\"" + shortcut.Target + "\"");
        else if (LaunchTarget.IsLink(shortcut.Target))
            info = new(shortcut.Target) { UseShellExecute = true };
        else
        {
            if (!Exists(shortcut)) throw new FileNotFoundException(L.F("Файл не знайдено: {0}", shortcut.Target));
            var plainLink = IsLinkFile(shortcut.Target) && shortcut.Arguments.Length == 0 && shortcut.WorkingDirectory.Length == 0;
            var (target, arguments, directory) = plainLink ? (shortcut.Target, "", "") : Resolve(shortcut);
            info = new(target) { UseShellExecute = true, Arguments = arguments };
            if (directory.Length > 0 && Directory.Exists(directory)) info.WorkingDirectory = directory;
            else if (File.Exists(target) && !plainLink) info.WorkingDirectory = Path.GetDirectoryName(target) ?? "";
            if (admin) info.Verb = "runas";
        }
        try { using var process = Process.Start(info); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { } // The user declined the elevation prompt.
    });

    internal static void RevealInExplorer(Shortcut shortcut)
    {
        var (target, _, _) = Resolve(shortcut);
        var path = File.Exists(target) || Directory.Exists(target) ? target : shortcut.Target;
        if (File.Exists(path) || Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\""))?.Dispose();
    }

    /// <summary>
    /// Copies a .lnk or .url file next to the configuration and points the shortcut at the copy, so that it keeps
    /// working after the user deletes the original from the desktop or unpins it. Start menu shortcuts stay where
    /// they are: installers keep them up to date. Returns true when a target changed.
    /// </summary>
    internal static bool KeepOwnCopy(Shortcut shortcut, string configPath)
    {
        if (shortcut.IsSet) return shortcut.Steps.Count(step => KeepOwnCopy(step, configPath)) > 0;
        var source = shortcut.Target;
        if (!IsLinkFile(source) && !source.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return false;
        if (LaunchTarget.IsLink(source) || !File.Exists(source)) return false;
        var folder = Path.Combine(Path.GetDirectoryName(configPath)!, "ImportedShortcuts");
        bool Inside(string root) => root.Length > 0 && Path.GetFullPath(source).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (Inside(folder) || Inside(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu))
            || Inside(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu))) return false;
        try
        {
            Directory.CreateDirectory(folder);
            var copy = Path.Combine(folder, shortcut.Id + Path.GetExtension(source).ToLowerInvariant());
            File.Copy(source, copy, true);
            shortcut.Target = copy;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; } // The original still works.
    }

    /// <summary>Creates a shortcut model from a file, folder or link dropped or picked by the user.</summary>
    internal static Shortcut? FromPath(string path)
    {
        if (LaunchTarget.IsLink(path))
        {
            var uri = new Uri(path);
            return new Shortcut { Name = uri.Scheme == "steam" ? L.T("Гра Steam") : uri.Host, Target = path };
        }
        if (!LaunchTarget.IsSupportedFile(path)) return null;
        var name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path);
        if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            try { if (FileVersionInfo.GetVersionInfo(path).FileDescription is { Length: > 0 and < 60 } description) name = description.Trim(); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        return new Shortcut { Name = name.Length > 0 ? name : path, Target = path };
    }
}
