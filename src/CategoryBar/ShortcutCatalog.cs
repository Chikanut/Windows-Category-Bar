using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using CategoryBar.Core;

namespace CategoryBar;

internal static class ShortcutCatalog
{
    internal static List<Shortcut> Discover()
    {
        var result = new List<Shortcut>();
        foreach (var root in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
            Scan(Environment.GetFolderPath(root), result);
        var shellType = Type.GetTypeFromProgID("Shell.Application");
        if (shellType is not null)
        {
            object? shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                folder = ((dynamic)shell!).NameSpace("shell:AppsFolder");
                items = ((dynamic)folder!).Items();
                foreach (dynamic item in (dynamic)items)
                {
                    try
                    {
                        string name = item.Name, id = item.Path;
                        if (name.Length > 0 && id.Contains('!'))
                            result.Add(new Shortcut { Name = name, Target = "shell:AppsFolder\\" + id });
                    }
                    finally { Marshal.FinalReleaseComObject(item); }
                }
            }
            catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException) { }
            finally
            {
                foreach (var value in new[] { items, folder, shell }) if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
        }
        return result.DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase).OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static void Scan(string root, List<Shortcut> result)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var path in Directory.EnumerateFiles(root).Where(p => Path.GetExtension(p).ToLowerInvariant() is ".lnk" or ".url"))
                result.Add(new Shortcut { Name = Path.GetFileNameWithoutExtension(path), Target = path });
            foreach (var folder in Directory.EnumerateDirectories(root))
                if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) == 0) Scan(folder, result);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    internal static (string Target, string Arguments, string WorkingDirectory) Resolve(Shortcut shortcut)
    {
        if (!shortcut.Target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || !File.Exists(shortcut.Target))
            return (shortcut.Target, shortcut.Arguments, shortcut.WorkingDirectory);
        object? shell = null, link = null;
        try
        {
            var type = Type.GetTypeFromProgID("WScript.Shell");
            if (type is null) return (shortcut.Target, shortcut.Arguments, shortcut.WorkingDirectory);
            shell = Activator.CreateInstance(type);
            link = ((dynamic)shell!).CreateShortcut(shortcut.Target);
            string target = ((dynamic)link!).TargetPath, args = ((dynamic)link).Arguments, working = ((dynamic)link).WorkingDirectory;
            return (target.Length == 0 ? shortcut.Target : target, shortcut.Arguments.Length > 0 ? shortcut.Arguments : args,
                shortcut.WorkingDirectory.Length > 0 ? shortcut.WorkingDirectory : working);
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        { return (shortcut.Target, shortcut.Arguments, shortcut.WorkingDirectory); }
        finally
        {
            foreach (var value in new[] { link, shell }) if (value is not null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }
    }
}
