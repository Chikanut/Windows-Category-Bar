namespace CategoryBar;

/// <summary>Applies the theme's native CSS to actual WPF component styles and layout.</summary>
internal static class ThemeStyleResources
{
    internal static void Apply(ResourceDictionary resources, ThemeStyleSheet css, double radiusScale = 1)
    {
        resources["Typography.Emphasis"] = FontWeights.SemiBold;
        Thickness Spacing(string selector, string property, double fallback)
        {
            var v = css.Spacing(selector, property, fallback); return new Thickness(v[3], v[0], v[1], v[2]);
        }
        foreach (var selector in new[] { "window", "sidebar", "titlebar", "page", "toast", "bar", "tab", "menu", "menu-item", "button", "input", "card", "sidebar-item" })
        {
            resources[selector + ".Radius"] = new CornerRadius(Math.Clamp(css.Number(selector, "border-radius", 6) * radiusScale, 0, 32));
            resources[selector + ".Border"] = new Thickness(css.Number(selector, "border-width", 1));
            resources[selector + ".Padding"] = Spacing(selector, "padding", selector is "window" or "sidebar" or "titlebar" or "page" or "toast" ? 0 : 8);
            resources[selector + ".Margin"] = Spacing(selector, "margin", 0);
            resources[selector + ".Height"] = css.Number(selector, "height", selector == "titlebar" ? 40 : 36);
            resources[selector + ".MinHeight"] = css.Number(selector, "min-height", 30);
            resources[selector + ".FontSize"] = css.Number(selector, "font-size", 13);
            resources[selector + ".FontWeight"] = css.Value(selector, "font-weight", "normal") switch { "semibold" => FontWeights.SemiBold, "bold" => FontWeights.Bold, _ => FontWeights.Normal };
        }
        var tabMargin = Spacing("tab", "margin", 0);
        var gap = css.Number("bar", "gap", 0) / 2;
        resources["tab.Margin"] = new Thickness(tabMargin.Left + gap, tabMargin.Top, tabMargin.Right + gap, tabMargin.Bottom);
        var rowMargin = Spacing("menu-item", "margin", 0); var rowGap = css.Number("menu", "gap", 0) / 2;
        resources["menu-item.Margin"] = new Thickness(rowMargin.Left, rowMargin.Top + rowGap, rowMargin.Right, rowMargin.Bottom + rowGap);
        resources["tab.Indicator"] = css.Value("tab", "indicator", "underline");
        resources["menu.Width"] = css.Number("menu", "width", 264);
        resources["menu-item.IconSize"] = css.Number("menu-item", "icon-size", 24);
        resources["card.Margin"] = new Thickness(0, 0, 0, css.Number("card", "gap", 6));
        resources["Toggle.Radius"] = new CornerRadius(css.Skin == "midnight" ? 0 : 10);
        resources["Radius"] = resources["menu.Radius"];
        resources["Radius.Small"] = resources["button.Radius"];
        foreach (var suffix in new[] { "Radius", "Border", "Padding", "Margin", "Height", "MinHeight", "FontSize", "FontWeight" }) resources["button-primary." + suffix] = resources["button." + suffix];
        resources["button-primary.Radius"] = new CornerRadius(Math.Clamp(css.Number("button-primary", "border-radius", css.Number("button", "border-radius", 6)) * radiusScale, 0, 32));
        resources["button-primary.Border"] = new Thickness(css.Number("button-primary", "border-width", css.Number("button", "border-width", 0)));
        if (css.Value("button-primary", "padding", "").Length > 0) resources["button-primary.Padding"] = Spacing("button-primary", "padding", 8);
        resources["button-primary.FontSize"] = css.Number("button-primary", "font-size", css.Number("button", "font-size", 13));
        resources["button-primary.FontWeight"] = css.Value("button-primary", "font-weight", css.Value("button", "font-weight", "semibold")) switch { "bold" => FontWeights.Bold, "normal" => FontWeights.Normal, _ => FontWeights.SemiBold };
        ThemePaintResources.Apply(resources, css);
    }

    internal static ResourceDictionary Templates(string skin)
    {
        var result = new ResourceDictionary { Source = new Uri("/CategoryBar;component/Themes/Styles/" + skin + ".xaml", UriKind.Relative) };
        if (!result.Contains("Theme.PrimaryButtonTemplate")) result["Theme.PrimaryButtonTemplate"] = result["Theme.ButtonTemplate"];
        return result;
    }
}
