using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CategoryBar;

internal sealed class ToastWindow : Window
{
    private readonly DispatcherTimer lifetime = new();
    internal ToastWindow(string title, string message, string? actionLabel, Action? action, bool error)
    {
        Width = 340; SizeToContent = SizeToContent.Height; WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize; AllowsTransparency = true; Background = Brushes.Transparent;
        Topmost = true; ShowInTaskbar = false; ShowActivated = false;
        var root = new Grid(); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        root.ColumnDefinitions.Add(new ColumnDefinition()); root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        var glyph = new TextBlock { Text = error ? "!" : "✓", FontSize = 18, Foreground = BarWindow.Brush(error ? "#FF7A70" : "#8EA8FF"), Margin = new Thickness(0, 1, 0, 0) }; root.Children.Add(glyph);
        var text = new StackPanel(); text.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = message, FontSize = 12.5, Foreground = BarWindow.Brush("#A6A6B0"), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
        if (action is not null)
        {
            var button = new Button { Content = actionLabel ?? "Скасувати", Background = BarWindow.Brush("#18FFFFFF"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0), Height = 26 };
            button.Click += (_, _) => { Close(); action(); }; text.Children.Add(button);
        }
        Grid.SetColumn(text, 1); root.Children.Add(text);
        var close = new Button { Content = "✕", Padding = new Thickness(0), Width = 20, Height = 20, VerticalAlignment = VerticalAlignment.Top };
        close.Click += (_, _) => Close(); Grid.SetColumn(close, 2); root.Children.Add(close);
        Content = new Border { Background = BarWindow.Brush("#FA28282E"), BorderBrush = BarWindow.Brush("#18FFFFFF"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(14, 12, 14, 12), Child = root };
        lifetime.Interval = TimeSpan.FromSeconds(6); lifetime.Tick += (_, _) => Close();
        if (!error) lifetime.Start();
        MouseEnter += (_, _) => lifetime.Stop(); MouseLeave += (_, _) => { if (!error) lifetime.Start(); };
        Closed += (_, _) => lifetime.Stop();
    }
}
