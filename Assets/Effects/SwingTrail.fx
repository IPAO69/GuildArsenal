float4x4 uWorldViewProjection;
float3 uColor;
float uOpacity;
float uTime;
float uEdgeSharpness;
float uThrust;

struct VertexInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

struct VertexOutput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 UV : TEXCOORD0;
};

VertexOutput TrailVertex(VertexInput input)
{
    VertexOutput output;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Color = input.Color;
    output.UV = input.UV;
    return output;
}

float4 TrailPixel(VertexOutput input) : COLOR0
{
    float age = saturate(input.UV.x);
    float radius = saturate(input.UV.y);
    if (uThrust > 0.5)
    {
        // Solid, bright triangular heads; smooth narrow tails without the slash's animated stripes.
        float across = abs(radius * 2.0 - 1.0);
        float head = 1.0 - smoothstep(0.16, 0.24, age);
        float headMask = 1.0 - smoothstep(0.94, 1.0, across);
        float bodyMask = pow(saturate(1.0 - across), 0.7);
        float mask = lerp(bodyMask, headMask, head);
        float core = pow(saturate(1.0 - across), 5.0);
        float fade = 1.0 - smoothstep(0.25, 1.0, age);
        float3 color = lerp(uColor, float3(0.9, 1.0, 0.95), saturate(head * 0.65 + core * 0.35));
        float alpha = mask * fade * (0.8 + core * 0.2) * uOpacity * input.Color.a;
        return float4(color * input.Color.rgb, saturate(alpha));
    }
    float tail = pow(1.0 - age, 1.6);
    float innerFade = smoothstep(0.0, 0.28, radius);
    float outerFade = 1.0 - smoothstep(0.965, 1.0, radius);
    float edge = exp(-pow((radius - 0.92) * uEdgeSharpness, 2.0));
    float streak = 0.82 + 0.18 * sin(radius * 52.0 - age * 9.0 + uTime * 5.0);
    float mask = innerFade * outerFade;
    float3 color = lerp(uColor * (0.55 + radius * 0.45), float3(0.9, 1.0, 0.95), edge);
    float alpha = tail * mask * (0.5 * streak + edge * 0.5) * uOpacity * input.Color.a;
    return float4(color * input.Color.rgb, saturate(alpha));
}

technique SwingTrail
{
    pass TrailPass
    {
        VertexShader = compile vs_3_0 TrailVertex();
        PixelShader = compile ps_3_0 TrailPixel();
    }
}
