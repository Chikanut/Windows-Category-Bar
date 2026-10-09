using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using CategoryBar.Core;

namespace CategoryBar;

internal sealed class MenuWindow : Window
{
    internal Category Category { get; }
    private readonly Button anchor;
    private readonly BarWindow bar;
    private readonly StackPanel rows = new();
    private readonly TextBox search = new() { Margin = new Thickness(4, 0, 4, 6), Height = 30, ToolTip = "Пошук у категорії" };
    private readonly ScrollViewer scroll;
    private int selected;
    private Shortcut[] filtered = [];
    private bool contextOpen;
    internal bool ContextOpen => contextOpen;

    internal MenuWindow(Category category, Settings settings, Button anchor, BarWindow bar)
    {
        Category = category; this.anchor = anchor; this.bar = bar;
        Title = category.Name;
        Owner = bar;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true; Background = Brushes.Transparent; ShowInTaskbar = false;
        ShowActivated = false; Topmost = true; Width = 240; SizeToContent = SizeToContent.Height;
        var body = new StackPanel();
        var header = new DockPanel { Margin = new Thickness(10, 6, 10, 8) };
        var count = new TextBlock { Text = category.Shortcuts.Count.ToString(), FontSize = 11, Foreground = BarWindow.Brush("#8E8E98"), HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(count, Dock.Right); header.Children.Add(count);
        header.Children.Add(new TextBlock { Text = category.Name.ToUpperInvariant(), FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = BarWindow.Brush("#8E8E98") });
        body.Children.Add(header);
        if (category.Shortcuts.Count >= settings.SearchThreshold) body.Children.Add(search);
        search.TextChanged += (_, _) => { selected = 0; RefreshRows(); };
        scroll = new ScrollViewer { Content = rows, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        body.Children.Add(scroll);
        body.Children.Add(new Border { Height = 1, Background = BarWindow.Brush("#12FFFFFF"), Margin = new Thickness(4, 5, 4, 4) });
        var add = new Button { Content = "＋  Додати застосунок", Height = 32, Foreground = BarWindow.Brush("#A6A6B0") };
        add.Click += (_, _) => bar.PickFiles(category);
        body.Children.Add(add);
        var surface = new Border { Background = BarWindow.Brush("#FA24242A"), BorderBrush = BarWindow.Brush("#18FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(6), Child = body };
        // This transparent strip is a real input surface connecting the tab and menu.
        Content = new Border { Background = Brushes.Transparent, Padding = new Thickness(0, 0, 0, 8), Child = surface };
        MouseEnter += (_, _) => bar.KeepMenu();
        MouseLeave += (_, _) => { if (!contextOpen) bar.ScheduleClose(); };
        AllowDrop = true;
        Drop += (_, e) => { if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) bar.AddFiles(category, paths); e.Handled = true; };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape) { Escape(); e.Handled = true; }
            else if (e.Key is Key.Down or Key.Up)
            {
                if (filtered.Length == 0) return;
                selected = (selected + (e.Key == Key.Down ? 1 : -1) + filtered.Length) % filtered.Length;
                Highlight(); ((Button)rows.Children[selected]).BringIntoView(); e.Handled = true;
            }
            else if (e.Key == Key.Enter && filtered.Length > 0) { bar.Launch(filtered[selected]); e.Handled = true; }
        };
        RefreshRows();
    }

    internal void Escape()
    {
        if (search.Text.Length > 0) search.Clear(); else bar.CloseMenu();
    }

    internal void Position(PixelRect monitor)
    {
        UpdateLayout();
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;
        var dpi = NativeMethods.GetDpiForWindow(handle);
        var scale = (dpi == 0 ? 96 : dpi) / 96.0;
        var location = anchor.PointToScreen(new Point(anchor.ActualWidth / 2, 0));
        var room = Math.Max(80, (location.Y - monitor.Top) / scale - 90);
        if (Math.Abs(scroll.MaxHeight - Math.Min(360, room)) > 1) { scroll.MaxHeight = Math.Min(360, room); UpdateLayout(); }
        var w = (int)Math.Ceiling(ActualWidth * scale);
        var h = (int)Math.Ceiling(ActualHeight * scale);
        var x = (int)Math.Clamp(location.X - w / 2.0, monitor.Left, Math.Max(monitor.Left, monitor.Right - w));
        var y = Math.Max(monitor.Top, (int)location.Y - h);
        NativeMethods.SetWindowPos(handle, new IntPtr(-1), x, y, w, h, 0x0010);
    }

    private void RefreshRows()
    {
        rows.Children.Clear();
        filtered = Category.Shortcuts.Where(s => s.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        selected = Math.Clamp(selected, 0, Math.Max(0, filtered.Length - 1));
        if (filtered.Length == 0)
        {
            rows.Children.Add(new TextBlock
            {
                Text = Category.Shortcuts.Count == 0 ? "Тут поки порожньо\nПеретягніть сюди ярлик, .exe або папку" : "Нічого не знайдено",
                TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap, Foreground = BarWindow.Brush("#8E8E98"), Margin = new Thickness(10, 18, 10, 18)
            });
            return;
        }
        foreach (var shortcut in filtered)
        {
            var missing = !File.Exists(shortcut.Target) && !Directory.Exists(shortcut.Target);
            var content = new DockPanel { LastChildFill = true };
            var image = ShellIcon.Load(shortcut.Target);
            UIElement icon = image is not null ? new Image { Source = image, Width = 24, Height = 24, Margin = new Thickness(0, 0, 10, 0) }
                : new TextBlock { Text = "▧", FontSize = 22, Width = 34 };
            DockPanel.SetDock(icon, Dock.Left); content.Children.Add(icon);
            if (missing)
            {
                var warning = new TextBlock { Text = "⚠", Foreground = BarWindow.Brush("#F2A541"), Margin = new Thickness(4, 0, 0, 0) };
                DockPanel.SetDock(warning, Dock.Right); content.Children.Add(warning);
            }
            content.Children.Add(new TextBlock { Text = shortcut.Name, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = BarWindow.Brush(missing ? "#8E8E98" : "#ECECEF") });
            var row = new Button { Content = content, Height = 36, Padding = new Thickness(10, 0, 10, 0), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = shortcut.Name + "\n" + shortcut.Target };
            row.Click += (_, _) => bar.Launch(shortcut);
            var context = new ContextMenu();
            context.Opened += (_, _) => { contextOpen = true; bar.KeepMenu(); };
            context.Closed += (_, _) => { contextOpen = false; bar.ScheduleClose(); };
            BarWindow.AddItem(context, "Запустити", () => bar.Launch(shortcut));
            BarWindow.AddItem(context, "Від імені адміністратора", () => bar.Launch(shortcut, true));
            BarWindow.AddItem(context, "Відкрити розташування файлу", () =>
            {
                if (File.Exists(shortcut.Target)) Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{shortcut.Target}\"") { UseShellExecute = true });
                else bar.Notify("Файл не знайдено", shortcut.Target);
            });
            BarWindow.AddItem(context, "Перейменувати…", () =>
            {
                var name = TextDialog.Ask("Назва ярлика", shortcut.Name);
                if (name is not null) { shortcut.Name = name; bar.SaveAndRefresh(); }
            });
            BarWindow.AddItem(context, "Перемістити вище", () => Move(shortcut, -1));
            BarWindow.AddItem(context, "Перемістити нижче", () => Move(shortcut, 1));
            var move = new MenuItem { Header = "Перемістити до" };
            foreach (var destination in bar.Categories.Where(c => c != Category))
            {
                var item = new MenuItem { Header = destination.Name };
                item.Click += (_, _) => { Category.Shortcuts.Remove(shortcut); destination.Shortcuts.Add(shortcut); bar.SaveAndRefresh(); };
                move.Items.Add(item);
            }
            context.Items.Add(move);
            BarWindow.AddItem(context, "Прибрати з категорії", () => { Category.Shortcuts.Remove(shortcut); bar.SaveAndRefresh(); });
            row.ContextMenu = context;
            rows.Children.Add(row);
        }
    }

    private void Highlight()
    {
        for (var i = 0; i < rows.Children.Count; i++)
            if (rows.Children[i] is Button button) button.Background = i == selected ? BarWindow.Brush("#18FFFFFF") : Brushes.Transparent;
    }
    private void Move(Shortcut shortcut, int delta)
    {
        var index = Category.Shortcuts.IndexOf(shortcut);
        var next = index + delta;
        if (next < 0 || next >= Category.Shortcuts.Count) return;
        Category.Shortcuts.RemoveAt(index); Category.Shortcuts.Insert(next, shortcut); bar.SaveAndRefresh();
    }
}

internal static class ShellIcon
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct FileInfo
    {
        public IntPtr Icon; public int Index; public uint Attributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SHGetFileInfo(string path, uint attributes, out FileInfo info, uint size, uint flags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    internal static ImageSource? Load(string path)
    {
        if (SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(24, 24));
            image.Freeze(); return image;
        }
        finally { DestroyIcon(info.Icon); }
    }
}
