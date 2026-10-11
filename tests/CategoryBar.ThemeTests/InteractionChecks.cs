using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CategoryBar;
using CategoryBar.Core;
using CategoryBar.Views;

internal static class InteractionChecks
{
    internal static void Run(Settings settings, string configPath)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var button = Ui.Button("", () => { });
        button.Content = new StackPanel { IsHitTestVisible = false, Children = { new Border { Height = 90 }, Ui.Text("Theme preview") } };
        button.Width = 280; button.Height = 150;
        var host = new Window { Content = button, Width = 280, Height = 150, Left = -32000, Top = -32000, ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None };
        host.Show(); button.ApplyTemplate(); Layout(button, 280, 150);
        foreach (var point in new[] { new Point(140, 15), new Point(140, 60), new Point(140, 130), new Point(3, 75) })
            if (button.InputHitTest(point) is not DependencyObject hit || !Within(hit, button)) throw new Exception($"Theme button has an unclickable area at {point}; visible={button.IsVisible}, template={button.Template != null}.");
        host.Close();
        var config = new Configuration { Settings = settings }; settings.MiniatureMode = true;
        var bar = new BarWindow(new Session(config, new ConfigurationStore(configPath)), trackTaskbar: false);
        try
        {
            var tabs = (StackPanel)typeof(BarWindow).GetField("tabs", flags)!.GetValue(bar)!;
            bar.Left = -32000; bar.Top = -32000; bar.Width = 120; bar.Height = 30; bar.Show();
            var handle = (ContentControl)tabs.Children[0];
            Layout(handle, 80, 13);
            if (handle.InputHitTest(new Point(handle.ActualWidth / 2, handle.ActualHeight / 2)) is not DependencyObject hit || !Within(hit, handle))
                throw new Exception("Miniature arrow has no pointer hit target.");
            handle.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            if (!(bool)typeof(BarWindow).GetField("expanded", flags)!.GetValue(bar)!) throw new Exception("Clicking miniature arrow does not expand the bar.");
        }
        finally { bar.Close(); settings.MiniatureMode = false; }
        Console.WriteLine($"PASS: {settings.ThemeId} {settings.ThemeMode} full button and arrow hit targets, arrow click");
    }

    internal static void Colors(string configPath)
    {
        var config = new Configuration(); var settings = config.Settings;
        settings.ThemeMode = "Dark"; Theme.Select(settings, BuiltInThemes.All[0]);
        settings.ThemeColors["fluent"] = new() { ["Dark:Card.Bg"] = "#135724", ["Light:Card.Bg"] = "#FEDCBA", ["Dark:Text"] = "#EECCAA", ["bad"] = "invalid" };
        config.Validate();
        if (settings.ThemeColors["fluent"].ContainsKey("bad")) throw new Exception("Invalid palette override was retained.");
        new ConfigurationStore(configPath).Save(config);
        settings = new ConfigurationStore(configPath).Load().Settings; Theme.Apply(settings);
        if (((SolidColorBrush)Application.Current.Resources["Card.Bg"]).Color != Theme.Parse("#135724")) throw new Exception("Card override does not reach actual theme resources.");
        var exported = ThemeFiles.Parse(ThemeFiles.Serialize(Theme.Capture(settings)));
        if (exported.Dark["Card.Bg"] != "#135724" || exported.Light["Card.Bg"] != "#FEDCBA") throw new Exception("Export dropped independent color overrides.");
        Theme.Select(settings, BuiltInThemes.All.Single(t => t.Id == "midnight")); Theme.Apply(settings);
        if (((SolidColorBrush)Application.Current.Resources["Card.Bg"]).Color == Theme.Parse("#135724")) throw new Exception("Color overrides leaked into another theme.");
        Theme.Select(settings, BuiltInThemes.All[0]); settings.ThemeMode = "Light"; Theme.Apply(settings);
        if (((SolidColorBrush)Application.Current.Resources["Card.Bg"]).Color != Theme.Parse("#FEDCBA")) throw new Exception("Light and dark overrides are mixed.");
        Theme.Select(settings, BuiltInThemes.All.Single(t => t.Id == "expressive")); settings.ThemeMode = "Dark"; Theme.Apply(settings);
        var editor = new SettingsWindow(new Session(new Configuration { Settings = settings }, new ConfigurationStore(configPath)));
        try
        {
            editor.Navigate("Appearance"); var root = (FrameworkElement)editor.Content; Layout(root, 900, 640);
            var input = Descendants(root).OfType<TextBox>().Single(t => System.Windows.Automation.AutomationProperties.GetName(t) == L.T("Фон елементів керування"));
            input.Text = "#123456";
            input.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, 0, input, null) { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            if (new ConfigurationStore(configPath).Load().Settings.ThemeColors["expressive"]["Dark:Control.Bg"] != "#123456"
                || ((SolidColorBrush)Application.Current.Resources["input.Background"]).Color != Theme.Parse("#123456")
                || ((SolidColorBrush)Application.Current.Resources["button.Background"]).Color != Theme.Parse("#123456"))
                throw new Exception("Palette editor does not persist or override authored control backgrounds.");
            var controlExport = Theme.Capture(settings);
            if (ThemeStyleSheet.Parse(controlExport.StyleSheet).Value("input", "background", "") != "var(--control-bg)" || controlExport.Dark["Control.Bg"] != "#123456")
                throw new Exception("Export does not preserve control color overrides over authored CSS backgrounds.");
        }
        finally { editor.Close(); }
        Console.WriteLine("PASS: theme colors persist, affect resources, export and remain independent by theme and mode");
    }

    internal static void Gallery(SettingsWindow window, FrameworkElement root)
    {
        window.Left = -32000; window.Top = -32000; window.Show(); root.UpdateLayout();
        foreach (var button in Descendants(root).OfType<Button>().Where(b => BuiltInThemes.All.Any(t => t.Name == System.Windows.Automation.AutomationProperties.GetName(b))))
            foreach (var point in new[] { new Point(button.ActualWidth / 2, 25), new Point(button.ActualWidth / 2, 75), new Point(8, button.ActualHeight / 2) })
                if (button.InputHitTest(point) is not DependencyObject hit || !Within(hit, button)) throw new Exception("Real theme gallery has a dead preview/text/padding area.");
        Console.WriteLine("PASS: real gallery preview, title and padding hit targets");
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        { var child = VisualTreeHelper.GetChild(root, i); yield return child; foreach (var descendant in Descendants(child)) yield return descendant; }
    }

    private static void Layout(FrameworkElement element, double width, double height)
    { element.Measure(new Size(width, height)); element.Arrange(new Rect(0, 0, width, height)); element.UpdateLayout(); }
    private static bool Within(DependencyObject hit, DependencyObject ancestor)
    { for (DependencyObject? node = hit; node is not null; node = VisualTreeHelper.GetParent(node)) if (node == ancestor) return true; return false; }
}
