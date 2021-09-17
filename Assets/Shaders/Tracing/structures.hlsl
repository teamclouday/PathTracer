#ifndef TRACER_STRUCTURES
#define TRACER_STRUCTURES

struct Ray
{
    float3 origin;
    float3 dir;
    float3 energy;
};

struct BRDF
{
    float3 diffuse;
    float3 specular;
    float roughness;
    float fresnel;
};

struct HitInfo
{
    float dist;
    float3 pos;
    float3 norm;
    BRDF brdf;
};
#endif