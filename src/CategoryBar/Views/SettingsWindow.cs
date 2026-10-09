using System.Diagnostics;
using System.Reflection;

namespace CategoryBar.Views;

/// <summary>Settings with a sidebar. Every control applies and saves immediately.</summary>
internal sealed class SettingsWindow : ChromeWindow
{
    private static readonly (string Key, string Title, string Icon)[] Pages =
    [
        ("General", L.T("Загальне"), Icons.Home), ("Appearance", L.T("Вигляд"), Icons.Palette), ("Panel", L.T("Панель і меню"), Icons.Layout),
        ("Categories", L.T("Категорії"), Icons.Apps), ("About", L.T("Про програму"), Icons.Info)
    ];

    private readonly Session session;
    private readonly StackPanel navigation = new() { Margin = new Thickness(10, 6, 10, 10) };
    private readonly Grid page = new();
    private string current = "General";
    private BoardView? board;
    private Settings Settings => session.Settings;

    internal SettingsWindow(Session session) : base(L.T("Панель категорій — Налаштування"), 900, 640, canMinimize: true)
    {
        this.session = session;
        var layout = new Grid();
        layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(208) });
        layout.ColumnDefinitions.Add(new ColumnDefinition());
        layout.Children.Add(new Border { Child = navigation, Margin = new Thickness(0, 0, 0, 0) }.Res(Border.BackgroundProperty, "Sidebar.Bg"));
        layout.Children.Add(page.At(1));
        Body.Children.Add(layout);
        TitleBar.SetResourceReference(Panel.BackgroundProperty, "Sidebar.Bg");
        session.Changed += OnSessionChanged;
        Closed += (_, _) => session.Changed -= OnSessionChanged;
        Navigate("General");
    }

    private void OnSessionChanged() { if (current == "Categories") board?.Refresh(); }

    internal void Navigate(string key)
    {
        current = Pages.Any(p => p.Key == key) ? key : "General";
        navigation.Children.Clear();
        foreach (var (pageKey, title, icon) in Pages)
        {
            var active = pageKey == current;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            var glyph = Ui.Icon(icon, active ? "Accent" : "Text.Muted"); glyph.Margin = new Thickness(0, 0, 11, 0);
            content.Children.Add(glyph);
            content.Children.Add(Ui.Text(title, 13, active ? "Text" : "Text.Muted", bold: active));
            var item = new Border { Height = 36, Padding = new Thickness(12, 0, 12, 0), Margin = new Thickness(0, 0, 0, 2), Background = Brushes.Transparent, Child = content, Cursor = Cursors.Hand }.Res(Border.CornerRadiusProperty, "Radius.Small");
            if (active) item.SetResourceReference(Border.BackgroundProperty, "Selected");
            else
            {
                item.MouseEnter += (_, _) => item.SetResourceReference(Border.BackgroundProperty, "Hover");
                item.MouseLeave += (_, _) => item.Background = Brushes.Transparent;
                item.MouseLeftButtonUp += (_, _) => Navigate(pageKey);
            }
            navigation.Children.Add(item);
        }
        board = null;
        page.Children.Clear();
        if (current == "Categories") { page.Children.Add(CategoriesPage()); return; }
        var stack = new StackPanel { Margin = new Thickness(28, 4, 28, 28) };
        var heading = Ui.Text(Pages.First(p => p.Key == current).Title, 22, "Text", bold: true); heading.Margin = new Thickness(0, 0, 0, 16);
        stack.Children.Add(heading);
        switch (current)
        {
            case "General": General(stack); break;
            case "Appearance": Appearance(stack); break;
            case "Panel": PanelPage(stack); break;
            default: About(stack); break;
        }
        page.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = stack });
    }

    // ---- Building blocks -------------------------------------------------------------------------

    private static void Group(StackPanel stack, string title)
    {
        var caption = Ui.Text(title.ToUpper(), 11, "Text.Faint", bold: true);
        caption.Margin = new Thickness(2, stack.Children.Count > 1 ? 18 : 0, 0, 8);
        stack.Children.Add(caption);
    }

    private static Border Card(string label, string? hint, FrameworkElement control, bool stacked = false)
    {
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Ui.Text(label, 13.5));
        if (hint is not null)
        {
            var note = Ui.Text(hint, 12, "Text.Muted", wrap: true); note.Margin = new Thickness(0, 3, 0, 0); note.LineHeight = 17;
            texts.Children.Add(note);
        }
        FrameworkElement content;
        if (stacked)
        {
            control.Margin = new Thickness(0, 12, 0, 0); control.HorizontalAlignment = HorizontalAlignment.Left;
            content = new StackPanel { Children = { texts, control } };
        }
        else
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            control.VerticalAlignment = VerticalAlignment.Center; control.Margin = new Thickness(20, 0, 0, 0);
            grid.Children.Add(texts); grid.Children.Add(control.At(1));
            content = grid;
        }
        return new Border { Padding = new Thickness(16, 13, 16, 13), Margin = new Thickness(0, 0, 0, 6), BorderThickness = new Thickness(1), Child = content, MinHeight = 56 }
            .Res(Border.BackgroundProperty, "Card.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius");
    }

    private CheckBox Toggle(bool value, Action<Settings, bool> apply)
    {
        var toggle = new CheckBox { IsChecked = value }.Res(StyleProperty, "Toggle");
        toggle.Click += (_, _) => session.ChangeSettings(s => apply(s, toggle.IsChecked == true));
        return toggle;
    }

    private Border Segments(string value, Action<Settings, string> apply, params (string Value, string Label)[] options)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var group = Guid.NewGuid().ToString();
        foreach (var (option, label) in options)
        {
            var button = new RadioButton { Content = label, GroupName = group, IsChecked = option == value, Margin = new Thickness(1, 0, 1, 0) }.Res(StyleProperty, "Segment");
            button.Checked += (_, _) => session.ChangeSettings(s => apply(s, option));
            panel.Children.Add(button);
        }
        return new Border { Padding = new Thickness(2), BorderThickness = new Thickness(1), Child = panel, HorizontalAlignment = HorizontalAlignment.Right }
            .Res(Border.BackgroundProperty, "Control.Bg").Res(Border.BorderBrushProperty, "Surface.Border").Res(Border.CornerRadiusProperty, "Radius.Small");
    }

    private StackPanel Range(double value, double minimum, double maximum, double step, Func<double, string> format, Action<Settings, double> apply)
    {
        var label = Ui.Text(format(value), 12.5, "Text.Muted"); label.Width = 62; label.TextAlignment = TextAlignment.Right;
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, Width = 190, TickFrequency = step, IsSnapToTickEnabled = true, SmallChange = step, LargeChange = step };
        slider.ValueChanged += (_, _) => label.Text = format(slider.Value);
        // Apply when the thumb is released: re-theming every open window on each pixel would make dragging stutter.
        var applied = value;
        void Commit() { if (Math.Abs(applied - slider.Value) < 1e-9) return; applied = slider.Value; session.ChangeSettings(s => apply(s, slider.Value)); }
        slider.PreviewMouseLeftButtonUp += (_, _) => Dispatcher.BeginInvoke(Commit);
        slider.LostMouseCapture += (_, _) => Commit();
        slider.KeyUp += (_, _) => Commit();
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { slider, label } };
    }

    // ---- Pages -----------------------------------------------------------------------------------

    private void General(StackPanel stack)
    {
        Group(stack, L.T("Запуск"));
        var startup = new CheckBox { IsChecked = StartupRegistration.IsEnabled }.Res(StyleProperty, "Toggle");
        startup.Click += (_, _) =>
        {
            try { StartupRegistration.SetEnabled(startup.IsChecked == true, App.Instance.CustomConfigPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                startup.IsChecked = StartupRegistration.IsEnabled;
                session.Toast(L.T("Не вдалося змінити автозапуск"), ex.Message, ToastKind.Error);
            }
        };
        stack.Children.Add(Card(L.T("Запускати разом із Windows"), L.T("Панель з’явиться одразу після входу в систему."), startup));

        Group(stack, L.T("Мова"));
        var languages = new ComboBox { Width = 230 };
        languages.Items.Add(new ComboBoxItem { Content = L.T("Як у Windows"), Tag = "System" });
        foreach (var (code, name) in L.Languages) languages.Items.Add(new ComboBoxItem { Content = name, Tag = code });
        languages.SelectedIndex = Math.Max(0, languages.Items.Cast<ComboBoxItem>().ToList().FindIndex(i => (string)i.Tag == Settings.Language));
        languages.SelectionChanged += (_, _) =>
        {
            if (languages.SelectedItem is not ComboBoxItem { Tag: string code } || code == Settings.Language) return;
            var before = L.Current;
            session.ChangeSettings(s => s.Language = code);
            if (L.Resolve(code) != before) App.Instance.Restart();
        };
        stack.Children.Add(Card(L.T("Мова інтерфейсу"), L.T("Після зміни мови програма перезапуститься."), languages));

        Group(stack, L.T("Застосунки"));
        stack.Children.Add(Card(L.T("Індикатори запущених застосунків"), L.T("Позначка під іконкою в меню та під назвою категорії."), Toggle(Settings.ShowRunningIndicators, (s, v) => s.ShowRunningIndicators = v)));
        stack.Children.Add(Card(L.T("Клік по запущеному застосунку"), L.T("Shift + клік завжди відкриває нове вікно."),
            Segments(Settings.ActivateRunning ? "Activate" : "Launch", (s, v) => s.ActivateRunning = v == "Activate", ("Activate", L.T("Перейти до вікна")), ("Launch", L.T("Запустити ще раз")))));

        Group(stack, L.T("Початкове налаштування"));
        stack.Children.Add(Card(L.T("Перенести з панелі завдань Windows"), L.T("Знайти закріплені застосунки та розкласти їх по категоріях. Наявні категорії можна залишити."),
            Ui.Button(L.T("Відкрити майстер"), () => App.Instance.ShowOnboarding(), icon: Icons.Import)));

        Group(stack, L.T("Дані"));
        stack.Children.Add(Card(L.T("Файл налаштувань"), session.Store.Path, Ui.Button(L.T("Показати в папці"), () =>
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + session.Store.Path + "\""))?.Dispose(), icon: Icons.Folder)));
        stack.Children.Add(Card(L.T("Скасувати останню зміну"), L.T("Повертає останнє додавання, переміщення або видалення."),
            Ui.Button(L.T("Скасувати##undo"), () => { session.Undo(); Navigate(current); }, icon: Icons.Undo)));
    }

    private void Appearance(StackPanel stack)
    {
        Group(stack, L.T("Кольори"));
        stack.Children.Add(Card(L.T("Тема"), L.T("«Як у Windows» перемикається разом із системною темою."),
            Segments(Settings.ThemeMode, (s, v) => s.ThemeMode = v, ("System", L.T("Як у Windows")), ("Dark", L.T("Темна")), ("Light", L.T("Світла")))));

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        void Rebuild()
        {
            swatches.Children.Clear();
            var system = Settings.AccentMode == "System";
            swatches.Children.Add(Swatch(Theme.WindowsAccent(), system, L.T("Колір Windows"), s => s.AccentMode = "System", Icons.Windows));
            foreach (var preset in Theme.AccentPresets)
                swatches.Children.Add(Swatch(Theme.Parse(preset), !system && Settings.AccentColor.Equals(preset, StringComparison.OrdinalIgnoreCase), preset, s => { s.AccentMode = "Custom"; s.AccentColor = preset; }));
            var custom = new TextBox { Text = Settings.AccentColor, Width = 84, MaxLength = 7, Margin = new Thickness(8, 0, 0, 0), ToolTip = L.T("Власний колір у форматі #RRGGBB"), FontFamily = new FontFamily("Consolas") };
            void Apply()
            {
                var text = custom.Text.Trim();
                if (!text.StartsWith('#')) text = "#" + text;
                if (!Settings.IsHexColor(text) || !system && text.Equals(Settings.AccentColor, StringComparison.OrdinalIgnoreCase)) return;
                session.ChangeSettings(s => { s.AccentMode = "Custom"; s.AccentColor = text.ToUpperInvariant(); });
                Rebuild();
            }
            custom.KeyDown += (_, e) => { if (e.Key == Key.Enter) Apply(); };
            custom.LostKeyboardFocus += (_, _) => Apply();
            swatches.Children.Add(custom);
        }
        Border Swatch(Color color, bool selected, string tip, Action<Settings> apply, string? icon = null)
        {
            var dot = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(11), Background = new SolidColorBrush(color) };
            if (icon is not null) { var glyph = Ui.Icon(icon, "Accent.Text", 10); glyph.Opacity = .9; dot.Child = glyph; }
            var ring = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(15), BorderThickness = new Thickness(2), Child = dot, Margin = new Thickness(0, 0, 3, 0), Background = Brushes.Transparent, ToolTip = tip, Cursor = Cursors.Hand };
            if (selected) ring.SetResourceReference(Border.BorderBrushProperty, "Text"); else ring.BorderBrush = Brushes.Transparent;
            ring.MouseLeftButtonUp += (_, _) => { session.ChangeSettings(apply); Rebuild(); };
            return ring;
        }
        Rebuild();
        stack.Children.Add(Card(L.T("Колір акценту"), L.T("Колір Windows оновлюється автоматично, коли ви змінюєте його в системі."), swatches, stacked: true));
        stack.Children.Add(Card(L.T("Тонувати поверхні акцентом"), L.T("Додає панелі й меню легкий відтінок вибраного кольору."), Toggle(Settings.TintSurfaces, (s, v) => s.TintSurfaces = v)));

        Group(stack, L.T("Текст і форма"));
        var fonts = new ComboBox { Width = 230 };
        var preferred = new[] { Settings.DefaultFont, "Segoe UI", "Segoe UI Variable Display", "Inter", "Onest", "Bahnschrift", "Calibri", "Verdana", "Tahoma", "Consolas", "Cascadia Code", "JetBrains Mono" };
        var installed = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = preferred.Where(installed.Contains).Concat(installed.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!names.Contains(Settings.FontName, StringComparer.OrdinalIgnoreCase)) names.Insert(0, Settings.FontName);
        foreach (var name in names) fonts.Items.Add(new ComboBoxItem { Content = name, FontFamily = new FontFamily(name), Tag = name });
        fonts.SelectedIndex = names.FindIndex(n => n.Equals(Settings.FontName, StringComparison.OrdinalIgnoreCase));
        fonts.SelectionChanged += (_, _) => { if (fonts.SelectedItem is ComboBoxItem { Tag: string name }) session.ChangeSettings(s => s.FontName = name); };
        stack.Children.Add(Card(L.T("Шрифт"), L.T("Використовується на панелі, в меню та у вікнах програми."), fonts));
        stack.Children.Add(Card(L.T("Масштаб інтерфейсу"), null, Range(Settings.InterfaceScale * 100, 80, 150, 5, v => v.ToString("0") + "%", (s, v) => s.InterfaceScale = v / 100)));
        stack.Children.Add(Card(L.T("Непрозорість панелі та меню"), null, Range(Settings.SurfaceOpacity * 100, 50, 100, 2, v => v.ToString("0") + "%", (s, v) => s.SurfaceOpacity = v / 100)));
        stack.Children.Add(Card(L.T("Заокруглення кутів"), null, Range(Settings.CornerRadius, 0, 16, 1, v => v.ToString("0") + " px", (s, v) => s.CornerRadius = (int)v)));
        stack.Children.Add(Card(L.T("Стандартний вигляд"), L.T("Повертає тему, колір, шрифт, масштаб і форму до початкових значень."), Ui.Button(L.T("Скинути"), () =>
        {
            var defaults = new Settings();
            session.ChangeSettings(s =>
            {
                s.ThemeMode = defaults.ThemeMode; s.AccentMode = defaults.AccentMode; s.AccentColor = defaults.AccentColor; s.TintSurfaces = defaults.TintSurfaces;
                s.FontName = defaults.FontName; s.InterfaceScale = defaults.InterfaceScale; s.SurfaceOpacity = defaults.SurfaceOpacity; s.CornerRadius = defaults.CornerRadius;
            });
            Navigate(current);
        })));
    }

    private void PanelPage(StackPanel stack)
    {
        Group(stack, L.T("Розташування"));
        stack.Children.Add(Card(L.T("Вирівнювання панелі"), L.T("«Як у Windows» повторює розташування кнопок панелі завдань."),
            Segments(Settings.Alignment, (s, v) => s.Alignment = v, ("Windows", L.T("Як у Windows")), ("Left", L.T("Ліворуч")), ("Center", L.T("По центру")), ("Right", L.T("Праворуч")))));
        stack.Children.Add(Card(L.T("Монітори"), L.T("На всіх моніторах, де є панель завдань Windows, або лише на основному."),
            Segments(Settings.AllMonitors ? "All" : "Primary", (s, v) => s.AllMonitors = v == "All", ("Primary", L.T("Основний")), ("All", L.T("Усі")))));
        stack.Children.Add(Card(L.T("Місце на екрані"), L.T("Резервування зменшує максимізовані вікна, щоб панель їх не перекривала."),
            Segments(Settings.ReserveSpace ? "Reserve" : "Overlay", (s, v) => s.ReserveSpace = v == "Reserve", ("Overlay", L.T("Поверх вікон")), ("Reserve", L.T("Резервувати")))));
        stack.Children.Add(Card(L.T("Мініатюрний режим"), L.T("Панель ховається за панель завдань Windows, видно лише стрілку. Наведіть на неї — панель виїде."), Toggle(Settings.MiniatureMode, (s, v) => s.MiniatureMode = v)));

        Group(stack, L.T("Меню"));
        stack.Children.Add(Card(L.T("Затримка відкриття"), L.T("Випадковий проліт курсора не відкриває меню."), Range(Settings.OpenDelayMs, 0, 600, 25, v => L.F("{0} мс", v.ToString("0")), (s, v) => s.OpenDelayMs = (int)v)));
        stack.Children.Add(Card(L.T("Затримка закриття"), L.T("Скільки меню чекає після виходу курсора."), Range(Settings.CloseDelayMs, 0, 1000, 50, v => L.F("{0} мс", v.ToString("0")), (s, v) => s.CloseDelayMs = (int)v)));
        stack.Children.Add(Card(L.T("Лічильники на вкладках"), L.T("Кількість ярликів поруч із назвою категорії."), Toggle(Settings.ShowCounts, (s, v) => s.ShowCounts = v)));
        stack.Children.Add(Card(L.T("Пошук у меню"), L.T("Поле пошуку з’являється, коли в категорії стільки ярликів або більше."),
            Range(Settings.SearchThreshold, 0, 40, 1, v => v == 0 ? L.T("вимкнено") : L.F("від {0}", v.ToString("0")), (s, v) => s.SearchThreshold = (int)v)));
        stack.Children.Add(Card(L.T("Сітка для великих категорій"), L.T("Від 20 ярликів меню показує іконки сіткою."), Toggle(Settings.GridForLargeCategories, (s, v) => s.GridForLargeCategories = v)));
    }

    private FrameworkElement CategoriesPage()
    {
        var heading = Ui.Text(L.T("Категорії"), 22, "Text", bold: true);
        var hint = Ui.Text(L.T("Перетягуйте застосунки між категоріями, а категорії — за заголовок. Подвійний клік по назві перейменовує."), 12.5, "Text.Muted", wrap: true);
        hint.Margin = new Thickness(0, 6, 0, 0);
        var titles = new StackPanel { Children = { heading, hint } };
        var undo = Ui.Button(L.T("Скасувати зміну"), session.Undo, icon: Icons.Undo); undo.VerticalAlignment = VerticalAlignment.Top;
        var top = new Grid { Margin = new Thickness(28, 4, 28, 16) };
        top.ColumnDefinitions.Add(new ColumnDefinition());
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(titles); top.Children.Add(undo.At(1));

        board = new BoardView(() => session.Config.Categories, (change, title) => session.Change(change, title), category => AppPickerWindow.Open(session, category))
        { Margin = new Thickness(28, 0, 0, 18), DeleteRequested = category => Dialogs.DeleteCategory(session, category) };
        var layout = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top); layout.Children.Add(board);
        return layout;
    }

    private void About(StackPanel stack)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var logo = new Border { Width = 52, Height = 52, CornerRadius = new CornerRadius(13), Child = Ui.Icon(Icons.Layout, "Accent.Text", 26) }.Res(Border.BackgroundProperty, "Accent");
        var name = Ui.Text(L.T("Панель категорій"), 18, "Text", bold: true);
        var build = Ui.Text(L.F("Версія {0}", version is null ? "—" : version.ToString(3)) + " · Windows 11", 12.5, "Text.Muted"); build.Margin = new Thickness(0, 4, 0, 0);
        var texts = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { name, build } };
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18), Children = { logo, texts } });
        var about = Ui.Text(L.T("Категорії застосунків над панеллю завдань Windows. Наведіть курсор на категорію, щоб побачити її застосунки; панель з’являється і зникає разом із панеллю завдань."), 13, "Text.Muted", wrap: true);
        about.LineHeight = 20; about.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(about);

        Group(stack, L.T("Підказки"));
        foreach (var (keys, text) in new[]
        {
            (L.T("Клік по вкладці"), L.T("Відкриває або закриває меню без затримки.")),
            (L.T("Подвійний клік по вкладці"), L.T("Перейменування категорії на місці.")),
            (L.T("Перетягування вкладки"), L.T("Змінює порядок категорій.")),
            (L.T("Перетягування ярлика"), L.T("Змінює порядок; над іншою вкладкою — переносить у ту категорію.")),
            (L.T("Файли з Провідника"), L.T("Перетягніть .exe, ярлик або папку на вкладку чи в меню.")),
            (L.T("Shift + клік"), L.T("Нове вікно вже запущеного застосунку. Ctrl + Shift + клік — від імені адміністратора.")),
            ("Esc", L.T("Закриває меню."))
        })
        {
            var key = Ui.Text(keys, 12.5, "Text", bold: true); key.Width = 210;
            var description = Ui.Text(text, 12.5, "Text.Muted", wrap: true);
            var line = new DockPanel { Margin = new Thickness(2, 0, 0, 9) };
            DockPanel.SetDock(key, Dock.Left); key.VerticalAlignment = VerticalAlignment.Top;
            line.Children.Add(key); line.Children.Add(description);
            stack.Children.Add(line);
        }
    }
}
