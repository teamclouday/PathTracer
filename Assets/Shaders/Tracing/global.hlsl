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
float3 _DirectionalLight;
float4 _DirectionalLightColor;

// point lights info
StructuredBuffer<float4> _PointLights;
int _PointLightsCount;

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
    float ior;
    float mode;
    int albedoIdx;
    int emitIdx;
    int metalIdx;
    int normIdx;
    int roughIdx;
};
StructuredBuffer<MaterialData> _Materials;

StructuredBuffer<float3> _Vertices;
StructuredBuffer<int> _Indices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<float4> _Tangents;
StructuredBuffer<float2> _UVs;
StructuredBuffer<float4x4> _Transforms;

Texture2DArray<float4> _AlbedoTextures;
SamplerState sampler_AlbedoTextures;
Texture2DArray<float4> _EmitTextures;
SamplerState sampler_EmitTextures;
Texture2DArray<float4> _MetallicTextures;
SamplerState sampler_MetallicTextures;
Texture2DArray<float4> _NormalTextures;
SamplerState sampler_NormalTextures;
Texture2DArray<float4> _RoughnessTextures;
SamplerState sampler_RoughnessTextures;

// current pixel center
//float2 PixelCenter;
uint4 RandomSeed;

// define PI value
#define PI              3.14159265358979323846
#define PI_TWO          6.28318530717958623198

#define LUM             0.33333333333333333333
#endif