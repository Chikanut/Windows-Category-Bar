// Liquid glass for a rounded rectangle. s0 is the material layer only (the shape filled with its tint), never text.
// s1 is the live picture of whatever lies behind the surface (the engine's "Theme.Backdrop" brush).
// The distance to the edge is computed from the element's size and corner radius, so the rim is a smooth
// bevel of any width: it bends the picture like a lens, fringes its colours a little and catches the light.
// c0 Specular: light on the rim.          c1 Refraction: how far, in pixels, the rim bends the backdrop.
// c2/c3: the element's size in pixels.    c4 Bevel: width of the rim in pixels.
// c5 Live: 1 when s1 holds a backdrop.    c6 Dispersion: colour fringing.    c7 Radius: corner radius in pixels.
sampler2D Input : register(s0);
sampler2D Backdrop : register(s1);
float Specular : register(c0);
float Refraction : register(c1);
float Width : register(c2);
float Height : register(c3);
float Bevel : register(c4);
float Live : register(c5);
float Dispersion : register(c6);
float Radius : register(c7);
float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 size = max(float2(Width, Height), 1);
    float2 p = (uv - 0.5) * size;
    float2 q = abs(p) - (size * 0.5 - Radius);
    float2 corner = max(q, 0);
    float reach = length(corner);
    // Depth below the edge, in pixels, and the direction out of the shape.
    float depth = Radius - reach - min(max(q.x, q.y), 0);
    float2 flat = q.x > q.y ? float2(1, 0) : float2(0, 1);
    float2 normal = (reach > 0 ? corner / reach : flat) * (p > 0 ? 1 : -1);
    float rim = saturate(1 - depth / Bevel);
    rim *= rim;

    float4 c = tex2D(Input, uv);
    // The tint's own strength is its alpha in the middle; what remains of this pixel's alpha is edge coverage.
    float strength = max(tex2D(Input, 0.5).a, 0.004);
    float coverage = saturate(c.a / strength);
    float2 bend = normal * rim * Refraction / size;
    float3 behind = float3(tex2D(Backdrop, uv - bend * (1 + Dispersion)).r, tex2D(Backdrop, uv - bend).g, tex2D(Backdrop, uv - bend * (1 - Dispersion)).b);
    float3 tint = c.rgb / max(c.a, 0.001);
    float3 rgb = lerp(tint, behind, Live * (1 - strength));
    // Without a picture behind it the sheet is made denser, so text stays readable over a busy desktop.
    float alpha = lerp(c.a, coverage, max(Live, 0.45));
    float facing = dot(normal, float2(-0.42, -0.9));
    rgb = saturate(rgb + max(facing, facing * -0.4) * rim * Specular);
    return float4(rgb * alpha, alpha);
}
