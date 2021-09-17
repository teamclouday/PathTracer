using UnityEngine;

// add this script as component of the parent of all sub-objects
public class Object : MonoBehaviour
{
    private void OnEnable()
    {
        for(int i = 0; i < transform.childCount; i++)
        {
            GameObject obj = transform.GetChild(i).gameObject;
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
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject obj = transform.GetChild(i).gameObject;
            if (obj.GetComponent<MeshFilter>() != null &&
                obj.GetComponent<Renderer>() != null)
            {
                ObjectManager.UnregisterObject(obj);
            }
        }
    }
}
