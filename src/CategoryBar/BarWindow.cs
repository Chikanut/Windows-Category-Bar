using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CategoryBar.Core;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace CategoryBar;

public sealed class BarWindow : Window
{
    private readonly Configuration config;
    private readonly ConfigurationStore store;
    private readonly StackPanel tabs = new() { Orientation = Orientation.Horizontal };
    private readonly DispatcherTimer tracker = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer openTimer = new();
    private readonly DispatcherTimer closeTimer = new();
    private readonly Forms.NotifyIcon tray;
    private MenuWindow? menu;
    private Category? pending;
    private Button? anchor;
    private IntPtr handle;
    private bool temporarilyHidden;
    private PixelRect monitor;
    private uint taskbarCreated;
    private bool lastEscape;
    private TaskbarObservation? lastObservation;
    private readonly string logPath;
    private SettingsWindow? settingsWindow;
    internal System.Collections.Generic.IEnumerable<Category> Categories => config.Categories;

    public BarWindow(Configuration config, ConfigurationStore store)
    {
        this.config = config;
        this.store = store;
        logPath = Path.Combine(Path.GetDirectoryName(store.Path)!, "taskbar.log");
        Title = "Панель категорій";
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        SizeToContent = SizeToContent.Width;
        Height = 34;
        FontFamily = new FontFamily("Segoe UI");
        Content = new Border
        {
            Background = Brush("#F226262C"), BorderBrush = Brush("#18FFFFFF"),
            BorderThickness = new Thickness(1, 1, 1, 0), CornerRadius = new CornerRadius(9, 9, 0, 0),
            Padding = new Thickness(3, 2, 3, 3), Child = tabs
        };
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "Панель категорій", Visible = true };
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Налаштування…", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        trayMenu.Items.Add("Створити категорію", null, (_, _) => Dispatcher.Invoke(CreateCategory));
        trayMenu.Items.Add("Сховати / показати панель", null, (_, _) => Dispatcher.Invoke(() => { temporarilyHidden = !temporarilyHidden; UpdatePosition(); }));
        var startupItem = new Forms.ToolStripMenuItem("Запускати з Windows");
        startupItem.Click += (_, _) => Dispatcher.Invoke(() =>
        {
            try { StartupRegistration.SetEnabled(!StartupRegistration.IsEnabled, store.Path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { Notify("Не вдалося змінити автозапуск", ex.Message); }
        });
        trayMenu.Opening += (_, _) => startupItem.Checked = StartupRegistration.IsEnabled;
        trayMenu.Items.Add(startupItem);
        trayMenu.Items.Add("Відкрити конфігурацію", null, (_, _) => Process.Start(new ProcessStartInfo("explorer.exe", Path.GetDirectoryName(store.Path)!) { UseShellExecute = true }));
        trayMenu.Items.Add(new Forms.ToolStripSeparator());
        trayMenu.Items.Add("Вийти", null, (_, _) => Dispatcher.Invoke(() => Application.Current.Shutdown()));
        tray.ContextMenuStrip = trayMenu;
        tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
        SourceInitialized += (_, _) =>
        {
            handle = new WindowInteropHelper(this).Handle;
            var extended = NativeMethods.GetWindowLongPtr(handle, -20).ToInt64();
            NativeMethods.SetWindowLongPtr(handle, -20, new IntPtr(extended | 0x80 | 0x08000000));
            taskbarCreated = NativeMethods.RegisterWindowMessage("TaskbarCreated");
            HwndSource.FromHwnd(handle)?.AddHook(WindowMessage);
            UpdatePosition();
            tracker.Start();
        };
        tracker.Tick += (_, _) =>
        {
            UpdatePosition();
            var escape = (NativeMethods.GetAsyncKeyState(0x1B) & 0x8000) != 0;
            if (escape && !lastEscape && menu?.IsActive != true) menu?.Escape();
            lastEscape = escape;
        };
        openTimer.Tick += (_, _) => { openTimer.Stop(); if (pending is not null && anchor is not null) OpenMenu(pending, anchor); };
        closeTimer.Tick += (_, _) => { closeTimer.Stop(); if (!IsMouseOver && menu?.IsMouseOver != true && menu?.ContextOpen != true) CloseMenu(); };
        MouseEnter += (_, _) => closeTimer.Stop();
        MouseLeave += (_, _) => ScheduleClose();
        Closed += (_, _) => { tracker.Stop(); openTimer.Stop(); closeTimer.Stop(); CloseMenu(); tray.Dispose(); };
        RebuildTabs();
    }

    internal static SolidColorBrush Brush(string value) => (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == taskbarCreated || message is 0x007E or 0x02E0 or 0x001A)
            Dispatcher.BeginInvoke(UpdatePosition);
        return IntPtr.Zero;
    }

    private void UpdatePosition()
    {
        if (handle == IntPtr.Zero) return;
        var observation = NativeMethods.Observe();
        if (lastObservation != observation)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 1_000_000) File.Move(logPath, logPath + ".previous", true);
                File.AppendAllText(logPath, $"{DateTimeOffset.Now:O} show={VisibilityPolicy.ShouldShow(observation)} {observation}\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            lastObservation = observation;
        }
        var bottomTaskbar = observation.Taskbar.Width > observation.Taskbar.Height
            && observation.Taskbar.Bottom >= observation.Monitor.Bottom - 2;
        if (temporarilyHidden || !bottomTaskbar || !VisibilityPolicy.ShouldShow(observation))
        {
            CloseMenu();
            if (IsVisible) Hide();
            return;
        }
        monitor = observation.Monitor;
        var dpi = NativeMethods.GetDpiForWindow(NativeMethods.FindWindow("Shell_TrayWnd", null));
        var scale = (dpi == 0 ? 96 : dpi) / 96.0;
        var newMax = Math.Max(140, monitor.Width * .6 / scale);
        if (Math.Abs(MaxWidth - newMax) > 1 || double.IsPositiveInfinity(MaxWidth))
        {
            MaxWidth = newMax;
            RebuildTabs();
        }
        UpdateLayout();
        var width = (int)Math.Ceiling(ActualWidth * scale);
        var height = (int)Math.Ceiling(34 * scale);
        var leftAligned = config.Settings.Alignment == "Left";
        if (config.Settings.Alignment == "Windows")
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced");
            leftAligned = key?.GetValue("TaskbarAl") is int alignment && alignment == 0;
        }
        var x = leftAligned ? monitor.Left + (int)(8 * scale) : monitor.Left + (monitor.Width - width) / 2;
        var y = observation.Taskbar.Top - height;
        // Topmost is applied only after the taskbar visibility policy permits it.
        NativeMethods.SetWindowPos(handle, new IntPtr(-1), x, y, width, height, 0x0010);
        if (!IsVisible) Show();
        menu?.Position(monitor);
    }

    private void RebuildTabs()
    {
        CloseMenu();
        tabs.Children.Clear();
        if (config.Categories.Count == 0) { AddTabAction("＋ Створити першу категорію", CreateCategory); return; }
        var budget = double.IsFinite(MaxWidth) ? MaxWidth - 90 : 800;
        var used = 0.0;
        var visible = 0;
        foreach (var category in config.Categories)
        {
            var tab = BuildTab(category);
            tab.Measure(new Size(double.PositiveInfinity, 28));
            if (used + tab.DesiredSize.Width > budget && visible > 0) break;
            used += tab.DesiredSize.Width;
            tabs.Children.Add(tab);
            visible++;
        }
        if (visible < config.Categories.Count)
        {
            var more = new Button { Content = $"Ще {config.Categories.Count - visible} ⌃" };
            more.Click += (_, _) =>
            {
                var context = new ContextMenu();
                foreach (var category in config.Categories.Skip(visible))
                    AddItem(context, category.Name, () => OpenMenu(category, more));
                context.PlacementTarget = more;
                context.IsOpen = true;
            };
            tabs.Children.Add(more);
        }
        AddTabAction("＋", CreateCategory);
        AddTabAction("⚙", ShowSettings);
    }

    private void AddTabAction(string text, Action action)
    {
        var button = new Button { Content = text, ToolTip = text };
        button.Click += (_, _) => action();
        tabs.Children.Add(button);
    }

    private Button BuildTab(Category category)
    {
        var label = new StackPanel { Orientation = Orientation.Horizontal };
        label.Children.Add(new TextBlock { Text = category.Name, MaxWidth = 150, TextTrimming = TextTrimming.CharacterEllipsis });
        if (config.Settings.ShowCounts) label.Children.Add(new TextBlock { Text = category.Shortcuts.Count.ToString(), Foreground = Brush("#8E8E98"), FontSize = 11, Margin = new Thickness(6, 1, 0, 0) });
        var button = new Button { Content = label, ToolTip = category.Name, AllowDrop = true };
        button.MouseEnter += (_, _) =>
        {
            closeTimer.Stop();
            if (menu?.Category == category) return;
            if (menu is not null) { OpenMenu(category, button); return; }
            pending = category; anchor = button;
            openTimer.Stop(); openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, config.Settings.OpenDelayMs)); openTimer.Start();
        };
        button.MouseLeave += (_, _) => { openTimer.Stop(); ScheduleClose(); };
        button.Click += (_, _) => { openTimer.Stop(); if (menu?.Category == category) CloseMenu(); else OpenMenu(category, button); };
        var context = new ContextMenu();
        AddItem(context, "Додати застосунок…", () => PickFiles(category));
        AddItem(context, "Перейменувати…", () =>
        {
            var name = TextDialog.Ask("Назва категорії", category.Name);
            if (name is not null) { category.Name = name; SaveAndRefresh(); }
        });
        AddItem(context, "Перемістити ліворуч", () => MoveCategory(category, -1));
        AddItem(context, "Перемістити праворуч", () => MoveCategory(category, 1));
        AddItem(context, "Видалити категорію…", () =>
        {
            if (category.Shortcuts.Count > 0 && MessageBox.Show($"Прибрати категорію «{category.Name}» та {category.Shortcuts.Count} ярликів? Програми залишаться на комп’ютері.", "Видалення категорії", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
            config.Categories.Remove(category); SaveAndRefresh();
        });
        button.ContextMenu = context;
        button.Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddFiles(category, paths); e.Handled = true; };
        button.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        return button;
    }

    internal static void AddItem(ContextMenu menu, string label, Action action)
    {
        var item = new MenuItem { Header = label };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }

    private void MoveCategory(Category category, int delta)
    {
        var index = config.Categories.IndexOf(category);
        var target = index + delta;
        if (target < 0 || target >= config.Categories.Count) return;
        config.Categories.RemoveAt(index); config.Categories.Insert(target, category); SaveAndRefresh();
    }

    private void CreateCategory()
    {
        CloseMenu();
        var name = TextDialog.Ask("Створити категорію", "");
        if (name is null) return;
        config.Categories.Add(new Category { Name = name }); SaveAndRefresh();
    }

    private void OpenMenu(Category category, Button target)
    {
        CloseMenu();
        anchor = target;
        target.Background = Brush("#18FFFFFF");
        menu = new MenuWindow(category, config.Settings, target, this);
        menu.Show(); menu.Position(monitor);
    }

    internal void ScheduleClose()
    {
        openTimer.Stop();
        closeTimer.Stop(); closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, config.Settings.CloseDelayMs)); closeTimer.Start();
    }
    internal void KeepMenu() => closeTimer.Stop();
    internal void CloseMenu()
    {
        openTimer.Stop(); closeTimer.Stop();
        if (anchor is not null) anchor.Background = Brushes.Transparent;
        menu?.Close(); menu = null;
    }

    internal void PickFiles(Category category)
    {
        CloseMenu();
        var picker = new OpenFileDialog { Title = "Додати застосунки", Multiselect = true, Filter = "Програми та ярлики|*.exe;*.lnk;*.url" };
        if (picker.ShowDialog() == true) AddFiles(category, picker.FileNames);
    }

    internal void AddFiles(Category category, string[] paths)
    {
        CloseMenu();
        foreach (var path in paths)
        {
            var extension = Path.GetExtension(path).ToLowerInvariant();
            if (!Directory.Exists(path) && (extension is not (".exe" or ".lnk" or ".url") || !File.Exists(path)))
            { Notify("Непідтримуваний файл", Path.GetFileName(path)); continue; }
            if (category.Shortcuts.Any(s => string.Equals(s.Target, path, StringComparison.OrdinalIgnoreCase)))
            { Notify("Ярлик уже додано", Path.GetFileName(path)); continue; }
            category.Shortcuts.Add(new Shortcut { Name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path), Target = path });
        }
        SaveAndRefresh();
    }

    internal void Launch(Shortcut shortcut, bool admin = false)
    {
        CloseMenu();
        try
        {
            if (!File.Exists(shortcut.Target) && !Directory.Exists(shortcut.Target)) throw new FileNotFoundException("Ярлик переміщено або видалено.", shortcut.Target);
            // Launch the original .lnk through ShellExecute, preserving its parameters and working directory.
            var start = new ProcessStartInfo(shortcut.Target, shortcut.Arguments) { UseShellExecute = true };
            if (shortcut.WorkingDirectory.Length > 0) start.WorkingDirectory = shortcut.WorkingDirectory;
            if (admin) start.Verb = "runas";
            Process.Start(start)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        { Notify("Не вдалося запустити «" + shortcut.Name + "»", ex.Message); }
    }

    internal void SaveAndRefresh()
    {
        try { store.Save(config); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notify("Не вдалося зберегти", ex.Message); }
        RebuildTabs(); UpdatePosition();
    }

    internal void Notify(string title, string message)
    {
        tray.ShowBalloonTip(6000, title, message, Forms.ToolTipIcon.Info);
    }

    private void ShowSettings()
    {
        CloseMenu();
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(config, store.Path, () =>
        {
            store.Save(config);
            RebuildTabs(); UpdatePosition();
        });
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }
}
