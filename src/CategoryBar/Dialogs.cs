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
