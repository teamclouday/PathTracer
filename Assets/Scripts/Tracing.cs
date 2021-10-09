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
    ComputeShader InfoShader;

    [SerializeField]
    Texture SkyboxTexture;

    [SerializeField]
    Light DirectionalLight;

    [SerializeField, Range(2, 20)]
    int TraceDepth = 5;

    [SerializeField, Range(0.01f, 50.0f)]
    float CameraFocalDistance = 1.0f;

    [SerializeField, Range(0.0f, 10.0f)]
    float CameraAperture = 0.0f;

    [SerializeField]
    bool EnableDenoiser = false;

    [SerializeField, Range(10, 200)]
    int DenoiserStartSamples = 100;

    public static bool ComputeLock = false;

    private RenderTexture frameTarget;
    private RenderTexture frameConverged;
    private RenderTexture denoiseNormal;
    private RenderTexture denoiseAlbedo;

    private Camera mainCamera;

    private uint sampleCount;
    private Material collectMaterial;

    private int dispatchGroupX, dispatchGroupY;

    private Vector4 directionalLightInfo;

    private Denoise denoiser;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        GetSceneInfo();
        Render(destination);
    }

    private void GetSceneInfo()
    {
        if (sampleCount != DenoiserStartSamples || !EnableDenoiser) return;
        ValidateTextures();
        SetShaderParameters(InfoShader, DenoiserStartSamples);
        InfoShader.SetTexture(0, "_FrameTarget", denoiseAlbedo);
        InfoShader.SetTexture(0, "_FrameNormalTarget", denoiseNormal);
        InfoShader.Dispatch(0, dispatchGroupX, dispatchGroupY, 1);
        Debug.Log("Scene Info fetched");
    }

    private void Render(RenderTexture destination)
    {
        // check if textures are ready
        ValidateTextures();
        // set shader parameters
        SetShaderParameters(RayTracingShader, 0);
        // set frame target
        RayTracingShader.SetTexture(0, "_FrameTarget", frameTarget);
        // set sample count in collect shader
        collectMaterial.SetFloat("_SampleCount", sampleCount);
        // dispatch and generate frame
        RayTracingShader.Dispatch(0, dispatchGroupX, dispatchGroupY, 1);
        // update frames
        Graphics.Blit(frameTarget, frameConverged, collectMaterial);
        if(EnableDenoiser && denoiser != null && sampleCount > DenoiserStartSamples)
        {
            if(denoiser.FilteredOnce)
            {
                if (!denoiser.IsRunning)
                {
                    denoiser.UpdateTexture();
                    Graphics.Blit(denoiser.FilteredTexture, destination);
                    denoiser.Filter(frameConverged, denoiseAlbedo, denoiseNormal);
                }
                else
                    Graphics.Blit(denoiser.FilteredTexture, destination);
            }
            else
            {
                denoiser.Filter(frameConverged, denoiseAlbedo, denoiseNormal);
                Graphics.Blit(frameConverged, destination);
            }    

        }
        else
            Graphics.Blit(frameConverged, destination);
        // update sample count
        sampleCount++;
    }

    private void SetShaderParameters(ComputeShader shader, int targetCount)
    {
        // random pixel offset
        shader.SetVector("_PixelOffset", GeneratePixelOffset());
        // trace depth
        shader.SetInt("_TraceDepth", ComputeLock ? 2 : TraceDepth);
        // random seed
        //shader.SetFloat("_Seed", Random.value);
        // frame count
        shader.SetInt("_FrameCount", (int)sampleCount);
        // only update these parameters if redraw
        if(sampleCount == targetCount)
        {
            // set camera info
            shader.SetVector("_CameraPos", mainCamera.transform.position);
            shader.SetVector("_CameraUp", mainCamera.transform.up);
            shader.SetVector("_CameraRight", mainCamera.transform.right);
            shader.SetVector("_CameraForward", mainCamera.transform.forward);
            shader.SetVector("_CameraInfo", new Vector4(
                Mathf.Deg2Rad * mainCamera.fieldOfView,
                CameraFocalDistance,
                CameraAperture,
                frameTarget.height / (float)frameTarget.width
            ));
            // set skybox
            shader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
            // set directional light
            shader.SetVector("_DirectionalLight", directionalLightInfo);
            // set objects info
            if (ObjectManager.VertexBuffer != null) shader.SetBuffer(0, "_Vertices", ObjectManager.VertexBuffer);
            if (ObjectManager.IndexBuffer != null) shader.SetBuffer(0, "_Indices", ObjectManager.IndexBuffer);
            if (ObjectManager.NormalBuffer != null) shader.SetBuffer(0, "_Normals", ObjectManager.NormalBuffer);
            if (ObjectManager.MaterialBuffer != null) shader.SetBuffer(0, "_Materials", ObjectManager.MaterialBuffer);
            if (ObjectManager.TLASBuffer != null) shader.SetBuffer(0, "_TNodes", ObjectManager.TLASBuffer);
            if (ObjectManager.BLASBuffer != null) shader.SetBuffer(0, "_BNodes", ObjectManager.BLASBuffer);
            if (ObjectManager.TransformBuffer != null) shader.SetBuffer(0, "_Transforms", ObjectManager.TransformBuffer);

        }
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
        // also for normal texture
        if (denoiseNormal == null ||
            denoiseNormal.width != Screen.width ||
            denoiseNormal.height != Screen.height)
        {
            if (denoiseNormal != null)
                denoiseNormal.Release();
            denoiseNormal = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            denoiseNormal.enableRandomWrite = true;
            denoiseNormal.Create();
        }
        // for albedo texture
        if (denoiseAlbedo == null ||
            denoiseAlbedo.width != Screen.width ||
            denoiseAlbedo.height != Screen.height)
        {
            if (denoiseAlbedo != null)
                denoiseAlbedo.Release();
            denoiseAlbedo = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            denoiseAlbedo.enableRandomWrite = true;
            denoiseAlbedo.Create();
        }
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
        ResetSamples();
        //Random.InitState(12345);
        // set up denoiser
        denoiser = new Denoise(Screen.width, Screen.height);
    }

    private void OnValidate()
    {
        ResetSamples();
    }

    private void Update()
    {
        // if current transform has changed, resample
        if (transform.hasChanged)
        {
            ResetSamples();
            transform.hasChanged = false;
        }
        // check for directional light
        if(DirectionalLight.transform.hasChanged)
        {
            ResetSamples();
            UpdateDirectionalLight();
            DirectionalLight.transform.hasChanged = false;
        }
        if (ObjectManager.Validate()) ResetSamples();
        // press ESC to exit program
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
        // press ctrl + X to save screenshot
        if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.X))
        {
            Debug.Log("Screenshot: " + Path.Combine(Application.dataPath, "ScreenShot_S" + sampleCount + ".png"));
            ScreenCapture.CaptureScreenshot(
                Path.Combine(Application.dataPath, "ScreenShot_S" + sampleCount + ".png")
            );
        }
        // press ctrl + V to toggle denoise
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.V))
        {
            EnableDenoiser = !EnableDenoiser;
            Debug.Log("Denoiser " + (EnableDenoiser ? "enabled" : "disabled"));
        }
    }

    private void OnDestroy()
    {
        ObjectManager.Destroy();
        denoiser.Destroy();
        if(frameTarget != null) frameTarget.Release();
        if(frameConverged != null) frameConverged.Release();
        if(denoiseAlbedo != null) denoiseAlbedo.Release();
        if(denoiseNormal != null) denoiseNormal.Release();
    }

    private void UpdateDirectionalLight()
    {
        Vector3 dir = DirectionalLight.transform.forward;
        directionalLightInfo = new Vector4(
            -dir.x, -dir.y, -dir.z,
            DirectionalLight.intensity
        );
    }

    private Vector2 GeneratePixelOffset()
    {
        //Vector2 offset;
        //float r1 = 2.0f * Random.value;
        //float r2 = 2.0f * Random.value;
        //// reference: https://github.com/knightcrawler25/GLSL-PathTracer/blob/master/src/shaders/preview.glsl
        //offset.x = r1 < 1.0f ? Mathf.Sqrt(r1) - 1.0f : 1.0f - Mathf.Sqrt(2.0f - r1);
        //offset.y = r2 < 1.0f ? Mathf.Sqrt(r2) - 1.0f : 1.0f - Mathf.Sqrt(2.0f - r2);
        //offset.x += 1.0f;
        //offset.y += 1.0f;
        //offset *= 0.5f;
        //return offset;
        return new Vector2(Random.value, Random.value);
    }

    private void ResetSamples()
    {
        sampleCount = 0;
        if (denoiser != null) denoiser.FilteredOnce = false;
    }
}
