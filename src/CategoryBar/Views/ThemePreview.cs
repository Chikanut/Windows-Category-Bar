namespace CategoryBar.Views;

/// <summary>Uses the theme's real templates and stylesheet in an isolated resource scope.</summary>
internal static class ThemePreview
{
    internal static FrameworkElement Create(ThemeDefinition theme, bool dark, Settings? settings = null)
    {
        var css = ThemeStyleSheet.Parse(theme.StyleSheet);
        if (settings is not null && settings.ThemeColors.TryGetValue(theme.Id, out var colors))
            css = ThemeStyleSheet.Parse(css.WithSurfaceColors(colors.Keys.Where(key => key.StartsWith(dark ? "Dark:" : "Light:", StringComparison.Ordinal)).Select(key => key[(key.IndexOf(':') + 1)..])));
        var palette = settings is null ? (dark ? theme.Dark : theme.Light) : Theme.Palette(settings, theme, dark);
        var scope = new StackPanel { Width = 270 };
        scope.Resources["Font"] = new FontFamily(theme.FontName);
        System.Windows.Documents.TextElement.SetFontFamily(scope, (FontFamily)scope.Resources["Font"]);
        var shared = new ResourceDictionary { Source = new Uri("/CategoryBar;component/Themes/ThemeSurfaces.xaml", UriKind.Relative) };
        scope.Resources.MergedDictionaries.Add(shared);
        scope.Resources.MergedDictionaries.Add(ThemeStyleResources.Templates(css.Skin));
        scope.Resources.MergedDictionaries.Add(Theming.ThemeResourceLoader.Load(theme));
        foreach (var (key, value) in palette) scope.Resources[key] = new SolidColorBrush(Theme.Parse(value));
        var active = settings is not null && theme.Id == settings.ThemeId;
        var accent = active ? Theme.Accent : Theme.Parse(palette["Accent"]);
        scope.Resources["Accent"] = new SolidColorBrush(accent);
        scope.Resources["Accent.Soft"] = new SolidColorBrush(Color.FromArgb(46, accent.R, accent.G, accent.B));
        scope.Resources["Hover"] = Brushes.Transparent;
        scope.Resources["Accent.Text"] = active ? Application.Current.Resources["Accent.Text"] : dark ? Brushes.Black : Brushes.White;
        scope.Resources["Glass.Rim"] = new SolidColorBrush(Colors.White) { Opacity = .4 };
        scope.Resources["Glass.Sheen"] = new LinearGradientBrush(Color.FromArgb(45, 255, 255, 255), Colors.Transparent, 90);
        ThemeStyleResources.Apply(scope.Resources, css);
        Theming.ThemeParameterResources.Apply(scope.Resources, theme, settings ?? new Settings());
        scope.Resources["Theme.ControlMaterialTemplate"] = theme.Material == "Glass" ? Application.Current.Resources["Theme.FlatMaterialTemplate"] : scope.Resources["Theme.MaterialTemplate"];
        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < 3; i++)
        {
            var label = new TextBlock { Text = i == 0 ? "Aa" : "•••", FontSize = css.Number("tab", "font-size", 13), VerticalAlignment = VerticalAlignment.Center };
            label.SetResourceReference(TextBlock.ForegroundProperty, i == 0 ? "Accent" : "Text.Muted");
            var tab = new ContentControl { Content = label }.Res(FrameworkElement.StyleProperty, "Theme.Tab")
                .Res(FrameworkElement.HeightProperty, "tab.Height").Res(FrameworkElement.MarginProperty, "tab.Margin");
            Theming.ThemeVisual.SetIsSelected(tab, i == 0);
            tabs.Children.Add(tab);
        }
        scope.Children.Add(new ContentControl { Content = tabs }.Res(FrameworkElement.StyleProperty, "Theme.Bar"));
        var button = Ui.Button("Aa", () => { }, "Button.Primary"); button.IsHitTestVisible = false;
        button.HorizontalAlignment = HorizontalAlignment.Left; button.MinWidth = 72;
        var row = new DockPanel(); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
        row.Children.Add(new TextBlock { Text = "Aa", VerticalAlignment = VerticalAlignment.Center }.Res(TextBlock.ForegroundProperty, "Text"));
        var card = new ContentControl { Content = row, Margin = new Thickness(0, 8, 0, 0) }.Res(FrameworkElement.StyleProperty, "Theme.Card"); scope.Children.Add(card);
        return new Viewbox { Child = scope, Stretch = Stretch.Uniform, Height = 90, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false };
    }
}
