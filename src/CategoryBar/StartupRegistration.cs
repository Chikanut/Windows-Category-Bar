using System;
using System.IO;
using Microsoft.Win32;

namespace CategoryBar;

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsCategoryBar";
    internal static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
    }
    internal static void SetEnabled(bool enabled, string configPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (!enabled) { key.DeleteValue(ValueName, throwOnMissingValue: false); return; }
        var executable = Environment.ProcessPath ?? throw new IOException("Не вдалося визначити шлях застосунку.");
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Для автозапуску запустіть CategoryBar.exe зі збірки.");
        key.SetValue(ValueName, "\"" + executable + "\" --config \"" + Path.GetFullPath(configPath) + "\"", RegistryValueKind.String);
    }
}
