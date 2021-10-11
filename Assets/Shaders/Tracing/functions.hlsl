#ifndef TRACER_FUNCTIONS
#define TRACER_FUNCTIONS

#include "global.hlsl"
#include "structures.hlsl"
#include "random.hlsl"

Camera CreateCamera()
{
    Camera camera;
    camera.fov_scale = _CameraInfo.x;
    camera.focalDist = _CameraInfo.y;
    camera.aperture = _CameraInfo.z;
    camera.ratio = _CameraInfo.w;
    camera.offset = _PixelOffset;
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
    //float3 origin = mul(_CameraToWorld, float4(0.0, 0.0, 0.0, 1.0)).xyz;
    //float3 direction = mul(_CameraProjInv, float4(uv, 0.0, 1.0)).xyz;
    //direction = mul(_CameraToWorld, float4(direction, 0.0)).xyz;
    //direction = normalize(direction);
    //return CreateRay(origin, direction);
    
    // reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/preview.glsl
    d.x *= camera.fov_scale;
    d.y *= camera.ratio * camera.fov_scale;
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
    //float scale = tan(camera.fov * 0.5f);
    //d.x *= scale;
    //d.y *= camera.ratio * scale;
    //float3 direction = normalize(d.x * camera.right + d.y * camera.up + camera.forward);
    //return CreateRay(camera.pos, direction);
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

float3 SampleHemisphere3(float3 norm, float alpha = 0.0)
{
    float4 rand = rand4();
    float r = pow(rand.w, 1.0 / (1.0 + alpha));
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
    float sint = etai / etat * sqrt(1.0 - cosi * cosi);
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
        return (Rs * Rs + Rp * Rp) / 2.0;
    }
}

// prepare a new ray when entering a BLAS tree
Ray PrepareTreeEnterRay(Ray ray, int transformIdx)
{
    float4x4 worldToLocal = _Transforms[transformIdx * 2 + 1];
    float3 origin = mul(worldToLocal, float4(ray.origin, 1.0)).xyz;
    float3 dir = normalize(mul(worldToLocal, float4(ray.dir, 0.0)).xyz);
    return CreateRay(origin, dir);
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