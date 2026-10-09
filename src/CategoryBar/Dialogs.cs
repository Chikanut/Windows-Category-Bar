using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CategoryBar.Core;

namespace CategoryBar;

internal sealed class TextDialog : Window
{
    internal static string? Ask(string title, string initial)
    {
        var dialog = new TextDialog { Title = title, Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterScreen, Background = BarWindow.Brush("#2A2A30") };
        var panel = new StackPanel { Margin = new Thickness(22) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 18, Margin = new Thickness(0, 0, 0, 14) });
        var text = new TextBox { Text = initial, MaxLength = 100 };
        panel.Children.Add(text);
        var error = new TextBlock { Text = "Введіть непорожню назву", Foreground = BarWindow.Brush("#FF9A92"), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
        panel.Children.Add(error);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        var cancel = new Button { Content = "Скасувати", IsCancel = true, Height = 32 };
        var save = new Button { Content = "Зберегти", IsDefault = true, Height = 32, Background = BarWindow.Brush("#3D5BD9") };
        save.Click += (_, _) => { if (string.IsNullOrWhiteSpace(text.Text)) error.Visibility = Visibility.Visible; else dialog.DialogResult = true; };
        actions.Children.Add(cancel); actions.Children.Add(save); panel.Children.Add(actions); dialog.Content = panel;
        dialog.Loaded += (_, _) => { text.Focus(); text.SelectAll(); };
        return dialog.ShowDialog() == true ? text.Text.Trim() : null;
    }
}

internal sealed class SettingsWindow : Window
{
    internal SettingsWindow(Settings settings, Action save)
    {
        Title = "Панель категорій — Налаштування (прототип)"; Width = 560; SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = BarWindow.Brush("#202024");
        var body = new StackPanel { Margin = new Thickness(28) };
        body.Children.Add(new TextBlock { Text = "Панель і меню", FontSize = 22, Margin = new Thickness(0, 0, 0, 18) });
        var opening = SliderRow(body, "Затримка відкриття", 600, settings.OpenDelayMs);
        var closing = SliderRow(body, "Затримка закриття", 1000, settings.CloseDelayMs);
        var counts = new CheckBox { Content = "Показувати кількість ярликів", IsChecked = settings.ShowCounts, Foreground = BarWindow.Brush("#ECECEF"), Margin = new Thickness(0, 12, 0, 12) };
        body.Children.Add(counts);
        body.Children.Add(new TextBlock { Text = "Вирівнювання", Margin = new Thickness(0, 6, 0, 6) });
        var alignment = new ComboBox { ItemsSource = new[] { "Як у Windows", "Ліворуч", "По центру" }, SelectedIndex = settings.Alignment == "Left" ? 1 : settings.Alignment == "Center" ? 2 : 0 };
        body.Children.Add(alignment);
        body.Children.Add(new TextBlock { Text = "Пошук у меню", Margin = new Thickness(0, 16, 0, 6) });
        var search = new ComboBox { ItemsSource = new[] { "Від 15 ярликів", "Завжди", "Ніколи" }, SelectedIndex = settings.SearchThreshold == 0 ? 1 : settings.SearchThreshold == int.MaxValue ? 2 : 0 };
        body.Children.Add(search);
        body.Children.Add(new TextBlock { Text = "Прототип: основний монітор, перекриття вікон.\nПеревірка автоприховування й повного екрана ще триває.", Foreground = BarWindow.Brush("#8E8E98"), Margin = new Thickness(0, 20, 0, 20), TextWrapping = TextWrapping.Wrap });
        var apply = new Button { Content = "Зберегти", Height = 32, HorizontalAlignment = HorizontalAlignment.Right, Background = BarWindow.Brush("#3D5BD9") };
        apply.Click += (_, _) =>
        {
            settings.OpenDelayMs = (int)opening.Value; settings.CloseDelayMs = (int)closing.Value; settings.ShowCounts = counts.IsChecked == true;
            settings.Alignment = alignment.SelectedIndex == 1 ? "Left" : alignment.SelectedIndex == 2 ? "Center" : "Windows";
            settings.SearchThreshold = search.SelectedIndex == 1 ? 0 : search.SelectedIndex == 2 ? int.MaxValue : 15;
            save(); Close();
        };
        body.Children.Add(apply); Content = body;
    }

    private static Slider SliderRow(Panel body, string label, int maximum, int value)
    {
        var heading = new TextBlock { Text = $"{label}: {value} мс", Margin = new Thickness(0, 12, 0, 8) };
        var slider = new Slider { Minimum = 0, Maximum = maximum, Value = value, TickFrequency = 25, IsSnapToTickEnabled = true };
        slider.ValueChanged += (_, _) => heading.Text = $"{label}: {(int)slider.Value} мс";
        body.Children.Add(heading); body.Children.Add(slider); return slider;
    }
}
