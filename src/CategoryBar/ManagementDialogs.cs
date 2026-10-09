using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using CategoryBar.Core;

namespace CategoryBar;
internal sealed class DeleteCategoryWindow : Window
{
    private readonly ComboBox destinations = new() { DisplayMemberPath = "Name", Margin = new Thickness(0, 12, 0, 12) };
    internal Category? Destination => destinations.SelectedItem as Category;
    internal DeleteCategoryWindow(Category category, IEnumerable<Category> categories)
    {
        Title = "Видалити категорію"; Width = 400; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = BarWindow.Brush("#2A2A30");
        var root = new StackPanel { Margin = new Thickness(20) };
        root.Children.Add(new TextBlock { Text = $"Видалити «{category.Name}»?", FontSize = 18 });
        root.Children.Add(new TextBlock { Text = "Програми залишаться на комп’ютері. За бажанням перенесіть ярлики до іншої категорії.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        destinations.Items.Add("Прибрати ярлики з панелі"); foreach (var item in categories.Where(c => c != category)) destinations.Items.Add(item);
        destinations.SelectedIndex = 0; root.Children.Add(destinations);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(new Button { Content = "Скасувати", IsCancel = true });
        var remove = new Button { Content = "Видалити", Background = BarWindow.Brush("#60343C") }; remove.Click += (_, _) => DialogResult = true;
        actions.Children.Add(remove); root.Children.Add(actions); Content = root;
    }
}
internal sealed class ShortcutPropertiesWindow : Window
{
    private readonly TextBox name, target, arguments, directory;
    private readonly CheckBox administrator = new() { Content = "Запускати від імені адміністратора", Foreground = BarWindow.Brush("#ECECEF"), Margin = new Thickness(0, 12, 0, 16) };
    internal ShortcutPropertiesWindow(Shortcut shortcut)
    {
        Title = "Властивості ярлика"; Width = 520; SizeToContent = SizeToContent.Height; ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; Background = BarWindow.Brush("#2A2A30");
        var root = new StackPanel { Margin = new Thickness(20) };
        TextBox Field(string label, string value) { root.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0, 10, 0, 4) }); var input = new TextBox { Text = value, Height = 30 }; root.Children.Add(input); return input; }
        name = Field("Назва", shortcut.Name); target = Field("Файл, папка або посилання", shortcut.Target);
        arguments = Field("Аргументи запуску", shortcut.Arguments); directory = Field("Робоча папка", shortcut.WorkingDirectory);
        administrator.IsChecked = shortcut.RunAsAdministrator; root.Children.Add(administrator);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(new Button { Content = "Скасувати", IsCancel = true });
        var save = new Button { Content = "Зберегти", IsDefault = true }; save.Click += (_, _) => { if (name.Text.Trim().Length > 0 && LaunchTarget.Exists(target.Text.Trim())) DialogResult = true; else MessageBox.Show(this, "Вкажіть назву та наявний файл або правильне посилання."); };
        actions.Children.Add(save); root.Children.Add(actions); Content = root;
    }
    internal void Apply(Shortcut shortcut) { shortcut.Name = name.Text.Trim(); shortcut.Target = target.Text.Trim(); shortcut.Arguments = arguments.Text; shortcut.WorkingDirectory = directory.Text.Trim(); shortcut.RunAsAdministrator = administrator.IsChecked == true; }
}
