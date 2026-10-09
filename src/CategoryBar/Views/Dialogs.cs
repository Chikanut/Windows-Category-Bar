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
        var (window, content, buttons) = Frame("Панель категорій", 440, 220);
        var result = false;
        content.Children.Add(Heading(title));
        var body = Ui.Text(text, 13, "Text.Muted", wrap: true); body.LineHeight = 19;
        content.Children.Add(body);
        buttons.Children.Add(Sized(Ui.Button("Скасувати", window.Close), true));
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
            session.Change(() => list.Remove(category), "Категорію «" + category.Name + "» видалено");
            return;
        }
        var (window, content, buttons) = Frame("Панель категорій", 460, 270);
        var count = category.Shortcuts.Count;
        content.Children.Add(Heading("Видалити категорію «" + category.Name + "»?"));
        var body = Ui.Text(Ui.Plural(count, "ярлик", "ярлики", "ярликів") + " буде прибрано з панелі. Самі програми та їхні файли залишаться на комп’ютері.", 13, "Text.Muted", wrap: true);
        body.LineHeight = 19;
        content.Children.Add(body);
        var others = list.Where(c => c != category).ToList();
        var keep = new CheckBox { Content = "Перенести ярлики до", IsChecked = false, VerticalAlignment = VerticalAlignment.Center };
        var target = new ComboBox { Width = 190, Margin = new Thickness(10, 0, 0, 0), ItemsSource = others.Select(c => c.Name).ToList(), SelectedIndex = 0 };
        target.SelectionChanged += (_, _) => keep.IsChecked = true;
        if (others.Count > 0)
            content.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0), Children = { keep, target } });
        buttons.Children.Add(Sized(Ui.Button("Скасувати", window.Close), true));
        buttons.Children.Add(Sized(Ui.Button("Видалити", () =>
        {
            var destination = keep.IsChecked == true && target.SelectedIndex >= 0 ? others[target.SelectedIndex] : null;
            window.Close();
            session.Change(() =>
            {
                if (destination is not null) destination.Shortcuts.AddRange(category.Shortcuts);
                list.Remove(category);
            }, "Категорію «" + category.Name + "» видалено", destination is null ? Ui.Plural(count, "ярлик прибрано", "ярлики прибрано", "ярликів прибрано") + " з панелі." : "Ярлики перенесено до «" + destination.Name + "».");
        }, "Button.Danger")));
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
        var (window, content, buttons) = Frame("Властивості ярлика", 520, 470);
        var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        header.Children.Add(Ui.AppIcon(shortcut.Name, shortcut.Target, 32));
        var title = Ui.Text(shortcut.Name, 17, "Text", bold: true); title.Margin = new Thickness(12, 0, 0, 0);
        header.Children.Add(title);
        content.Children.Add(header);

        var name = new TextBox { Text = shortcut.Name, MaxLength = 100 };
        var target = new TextBox { Text = shortcut.Target };
        var arguments = new TextBox { Text = shortcut.Arguments, Tag = "Необов’язково" };
        var directory = new TextBox { Text = shortcut.WorkingDirectory, Tag = "Типово — папка програми" };
        var admin = new CheckBox { Content = "Завжди запускати від імені адміністратора", IsChecked = shortcut.RunAsAdministrator, Margin = new Thickness(0, 16, 0, 0) };
        var browse = Ui.Button("Огляд…", () => { if (PickFile() is { } file) target.Text = file; });
        content.Children.Add(Field("Назва", name));
        content.Children.Add(Field("Ціль", target, browse));
        content.Children.Add(Field("Аргументи", arguments));
        content.Children.Add(Field("Робоча папка", directory));
        content.Children.Add(admin);

        buttons.Children.Add(Sized(Ui.Button("Скасувати", window.Close), true));
        buttons.Children.Add(Sized(Ui.Button("Зберегти", () =>
        {
            if (name.Text.Trim().Length == 0 || target.Text.Trim().Length == 0) { name.Focus(); return; }
            window.Close();
            IconCache.Forget(shortcut.Target);
            session.Change(() =>
            {
                shortcut.Name = name.Text.Trim(); shortcut.Target = target.Text.Trim().Trim('"');
                shortcut.Arguments = arguments.Text.Trim(); shortcut.WorkingDirectory = directory.Text.Trim().Trim('"');
                shortcut.RunAsAdministrator = admin.IsChecked == true;
            }, "Ярлик оновлено");
        }, "Button.Primary")));
        window.ShowDialog();
    }

    internal static string? PickFile()
    {
        var dialog = new OpenFileDialog { Title = "Виберіть програму або ярлик", Filter = "Програми та ярлики|*.exe;*.lnk;*.url|Усі файли|*.*", DereferenceLinks = false };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>Points a broken shortcut at a new file.</summary>
    internal static void Relocate(Session session, Shortcut shortcut)
    {
        if (PickFile() is not { } file) return;
        IconCache.Forget(shortcut.Target);
        session.Change(() => { shortcut.Target = file; shortcut.Arguments = ""; shortcut.WorkingDirectory = ""; }, "Ярлик «" + shortcut.Name + "» виправлено");
    }

    /// <summary>Asks for a web address or a Steam link.</summary>
    internal static Shortcut? PromptLink()
    {
        var (window, content, buttons) = Frame("Додати посилання", 480, 300);
        Shortcut? result = null;
        content.Children.Add(Heading("Вебпосилання або гра Steam"));
        var address = new TextBox { Tag = "https://…  або  steam://rungameid/…" };
        var name = new TextBox { Tag = "Як підписати в меню" };
        content.Children.Add(Field("Адреса", address));
        content.Children.Add(Field("Назва", name));
        void Accept()
        {
            var url = address.Text.Trim();
            if (!url.Contains("://")) url = "https://" + url;
            if (Shortcuts.FromPath(url) is not { } shortcut) { address.Focus(); address.SelectAll(); return; }
            if (name.Text.Trim().Length > 0) shortcut.Name = name.Text.Trim();
            result = shortcut;
            window.Close();
        }
        buttons.Children.Add(Sized(Ui.Button("Скасувати", window.Close), true));
        buttons.Children.Add(Sized(Ui.Button("Додати", Accept, "Button.Primary")));
        window.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; Accept(); } };
        window.Loaded += (_, _) => address.Focus();
        window.ShowDialog();
        return result;
    }
}
