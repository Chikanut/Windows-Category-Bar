using CategoryBar.Services;
using Microsoft.Win32;

namespace CategoryBar.Views;

/// <summary>Chooses installed applications, files, folders or links to add to a category.</summary>
internal sealed class AppPickerWindow : ChromeWindow
{
    private readonly Category category;
    private readonly Action<List<Shortcut>> accept;
    private readonly HashSet<string> existing;
    private readonly StackPanel list = new();
    private readonly TextBox search = new() { Tag = L.T("Пошук серед установлених програм"), Padding = new Thickness(30, 0, 8, 0), MinHeight = 34 };
    private readonly TextBlock status = Ui.Text("", 12.5, "Text.Muted");
    private readonly Button add;
    private readonly List<(CatalogEntry Entry, Border Row, CheckBox Check)> rows = [];

    /// <summary>Opens the picker for a category of the live configuration.</summary>
    internal static void Open(Session session, Category category) =>
        new AppPickerWindow(category, shortcuts => session.AddShortcuts(category, shortcuts)).Surface();

    internal AppPickerWindow(Category category, Action<List<Shortcut>> accept) : base(L.T("Додати застосунки"), 640, 660)
    {
        this.category = category; this.accept = accept;
        existing = category.Shortcuts.Select(Shortcuts.Identity).ToHashSet();

        var heading = Ui.Text(L.F("Додати до «{0}»", category.Name), 18, "Text", bold: true); heading.Margin = new Thickness(22, 2, 22, 12);
        var searchHost = new Grid { Margin = new Thickness(22, 0, 22, 10) };
        searchHost.Children.Add(search);
        var glass = Ui.Icon(Icons.Search, "Text.Faint", 14); glass.HorizontalAlignment = HorizontalAlignment.Left; glass.Margin = new Thickness(11, 0, 0, 0);
        searchHost.Children.Add(glass);

        var loading = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0) };
        loading.Children.Add(Ui.Spinner(16));
        var loadingText = Ui.Text(L.T("Шукаємо встановлені програми…"), 13, "Text.Muted"); loadingText.Margin = new Thickness(10, 0, 0, 0);
        loading.Children.Add(loadingText);
        list.Children.Add(loading);
        var scroller = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = list, Padding = new Thickness(16, 0, 16, 0) };

        var extras = new StackPanel { Orientation = Orientation.Horizontal };
        extras.Children.Add(Ui.Button(L.T("Файл…"), PickFiles, icon: Icons.External));
        var folder = Ui.Button(L.T("Папка…"), PickFolder, icon: Icons.Folder); folder.Margin = new Thickness(8, 0, 0, 0);
        var link = Ui.Button(L.T("Посилання…"), () => { if (Dialogs.PromptLink() is { } shortcut) Finish([shortcut]); }, icon: Icons.Globe); link.Margin = new Thickness(8, 0, 0, 0);
        extras.Children.Add(folder); extras.Children.Add(link);

        add = Ui.Button(L.T("Додати"), () => Finish(rows.Where(r => r.Check.IsChecked == true).Select(r => new Shortcut { Name = r.Entry.Name, Target = r.Entry.Target }).ToList()), "Button.Primary");
        add.MinWidth = 110; add.MinHeight = 32; add.IsEnabled = false; add.Margin = new Thickness(8, 0, 0, 0);
        var cancel = Ui.Button(L.T("Скасувати"), Close); cancel.MinHeight = 32;
        var footer = new Grid { Margin = new Thickness(22, 12, 22, 18) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(extras);
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { cancel, add } }.At(1));

        var layout = new DockPanel();
        DockPanel.SetDock(heading, Dock.Top); DockPanel.SetDock(searchHost, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
        var divider = new Border { Height = 1 }.Res(Border.BackgroundProperty, "Divider"); DockPanel.SetDock(divider, Dock.Bottom);
        status.Margin = new Thickness(22, 8, 22, 8); DockPanel.SetDock(status, Dock.Bottom);
        layout.Children.Add(heading); layout.Children.Add(searchHost); layout.Children.Add(footer); layout.Children.Add(divider); layout.Children.Add(status); layout.Children.Add(scroller);
        Body.Children.Add(layout);

        search.TextChanged += (_, _) => Filter();
        Loaded += async (_, _) =>
        {
            search.Focus();
            try { Populate(await AppCatalog.Installed()); }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException)
            {
                list.Children.Clear();
                list.Children.Add(Ui.Text(L.T("Не вдалося прочитати список програм. Додайте файл або папку вручну."), 13, "Text.Muted", wrap: true));
            }
        };
    }

    private void Populate(List<CatalogEntry> entries)
    {
        list.Children.Clear(); rows.Clear();
        foreach (var entry in entries)
        {
            var present = existing.Contains(Shortcuts.Identity(new Shortcut { Name = entry.Name, Target = entry.Target }));
            var check = new CheckBox { IsChecked = present, IsEnabled = !present, IsHitTestVisible = false, Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            texts.Children.Add(Ui.Text(entry.Name, 13, present ? "Text.Faint" : "Text"));
            texts.Children.Add(Ui.Text(present ? L.T("Уже в категорії") : entry.Source + " · " + entry.SuggestedCategory, 11.5, "Text.Faint"));
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = Ui.AppIcon(entry.Name, entry.Target, 28); icon.Margin = new Thickness(12, 0, 0, 0);
            if (present) icon.Opacity = .5;
            line.Children.Add(check); line.Children.Add(icon.At(1)); line.Children.Add(texts.At(2));
            var row = new Border { Height = 46, Padding = new Thickness(10, 0, 10, 0), Background = Brushes.Transparent, Child = line }.Res(Border.CornerRadiusProperty, "Radius.Small");
            if (!present)
            {
                row.MouseEnter += (_, _) => row.SetResourceReference(Border.BackgroundProperty, "Hover");
                row.MouseLeave += (_, _) => row.Background = Brushes.Transparent;
                row.MouseLeftButtonUp += (_, _) => { check.IsChecked = check.IsChecked != true; UpdateSelection(); };
            }
            rows.Add((entry, row, check));
            list.Children.Add(row);
        }
        Filter();
        UpdateSelection();
    }

    private void Filter()
    {
        var query = search.Text.Trim();
        var shown = 0;
        foreach (var (entry, row, _) in rows)
        {
            var match = query.Length == 0 || entry.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase);
            row.Visibility = match ? Visibility.Visible : Visibility.Collapsed;
            if (match) shown++;
        }
        if (rows.Count > 0 && shown == 0) status.Text = L.F("Нічого не знайдено за «{0}». Спробуйте «Файл…».", query);
        else UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = rows.Count(r => r.Check.IsChecked == true && r.Check.IsEnabled);
        add.IsEnabled = selected > 0;
        add.Content = selected > 0 ? L.F("Додати {0}", selected) : L.T("Додати");
        status.Text = selected > 0 ? L.F("Вибрано: {0}", selected) : rows.Count > 0 ? L.F("Програм: {0}. Позначте потрібні.", rows.Count) : "";
    }

    private void PickFiles()
    {
        var dialog = new OpenFileDialog { Title = L.T("Виберіть програми або ярлики"), Filter = L.T("Програми та ярлики|*.exe;*.lnk;*.url|Усі файли|*.*"), Multiselect = true, DereferenceLinks = false };
        if (dialog.ShowDialog(this) == true) Finish(dialog.FileNames.Select(Shortcuts.FromPath).OfType<Shortcut>().ToList());
    }

    private void PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = L.T("Виберіть папку"), Multiselect = true };
        if (dialog.ShowDialog(this) == true) Finish(dialog.FolderNames.Select(Shortcuts.FromPath).OfType<Shortcut>().ToList());
    }

    private void Finish(List<Shortcut> shortcuts)
    {
        var chosen = shortcuts.Where(s => rows.All(r => r.Entry.Target != s.Target || r.Check.IsEnabled)).ToList();
        Close();
        if (chosen.Count > 0) accept(chosen);
    }
}
