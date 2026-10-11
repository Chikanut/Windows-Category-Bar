// Category Bar liquid material. s0 is the material layer, never foreground text.
// P0/c0: refraction; P1/c1: rim strength. UV-space illumination stays resolution independent.
sampler2D Input : register(s0);
float Refraction : register(c0);
float Rim : register(c1);
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 p = uv * 2 - 1;
    float edge = saturate(max(abs(p.x), abs(p.y)));
    float2 bent = clamp(uv - p * pow(edge, 6) * Refraction, 0.002, 0.998);
    float4 source = tex2D(Input, bent);
    float4 mask = tex2D(Input, uv);
    float light = pow(edge, 18) * Rim;
    float sheen = pow(saturate(1 - abs(uv.y - (0.13 + uv.x * 0.28)) * 3), 4) * .08;
    source = lerp(mask, source, step(.01, source.a));
    float3 rgb = source.rgb / max(source.a, .001);
    rgb = saturate(rgb + light * float3(.70, .88, 1) + sheen);
    return float4(rgb * mask.a, mask.a);
}
