namespace CategoryBar.Theming;

/// <summary>Visual slots only. Templates may rearrange Header and Content without owning application actions.</summary>
public sealed class ThemeSurface : HeaderedContentControl
{
    public ThemeSurface()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Focusable = false;
    }
}

/// <summary>Semantic state shared with author templates; business code never paints these states.</summary>
public static class ThemeVisual
{
    public static readonly DependencyProperty IsSelectedProperty = State("IsSelected");
    public static readonly DependencyProperty IsRunningProperty = State("IsRunning");
    public static readonly DependencyProperty IsDropTargetProperty = State("IsDropTarget");
    private static DependencyProperty State(string name) => DependencyProperty.RegisterAttached(name, typeof(bool), typeof(ThemeVisual), new FrameworkPropertyMetadata(false));
    public static bool GetIsSelected(DependencyObject obj) => (bool)obj.GetValue(IsSelectedProperty);
    public static void SetIsSelected(DependencyObject obj, bool value) => obj.SetValue(IsSelectedProperty, value);
    public static bool GetIsRunning(DependencyObject obj) => (bool)obj.GetValue(IsRunningProperty);
    public static void SetIsRunning(DependencyObject obj, bool value) => obj.SetValue(IsRunningProperty, value);
    public static bool GetIsDropTarget(DependencyObject obj) => (bool)obj.GetValue(IsDropTargetProperty);
    public static void SetIsDropTarget(DependencyObject obj, bool value) => obj.SetValue(IsDropTargetProperty, value);
}
