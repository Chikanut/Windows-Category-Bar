// Input is premultiplied RGBA. c0 may be animated by a WPF Storyboard.
sampler2D input : register(s0);
float time : register(c0);
float strength : register(c1);
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float4 color = tex2D(input, uv);
    float wave = 0.5 + 0.5 * sin(uv.x * 7.0 + uv.y * 3.0 + time);
    color.rgb = saturate(color.rgb + float3(0.08, 0.16, 0.25) * wave * strength * color.a);
    return color;
}
