#ifndef TRACER_STRUCTURES
#define TRACER_STRUCTURES

struct Camera
{
    float fov_scale;
    float focalDist;
    float aperture;
    float ratio;
    float2 offset;
    float3 forward;
    float3 right;
    float3 up;
    float3 pos;
};

struct Ray
{
    float3 origin;
    float3 dir;
    float3 energy;
};

struct Material
{
    float3 albedo;
    float3 emission;
    float roughness;
    float metallic;
    float alpha;
    float ior;
};

struct HitInfo
{
    float dist;
    float3 pos;
    float3 norm;
    Material mat;
    float mode;
};
#endif