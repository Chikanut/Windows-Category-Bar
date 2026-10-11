using CategoryBar.Services;
using Microsoft.Win32;

namespace CategoryBar.Views;

/// <summary>Small modal dialogs: confirmation, shortcut properties, repairing a broken shortcut.</summary>
internal static class Dialogs
{
    private static (ChromeWindow Window, StackPanel Content, StackPanel Buttons) Frame(string title, double width, double height)
    {
        var window = new ChromeWindow(title, width, height) { ShowInTaskbar = false, Topmost = true };
        var content = new StackPanel { Margin = new Thickness(22, 6, 22, 0) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(22, 0, 22, 20) };
        window.Body.Children.Add(content); window.Body.Children.Add(buttons);
        return (window, content, buttons);
    }

    private static Button Sized(Button button, bool first = false)
    {
        button.MinWidth = 96; button.MinHeight = 32;
        if (!first) button.Margin = new Thickness(8, 0, 0, 0);
        return button;
    }

    private static TextBlock Heading(string text)
    {
        var heading = Ui.Text(text, 17, "Text", bold: true, wrap: true);
        heading.Margin = new Thickness(0, 0, 0, 8);
        return heading;
    }

    internal static bool Confirm(string title, string text, string confirm, bool danger = false)
    {
        var (window, content, buttons) = Frame(L.T("Панель категорій"), 440, 220);
        var result = false;
        content.Children.Add(Heading(title));
        var body = Ui.Text(text, 13, "Text.Muted", wrap: true); body.LineHeight = 19;
        content.Children.Add(body);
        buttons.Children.Add(Sized(Ui.Button(L.T("Скасувати"), window.Close), true));
        buttons.Children.Add(Sized(Ui.Button(confirm, () => { result = true; window.Close(); }, danger ? "Button.Danger" : "Button.Primary")));
        window.ShowDialog();
        return result;
    }

    /// <summary>An empty category is removed at once; otherwise the user chooses what happens to its shortcuts.</summary>
    internal static void DeleteCategory(Session session, Category category)
    {
        var list = session.Config.Categories;
        if (category.Shortcuts.Count == 0)
        {
            session.Change(() => list.Remove(category), L.F("Категорію «{0}» видалено", category.Name));
            return;
        }
        var (window, content, buttons) = Frame(L.T("Панель категорій"), 460, 270);
        var count = category.Shortcuts.Count;
        content.Children.Add(Heading(L.F("Видалити категорію «{0}»?", category.Name)));
        var body = Ui.Text(L.N(count, "{0} ярлик буде прибрано з панелі. Самі програми та їхні файли залишаться на комп’ютері.|{0} ярлики буде прибрано з панелі. Самі програми та їхні файли залишаться на комп’ютері.|{0} ярликів буде прибрано з панелі. Самі програми та їхні файли залишаться на комп’ютері."), 13, "Text.Muted", wrap: true);
        body.LineHeight = 19;
        content.Children.Add(body);
        var others = list.Where(c => c != category).ToList();
        var keep = new CheckBox { Content = L.T("Перенести ярлики до"), IsChecked = false, VerticalAlignment = VerticalAlignment.Center };
        var target = new ComboBox { Width = 190, Margin = new Thickness(10, 0, 0, 0), ItemsSource = others.Select(c => c.Name).ToList(), SelectedIndex = 0 };
        target.SelectionChanged += (_, _) => keep.IsChecked = true;
        if (others.Count > 0)
            content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0), Children = { keep, target } });
        buttons.Children.Add(Sized(Ui.Button(L.T("Скасувати"), window.Close), true));
        buttons.Children.Add(Sized(Ui.Button(L.T("Видалити"), () =>
        {
            var destination = keep.IsChecked == true && target.SelectedIndex >= 0 ? others[target.SelectedIndex] : null;
            window.Close();
            session.Change(() =>
            {
                if (destination is not null) destination.Shortcuts.AddRange(category.Shortcuts);
                list.Remove(category);
            }, L.F("Категорію «{0}» видалено", category.Name), destination is null ? L.N(count, "{0} ярлик прибрано з панелі.|{0} ярлики прибрано з панелі.|{0} ярликів прибрано з панелі.") : L.F("Ярлики перенесено до «{0}».", destination.Name));
        }, "Button.Danger")));
        window.ShowDialog();
    }

    /// <summary>Chooses what a category looks like on the Windows taskbar. A click on an option applies it.</summary>
    internal static void PickIcon(Session session, Category category)
    {
        var (window, content, buttons) = Frame(L.T("Значок на панелі завдань"), 470, 640);
        content.Children.Add(Heading(category.Name));
        void Apply(string icon) { window.Close(); session.Change(() => category.Icon = icon, L.F("Значок «{0}» змінено", category.Name)); }
        Button Option(FrameworkElement picture, string? tip, string value)
        {
            var button = Ui.Button("", () => Apply(value), "Button.Secondary");
            button.Content = picture; button.ToolTip = tip; button.Width = 44; button.Height = 44; button.Padding = new Thickness(0); button.Margin = new Thickness(0, 0, 6, 6);
            return button;
        }
        FrameworkElement Caption(string text) { var caption = Ui.Text(text, 12, "Text.Muted"); caption.Margin = new Thickness(0, 14, 0, 8); return caption; }

        content.Children.Add(Caption(L.T("Значок застосунку")));
        var apps = new WrapPanel();
        var automatic = Ui.Button(L.T("Перший застосунок"), () => Apply(""), category.Icon.Length == 0 ? "Button.Primary" : "Button.Secondary");
        automatic.Height = 44; automatic.Margin = new Thickness(0, 0, 6, 6);
        apps.Children.Add(automatic);
        foreach (var shortcut in category.Shortcuts.Where(s => !LaunchTarget.IsLink(s.Target)).Take(23))
            apps.Children.Add(Option(Ui.ShortcutIcon(shortcut, 28), shortcut.Name, "app:" + shortcut.Id));
        content.Children.Add(apps);

        content.Children.Add(Caption(L.T("Текст: до трьох символів на плитці кольору теми")));
        var letters = new TextBox { MaxLength = 3, Width = 90, Height = 34, VerticalContentAlignment = VerticalAlignment.Center, Text = CategoryIcons.TryText(category.Icon, out var written) ? written : "" };
        void ApplyText() { if (CategoryIcons.TryText("text:" + letters.Text, out var text)) Apply("text:" + text); }
        letters.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ApplyText(); } };
        var useText = Ui.Button(L.T("Застосувати"), ApplyText); useText.Height = 34; useText.Margin = new Thickness(8, 0, 0, 0);
        content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { letters, useText } });

        if (CategoryIcons.Font is { } font)
        {
            content.Children.Add(Caption(L.T("Символ")));
            var chosen = CategoryIcons.TryGlyph(category.Icon, out _, out var used) ? $"#{used.R:X2}{used.G:X2}{used.B:X2}" : CategoryIcons.Colors[0];
            var swatches = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            var glyphs = new WrapPanel();
            void Fill()
            {
                swatches.Children.Clear(); glyphs.Children.Clear();
                foreach (var color in CategoryIcons.Colors)
                {
                    var swatch = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand, Background = new SolidColorBrush(Theme.Parse(color)), BorderThickness = new Thickness(color == chosen ? 3 : 0) }
                        .Res(Border.BorderBrushProperty, "Text");
                    swatch.MouseLeftButtonUp += (_, _) => { chosen = color; Fill(); };
                    swatches.Children.Add(swatch);
                }
                foreach (var glyph in CategoryIcons.Glyphs)
                {
                    var tile = new Border
                    {
                        Width = 32, Height = 32, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Theme.Parse(chosen)),
                        Child = new TextBlock { Text = glyph, FontFamily = font, FontSize = 17, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
                    };
                    glyphs.Children.Add(Option(tile, null, CategoryIcons.Format(glyph, chosen)));
                }
            }
            Fill();
            content.Children.Add(swatches);
            content.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 250, Content = glyphs });
        }
        buttons.Children.Add(Sized(Ui.Button(L.T("Скасувати"), window.Close), true));
        window.ShowDialog();
    }

    private static FrameworkElement Field(string label, TextBox box, Button? extra = null)
    {
        var caption = Ui.Text(label, 12, "Text.Muted"); caption.Margin = new Thickness(0, 12, 0, 5);
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition());
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(box);
        if (extra is not null) { extra.Margin = new Thickness(8, 0, 0, 0); line.Children.Add(extra.At(1)); }
        return new StackPanel { Children = { caption, line } };
    }

    internal static void Properties(Session session, Shortcut shortcut)
    {
        var (window, content, buttons) = Frame(L.T("Властивості ярлика"), 520, 470);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        header.Children.Add(Ui.AppIcon(shortcut.Name, shortcut.Target, 32));
        var title = Ui.Text(shortcut.Name, 17, "Text", bold: true); title.Margin = new Thickness(12, 0, 0, 0);
        header.Children.Add(title);
        content.Children.Add(header);

        var name = new TextBox { Text = shortcut.Name, MaxLength = 100 };
        var target = new TextBox { Text = shortcut.Target };
        var arguments = new TextBox { Text = shortcut.Arguments, Tag = L.T("Необов’язково") };
        var directory = new TextBox { Text = shortcut.WorkingDirectory, Tag = L.T("Типово — папка програми") };
        var admin = new CheckBox { Content = L.T("Завжди запускати від імені адміністратора"), IsChecked = shortcut.RunAsAdministrator, Margin = new Thickness(0, 16, 0, 0) };
        var browse = Ui.Button(L.T("Огляд…"), () => { if (PickFile() is { } file) target.Text = file; });
        content.Children.Add(Field(L.T("Назва"), name));
        content.Children.Add(Field(L.T("Ціль"), target, browse));
        content.Children.Add(Field(L.T("Аргументи"), arguments));
        content.Children.Add(Field(L.T("Робоча папка"), directory));
        content.Children.Add(admin);

        buttons.Children.Add(Sized(Ui.Button(L.T("Скасувати"), window.Close), true));
        buttons.Children.Add(Sized(Ui.Button(L.T("Зберегти"), () =>
        {
            if (name.Text.Trim().Length == 0 || target.Text.Trim().Length == 0) { name.Focus(); return; }
            window.Close();
            IconCache.Forget(shortcut.Target);
            session.Change(() =>
            {
                shortcut.Name = name.Text.Trim(); shortcut.Target = target.Text.Trim().Trim('"');
                shortcut.Arguments = arguments.Text.Trim(); shortcut.WorkingDirectory = directory.Text.Trim().Trim('"');
                shortcut.RunAsAdministrator = admin.IsChecked == true;
            }, L.T("Ярлик оновлено"));
        }, "Button.Primary")));
        window.ShowDialog();
    }

    internal static string? PickFile()
    {
        var dialog = new OpenFileDialog { Title = L.T("Виберіть програму або ярлик"), Filter = L.T("Програми та ярлики|*.exe;*.lnk;*.url|Усі файли|*.*"), DereferenceLinks = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Points a broken shortcut at a new file.</summary>
    internal static void Relocate(Session session, Shortcut shortcut)
    {
        if (PickFile() is not { } file) return;
        IconCache.Forget(shortcut.Target);
        session.Change(() => { shortcut.Target = file; shortcut.Arguments = ""; shortcut.WorkingDirectory = ""; Shortcuts.KeepOwnCopy(shortcut, session.Store.Path); }, L.F("Ярлик «{0}» виправлено", shortcut.Name));
    }

    /// <summary>Asks for a web address or a Steam link.</summary>
    internal static Shortcut? PromptLink()
    {
        var (window, content, buttons) = Frame(L.T("Додати посилання"), 480, 300);
        Shortcut? result = null;
        content.Children.Add(Heading(L.T("Вебпосилання або гра Steam")));
        var address = new TextBox { Tag = L.T("https://…  або  steam://rungameid/…") };
        var name = new TextBox { Tag = L.T("Як підписати в меню") };
        content.Children.Add(Field(L.T("Адреса"), address));
        content.Children.Add(Field(L.T("Назва"), name));
        void Accept()
        {
            var url = address.Text.Trim();
            if (!url.Contains("://")) url = "https://" + url;
            if (Shortcuts.FromPath(url) is not { } shortcut) { address.Focus(); address.SelectAll(); return; }
            if (name.Text.Trim().Length > 0) shortcut.Name = name.Text.Trim();
            result = shortcut;
            window.Close();
        }
        buttons.Children.Add(Sized(Ui.Button(L.T("Скасувати"), window.Close), true));
        buttons.Children.Add(Sized(Ui.Button(L.T("Додати"), Accept, "Button.Primary")));
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Accept(); } };
        window.Loaded += (_, _) => address.Focus();
        window.ShowDialog();
        return result;
    }
}
