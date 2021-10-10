#ifndef TRACER_RANDOM
#define TRACER_RANDOM

#include "global.hlsl"

//float rand()
//{
//    float res = frac(sin(dot(_PixelOffset + PixelCenter, float2(12.9898, 78.233))) * 43758.5453);
//    //_Seed += frac(sin(dot(res * _Seed, float2(12.9898, 78.233))) * 43758.5453);
//    _PixelOffset += res;
//    return res;
//}

// reference: https://www.shadertoy.com/view/wltcRS
void rng_initialize(float2 p, int frame)
{
    //white noise seed
    RandomSeed = uint4(p, frame, p.x + p.y);
    
    //blue noise seed
    //s1 = uvec4(frame, frame * 15843, frame * 31 + 4566, frame * 2345 + 58585);
}

void pcg4d(inout uint4 v)
{
    v = v * 1664525u + 1013904223u;
    v.x += v.y * v.w;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v.w += v.y * v.z;
    v = v ^ (v >> 16u);
    v.x += v.y * v.w;
    v.y += v.z * v.x;
    v.z += v.x * v.y;
    v.w += v.y * v.z;
}

float rand()
{
    pcg4d(RandomSeed);
    return float(RandomSeed.x) / float(0xffffffffu);
}

float2 rand2()
{
    pcg4d(RandomSeed);
    return float2(RandomSeed.xy) / float(0xffffffffu);
}

float3 rand3()
{
    pcg4d(RandomSeed);
    return float3(RandomSeed.xyz) / float(0xffffffffu);
}

float4 rand4()
{
    pcg4d(RandomSeed);
    return float4(RandomSeed) / float(0xffffffffu);
}

#endif