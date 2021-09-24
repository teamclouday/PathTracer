using System;
using System.Threading;
using UnityEngine;
using Unity.Collections;
using oidn;

public class Denoise
{
    private IntPtr _device;

    public static object ThreadLock = new object();
    public bool IsRunning = false;

    private Texture2D CopyTexture;
    private Texture2D CopyNormalTexture;
    private Texture2D CopyAlbedoTexture;
    public Texture2D FilteredTexture;

    public bool FilteredOnce = false;

    public Denoise(int width, int height)
    {
        _device = OIDN_API.oidnNewDevice(OIDNDeviceType.OIDN_DEVICE_TYPE_DEFAULT);
        OIDN_API.oidnCommitDevice(_device);
        // init texture
        ValidateTexture(width, height);
    }

    public void Destroy()
    {
        while(IsRunning)
        {
            Thread.Sleep(1);
        }
        OIDN_API.oidnReleaseDevice(_device);
        if (FilteredTexture != null) UnityEngine.Object.Destroy(FilteredTexture);
        if (CopyTexture != null) UnityEngine.Object.Destroy(CopyTexture);
        if (CopyNormalTexture != null) UnityEngine.Object.Destroy(CopyNormalTexture);
        if (CopyAlbedoTexture != null) UnityEngine.Object.Destroy(CopyAlbedoTexture);
    }

    public void Filter(RenderTexture converged, RenderTexture albedo, RenderTexture normal)
    {
        if (IsRunning) return;
        // validate texture
        ValidateTexture(converged.width, converged.height);
        // copy texture
        RenderTexture.active = converged;
        CopyTexture.ReadPixels(new Rect(0, 0, converged.width, converged.height), 0, 0);
        CopyTexture.Apply();
        // copy other textures
        if (!FilteredOnce)
            UpdateInfo(albedo, normal);
        // start running oidn in another thread
        var _imgIn = CopyTexture.GetRawTextureData<float>();
        var _imgOut = FilteredTexture.GetRawTextureData<float>();
        var _imgNormal = CopyNormalTexture.GetRawTextureData<float>();
        var _imgAlbedo = CopyAlbedoTexture.GetRawTextureData<float>();
        //Debug.Log(array.Length);
        //NativeArray<float>.Copy(array, _imgIn);
        uint width = (uint)CopyTexture.width;
        uint height = (uint)CopyTexture.height;
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
    }

    public void UpdateTexture()
    {
        FilteredTexture.Apply();
    }

    private void UpdateInfo(RenderTexture albedo, RenderTexture normal)
    {
        RenderTexture.active = albedo;
        CopyAlbedoTexture.ReadPixels(new Rect(0, 0, albedo.width, albedo.height), 0, 0);
        CopyAlbedoTexture.Apply();
        RenderTexture.active = normal;
        CopyNormalTexture.ReadPixels(new Rect(0, 0, normal.width, normal.height), 0, 0);
        CopyNormalTexture.Apply();
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
            if (CopyTexture != null)
                UnityEngine.Object.Destroy(CopyTexture);
            CopyTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            if (CopyNormalTexture != null)
                UnityEngine.Object.Destroy(CopyNormalTexture);
            CopyNormalTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
            if (CopyAlbedoTexture != null)
                UnityEngine.Object.Destroy(CopyAlbedoTexture);
            CopyAlbedoTexture = new Texture2D(width, height, TextureFormat.RGBAFloat, false);
        }
        
    }
}
