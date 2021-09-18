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
            // a valid gameobject should have:
            // 1. mesh filter (for vertices data)
            // 2. renderer (for materials)
            if(obj.GetComponent<MeshFilter>() != null &&
                obj.GetComponent<Renderer>() != null)
            {
                ObjectManager.RegisterObject(obj);
            }
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
