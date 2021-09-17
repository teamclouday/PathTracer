#ifndef TRACER_RANDOM
#define TRACER_RANDOM

#include "global.hlsl"

float rand()
{
    float result = frac(sin(_Seed * 0.01 * dot(PixelCenter, float2(12.9898, 78.233))) * 43758.5453);
    _Seed += 1.0;
    return result;
}

#endif