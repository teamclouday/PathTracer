#ifndef TRACER_GLOBAL
#define TRACER_GLOBAL

// frame target
RWTexture2D<float4> _FrameTarget;

// camera transformation matrix
float4x4 _CameraToWorld;
float4x4 _CameraProjInv;

// skybox texture
TextureCube<float4> _SkyboxTexture;
SamplerState sampler_SkyboxTexture;

// directional light
float4 _DirectionalLight;

// random offset in pixel
float2 _PixelOffset;

// trace depth
int _TraceDepth;

// random seed
float _Seed;

// object info
struct MeshData
{
    float4x4 localToWorld;
    int indicesStart;
    int indicesCount;
    int materialIdx;
};
StructuredBuffer<MeshData> _Meshes;

struct MaterialData
{
    float3 color;
    float metallic;
    float smoothness;
};
StructuredBuffer<MaterialData> _Materials;

StructuredBuffer<float3> _Vertices;
StructuredBuffer<int> _Indices;
StructuredBuffer<float3> _Normals;

// current pixel center
float2 PixelCenter;

// define PI value
const float PI = 3.141592653589793;
#endif