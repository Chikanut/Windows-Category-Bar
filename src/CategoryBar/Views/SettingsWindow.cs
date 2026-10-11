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
    private readonly StackPanel navigation = new StackPanel().Res(StyleProperty, "Theme.NavigationPanel");
    private readonly Grid page = new();
    private string current = "General";
    private BoardView? board;
    private readonly Dictionary<string, (ThemeDefinition Theme, ContentControl Host)> themePreviews = [];
    private Button? updateCheck;
    private Button? updateInstall;
    private TextBlock? updateStatus, pinStatus;
    private bool shownTaskbarIcons;
    private Settings Settings => session.Settings;

    internal SettingsWindow(Session session) : base(L.T("Панель категорій — Налаштування"), 900, 640, canMinimize: true)
    {
        this.session = session;
        Body.Children.Add(new Theming.ThemeSurface { Header = navigation, Content = page }.Res(StyleProperty, "Theme.Workspace"));
        session.Changed += OnSessionChanged;
        App.Instance.Updates.Changed += RefreshUpdates;
        if (App.Instance.Groups is { } groups) { groups.StateChanged += RefreshPins; Closed += (_, _) => groups.StateChanged -= RefreshPins; }
        Closed += (_, _) => { session.Changed -= OnSessionChanged; App.Instance.Updates.Changed -= RefreshUpdates; };
        Navigate("General");
    }

    private void RefreshPins() { if (pinStatus is not null) pinStatus.Text = PinStatus(); }

    private string PinStatus() => L.F("Закріплено {0} із {1}.", App.Instance.Groups?.PinnedCount ?? 0, session.Config.Categories.Count);

    private void OnSessionChanged()
    {
        if (current == "Panel" && shownTaskbarIcons != Settings.TaskbarIcons) { Navigate("Panel"); return; }
        RefreshPins();
        if (current == "Categories") board?.Refresh();
        if (current == "Appearance")
            foreach (var (theme, host) in themePreviews.Values) host.Content = ThemePreview.Create(theme, Theme.IsDark, Settings);
    }

    internal void Navigate(string key)
    {
        current = Pages.Any(p => p.Key == key) ? key : "General";
        navigation.Children.Clear();
        foreach (var (pageKey, title, icon) in Pages)
        {
            var active = pageKey == current;
            var content = new StackPanel { Orientation = Orientation.Horizontal };
            // The entry's own colours come from ".sidebar-item" and its ":selected" state in the theme's stylesheet.
            var glyph = Ui.Icon(icon, active ? "sidebar-item:selected.Icon" : "sidebar-item.Icon"); glyph.Margin = new Thickness(0, 0, 11, 0);
            content.Children.Add(glyph);
            var label = Ui.Text(title, 13, "inherit", bold: active);
            if (!active) label.Opacity = .82;
            label.SetResourceReference(TextBlock.FontSizeProperty, "sidebar-item.FontSize");
            content.Children.Add(label);
            var item = Ui.Button(title, () => Navigate(pageKey), "Navigation.Item");
            item.Content = content; item.Tag = active ? "selected" : null;
            System.Windows.Automation.AutomationProperties.SetName(item, title);
            navigation.Children.Add(item);
        }
        board = null;
        Theming.ThemeMotion.Stop(page);
        page.Children.Clear();
        Theming.ThemeMotion.Play(page, "Theme.Page.Enter", offset: 12, milliseconds: 220);
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
        // Buttons placed straight on the page keep their natural width instead of stretching across it.
        foreach (var button in stack.Children.OfType<Button>()) { button.HorizontalAlignment = HorizontalAlignment.Left; button.Margin = new Thickness(0, 4, 0, 8); }
        page.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = stack });
        // Cards may cascade in when the theme defines "Theme.Card.Enter"; only the first screenful is worth animating.
        var order = 0;
        foreach (var card in stack.Children.OfType<Theming.ThemeSurface>().Take(8))
        {
            var index = order++;
            card.Loaded += (_, _) => Theming.ThemeMotion.Play(card, "Theme.Card.Enter", fallback: false, delay: index * Theming.ThemeMotion.Stagger(card, "card"));
        }
    }

    // ---- Building blocks -------------------------------------------------------------------------

    private static void Group(StackPanel stack, string title)
    {
        var caption = Ui.Text(title.ToUpper(), 11, "Text.Faint", bold: true);
        caption.Margin = new Thickness(2, stack.Children.Count > 1 ? 18 : 0, 0, 8);
        stack.Children.Add(caption);
    }

    private static FrameworkElement Card(string label, string? hint, FrameworkElement control, bool stacked = false)
    {
        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(Ui.Text(label, 13.5));
        if (hint is not null)
        {
            var note = Ui.Text(hint, 12, "Text.Muted", wrap: true); note.Margin = new Thickness(0, 3, 0, 0); note.LineHeight = 17;
            texts.Children.Add(note);
        }
        return new Theming.ThemeSurface { Header = texts, Content = control, Tag = stacked ? "stacked" : null }
            .Res(StyleProperty, "Theme.Setting");
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
        Group(stack, L.T("Оновлення"));
        updateStatus = Ui.Text(App.Instance.Updates.Status, 12, "Text.Muted", wrap: true);
        updateStatus.Margin = new Thickness(0, 0, 0, 10);
        updateCheck = Ui.Button(L.T("Перевірити оновлення"), () => { _ = App.Instance.Updates.Check(manual: true); });
        updateInstall = Ui.Button(L.T("Оновити та перезапустити"), () => { _ = App.Instance.Updates.Install(); }, "Button.Primary");
        updateInstall.Margin = new Thickness(0, 8, 0, 0);
        var updateControls = new StackPanel { Children = { updateStatus, updateCheck, updateInstall } };
        stack.Children.Add(Card(L.F("Версія {0}", Services.AppUpdates.Version), L.T("Перевірка при запуску. Встановлення — за вашим підтвердженням."), updateControls, stacked: true));
        RefreshUpdates();
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

    private void RefreshUpdates()
    {
        var updates = App.Instance.Updates;
        if (updateStatus is not null) updateStatus.Text = updates.Status;
        if (updateCheck is not null) updateCheck.IsEnabled = !updates.Busy;
        if (updateInstall is not null)
        {
            updateInstall.Visibility = updates.HasUpdate ? Visibility.Visible : Visibility.Collapsed;
            updateInstall.IsEnabled = !updates.Busy;
        }
    }

    private void Appearance(StackPanel stack)
    {
        ThemeGallery(stack);
        ThemeParameters(stack);
        Group(stack, L.T("Кольори"));
        stack.Children.Add(Card(L.T("Тема"), L.T("«Як у Windows» перемикається разом із системною темою."),
            Segments(Settings.ThemeMode, (s, v) => { s.ThemeMode = v; Dispatcher.BeginInvoke(() => Navigate("Appearance")); }, ("System", L.T("Як у Windows")), ("Dark", L.T("Темна")), ("Light", L.T("Світла")))));

        var swatches = new StackPanel { Orientation = Orientation.Horizontal };
        void Rebuild()
        {
            swatches.Children.Clear();
            var system = Settings.AccentMode == "System";
            swatches.Children.Add(Swatch(Theme.Parse((Theme.IsDark ? Theme.Current.Dark : Theme.Current.Light)["Accent"]), Settings.AccentMode == "Theme", L.T("Колір теми"), s => s.AccentMode = "Theme"));
            swatches.Children.Add(Swatch(Theme.WindowsAccent(), system, L.T("Колір Windows"), s => s.AccentMode = "System", Icons.Windows));
            foreach (var preset in Theme.AccentPresets)
                swatches.Children.Add(Swatch(Theme.Parse(preset), Settings.AccentMode == "Custom" && Settings.AccentColor.Equals(preset, StringComparison.OrdinalIgnoreCase), preset, s => { s.AccentMode = "Custom"; s.AccentColor = preset; }));
            var custom = new TextBox { Text = Settings.AccentColor, Width = 84, MaxLength = 7, Margin = new Thickness(8, 0, 0, 0), ToolTip = L.T("Власний колір у форматі #RRGGBB"), FontFamily = new FontFamily("Consolas") };
            void Apply()
            {
                var text = custom.Text.Trim();
                if (!text.StartsWith('#')) text = "#" + text;
                if (!Settings.IsHexColor(text) || Settings.AccentMode == "Custom" && text.Equals(Settings.AccentColor, StringComparison.OrdinalIgnoreCase)) return;
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

        PaletteColors(stack);

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
                s.ThemeId = defaults.ThemeId; s.ThemeMode = defaults.ThemeMode; s.AccentMode = defaults.AccentMode; s.AccentColor = defaults.AccentColor; s.TintSurfaces = defaults.TintSurfaces;
                s.ThemeColors.Clear();
                s.FontName = defaults.FontName; s.InterfaceScale = defaults.InterfaceScale; s.SurfaceOpacity = defaults.SurfaceOpacity; s.CornerRadius = defaults.CornerRadius;
            });
            Navigate(current);
        })));
    }

    private void PaletteColors(StackPanel stack)
    {
        var theme = Theme.Current;
        var prefix = Theme.IsDark ? "Dark:" : "Light:";
        var palette = Theme.Palette(Settings, theme, Theme.IsDark);
        Group(stack, L.T("Кольори поверхонь"));
        var note = Ui.Text(L.T("Зберігаються окремо для кожної теми та світлого або темного режиму."), 12, "Text.Muted", wrap: true); note.Margin = new Thickness(2, 0, 0, 10);
        stack.Children.Add(note);
        foreach (var (token, label) in new[] {
            ("Window.Bg", L.T("Фон вікна")), ("Sidebar.Bg", L.T("Фон навігації")),
            ("Bar.Bg", L.T("Фон панелі")), ("Menu.Bg", L.T("Фон меню")),
            ("Card.Bg", L.T("Фон карток")), ("Control.Bg", L.T("Фон елементів керування")),
            ("Text", L.T("Текст")), ("Text.Muted", L.T("Другорядний текст")), ("Surface.Border", L.T("Контури")) })
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 10, 0), Background = new SolidColorBrush(Theme.Parse(palette[token])) };
            var input = new TextBox { Text = palette[token], Width = 112, MaxLength = 7, FontFamily = new FontFamily("Consolas"), ToolTip = L.T("Власний колір у форматі #RRGGBB") };
            void ApplyColor()
            {
                var value = input.Text.Trim(); if (!value.StartsWith('#')) value = "#" + value;
                if (!Settings.IsHexColor(value)) { input.Text = palette[token]; return; }
                value = value.ToUpperInvariant(); if (value == palette[token]) return;
                if (!session.ChangeSettings(s => {
                    if (!s.ThemeColors.TryGetValue(theme.Id, out var colors)) s.ThemeColors[theme.Id] = colors = [];
                    colors[prefix + token] = value;
                })) { input.Text = palette[token]; return; }
                palette[token] = value; input.Text = value; swatch.Background = new SolidColorBrush(Theme.Parse(value));
            }
            input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { ApplyColor(); e.Handled = true; } };
            input.LostKeyboardFocus += (_, _) => ApplyColor();
            System.Windows.Automation.AutomationProperties.SetName(input, label);
            var picker = Ui.Button("", () => { if (Services.ColorPicker.Show(this, Theme.Parse(palette[token])) is { } color) { input.Text = Theme.Hex(color); ApplyColor(); } }, "Button.Icon");
            picker.Content = swatch; picker.Width = 36; picker.Height = 36; picker.Margin = new Thickness(0, 0, 8, 0); swatch.Margin = new Thickness(0);
            picker.ToolTip = L.T("Вибрати колір"); System.Windows.Automation.AutomationProperties.SetName(picker, label + ": " + L.T("Вибрати колір"));
            row.Children.Add(picker); row.Children.Add(input);
            stack.Children.Add(Card(label, null, row));
        }
        stack.Children.Add(Ui.Button(L.T("Скинути кольори цієї теми"), () => {
            session.ChangeSettings(s => { if (s.ThemeColors.TryGetValue(theme.Id, out var colors))
                foreach (var key in colors.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) colors.Remove(key); });
            Navigate("Appearance");
        }, "Button.Secondary"));
    }

    private void ThemeParameters(StackPanel stack)
    {
        var theme = Theme.Current;
        if (theme.Parameters.Count == 0) return;
        // Parameters keep the author's order; a new group name starts a new heading.
        string? heading = null;
        foreach (var parameter in theme.Parameters)
        {
            if (parameter.Group != heading)
            {
                heading = parameter.Group;
                Group(stack, heading.Length == 0 ? L.T("Параметри теми") : L.T("Параметри теми") + " · " + L.T(heading));
            }
            var value = parameter.Resolve(Settings, theme.Id);
            FrameworkElement control;
            void Set(Settings settings, string next) => parameter.Set(settings, theme.Id, next);
            switch (parameter.Type)
            {
                case "Number":
                    control = Range(double.Parse(value, System.Globalization.CultureInfo.InvariantCulture), parameter.Minimum, parameter.Maximum, parameter.Step,
                        number => number.ToString("0.###"), (settings, number) => Set(settings, number.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)));
                    break;
                case "Toggle":
                    control = Toggle(value == "true", (settings, enabled) => Set(settings, enabled ? "true" : "false"));
                    break;
                case "Choice":
                    var choices = new ComboBox { MinWidth = 190, MaxWidth = 250 };
                    foreach (var option in parameter.Options) choices.Items.Add(new ComboBoxItem { Content = L.T(option.Value), Tag = option.Key });
                    choices.SelectedIndex = parameter.Options.Keys.ToList().IndexOf(value);
                    choices.SelectionChanged += (_, _) =>
                    {
                        if (choices.SelectedItem is ComboBoxItem { Tag: string selected }) session.ChangeSettings(settings => Set(settings, selected));
                    };
                    control = choices;
                    break;
                default:
                    var color = new TextBox { Text = value, Width = 112, MaxLength = 7, FontFamily = new FontFamily("Consolas"), ToolTip = L.T("Власний колір у форматі #RRGGBB") };
                    var swatch = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(6) }
                        .Res(Border.BackgroundProperty, "Theme.Parameter." + parameter.Id);
                    void CommitColor()
                    {
                        var next = color.Text.Trim(); if (!next.StartsWith('#')) next = "#" + next;
                        if (!parameter.Accepts(next)) { color.SetResourceReference(Control.BorderBrushProperty, "Danger"); return; }
                        color.SetResourceReference(Control.BorderBrushProperty, "input.BorderBrush");
                        if (next.Equals(parameter.Resolve(Settings, theme.Id), StringComparison.OrdinalIgnoreCase)) return;
                        session.ChangeSettings(settings => Set(settings, next.ToUpperInvariant()));
                    }
                    color.KeyDown += (_, e) => { if (e.Key == Key.Enter) { CommitColor(); e.Handled = true; } };
                    color.LostKeyboardFocus += (_, _) => CommitColor();
                    System.Windows.Automation.AutomationProperties.SetName(color, L.T(parameter.Label));
                    var picker = Ui.Button("", () =>
                    {
                        if (Services.ColorPicker.Show(this, Theme.Parse(parameter.Resolve(Settings, theme.Id))) is not { } picked) return;
                        color.Text = Theme.Hex(picked); CommitColor();
                    }, "Button.Icon");
                    picker.Content = swatch; picker.Width = 36; picker.Height = 36; picker.Margin = new Thickness(0, 0, 8, 0);
                    picker.ToolTip = L.T("Вибрати колір"); System.Windows.Automation.AutomationProperties.SetName(picker, L.T(parameter.Label) + ": " + L.T("Вибрати колір"));
                    control = new StackPanel { Orientation = Orientation.Horizontal, Children = { picker, color } };
                    break;
            }
            System.Windows.Automation.AutomationProperties.SetName(control, L.T(parameter.Label));
            System.Windows.Automation.AutomationProperties.SetHelpText(control, L.T(parameter.Description));
            if (control is StackPanel panel && panel.Children.OfType<Slider>().FirstOrDefault() is { } slider)
                System.Windows.Automation.AutomationProperties.SetName(slider, L.T(parameter.Label));
            stack.Children.Add(Card(L.T(parameter.Label), parameter.Description.Length == 0 ? null : L.T(parameter.Description), control));
        }
        stack.Children.Add(Ui.Button(L.T("Скинути параметри теми"), () =>
        {
            if (session.ChangeSettings(settings => settings.ThemeParameters.Remove(theme.Id))) Navigate("Appearance");
        }));
    }

    private void ThemeGallery(StackPanel stack)
    {
        themePreviews.Clear();
        Group(stack, L.T("Стилі оформлення"));
        var gallery = new Grid();
        gallery.ColumnDefinitions.Add(new ColumnDefinition()); gallery.ColumnDefinitions.Add(new ColumnDefinition());
        var all = Theme.Library.All.ToList();
        for (var i = 0; i < all.Count; i++)
        {
            var theme = all[i]; var selected = theme.Id == Theme.Current.Id;
            if (i % 2 == 0) gallery.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var sample = new StackPanel { IsHitTestVisible = false };
            var preview = new ContentControl { Content = ThemePreview.Create(theme, Theme.IsDark, Settings), IsHitTestVisible = false };
            themePreviews[theme.Id] = (theme, preview); sample.Children.Add(preview);
            var title = Ui.Text(theme.Name + (selected ? "  ✓" : ""), 14, bold: true); title.Margin = new Thickness(0, 10, 0, 4); sample.Children.Add(title);
            var description = L.T(theme.Description);
            sample.Children.Add(Ui.Text(description, 12, "Text.Muted", wrap: true));
            var button = Ui.Button("", () => { session.ChangeSettings(s => Theme.Select(s, theme)); Navigate("Appearance"); }, "Button.Secondary");
            button.Content = sample; button.Padding = new Thickness(12); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            button.Margin = new Thickness(i % 2 == 0 ? 0 : 5, 0, i % 2 == 0 ? 5 : 0, 10);
            if (selected) { button.BorderThickness = new Thickness(2); button.SetResourceReference(Control.BorderBrushProperty, "Accent"); }
            System.Windows.Automation.AutomationProperties.SetName(button, theme.Name);
            System.Windows.Automation.AutomationProperties.SetHelpText(button, description);
            gallery.Children.Add(button.At(i % 2, i / 2));
        }
        stack.Children.Add(gallery);
        var actions = new WrapPanel { Margin = new Thickness(0, 2, 0, 10) };
        actions.Children.Add(Ui.Button(L.T("Імпортувати тему…"), ImportTheme, "Button.Secondary"));
        actions.Children.Add(Ui.Button(L.T("Експортувати тему…"), ExportTheme, "Button.Secondary"));
        if (!Theme.Library.IsBuiltIn(Theme.Current.Id))
            actions.Children.Add(Ui.Button(L.T("Видалити тему"), () =>
            {
                var id = Theme.Current.Id;
                if (!Dialogs.Confirm(L.T("Видалити тему"), Theme.Current.Name, L.T("Видалити"), danger: true)) return;
                ThemeOperation(() => { if (!session.ChangeSettings(s => Theme.Select(s, BuiltInThemes.All[0]))) return; Theme.Library.Remove(id); Navigate("Appearance"); });
            }));
        foreach (FrameworkElement action in actions.Children) action.Margin = new Thickness(0, 0, 6, 6);
        stack.Children.Add(actions);
        stack.Children.Add(Ui.Text(L.T("Вибір стилю застосовує його колір, шрифт і форму. Нижче їх можна налаштувати. Для власних тем використовуйте файли .categorybar-theme.json."), 12, "Text.Muted", wrap: true));
    }

    private void ThemeOperation(Action action)
    {
        try { action(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { session.Toast(L.T("Не вдалося змінити тему"), ex.Message, ToastKind.Error); }
    }

    private void ImportTheme()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = L.T("Теми Category Bar") + "|*.categorybar-theme.json;*.json", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        ThemeOperation(() =>
        {
            var theme = ThemeFiles.Read(dialog.FileName);
            // Keep both themes when IDs collide; downloaded themes cannot overwrite installed ones.
            if (Theme.Library.All.Any(t => t.Id == theme.Id)) theme.Id = "custom-" + Guid.NewGuid().ToString("N");
            _ = Theming.ThemeResourceLoader.Load(theme);
            Theme.Library.Install(theme); session.ChangeSettings(s => Theme.Select(s, theme)); Navigate("Appearance");
            session.Toast(L.T("Тему встановлено"), theme.Name);
        });
    }

    private void ExportTheme()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = L.T("Теми Category Bar") + "|*.categorybar-theme.json", FileName = Theme.Current.Id + ThemeFiles.Extension, AddExtension = true, DefaultExt = ThemeFiles.Extension };
        if (dialog.ShowDialog(this) != true) return;
        ThemeOperation(() =>
        {
            var theme = Theme.Capture(Settings); theme.Id = Theme.Current.Id;
            ThemeFiles.Write(dialog.FileName, theme); session.Toast(L.T("Тему експортовано"), Theme.Current.Name);
        });
    }

    private void PanelPage(StackPanel stack)
    {
        Group(stack, L.T("Вигляд##mode"));
        shownTaskbarIcons = Settings.TaskbarIcons; pinStatus = null;
        var mode = new CheckBox { IsChecked = Settings.TaskbarIcons }.Res(StyleProperty, "Toggle");
        mode.Click += (_, _) => App.Instance.SetTaskbarIcons(mode.IsChecked == true);
        stack.Children.Add(Card(L.T("Значки на панелі завдань"), L.T("Категорії стають значками на панелі завдань Windows, а ця панель ховається. Меню відкривається наведенням або клацанням, права кнопка показує список застосунків."), mode));
        if (Settings.TaskbarIcons && App.Instance.Groups is { } groups)
        {
            pinStatus = Ui.Text(PinStatus(), 12.5, "Text.Muted"); pinStatus.VerticalAlignment = VerticalAlignment.Center; pinStatus.Margin = new Thickness(0, 0, 12, 0);
            stack.Children.Add(Card(L.T("Закріплення значків"), L.T("Значки категорій закріплюються самі й стають першими на панелі завдань; порядок між ними змінюється перетягуванням. Значок, який ви відкріпили вручну, повернеться лише після «Закріпити всі»."),
                new StackPanel { Orientation = Orientation.Horizontal, Children = { pinStatus, Ui.Button(L.T("Закріпити всі"), groups.PinAll, icon: Icons.Check) } }));
            stack.Children.Add(Card(L.T("Роздільник"), L.T("Тонка риска після значків категорій відділяє їх від ваших значків і запущених програм. Клацання по ній відкриває ці налаштування."), Toggle(Settings.TaskbarDivider, (s, v) => s.TaskbarDivider = v)));
            stack.Children.Add(Card(L.T("Значки категорій"), L.T("Значок застосунку, символ на кольоровій плитці або до трьох літер. Зелена крапка на значку означає, що застосунок категорії запущено."),
                Ui.Button(L.T("Упорядкувати категорії…"), () => Navigate("Categories"), icon: Icons.Apps)));
        }

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
        { Margin = new Thickness(28, 0, 0, 18), DeleteRequested = category => Dialogs.DeleteCategory(session, category), IconRequested = category => Dialogs.PickIcon(session, category) };
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
