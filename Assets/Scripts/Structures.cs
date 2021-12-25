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
    public Vector3 Color;
    public Vector3 Emission;
    public float Metallic;
    public float Smoothness;
    public float RenderMode;
    public int DiffuseIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(float)*3+sizeof(int);
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

public struct TLASNode
{
    public Vector3 BoundMax;
    public Vector3 BoundMin;
    public int TransformIdx;
    public int NodeRootIdx;

    public static int TypeSize = sizeof(float)*3*2+sizeof(int)*2;
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
    private static List<MaterialData> materials = new List<MaterialData>();

    // TLAS, BLAS
    private static List<BLASNode> bnodes = new List<BLASNode>();
    private static List<TLASNode> tnodes = new List<TLASNode>();
    // size of objects * 2, local to world & world to local transform
    private static List<Matrix4x4> transforms = new List<Matrix4x4>();
    public static BVHType BVHConstructorType = BVHType.SAH;

    private static List<int> indices = new List<int>();

    public static ComputeBuffer VertexBuffer;
    public static ComputeBuffer UVBuffer;
    public static ComputeBuffer IndexBuffer;
    public static ComputeBuffer NormalBuffer;
    public static ComputeBuffer MaterialBuffer;
    public static ComputeBuffer BLASBuffer;
    public static ComputeBuffer TLASBuffer;
    public static ComputeBuffer TransformBuffer;
    public static Texture2DArray AlbedoTextures = null;

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
        materials.Clear();
        bnodes.Clear();
        tnodes.Clear();

        List<Texture2D> textures = new List<Texture2D>();

        // add default material if submesh does not have a material
        materials.Add(new MaterialData()
        {
            Color = new Vector3(1.0f, 1.0f, 1.0f), // white color by default
            Emission = Vector3.zero,
            Metallic = 0.0f,
            Smoothness = 0.0f,
            RenderMode = 0,
            DiffuseIdx = -1
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
                int textureIdx = -1;
                if (mat.mainTexture != null)
                {
                    textureIdx = textures.IndexOf(mat.mainTexture as Texture2D);
                    if(textureIdx < 0)
                    {
                        textureIdx = textures.Count;
                        textures.Add(mat.mainTexture as Texture2D);
                    }
                }
                materials.Add(new MaterialData()
                {
                    Color = ColorToVector(mat.color),
                    Emission = mat.IsKeywordEnabled("_EMISSION") ? ColorToVector(mat.GetColor("_EmissionColor")) : Vector3.zero,
                    // assuming standard unity shader
                    Metallic = mat.GetFloat("_Metallic"),
                    Smoothness = mat.GetFloat("_Glossiness"), // smoothness
                    RenderMode = mat.GetFloat("_Mode"), // 0 for opaque, > 0 for transparent
                    DiffuseIdx = textureIdx // texture index for albedo map, -1 if not exist
                });
            }

            var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            var meshVertices = mesh.vertices.ToList();
            var meshNormals = mesh.normals;
            var meshUVs = mesh.uv;
            int vertexStart = vertices.Count;
            
            for(int i = 0; i < mesh.subMeshCount; i++)
            {
                var submeshIndices = mesh.GetIndices(i).ToList();
                BVH blasTree = BVH.Construct(meshVertices, submeshIndices, BVHConstructorType);
                blasTree.Flatten(ref indices, ref bnodes, ref tnodes, submeshIndices, vertexStart, i < matCount ? i + matStart : 0, idx);
            }

            vertices.AddRange(meshVertices);
            uvs.AddRange(meshUVs);
            normals.AddRange(meshNormals);
            if (meshNormals.Length != meshVertices.Count)
                Debug.LogError("Object " + obj.name + " has different normals and vertices size");
            if (meshUVs.Length != meshVertices.Count)
                Debug.LogError("Object " + obj.name + " has different uvs and vertices size");
        }

        UpdateBuffer(ref IndexBuffer, indices, sizeof(int));
        UpdateBuffer(ref VertexBuffer, vertices, sizeof(float) * 3);
        UpdateBuffer(ref UVBuffer, uvs, sizeof(float) * 2);
        UpdateBuffer(ref NormalBuffer, normals, sizeof(float) * 3);
        UpdateBuffer(ref MaterialBuffer, materials, MaterialData.TypeSize);
        UpdateBuffer(ref BLASBuffer, bnodes, BLASNode.TypeSize);
        UpdateBuffer(ref TLASBuffer, tnodes, TLASNode.TypeSize);

        // create texture 2d array
        CreateAlbedoTexture(ref textures);

        // final report
        Debug.Log(
            "BVH built\n" +
            "TLAS nodes = " + tnodes.Count + "\n" +
            "BLAS nodes = " + bnodes.Count + "\n" +
            "Total vertices = " + vertices.Count + "\n" +
            "Total indices = " + indices.Count + "\n" +
            "Total normals = " + normals.Count + "\n" +
            "Total materials = " + materials.Count + "\n" +
            "Total textures = " + textures.Count
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
        if (UVBuffer != null) UVBuffer.Release();
        if (MaterialBuffer != null) MaterialBuffer.Release();
        if (TLASBuffer != null) TLASBuffer.Release();
        if (BLASBuffer != null) BLASBuffer.Release();
        if (TransformBuffer != null) TransformBuffer.Release();
        if (AlbedoTextures != null) DestroyAlbedoTexture();
    }

    private static Vector3 ColorToVector(Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }

    private static void CreateAlbedoTexture(ref List<Texture2D> textures)
    {
        if (AlbedoTextures != null) DestroyAlbedoTexture();
        int texWidth = 1, texHeight = 1;
        foreach (Texture tex in textures)
        {
            texWidth = Mathf.Max(texWidth, tex.width);
            texHeight = Mathf.Max(texHeight, tex.height);
        }
        AlbedoTextures = new Texture2DArray(
            texWidth, texHeight, Mathf.Max(1, textures.Count),
            TextureFormat.ARGB32, true, false
        );
        AlbedoTextures.SetPixels(Enumerable.Repeat(Color.white, texWidth * texHeight).ToArray(), 0, 0);
        RenderTexture rt = new RenderTexture(texWidth, texHeight, 1, RenderTextureFormat.ARGB32);
        Texture2D tmp = new Texture2D(texWidth, texHeight, TextureFormat.ARGB32, false);
        for (int i = 0; i < textures.Count; i++)
        {
            RenderTexture.active = rt;
            Graphics.Blit(textures[i], rt);
            tmp.ReadPixels(new Rect(0, 0, texWidth, texHeight), 0, 0);
            tmp.Apply();
            AlbedoTextures.SetPixels(tmp.GetPixels(0), i, 0);
        }
        AlbedoTextures.Apply();
        RenderTexture.active = null;
        UnityEngine.Object.Destroy(rt);
        UnityEngine.Object.Destroy(tmp);
    }

    private static void DestroyAlbedoTexture()
    {
        if (AlbedoTextures != null) UnityEngine.Object.Destroy(AlbedoTextures);
        AlbedoTextures = null;
    }
}
