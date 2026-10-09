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
    private readonly Panel rows;
    private readonly bool gridMode;
    private readonly TextBox search = new() { Margin = new Thickness(4, 0, 4, 6), Height = 30, ToolTip = "Пошук у категорії" };
    private readonly ScrollViewer scroll;
    private int selected;
    private Shortcut[] filtered = [];
    private bool contextOpen;
    internal bool ContextOpen => contextOpen;
    internal bool Editing { get; private set; }
    private readonly Button done = new() { Content = "Готово", Visibility = Visibility.Collapsed };
    internal void SetEditing(bool value) { Editing = value; done.Visibility = value ? Visibility.Visible : Visibility.Collapsed; RefreshRows(); }

    internal MenuWindow(Category category, Settings settings, Button anchor, BarWindow bar)
    {
        Category = category; this.anchor = anchor; this.bar = bar;
        gridMode = settings.GridForLargeCategories && category.Shortcuts.Count >= 20;
        rows = gridMode ? new WrapPanel() : new StackPanel();
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
        done.Click += (_, _) => { SetEditing(false); bar.CloseMenu(); }; body.Children.Add(done);
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
            var missing = !LaunchTarget.Exists(shortcut.Target);
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
            if (gridMode)
            {
                var tile = new StackPanel();
                if (icon is Image tileImage) tile.Children.Add(new Image { Source = tileImage.Source, Width = 28, Height = 28, Margin = new Thickness(0, 4, 0, 5) });
                else tile.Children.Add(new TextBlock { Text = "▧", FontSize = 24, HorizontalAlignment = HorizontalAlignment.Center });
                tile.Children.Add(new TextBlock { Text = shortcut.Name, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, FontSize = 11 });
                row.Content = tile; row.Width = 74; row.Height = 70; row.Padding = new Thickness(4);
            }
            row.Click += (_, _) => { if (!Editing) bar.Launch(shortcut); };
            row.AllowDrop = true;
            var origin = new Point();
            row.PreviewMouseLeftButtonDown += (_, e) => origin = e.GetPosition(row);
            row.PreviewMouseMove += (_, e) =>
            {
                if (!Editing || e.LeftButton != MouseButtonState.Pressed) return;
                var point = e.GetPosition(row);
                if (Math.Abs(point.X - origin.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - origin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
                DragDrop.DoDragDrop(row, new DataObject("CategoryBar.Shortcut", shortcut.Id), DragDropEffects.Move);
            };
            row.DragOver += (_, e) => { e.Effects = e.Data.GetDataPresent("CategoryBar.Shortcut") ? DragDropEffects.Move : e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; row.BorderBrush = BarWindow.Brush("#8EA8FF"); row.BorderThickness = new Thickness(0, 2, 0, 0); };
            row.DragLeave += (_, _) => row.BorderThickness = new Thickness(0);
            row.Drop += (_, e) =>
            {
                if (e.Data.GetData("CategoryBar.Shortcut") is Guid id)
                {
                    var source = bar.Categories.FirstOrDefault(c => c.Shortcuts.Any(s => s.Id == id));
                    var moving = source?.Shortcuts.First(s => s.Id == id);
                    if (source is not null && moving is not null) bar.Change(() => { var index = Category.Shortcuts.IndexOf(shortcut); source.Shortcuts.Remove(moving); Category.Shortcuts.Insert(Math.Clamp(index, 0, Category.Shortcuts.Count), moving); }, "Ярлик переміщено", moving.Name);
                }
                else if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) bar.AddFiles(Category, paths, Category.Shortcuts.IndexOf(shortcut));
                e.Handled = true;
            };
            if (Editing) { var grip = new TextBlock { Text = "⠿", Width = 18, Foreground = BarWindow.Brush("#8E8E98") }; DockPanel.SetDock(grip, Dock.Left); content.Children.Insert(0, grip); }
            var context = new ContextMenu();
            context.Opened += (_, _) => { contextOpen = true; bar.KeepMenu(); };
            context.Closed += (_, _) => { contextOpen = false; bar.ScheduleClose(); };
            BarWindow.AddItem(context, "Запустити", () => bar.Launch(shortcut));
            BarWindow.AddItem(context, "Від імені адміністратора", () => bar.Launch(shortcut, true));
            BarWindow.AddItem(context, "Відкрити розташування файлу", () => bar.OpenLocation(shortcut));
            BarWindow.AddItem(context, "Перейменувати…", () =>
            {
                Editing = true; done.Visibility = Visibility.Visible;
                var input = new TextBox { Text = shortcut.Name, MaxLength = 100, Height = 30 };
                row.Content = input; var finished = false;
                void Finish(bool commit)
                {
                    if (finished) return; finished = true;
                    var name = input.Text.Trim();
                    if (commit && name.Length > 0 && name != shortcut.Name) bar.Change(() => shortcut.Name = name, "Назву змінено", name);
                    else RefreshRows();
                }
                input.PreviewKeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Escape) { e.Handled = true; Finish(e.Key == Key.Enter); } };
                input.LostKeyboardFocus += (_, _) => Finish(true); input.Focus(); input.SelectAll();
            });
            BarWindow.AddItem(context, "Перемістити вище", () => Move(shortcut, -1));
            BarWindow.AddItem(context, "Перемістити нижче", () => Move(shortcut, 1));
            var move = new MenuItem { Header = "Перемістити до" };
            foreach (var destination in bar.Categories.Where(c => c != Category))
            {
                var item = new MenuItem { Header = destination.Name };
                item.Click += (_, _) => { bar.Change(() => { Category.Shortcuts.Remove(shortcut); destination.Shortcuts.Add(shortcut); }, "Ярлик переміщено", destination.Name); };
                move.Items.Add(item);
            }
            context.Items.Add(move);
            BarWindow.AddItem(context, "Властивості…", () => bar.EditProperties(shortcut));
            BarWindow.AddItem(context, "Редагувати ярлики", () => SetEditing(true));
            BarWindow.AddItem(context, "Прибрати з категорії", () => bar.Change(() => Category.Shortcuts.Remove(shortcut), "Ярлик прибрано", shortcut.Name));
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
        bar.Change(() => ConfigurationEditor.Move(Category.Shortcuts, shortcut, next), "Порядок ярликів змінено", shortcut.Name);
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
    private static readonly System.Collections.Generic.Dictionary<string, ImageSource> cache = new(StringComparer.OrdinalIgnoreCase);
    [StructLayout(LayoutKind.Sequential)] private struct ImageSize { public int Width, Height; }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(ImageSize size, int flags, out IntPtr bitmap); }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)] private static extern int SHCreateItemFromParsingName(string path, IntPtr context, ref Guid id, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? item);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr bitmap);
    internal static ImageSource? Load(string path)
    {
        if (cache.TryGetValue(path, out var cached)) return cached;
        if (LaunchTarget.IsShellApplication(path))
        {
            var id = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref id, out var item) == 0 && item is not null)
            {
                try
                {
                    if (item.GetImage(new ImageSize { Width = 32, Height = 32 }, 4, out var bitmap) == 0 && bitmap != IntPtr.Zero)
                    {
                        try { var image = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions()); image.Freeze(); cache[path] = image; return image; }
                        finally { DeleteObject(bitmap); }
                    }
                }
                finally { Marshal.FinalReleaseComObject(item); }
            }
            return null;
        }
        if (SHGetFileInfo(path, 0, out var info, (uint)Marshal.SizeOf<FileInfo>(), 0x100) == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;
        try
        {
            var image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, System.Windows.Media.Imaging.BitmapSizeOptions.FromWidthAndHeight(24, 24));
            image.Freeze(); if (cache.Count > 512) cache.Clear(); cache[path] = image; return image;
        }
        finally { DestroyIcon(info.Icon); }
    }
}
