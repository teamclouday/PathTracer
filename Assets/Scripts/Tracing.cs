using UnityEngine;
using System.IO;

/// <summary>
/// Main body of ray tracing
/// </summary>
public class Tracing : MonoBehaviour
{
    [SerializeField]
    ComputeShader RayTracingShader;

    [SerializeField]
    Texture SkyboxTexture;

    [SerializeField]
    Light DirectionalLight;

    [SerializeField, Range(1, 10)]
    int TraceDepth = 5;


    private RenderTexture frameTarget;
    private RenderTexture frameConverged;

    private Camera mainCamera;

    private uint sampleCount;
    private Material collectMaterial;

    private int dispatchGroupX, dispatchGroupY;

    private Vector4 directionalLightInfo;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        // check if textures are ready
        ValidateTextures();
        // check if object buffers are ready
        ValidateObjects();
        // set shader parameters
        SetShaderParameters();
        // dispatch and generate frame
        RayTracingShader.Dispatch(0, dispatchGroupX, dispatchGroupY, 1);
        // update frames
        Graphics.Blit(frameTarget, frameConverged, collectMaterial);
        Graphics.Blit(frameConverged, destination);
        // update sample count
        sampleCount++;
    }

    private void SetShaderParameters()
    {
        // set sample count in collect shader
        collectMaterial.SetFloat("_SampleCount", sampleCount);
        // set frame target
        RayTracingShader.SetTexture(0, "_FrameTarget", frameTarget);
        // set camera matrix
        RayTracingShader.SetMatrix("_CameraToWorld", mainCamera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraProjInv", mainCamera.projectionMatrix.inverse);
        // set skybox
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        // random pixel offset
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        // set directional light
        RayTracingShader.SetVector("_DirectionalLight", directionalLightInfo);
        // trace depth
        RayTracingShader.SetInt("_TraceDepth", TraceDepth);
        // random seed
        RayTracingShader.SetFloat("_Seed", Random.value);
        // set objects info
        //if (ObjectManager.MeshBuffer != null) RayTracingShader.SetBuffer(0, "_Meshes", ObjectManager.MeshBuffer);
        if (ObjectManager.VertexBuffer != null) RayTracingShader.SetBuffer(0, "_Vertices", ObjectManager.VertexBuffer);
        if (ObjectManager.IndexBuffer != null) RayTracingShader.SetBuffer(0, "_Indices", ObjectManager.IndexBuffer);
        if (ObjectManager.NormalBuffer != null) RayTracingShader.SetBuffer(0, "_Normals", ObjectManager.NormalBuffer);
        if (ObjectManager.MaterialBuffer != null) RayTracingShader.SetBuffer(0, "_Materials", ObjectManager.MaterialBuffer);
        if (ObjectManager.NodeBuffer != null) RayTracingShader.SetBuffer(0, "_Nodes", ObjectManager.NodeBuffer);
    
    }

    private void ValidateTextures()
    {
        // if frame target is not initialized
        // or screen size has changed
        // reinitialize
        if(frameTarget == null ||
            frameTarget.width != Screen.width ||
            frameTarget.height != Screen.height)
        {
            if (frameTarget != null)
                frameTarget.Release();
            frameTarget = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            frameTarget.enableRandomWrite = true;
            frameTarget.Create();
            dispatchGroupX = Mathf.CeilToInt(Screen.width / 8.0f);
            dispatchGroupY = Mathf.CeilToInt(Screen.height / 8.0f);
        }
        // same for frame converged
        if (frameConverged == null ||
            frameConverged.width != Screen.width ||
            frameConverged.height != Screen.height)
        {
            if (frameConverged != null)
                frameConverged.Release();
            frameConverged = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            frameConverged.enableRandomWrite = true;
            frameConverged.Create();
        }
    }

    private void ValidateObjects()
    {
        // validate objects in the scene
        // and update compute buffers
        if (ObjectManager.Validate())
            sampleCount = 0;
    }

    private void Awake()
    {
        // get main camera in the scene
        mainCamera = GetComponent<Camera>();
        // set collect material
        if (collectMaterial == null)
            collectMaterial = new Material(Shader.Find("Hidden/Collect"));
        // set directional light info
        UpdateDirectionalLight();
    }

    private void Start()
    {
        // reduce framerate and gpu workload, hopefully
        Application.targetFrameRate = 72;
        sampleCount = 0;
        //Random.InitState(12345);
    }

    private void Update()
    {
        // if current transform has changed, resample
        if (transform.hasChanged)
        {
            sampleCount = 0;
            transform.hasChanged = false;
        }
        // check for directional light
        if(DirectionalLight.transform.hasChanged)
        {
            sampleCount = 0;
            UpdateDirectionalLight();
            DirectionalLight.transform.hasChanged = false;
        }
        // press ESC to exit program
        if(Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
        // press ctrl + X to save screenshot
        if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("Screenshot: " + Path.Combine(Application.dataPath, "ScreenShot_S" + sampleCount + ".png"));
            ScreenCapture.CaptureScreenshot(
                Path.Combine(Application.dataPath, "ScreenShot_S" + sampleCount + ".png")
            );
        }
    }

    private void OnDestroy()
    {
        ObjectManager.Destroy();
        if(frameTarget != null) frameTarget.Release();
        if(frameConverged != null) frameConverged.Release();
    }

    private void UpdateDirectionalLight()
    {
        Vector3 dir = DirectionalLight.transform.forward;
        directionalLightInfo = new Vector4(
            -dir.x, -dir.y, -dir.z,
            DirectionalLight.intensity
        );
    }
}
