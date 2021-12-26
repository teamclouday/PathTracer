#ifndef TRACER_GLOBAL
#define TRACER_GLOBAL

// frame target
RWTexture2D<float4> _FrameTarget;

// camera info
float3 _CameraPos;
float3 _CameraUp;
float3 _CameraRight;
float3 _CameraForward;
float4 _CameraInfo; // fov scale, focal distance, aperture, height / width ratio

// skybox texture
TextureCube<float4> _SkyboxTexture;
SamplerState sampler_SkyboxTexture;

// skybox intensity
float _SkyboxIntensity;

// random offset in pixel
float2 _PixelOffset;

// directional light info
float4 _DirectionalLight;

// trace depth
int _TraceDepth;

// random seed
//float _Seed;
int _FrameCount;

// object info
struct MeshData
{
    //float4x4 localToWorld;
    int indicesStart;
    int indicesCount;
    int materialIdx;
};
//StructuredBuffer<MeshData> _Meshes;

struct BLASNode
{
    float3 boundMax;
    float3 boundMin;
    int faceStartIdx;
    int faceEndIdx;
    int materialIdx;
    int childIdx;
};
StructuredBuffer<BLASNode> _BNodes;

struct TLASNodeRaw
{
    float3 boundMax;
    float3 boundMin;
    int transformIdx;
    int rootIdx;
};
StructuredBuffer<TLASNodeRaw> _TNodesRaw;

struct TLASNode
{
    float3 boundMax;
    float3 boundMin;
    int rawNodeStartIdx;
    int rawNodeEndIdx;
    int childIdx;
};
StructuredBuffer<TLASNode> _TNodes;

struct MaterialData
{
    float4 color;
    float3 emission;
    float metallic;
    float smoothness;
    float mode;
    int albedoIdx;
    int emitIdx;
    int metalIdx;
};
StructuredBuffer<MaterialData> _Materials;

StructuredBuffer<float3> _Vertices;
StructuredBuffer<int> _Indices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<float2> _UVs;
StructuredBuffer<float4x4> _Transforms;

Texture2DArray<float4> _AlbedoTextures;
SamplerState sampler_AlbedoTextures;
Texture2DArray<float4> _EmitTextures;
SamplerState sampler_EmitTextures;
Texture2DArray<float4> _MetallicTextures;
SamplerState sampler_MetallicTextures;

// current pixel center
//float2 PixelCenter;
uint4 RandomSeed;

// define PI value
#define PI              3.14159265358979323846
#define PI_TWO          6.28318530717958623198
// define sRGB conversion value
#define SRGB_CONVERT    0.45454545454545454545
#endif