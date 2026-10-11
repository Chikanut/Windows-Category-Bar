using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace CategoryBar.Core;

/// <summary>Only visual WPF types are accepted. No code, providers, arbitrary CLR types or URI loading.</summary>
public static class ThemeMarkup
{
    public const int MaximumMarkupBytes = 256 * 1024;
    public const int MaximumShaderBytes = 64 * 1024;
    public const string Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    public const string Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    public const string Effects = "clr-namespace:CategoryBar.Theming;assembly=CategoryBar";
    private static readonly HashSet<string> Types = new(("ResourceDictionary Style Setter Trigger MultiTrigger DataTrigger MultiDataTrigger Condition EventTrigger " +
        "ControlTemplate DataTemplate ItemsPanelTemplate FrameworkElement Grid Border StackPanel WrapPanel DockPanel Canvas UniformGrid Viewbox ContentPresenter ContentControl " +
        "ScrollViewer TextBlock Run Span Bold Italic Underline Button ToggleButton CheckBox RadioButton TextBox ComboBox ComboBoxItem ListBox ListBoxItem ItemsControl " +
        "Rectangle Ellipse Path Polygon Polyline Line GeometryGroup PathGeometry PathFigure LineSegment PolyLineSegment BezierSegment PolyBezierSegment QuadraticBezierSegment ArcSegment RectangleGeometry EllipseGeometry CombinedGeometry " +
        "SolidColorBrush LinearGradientBrush RadialGradientBrush GradientStop DrawingBrush DrawingGroup GeometryDrawing Pen VisualBrush " +
        "ScaleTransform TranslateTransform RotateTransform SkewTransform TransformGroup MatrixTransform DropShadowEffect BlurEffect " +
        "Storyboard BeginStoryboard StopStoryboard RemoveStoryboard PauseStoryboard ResumeStoryboard DoubleAnimation ColorAnimation PointAnimation ThicknessAnimation " +
        "DoubleAnimationUsingKeyFrames ColorAnimationUsingKeyFrames PointAnimationUsingKeyFrames ThicknessAnimationUsingKeyFrames ObjectAnimationUsingKeyFrames " +
        "DiscreteDoubleKeyFrame LinearDoubleKeyFrame SplineDoubleKeyFrame EasingDoubleKeyFrame DiscreteColorKeyFrame LinearColorKeyFrame SplineColorKeyFrame EasingColorKeyFrame " +
        "DiscreteObjectKeyFrame LinearPointKeyFrame SplinePointKeyFrame EasingPointKeyFrame LinearThicknessKeyFrame SplineThicknessKeyFrame EasingThicknessKeyFrame " +
        "CubicEase QuadraticEase QuarticEase QuinticEase SineEase CircleEase ExponentialEase BackEase BounceEase ElasticEase PowerEase KeySpline " +
        "VisualStateManager VisualStateGroup VisualState VisualTransition Binding MultiBinding PriorityBinding RelativeSource ColumnDefinition RowDefinition " +
        "Color Thickness CornerRadius Point Size Double Boolean String FontFamily").Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
    public static bool IsRelativeFile(string value, string extension, bool allowEmpty = false) => value is not null && ((allowEmpty && value.Length == 0) ||
        value.Length is > 0 and <= 180 && value.EndsWith(extension, StringComparison.OrdinalIgnoreCase) && !Path.IsPathRooted(value) && !value.Contains(':') && !value.Contains('\\') && value.Split('/').All(p => p.Length > 0 && p is not ("." or "..")));
    public static void ValidateShader(byte[] bytes)
    {
        if (bytes.Length is < 8 or > MaximumShaderBytes || bytes.Length % 4 != 0 || (BitConverter.ToUInt32(bytes, 0) != 0xFFFF0200 && BitConverter.ToUInt32(bytes, 0) != 0xFFFF0300) || BitConverter.ToUInt32(bytes, bytes.Length - 4) != 0x0000FFFF)
            throw new InvalidDataException("Expected compiled WPF ps_2_0 or ps_3_0 shader bytecode.");
    }
    public static void Validate(string markup)
    {
        if (markup is null || Encoding.UTF8.GetByteCount(markup) > MaximumMarkupBytes) throw new InvalidDataException("Theme markup is too large.");
        if (markup.Length == 0) return;
        try
        {
            using var reader = XmlReader.Create(new StringReader(markup), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumMarkupBytes });
            var document = XDocument.Load(reader);
            if (document.Root?.Name != XName.Get("ResourceDictionary", Presentation)) throw new InvalidDataException("Theme resources must be a ResourceDictionary.");
            foreach (var element in document.Descendants())
            {
                var type = element.Name.LocalName.Split('.')[0];
                var member = element.Name.LocalName.Split('.').Last();
                if (member is "Source" or "UriSource" or "SourceUri" or "Command" or "DataContext" or "Converter") throw new InvalidDataException("Unsupported theme property: " + member);
                if (!(element.Name.NamespaceName == Presentation && Types.Contains(type)) && !(element.Name.NamespaceName == Effects && type is "ThemeShaderEffect" or "ThemeSurface" or "ThemeVisual")) throw new InvalidDataException("Unsupported theme type: " + element.Name);
                foreach (var attribute in element.Attributes())
                {
                    var name = attribute.Name.LocalName;
                    if (attribute.IsNamespaceDeclaration)
                    {
                        if (attribute.Value is not (Presentation or Xaml or Effects)) throw new InvalidDataException("Unsupported theme namespace.");
                        continue;
                    }
                    if (attribute.Name.NamespaceName == Xaml && name is not ("Key" or "Name")) throw new InvalidDataException("Executable XAML directives are not allowed.");
                    if (name is "Source" or "UriSource" or "SourceUri" or "Command" or "CommandParameter" or "Converter" or "ConverterParameter" or "DataContext" or "BindingGroup" or "ContextMenu" || name.EndsWith("Click", StringComparison.Ordinal)) throw new InvalidDataException("Unsupported theme property: " + name);
                    foreach (Match extension in Regex.Matches(attribute.Value, @"\{([\w:]+)"))
                        if (extension.Groups[1].Value is not ("StaticResource" or "DynamicResource" or "TemplateBinding" or "Binding" or "RelativeSource" or "x:Type" or "x:Null")) throw new InvalidDataException("Unsupported markup extension.");
                    if (attribute.Value.Contains("http:", StringComparison.OrdinalIgnoreCase) || attribute.Value.Contains("https:", StringComparison.OrdinalIgnoreCase) || attribute.Value.Contains("file:", StringComparison.OrdinalIgnoreCase) || attribute.Value.Contains("pack:", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("External resources are not allowed.");
                }
            }
        }
        catch (XmlException ex) { throw new InvalidDataException("Invalid theme resource markup.", ex); }
    }
}
