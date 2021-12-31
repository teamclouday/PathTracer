using System;
using System.Threading;
using UnityEngine;
using oidn;

public enum DenoiserType
{
    Offline,
    Realtime
}

[Serializable]
public class DenoiseCoeff
{
    [Min(0.0f)]
    public float Color;

    [Min(0.0f)]
    public float Normal;

    [Min(0.0f)]
    public float Depth;

    [Min(1)]
    public int Strength;
}

public class Denoise
{
    private IntPtr _device;

    public static object ThreadLock = new object();
    public bool IsRunning = false;

    public Texture2D _copyTexture;
    public Texture2D _copyNormalTexture;
    public Texture2D _copyAlbedoTexture;
    private Material denoiseMaterial;
    public Texture2D FilteredTexture;


    public bool FilteredOnce = false;

    public Denoise(int width, int height)
    {
        _device = OIDN_API.oidnNewDevice(OIDNDeviceType.OIDN_DEVICE_TYPE_DEFAULT);
        OIDN_API.oidnCommitDevice(_device);
        // init texture
        ValidateTexture(width, height);
        // find shader
        denoiseMaterial = new Material(Shader.Find("Hidden/Denoise"));
    }

    public void Destroy()
    {
        while(IsRunning)
        {
            Thread.Sleep(1);
        }
        OIDN_API.oidnReleaseDevice(_device);
        if (FilteredTexture != null) UnityEngine.Object.Destroy(FilteredTexture);
        if (_copyTexture != null) UnityEngine.Object.Destroy(_copyTexture);
        if (_copyNormalTexture != null) UnityEngine.Object.Destroy(_copyNormalTexture);
        if (_copyAlbedoTexture != null) UnityEngine.Object.Destroy(_copyAlbedoTexture);
    }

    public void Filter(DenoiserType type, RenderTexture converged,
        RenderTexture albedo, RenderTexture normal, RenderTexture destination,
        DenoiseCoeff coeff)
    {
        if (IsRunning) return;
        // validate texture
        ValidateTexture(converged.width, converged.height);
        switch (type)
        {
            case DenoiserType.Offline:
            {
                // copy texture
                RenderTexture.active = converged;
                _copyTexture.ReadPixels(new Rect(0, 0, converged.width, converged.height), 0, 0);
                _copyTexture.Apply();
                // copy other textures
                if (!FilteredOnce)
                    UpdateInfo(albedo, normal);
                // start running oidn in another thread
                var _imgIn = _copyTexture.GetRawTextureData<float>();
                var _imgOut = FilteredTexture.GetRawTextureData<float>();
                var _imgNormal = _copyNormalTexture.GetRawTextureData<float>();
                var _imgAlbedo = _copyAlbedoTexture.GetRawTextureData<float>();
                //Debug.Log(array.Length);
                //NativeArray<float>.Copy(array, _imgIn);
                uint width = (uint)_copyTexture.width;
                uint height = (uint)_copyTexture.height;
                IsRunning = true;
                new Thread(() =>
                {
                    // create filter for ray tracing
                    IntPtr _filter = OIDN_API.oidnNewFilter(_device, "RT");
                    unsafe
                    {
                        var ptrIn = (IntPtr)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(_imgIn);
                        var ptrNormal = (IntPtr)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(_imgNormal);
                        var ptrAlbedo = (IntPtr)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(_imgAlbedo);
                        var ptrOut = (IntPtr)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafePtr(_imgOut);
                        OIDN_API.oidnSetSharedFilterImage(
                            _filter, "color", ptrIn, OIDNFormat.OIDN_FORMAT_FLOAT3,
                            width, height, 0,
                            4 * sizeof(float), 4 * sizeof(float) * width
                        );
                        OIDN_API.oidnSetSharedFilterImage(
                            _filter, "albedo", ptrAlbedo, OIDNFormat.OIDN_FORMAT_FLOAT3,
                            width, height, 0,
                            4 * sizeof(float), 4 * sizeof(float) * width
                        );
                        OIDN_API.oidnSetSharedFilterImage(
                            _filter, "normal", ptrNormal, OIDNFormat.OIDN_FORMAT_FLOAT3,
                            width, height, 0,
                            4 * sizeof(float), 4 * sizeof(float) * width
                        );
                        OIDN_API.oidnSetSharedFilterImage(
                            _filter, "output", ptrOut, OIDNFormat.OIDN_FORMAT_FLOAT3,
                            width, height, 0,
                            4 * sizeof(float), 4 * sizeof(float) * width
                        );
                        OIDN_API.oidnSetFilter1b(_filter, "hdr", true);
                        OIDN_API.oidnCommitFilter(_filter);
                        // filter image
                        OIDN_API.oidnExecuteFilter(_filter);
                        // get any error
                        if (OIDN_API.oidnGetDeviceError(_device, out var message) != OIDNError.OIDN_ERROR_NONE)
                            Debug.Log("OIDN Error: " + message);
                    }
                    OIDN_API.oidnReleaseFilter(_filter);
                    Thread.Sleep(5);
                    IsRunning = false;
                    FilteredOnce = true;
                }).Start();
                break;
            }
            case DenoiserType.Realtime:
            {
                IsRunning = false;
                denoiseMaterial.SetTexture("_NormTex", normal);
                denoiseMaterial.SetVector("_StepSize", new Vector2(1.0f / converged.width, 1.0f / converged.height));
                denoiseMaterial.SetVector("_Coeff", new Vector4(coeff.Color, coeff.Normal, coeff.Depth, coeff.Strength));
                Graphics.Blit(converged, destination, denoiseMaterial);
                FilteredOnce = true;
                break;
            }
        }
        
    }

    private void UpdateInfo(RenderTexture albedo, RenderTexture normal)
    {
        RenderTexture.active = albedo;
        _copyAlbedoTexture.ReadPixels(new Rect(0, 0, albedo.width, albedo.height), 0, 0);
        _copyAlbedoTexture.Apply();
        RenderTexture.active = normal;
        _copyNormalTexture.ReadPixels(new Rect(0, 0, normal.width, normal.height), 0, 0);
        _copyNormalTexture.Apply();
    }

    private void ValidateTexture(int width, int height)
    {
        if(FilteredTexture == null ||
            FilteredTexture.width != width ||
            FilteredTexture.height != height)
        {
            if (FilteredTexture != null)
                UnityEngine.Object.Destroy(FilteredTexture);
            FilteredTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            if (_copyTexture != null)
                UnityEngine.Object.Destroy(_copyTexture);
            _copyTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            if (_copyNormalTexture != null)
                UnityEngine.Object.Destroy(_copyNormalTexture);
            _copyNormalTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            if (_copyAlbedoTexture != null)
                UnityEngine.Object.Destroy(_copyAlbedoTexture);
            _copyAlbedoTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        }
        
    }
}
