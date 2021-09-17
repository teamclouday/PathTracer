using UnityEngine;

public struct Sphere
{
    public Vector3 Position;
    public float Radius;
    public Vector3 Emission;
    public float Smoothness;
    public Vector3 Albedo;
    public Vector3 Specular;
    public static readonly int Size = 56; // structure size
}

public struct MeshObject
{
    public Matrix4x4 localToWorldMatrix;
    public int indices_offset;
    public int indices_count;
}