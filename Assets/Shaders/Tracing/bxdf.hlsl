#ifndef TRACER_BXDF
#define TRACER_BXDF

#include "global.hlsl"
#include "structures.hlsl"

// references:
// https://media.disneyanimation.com/uploads/production/publication_asset/48/asset/s2012_pbs_disney_brdf_notes_v3.pdf
// https://blog.selfshadow.com/publications/s2015-shading-course/burley/s2015_pbs_disney_bsdf_notes.pdf
// https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/common/disney.glsl
// https://github.com/HummaWhite/ZillumGL/blob/main/src/shader/material.shader
// https://typhomnt.github.io/teaching/ray_tracing/pbr_intro/

float DistributionGGX(float3 N, float3 H, float a)
{
    float a2 = a * a;
    float NdotH = dot(N, H);
    float t = NdotH * NdotH * (a2 - 1.0) + 1.0;
    return a2 / (PI * t * t);
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r * r) / 8.0;

    float nom = NdotV;
    float denom = NdotV * (1.0 - k) + k;

    return nom / denom;
}

float GeometrySmith(float3 N, float3 V, float3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2 = GeometrySchlickGGX(NdotV, roughness);
    float ggx1 = GeometrySchlickGGX(NdotL, roughness);

    return ggx1 * ggx2;
}

float3 SchlickFresnel(float cosTheta, float3 F0)
{
    //return F0 + (1.0 - F0) * pow(abs(1.0 - cosTheta), 5.0);
    return lerp(F0, 1.0, pow(abs(1.0 - cosTheta), 5.0));
}

float SmithG(float NDotV, float alphaG)
{
    float a = alphaG * alphaG;
    float b = NDotV * NDotV;
    return (2.0 * NDotV) / (NDotV + sqrt(a + b - a * b));
}


void DiffuseModel(HitInfo hit, float3 V, float3 L, float3 H, inout float3 energy)
{
    float NdotL = dot(hit.norm, L);
    float NdotV = dot(hit.norm, -V);
    float fd90 = 0.5 + 2.0 * hit.mat.roughness * dot(L, H) * dot(L, H);
    float fd = lerp(1.0, fd90, NdotL) * lerp(1.0, fd90, NdotV);
    energy *= (1.0 - hit.mat.metallic) * fd * hit.mat.albedo;
}

void SpecReflModel(HitInfo hit, float3 V, float3 L, float3 H, inout float3 energy)
{
    float NdotL = abs(dot(hit.norm, L));
    //float NdotV = abs(dot(hit.norm, -V));
    float3 specColor = lerp(0.04, hit.mat.albedo, hit.mat.metallic);
    float3 F = SchlickFresnel(dot(L, H), specColor);
    //float D = DistributionGGX(hit.norm, H, hit.mat.roughness);
    //float G = GeometrySmith(hit.norm, -V, L, hit.mat.roughness);
    float G = SmithG(NdotL, hit.mat.roughness);
    energy *= F * G;
}



// refer to: https://github.com/HummaWhite/ZillumGL/blob/main/src/shader/material.shader
float DielectricFresnel(float cosTi, float eta)
{
    cosTi = clamp(cosTi, -1.0, 1.0);
    if (cosTi < 0.0)
    {
        eta = 1.0 / eta;
        cosTi = -cosTi;
    }

    float sinTi = sqrt(1.0 - cosTi * cosTi);
    float sinTt = sinTi / eta;
    if (sinTt >= 1.0)
        return 1.0;

    float cosTt = sqrt(1.0 - sinTt * sinTt);

    float rPa = (cosTi - eta * cosTt) / (cosTi + eta * cosTt);
    float rPe = (eta * cosTi - cosTt) / (eta * cosTi + cosTt);
    return (rPa * rPa + rPe * rPe) * 0.5;
}

void SpecRefrModel(HitInfo hit, float3 V, float3 L, float3 H, inout float3 energy)
{
    float NdotL = abs(dot(hit.norm, L));
    //float NdotV = abs(dot(-hit.norm, -V));
    float F = DielectricFresnel(dot(V, H), hit.mat.ior);
    //float D = DistributionGGX(hit.norm, H, hit.mat.roughness);
    float G = SmithG(NdotL, hit.mat.roughness);
    //float eta2 = hit.mat.ior * hit.mat.ior;
    energy *= pow(hit.mat.albedo, 0.5) * (1.0 - hit.mat.metallic) *
        (1.0 - F) * G;
}


#endif