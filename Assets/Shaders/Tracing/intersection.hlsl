#ifndef TRACER_INTERSECTION
#define TRACER_INTERSECTION

#include "global.hlsl"
#include "structures.hlsl"
#include "functions.hlsl"

#define BVHTREE_RECURSE_SIZE 32

// intersection with ground
void IntersectGround(Ray ray, inout HitInfo bestHit, float yVal = 0.0)
{
    float t = -(ray.origin.y - yVal) / ray.dir.y;
    if (t > 0.0 && t < bestHit.dist)
    {
        bestHit.dist = t;
        bestHit.pos = ray.origin + t * ray.dir;
        bestHit.norm = float3(0.0, 1.0, 0.0);
        bestHit.mat = CreateMaterial(1.0, 0.0, 0.0, 0.0, 1.0, 1.0);
        bestHit.mode = 0.0;
    }
}

// quickly determine if intersect with ground
bool IntersectGroundFast(Ray ray, float targetDist, float yVal = 0.0)
{
    float t = -(ray.origin.y - yVal) / ray.dir.y;
    if (t > 0.0 && t < targetDist)
        return true;
    return false;
}

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

// test intersection with bounding box
bool IntersectBox1(Ray ray, float3 pMax, float3 pMin)
{
    float t0 = 0.0;
    float t1 = 1.#INF;
    float invRayDir, tNear, tFar;
    for (int i = 0; i < 3; i++)
    {
        invRayDir = 1.0 / ray.dir[i];
        tNear = (pMin[i] - ray.origin[i]) * invRayDir;
        tFar = (pMax[i] - ray.origin[i]) * invRayDir;
        t0 = max(t0, tNear);
        t1 = min(t1, tFar);
        if (t0 > t1)
        {
            return false;
        }
    }
    return true;
}

bool IntersectBox2(Ray ray, float3 pMax, float3 pMin)
{
    // reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/common/intersection.glsl
    // reference: https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525
    float3 invDir = 1.0 / ray.dir;
    float3 f = (pMax - ray.origin) * invDir;
    float3 n = (pMin - ray.origin) * invDir;
    float3 tMax = max(f, n);
    float3 tMin = min(f, n);
    float t0 = max(tMin.x, max(tMin.y, tMin.z));
    float t1 = min(tMax.x, min(tMax.y, tMax.z));
    return t1 >= t0;
}

bool IntersectBox3(Ray ray, HitInfo bestHit, float3 pMax, float3 pMin)
{
    bool intersectForward = IntersectBox2(ray, pMax, pMin);
    bool intersectBackward = bestHit.dist < 1.#INF ? IntersectBox2(CreateRay(bestHit.pos, -ray.dir), pMax, pMin) : true;
    return intersectForward && intersectBackward;
}

// intersect with mesh object every vertices
void IntersectMeshObject(Ray ray, inout HitInfo bestHit, MeshData mesh)
{
    int offset = mesh.indicesStart;
    int count = mesh.indicesCount;
    for (int i = offset; i < offset + count; i += 3)
    {
        float3 v0 = _Vertices[_Indices[i]];
        float3 v1 = _Vertices[_Indices[i + 1]];
        float3 v2 = _Vertices[_Indices[i + 2]];
        float2 uv0 = _UVs[_Indices[i]];
        float2 uv1 = _UVs[_Indices[i + 1]];
        float2 uv2 = _UVs[_Indices[i + 2]];
        float t, u, v;
        if(IntersectTriangle(ray, v0, v1, v2, t, u, v))
        {
            if(t > 0.0 && t < bestHit.dist)
            {
                MaterialData mat = _Materials[mesh.materialIdx];
                float3 hitPos = ray.origin + t * ray.dir;
                float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);
                float3 norm = GetNormal(i, float2(u, v), mat.normIdx, uv);
                Material mats = CreateMaterial(
                    mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
                    int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
                );
                if ((mat.mode == 1.0 && mats.alpha < 1.0) || (mat.mode > 1.0 && SkipTransparent(mats)))
                    continue;
                bestHit.dist = t;
                bestHit.pos = hitPos;
                bestHit.norm = normalize(norm);
                bestHit.mat = mats;
                bestHit.mode = mat.mode;
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
        float3 v0 = _Vertices[_Indices[i]];
        float3 v1 = _Vertices[_Indices[i + 1]];
        float3 v2 = _Vertices[_Indices[i + 2]];
        float t, u, v;
        if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
        {
            if (t > 0.0 && t < targetDist)
            {
                return true; // do not test for back face culling
            }
        }
    }
    return false;
}

// test intersection with BVH tree
// reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/common/closest_hit.glsl
void IntersectBlasTree(Ray ray, inout HitInfo bestHit, int startIdx, int transformIdx)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int faceIdx;
    stack[stackPtr] = startIdx;
    float4x4 localToWorld = _Transforms[transformIdx * 2];
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];
        BLASNode node = _BNodes[idx];
        // check if ray intersect with bounding box
        bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);
        bool leaf = node.faceStartIdx >= 0;
        if (hit)
        {
            if (leaf)
            {
                for (faceIdx = node.faceStartIdx; faceIdx < node.faceEndIdx; faceIdx++)
                {
                    int i = faceIdx * 3;
                    float3 v0 = _Vertices[_Indices[i]];
                    float3 v1 = _Vertices[_Indices[i + 1]];
                    float3 v2 = _Vertices[_Indices[i + 2]];
                    float2 uv0 = _UVs[_Indices[i]];
                    float2 uv1 = _UVs[_Indices[i + 1]];
                    float2 uv2 = _UVs[_Indices[i + 2]];
                    float t, u, v;
                    if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
                    {
                        if (t > 0.0 && t < bestHit.dist)
                        {
                            MaterialData mat = _Materials[node.materialIdx];
                            float3 hitPos = ray.origin + t * ray.dir;
                            float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);
                            float3 norm = GetNormal(i, float2(u, v), mat.normIdx, uv);
                            Material mats = CreateMaterial(
                                mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
                                int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
                            );
                            if (mat.mode == 1.0 && mats.alpha < 1.0)
                                continue;
                            bestHit.dist = t;
                            bestHit.pos = hitPos;
                            bestHit.norm = normalize(mul(localToWorld, float4(norm, 0.0)).xyz);
                            bestHit.mat = mats;
                            bestHit.mode = mat.mode;
                        }
                    }
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;
                stack[++stackPtr] = node.childIdx + 1;
            }
        }
    }
}

// quickly determine if intersect with something
bool IntersectBlasTreeFast(Ray ray, int startIdx, float targetDist)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int faceIdx;
    stack[stackPtr] = startIdx;
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];
        BLASNode node = _BNodes[idx];
        // check if ray intersect with bounding box
        bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);
        bool leaf = node.faceStartIdx >= 0;
        if (hit)
        {
            if (leaf)
            {
                for (faceIdx = node.faceStartIdx; faceIdx < node.faceEndIdx; faceIdx++)
                {
                    int i = faceIdx * 3;
                    float3 v0 = _Vertices[_Indices[i]];
                    float3 v1 = _Vertices[_Indices[i + 1]];
                    float3 v2 = _Vertices[_Indices[i + 2]];
                    float2 uv0 = _UVs[_Indices[i]];
                    float2 uv1 = _UVs[_Indices[i + 1]];
                    float2 uv2 = _UVs[_Indices[i + 2]];
                    float t, u, v;
                    if (IntersectTriangle(ray, v0, v1, v2, t, u, v))
                    {
                        if (t > 0.0 && t < targetDist)
                        {
                            MaterialData mat = _Materials[node.materialIdx];
                            float2 uv = uv1 * u + uv2 * v + uv0 * (1.0 - u - v);
                            Material mats = CreateMaterial(
                                mat.color.rgb, mat.emission, mat.metallic, mat.smoothness, mat.color.a, mat.ior,
                                int4(mat.albedoIdx, mat.metalIdx, mat.emitIdx, mat.roughIdx), uv
                            );
                            if (mat.mode == 1.0 && mats.alpha < 1.0 ||
                                    (mat.mode > 1.0 && SkipTransparent(mats)))
                                continue;
                            return true;
                        }
                    }
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;
                stack[++stackPtr] = node.childIdx + 1;
            }
        }
    }
    return false;
}

void IntersectTlasTree(Ray ray, inout HitInfo bestHit)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int rawNodeIdx;
    stack[stackPtr] = 0;
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];
        TLASNode node = _TNodes[idx];
        bool hit = IntersectBox3(ray, bestHit, node.boundMax, node.boundMin);
        bool leaf = node.rawNodeStartIdx >= 0;
        if(hit)
        {
            if(leaf)
            {
                for (rawNodeIdx = node.rawNodeStartIdx; rawNodeIdx < node.rawNodeEndIdx; rawNodeIdx++)
                {
                    TLASNodeRaw rawNode = _TNodesRaw[rawNodeIdx];
                    Ray localRay = PrepareTreeEnterRay(ray, rawNode.transformIdx);
                    PrepareTreeEnterHit(localRay, bestHit, rawNode.transformIdx);
                    IntersectBlasTree(localRay, bestHit, rawNode.rootIdx, rawNode.transformIdx);
                    PrepareTreeExit(ray, bestHit, rawNode.transformIdx);
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;
                stack[++stackPtr] = node.childIdx + 1;
            }
        }
    }
}

void IntersectTlas(Ray ray, inout HitInfo bestHit)
{
    uint size, stride;
    _TNodesRaw.GetDimensions(size, stride);
    for (uint i = 0; i < size; i++)
    {
        TLASNodeRaw node = _TNodesRaw[i];
        Ray localRay = PrepareTreeEnterRay(ray, node.transformIdx);
        if (IntersectBox2(localRay, node.boundMax, node.boundMin))
        {
            // intersect with BLAS tree
            PrepareTreeEnterHit(localRay, bestHit, node.transformIdx);
            IntersectBlasTree(localRay, bestHit, node.rootIdx, node.transformIdx);
            PrepareTreeExit(ray, bestHit, node.transformIdx);
        }
    }
}

bool IntersectTlasTreeFast(Ray ray, HitInfo bestHit, float targetDist)
{
    int stack[BVHTREE_RECURSE_SIZE];
    int stackPtr = 0;
    int rawNodeIdx;
    stack[stackPtr] = 0;
    while (stackPtr >= 0 && stackPtr < BVHTREE_RECURSE_SIZE)
    {
        int idx = stack[stackPtr--];
        TLASNode node = _TNodes[idx];
        bool hit = IntersectBox2(ray, node.boundMax, node.boundMin);
        bool leaf = node.rawNodeStartIdx >= 0;
        if (hit)
        {
            if (leaf)
            {
                for (rawNodeIdx = node.rawNodeStartIdx; rawNodeIdx < node.rawNodeEndIdx; rawNodeIdx++)
                {
                    TLASNodeRaw rawNode = _TNodesRaw[rawNodeIdx];
                    Ray localRay = PrepareTreeEnterRay(ray, rawNode.transformIdx);
                    PrepareTreeEnterHit(localRay, bestHit, rawNode.transformIdx);
                    if (IntersectBlasTreeFast(localRay, rawNode.rootIdx, targetDist))
                        return true;
                    PrepareTreeExit(ray, bestHit, rawNode.transformIdx);
                }
            }
            else
            {
                stack[++stackPtr] = node.childIdx;
                stack[++stackPtr] = node.childIdx + 1;
            }
        }
    }
    return false;
}

bool IntersectTlasFast(Ray ray, HitInfo bestHit, float targetDist)
{
    uint size, stride;
    _TNodesRaw.GetDimensions(size, stride);
    float dist = targetDist;
    for (uint i = 0; i < size; i++)
    {
        TLASNodeRaw node = _TNodesRaw[i];
        Ray localRay = PrepareTreeEnterRay(ray, node.transformIdx);
        if (IntersectBox2(localRay, node.boundMax, node.boundMin))
        {
            // intersect with BLAS tree
            PrepareTreeEnterHit(localRay, bestHit, node.transformIdx);
            dist = PrepareTreeEnterTargetDistance(targetDist, node.transformIdx);
            if (IntersectBlasTreeFast(localRay, node.rootIdx, dist))
                return true;
            PrepareTreeExit(ray, bestHit, node.transformIdx);
        }
    }
    return false;
}

#endif