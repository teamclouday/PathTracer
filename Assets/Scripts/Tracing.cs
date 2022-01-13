using UnityEngine;
using System.IO;
using System.Collections.Generic;

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
    Light DirectionalLight;

    [SerializeField]
    Light[] PointLights;

    [SerializeField]
    Texture SkyboxTexture;

    [SerializeField, Range(0.0f, 10.0f)]
    float SkyboxIntensity = 1.0f;

    [SerializeField, Range(2, 20)]
    int TraceDepth = 5;

    [SerializeField, Range(0.01f, 100.0f)]
    float CameraFocalDistance = 1.0f;

    [SerializeField, Range(0.0f, 2.0f)]
    float CameraAperture = 0.0f;

    [SerializeField]
    bool EnableDenoiser = false;

    [SerializeField]
    DenoiserType DenoiseType = DenoiserType.Offline;

    [SerializeField]
    DenoiseCoeff DenoiserCoefficients = new DenoiseCoeff()
    {
        Color = 1.0f,
        Normal = 0.5f,
        Depth = 0.3f,
        Strength = 1
    }; // coefficient for real time denoiser

    [SerializeField, Range(10, 200)]
    int DenoiserStartSamples = 50;

    public static bool ComputeLock = false;
    public static bool ComputeLockUpdated = false;

    private RenderTexture frameTarget;
    private RenderTexture frameConverged;
    private RenderTexture denoiseNormal;
    private RenderTexture denoiseAlbedo;

    private Camera mainCamera;

    private int sampleCount;
    private Material collectMaterial, clearMaterial;

    private readonly int dispatchGroupX = 32;
    private readonly int dispatchGroupY = 32;
    private int dispatchGroupXFull, dispatchGroupYFull;
    private Vector2 dispatchOffsetLimit;
    private Vector4 dispatchCount;


    private Vector3 directionalLightInfo;
    private Vector4 directionalLightColorInfo;
    // angles in radians
    private float directionalLightYaw = 0.0f;
    private float directionalLightPitch = 0.0f;
    // point lights
    private int pointLightsCount;
    private ComputeBuffer pointLightsBuffer;

    private Denoise denoiser;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if(ComputeLockUpdated)
        {
            ResetSamples();
            ComputeLockUpdated = false;
        }
        if(ComputeLock)
        {
            Graphics.Blit(source, destination);
        }
        else
        {
            GetSceneInfo();
            Render(destination);
        }
    }

    private void GetSceneInfo(bool force = false)
    {
        if ((!EnableDenoiser || sampleCount % DenoiserStartSamples != 0) && !force) return;
        ValidateTextures();
        SetShaderParameters(InfoShader, 1);
        InfoShader.SetTexture(0, "_FrameTarget", denoiseAlbedo);
        InfoShader.SetTexture(0, "_FrameNormalTarget", denoiseNormal);
        InfoShader.Dispatch(0, dispatchGroupXFull, dispatchGroupYFull, 1);
        Debug.Log("Scene Info fetched (Samples " + sampleCount + ")");
    }

    private void Render(RenderTexture destination)
    {
        // check if textures are ready
        ValidateTextures();
        // set shader parameters
        SetShaderParameters(RayTracingShader, 1000);
        // set frame target
        RayTracingShader.SetTexture(0, "_FrameTarget", frameTarget);
        // set sample count in collect shader
        //collectMaterial.SetFloat("_SampleCount", sampleCount);
        // dispatch and generate frame
        RayTracingShader.Dispatch(0, dispatchGroupX, dispatchGroupY, 1);
        // update frames
        Graphics.Blit(frameTarget, frameConverged, collectMaterial);
        if(EnableDenoiser && denoiser != null && sampleCount > DenoiserStartSamples)
        {
            if (denoiser.FilteredOnce)
            {
                if (!denoiser.IsRunning)
                {
                    denoiser.FilteredTexture.Apply();
                    if (DenoiseType == DenoiserType.Offline)
                        Graphics.Blit(denoiser.FilteredTexture, destination);
                    denoiser.Filter(DenoiseType, frameConverged, denoiseAlbedo, denoiseNormal, destination, DenoiserCoefficients);
                }
                else if (DenoiseType == DenoiserType.Offline)
                    Graphics.Blit(denoiser.FilteredTexture, destination);
            }
            else
            {
                denoiser.Filter(DenoiseType, frameConverged, denoiseAlbedo, denoiseNormal, destination, DenoiserCoefficients);
                Graphics.Blit(frameConverged, destination);
            }
        }
        else
            Graphics.Blit(frameConverged, destination);
        // update sample count
        IncrementDispatchCount();
    }

    private void SetShaderParameters(ComputeShader shader, int targetCount)
    {
        // random pixel offset
        shader.SetVector("_PixelOffset", GeneratePixelOffset());
        // trace depth
        shader.SetInt("_TraceDepth", TraceDepth);
        // frame count
        shader.SetInt("_FrameCount", sampleCount);
        // only update these parameters if redraw
        if (sampleCount % targetCount == 0)
        {
            // set camera info
            shader.SetVector("_CameraPos", mainCamera.transform.position);
            shader.SetVector("_CameraUp", mainCamera.transform.up);
            shader.SetVector("_CameraRight", mainCamera.transform.right);
            shader.SetVector("_CameraForward", mainCamera.transform.forward);
            shader.SetVector("_CameraInfo", new Vector4(
                Mathf.Tan(Mathf.Deg2Rad * mainCamera.fieldOfView * 0.5f),
                CameraFocalDistance,
                CameraAperture,
                frameTarget.width / (float)frameTarget.height
            ));
            // set skybox
            shader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
            // set intensity
            shader.SetFloat("_SkyboxIntensity", SkyboxIntensity);
            // set directional light
            shader.SetVector("_DirectionalLight", directionalLightInfo);
            shader.SetVector("_DirectionalLightColor", directionalLightColorInfo);
            // set point lights
            shader.SetBuffer(0, "_PointLights", pointLightsBuffer);
            shader.SetInt("_PointLightsCount", pointLightsCount);
            // set objects info
            if (ObjectManager.VertexBuffer != null) shader.SetBuffer(0, "_Vertices", ObjectManager.VertexBuffer);
            if (ObjectManager.IndexBuffer != null) shader.SetBuffer(0, "_Indices", ObjectManager.IndexBuffer);
            if (ObjectManager.NormalBuffer != null) shader.SetBuffer(0, "_Normals", ObjectManager.NormalBuffer);
            if (ObjectManager.TangentBuffer != null) shader.SetBuffer(0, "_Tangents", ObjectManager.TangentBuffer);
            if (ObjectManager.UVBuffer != null) shader.SetBuffer(0, "_UVs", ObjectManager.UVBuffer);
            if (ObjectManager.MaterialBuffer != null) shader.SetBuffer(0, "_Materials", ObjectManager.MaterialBuffer);
            if (ObjectManager.TLASBuffer != null) shader.SetBuffer(0, "_TNodes", ObjectManager.TLASBuffer);
            if (ObjectManager.TLASRawBuffer != null) shader.SetBuffer(0, "_TNodesRaw", ObjectManager.TLASRawBuffer);
            if (ObjectManager.BLASBuffer != null) shader.SetBuffer(0, "_BNodes", ObjectManager.BLASBuffer);
            if (ObjectManager.TransformBuffer != null) shader.SetBuffer(0, "_Transforms", ObjectManager.TransformBuffer);
            if (ObjectManager.AlbedoTextures != null) shader.SetTexture(0, "_AlbedoTextures", ObjectManager.AlbedoTextures);
            if (ObjectManager.EmissionTextures != null) shader.SetTexture(0, "_EmitTextures", ObjectManager.EmissionTextures);
            if (ObjectManager.MetallicTextures != null) shader.SetTexture(0, "_MetallicTextures", ObjectManager.MetallicTextures);
            if (ObjectManager.NormalTextures != null) shader.SetTexture(0, "_NormalTextures", ObjectManager.NormalTextures);
            if (ObjectManager.RoughnessTextures != null) shader.SetTexture(0, "_RoughnessTextures", ObjectManager.RoughnessTextures);
        }
    }

    private void EstimateGroups(int width, int height)
    {
        // target dispatch 32x32 groups
        // each group has 8x8 threads
        //int pixels = width * height;
        dispatchGroupXFull = Mathf.CeilToInt(Screen.width / 8.0f);
        dispatchGroupYFull = Mathf.CeilToInt(Screen.height / 8.0f);
        dispatchOffsetLimit = new Vector2(
            width - dispatchGroupX * 8,
            height - dispatchGroupY * 8
        );
        dispatchOffsetLimit = Vector2.Max(dispatchOffsetLimit, Vector2.zero);
        dispatchCount = new Vector4(
            0.0f, 0.0f,
            Mathf.Ceil(width / (float)(dispatchGroupX * 8)),
            Mathf.Ceil(height / (float)(dispatchGroupY * 8))
        );
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
            EstimateGroups(Screen.width, Screen.height);
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
        if (clearMaterial == null)
            clearMaterial = new Material(Shader.Find("Hidden/Clear"));
        // update lights in the scene
        UpdateLights();
        // init directional light pitch and yaw
        var rot = DirectionalLight.transform.eulerAngles;
        directionalLightPitch = -rot.x * Mathf.Deg2Rad;
        directionalLightYaw = 0.5f * Mathf.PI - rot.y * Mathf.Deg2Rad;
    }

    private void Start()
    {
        // init sample counts
        ResetSamples();
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
            UpdateLights();
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
        // press ctrl + R to reload materials
        if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R) && Application.isEditor)
        {
            ObjectManager.ReloadMaterials();
            UpdateLights();
            ResetSamples();
        }
        // control light rotation
        if(Input.GetKey(KeyCode.UpArrow))
        {
            UpdateDirectionalLight(0.0f, 1.0f);
        }
        if(Input.GetKey(KeyCode.DownArrow))
        {
            UpdateDirectionalLight(0.0f, -1.0f);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            UpdateDirectionalLight(-1.0f, 0.0f);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            UpdateDirectionalLight(1.0f, 0.0f);
        }
        // CTRL + C to toggle camera depth of view
        if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.C))
        {
            CameraAperture = (CameraAperture > 0.0f) ? 0.0f : 1.0f;
            ResetSamples();
        }
        if (CameraAperture > 0.0f && Input.GetMouseButtonDown(2))
        {
            // get depth from the scene
            GetSceneInfo(true);
            var lastActive = RenderTexture.active;
            Vector3 pos = Input.mousePosition;
            RenderTexture.active = denoiseNormal;
            Texture2D copyTex = new Texture2D(1, 1, TextureFormat.RGBAFloat, false);
            copyTex.ReadPixels(new Rect(pos.x, denoiseNormal.height - pos.y - 1, 1, 1), 0, 0);
            copyTex.Apply();
            CameraFocalDistance = copyTex.GetPixel(0, 0).a;
            if (float.IsInfinity(CameraFocalDistance))
                CameraFocalDistance = 1.0f;
            Debug.Log("Hit " + CameraFocalDistance);
            RenderTexture.active = lastActive;
            UnityEngine.Object.Destroy(copyTex);
            ResetSamples();
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
        if(pointLightsBuffer != null) pointLightsBuffer.Release();
    }

    private void UpdateLights()
    {
        // record directional light info
        Vector3 dir = DirectionalLight.transform.forward;
        directionalLightInfo = new Vector3(
            -dir.x, -dir.y, -dir.z
        );
        directionalLightInfo = Vector3.Normalize(directionalLightInfo);
        directionalLightColorInfo = new Vector4(
            DirectionalLight.color.r,
            DirectionalLight.color.g,
            DirectionalLight.color.b,
            DirectionalLight.intensity
        );
        // prepare point lights
        if (pointLightsBuffer != null)
            pointLightsBuffer.Release();
        List<Vector4> pointLightsPosColor = new List<Vector4>();
        foreach(Light light in PointLights)
        {
            if (light.type != LightType.Point) continue;
            pointLightsCount++;
            pointLightsPosColor.Add(new Vector4(
                light.transform.position.x,
                light.transform.position.y,
                light.transform.position.z,
                light.range
            ));
            pointLightsPosColor.Add(new Vector4(
                light.color.r,
                light.color.g,
                light.color.b,
                light.intensity
            ));
        }
        // if no point light, insert empty vector to make buffer happy
        if (pointLightsCount == 0)
            pointLightsPosColor.Add(Vector4.zero);
        pointLightsBuffer = new ComputeBuffer(pointLightsPosColor.Count, 4 * sizeof(float));
        pointLightsBuffer.SetData(pointLightsPosColor);
    }

    private void UpdateDirectionalLight(float x, float y)
    {
        // modify directional light rotation
        directionalLightPitch -= x * Mathf.Deg2Rad;
        directionalLightYaw -= y * Mathf.Deg2Rad;
        Vector3 dir = new Vector3(
            Mathf.Cos(directionalLightYaw) * Mathf.Cos(directionalLightPitch),
            Mathf.Sin(directionalLightPitch),
            Mathf.Sin(directionalLightYaw) * Mathf.Cos(directionalLightPitch)
        );
        DirectionalLight.transform.position = Vector3.zero;
        DirectionalLight.transform.LookAt(dir);
        DirectionalLight.transform.hasChanged = true;
    }

    private void ResetSamples()
    {
        sampleCount = 0;
        if (denoiser != null) denoiser.FilteredOnce = false;
        if (clearMaterial != null)
            Graphics.Blit(frameTarget, frameTarget, clearMaterial);
    }

    private Vector2 GeneratePixelOffset()
    {
        // first create offset for camera pixel
        Vector2 offset = new Vector2(Random.value, Random.value);
        // next for dispatch group offset
        //offset += new Vector2(
        //    Mathf.Floor(Random.value * dispatchOffsetLimit.x),
        //    Mathf.Floor(Random.value * dispatchOffsetLimit.y)
        //);
        offset.x += dispatchCount.x * dispatchGroupX * 8;
        offset.y += dispatchCount.y * dispatchGroupY * 8;
        return offset;
    }

    private void IncrementDispatchCount()
    {
        dispatchCount.x += 1.0f;
        if(dispatchCount.x >= dispatchCount.z)
        {
            dispatchCount.x = 0.0f;
            dispatchCount.y += 1.0f;
            if(dispatchCount.y >= dispatchCount.w)
            {
                dispatchCount.x = 0.0f;
                dispatchCount.y = 0.0f;
                sampleCount++;
            }
        }
    }
}
