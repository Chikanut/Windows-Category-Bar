using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
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
    private readonly IntPtr attachedTaskbar;
    private readonly BarWindow? primary;
    private readonly ConfigurationEditor editor;
    private readonly Dictionary<IntPtr, BarWindow> secondaryBars = [];
    private readonly Dictionary<Guid, Border> accentLines = [];
    private DateTime nextMonitorCheck;
    private AppBarReservation? reservation;
    private ToastWindow? toast;
    private Button? overflowButton;
    private bool renaming;
    internal System.Collections.Generic.IEnumerable<Category> Categories => config.Categories;

    public BarWindow(Configuration config, ConfigurationStore store) : this(config, store, IntPtr.Zero, null) { }
    private BarWindow(Configuration config, ConfigurationStore store, IntPtr taskbar, BarWindow? primary)
    {
        this.config = config;
        this.store = store;
        attachedTaskbar = taskbar; this.primary = primary;
        editor = primary?.editor ?? new ConfigurationEditor(config, store.Save);
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
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "Панель категорій", Visible = primary is null };
        var trayMenu = new Forms.ContextMenuStrip();
        trayMenu.Items.Add("Налаштування…", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        trayMenu.Items.Add("Створити категорію", null, (_, _) => Dispatcher.Invoke(CreateCategory));
        trayMenu.Items.Add("Скасувати останню зміну", null, (_, _) => Dispatcher.Invoke(Undo));
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
        Closed += (_, _) =>
        {
            tracker.Stop(); openTimer.Stop(); closeTimer.Stop(); CloseMenu(); toast?.Close(); reservation?.Dispose(); tray.Dispose();
            foreach (var secondary in secondaryBars.Values.ToArray()) secondary.Close();
        };
        Loaded += (_, _) =>
        {
            if (primary is not null || config.Settings.HasSeenWelcome) return;
            config.Settings.HasSeenWelcome = true;
            try { store.Save(config); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            Notify("Панель категорій працює", "Категорії — над панеллю Windows. Налаштування та вихід доступні в треї.", "Створити категорію", CreateCategory);
        };
        RebuildTabs();
    }

    internal static SolidColorBrush Brush(string value) => (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;

    private IntPtr WindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == taskbarCreated) { reservation?.Dispose(); reservation = null; nextMonitorCheck = DateTime.MinValue; }
        if (message == taskbarCreated || message is 0x007E or 0x02E0 or 0x001A)
            Dispatcher.BeginInvoke(UpdatePosition);
        return IntPtr.Zero;
    }

    private void UpdatePosition()
    {
        if (handle == IntPtr.Zero) return;
        if (primary is null && DateTime.UtcNow >= nextMonitorCheck)
        {
            nextMonitorCheck = DateTime.UtcNow.AddSeconds(1);
            SyncMonitors();
        }
        var observation = NativeMethods.Observe(attachedTaskbar);
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
        if (temporarilyHidden || primary?.temporarilyHidden == true || !bottomTaskbar || !VisibilityPolicy.ShouldShow(observation))
        {
            CloseMenu();
            toast?.Close(); toast = null;
            reservation?.Dispose(); reservation = null;
            if (IsVisible) Hide();
            return;
        }
        monitor = observation.Monitor;
        var dpi = NativeMethods.GetDpiForWindow(attachedTaskbar == IntPtr.Zero ? NativeMethods.FindWindow("Shell_TrayWnd", null) : attachedTaskbar);
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
        if (config.Settings.ReserveSpace && !observation.AutoHide)
        {
            reservation ??= new AppBarReservation();
            reservation.Update(monitor, height, observation.Taskbar.Top);
        }
        else { reservation?.Dispose(); reservation = null; }
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
        accentLines.Clear(); overflowButton = null;
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
            overflowButton = more;
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
        var content = new Grid(); content.Children.Add(label);
        var line = new Border { Height = 2, Background = Brush("#8EA8FF"), CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, -6), Visibility = Visibility.Collapsed };
        content.Children.Add(line); accentLines[category.Id] = line;
        var button = new Button { Content = content, Tag = category.Id, ToolTip = category.Name, AllowDrop = true };
        var dragOrigin = new Point();
        button.PreviewMouseLeftButtonDown += (_, e) => dragOrigin = e.GetPosition(button);
        button.PreviewMouseMove += (_, e) =>
        {
            if (renaming || e.LeftButton != MouseButtonState.Pressed) return;
            var point = e.GetPosition(button);
            if (Math.Abs(point.X - dragOrigin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - dragOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
            CloseMenu(); DragDrop.DoDragDrop(button, new DataObject("CategoryBar.Category", category.Id), DragDropEffects.Move);
        };
        button.MouseDoubleClick += (_, e) => { e.Handled = true; RenameInline(category, button); };
        button.MouseEnter += (_, _) =>
        {
            closeTimer.Stop();
            if (renaming || menu?.ContextOpen == true || menu?.Editing == true || menu?.Category == category) return;
            if (menu is not null) { OpenMenu(category, button); return; }
            pending = category; anchor = button;
            openTimer.Stop(); openTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, config.Settings.OpenDelayMs)); openTimer.Start();
        };
        button.MouseLeave += (_, _) => { openTimer.Stop(); ScheduleClose(); };
        button.Click += (_, _) => { if (renaming) return; openTimer.Stop(); if (menu?.Category == category) CloseMenu(); else OpenMenu(category, button); };
        var context = new ContextMenu();
        AddItem(context, "Додати застосунок…", () => PickFiles(category));
        AddItem(context, "Перейменувати…", () =>
        {
            RenameInline(category, button);
        });
        AddItem(context, "Редагувати ярлики", () => OpenMenu(category, button, true));
        AddItem(context, "Нова категорія", CreateCategory);
        AddItem(context, "Перемістити ліворуч", () => MoveCategory(category, -1));
        AddItem(context, "Перемістити праворуч", () => MoveCategory(category, 1));
        AddItem(context, "Видалити категорію…", () =>
        {
            var dialog = new DeleteCategoryWindow(category, config.Categories);
            if (category.Shortcuts.Count > 0 && dialog.ShowDialog() != true) return;
            Change(() =>
            {
                if (dialog.Destination is { } destination) destination.Shortcuts.AddRange(category.Shortcuts);
                config.Categories.Remove(category);
            }, "Категорію прибрано", $"«{category.Name}». Програми залишилися на комп’ютері.");
        });
        button.ContextMenu = context;
        button.Drop += (_, e) =>
        {
            openTimer.Stop();
            if (e.Data.GetData("CategoryBar.Category") is Guid id && config.Categories.FirstOrDefault(c => c.Id == id) is { } moving)
                Change(() => ConfigurationEditor.Move(config.Categories, moving, config.Categories.IndexOf(category)), "Порядок категорій змінено", "Перетягування збережено.");
            else if (e.Data.GetData("CategoryBar.Shortcut") is Guid shortcutId && config.Categories.FirstOrDefault(c => c.Shortcuts.Any(s => s.Id == shortcutId)) is { } source)
            {
                var shortcut = source.Shortcuts.First(s => s.Id == shortcutId);
                Change(() => { source.Shortcuts.Remove(shortcut); category.Shortcuts.Add(shortcut); }, "Ярлик переміщено", category.Name);
            }
            else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddFiles(category, paths);
            e.Handled = true;
        };
        button.DragEnter += (_, e) =>
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            pending = category; anchor = button; openTimer.Stop(); openTimer.Interval = TimeSpan.FromMilliseconds(600); openTimer.Start();
        };
        button.DragOver += (_, e) =>
        {
            e.Effects = (e.Data.GetDataPresent("CategoryBar.Category") || e.Data.GetDataPresent("CategoryBar.Shortcut")) ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
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
        Change(() => ConfigurationEditor.Move(config.Categories, category, target), "Порядок категорій змінено", category.Name);
    }

    private void CreateCategory()
    {
        CloseMenu();
        var name = TextDialog.Ask("Створити категорію", "");
        if (name is null) return;
        Change(() => config.Categories.Add(new Category { Name = name }), "Категорію створено", name);
    }

    private void OpenMenu(Category category, Button target, bool editing = false)
    {
        CloseMenu();
        anchor = target;
        target.Background = Brush("#18FFFFFF");
        if (accentLines.TryGetValue(category.Id, out var line)) line.Visibility = Visibility.Visible;
        menu = new MenuWindow(category, config.Settings, target, this);
        menu.SetEditing(editing);
        menu.Show(); menu.Position(monitor);
    }

    internal void ScheduleClose()
    {
        if (renaming || menu?.Editing == true) return;
        openTimer.Stop();
        closeTimer.Stop(); closeTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, config.Settings.CloseDelayMs)); closeTimer.Start();
    }
    internal void KeepMenu() => closeTimer.Stop();
    internal void CloseMenu()
    {
        openTimer.Stop(); closeTimer.Stop();
        if (anchor is not null) anchor.Background = Brushes.Transparent;
        foreach (var line in accentLines.Values) line.Visibility = Visibility.Collapsed;
        menu?.Close(); menu = null;
    }

    internal void PickFiles(Category category)
    {
        CloseMenu();
        var picker = new AppPickerWindow(category);
        if (picker.ShowDialog() == true)
            Change(() => category.Shortcuts.AddRange(picker.Selected), $"Додано {picker.Selected.Count} застосунків", $"До «{category.Name}».");
    }

    internal void AddFiles(Category category, string[] paths, int? insertionIndex = null)
    {
        var shortcuts = new List<Shortcut>();
        foreach (var path in paths)
        {
            if (!LaunchTarget.IsSupportedFile(path))
            { Notify("Непідтримуваний файл", Path.GetFileName(path)); continue; }
            if (category.Shortcuts.Any(s => string.Equals(s.Target, path, StringComparison.OrdinalIgnoreCase)))
            {
                if (MessageBox.Show($"{Path.GetFileName(path)} уже є в «{category.Name}». Додати ще один ярлик?", "Повторний ярлик", MessageBoxButton.YesNo) != MessageBoxResult.Yes) continue;
            }
            shortcuts.Add(new Shortcut { Name = Directory.Exists(path) ? new DirectoryInfo(path).Name : Path.GetFileNameWithoutExtension(path), Target = path });
        }
        if (shortcuts.Count > 0) Change(() => category.Shortcuts.InsertRange(Math.Clamp(insertionIndex ?? category.Shortcuts.Count, 0, category.Shortcuts.Count), shortcuts), $"Додано {shortcuts.Count} застосунків", $"До «{category.Name}».");
    }

    internal void Launch(Shortcut shortcut, bool admin = false)
    {
        CloseMenu();
        try
        {
            if (!LaunchTarget.Exists(shortcut.Target)) throw new FileNotFoundException("Ярлик переміщено або видалено.", shortcut.Target);
            // Launch the original .lnk through ShellExecute, preserving its parameters and working directory.
            admin |= shortcut.RunAsAdministrator;
            var resolved = (admin || shortcut.Arguments.Length > 0 || shortcut.WorkingDirectory.Length > 0) ? ShortcutCatalog.Resolve(shortcut) : (shortcut.Target, shortcut.Arguments, shortcut.WorkingDirectory);
            var start = LaunchTarget.IsShellApplication(shortcut.Target)
                ? new ProcessStartInfo("explorer.exe", "\"" + shortcut.Target + "\"") { UseShellExecute = true }
                : new ProcessStartInfo(resolved.Item1, resolved.Item2) { UseShellExecute = true };
            if (resolved.Item3.Length > 0) start.WorkingDirectory = resolved.Item3;
            if (admin && !LaunchTarget.IsShellApplication(shortcut.Target)) start.Verb = "runas";
            Process.Start(start)?.Dispose();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223) { }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException)
        { Notify("Не вдалося запустити «" + shortcut.Name + "»", ex.Message, "Знайти файл", () => RepairShortcut(shortcut), true); }
    }

    internal void SaveAndRefresh()
    {
        try { store.Save(config); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notify("Не вдалося зберегти", ex.Message); }
        RefreshAll();
    }

    internal void Notify(string title, string message, string? actionLabel = null, Action? action = null, bool error = false)
    {
        if (primary is not null && !IsVisible) { primary.Notify(title, message, actionLabel, action, error); return; }
        if (!IsVisible) { (primary?.tray ?? tray).ShowBalloonTip(6000, title, message, error ? Forms.ToolTipIcon.Error : Forms.ToolTipIcon.Info); return; }
        toast?.Close(); toast = new ToastWindow(title, message, actionLabel, action, error);
        toast.Show(); toast.UpdateLayout();
        var point = PointToScreen(new Point(ActualWidth, 0));
        var toastHandle = new WindowInteropHelper(toast).Handle;
        var dpi = NativeMethods.GetDpiForWindow(toastHandle) / 96.0;
        var x = (int)Math.Clamp(point.X - toast.ActualWidth * dpi, monitor.Left, Math.Max(monitor.Left, monitor.Right - toast.ActualWidth * dpi));
        var y = Math.Max(monitor.Top, (int)(point.Y - toast.ActualHeight * dpi - 12 * dpi));
        NativeMethods.SetWindowPos(toastHandle, new IntPtr(-1), x, y, (int)(toast.ActualWidth * dpi), (int)(toast.ActualHeight * dpi), 0x0010);
    }

    private void ShowSettings()
    {
        if (primary is not null) { primary.ShowSettings(); return; }
        CloseMenu();
        if (settingsWindow is not null) { settingsWindow.Activate(); return; }
        settingsWindow = new SettingsWindow(config, store.Path, () =>
        {
            store.Save(config);
            RefreshAll();
        });
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    private void SyncMonitors()
    {
        var handles = config.Settings.AllMonitors ? NativeMethods.Taskbars().Where(h => h != NativeMethods.FindWindow("Shell_TrayWnd", null)).ToArray() : [];
        foreach (var stale in secondaryBars.Keys.Except(handles).ToArray()) { secondaryBars[stale].Close(); secondaryBars.Remove(stale); }
        foreach (var taskbar in handles.Where(h => !secondaryBars.ContainsKey(h)))
        {
            var secondary = new BarWindow(config, store, taskbar, this); secondaryBars.Add(taskbar, secondary); secondary.Show();
        }
    }

    private void RefreshAll()
    {
        if (primary is not null) { primary.RefreshAll(); return; }
        RebuildTabs(); SyncMonitors(); UpdatePosition();
        foreach (var secondary in secondaryBars.Values) { secondary.RebuildTabs(); secondary.UpdatePosition(); }
    }

    internal void Change(Action change, string title, string message)
    {
        var pinned = menu?.Editing == true ? menu.Category.Id : (Guid?)null;
        try { editor.Change(change); RefreshAll(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        { RefreshAll(); Notify("Не вдалося зберегти", ex.Message, error: true); return; }
        if (pinned is Guid id && config.Categories.FirstOrDefault(c => c.Id == id) is { } category)
        {
            var target = tabs.Children.OfType<Button>().FirstOrDefault(b => Equals(b.Tag, id)) ?? overflowButton;
            if (target is not null) OpenMenu(category, target, true);
        }
        Notify(title, message, "Скасувати", Undo);
    }

    private void Undo()
    {
        try { editor.Undo(); RefreshAll(); Notify("Зміну скасовано", "Попередній стан відновлено."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { RefreshAll(); Notify("Не вдалося скасувати", ex.Message, error: true); }
    }

    private void RenameInline(Category category, Button button)
    {
        CloseMenu(); renaming = true;
        var input = new TextBox { Text = category.Name, MinWidth = 70, MaxWidth = 150, MaxLength = 100 };
        button.Content = input; var finished = false;
        void Finish(bool save)
        {
            if (finished) return; finished = true; renaming = false;
            var name = input.Text.Trim();
            if (save && name.Length > 0 && name != category.Name) Change(() => category.Name = name, "Назву змінено", name);
            else RebuildTabs();
        }
        input.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Escape) { e.Handled = true; Finish(e.Key == Key.Enter); } };
        input.LostKeyboardFocus += (_, _) => Finish(true);
        input.Focus(); input.SelectAll();
    }

    internal void RepairShortcut(Shortcut shortcut)
    {
        var picker = new OpenFileDialog { Title = "Знайти файл для «" + shortcut.Name + "»", Filter = "Програми та ярлики|*.exe;*.lnk;*.url" };
        if (picker.ShowDialog() == true) Change(() => shortcut.Target = picker.FileName, "Ярлик виправлено", shortcut.Name);
    }

    internal void OpenLocation(Shortcut shortcut)
    {
        try
        {
            var target = ShortcutCatalog.Resolve(shortcut).Target;
            var arguments = LaunchTarget.IsShellApplication(target) ? "shell:AppsFolder" : File.Exists(target) ? "/select,\"" + target + "\"" : Directory.Exists(target) ? "\"" + target + "\"" : throw new FileNotFoundException("Розташування файлу недоступне.");
            Process.Start(new ProcessStartInfo("explorer.exe", arguments) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { Notify("Не вдалося відкрити розташування", ex.Message, error: true); }
    }

    internal void EditProperties(Shortcut shortcut)
    {
        CloseMenu(); var dialog = new ShortcutPropertiesWindow(shortcut);
        if (dialog.ShowDialog() == true) Change(() => dialog.Apply(shortcut), "Властивості збережено", shortcut.Name);
    }
}
