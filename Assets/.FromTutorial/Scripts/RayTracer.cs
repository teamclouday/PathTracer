using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class RayTracer : MonoBehaviour
{
    [SerializeField]
    ComputeShader RayTracingShader;

    [SerializeField]
    Texture SkyboxTexture;

    [SerializeField]
    Light DirectionalLight;

    [SerializeField]
    Vector2 SphereRadiusMinMax = new Vector2(5.0f, 30.0f);

    [SerializeField]
    uint SphereNum = 10000;

    [SerializeField]
    float SpherePlacementRadius = 100.0f;

    [SerializeField]
    int SphereSeed = 1999;

    private RenderTexture _target;
    private RenderTexture _converged;

    private Camera _camera;

    // anti-aliasing
    private uint _currentSample = 0;
    private Material _addMaterial;

    // spheres
    private ComputeBuffer _sphereBuffer;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
    }

    private void Start()
    {
        Application.targetFrameRate = 72; // 144 / 2
    }

    private void OnEnable()
    {
        _currentSample = 0;
        SetupScene();
    }

    private void OnDisable()
    {
        if (_sphereBuffer != null)
            _sphereBuffer.Release();
    }

    private void Update()
    {
        if(transform.hasChanged)
        {
            _currentSample = 0;
            transform.hasChanged = false;
        }
        if(DirectionalLight.transform.hasChanged)
        {
            _currentSample = 0;
            DirectionalLight.transform.hasChanged = false;
        }
        //if(Input.GetKeyDown(KeyCode.R))
        //{
        //    OnDisable();
        //    OnEnable();
        //}
        if (Input.GetKeyDown(KeyCode.Escape))
            Application.Quit();
    }

    private void SetShaderParameters()
    {
        RayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
        RayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
        RayTracingShader.SetTexture(0, "_SkyboxTexture", SkyboxTexture);
        RayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
        Vector3 lightDir = DirectionalLight.transform.forward;
        RayTracingShader.SetVector("_DirectionalLight",
            new Vector4(lightDir.x, lightDir.y, lightDir.z, DirectionalLight.intensity));
        RayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
        RayTracingShader.SetFloat("_Seed", Random.value);
        SetComputeBuffer("_Spheres", _sphereBuffer);
        SetComputeBuffer("_MeshObjects", _meshObjectBuffer);
        SetComputeBuffer("_Vertices", _vertexBuffer);
        SetComputeBuffer("_Indices", _indexBuffer);
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        RebuildMeshObjectBuffers();
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
        if (_addMaterial == null)
            _addMaterial = new Material(Shader.Find("Hidden/AddShader"));
        _addMaterial.SetFloat("_Sample", _currentSample);
        Graphics.Blit(_target, _converged, _addMaterial);
        Graphics.Blit(_converged, destination);
        _currentSample++;
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
        if(_converged == null ||
            _converged.width != Screen.width ||
            _converged.height != Screen.height)
        {
            if (_converged != null)
                _converged.Release();
            _converged = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            _converged.enableRandomWrite = true;
            _converged.Create();
        }
    }

    private void SetupScene()
    {
        Random.InitState(SphereSeed);
        List<Sphere> spheres = new List<Sphere>();
        for(int i = 0; i < SphereNum; i++)
        {
            Sphere sphere = new Sphere();
            sphere.Radius = Random.Range(SphereRadiusMinMax.x, SphereRadiusMinMax.y);
            Vector2 pos = Random.insideUnitCircle * SpherePlacementRadius;
            sphere.Position = new Vector3(pos.x, sphere.Radius, pos.y);
            bool intersected = false;
            // avoid intersections
            foreach (Sphere other in spheres)
            {
                float dist = sphere.Radius + other.Radius;
                if (Vector3.SqrMagnitude(sphere.Position - other.Position) < dist * dist)
                {
                    intersected = true;
                    break;
                }
            }
            if (intersected) continue;
            // set colors
            Color color = Random.ColorHSV();
            bool metal = Random.value < 0.5f;
            sphere.Albedo = metal ? Vector3.zero : new Vector3(color.r, color.g, color.b);
            sphere.Specular = metal ? new Vector3(color.r, color.g, color.b) : Vector3.one * 0.04f;
            // set emission
            bool emit = Random.value < 0.5f;
            color = Random.ColorHSV();
            sphere.Emission = emit ? new Vector3(color.r, color.g, color.b) : Vector3.zero;
            // set smoothness
            sphere.Smoothness = Random.value;
            spheres.Add(sphere);
        }
        _sphereBuffer = new ComputeBuffer(spheres.Count, Sphere.Size);
        _sphereBuffer.SetData(spheres);
    }

    private static bool _meshObjectsNeedRebuilding = false;
    private static List<RayTracingObject> _rayTracingObjects = new List<RayTracingObject>();

    public static void RegisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Add(obj);
        _meshObjectsNeedRebuilding = true;
    }

    public static void UnregisterObject(RayTracingObject obj)
    {
        _rayTracingObjects.Remove(obj);
        _meshObjectsNeedRebuilding = true;
    }

    private static List<MeshObject> _meshObjects = new List<MeshObject>();
    private static List<Vector3> _vertices = new List<Vector3>();
    private static List<int> _indices = new List<int>();
    private ComputeBuffer _meshObjectBuffer;
    private ComputeBuffer _vertexBuffer;
    private ComputeBuffer _indexBuffer;

    private void RebuildMeshObjectBuffers()
    {
        if (!_meshObjectsNeedRebuilding)
        {
            return;
        }
        _meshObjectsNeedRebuilding = false;
        _currentSample = 0;
        // Clear all lists
        _meshObjects.Clear();
        _vertices.Clear();
        _indices.Clear();
        // Loop over all objects and gather their data
        foreach (RayTracingObject obj in _rayTracingObjects)
        {
            Debug.Log(obj);
            Mesh mesh = obj.GetComponent<MeshFilter>().sharedMesh;
            // Add vertex data
            int firstVertex = _vertices.Count;
            _vertices.AddRange(mesh.vertices);
            // Add index data - if the vertex buffer wasn't empty before, the
            // indices need to be offset
            int firstIndex = _indices.Count;
            var indices = mesh.GetIndices(0);
            _indices.AddRange(indices.Select(index => index + firstVertex));
            // Add the object itself
            _meshObjects.Add(new MeshObject()
            {
                localToWorldMatrix = obj.transform.localToWorldMatrix,
                indices_offset = firstIndex,
                indices_count = indices.Length
            });
        }
        CreateComputeBuffer(ref _meshObjectBuffer, _meshObjects, 72);
        CreateComputeBuffer(ref _vertexBuffer, _vertices, 12);
        CreateComputeBuffer(ref _indexBuffer, _indices, 4);
    }

    private static void CreateComputeBuffer<T>(ref ComputeBuffer buffer, List<T> data, int stride)
    where T : struct
    {
        // Do we already have a compute buffer?
        if (buffer != null)
        {
            // If no data or buffer doesn't match the given criteria, release it
            if (data.Count == 0 || buffer.count != data.Count || buffer.stride != stride)
            {
                buffer.Release();
                buffer = null;
            }
        }
        if (data.Count != 0)
        {
            // If the buffer has been released or wasn't there to
            // begin with, create it
            if (buffer == null)
            {
                buffer = new ComputeBuffer(data.Count, stride);
            }
            // Set data on the buffer
            buffer.SetData(data);
        }
    }
    private void SetComputeBuffer(string name, ComputeBuffer buffer)
    {
        if (buffer != null)
        {
            RayTracingShader.SetBuffer(0, name, buffer);
        }
    }
}
