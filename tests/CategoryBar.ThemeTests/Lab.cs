using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Views;

/// <summary>
/// Visual review renders: `dotnet run --project tests/CategoryBar.ThemeTests -- --lab [themeId…]`.
/// Each theme is drawn as the bar with an open menu and a toast over a wallpaper, plus the settings pages.
/// </summary>
internal static class Lab
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static void Run(string[] ids)
    {
        var folder = Path.GetFullPath(Path.Combine(".local", "theme-lab")); Directory.CreateDirectory(folder);
        var configPath = Path.Combine(folder, "config.json");
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        foreach (var theme in Theme.Library.All.Where(t => ids.Length == 0 || ids.Contains(t.Id)).ToList())
        foreach (var dark in new[] { true, false })
        {
            var config = new Configuration();
            Shortcut App(string name, string file) => new() { Name = name, Target = Path.Combine(windows, file) };
            config.Categories.Add(new Category { Name = "Робота", Shortcuts = [App("Блокнот", "notepad.exe"), App("Провідник", "explorer.exe"), App("Paint", @"System32\mspaint.exe"), App("Термінал", @"System32\cmd.exe"), App("Редактор реєстру", "regedit.exe")] });
            config.Categories.Add(new Category { Name = "Ігри", Shortcuts = [App("Калькулятор", @"System32\calc.exe")] });
            config.Categories.Add(new Category { Name = "Дизайн", Shortcuts = [App("Paint", @"System32\mspaint.exe"), App("Екранна лупа", @"System32\magnify.exe")] });
            config.Categories.Add(new Category { Name = "Утиліти", Shortcuts = [] });
            Theme.Select(config.Settings, theme); config.Settings.ThemeMode = dark ? "Dark" : "Light";
            Theme.Apply(config.Settings);
            var session = new Session(config, new ConfigurationStore(configPath));
            var name = theme.Id + (dark ? "-dark" : "-light");

            var bar = new BarWindow(session, trackTaskbar: false) { Left = -31000, Top = -31000 };
            var toast = new ToastWindow();
            try
            {
                typeof(BarWindow).GetField("monitor", Hidden)!.SetValue(bar, new PixelRect(-32000, -32000, -30000, -30000));
                typeof(BarWindow).GetField("animateTabs", Hidden)!.SetValue(bar, true);
                typeof(BarWindow).GetMethod("Rebuild", Hidden)!.Invoke(bar, null);
                bar.Show(); Pump(150);
                bar.ShowCategory(config.Categories[0]);
                toast.Show(new ToastRequest("Категорію створено", "«Утиліти» — у списку «Ще». Перейменувати можна в налаштуваннях.", ToastKind.Success, true, new ToastAction("Скасувати", () => { })), new PixelRect(-32000, -32000, -30000, -30000), -31000);
                Pump(900);
                var menu = (Window)typeof(BarWindow).GetField("menu", Hidden)!.GetValue(bar)!;
                var barRoot = Root(bar); var menuRoot = Root(menu); var toastRoot = Root(toast);
                const int width = 1000, height = 620;
                var surface = (FrameworkElement)((Panel)barRoot).Children[0];
                var barTop = height - 48 - barRoot.DesiredSize.Height;
                var places = new (Window Window, FrameworkElement Root, double X, double Y)[]
                {
                    (bar, barRoot, 60, barTop),
                    (menu, menuRoot, 60 + surface.Margin.Left + surface.ActualWidth / 2 - menuRoot.DesiredSize.Width / 2 + 60, barTop + surface.Margin.Top - menuRoot.DesiredSize.Height),
                    (toast, toastRoot, width - toastRoot.DesiredSize.Width + 6, barTop + surface.Margin.Top - toastRoot.DesiredSize.Height + 6)
                };
                void Wallpaper(DrawingContext dc)
                {
                    var wallpaper = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
                    foreach (var (color, offset) in dark ? new[] { ("#1B2A4A", 0d), ("#5A2D6E", .45), ("#C2553D", .8), ("#E8A23C", 1) } : new[] { ("#9FD3F5", 0d), ("#F3C6E0", .5), ("#FFE2A8", 1) })
                        wallpaper.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(color), offset));
                    dc.DrawRectangle(wallpaper, null, new Rect(0, 0, width, height));
                    for (var i = 0; i < 9; i++) dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), null, new Point(60 + i * 115, 500 - (i * 97 % 300)), 26 + i * 7 % 40, 26 + i * 7 % 40);
                    // Sharp detail, so bending and blurring are easy to judge.
                    if (bar.TryFindResource("bar.Backdrop") is true)
                        for (var i = 0; i < 14; i++) dc.DrawText(new FormattedText("Category Bar — liquid glass " + i, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 15, dark ? Brushes.White : Brushes.Black, 1), new Point(30 + i % 3 * 310, 150 + i * 30));
                    dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(28, 30, 38) : Color.FromRgb(232, 236, 244)), null, new Rect(0, height - 48, width, 48));
                }
                // Stand-in for the live backdrop: each surface receives the part of the wallpaper it will cover.
                var behind = new DrawingVisual();
                using (var dc = behind.RenderOpen()) Wallpaper(dc);
                var desktop = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); desktop.Render(behind);
                foreach (var place in places)
                {
                    if (place.Window.TryFindResource(place.Window == bar ? "bar.Backdrop" : place.Window == menu ? "menu.Backdrop" : "toast.Backdrop") is not true) continue;
                    var glass = (FrameworkElement)typeof(OverlayWindow).GetField("glass", Hidden)!.GetValue(place.Window)!;
                    var origin = glass.TranslatePoint(new Point(0, 0), place.Root);
                    var area = new Int32Rect((int)Math.Round(place.X + origin.X), (int)Math.Round(place.Y + origin.Y), (int)glass.ActualWidth, (int)glass.ActualHeight);
                    area = new Int32Rect(Math.Max(0, area.X), Math.Max(0, area.Y), Math.Min(area.Width, width - Math.Max(0, area.X)), Math.Min(area.Height, height - Math.Max(0, area.Y)));
                    var raw = new byte[area.Width * area.Height * 4]; desktop.CopyPixels(area, raw, area.Width * 4, 0);
                    var (pixels, w, h) = CategoryBar.Interop.ScreenBackdrop.Soften(raw, area.Width, area.Height, area.Width * 4, place.Window.TryFindResource("bar.BackdropBlur") is double blur ? blur : 0);
                    place.Window.Resources["Theme.Backdrop"] = new ImageBrush(BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgr32, null, pixels, w * 4)) { Stretch = Stretch.Fill };
                    place.Window.Resources["Theme.Backdrop.Enabled"] = 1d;
                }
                Pump(120);
                var scene = new DrawingVisual();
                using (var dc = scene.RenderOpen())
                {
                    Wallpaper(dc);
                    foreach (var place in places) Draw(dc, place.Root, place.X, place.Y);
                }
                Save(scene, width, height, Path.Combine(folder, name + "-desk.png"));
            }
            finally { toast.Close(); bar.Close(); }
            foreach (var view in new[] { "General", "Appearance", "Appearance:560", "Appearance:1500", "Panel", "Categories" })
            {
                var page = view.Split(':')[0];
                var window = new SettingsWindow(session);
                window.Navigate(page);
                var root = (FrameworkElement)window.Content;
                root.Measure(new Size(900, 640)); root.Arrange(new Rect(0, 0, 900, 640)); root.UpdateLayout();
                if (view.Contains(':')) { Scroller(root)?.ScrollToVerticalOffset(double.Parse(view.Split(':')[1])); root.UpdateLayout(); }
                Pump(700);
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Theme.WindowBaseColor), null, new Rect(0, 0, 900, 640));
                    dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(0, 0, 900, 640) }, null, new Rect(0, 0, 900, 640));
                }
                Save(visual, 900, 640, Path.Combine(folder, name + "-" + view.ToLowerInvariant().Replace(':', '-') + ".png"));
                window.Close();
            }
            // Other windows and states that share the theme: dialogs, the tucked-away bar and a menu being edited.
            Window[] others = [new OnboardingWindow(session), new AppPickerWindow(config.Categories[0], _ => { })];
            foreach (var other in others)
            {
                var root = (FrameworkElement)other.Content;
                var size = new Size(other.Width, other.Height);
                root.Measure(size); root.Arrange(new Rect(size)); root.UpdateLayout(); Pump(600);
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(Theme.WindowBaseColor), null, new Rect(size));
                    dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(size) }, null, new Rect(size));
                }
                Save(visual, (int)size.Width, (int)size.Height, Path.Combine(folder, name + "-" + other.GetType().Name.Replace("Window", "").ToLowerInvariant() + ".png"));
                other.Close();
            }
            config.Settings.MiniatureMode = true;
            var tucked = new BarWindow(session, trackTaskbar: false) { Left = -31000, Top = -31000 };
            config.Settings.MiniatureMode = false;
            var editing = new BarWindow(session, trackTaskbar: false) { Left = -31000, Top = -30000 };
            try
            {
                typeof(BarWindow).GetField("monitor", Hidden)!.SetValue(editing, new PixelRect(-32000, -32000, -29000, -29000));
                typeof(BarWindow).GetMethod("Rebuild", Hidden)!.Invoke(editing, null);
                tucked.Show(); editing.Show(); Pump(150);
                editing.ShowCategory(config.Categories[0], edit: true); Pump(900);
                var menu = (Window)typeof(BarWindow).GetField("menu", Hidden)!.GetValue(editing)!;
                var visual = new DrawingVisual();
                using (var dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(70, 60, 90) : Color.FromRgb(200, 215, 235)), null, new Rect(0, 0, 700, 520));
                    dc.DrawRectangle(new SolidColorBrush(dark ? Color.FromRgb(28, 30, 38) : Color.FromRgb(232, 236, 244)), null, new Rect(0, 472, 700, 48));
                    Draw(dc, Root(tucked), 500, 472 - Root(tucked).DesiredSize.Height);
                    var barRoot = Root(editing); var surface = (FrameworkElement)((Panel)barRoot).Children[0];
                    Draw(dc, barRoot, 30, 472 - barRoot.DesiredSize.Height);
                    Draw(dc, Root(menu), 30, 472 - barRoot.DesiredSize.Height + surface.Margin.Top - Root(menu).DesiredSize.Height);
                }
                Save(visual, 700, 520, Path.Combine(folder, name + "-states.png"));
            }
            finally { tucked.Close(); editing.Close(); }
            Console.WriteLine("LAB: " + name);
        }
    }

    /// <summary>The four designs side by side for the README, cut from the desk renders: `-- --lab --showcase`.</summary>
    internal static void Showcase()
    {
        var folder = Path.GetFullPath(Path.Combine(".local", "theme-lab"));
        var crop = new Int32Rect(40, 40, 920, 560);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var index = 0;
            foreach (var name in new[] { "fluent-dark", "glass-dark", "expressive-light", "brutal-light" })
            {
                var image = new CroppedBitmap(new BitmapImage(new Uri(Path.Combine(folder, name + "-desk.png"))), crop);
                dc.DrawImage(image, new Rect(index % 2 * 464, index / 2 * 284, 460, 280));
                index++;
            }
        }
        Save(visual, 924, 564, Path.Combine("Documentation", "screenshots", "designs.png"));
    }

    private static ScrollViewer? Scroller(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer { ScrollableHeight: > 0 } found) return found;
            if (Scroller(child) is { } nested) return nested;
        }
        return null;
    }

    private static FrameworkElement Root(Window window)
    {
        for (var type = window.GetType(); type is not null; type = type.BaseType)
            if (type.GetProperty("Root", Hidden) is { } property) return (FrameworkElement)property.GetValue(window)!;
        throw new InvalidOperationException("No overlay root.");
    }

    private static void Draw(DrawingContext dc, FrameworkElement element, double x, double y)
    {
        // Overlay roots live on a canvas at their measured size, margins included.
        var size = new Rect(new Point(0, 0), element.DesiredSize);
        if (size.Width <= 0 || size.Height <= 0) return;
        var source = VisualTreeHelper.GetParent(element) as Canvas ?? (Visual)element;
        var brush = new VisualBrush(source) { Stretch = Stretch.None, AlignmentX = AlignmentX.Left, AlignmentY = AlignmentY.Top, ViewboxUnits = BrushMappingMode.Absolute, Viewbox = size };
        dc.DrawRectangle(brush, null, new Rect(Math.Round(x), Math.Round(y), size.Width, size.Height));
    }

    private static void Save(Visual visual, int width, int height, string path)
    {
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    internal static void Pump(int milliseconds)
    {
        var frame = new DispatcherFrame(); var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; }; timer.Start(); Dispatcher.PushFrame(frame);
    }
}
