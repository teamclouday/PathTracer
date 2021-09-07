using UnityEngine;

public class RayTracer : MonoBehaviour
{
    [SerializeField]
    ComputeShader RayTracingShader;

    [SerializeField]
    Texture SkyboxTexture;

    private RenderTexture _target;

    private Camera _camera;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        Render(destination);
    }

    private void Render(RenderTexture destination)
    {
        InitRenderTexture();
        RayTracingShader.SetTexture(0, "Result", _target);
        SetShaderParameters();
        int threadGroupX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupY = Mathf.CeilToInt(Screen.height / 8.0f);
        RayTracingShader.Dispatch(0, threadGroupX, threadGroupY, 1);
        Graphics.Blit(_target, destination);
    }

    private void InitRenderTexture()
    {
        if(_target == null ||
            _target.width != Screen.width ||
            _target.height != Screen.height)
        {
            if (_target != null)
                _target.Release();
            _target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _target.enableRandomWrite = true;
            _target.Create();
        }
    }
}
