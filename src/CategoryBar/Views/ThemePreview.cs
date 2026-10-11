namespace CategoryBar.Views;

/// <summary>
/// A small live sample of a theme: its bar with tabs and a card with a button, drawn by the theme's real
/// templates, stylesheet and parameters inside a resource scope of its own.
/// </summary>
internal static class ThemePreview
{
    internal static FrameworkElement Create(ThemeDefinition theme, bool dark, Settings? settings = null)
    {
        // The selected theme is shown as tuned; any other one as it would look right after choosing it.
        var applied = new Settings();
        if (settings is not null) { applied.ThemeColors = settings.ThemeColors; applied.ThemeParameters = settings.ThemeParameters; applied.AccentColor = settings.AccentColor; }
        if (settings is not null && settings.ThemeId == theme.Id)
        {
            applied.AccentMode = settings.AccentMode; applied.TintSurfaces = settings.TintSurfaces;
            applied.CornerRadius = settings.CornerRadius; applied.SurfaceOpacity = settings.SurfaceOpacity; applied.FontName = settings.FontName;
        }
        else Theme.Select(applied, theme);

        var scope = new StackPanel { Width = 270 };
        // The shared templates first, so another theme's replacements at application level cannot leak in.
        foreach (var file in new[] { "ThemeControls", "ThemeSurfaces" })
            scope.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/CategoryBar;component/Themes/" + file + ".xaml", UriKind.Relative) });
        scope.Resources.MergedDictionaries.Add(Theming.ThemeResourceLoader.Load(theme));
        var sheet = Theme.Fill(scope.Resources, theme, applied, dark).Sheet;
        scope.Resources["Font"] = new FontFamily(applied.FontName + ", Segoe UI Variable Text, Segoe UI");
        System.Windows.Documents.TextElement.SetFontFamily(scope, (FontFamily)scope.Resources["Font"]);

        var tabs = new StackPanel { Orientation = Orientation.Horizontal };
        for (var i = 0; i < 3; i++)
        {
            var label = new TextBlock { Text = i == 0 ? "Aa" : "•••", VerticalAlignment = VerticalAlignment.Center }
                .Res(TextBlock.FontSizeProperty, "tab.FontSize").Res(TextBlock.FontWeightProperty, "tab.FontWeight");
            var tab = new ContentControl { Content = label }.Res(FrameworkElement.StyleProperty, "Theme.Tab")
                .Res(FrameworkElement.HeightProperty, "tab.Height").Res(FrameworkElement.MarginProperty, "tab.Margin");
            Theming.ThemeVisual.SetIsSelected(tab, i == 0);
            tabs.Children.Add(tab);
        }
        var bar = new ContentControl { Content = tabs, Height = sheet.Number("bar", "height", 40), HorizontalAlignment = HorizontalAlignment.Left }.Res(FrameworkElement.StyleProperty, "Theme.Bar");
        scope.Children.Add(new Border { Padding = new Thickness(6, 6, 6, 10), Child = bar });
        var button = Ui.Button("Aa", () => { }, "Button.Primary"); button.IsHitTestVisible = false; button.Focusable = false;
        button.HorizontalAlignment = HorizontalAlignment.Left; button.MinWidth = 72;
        var row = new DockPanel(); DockPanel.SetDock(button, Dock.Right); row.Children.Add(button);
        row.Children.Add(new TextBlock { Text = "Aa", VerticalAlignment = VerticalAlignment.Center }.Res(TextBlock.ForegroundProperty, "Text"));
        scope.Children.Add(new Border { Padding = new Thickness(6, 0, 6, 8), Child = new ContentControl { Content = row, Margin = new Thickness(0) }.Res(FrameworkElement.StyleProperty, "Theme.Card") });
        // The sample sits on the theme's own window colour, whatever theme the settings window currently wears.
        return new Border
        {
            Width = 300, Height = 116, HorizontalAlignment = HorizontalAlignment.Left, IsHitTestVisible = false, CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Theme.Parse(Theme.Palette(applied, theme, dark)["Window.Bg"])),
            Child = new Viewbox { Child = scope, Stretch = Stretch.Uniform, Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left }
        };
    }
}
