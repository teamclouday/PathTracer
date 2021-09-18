using UnityEngine;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Mesh info for each object
/// </summary>
public struct MeshData
{
    public Matrix4x4 LocalToWorld;
    public int IndicesStart;
    public int IndicesCount;
    public int MaterialIdx;

    public static int TypeSize = sizeof(float)*16+sizeof(int)*3;
}

public struct MaterialData
{
    public Vector3 Color;
    public Vector3 Emission;
    public float Smoothness;
    public float Metallic;

    public static int TypeSize = sizeof(float)*3*2+sizeof(float)*2;
}

/// <summary>
/// Bounding volume hierarchy
/// </summary>
public struct BVH
{

}

/// <summary>
/// Global Object Manager
/// </summary>
public class ObjectManager
{
    private static readonly List<GameObject> objects = new List<GameObject>();
    private static readonly List<MeshData> meshes = new List<MeshData>();
    private static readonly List<Vector3> vertices = new List<Vector3>();
    private static readonly List<Vector3> normals = new List<Vector3>();
    private static readonly List<MaterialData> materials = new List<MaterialData>();

    private static readonly List<int> indices = new List<int>();

    public static ComputeBuffer MeshBuffer;
    public static ComputeBuffer VertexBuffer;
    public static ComputeBuffer IndexBuffer;
    public static ComputeBuffer NormalBuffer;
    public static ComputeBuffer MaterialBuffer;

    private static bool objectUpdated = false;

    public static void RegisterObject(GameObject o)
    {
        objects.Add(o);
        objectUpdated = true;
    }

    public static void UnregisterObject(GameObject o)
    {
        objects.Remove(o);
        objectUpdated = true;
    }

    public static bool Validate()
    {
        foreach (GameObject obj in objects)
        {
            if(obj.transform.hasChanged)
            {
                objectUpdated = true;
                obj.transform.hasChanged = false;
            }
            if (obj.transform.parent.transform.hasChanged)
            {
                objectUpdated = true;
                obj.transform.parent.transform.hasChanged = false;
            }
        }
        if (!objectUpdated) return false;

        meshes.Clear();
        vertices.Clear();
        indices.Clear();
        normals.Clear();
        materials.Clear();
        // add default material if submesh does not have a material
        materials.Add(new MaterialData()
        {
            Color = new Vector3(1.0f, 1.0f, 1.0f), // white color by default
            Metallic = 0.0f,
            Smoothness = 0.0f
        });
        foreach(GameObject obj in objects)
        {
            // process all materials
            Material[] mats = obj.GetComponent<Renderer>().sharedMaterials;
            int matStart = materials.Count;
            int matCount = mats.Length;
            foreach(Material mat in mats)
            {
                materials.Add(new MaterialData()
                {
                    Color = ColorToVector(mat.color),
                    Emission = mat.IsKeywordEnabled("_EMISSION") ? ColorToVector(mat.GetColor("_EmissionColor")) : Vector3.zero,
                    Metallic = mat.GetFloat("_Metallic"), // here I assume it is standard unity shader
                    Smoothness = mat.GetFloat("_Glossiness")
                });
            }
            // process mesh data
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            int vertexStart = vertices.Count;
            vertices.AddRange(mesh.vertices);
            int count = 0;
            for(int i = 0; i < mesh.subMeshCount; i++)
            {
                int indexStart = indices.Count;
                var submesh = mesh.GetIndices(i);
                indices.AddRange(submesh.Select(idx => idx + vertexStart));
                meshes.Add(new MeshData()
                {
                    LocalToWorld = obj.transform.parent.localToWorldMatrix,
                    IndicesStart = indexStart,
                    IndicesCount = submesh.Length,
                    MaterialIdx = i < matCount ? i + matStart : 0 // if index is not valid, no material, then use default one
                });
                count += submesh.Length;
            }
            normals.AddRange(mesh.normals);
            if (mesh.vertices.Length != mesh.normals.Length)
                Debug.LogError("Object " + obj.name + " has different normals and vertices size");
            
            Debug.Log("Object " + obj.name + 
                " loaded (vertices = " + (vertices.Count - vertexStart) +
                ", indices = " + count + ")"
            );
        }

        UpdateBuffer(ref MeshBuffer, meshes, MeshData.TypeSize);
        UpdateBuffer(ref IndexBuffer, indices, sizeof(int));
        UpdateBuffer(ref VertexBuffer, vertices, sizeof(float) * 3);
        UpdateBuffer(ref NormalBuffer, normals, sizeof(float) * 3);
        UpdateBuffer(ref MaterialBuffer, materials, MaterialData.TypeSize);

        objectUpdated = false;
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
        if (MeshBuffer != null) MeshBuffer.Release();
        if (IndexBuffer != null) IndexBuffer.Release();
        if (VertexBuffer != null) VertexBuffer.Release();
        if (NormalBuffer != null) NormalBuffer.Release();
        if (MaterialBuffer != null) MaterialBuffer.Release();
    }

    private static Vector3 ColorToVector(Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }
}
