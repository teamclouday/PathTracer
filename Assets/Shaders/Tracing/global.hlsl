#ifndef TRACER_GLOBAL
#define TRACER_GLOBAL

// frame target
RWTexture2D<float4> _FrameTarget;

// camera transformation matrix
//float4x4 _CameraToWorld;
//float4x4 _CameraProjInv;
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

struct TLASNode
{
    float3 boundMax;
    float3 boundMin;
    int transformIdx;
    int rootIdx;
};
StructuredBuffer<TLASNode> _TNodes;

struct MaterialData
{
    float3 color;
    float3 emission;
    float metallic;
    float smoothness;
    float mode;
    int albedoIdx;
};
StructuredBuffer<MaterialData> _Materials;

StructuredBuffer<float3> _Vertices;
StructuredBuffer<int> _Indices;
StructuredBuffer<float3> _Normals;
StructuredBuffer<float2> _UVs;
StructuredBuffer<float4x4> _Transforms;

Texture2DArray<float4> _AlbedoTextures;
SamplerState sampler_AlbedoTextures;

// current pixel center
//float2 PixelCenter;
uint4 RandomSeed;

// define PI value
const float PI =        3.14159265358979323846;
const float PI_TWO =    6.28318530717958623198;
#endif