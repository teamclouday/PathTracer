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

Colors CreateColors(float3 baseColor, float3 emission, float metallic)
{
    const float alpha = 0.04;
    Colors colors;
    colors.albedo = lerp(baseColor * (1.0 - alpha), 0.0, metallic);
    colors.specular = lerp(alpha, baseColor, metallic);
    colors.emission = emission;
    return colors;
}

HitInfo CreateHitInfo()
{
    HitInfo hit;
    hit.dist = 1.#INF;
    hit.pos = 0.0;
    hit.norm = 0.0;
    hit.colors = CreateColors(0.0, 0.0, 0.0);
    hit.smoothness = 0.0;
    return hit;
}

// return true if it is backface
bool CullFace(float3 norm, float3 eye, float3 pos)
{
    return dot(norm, (eye - pos)) < 0.0;
}

// create new sample direction in a hemisphere
float3 SampleHemisphere1(float3 norm, float alpha = 0.0)
{
    float cosTheta = pow(rand(), 1.0 / (1.0 + alpha));
    float sinTheta = sqrt(max(0.0, 1.0 - cosTheta * cosTheta));
    float phi = 2.0 * PI * rand();
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

// reference: https://github.com/LWJGL/lwjgl3-demos/blob/main/res/org/lwjgl/demo/opengl/raytracing/randomCommon.glsl
float3 SampleHemisphere2(float3 norm)
{
    float angle = rand() * 2.0 * PI;
    float u = rand() * 2.0 - 1.0;
    float sqrtMinusU2 = sqrt(1.0 - u * u);
    float3 v = float3(sqrtMinusU2 * cos(angle), sqrtMinusU2 * sin(angle), u);
    return v * sign(dot(v, norm));
}

float3 SampleHemisphere3(float3 norm, float alpha = 0.0)
{
    float3 randomVec = float3(rand(), rand(), rand());
    float r = pow(randomVec.x, 1.0 / (1.0 + alpha));
    float angle = randomVec.y * 2.0 * PI;
    float sr = sqrt(1.0 - r * r);
    float3 ph = float3(sr * cos(angle), sr * sin(angle), r);
    float3 tangent = normalize(randomVec * 2.0 - 1.0);
    float3 bitangent = normalize(cross(tangent, norm));
    tangent = normalize(cross(bitangent, norm));
    return mul(ph, float3x3(tangent, bitangent, norm));
}

// reference: https://www.scratchapixel.com/lessons/3d-basic-rendering/introduction-to-shading/reflection-refraction-fresnel
float Fresnel(float3 dir, float3 norm, float ior)
{
    float cosi = clamp(dot(dir, norm), -1.0, 1.0);
    float etai, etat;
    if(cosi > 0.0)
    {
        etai = ior;
        etat = 1.0;
    }
    else
    {
        etai = 1.0;
        etat = ior;
    }
    float sint = etai / etat * sqrt(max(0.0, 1.0 - cosi * cosi));
    if(sint >= 1.0)
    {
        return 1.0;
    }
    else
    {
        float cost = sqrt(max(0.0, 1.0 - sint * sint));
        cosi = abs(cosi);
        float Rs = ((etat * cosi) - (etai * cost)) / ((etat * cosi) + (etai * cost));
        float Rp = ((etai * cosi) - (etat * cost)) / ((etai * cosi) + (etat * cost));
        return (Rs * Rs + Rp * Rp) / 2;
    }
}
#endif