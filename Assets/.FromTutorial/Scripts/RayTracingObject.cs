using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshFilter))]
public class RayTracingObject : MonoBehaviour
{
    private void OnEnable()
    {
        RayTracer.RegisterObject(this);
    }

    private void OnDisable()
    {
        RayTracer.UnregisterObject(this);
    }
}
