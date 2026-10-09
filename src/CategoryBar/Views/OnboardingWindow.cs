using System.Text.Json;
using CategoryBar.Services;

namespace CategoryBar.Views;

/// <summary>
/// First-run wizard: choose how to start, review the proposed categories on a board, pick the look.
/// Nothing is written to the configuration until the last step is confirmed.
/// </summary>
internal sealed class OnboardingWindow : ChromeWindow
{
    private static readonly string[] CategoryOrder = [L.T("Робота"), L.T("Розробка"), L.T("Браузери"), L.T("Спілкування"), L.T("Медіа"), L.T("Ігри"), L.T("Системні"), L.T(ApplicationClassifier.Fallback)];
    private static readonly string[] StarterCategories = [L.T("Робота"), L.T("Розробка"), L.T("Медіа"), L.T("Ігри"), L.T("Тулзи")];

    private readonly Session session;
    private readonly bool hasExisting;
    private readonly Grid content = new() { Margin = new Thickness(36, 8, 36, 0) };
    private readonly StackPanel steps = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid footer = new() { Margin = new Thickness(36, 14, 36, 24) };
    private List<Category> draft = [];
    private string mode;
    private string? draftMode;
    private int step;
    private bool autostart = true;
    private BoardView? board;

    internal bool Finished { get; private set; }

    internal OnboardingWindow(Session session) : base(L.T("Панель категорій — початкове налаштування"), 920, 660)
    {
        this.session = session;
        hasExisting = session.Config.Categories.Count > 0;
        mode = hasExisting ? "Keep" : "Import";
        autostart = StartupRegistration.IsEnabled || !session.Settings.OnboardingCompleted;
        var layout = new DockPanel();
        steps.Margin = new Thickness(0, 0, 0, 14);
        DockPanel.SetDock(steps, Dock.Top); DockPanel.SetDock(footer, Dock.Bottom);
        layout.Children.Add(steps); layout.Children.Add(footer); layout.Children.Add(content);
        Body.Children.Add(layout);
        Show(0);
    }

    protected override bool CloseOnEscape => false;

    private void Show(int index)
    {
        step = index;
        steps.Children.Clear();
        var titles = new[] { L.T("Спосіб"), L.T("Категорії"), L.T("Вигляд") };
        for (var i = 0; i < titles.Length; i++)
        {
            var active = i == step; var done = i < step;
            var number = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), BorderThickness = new Thickness(1) }.Res(Border.BorderBrushProperty, active || done ? "Accent" : "Surface.Border");
            if (active || done) number.SetResourceReference(Border.BackgroundProperty, done ? "Accent.Soft" : "Accent");
            var mark = done ? Ui.Icon(Icons.Check, "Accent", 12, 1.8) : Ui.Text((i + 1).ToString(), 11.5, active ? "Accent.Text" : "Text.Faint", bold: true);
            mark.HorizontalAlignment = HorizontalAlignment.Center;
            number.Child = mark;
            var label = Ui.Text(titles[i], 12.5, active ? "Text" : "Text.Faint", bold: active); label.Margin = new Thickness(8, 0, 0, 0);
            steps.Children.Add(number); steps.Children.Add(label);
            if (i < titles.Length - 1) steps.Children.Add(new Border { Width = 44, Height = 1, Margin = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center }.Res(Border.BackgroundProperty, "Surface.Border"));
        }
        content.Children.Clear(); footer.Children.Clear(); board = null;
        switch (step)
        {
            case 0: Welcome(); break;
            case 1: Review(); break;
            default: Look(); break;
        }
        Ui.FadeIn(content, 0, 8, 160);
    }

    private void Buttons(string? back, Action? onBack, string next, Action onNext, string? note = null)
    {
        footer.ColumnDefinitions.Clear();
        footer.ColumnDefinitions.Add(new ColumnDefinition());
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        if (note is not null) footer.Children.Add(Ui.Text(note, 12, "Text.Faint", wrap: true));
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        if (back is not null && onBack is not null)
        {
            var previous = Ui.Button(back, onBack); previous.MinWidth = 100; previous.MinHeight = 34;
            row.Children.Add(previous);
        }
        var forward = Ui.Button(next, onNext, "Button.Primary"); forward.MinWidth = 140; forward.MinHeight = 34; forward.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(forward);
        footer.Children.Add(row.At(1));
    }

    private static TextBlock Headline(string text, string subtitle, StackPanel into)
    {
        var title = Ui.Text(text, 26, "Text", bold: true);
        var sub = Ui.Text(subtitle, 13.5, "Text.Muted", wrap: true); sub.Margin = new Thickness(0, 8, 0, 20); sub.LineHeight = 21; sub.MaxWidth = 720; sub.HorizontalAlignment = HorizontalAlignment.Left;
        into.Children.Add(title); into.Children.Add(sub);
        return sub;
    }

    // ---- Step 1: how to start --------------------------------------------------------------------

    private void Welcome()
    {
        var stack = new StackPanel();
        Headline(L.T("Вітаємо в Панелі категорій"), L.T("Замість довгого ряду закріплених значків — кілька вкладок над панеллю завдань. Наведіть на вкладку, і її застосунки відкриються колонкою. Оберіть, з чого почати."), stack);
        var options = new StackPanel();
        void Render()
        {
            options.Children.Clear();
            if (hasExisting) options.Children.Add(Option("Keep", Icons.Check, L.T("Залишити мої категорії"), L.N(session.Config.Categories.Count, "У вас уже є {0} категорія. Перегляньте її і за потреби перерозподіліть застосунки.|У вас уже є {0} категорії. Перегляньте їх і за потреби перерозподіліть застосунки.|У вас уже є {0} категорій. Перегляньте їх і за потреби перерозподіліть застосунки."), null));
            options.Children.Add(Option("Import", Icons.Import, L.T("Перенести з панелі завдань Windows"), L.T("Знайдемо закріплені застосунки й розкладемо їх по категоріях. Перед збереженням усе можна поправити.") + (hasExisting ? " " + L.T("Наявні категорії залишаться.") : ""), hasExisting ? null : L.T("Рекомендовано")));
            options.Children.Add(Option("StarterCategories", Icons.Sparkle, L.T("Почати з готових категорій"), L.T("«Робота», «Розробка», «Медіа», «Ігри», «Тулзи» — порожні, наповните їх самі."), null));
            if (!hasExisting) options.Children.Add(Option("Empty", Icons.Square, L.T("Порожня панель"), L.T("Створите категорії та додасте застосунки вручну."), null));
        }
        Border Option(string key, string icon, string title, string description, string? badge)
        {
            var selected = mode == key;
            var tile = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Child = Ui.Icon(icon, selected ? "Accent.Text" : "Text.Muted", 18) }.Res(Border.BackgroundProperty, selected ? "Accent" : "Control.Bg");
            var heading = new StackPanel { Orientation = Orientation.Horizontal };
            heading.Children.Add(Ui.Text(title, 14.5, "Text", bold: true));
            if (badge is not null)
            {
                var pill = new Border { CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(10, 0, 0, 0), Child = Ui.Text(badge, 11, "Accent", bold: true) }.Res(Border.BackgroundProperty, "Accent.Soft");
                heading.Children.Add(pill);
            }
            var note = Ui.Text(description, 12.5, "Text.Muted", wrap: true); note.Margin = new Thickness(0, 4, 0, 0); note.LineHeight = 18;
            var texts = new StackPanel { Margin = new Thickness(16, 0, 16, 0), VerticalAlignment = VerticalAlignment.Center, Children = { heading, note } };
            var radio = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(selected ? 5 : 1.5), VerticalAlignment = VerticalAlignment.Center }.Res(Border.BorderBrushProperty, selected ? "Accent" : "Text.Faint");
            var line = new DockPanel();
            DockPanel.SetDock(tile, Dock.Left); DockPanel.SetDock(radio, Dock.Right);
            line.Children.Add(tile); line.Children.Add(radio); line.Children.Add(texts);
            var card = new Border { Padding = new Thickness(16, 14, 18, 14), Margin = new Thickness(0, 0, 0, 8), BorderThickness = new Thickness(1.5), Child = line, Cursor = Cursors.Hand }
                .Res(Border.BackgroundProperty, selected ? "Accent.Soft" : "Card.Bg").Res(Border.BorderBrushProperty, selected ? "Accent" : "Surface.Border").Res(Border.CornerRadiusProperty, "Radius");
            card.MouseLeftButtonUp += (_, _) => { mode = key; Render(); };
            card.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) { mode = key; Next(); } };
            return card;
        }
        Render();
        stack.Children.Add(options);
        content.Children.Add(stack);
        Buttons(null, null, L.T("Далі"), Next, L.T("Майстер можна відкрити знову: Налаштування → Загальне."));
        void Next() => Show(mode == "Empty" ? 2 : 1);
    }

    // ---- Step 2: review on the board -------------------------------------------------------------

    private List<Category> CloneExisting() =>
        JsonSerializer.Deserialize<List<Category>>(JsonSerializer.Serialize(session.Config.Categories)) ?? [];

    private async void Review()
    {
        var stack = new DockPanel();
        var header = new StackPanel();
        var subtitle = Headline(L.T("Перевірте категорії"), L.T("Шукаємо закріплені застосунки…"), header);
        subtitle.Margin = new Thickness(0, 8, 0, 14);
        DockPanel.SetDock(header, Dock.Top);
        stack.Children.Add(header);
        var holder = new Grid();
        stack.Children.Add(holder);
        content.Children.Add(stack);
        Buttons(L.T("Назад"), () => Show(0), L.T("Далі"), () => Show(2), L.T("Перетягуйте застосунки між колонками. Подвійний клік по назві — перейменувати."));

        if (draftMode != mode)
        {
            draft = mode == "Empty" ? [] : CloneExisting();
            if (mode == "StarterCategories")
                foreach (var name in StarterCategories.Where(n => draft.All(c => !c.Name.Equals(n, StringComparison.CurrentCultureIgnoreCase)))) draft.Add(new Category { Name = name });
            if (mode == "Import")
            {
                var wait = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                wait.Children.Add(Ui.Spinner(18));
                var text = Ui.Text(L.T("Читаємо панель завдань Windows…"), 13.5, "Text.Muted"); text.Margin = new Thickness(12, 0, 0, 0);
                wait.Children.Add(text);
                holder.Children.Add(wait);
                var imported = 0;
                try
                {
                    var requestedMode = mode;
                    var pinned = await AppCatalog.Pinned();
                    if (step != 1 || mode != requestedMode) return; // The user went back while the scan was running.
                    imported = Merge(pinned);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Runtime.InteropServices.COMException)
                {
                    session.Toast(L.T("Не вдалося прочитати панель завдань"), ex.Message, ToastKind.Warning);
                }
                holder.Children.Clear();
                subtitle.Text = imported > 0
                    ? L.N(imported, "Знайдено {0} закріплений застосунок. Ми розклали їх за типом — перетягніть, якщо щось не на своєму місці.|Знайдено {0} закріплені застосунки. Ми розклали їх за типом — перетягніть, якщо щось не на своєму місці.|Знайдено {0} закріплених застосунків. Ми розклали їх за типом — перетягніть, якщо щось не на своєму місці.")
                    : L.T("Нових закріплених застосунків не знайдено. Додайте застосунки кнопкою «Додати» в потрібній категорії.");
            }
            draftMode = mode;
        }
        if (mode != "Import" || subtitle.Text.StartsWith(L.T("Шукаємо"), StringComparison.Ordinal))
            subtitle.Text = mode switch
            {
                "StarterCategories" => L.T("Готові категорії створено. Додайте в них застосунки зараз або пізніше — прямо з панелі."),
                "Keep" => L.T("Це ваші поточні категорії. Перерозподіліть застосунки, якщо потрібно."),
                _ => L.T("Перетягніть застосунки, якщо щось не на своєму місці.")
            };

        board = new BoardView(() => draft, (change, _) => { change(); board?.Refresh(); return true; }, category =>
        {
            var picker = new AppPickerWindow(category, shortcuts =>
            {
                var known = category.Shortcuts.Select(Shortcuts.Identity).ToHashSet();
                category.Shortcuts.AddRange(shortcuts.Where(s => known.Add(Shortcuts.Identity(s))));
                board?.Refresh();
            }) { Owner = this };
            picker.ShowDialog();
        })
        { DeleteRequested = category => { draft.Remove(category); board?.Refresh(); } };
        holder.Children.Add(board);
    }

    /// <summary>Adds pinned applications that are not on the bar yet, grouped by suggested category.</summary>
    private int Merge(List<CatalogEntry> pinned)
    {
        var known = draft.SelectMany(c => c.Shortcuts).Select(Shortcuts.Identity).ToHashSet();
        var added = 0;
        foreach (var entry in pinned)
        {
            if (!known.Add(Shortcuts.Identity(new Shortcut { Name = entry.Name, Target = entry.Target }))) continue;
            var category = draft.FirstOrDefault(c => c.Name.Equals(entry.SuggestedCategory, StringComparison.CurrentCultureIgnoreCase));
            if (category is null) { category = new Category { Name = entry.SuggestedCategory }; draft.Add(category); }
            category.Shortcuts.Add(AppCatalog.ToShortcut(entry, session.Store.Path));
            added++;
        }
        if (!hasExisting) draft = draft.OrderBy(c => Array.IndexOf(CategoryOrder, c.Name) is var i && i < 0 ? 99 : i).ToList();
        return added;
    }

    // ---- Step 3: appearance ----------------------------------------------------------------------

    private void Look()
    {
        var left = new StackPanel { Width = 400 };
        var title = Headline(L.T("Вигляд"), L.T("Усе це можна змінити пізніше в налаштуваннях: шрифт, масштаб, прозорість, заокруглення."), left);
        title.MaxWidth = 380;

        void Caption(string text) { var caption = Ui.Text(text.ToUpper(), 11, "Text.Faint", bold: true); caption.Margin = new Thickness(0, 6, 0, 8); left.Children.Add(caption); }
        Caption(L.T("Тема"));
        var themes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14) };
        var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18) };
        void Render()
        {
            themes.Children.Clear();
            foreach (var (key, label, icon) in new[] { ("System", L.T("Як у Windows"), Icons.Windows), ("Dark", L.T("Темна"), Icons.Moon), ("Light", L.T("Світла"), Icons.Sun) })
            {
                var selected = session.Settings.ThemeMode == key;
                var inner = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                var glyph = Ui.Icon(icon, selected ? "Accent" : "Text.Muted", 14); glyph.Margin = new Thickness(0, 0, 8, 0);
                inner.Children.Add(glyph); inner.Children.Add(Ui.Text(label, 13, selected ? "Text" : "Text.Muted", bold: selected));
                var choice = new Border { Height = 40, Width = 126, Margin = new Thickness(0, 0, 8, 0), BorderThickness = new Thickness(1.5), Child = inner, Cursor = Cursors.Hand }
                    .Res(Border.BackgroundProperty, selected ? "Accent.Soft" : "Card.Bg").Res(Border.BorderBrushProperty, selected ? "Accent" : "Surface.Border").Res(Border.CornerRadiusProperty, "Radius.Small");
                choice.MouseLeftButtonUp += (_, _) => { session.ChangeSettings(s => s.ThemeMode = key); Render(); };
                themes.Children.Add(choice);
            }
            swatches.Children.Clear();
            var system = session.Settings.AccentMode == "System";
            swatches.Children.Add(Swatch(Theme.WindowsAccent(), system, L.T("Колір Windows"), s => s.AccentMode = "System", Icons.Windows));
            foreach (var preset in Theme.AccentPresets.Take(9))
                swatches.Children.Add(Swatch(Theme.Parse(preset), !system && session.Settings.AccentColor == preset, preset, s => { s.AccentMode = "Custom"; s.AccentColor = preset; }));
        }
        Border Swatch(Color color, bool selected, string tip, Action<Settings> apply, string? icon = null)
        {
            var dot = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(color) };
            if (icon is not null) dot.Child = Ui.Icon(icon, "Accent.Text", 10);
            var ring = new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(2), Child = dot, Margin = new Thickness(0, 0, 4, 0), Background = Brushes.Transparent, ToolTip = tip, Cursor = Cursors.Hand };
            if (selected) ring.SetResourceReference(Border.BorderBrushProperty, "Text"); else ring.BorderBrush = Brushes.Transparent;
            ring.MouseLeftButtonUp += (_, _) => { session.ChangeSettings(apply); Render(); };
            return ring;
        }
        left.Children.Add(themes);
        Caption(L.T("Колір акценту"));
        left.Children.Add(swatches);
        Render();

        var toggle = new CheckBox { IsChecked = autostart, VerticalAlignment = VerticalAlignment.Center }.Res(StyleProperty, "Toggle");
        toggle.Click += (_, _) => autostart = toggle.IsChecked == true;
        var startText = new StackPanel { Margin = new Thickness(14, 0, 0, 0), Children = { Ui.Text(L.T("Запускати разом із Windows"), 13.5), Ui.Text(L.T("Панель буде на місці після кожного входу."), 12, "Text.Muted") } };
        left.Children.Add(new Border { Padding = new Thickness(14, 12, 14, 12), BorderThickness = new Thickness(1), Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { toggle, startText } } }
            .Res(Border.BackgroundProperty, "Card.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius"));

        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.Children.Add(left); layout.Children.Add(Preview().At(1));
        content.Children.Add(layout);
        Buttons(L.T("Назад"), () => Show(mode == "Empty" ? 0 : 1), L.T("Готово"), Finish);
    }

    /// <summary>A miniature desktop drawn with the live theme resources.</summary>
    private FrameworkElement Preview()
    {
        var names = (draft.Count > 0 ? draft.Select(c => c.Name) : StarterCategories).Take(4).ToList();
        var apps = draft.FirstOrDefault(c => c.Shortcuts.Count > 0)?.Shortcuts.Take(4).Select(s => (s.Name, s.Target)).ToList()
            ?? [(L.T("Браузер"), ""), (L.T("Редактор коду"), ""), (L.T("Термінал"), ""), (L.T("Нотатки"), "")];
        var open = draft.FindIndex(c => c.Shortcuts.Count > 0) is var found && found >= 0 && found < 4 ? found : 0;

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < names.Count; i++)
        {
            var label = Ui.Text(names[i], 11.5); label.MaxWidth = 76;
            var line = new Border { Height = 2, CornerRadius = new CornerRadius(1), VerticalAlignment = VerticalAlignment.Bottom, Visibility = i == open ? Visibility.Visible : Visibility.Hidden }.Res(Border.BackgroundProperty, "Accent");
            var tab = new Border { Height = 24, Padding = new Thickness(9, 0, 9, 0), Margin = new Thickness(1, 0, 1, 0), Child = new Grid { Children = { label, line } } }.Res(Border.CornerRadiusProperty, "Radius.Small");
            if (i == open) tab.SetResourceReference(Border.BackgroundProperty, "Selected");
            tabs.Children.Add(tab);
        }
        var bar = new Border { Padding = new Thickness(3, 3, 3, 2), BorderThickness = new Thickness(1, 1, 1, 0), Child = tabs, HorizontalAlignment = HorizontalAlignment.Center }
            .Res(Border.BackgroundProperty, "Bar.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius.Top");

        var rows = new StackPanel();
        var header = Ui.Text(names.Count > open ? names[open].ToUpper() : "", 9.5, "Text.Faint", bold: true); header.Margin = new Thickness(8, 4, 8, 6);
        rows.Children.Add(header);
        foreach (var (name, target) in apps)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Height = 30, Margin = new Thickness(8, 0, 8, 0) };
            row.Children.Add(Ui.AppIcon(name, target, 18));
            var text = Ui.Text(name, 11.5); text.Margin = new Thickness(8, 0, 0, 0); text.MaxWidth = 130;
            row.Children.Add(text);
            rows.Children.Add(row);
        }
        var menu = new Border { Width = 190, Padding = new Thickness(5), BorderThickness = new Thickness(1), Child = rows, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 7) }
            .Res(Border.BackgroundProperty, "Menu.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius");

        var taskbar = new Border { Height = 34, CornerRadius = new CornerRadius(0, 0, 11, 11), Background = new SolidColorBrush(Color.FromArgb(235, 28, 28, 32)) };
        var dots = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        for (var i = 0; i < 6; i++) dots.Children.Add(new Border { Width = 16, Height = 16, CornerRadius = new CornerRadius(4), Margin = new Thickness(5, 0, 5, 0), Background = new SolidColorBrush(Color.FromArgb(i == 0 ? (byte)255 : (byte)40, i == 0 ? (byte)142 : (byte)255, i == 0 ? (byte)168 : (byte)255, 255)) });
        taskbar.Child = dots;

        var scene = new DockPanel { LastChildFill = false };
        DockPanel.SetDock(taskbar, Dock.Bottom); DockPanel.SetDock(bar, Dock.Bottom); DockPanel.SetDock(menu, Dock.Bottom);
        scene.Children.Add(taskbar); scene.Children.Add(bar); scene.Children.Add(menu);
        var wallpaper = new LinearGradientBrush(Theme.Parse("#41536A"), Theme.Parse("#171B22"), 65);
        return new Border { Margin = new Thickness(32, 4, 0, 4), CornerRadius = new CornerRadius(12), Background = wallpaper, ClipToBounds = true, Child = scene, BorderThickness = new Thickness(1) }.Res(Border.BorderBrushProperty, "Surface.Border");
    }

    private void Finish()
    {
        var categories = mode == "Empty" ? [] : draftMode == mode ? draft : CloneExisting();
        var saved = session.Change(() =>
        {
            session.Config.Categories.Clear();
            session.Config.Categories.AddRange(categories.Where(c => c.Name.Trim().Length > 0));
            session.Settings.OnboardingCompleted = true;
        });
        if (!saved) return;
        try { StartupRegistration.SetEnabled(autostart, App.Instance.CustomConfigPath); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        Finished = true;
        Close();
    }
}
