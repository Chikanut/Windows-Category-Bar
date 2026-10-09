using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CategoryBar.Core;

namespace CategoryBar;

public partial class SettingsWindow : Window
{
    private readonly Configuration config;
    private readonly string configPath;
    private readonly Action save;
    private readonly DispatcherTimer saveTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private bool ready;
    private bool pendingSave;
    private readonly bool allowSystemChanges;

    internal SettingsWindow(Configuration config, string configPath, Action save, bool allowSystemChanges = true)
    {
        this.config = config; this.configPath = configPath; this.save = save;
        this.allowSystemChanges = allowSystemChanges;
        InitializeComponent();
        var settings = config.Settings;
        Opening.Value = settings.OpenDelayMs; ClosingDelay.Value = settings.CloseDelayMs;
        OpeningLabel.Text = settings.OpenDelayMs + " мс"; ClosingLabel.Text = settings.CloseDelayMs + " мс";
        Counts.IsChecked = settings.ShowCounts;
        (settings.Alignment == "Left" ? LeftAlignment : settings.Alignment == "Center" ? CenterAlignment : WindowsAlignment).IsChecked = true;
        (settings.SearchThreshold == 0 ? AlwaysSearch : settings.SearchThreshold == int.MaxValue ? NeverSearch : ThresholdSearch).IsChecked = true;
        Startup.IsChecked = allowSystemChanges && StartupRegistration.IsEnabled;
        Startup.IsEnabled = allowSystemChanges;
        ConfigLocation.Text = configPath;
        Shell.SizeChanged += (_, _) => Shell.Clip = new RectangleGeometry(new Rect(0, 0, Shell.ActualWidth, Shell.ActualHeight), 10, 10);
        saveTimer.Tick += (_, _) => TrySave();
        Closing += OnClosing;
        Closed += (_, _) => saveTimer.Stop();
        ready = true;
        PanelNavigation.IsChecked = true;
    }

    private void Navigate(object sender, RoutedEventArgs e)
    {
        if (!ready || sender is not RadioButton radio) return;
        SelectPage((string)radio.Tag);
    }

    internal void SelectPage(string page)
    {
        GeneralPage.Visibility = page == "General" ? Visibility.Visible : Visibility.Collapsed;
        PanelPage.Visibility = page == "Panel" ? Visibility.Visible : Visibility.Collapsed;
        CategoriesPage.Visibility = page == "Categories" ? Visibility.Visible : Visibility.Collapsed;
        AboutPage.Visibility = page == "About" ? Visibility.Visible : Visibility.Collapsed;
        if (page == "Categories") RefreshCategories();
        foreach (var control in LogicalTreeHelper.GetChildren((DependencyObject)PanelNavigation.Parent).OfType<RadioButton>())
            control.IsChecked = Equals(control.Tag, page);
    }

    private void DelayChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        config.Settings.OpenDelayMs = (int)Opening.Value;
        config.Settings.CloseDelayMs = (int)ClosingDelay.Value;
        OpeningLabel.Text = config.Settings.OpenDelayMs + " мс";
        ClosingLabel.Text = config.Settings.CloseDelayMs + " мс";
        pendingSave = true;
        saveTimer.Stop(); saveTimer.Start();
    }

    private void AlignmentChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        config.Settings.Alignment = (string)((RadioButton)sender).Tag;
        SaveNow();
    }
    private void SearchChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        config.Settings.SearchThreshold = int.Parse((string)((RadioButton)sender).Tag, CultureInfo.InvariantCulture);
        SaveNow();
    }
    private void CountsChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        config.Settings.ShowCounts = Counts.IsChecked == true;
        SaveNow();
    }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        if (!ready || !allowSystemChanges) return;
        try
        {
            StartupRegistration.SetEnabled(Startup.IsChecked == true, configPath);
            SaveError.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            ready = false; Startup.IsChecked = StartupRegistration.IsEnabled; ready = true;
            ShowError("Не вдалося змінити автозапуск. " + ex.Message);
        }
    }

    private void SaveNow() { pendingSave = true; TrySave(); }
    internal bool FlushChanges() => TrySave();
    private bool TrySave()
    {
        saveTimer.Stop();
        if (!pendingSave) return true;
        try
        {
            save(); pendingSave = false; SaveError.Visibility = Visibility.Collapsed;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            ShowError("Не вдалося зберегти зміни. " + ex.Message);
            return false;
        }
    }

    private void ShowError(string message) { SaveError.Text = message; SaveError.Visibility = Visibility.Visible; }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!TrySave()) e.Cancel = true; }
    private void TitleDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
    private void Minimize(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void CloseSettings(object sender, RoutedEventArgs e) => Close();

    private void OpenConfigFolder(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.GetDirectoryName(configPath)!;
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", folder) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        { ShowError("Не вдалося відкрити папку. " + ex.Message); }
    }

    private void RefreshCategories()
    {
        CategoryRows.Children.Clear();
        if (config.Categories.Count == 0)
        {
            CategoryRows.Children.Add(new TextBlock
            {
                Text = "Категорій поки немає. Створіть першу, щоб додати застосунки.",
                Foreground = BarWindow.Brush("#8E8E98"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 18)
            });
            return;
        }
        foreach (var category in config.Categories)
        {
            var card = new Border { Style = (Style)FindResource("SettingsCard") };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var labels = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            labels.Children.Add(new TextBlock { Text = category.Name, TextTrimming = TextTrimming.CharacterEllipsis, Style = (Style)FindResource("SettingsText") });
            labels.Children.Add(new TextBlock { Text = $"Ярликів: {category.Shortcuts.Count}", Style = (Style)FindResource("SettingsHint") });
            grid.Children.Add(labels);
            var rename = new Button { Content = "Перейменувати", Background = BarWindow.Brush("#18FFFFFF"), VerticalAlignment = VerticalAlignment.Center };
            rename.Click += (_, _) =>
            {
                var name = TextDialog.Ask("Назва категорії", category.Name);
                if (name is null) return;
                category.Name = name; SaveNow(); RefreshCategories();
            };
            Grid.SetColumn(rename, 1); grid.Children.Add(rename);
            card.Child = grid; CategoryRows.Children.Add(card);
        }
    }
    private void CreateCategory(object sender, RoutedEventArgs e)
    {
        var name = TextDialog.Ask("Створити категорію", "");
        if (name is null) return;
        config.Categories.Add(new Category { Name = name }); SaveNow(); RefreshCategories();
    }
}
