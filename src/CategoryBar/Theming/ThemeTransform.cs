namespace CategoryBar.Theming;

/// <summary>
/// Animatable placement for theme templates and transitions: move, scale and turn any element without
/// writing its transform by hand, e.g. <c>Storyboard.TargetProperty="(fx:ThemeTransform.Scale)"</c>.
/// </summary>
public static class ThemeTransform
{
    public static readonly DependencyProperty XProperty = Register("X", 0);
    public static readonly DependencyProperty YProperty = Register("Y", 0);
    public static readonly DependencyProperty ScaleProperty = Register("Scale", 1);
    public static readonly DependencyProperty ScaleXProperty = Register("ScaleX", 1);
    public static readonly DependencyProperty ScaleYProperty = Register("ScaleY", 1);
    public static readonly DependencyProperty AngleProperty = Register("Angle", 0);
    /// <summary>The fixed point of scaling and turning, in fractions of the element: "0.5,1" is the middle of its bottom edge.</summary>
    public static readonly DependencyProperty OriginProperty = DependencyProperty.RegisterAttached("Origin", typeof(Point), typeof(ThemeTransform), new PropertyMetadata(new Point(.5, .5), Changed));
    private static readonly DependencyProperty OwnedProperty = DependencyProperty.RegisterAttached("Owned", typeof(TransformGroup), typeof(ThemeTransform));

    private static DependencyProperty Register(string name, double initial) =>
        DependencyProperty.RegisterAttached(name, typeof(double), typeof(ThemeTransform), new PropertyMetadata(initial, Changed));

    public static double GetX(DependencyObject o) => (double)o.GetValue(XProperty);
    public static void SetX(DependencyObject o, double value) => o.SetValue(XProperty, value);
    public static double GetY(DependencyObject o) => (double)o.GetValue(YProperty);
    public static void SetY(DependencyObject o, double value) => o.SetValue(YProperty, value);
    public static double GetScale(DependencyObject o) => (double)o.GetValue(ScaleProperty);
    public static void SetScale(DependencyObject o, double value) => o.SetValue(ScaleProperty, value);
    public static double GetScaleX(DependencyObject o) => (double)o.GetValue(ScaleXProperty);
    public static void SetScaleX(DependencyObject o, double value) => o.SetValue(ScaleXProperty, value);
    public static double GetScaleY(DependencyObject o) => (double)o.GetValue(ScaleYProperty);
    public static void SetScaleY(DependencyObject o, double value) => o.SetValue(ScaleYProperty, value);
    public static double GetAngle(DependencyObject o) => (double)o.GetValue(AngleProperty);
    public static void SetAngle(DependencyObject o, double value) => o.SetValue(AngleProperty, value);
    public static Point GetOrigin(DependencyObject o) => (Point)o.GetValue(OriginProperty);
    public static void SetOrigin(DependencyObject o, Point value) => o.SetValue(OriginProperty, value);

    private static void Changed(DependencyObject o, DependencyPropertyChangedEventArgs e)
    {
        if (o is not UIElement element) return;
        if (element.GetValue(OwnedProperty) is not TransformGroup group || !ReferenceEquals(element.RenderTransform, group))
        {
            group = new TransformGroup { Children = { new ScaleTransform(), new RotateTransform(), new TranslateTransform() } };
            element.SetValue(OwnedProperty, group);
            element.RenderTransform = group;
        }
        element.RenderTransformOrigin = GetOrigin(o);
        var scale = (ScaleTransform)group.Children[0]; var uniform = GetScale(o);
        scale.ScaleX = uniform * GetScaleX(o); scale.ScaleY = uniform * GetScaleY(o);
        ((RotateTransform)group.Children[1]).Angle = GetAngle(o);
        var move = (TranslateTransform)group.Children[2];
        move.X = GetX(o); move.Y = GetY(o);
    }
}
