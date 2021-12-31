using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Mesh info for each object
/// </summary>
public struct MeshData
{
    //public Matrix4x4 LocalToWorld;
    public int IndicesStart;
    public int IndicesCount;
    public int MaterialIdx;

    public static int TypeSize = sizeof(int)*3;
}

/// <summary>
/// Information for each material
/// </summary>
public struct MaterialData
{
    public Vector4 Color;
    public Vector3 Emission;
    public float Metallic;
    public float Smoothness;
    public float IOR;
    public float RenderMode;
    public int AlbedoIdx;
    public int EmitIdx;
    public int MetallicIdx;
    public int NormalIdx;
    public int RoughIdx;

    public static int TypeSize = sizeof(float)*11+sizeof(int)*5;
}

/// <summary>
/// BLAS node info
/// </summary>
public struct BLASNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int FaceStartIdx;
    public int FaceEndIdx;
    public int MaterialIdx;
    public int ChildIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*4;
}

/// <summary>
/// Raw TLAS node info
/// </summary>
public struct TLASRawNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int TransformIdx;
    public int NodeRootIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*2;
}

/// <summary>
/// TLAS node built with bvh
/// </summary>
public struct TLASNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int RawNodeStartIdx;
    public int RawNodeEndIdx;
    public int ChildIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*3;
}

public static class TextureManager
{
    public static int GetMaxDimension(int count, int dim)
    {
        if (dim >= 2048)
        {
            if (count <= 16) return 2048;
            else return 1024;
        }
        else if (dim >= 1024)
        {
            if (count <= 48) return 1024;
            else return 512;
        }
        else return dim;
    }
}

/// <summary>
/// Global Object Manager
/// </summary>
public class ObjectManager
{
    private static List<GameObject> objects = new List<GameObject>();
    private static List<Vector3> vertices = new List<Vector3>();
    private static List<Vector2> uvs = new List<Vector2>();
    private static List<Vector3> normals = new List<Vector3>();
    private static List<Vector4> tangents = new List<Vector4>();
    private static List<MaterialData> materials = new List<MaterialData>();

    // TLAS, BLAS
    private static List<BLASNode> bnodes = new List<BLASNode>();
    private static List<TLASNode> tnodes = new List<TLASNode>();
    private static List<TLASRawNode> tnodesRaw = new List<TLASRawNode>();
    // size of objects * 2, local to world & world to local transform
    private static List<Matrix4x4> transforms = new List<Matrix4x4>();
    public static BVHType BVHConstructorType = BVHType.SAH;

    private static List<int> indices = new List<int>();

    public static ComputeBuffer VertexBuffer;
    public static ComputeBuffer UVBuffer;
    public static ComputeBuffer IndexBuffer;
    public static ComputeBuffer NormalBuffer;
    public static ComputeBuffer TangentBuffer;
    public static ComputeBuffer MaterialBuffer;
    public static ComputeBuffer BLASBuffer;
    public static ComputeBuffer TLASBuffer;
    public static ComputeBuffer TLASRawBuffer;
    public static ComputeBuffer TransformBuffer;
    public static Texture2DArray AlbedoTextures = null;
    public static Texture2DArray EmissionTextures = null;
    public static Texture2DArray MetallicTextures = null;
    public static Texture2DArray NormalTextures = null;
    public static Texture2DArray RoughnessTextures = null;

    private static bool objectUpdated = false;
    private static bool objectTransformUpdated = false;

    public static void RegisterObject(GameObject o)
    {
        objects.Add(o);
        objectUpdated = true;
        objectTransformUpdated = true;
    }

    public static void UnregisterObject(GameObject o)
    {
        objects.Remove(o);
        objectUpdated = true;
        objectTransformUpdated = true;
    }

    public static bool Validate()
    {
        foreach (GameObject obj in objects)
        {
            if(obj.transform.hasChanged)
            {
                objectTransformUpdated = true;
                obj.transform.hasChanged = false;
                break;
            }
            if (obj.transform.parent.transform.hasChanged)
            {
                objectTransformUpdated = true;
                obj.transform.parent.transform.hasChanged = false;
                break;
            }
        }

        return BuildBVH() || LoadTransforms();
    }

    private static bool BuildBVH()
    {
        if (!objectUpdated) return false;

        vertices.Clear();
        uvs.Clear();
        indices.Clear();
        normals.Clear();
        tangents.Clear();
        materials.Clear();
        bnodes.Clear();
        tnodesRaw.Clear();

        List<Texture2D> albedoTex = new List<Texture2D>();
        List<Texture2D> emitTex = new List<Texture2D>();
        List<Texture2D> metalTex = new List<Texture2D>();
        List<Texture2D> normTex = new List<Texture2D>();
        List<Texture2D> roughTex = new List<Texture2D>();

        // add default material if submesh does not have a material
        materials.Add(new MaterialData()
        {
            Color = new Vector3(1.0f, 1.0f, 1.0f), // white color by default
            Emission = Vector3.zero,
            Metallic = 0.0f,
            Smoothness = 0.0f,
            IOR = 1.0f,
            RenderMode = 0,
            AlbedoIdx = -1,
            EmitIdx = -1,
            MetallicIdx = -1,
            NormalIdx = -1,
            RoughIdx = -1
        });

        // get info from each object
        for(int idx = 0; idx < objects.Count; idx++)
        {
            var obj = objects[idx];
            // load materials
            var meshMats = obj.GetComponent<Renderer>().sharedMaterials;
            int matStart = materials.Count;
            int matCount = meshMats.Length;
            foreach(var mat in meshMats)
            {
                int albedoTexIdx = -1, emiTexIdx = -1, metalTexIdx = -1, normTexIdx = -1, roughTexIdx = -1;
                if (mat.HasProperty("_MainTex"))
                {
                    albedoTexIdx = albedoTex.IndexOf(mat.mainTexture as Texture2D);
                    if(albedoTexIdx < 0 && mat.mainTexture != null)
                    {
                        albedoTexIdx = albedoTex.Count;
                        albedoTex.Add(mat.mainTexture as Texture2D);
                    }
                }
                if (mat.HasProperty("_EmissionMap"))
                {
                    var emitMap = mat.GetTexture("_EmissionMap");
                    emiTexIdx = emitTex.IndexOf(emitMap as Texture2D);
                    if (emiTexIdx < 0 && emitMap != null)
                    {
                        emiTexIdx = emitTex.Count;
                        emitTex.Add(emitMap as Texture2D);
                    }
                }
                if (mat.HasProperty("_MetallicGlossMap"))
                {
                    var metalMap = mat.GetTexture("_MetallicGlossMap");
                    metalTexIdx = metalTex.IndexOf(metalMap as Texture2D);
                    if (metalTexIdx < 0 && metalMap != null)
                    {
                        metalTexIdx = metalTex.Count;
                        metalTex.Add(metalMap as Texture2D);
                    }
                }
                if (mat.HasProperty("_BumpMap"))
                {
                    var normMap = mat.GetTexture("_BumpMap");
                    normTexIdx = normTex.IndexOf(normMap as Texture2D);
                    if (normTexIdx < 0 && normMap != null)
                    {
                        normTexIdx = normTex.Count;
                        normTex.Add(normMap as Texture2D);
                    }
                }
                if (mat.HasProperty("_SpecGlossMap"))
                {
                    var roughMap = mat.GetTexture("_SpecGlossMap"); // assume Autodesk interactive shader
                    roughTexIdx = roughTex.IndexOf(roughMap as Texture2D);
                    if (roughTexIdx < 0 && roughMap != null)
                    {
                        roughTexIdx = roughTex.Count;
                        roughTex.Add(roughMap as Texture2D);
                    }
                }
                materials.Add(new MaterialData()
                {
                    Color = ColorToVector4(mat.color),
                    Emission = mat.IsKeywordEnabled("_EMISSION") ? ColorToVector3(mat.GetColor("_EmissionColor")) : Vector3.zero,
                    // assuming standard unity shader
                    Metallic = mat.GetFloat("_Metallic"),
                    Smoothness = mat.GetFloat("_Glossiness"), // smoothness
                    IOR = mat.HasProperty("_IOR") ? mat.GetFloat("_IOR") : 1.0f,
                    RenderMode = mat.GetFloat("_Mode"), // 0 for opaque, > 0 for transparent
                    AlbedoIdx = albedoTexIdx, // texture index for albedo map, -1 if not exist
                    EmitIdx = emiTexIdx, // texture index for emission map
                    MetallicIdx = metalTexIdx, // texture index for metallic map
                    NormalIdx = normTexIdx, // texture index for normal map
                    RoughIdx = roughTexIdx, // texture index for roughness map
                });
            }

            var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            var meshVertices = mesh.vertices.ToList();
            var meshNormals = mesh.normals;
            var meshUVs = mesh.uv;
            var meshTangents = mesh.tangents;
            int vertexStart = vertices.Count;
            
            for(int i = 0; i < mesh.subMeshCount; i++)
            {
                var submeshIndices = mesh.GetIndices(i).ToList();
                BVH blasTree = BVH.Construct(meshVertices, submeshIndices, BVHConstructorType);
                blasTree.FlattenBLAS(ref indices, ref bnodes, ref tnodesRaw, submeshIndices, vertexStart, i < matCount ? i + matStart : 0, idx);
            }

            vertices.AddRange(meshVertices);
            uvs.AddRange(meshUVs);
            normals.AddRange(meshNormals);
            tangents.AddRange(meshTangents);
            if (meshNormals.Length != meshVertices.Count)
                Debug.LogWarning("Object " + obj.name + " has different normals and vertices size");
            if (meshTangents.Length != meshVertices.Count)
                Debug.LogWarning("Object " + obj.name + " has different tangents and vertices size");
            if (meshUVs.Length != meshVertices.Count)
                Debug.LogWarning("Object " + obj.name + " has different uvs and vertices size");
        }

        // if not UV is used, insert empty one
        if (uvs.Count <= 0) uvs.Add(Vector2.zero);

        // build TLAS bvh
        ReloadTLAS();

        UpdateBuffer(ref IndexBuffer, indices, sizeof(int));
        UpdateBuffer(ref VertexBuffer, vertices, sizeof(float) * 3);
        UpdateBuffer(ref UVBuffer, uvs, sizeof(float) * 2);
        UpdateBuffer(ref NormalBuffer, normals, sizeof(float) * 3);
        UpdateBuffer(ref TangentBuffer, tangents, sizeof(float) * 4);
        UpdateBuffer(ref MaterialBuffer, materials, MaterialData.TypeSize);
        UpdateBuffer(ref BLASBuffer, bnodes, BLASNode.TypeSize);

        // create texture 2d array
        if (AlbedoTextures != null) UnityEngine.Object.Destroy(AlbedoTextures);
        if (EmissionTextures != null) UnityEngine.Object.Destroy(EmissionTextures);
        if (MetallicTextures != null) UnityEngine.Object.Destroy(MetallicTextures);
        if (NormalTextures != null) UnityEngine.Object.Destroy(NormalTextures);
        if (RoughnessTextures != null) UnityEngine.Object.Destroy(RoughnessTextures);
        AlbedoTextures = CreateTextureArray(ref albedoTex);
        EmissionTextures = CreateTextureArray(ref emitTex);
        MetallicTextures = CreateTextureArray(ref metalTex);
        NormalTextures = CreateTextureArray(ref normTex);
        RoughnessTextures = CreateTextureArray(ref roughTex);

        // final report
        Debug.Log(
            "BVH built\n" +
            "TLAS nodes = " + tnodes.Count + "\n" +
            "TLAS raw nodes = " + tnodesRaw.Count + "\n" +
            "BLAS nodes = " + bnodes.Count + "\n" +
            "Total vertices = " + vertices.Count + "\n" +
            "Total indices = " + indices.Count + "\n" +
            "Total normals = " + normals.Count + "\n" +
            "Total tangents = " + tangents.Count + "\n" +
            "Total materials = " + materials.Count + "\n" +
            "Total albedo textures = " + albedoTex.Count + "\n" +
            "Total emissive textures = " + emitTex.Count + "\n" +
            "Total metallic textures = " + metalTex.Count + "\n" +
            "Total normal textures = " + normTex.Count + "\n" +
            "Total roughness textures = " + roughTex.Count
        );

        objectUpdated = false;
        return true;
    }

    private static bool LoadTransforms()
    {
        if (!objectTransformUpdated) return false;

        transforms.Clear();

        foreach(var obj in objects)
        {
            transforms.Add(obj.transform.localToWorldMatrix);
            transforms.Add(obj.transform.worldToLocalMatrix);
        }

        UpdateBuffer(ref TransformBuffer, transforms, sizeof(float) * 4 * 4);

        objectTransformUpdated = false;
        return true;
    }

    public static void ReloadMaterials()
    {
        int matIdx = 1;
        // get info from each object
        foreach (var obj in objects)
        {
            // load materials
            var meshMats = obj.GetComponent<Renderer>().sharedMaterials;
            foreach (var mat in meshMats)
            {
                materials[matIdx] = new MaterialData()
                {
                    Color = ColorToVector4(mat.color),
                    Emission = mat.IsKeywordEnabled("_EMISSION") ? ColorToVector3(mat.GetColor("_EmissionColor")) : Vector3.zero,
                    Metallic = mat.GetFloat("_Metallic"),
                    Smoothness = mat.GetFloat("_Glossiness"),
                    IOR = mat.HasProperty("_IOR") ? mat.GetFloat("_IOR") : 1.0f,
                    RenderMode = mat.GetFloat("_Mode"),
                    AlbedoIdx = materials[matIdx].AlbedoIdx,
                    EmitIdx = materials[matIdx].EmitIdx,
                    MetallicIdx = materials[matIdx].MetallicIdx,
                    NormalIdx = materials[matIdx].NormalIdx,
                    RoughIdx = materials[matIdx].RoughIdx,
                };
                matIdx++;
            }
        }
        UpdateBuffer(ref MaterialBuffer, materials, MaterialData.TypeSize);
        Debug.Log("Materials reloaded");
    }

    public static void ReloadTLAS()
    {
        if (tnodesRaw.Count <= 0) return;
        if (transforms.Count <= 0) LoadTransforms();
        tnodes.Clear();
        BVH tlasTree = BVH.Construct(tnodesRaw, transforms, BVHConstructorType);
        tlasTree.FlattenTLAS(ref tnodesRaw, ref tnodes);
        UpdateBuffer(ref TLASBuffer, tnodes, TLASNode.TypeSize);
        UpdateBuffer(ref TLASRawBuffer, tnodesRaw, TLASRawNode.TypeSize);
    }

    private static void UpdateBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride) where T : struct
    {
        if(buffer != null) buffer.Release();
        if (data.Count == 0) return;
        buffer = new ComputeBuffer(data.Count, stride);
        buffer.SetData(data);
    }

    public static void Destroy()
    {
        if (IndexBuffer != null) IndexBuffer.Release();
        if (VertexBuffer != null) VertexBuffer.Release();
        if (NormalBuffer != null) NormalBuffer.Release();
        if (TangentBuffer != null) TangentBuffer.Release();
        if (UVBuffer != null) UVBuffer.Release();
        if (MaterialBuffer != null) MaterialBuffer.Release();
        if (TLASBuffer != null) TLASBuffer.Release();
        if (TLASRawBuffer != null) TLASRawBuffer.Release();
        if (BLASBuffer != null) BLASBuffer.Release();
        if (TransformBuffer != null) TransformBuffer.Release();
        if (AlbedoTextures != null) UnityEngine.Object.Destroy(AlbedoTextures);
        if (EmissionTextures != null) UnityEngine.Object.Destroy(EmissionTextures);
        if (MetallicTextures != null) UnityEngine.Object.Destroy(MetallicTextures);
        if (NormalTextures != null) UnityEngine.Object.Destroy(NormalTextures);
        if (RoughnessTextures != null) UnityEngine.Object.Destroy(RoughnessTextures);
    }

    private static Vector4 ColorToVector4(Color color)
    {
        return new Vector4(color.r, color.g, color.b, color.a);
    }

    private static Vector3 ColorToVector3(Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }

    private static Texture2DArray CreateTextureArray(ref List<Texture2D> textures)
    {
        int texWidth = 1, texHeight = 1;
        foreach (Texture tex in textures)
        {
            texWidth = Mathf.Max(texWidth, tex.width);
            texHeight = Mathf.Max(texHeight, tex.height);
        }
        int maxDim = TextureManager.GetMaxDimension(textures.Count, Mathf.Max(texWidth, texHeight));
        texWidth = Mathf.Min(texWidth, maxDim);
        texHeight = Mathf.Min(texHeight, maxDim);
        var newTexture = new Texture2DArray(
            texWidth, texHeight, Mathf.Max(1, textures.Count),
            TextureFormat.ARGB32, true, false
        );
        newTexture.SetPixels(Enumerable.Repeat(Color.white, texWidth * texHeight).ToArray(), 0, 0);
        RenderTexture rt = new RenderTexture(texWidth, texHeight, 1, RenderTextureFormat.ARGB32);
        Texture2D tmp = new Texture2D(texWidth, texHeight, TextureFormat.ARGB32, false);
        for (int i = 0; i < textures.Count; i++)
        {
            RenderTexture.active = rt;
            Graphics.Blit(textures[i], rt);
            tmp.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);
            tmp.Apply();
            newTexture.SetPixels(tmp.GetPixels(0), i, 0);
        }
        newTexture.Apply();
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(tmp);
        return newTexture;
    }
}
