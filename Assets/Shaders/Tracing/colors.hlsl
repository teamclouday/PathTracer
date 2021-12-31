#ifndef TRACER_COLORS
#define TRACER_COLORS

float3 SRGB2Linear(float3 srgbIn)
{
    bool3 cutoff = srgbIn < 0.04045;
    float3 higher = pow(abs((srgbIn + 0.055) / 1.055), 2.4);
    float3 lower = srgbIn / 12.92;
    return lerp(higher, lower, cutoff);
}

float3 Linear2SRGB(float3 linearIn)
{
    bool3 cutoff = linearIn < 0.0031308;
    float3 higher = 1.055 * pow(abs(linearIn), 1.0 / 2.4) - 0.055;
    float3 lower = linearIn * 12.92;
    return lerp(higher, lower, cutoff);
}

float4 SRGB2Linear(float4 srgbIn)
{
    return float4(SRGB2Linear(srgbIn.rgb), srgbIn.a);
}

float4 Linear2SRGB(float4 linearIn)
{
    return float4(Linear2SRGB(linearIn.rgb), linearIn.a);
}

#endif