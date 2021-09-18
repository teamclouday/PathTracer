#ifndef TRACER_RANDOM
#define TRACER_RANDOM

#include "global.hlsl"

float rand()
{
    float res = frac(sin(dot(PixelCenter * _Seed, float2(12.9898, 78.233))) * 43758.5453);
    _Seed += res;
    return res;
}

#endif