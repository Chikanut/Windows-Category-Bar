using System.Text;
using CategoryBar.Interop;
using CategoryBar.Services;

namespace CategoryBar.Views;

/// <summary>
/// Builds a set: several applications behind one icon. Applications picked from the running ones keep their
/// window positions; applications picked from the installed list start the usual way.
/// </summary>
internal sealed class SetWindow : ChromeWindow
{
    private const int MaxSteps = 30;
    private sealed record Row(Shortcut Step, IntPtr Window, Border Element, CheckBox Check);

    private readonly Session session;
    private readonly Category category;
    private readonly TextBox name = new() { MaxLength = 100, Tag = L.T("Назва набору"), MinHeight = 34 };
    private readonly TextBox search = new() { Tag = L.T("Пошук"), Padding = new Thickness(30, 0, 8, 0), MinHeight = 34 };
    private readonly StackPanel runningList = new(), installedList = new();
    private readonly ScrollViewer scroller = new() { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(16, 0, 16, 0) };
    private readonly TextBlock hint = Ui.Text("", 12.5, "Text.Muted", wrap: true);
    private readonly TextBlock status = Ui.Text("", 12.5, "Text.Muted");
    private readonly List<Row> running = [], installed = [];
    private readonly Button create;
    private bool showInstalled;

    internal static void Open(Session session, Category category) => new SetWindow(session, category).Surface();

    private SetWindow(Session session, Category category) : base(L.T("Новий набір"), 640, 720)
    {
        this.session = session; this.category = category;
        name.Text = FreshName();

        var heading = Ui.Text(L.F("Новий набір у «{0}»", category.Name), 18, "Text", bold: true); heading.Margin = new Thickness(22, 2, 22, 4);
        var about = Ui.Text(L.T("Одна іконка, яка запускає кілька застосунків разом."), 12.5, "Text.Muted", wrap: true); about.Margin = new Thickness(22, 0, 22, 12);
        var nameHost = new Grid { Margin = new Thickness(22, 0, 22, 12), Children = { name } };

        var modes = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var (label, value) in new[] { (L.T("Запущені зараз"), false), (L.T("Усі програми"), true) })
        {
            var button = new RadioButton { Content = label, GroupName = "SetSource", IsChecked = value == showInstalled, Margin = new Thickness(1, 0, 1, 0) }.Res(StyleProperty, "Segment");
            button.Checked += (_, _) => { showInstalled = value; ShowSource(); };
            modes.Children.Add(button);
        }
        var modeHost = new Border { Padding = new Thickness(2), BorderThickness = new Thickness(1), Child = modes, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(22, 0, 22, 8) }
            .Res(Border.BackgroundProperty, "Control.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius.Small");
        hint.Margin = new Thickness(22, 0, 22, 10); hint.LineHeight = 18;

        var searchHost = new Grid { Margin = new Thickness(22, 0, 22, 10) };
        searchHost.Children.Add(search);
        var glass = Ui.Icon(Icons.Search, "Text.Faint", 14); glass.HorizontalAlignment = HorizontalAlignment.Left; glass.Margin = new Thickness(11, 0, 0, 0);
        searchHost.Children.Add(glass);

        create = Ui.Button(L.T("Створити набір"), Accept, "Button.Primary");
        create.MinWidth = 130; create.MinHeight = 32; create.IsEnabled = false; create.Margin = new Thickness(8, 0, 0, 0);
        var cancel = Ui.Button(L.T("Скасувати"), Close); cancel.MinHeight = 32;
        var footer = new Grid { Margin = new Thickness(22, 12, 22, 18) };
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        footer.Children.Add(status);
        footer.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { cancel, create } }.At(1));

        var layout = new DockPanel();
        foreach (var top in new FrameworkElement[] { heading, about, nameHost, modeHost, hint, searchHost }) { DockPanel.SetDock(top, Dock.Top); layout.Children.Add(top); }
        DockPanel.SetDock(footer, Dock.Bottom); layout.Children.Add(footer);
        var divider = new Border { Height = 1 }.Res(Border.BackgroundProperty, "Divider"); DockPanel.SetDock(divider, Dock.Bottom); layout.Children.Add(divider);
        layout.Children.Add(scroller);
        Body.Children.Add(layout);

        runningList.Children.Add(Loading(L.T("Шукаємо відкриті вікна…")));
        installedList.Children.Add(Loading(L.T("Шукаємо встановлені програми…")));
        search.TextChanged += (_, _) => Filter();
        name.TextChanged += (_, _) => UpdateSelection();
        ShowSource();
        Loaded += async (_, _) =>
        {
            try
            {
                var catalog = await AppCatalog.Installed();
                Populate(installedList, installed, catalog.Select(e => (new Shortcut { Name = e.Name, Target = e.Target }, IntPtr.Zero, e.Source)));
                PopulateRunning(await ShellWorker.Run(() => FindRunning(catalog)));
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException or IOException)
            {
                foreach (var (list, rows) in new[] { (runningList, running), (installedList, installed) })
                {
                    if (rows.Count > 0) continue;
                    list.Children.Clear();
                    list.Children.Add(Message(L.T("Не вдалося прочитати список програм.")));
                }
            }
        };
    }

    private string FreshName()
    {
        var names = category.Shortcuts.Select(s => s.Name).ToHashSet(StringComparer.CurrentCultureIgnoreCase);
        if (!names.Contains(L.T("Набір"))) return L.T("Набір");
        for (var i = 2; ; i++) if (!names.Contains(L.F("Набір {0}", i))) return L.F("Набір {0}", i);
    }

    private static StackPanel Loading(string text)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 60, 0, 0) };
        panel.Children.Add(Ui.Spinner(16));
        var label = Ui.Text(text, 13, "Text.Muted"); label.Margin = new Thickness(10, 0, 0, 0);
        panel.Children.Add(label);
        return panel;
    }

    private static TextBlock Message(string text)
    {
        var message = Ui.Text(text, 13, "Text.Muted", wrap: true);
        message.TextAlignment = TextAlignment.Center; message.Margin = new Thickness(20, 50, 20, 0); message.LineHeight = 19;
        return message;
    }

    private sealed record RunningApp(IntPtr Window, string Path, string AppId, string Title, bool Minimized, CatalogEntry? Known);

    /// <summary>
    /// One entry per running application. Matching against the installed list reuses the status tracker's rules,
    /// so an application started through a launcher is saved as its Start menu shortcut, not as a versioned file.
    /// </summary>
    private static List<RunningApp> FindRunning(List<CatalogEntry> catalog)
    {
        var candidates = catalog.Select(e => (Entry: e, Shortcut: new Shortcut { Name = e.Name, Target = e.Target })).ToList();
        var (matched, _) = AppStatus.Inspect(candidates.Select(c => c.Shortcut).ToList());
        var known = new Dictionary<IntPtr, CatalogEntry>();
        foreach (var (entry, shortcut) in candidates)
            if (matched.TryGetValue(shortcut.Id, out var window)) known.TryAdd(window, entry);

        var own = Environment.ProcessPath ?? "";
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var text = new StringBuilder(512);
        var result = new List<RunningApp>();
        foreach (var open in AppStatus.OpenWindows())
        {
            if (open.Path.Equals(own, StringComparison.OrdinalIgnoreCase)) continue;
            // Cloaked windows are system surfaces (the touch keyboard host) and windows on other virtual desktops.
            _ = Native.DwmGetWindowAttribute(open.Handle, 14, out var cloaked, sizeof(int));
            if (cloaked != 0) continue;
            if (!seen.Add(open.AppId.Length > 0 ? open.AppId : open.Path)) continue;
            text.Clear();
            _ = Native.GetWindowText(open.Handle, text, text.Capacity);
            result.Add(new(open.Handle, open.Path, open.AppId, text.ToString(), Native.IsIconic(open.Handle), known.GetValueOrDefault(open.Handle)));
        }
        return result;
    }

    private void PopulateRunning(List<RunningApp> apps)
    {
        var saved = session.Config.Categories.SelectMany(c => c.Shortcuts).SelectMany(s => s.IsSet ? s.Steps : [s]).ToList();
        var entries = new List<(Shortcut, IntPtr, string)>();
        foreach (var app in apps)
        {
            Shortcut? step = null;
            if (saved.FirstOrDefault(s => session.Status.WindowOf(s) == app.Window) is { } existing)
                step = new Shortcut { Name = existing.Name, Target = existing.Target, Arguments = existing.Arguments, WorkingDirectory = existing.WorkingDirectory, RunAsAdministrator = existing.RunAsAdministrator };
            else if (app.Known is { } entry) step = new Shortcut { Name = entry.Name, Target = entry.Target };
            else if (app.AppId.Contains('!')) step = new Shortcut { Name = app.Title.Length > 0 ? app.Title : app.AppId, Target = Shortcuts.AppsFolder + app.AppId };
            else step = Shortcuts.FromPath(app.Path);
            if (step is null) continue;
            var detail = app.Title.Length > 0 && app.Title != step.Name ? app.Title : L.T("Відкрите вікно");
            entries.Add((step, app.Window, app.Minimized ? L.F("{0} · згорнуте, розташування не запам’ятається", detail) : detail));
        }
        Populate(runningList, running, entries);
        if (running.Count == 0) runningList.Children.Add(Message(L.T("Зараз немає відкритих вікон. Запустіть потрібні застосунки, розставте їхні вікна й відкрийте це вікно знову — або виберіть програми зі списку «Усі програми».")));
    }

    private void Populate(StackPanel list, List<Row> rows, IEnumerable<(Shortcut Step, IntPtr Window, string Detail)> entries)
    {
        list.Children.Clear(); rows.Clear();
        foreach (var (step, window, detail) in entries)
        {
            var check = new CheckBox { IsHitTestVisible = false, Focusable = false, VerticalAlignment = VerticalAlignment.Center };
            var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            texts.Children.Add(Ui.Text(step.Name, 13));
            texts.Children.Add(Ui.Text(detail, 11.5, "Text.Faint"));
            var line = new Grid();
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition());
            var icon = Ui.AppIcon(step.Name, step.Target, 28); icon.Margin = new Thickness(12, 0, 0, 0);
            line.Children.Add(check); line.Children.Add(icon.At(1)); line.Children.Add(texts.At(2));
            var element = new Border { Height = 46, Padding = new Thickness(10, 0, 10, 0), Background = Brushes.Transparent, Child = line }.Res(Border.CornerRadiusProperty, "Radius.Small");
            element.MouseEnter += (_, _) => element.SetResourceReference(Border.BackgroundProperty, "Hover");
            element.MouseLeave += (_, _) => element.Background = Brushes.Transparent;
            element.MouseLeftButtonUp += (_, _) => { check.IsChecked = check.IsChecked != true; UpdateSelection(); };
            rows.Add(new(step, window, element, check));
            list.Children.Add(element);
        }
        Filter();
    }

    private void ShowSource()
    {
        scroller.Content = showInstalled ? installedList : runningList;
        hint.Text = showInstalled
            ? L.T("Застосунки зі списку запустяться у звичному вигляді, без розставляння вікон.")
            : L.T("Набір запам’ятає розмір і розташування вікон такими, якими вони є в момент створення, і відновлюватиме їх під час запуску.");
        search.Text = "";
        Filter();
    }

    private void Filter()
    {
        var query = search.Text.Trim();
        foreach (var row in showInstalled ? installed : running)
            row.Element.Visibility = query.Length == 0 || row.Step.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
        UpdateSelection();
    }

    private void UpdateSelection()
    {
        var windows = running.Count(r => r.Check.IsChecked == true);
        var total = windows + installed.Count(r => r.Check.IsChecked == true);
        create.IsEnabled = total is > 0 and <= MaxSteps && name.Text.Trim().Length > 0;
        status.Text = total == 0 ? L.T("Позначте застосунки для набору.")
            : total > MaxSteps ? L.F("У наборі може бути щонайбільше {0} застосунків.", MaxSteps)
            : L.F("Вибрано: {0}", total) + (windows > 0 ? " · " + L.F("з розташуванням вікон: {0}", windows) : "");
    }

    private void Accept()
    {
        var steps = new List<Shortcut>();
        var known = new HashSet<string>();
        foreach (var row in running.Concat(installed).Where(r => r.Check.IsChecked == true))
        {
            if (!known.Add(Shortcuts.Identity(row.Step))) continue;
            // Read the rectangle now, so the windows can still be arranged while this window is open.
            row.Step.Placement = AppSets.Capture(row.Window);
            steps.Add(row.Step);
        }
        if (steps.Count == 0 || name.Text.Trim().Length == 0) return;
        Close();
        session.AddShortcuts(category, [new Shortcut { Name = name.Text.Trim(), Steps = steps }]);
    }
}
