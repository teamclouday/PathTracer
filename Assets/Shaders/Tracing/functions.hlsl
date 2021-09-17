#ifndef TRACER_FUNCTIONS
#define TRACER_FUNCTIONS

#include "global.hlsl"
#include "structures.hlsl"
#include "random.hlsl"

Ray CreateRay(float3 origin, float3 direction)
{
    Ray ray;
    ray.origin = origin;
    ray.dir = direction;
    ray.energy = 1.0;
    return ray;
}

Ray CreateCameraRay(float2 uv)
{
    float3 origin = mul(_CameraToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
    float3 direction = mul(_CameraProjInv, float4(uv, 0.0, 1.0)).xyz;
    direction = mul(_CameraToWorld, float4(direction, 0.0)).xyz;
    direction = normalize(direction);
    return CreateRay(origin, direction);
}

BRDF CreateBRDF(float3 color, float roughness, float metallic)
{
    const float dielectricSpecular = 0.04;
    BRDF brdf;
    brdf.diffuse = lerp(color * (1.0 - dielectricSpecular), 0.0, metallic);
    brdf.specular = lerp(dielectricSpecular, color, metallic);
    brdf.roughness = roughness * roughness;
    brdf.fresnel = saturate(1.0 - roughness + dielectricSpecular);
    return brdf;
}

HitInfo CreateHitInfo()
{
    HitInfo hit;
    hit.dist = 1.#INF;
    hit.pos = 0.0;
    hit.norm = 0.0;
    hit.brdf = CreateBRDF(0.0, 0.0, 0.0);
    return hit;
}

// return true if it is backface
bool CullFace(float3 norm, float3 eye, float3 pos)
{
    return dot(norm, (eye - pos)) < 0.0;
}

// create new sample direction in a hemisphere
float3 SampleHemiSphere(float3 norm, float alpha=1.0)
{
    float cosTheta = pow(rand(), 1.0 / (1.0 + alpha));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    float phi = 2 * PI * rand();
    float3 tangentSpaceDir = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
    // from tangent space to world space
    float3 helper = float3(1, 0, 0);
    if (abs(norm.x) > 0.99)
        helper = float3(0, 0, 1);
    float3 tangent = normalize(cross(norm, helper));
    float3 binormal = normalize(cross(norm, tangent));
    // get new direction
    return mul(tangentSpaceDir, float3x3(tangent, binormal, norm));
}
#endif