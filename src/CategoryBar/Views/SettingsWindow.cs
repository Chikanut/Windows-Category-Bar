using System.Diagnostics;
using System.Reflection;

namespace CategoryBar.Views;

/// <summary>Settings with a sidebar. Every control applies and saves immediately.</summary>
internal sealed class SettingsWindow : ChromeWindow
{
    private static readonly (string Key, string Title, string Icon)[] Pages =
    [
        ("General", "Загальне", Icons.Home), ("Appearance", "Вигляд", Icons.Palette), ("Panel", "Панель і меню", Icons.Layout),
        ("Categories", "Категорії", Icons.Apps), ("About", "Про програму", Icons.Info)
    ];

    private readonly Session session;
    private readonly StackPanel navigation = new() { Margin = new Thickness(10, 6, 10, 10) };
    private readonly Grid page = new();
    private string current = "General";
    private BoardView? board;
    private Settings Settings => session.Settings;

    internal SettingsWindow(Session session) : base("Панель категорій — Налаштування", 900, 640, canMinimize: true)
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
        Group(stack, "Запуск");
        var startup = new CheckBox { IsChecked = StartupRegistration.IsEnabled }.Res(StyleProperty, "Toggle");
        startup.Click += (_, _) =>
        {
            try { StartupRegistration.SetEnabled(startup.IsChecked == true, App.Instance.CustomConfigPath); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                startup.IsChecked = StartupRegistration.IsEnabled;
                session.Toast("Не вдалося змінити автозапуск", ex.Message, ToastKind.Error);
            }
        };
        stack.Children.Add(Card("Запускати разом із Windows", "Панель з’явиться одразу після входу в систему.", startup));

        Group(stack, "Застосунки");
        stack.Children.Add(Card("Індикатори запущених застосунків", "Позначка під іконкою в меню та під назвою категорії.", Toggle(Settings.ShowRunningIndicators, (s, v) => s.ShowRunningIndicators = v)));
        stack.Children.Add(Card("Клік по запущеному застосунку", "Shift + клік завжди відкриває нове вікно.",
            Segments(Settings.ActivateRunning ? "Activate" : "Launch", (s, v) => s.ActivateRunning = v == "Activate", ("Activate", "Перейти до вікна"), ("Launch", "Запустити ще раз"))));

        Group(stack, "Початкове налаштування");
        stack.Children.Add(Card("Перенести з панелі завдань Windows", "Знайти закріплені застосунки та розкласти їх по категоріях. Наявні категорії можна залишити.",
            Ui.Button("Відкрити майстер", () => App.Instance.ShowOnboarding(), icon: Icons.Import)));

        Group(stack, "Дані");
        stack.Children.Add(Card("Файл налаштувань", session.Store.Path, Ui.Button("Показати в папці", () =>
            Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + session.Store.Path + "\""))?.Dispose(), icon: Icons.Folder)));
        stack.Children.Add(Card("Скасувати останню зміну", "Повертає останнє додавання, переміщення або видалення.",
            Ui.Button("Скасувати", () => { session.Undo(); Navigate(current); }, icon: Icons.Undo)));
    }

    private void Appearance(StackPanel stack)
    {
        Group(stack, "Кольори");
        stack.Children.Add(Card("Тема", "«Як у Windows» перемикається разом із системною темою.",
            Segments(Settings.ThemeMode, (s, v) => s.ThemeMode = v, ("System", "Як у Windows"), ("Dark", "Темна"), ("Light", "Світла"))));

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        void Rebuild()
        {
            swatches.Children.Clear();
            var system = Settings.AccentMode == "System";
            swatches.Children.Add(Swatch(Theme.WindowsAccent(), system, "Колір Windows", s => s.AccentMode = "System", Icons.Windows));
            foreach (var preset in Theme.AccentPresets)
                swatches.Children.Add(Swatch(Theme.Parse(preset), !system && Settings.AccentColor.Equals(preset, StringComparison.OrdinalIgnoreCase), preset, s => { s.AccentMode = "Custom"; s.AccentColor = preset; }));
            var custom = new TextBox { Text = Settings.AccentColor, Width = 84, MaxLength = 7, Margin = new Thickness(8, 0, 0, 0), ToolTip = "Власний колір у форматі #RRGGBB", FontFamily = new FontFamily("Consolas") };
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
        stack.Children.Add(Card("Колір акценту", "Колір Windows оновлюється автоматично, коли ви змінюєте його в системі.", swatches, stacked: true));
        stack.Children.Add(Card("Тонувати поверхні акцентом", "Додає панелі й меню легкий відтінок вибраного кольору.", Toggle(Settings.TintSurfaces, (s, v) => s.TintSurfaces = v)));

        Group(stack, "Текст і форма");
        var fonts = new ComboBox { Width = 230 };
        var preferred = new[] { Settings.DefaultFont, "Segoe UI", "Segoe UI Variable Display", "Inter", "Onest", "Bahnschrift", "Calibri", "Verdana", "Tahoma", "Consolas", "Cascadia Code", "JetBrains Mono" };
        var installed = Fonts.SystemFontFamilies.Select(f => f.Source).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = preferred.Where(installed.Contains).Concat(installed.OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (!names.Contains(Settings.FontName, StringComparer.OrdinalIgnoreCase)) names.Insert(0, Settings.FontName);
        foreach (var name in names) fonts.Items.Add(new ComboBoxItem { Content = name, FontFamily = new FontFamily(name), Tag = name });
        fonts.SelectedIndex = names.FindIndex(n => n.Equals(Settings.FontName, StringComparison.OrdinalIgnoreCase));
        fonts.SelectionChanged += (_, _) => { if (fonts.SelectedItem is ComboBoxItem { Tag: string name }) session.ChangeSettings(s => s.FontName = name); };
        stack.Children.Add(Card("Шрифт", "Використовується на панелі, в меню та у вікнах програми.", fonts));
        stack.Children.Add(Card("Масштаб інтерфейсу", null, Range(Settings.InterfaceScale * 100, 80, 150, 5, v => v.ToString("0") + "%", (s, v) => s.InterfaceScale = v / 100)));
        stack.Children.Add(Card("Непрозорість панелі та меню", null, Range(Settings.SurfaceOpacity * 100, 50, 100, 2, v => v.ToString("0") + "%", (s, v) => s.SurfaceOpacity = v / 100)));
        stack.Children.Add(Card("Заокруглення кутів", null, Range(Settings.CornerRadius, 0, 16, 1, v => v.ToString("0") + " px", (s, v) => s.CornerRadius = (int)v)));
        stack.Children.Add(Card("Стандартний вигляд", "Повертає тему, колір, шрифт, масштаб і форму до початкових значень.", Ui.Button("Скинути", () =>
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
        Group(stack, "Розташування");
        stack.Children.Add(Card("Вирівнювання панелі", "«Як у Windows» повторює розташування кнопок панелі завдань.",
            Segments(Settings.Alignment, (s, v) => s.Alignment = v, ("Windows", "Як у Windows"), ("Left", "Ліворуч"), ("Center", "По центру"), ("Right", "Праворуч"))));
        stack.Children.Add(Card("Монітори", "На всіх моніторах, де є панель завдань Windows, або лише на основному.",
            Segments(Settings.AllMonitors ? "All" : "Primary", (s, v) => s.AllMonitors = v == "All", ("Primary", "Основний"), ("All", "Усі"))));
        stack.Children.Add(Card("Місце на екрані", "Резервування зменшує максимізовані вікна, щоб панель їх не перекривала.",
            Segments(Settings.ReserveSpace ? "Reserve" : "Overlay", (s, v) => s.ReserveSpace = v == "Reserve", ("Overlay", "Поверх вікон"), ("Reserve", "Резервувати"))));
        stack.Children.Add(Card("Мініатюрний режим", "Панель ховається за панель завдань Windows, видно лише стрілку. Наведіть на неї — панель виїде.", Toggle(Settings.MiniatureMode, (s, v) => s.MiniatureMode = v)));

        Group(stack, "Меню");
        stack.Children.Add(Card("Затримка відкриття", "Випадковий проліт курсора не відкриває меню.", Range(Settings.OpenDelayMs, 0, 600, 25, v => v.ToString("0") + " мс", (s, v) => s.OpenDelayMs = (int)v)));
        stack.Children.Add(Card("Затримка закриття", "Скільки меню чекає після виходу курсора.", Range(Settings.CloseDelayMs, 0, 1000, 50, v => v.ToString("0") + " мс", (s, v) => s.CloseDelayMs = (int)v)));
        stack.Children.Add(Card("Лічильники на вкладках", "Кількість ярликів поруч із назвою категорії.", Toggle(Settings.ShowCounts, (s, v) => s.ShowCounts = v)));
        stack.Children.Add(Card("Пошук у меню", "Поле пошуку з’являється, коли в категорії стільки ярликів або більше.",
            Range(Settings.SearchThreshold, 0, 40, 1, v => v == 0 ? "вимкнено" : "від " + v.ToString("0"), (s, v) => s.SearchThreshold = (int)v)));
        stack.Children.Add(Card("Сітка для великих категорій", "Від 20 ярликів меню показує іконки сіткою.", Toggle(Settings.GridForLargeCategories, (s, v) => s.GridForLargeCategories = v)));
    }

    private FrameworkElement CategoriesPage()
    {
        var heading = Ui.Text("Категорії", 22, "Text", bold: true);
        var hint = Ui.Text("Перетягуйте застосунки між категоріями, а категорії — за заголовок. Подвійний клік по назві перейменовує.", 12.5, "Text.Muted", wrap: true);
        hint.Margin = new Thickness(0, 6, 0, 0);
        var titles = new StackPanel { Children = { heading, hint } };
        var undo = Ui.Button("Скасувати зміну", session.Undo, icon: Icons.Undo); undo.VerticalAlignment = VerticalAlignment.Top;
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
        var name = Ui.Text("Панель категорій", 18, "Text", bold: true);
        var build = Ui.Text("Версія " + (version is null ? "—" : version.ToString(3)) + " · Windows 11", 12.5, "Text.Muted"); build.Margin = new Thickness(0, 4, 0, 0);
        var texts = new StackPanel { Margin = new Thickness(16, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { name, build } };
        stack.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 18), Children = { logo, texts } });
        var about = Ui.Text("Категорії застосунків над панеллю завдань Windows. Наведіть курсор на категорію, щоб побачити її застосунки; панель з’являється і зникає разом із панеллю завдань.", 13, "Text.Muted", wrap: true);
        about.LineHeight = 20; about.Margin = new Thickness(0, 0, 0, 14);
        stack.Children.Add(about);

        Group(stack, "Підказки");
        foreach (var (keys, text) in new[]
        {
            ("Клік по вкладці", "Відкриває або закриває меню без затримки."),
            ("Подвійний клік по вкладці", "Перейменування категорії на місці."),
            ("Перетягування вкладки", "Змінює порядок категорій."),
            ("Перетягування ярлика", "Змінює порядок; над іншою вкладкою — переносить у ту категорію."),
            ("Файли з Провідника", "Перетягніть .exe, ярлик або папку на вкладку чи в меню."),
            ("Shift + клік", "Нове вікно вже запущеного застосунку. Ctrl + Shift + клік — від імені адміністратора."),
            ("Esc", "Закриває меню.")
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
