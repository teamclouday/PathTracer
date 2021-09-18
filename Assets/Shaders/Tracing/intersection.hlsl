#ifndef TRACER_INTERSECTION
#define TRACER_INTERSECTION

#include "global.hlsl"
#include "structures.hlsl"
#include "functions.hlsl"

// intersection with ground
//void IntersectGround(Ray ray, inout HitInfo bestHit, float yVal = 0.0)
//{
//    float t = -(ray.origin.y - yVal) / ray.dir.y;
//    if (t > 0.0 && t < bestHit.dist)
//    {
//        bestHit.dist = t;
//        bestHit.pos = ray.origin + t * ray.dir;
//        bestHit.norm = float3(0.0, 1.0, 0.0);
//        // create a mirror ground brdf
//        bestHit.colors = CreateColors(1.0, 0.0, 0.0);
//        bestHit.smoothness = 0.0;
//    }
//}

// quickly determine if intersect with ground
//bool IntersectGroundFast(Ray ray, float targetDist, float yVal = 0.0)
//{
//    float t = -(ray.origin.y - yVal) / ray.dir.y;
//    if (t > 0.0 && t < targetDist)
//        return true;
//    return false;
//}

// test intersection with triangle
bool IntersectTriangle(Ray ray, float3 v0, float3 v1, float3 v2,
    inout float t, inout float u, inout float v
)
{
    float3 e1 = v1 - v0;
    float3 e2 = v2 - v0;
    float3 pvec = cross(ray.dir, e2);
    float det = dot(e1, pvec);
    if (det < 1e-8)
        return false;
    float detInv = 1.0 / det;
    float3 tvec = ray.origin - v0;
    u = dot(tvec, pvec) * detInv;
    if(u < 0.0 || u > 1.0)
        return false;
    float3 qvec = cross(tvec, e1);
    v = dot(ray.dir, qvec) * detInv;
    if(v < 0.0 || v + u > 1.0)
        return false;
    t = dot(e2, qvec) * detInv;
    return true;
}

// intersect with mesh object every vertices
void IntersectMeshObject(Ray ray, inout HitInfo bestHit, MeshData mesh)
{
    int offset = mesh.indicesStart;
    int count = mesh.indicesCount;
    for (int i = offset; i < offset + count; i += 3)
    {
        float3 v0 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i]], 1.0))).xyz;
        float3 v1 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i+1]], 1.0))).xyz;
        float3 v2 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i+2]], 1.0))).xyz;
        float3 norm0 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i]], 0.0))).xyz;
        float3 norm1 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i+1]], 0.0))).xyz;
        float3 norm2 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i+2]], 0.0))).xyz;
        float t, u, v;
        if(IntersectTriangle(ray, v0, v1, v2, t, u, v))
        {
            if(t > 0.0 && t < bestHit.dist)
            {
                float3 hitPos = ray.origin + t * ray.dir;
                float3 norm = norm1 * u + norm2 * v + norm0 * (1.0 - u - v);
                //float3 norm = cross(v2 - v0, v1 - v0);
                //if (!CullFace(norm, ray.origin, hitPos))
                //{
                MaterialData mat = _Materials[mesh.materialIdx];
                bestHit.dist = t;
                bestHit.pos = hitPos;
                bestHit.norm = normalize(norm);
                bestHit.colors = CreateColors(mat.color, mat.emission, mat.metallic);
                bestHit.smoothness = mat.smoothness;
                //}
            }
        }

    }
}

// quickly determine if intersect with something
bool IntersectMeshObjectFast(Ray ray, MeshData mesh, float targetDist)
{
    int offset = mesh.indicesStart;
    int count = mesh.indicesCount;
    for (int i = offset; i < offset + count; i += 3)
    {
        float3 v0 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i]], 1.0))).xyz;
        float3 v1 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i + 1]], 1.0))).xyz;
        float3 v2 = (mul(mesh.localToWorld, float4(_Vertices[_Indices[i + 2]], 1.0))).xyz;
        float3 norm0 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i]], 0.0))).xyz;
        float3 norm1 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i + 1]], 0.0))).xyz;
        float3 norm2 = (mul(mesh.localToWorld, float4(_Normals[_Indices[i + 2]], 0.0))).xyz;
        float t, u, v;
        if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
        {
            if (t > 0.0 && t < targetDist)
            {
                //float3 hitPos = ray.origin + t * ray.dir;
                //float3 norm = norm1 * u + norm2 * v + norm0 * (1.0 - u - v);
                //if (!CullFace(norm, ray.origin, hitPos))
                //{
                //    return true;
                //}
                return true; // do not test for back face culling
            }
        }
    }
    return false;
}

#endif