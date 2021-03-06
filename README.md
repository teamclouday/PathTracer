# Path Tracer

My path tracer implemented in Unity engine

------

### Basic Scene

<details>
<summary>Expand for Details</summary>

Built based on the [tutorial](http://blog.three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/). Only contains a ground and multiple spheres with skybox.  
Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/tutorial)

<img src="Images/basic.png" width="600" alt="basic">
</details>

### Cornell Box

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/cornellbox)

Improved hemisphere sampling adapted from [lwjgl3-demos](https://github.com/LWJGL/lwjgl3-demos/blob/main/res/org/lwjgl/demo/opengl/raytracing/randomCommon.glsl).  
A better random value generator.  
Support unity material `_EMISSION`, `_EmissionColor`, `_Metallic`, `_Glossiness` values.  

<img src="Images/cornellbox.png" width="600" alt="cornellbox">
</details>

### Cornell Box & Bunny

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/cornellboxbunny)

Added basic BVH (bounding volume hierarchy).  
Scene Info:
```
BVH tree nodes count = 290135
Total vertices = 100277
Total indices = 435474
Total normals = 100277
Total materials = 10
```
RTX2060S has about 18fps at default view position in unity editor mode  
Looking forward to further optimize it.

<img src="Images/cornellboxbunny.png" width="600" alt="cornellboxbunny">
</details>

### Dragon

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/dragon)

Added reflection & refraction workflow for transparent materials.  
In my implementation, if a material (standard shader) render mode is not opaque, it will go through this workflow. In this case, smoothness is translated to index of refraction (`ior`):
```
k => k * 2.0 + 1.0
```
Scene Info:
```
BVH tree nodes count = 1735017
Total vertices = 439077
Total indices = 2614194
Total normals = 439077
Total materials = 3
```
Loading time is very slow. Need to optimize BVH tree construction.

<img src="Images/dragon.png" width="600" alt="dragon">
</details>

### Cornell Box & Buddha

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/cornellboxbuddha)

Added [openimagedenoise](https://github.com/OpenImageDenoise/oidn), so that when the scene cannot converge, the output image is smooth.  
Press Left CTRL + V to toggle denoiser, default is off.

Scene Info:
```
BVH tree nodes count = 2164457
Total vertices = 550046
Total indices = 3265248
Total normals = 550046
Total materials = 10
```
Expect a long loading time.

Without denoiser (412 samples):  
<img src="Images/cornellboxbuddha_S412.png" width="400" alt="cornellboxbuddha_S412">

With denoiser (195 samples):  
<img src="Images/cornellboxbuddha_S195.png" width="400" alt="cornellboxbuddha_S195">

_Side Note_:  
After multiple testing in this environment setup,
it seems that with a colored skybox (not dark in this case),
the scene converges much faster.
With a dark environment and only one small emissive light object,
the scene converges very slow.
The reason behind it, in my opinion,
is because during random sampling,
the ray hits a dark non-emissive object much more frequently than
hitting an emissive object.
Therefore, the samples contain many dark pixels because the ray is not
lucky enough to reach a light source.
A denoiser is required to improve the visual in this case.
</details>

### Bunny

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/bunny)

No new features added.
Fixed an important bug that occurs during cosine weighted hemisphere sampling.
Switched to another random value generator that is more convenient.
Finally, improve the transparent workload for better visual effects. (Not refering to any paper so not physically accurate)

Denoised view:  
<img src="Images/bunny.png" width="600" alt="bunny">

_Side Note_:  
As you may have noticed in previous "Cornell Box & Buddha" demo images, the head light in the scene casts light in biased directions.
This is caused by a bug in sampling function, which is partially fixed in this new scene.
Can refer to [here](https://stackoverflow.com/questions/69510208/path-tracing-cosine-hemisphere-sampling-and-emissive-objects) for details.
The reason I say it's partially fixed is because though the area light looks fine when it is large, it is still not perfect when it is a small light.
In fact, no matter of its geometric shape, it will tend to scatter a cross shape distribution of light on the ground.
</details>

### Depth of View

<details>
<summary>Expand for Details</summary>

No executable available, because it is meant to be tested in Unity editor.

Updated camera model, with focal length and aperture.

Denoised view:  
<img src="Images/bunny_camera.png" width="600" alt="bunny_camera">
</details>

### Paper Effect

<details>
<summary>Expand for Details</summary>

This is done by playing with `rng_initialize`:  
```hlsl
float2 center = float2(id.xy);
rng_initialize(dot(center, camera.offset), _FrameCount);
```
where `camera.offset` is just another vector of independent random values.

The result looks as if it is drawn on a paper, and it is cool without any denoising:  
<img src="Images/paperbunny.png" width="600" alt="paperbunny">
</details>

### Sponza

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/sponza)

Added SAH to improve BVH reference efficiency.  
Added support for albedo texture maps. Textures are first combined to a `Texture2DArray` object and then bound to compute shaders. Each texture is resized to the max size of all textures.

Scene Info:
```
TLAS nodes = 395
BLAS nodes = 501469
Total vertices = 192254
Total indices = 786873
Total normals = 192254
Total materials = 396
Total textures = 24
```
Expect a low fps (about 3-5).

Denoised views:  
<img src="Images/sponza_lion.png" width="600" alt="sponza_lion">  
<img src="Images/sponza_down.png" width="600" alt="sponza_down">
</details>

### Room

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/room)

Support emission maps and metallic maps.  
Support cutoff render mode (skip geometry if alpha < 1.0).  
Added bvh build for TLAS (but not used in this scene, see side note).  
Added sRGB color space conversion for texture maps.  
Change camera fov and add keyboard combination for material reloading.  
Support directional light in the scene (regarded as sun light), and tweak the lighting for transparent materials.

Scene Info:
```
TLAS nodes = 25
TLAS raw nodes = 45
BLAS nodes = 1225193
Total vertices = 469711
Total indices = 4042797
Total normals = 469711
Total materials = 46
Total albedo textures = 2
Total emissive textures = 0
Total metallic textures = 0
```
Expect a low fps.

Denoised view:  
<img src="Images/room.png" width="600" alt="room"> 

_Side Note_:  
My previous implementation regards TLAS nodes as an array. For each ray, it loops the full array, tests intersections with bounding volumes and enters BLAS nodes if hit. In scenes such as Sponza, number of TLAS nodes can be large. Therefore, I created another BVH for TLAS nodes, and the ray first recurse in the TLAS tree to find a hit and then enter the corresponding BLAS node. However, based on my experiments, this modification makes rendering even slower. The reason is probably because each node in TLAS tree may have overlapping bounding areas for left and right children. This potentially increases the amount of intersection tests in intermediate nodes that are not leaves. (which affects BLAS tree as well) I think the next improvement is to find a space partition strategy for geometries that reduces overlapping areas to minimum.
</details>

### Exterior

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/exterior)

Support normal maps and roughness maps (for Autodesk interactive shader materials).  
Possible to adjust directional light rotation at runtime.  
Now able to toggle between Unity renderer and Path tracer. It is recommended to move camera and adjust light direction in Unity renderer mode and then switch back to Path tracer.

Scene Info:
```
TLAS nodes = 2973
TLAS raw nodes = 1615
BLAS nodes = 4821155
Total vertices = 2921248
Total indices = 8500071
Total normals = 2921248
Total tangents = 2921248
Total materials = 1616
Total albedo textures = 106
Total emissive textures = 6
Total metallic textures = 0
Total normal textures = 86
Total roughness textures = 0
```
Expect a low fps and the longest loading time.
Do not launch with a large window size, or DirectX may crash.

Denoised views:  
<img src="Images/exterior_v1.png" width="500" alt="exterior_v1">  
<img src="Images/exterior_v2.png" width="500" alt="exterior_v2">  
<img src="Images/exterior_v3.png" width="500" alt="exterior_v3">  
<img src="Images/exterior_v4.png" width="500" alt="exterior_v4">  
</details>

### Fireplace Room

<details>
<summary>Expand for Details</summary>

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/fireplaceroom)

Modified rendering equations based on Disney BSDF and refering to online resources.  
Added support for multiple point lights, with customized illumination formulas.  
Now able to toggle camera depth of view, and allow camera focus by middle mouse click.  
Fixed crucial bug in denoiser, and added another optional realtime denoiser with spatial filter shader.  

Scene Info:
```
TLAS nodes = 43
TLAS raw nodes = 24
BLAS nodes = 261608
Total vertices = 190085
Total indices = 429135
Total normals = 190085
Total tangents = 190085
Total materials = 25
Total albedo textures = 4
Total emissive textures = 0
Total metallic textures = 0
Total normal textures = 0
Total roughness textures = 0
```

Denoised views:  
<img src="Images/fireplace1.png" width="500" alt="fireplace1">  
<img src="Images/fireplace2.png" width="500" alt="fireplace2">  
</details>

------

## Controls

```
W -> camera forward
S -> camera backward
A -> camera left
D -> camera right
UP -> light rotation X increases
DOWN -> light rotation X decreases
RIGHT -> light rotation Y decreases
LEFT -> light rotation Y increases
ESC -> quit application
Left click and drag -> camera look around
Scroll up -> move forward
Scroll down -> move backward
Left CTRL + X -> save screenshot in data folder
Left CTRL + V -> toggle denoiser (default is off)
Left CTRL + R -> Reload materials and light info (editor only)
Left CTRL + SPACE -> Switch between Unity renderer and Path tracer
Left CTRL + C -> toggle camera depth of view
Middle Mouse Click -> focus on current position (only when camera depth is enabled)
```

------

## References

[GPU Ray Tracing in Unity](http://blog.three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/)  
[Physically Based Rendering](https://www.pbr-book.org/3ed-2018/contents)  
[RadeonRays_SDK](https://github.com/GPUOpen-LibrariesAndSDKs/RadeonRays_SDK)  
[GLSL-PathTracer](https://github.com/knightcrawler25/GLSL-PathTracer)  
[Another View on the Classic Ray-AABB Intersection Algorithm for BVH Traversal](https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525)  
[Spatial Splits in Bounding Volume Hierarchies](https://www.nvidia.in/docs/IO/77714/sbvh.pdf)  
[Recursive SAH-based Bounding Volume Hierarchy Construction](https://www.gcc.tu-darmstadt.de/media/gcc/papers/rsah_gi2016.pdf)  
[Ray Tracing: The Next Week](https://raytracing.github.io/books/RayTracingTheNextWeek.html)  
[Casual Shadertoy Path Tracing 3: Fresnel, Rough Refraction & Absorption, Orbit Camera](https://blog.demofox.org/2020/06/14/casual-shadertoy-path-tracing-3-fresnel-rough-refraction-absorption-orbit-camera/)  