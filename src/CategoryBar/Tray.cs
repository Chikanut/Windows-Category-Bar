using System.Runtime.InteropServices;
using System.Windows.Interop;
using CategoryBar.Interop;
using Microsoft.Win32;

namespace CategoryBar;

/// <summary>The notification-area icon: settings on click, a themed menu on right click.</summary>
internal sealed class Tray : IDisposable
{
    private const uint CallbackMessage = 0x8001;
    private readonly HwndSource source;
    private readonly uint taskbarCreated = Native.RegisterWindowMessage("TaskbarCreated");
    private readonly IntPtr icon;
    private Window? menuHost;

    internal event Action? Clicked;
    internal Func<ContextMenu>? MenuFactory { get; set; }

    internal Tray()
    {
        var icons = new IntPtr[1];
        if (Environment.ProcessPath is { } path) Native.ExtractIconEx(path, 0, null, icons, 1);
        icon = icons[0];
        source = new HwndSource(new HwndSourceParameters("CategoryBar.Tray") { WindowStyle = unchecked((int)0x80000000), Width = 0, Height = 0 });
        source.AddHook(Hook);
        Add();
    }

    private Native.NotifyIconData Data(uint flags) => new()
    {
        Size = Marshal.SizeOf<Native.NotifyIconData>(), Window = source.Handle, Id = 1, Flags = flags,
        CallbackMessage = CallbackMessage, Icon = icon, Tip = L.T("Панель категорій"), Info = "", InfoTitle = ""
    };

    private void Add() { var data = Data(0x7); Native.Shell_NotifyIcon(0, ref data); }

    /// <summary>A Windows notification, used when the bar itself is hidden.</summary>
    internal void Balloon(string title, string text)
    {
        var data = Data(0x10);
        data.InfoTitle = title.Length > 60 ? title[..60] : title;
        data.Info = text.Length > 250 ? text[..250] : text.Length == 0 ? " " : text;
        Native.Shell_NotifyIcon(1, ref data);
    }

    private IntPtr Hook(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)message == taskbarCreated) Add(); // Explorer restarted and forgot the icon.
        else if ((uint)message == CallbackMessage)
        {
            var mouse = lParam.ToInt64() & 0xFFFF;
            if (mouse == 0x202) Clicked?.Invoke();
            else if (mouse == 0x205 && MenuFactory is not null) ShowMenu(MenuFactory());
        }
        return IntPtr.Zero;
    }

    /// <summary>A context menu needs an active window of ours to close when the user clicks elsewhere.</summary>
    private void ShowMenu(ContextMenu menu)
    {
        if (menuHost is null)
        {
            menuHost = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ShowInTaskbar = false,
                Width = 1, Height = 1, Left = -10000, Top = -10000, Topmost = true
            }.Res(Control.FontFamilyProperty, "Font");
            Native.SetExStyle(new WindowInteropHelper(menuHost).EnsureHandle(), Native.ExToolWindow);
        }
        menuHost.Show();
        Native.ForceForeground(new WindowInteropHelper(menuHost).Handle);
        menuHost.Activate();
        menu.PlacementTarget = menuHost;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.Closed += (_, _) => menuHost.Hide();
        menu.IsOpen = true;
    }

    public void Dispose()
    {
        var data = Data(0);
        Native.Shell_NotifyIcon(2, ref data);
        if (icon != IntPtr.Zero) Native.DestroyIcon(icon);
        menuHost?.Close();
        source.Dispose();
    }
}

internal static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WindowsCategoryBar";

    internal static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string { Length: > 0 };
        }
    }

    internal static void SetEnabled(bool enabled, string? configPath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        if (!enabled) { key.DeleteValue(ValueName, throwOnMissingValue: false); return; }
        var executable = Environment.ProcessPath ?? throw new IOException(L.T("Не вдалося визначити шлях застосунку."));
        if (string.Equals(Path.GetFileNameWithoutExtension(executable), "dotnet", StringComparison.OrdinalIgnoreCase))
            throw new IOException(L.T("Для автозапуску запустіть CategoryBar.exe зі збірки."));
        var command = "\"" + executable + "\"";
        if (configPath is not null) command += " --config \"" + Path.GetFullPath(configPath) + "\"";
        key.SetValue(ValueName, command, RegistryValueKind.String);
    }
}
