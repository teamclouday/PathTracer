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

// random offset in pixel
float2 _PixelOffset;

// directional light info
float4 _DirectionalLight;

// trace depth
int _TraceDepth;

// random seed
float _Seed;

// object info
struct MeshData
{
    //float4x4 localToWorld;
    int indicesStart;
    int indicesCount;
    int materialIdx;
};
//StructuredBuffer<MeshData> _Meshes;

struct NodeInfo
{
    float3 boundMax;
    float3 boundMin;
    int faceStartIdx;
    int faceCount;
    int materialIdx;
    int childIdx;
};
StructuredBuffer<NodeInfo> _Nodes;

struct MaterialData
{
    float3 color;
    float3 emission;
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
const float PI = 3.14159265358979323846;
#endif