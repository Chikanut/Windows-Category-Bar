using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CategoryBar.Core;
using Microsoft.Win32;

namespace CategoryBar;

internal sealed class AppPickerWindow : Window
{
    private readonly Category category;
    private readonly List<Shortcut> available = [];
    private readonly Dictionary<string, Shortcut> chosen = new(StringComparer.OrdinalIgnoreCase);
    private readonly StackPanel rows = new();
    private readonly TextBox search = new() { Margin = new Thickness(0, 12, 0, 12), Height = 32, ToolTip = "Пошук програм" };
    private readonly TextBlock count = new() { Foreground = BarWindow.Brush("#A6A6B0") };
    private readonly Button add = new() { Content = "Додати", Background = BarWindow.Brush("#8EA8FF"), Foreground = BarWindow.Brush("#141826"), Height = 32, IsEnabled = false };
    internal List<Shortcut> Selected { get; } = [];

    internal AppPickerWindow(Category category)
    {
        this.category = category;
        Title = $"Додати до «{category.Name}»"; Width = 520; Height = 500;
        MinWidth = 480; MinHeight = 400; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BarWindow.Brush("#2A2A30");
        var root = new DockPanel { Margin = new Thickness(20) };
        var header = new StackPanel();
        header.Children.Add(new TextBlock { Text = Title, FontSize = 18, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) });
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        var start = new Button { Content = "Меню «Пуск»", Background = BarWindow.Brush("#18FFFFFF") };
        start.Click += (_, _) => { search.Visibility = Visibility.Visible; Refresh(); };
        tabs.Children.Add(start);
        var file = new Button { Content = "Файл або папка…" }; file.Click += (_, _) => { var options = new ContextMenu(); BarWindow.AddItem(options, "Вибрати файли…", PickFiles); BarWindow.AddItem(options, "Вибрати папку…", PickFolder); options.PlacementTarget = file; options.IsOpen = true; }; tabs.Children.Add(file);
        var web = new Button { Content = "Вебпосилання" }; web.Click += (_, _) => AddLink(); tabs.Children.Add(web);
        header.Children.Add(tabs); header.Children.Add(search); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = "Скасувати", IsCancel = true, Height = 32 }; actions.Children.Add(cancel); actions.Children.Add(add);
        DockPanel.SetDock(actions, Dock.Right); footer.Children.Add(actions); footer.Children.Add(count); DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        root.Children.Add(new ScrollViewer { Content = rows, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }); Content = root;
        search.TextChanged += (_, _) => Refresh();
        add.Click += (_, _) => { Selected.AddRange(chosen.Values); DialogResult = true; };
        Loaded += (_, _) =>
        {
            rows.Children.Add(new TextBlock { Text = "Шукаю встановлені програми…", Margin = new Thickness(8) });
            // Shell namespace discovery runs on its own STA thread, never blocking hover/UI.
            var worker = new System.Threading.Thread(() =>
            {
                var apps = ShortcutCatalog.Discover();
                Dispatcher.BeginInvoke(() => { available.AddRange(apps); Refresh(); });
            }) { IsBackground = true };
            worker.SetApartmentState(System.Threading.ApartmentState.STA); worker.Start();
        };
        UpdateCount();
    }

    private void PickFiles()
    {
        var picker = new OpenFileDialog { Title = "Виберіть програми або ярлики", Multiselect = true, Filter = "Програми та ярлики|*.exe;*.lnk;*.url" };
        if (picker.ShowDialog(this) == true)
            foreach (var path in picker.FileNames) Choose(new Shortcut { Name = System.IO.Path.GetFileNameWithoutExtension(path), Target = path });
        Refresh();
    }
    private void PickFolder()
    {
        var folder = new OpenFolderDialog { Title = "Виберіть папку" };
        if (folder.ShowDialog(this) == true) Choose(new Shortcut { Name = new System.IO.DirectoryInfo(folder.FolderName).Name, Target = folder.FolderName });
        Refresh();
    }
    private void AddLink()
    {
        var target = TextDialog.Ask("Вебпосилання або Steam URL", "https://");
        if (target is null) return;
        if (!LaunchTarget.IsLink(target)) { MessageBox.Show(this, "Вкажіть посилання https://, http:// або steam://.", "Посилання"); return; }
        var name = TextDialog.Ask("Назва посилання", new Uri(target).Host);
        if (name is null) return;
        Choose(new Shortcut { Name = name, Target = target }); Refresh();
    }
    private void Choose(Shortcut shortcut)
    {
        if (category.Shortcuts.Any(s => string.Equals(s.Target, shortcut.Target, StringComparison.OrdinalIgnoreCase))) return;
        chosen[shortcut.Target] = shortcut; UpdateCount();
    }
    private void Refresh()
    {
        rows.Children.Clear();
        foreach (var app in available.Concat(chosen.Values).DistinctBy(s => s.Target, StringComparer.OrdinalIgnoreCase)
            .Where(s => s.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)))
        {
            var exists = category.Shortcuts.Any(s => string.Equals(s.Target, app.Target, StringComparison.OrdinalIgnoreCase));
            var label = new StackPanel(); label.Children.Add(new TextBlock { Text = app.Name, TextTrimming = TextTrimming.CharacterEllipsis });
            label.Children.Add(new TextBlock { Text = exists ? "Вже в категорії" : LaunchTarget.IsShellApplication(app.Target) ? "Застосунок Windows / Microsoft Store" : app.Target,
                FontSize = 11, Foreground = BarWindow.Brush("#8E8E98"), TextTrimming = TextTrimming.CharacterEllipsis });
            var box = new CheckBox { Content = label, Foreground = BarWindow.Brush("#ECECEF"), IsEnabled = !exists, IsChecked = exists || chosen.ContainsKey(app.Target), Margin = new Thickness(8, 5, 8, 5) };
            box.Checked += (_, _) => { chosen[app.Target] = app; UpdateCount(); };
            box.Unchecked += (_, _) => { chosen.Remove(app.Target); UpdateCount(); };
            rows.Children.Add(box);
        }
        if (rows.Children.Count == 0) rows.Children.Add(new TextBlock { Text = "Нічого не знайдено. Можна вибрати файл або додати посилання.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) });
    }
    private void UpdateCount() { count.Text = $"Вибрано {chosen.Count}"; add.Content = $"Додати {chosen.Count}"; add.IsEnabled = chosen.Count > 0; }
}
