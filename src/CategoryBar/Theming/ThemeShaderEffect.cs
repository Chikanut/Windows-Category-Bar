using System.Windows.Media.Effects;

namespace CategoryBar.Theming;

/// <summary>File-defined GPU effect. Sampler s0 is the element; P0..P7 map to float constants c0..c7.</summary>
public sealed class ThemeShaderEffect : ShaderEffect
{
    internal static Dictionary<string, PixelShader> Available { get; } = new();
    public static readonly DependencyProperty ShaderProperty = DependencyProperty.Register(nameof(Shader), typeof(string), typeof(ThemeShaderEffect), new PropertyMetadata("", ShaderChanged));
    public string Shader { get => (string)GetValue(ShaderProperty); set => SetValue(ShaderProperty, value); }
    public static readonly DependencyProperty InputProperty = RegisterPixelShaderSamplerProperty(nameof(Input), typeof(ThemeShaderEffect), 0);
    public Brush Input { get => (Brush)GetValue(InputProperty); set => SetValue(InputProperty, value); }
    public static readonly DependencyProperty AuxiliaryProperty = RegisterPixelShaderSamplerProperty(nameof(Auxiliary), typeof(ThemeShaderEffect), 1);
    public Brush Auxiliary { get => (Brush)GetValue(AuxiliaryProperty); set => SetValue(AuxiliaryProperty, value); }
    public static readonly DependencyProperty P0Property = Constant(nameof(P0), 0);
    public static readonly DependencyProperty P1Property = Constant(nameof(P1), 1);
    public static readonly DependencyProperty P2Property = Constant(nameof(P2), 2);
    public static readonly DependencyProperty P3Property = Constant(nameof(P3), 3);
    public static readonly DependencyProperty P4Property = Constant(nameof(P4), 4);
    public static readonly DependencyProperty P5Property = Constant(nameof(P5), 5);
    public static readonly DependencyProperty P6Property = Constant(nameof(P6), 6);
    public static readonly DependencyProperty P7Property = Constant(nameof(P7), 7);
    public double P0 { get => (double)GetValue(P0Property); set => SetValue(P0Property, value); }
    public double P1 { get => (double)GetValue(P1Property); set => SetValue(P1Property, value); }
    public double P2 { get => (double)GetValue(P2Property); set => SetValue(P2Property, value); }
    public double P3 { get => (double)GetValue(P3Property); set => SetValue(P3Property, value); }
    public double P4 { get => (double)GetValue(P4Property); set => SetValue(P4Property, value); }
    public double P5 { get => (double)GetValue(P5Property); set => SetValue(P5Property, value); }
    public double P6 { get => (double)GetValue(P6Property); set => SetValue(P6Property, value); }
    public double P7 { get => (double)GetValue(P7Property); set => SetValue(P7Property, value); }
    private static DependencyProperty Constant(string name, int register) => DependencyProperty.Register(name, typeof(double), typeof(ThemeShaderEffect), new UIPropertyMetadata(0d, PixelShaderConstantCallback(register)));
    public ThemeShaderEffect()
    {
        UpdateShaderValue(InputProperty); UpdateShaderValue(AuxiliaryProperty);
        foreach (var property in new[] { P0Property, P1Property, P2Property, P3Property, P4Property, P5Property, P6Property, P7Property }) UpdateShaderValue(property);
    }
    private static void ShaderChanged(DependencyObject obj, DependencyPropertyChangedEventArgs args)
    {
        var effect = (ThemeShaderEffect)obj;
        if (!Available.TryGetValue((string)args.NewValue, out var shader)) throw new InvalidOperationException("Shader is not included in this theme: " + args.NewValue);
        effect.PixelShader = shader;
        effect.UpdateShaderValue(InputProperty); effect.UpdateShaderValue(AuxiliaryProperty);
        foreach (var property in new[] { P0Property, P1Property, P2Property, P3Property, P4Property, P5Property, P6Property, P7Property }) effect.UpdateShaderValue(property);
    }
    protected override Freezable CreateInstanceCore() => new ThemeShaderEffect();
}
