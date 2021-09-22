#ifndef TRACER_STRUCTURES
#define TRACER_STRUCTURES

struct Ray
{
    float3 origin;
    float3 dir;
    float3 energy;
};

struct Colors
{
    float3 albedo;
    float3 specular;
    float3 emission;
};

struct HitInfo
{
    float dist;
    float3 pos;
    float3 norm;
    Colors colors;
    float smoothness;
    float mode;
};
#endif