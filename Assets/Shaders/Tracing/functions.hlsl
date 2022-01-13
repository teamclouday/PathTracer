#ifndef TRACER_FUNCTIONS
#define TRACER_FUNCTIONS

#include "global.hlsl"
#include "structures.hlsl"
#include "random.hlsl"
#include "colors.hlsl"
#include "bxdf.hlsl"

Camera CreateCamera(float2 offset)
{
    Camera camera;
    camera.fov_scale = _CameraInfo.x;
    camera.focalDist = _CameraInfo.y;
    camera.aperture = _CameraInfo.z;
    camera.ratio = _CameraInfo.w;
    camera.offset = offset;
    camera.forward = _CameraForward;
    camera.right = _CameraRight;
    camera.up = _CameraUp;
    camera.pos = _CameraPos;
    return camera;
}

Ray CreateRay(float3 origin, float3 direction)
{
    Ray ray;
    ray.origin = origin;
    ray.dir = direction;
    ray.energy = 1.0;
    return ray;
}

Ray CreateCameraRay(Camera camera, float2 d)
{
    // reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/preview.glsl
    d.x *= camera.ratio * camera.fov_scale;
    d.y *= camera.fov_scale;
    float3 dir = normalize(d.x * camera.right + d.y * camera.up + camera.forward);
    if (camera.aperture > 0.0)
    {
        float3 cam_r = rand3() * 2.0 - 1.0;
        // fixed biased distribution caused by cos sin functions in the reference
        float3 randomAperturePos = (cam_r.x * camera.right + cam_r.y * camera.up) * cam_r.z * camera.aperture;
        dir = normalize(dir * camera.focalDist - randomAperturePos);
        return CreateRay(camera.pos + randomAperturePos, dir);
    }
    else
    {
        return CreateRay(camera.pos, dir);
    }
}

Material CreateMaterial(float3 baseColor, float3 emission,
    float metallic, float smoothness, float alpha, float ior,
    int4 indices = -1, float2 uv = 0.0)
{
    if (indices.x >= 0)
    {
        // fetch albedo color
        // and convert from srgb space
        float4 color = _AlbedoTextures.SampleLevel(sampler_AlbedoTextures, float3(uv, indices.x), 0.0);
        baseColor = baseColor * color.rgb;
        alpha = alpha * color.a;
    }
    if (indices.y >= 0)
    {
        // fetch metallic value
        float4 metallicRoughness = _MetallicTextures.SampleLevel(sampler_MetallicTextures, float3(uv, indices.y), 0.0);
        metallic = metallicRoughness.r;
        smoothness = metallicRoughness.a;

    }
    if(indices.w >= 0)
    {
        smoothness = _RoughnessTextures.SampleLevel(sampler_RoughnessTextures, float3(uv, indices.w), 0.0).x;
        smoothness = 1.0 - smoothness;
    }
    Material mat;
    mat.alpha = alpha;
    mat.albedo = baseColor;
    mat.metallic = metallic;
    if (indices.z >= 0)
    {
        // fetch emission value
        emission = emission * _EmitTextures.SampleLevel(sampler_EmitTextures, float3(uv, indices.z), 0.0).xyz;
    }
    mat.emission = emission;
    mat.roughness = 1.0 - smoothness;
    mat.ior = ior;
    return mat;
}

float GetColorAlpha(float alpha, int albedoIdx, float2 uv)
{
    if(albedoIdx >= 0)
    {
        return alpha * _AlbedoTextures.SampleLevel(sampler_AlbedoTextures, float3(uv, albedoIdx), 0.0).a;
    }
    else
    {
        return alpha;
    }
}

float3 GetNormal(int idx, float2 data, int normIdx, float2 uv)
{
    float3 norm0 = _Normals[_Indices[idx]];
    float3 norm1 = _Normals[_Indices[idx + 1]];
    float3 norm2 = _Normals[_Indices[idx + 2]];
    float3 norm = norm1 * data.x + norm2 * data.y + norm0 * (1.0 - data.x - data.y);
    float4 tangent0 = _Tangents[_Indices[idx]];
    float4 tangent1 = _Tangents[_Indices[idx + 1]];
    float4 tangent2 = _Tangents[_Indices[idx + 2]];
    float4 tangent = tangent1 * data.x + tangent2 * data.y + tangent0 * (1.0 - data.x - data.y);
    //tangent.w = tangent0.w;
    if (normIdx >= 0)
    {
        float3 binorm = normalize(cross(norm, tangent.xyz)) * tangent.w;
        float3x3 TBN = float3x3(
            norm,
            binorm,
            tangent.xyz
        );
        float3 normTS = _NormalTextures.SampleLevel(sampler_NormalTextures, float3(uv, normIdx), 0.0).xyz * 2.0 - 1.0;
        return mul(normTS, TBN);
    }
    else
    {
        return norm;
    }
}

HitInfo CreateHitInfo()
{
    HitInfo hit;
    hit.dist = 1.#INF;
    hit.pos = 0.0;
    hit.norm = 0.0;
    hit.mat = CreateMaterial(0.0, 0.0, 0.0, 0.0, 1.0, 1.0);
    hit.mode = 0.0;
    return hit;
}

// return true if it is backface
bool CullFace(float3 norm, float3 eye, float3 pos)
{
    return dot(norm, (eye - pos)) < 0.0;
}

// reference: http://extremelearning.com.au/how-to-generate-uniformly-random-points-on-n-spheres-and-n-balls/
float3 SampleSphere1()
{
    float2 rand = rand2();
    float u = 2.0 * rand.x - 1.0;
    float phi = rand.y * PI_TWO;
    float r = sqrt(1.0 - u * u);
    return float3(r * cos(phi), r * sin(phi), u);
}

float3 SampleSphere2()
{
    return rand3() * 2.0 - 1.0;
}

// create new sample direction in a hemisphere
float3 SampleHemisphere1(float3 norm, float alpha = 0.0)
{
    float2 rand = rand2();
    float cosTheta = pow(rand.x, 1.0 / (1.0 + alpha));
    float sinTheta = saturate(sqrt(1.0 - cosTheta * cosTheta));
    float phi = PI_TWO * rand.y;
    float3 dir = float3(cos(phi) * sinTheta, sin(phi) * sinTheta, cosTheta);
    // from tangent space to world space
    float3 helper = abs(norm.x) < 0.99 ? float3(1, 0, 0) : float3(0, 0, 1);
    float3 tangent = normalize(cross(norm, helper));
    float3 binormal = normalize(cross(norm, tangent));
    // get new direction
    return mul(dir, float3x3(tangent, binormal, norm));
}

float3 SampleHemisphere2(float3 norm)
{
    float2 random = rand2();
    float phi = random.x * PI_TWO;
    float r = sqrt(1.0 - random.y * random.y);
    float3 v = float3(r * cos(phi), r * sin(phi), random.y);
    float3 helper = abs(norm.z) < 0.99 ? float3(0, 0, 1) : float3(1, 0, 0);
    float3 T = normalize(cross(helper, norm));
    float3 B = cross(T, norm);
    return mul(v, float3x3(T, B, norm));
}

// reference: https://github.com/LWJGL/lwjgl3-demos/blob/main/res/org/lwjgl/demo/opengl/raytracing/randomCommon.glsl
float3 SampleDiskPoint(float3 norm)
{
    float3 rand = rand3();
    rand.z = rand.z * 2.0 - 1.0;
    float angle = rand.x * PI_TWO;
    float sr = sqrt(rand.y);
    float2 p = float2(sr * cos(angle), sr * sin(angle));
    float3 tangent = normalize(rand);
    float3 bitangent = cross(tangent, norm);
    tangent = cross(bitangent, norm);
    return tangent * p.x + bitangent * p.y;
}

float3 SampleHemisphere3(float3 norm, float alpha = 1.0)
{
    float4 rand = rand4();
    float r = pow(rand.w, 1.0 / (1.0 + alpha));
    //float r = rand.w;
    float angle = rand.y * PI_TWO;
    float sr = sqrt(1.0 - r * r);
    float3 ph = float3(sr * cos(angle), sr * sin(angle), r);
    //float3 tangent = normalize(SampleSphere2());
    float3 tangent = normalize(rand.zyx + rand3() - 1.0);
    float3 bitangent = cross(norm, tangent);
    tangent = cross(norm, bitangent);
    return mul(ph, float3x3(tangent, bitangent, norm));
}

float3 SampleHemisphere4(float3 norm)
{
    float2 rand = rand2();
    float theta = rand.x * PI_TWO;
    float phi = acos(1.0 - 2.0 * rand.y);
    float3 v = float3(sin(phi) * cos(theta), sin(phi) * sin(theta), cos(phi));
    return v * sign(dot(v, norm));
}

bool SkipTransparent(Material mat)
{
    float f = DielectricFresnel(0.2, mat.ior);
    float r = mat.roughness * mat.roughness;
    return rand() < (1.0 - f) * (1.0 - mat.metallic) * (1.0 - r);
}

// prepare a new ray when entering a BLAS tree
Ray PrepareTreeEnterRay(Ray ray, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    float3 origin = mul(worldToLocal, float4(ray.origin, 1.0)).xyz;
    float3 dir = normalize(mul(worldToLocal, float4(ray.dir, 0.0)).xyz);
    return CreateRay(origin, dir);
}

float PrepareTreeEnterTargetDistance(float targetDist, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (targetDist >= 1.#INF)
    {
        return targetDist;
    }
    else
    {
        // transform a directional vector of length targetDist
        // and return the new length
        float3 dir = mul(worldToLocal, float4(targetDist, 0.0, 0.0, 0.0)).xyz;
        return length(dir);
    }
}

void PrepareTreeEnterHit(Ray rayLocal, inout HitInfo hit, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    if (hit.dist < 1.#INF)
    {
        hit.pos = mul(worldToLocal, float4(hit.pos, 1.0)).xyz;
        hit.dist = length(hit.pos - rayLocal.origin);
    }
}

// update a hit info after exiting a BLAS tree
void PrepareTreeExit(Ray rayWorld, inout HitInfo hit, int transformIdx)
{
    float4x4 localToWorld = _Transforms[transformIdx * 2];
    if (hit.dist < 1.#INF)
    {
        hit.pos = mul(localToWorld, float4(hit.pos, 1.0)).xyz;
        hit.dist = length(hit.pos - rayWorld.origin);
    }
}
#endif