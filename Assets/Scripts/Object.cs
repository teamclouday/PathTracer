using UnityEngine;
using System.Collections.Generic;

// add this script as component of the parent of all sub-objects
public class Object : MonoBehaviour
{
    private void OnEnable()
    {
        foreach(Transform sub in transform)
        {
            GameObject obj = sub.gameObject;
            bool skipped = false;
            // a valid gameobject should have:
            // 1. mesh filter (for vertices data)
            // 2. renderer (for materials)
            // 3. topology is triangles
            if (obj.GetComponent<MeshFilter>() != null &&
                obj.GetComponent<Renderer>() != null)
            {
                var mesh = obj.GetComponent<MeshFilter>().sharedMesh;
                bool valid = true;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    if (mesh.GetTopology(i) != MeshTopology.Triangles)
                    {
                        valid = false;
                        break;
                    }
                }
                if (valid) ObjectManager.RegisterObject(obj);
                else skipped = true;
            }
            else skipped = true;
            if (skipped) Debug.Log("Skipped object " + obj.name + " because it has invalid layout");
        }
    }

    private void OnDisable()
    {
        foreach (Transform sub in transform)
        {
            GameObject obj = sub.gameObject;
            if (obj.GetComponent<MeshFilter>() != null &&
                obj.GetComponent<Renderer>() != null)
            {
                ObjectManager.UnregisterObject(obj);
            }
        }
    }
}
