# Path Tracer

My path tracer implemented in Unity engine

------

### Basic Scene
Built based on the tutorial. Only contains a ground and multiple spheres with skybox.  
Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/tutorial)

<img src="Images/basic.png" width="600" alt="basic">

### Cornell Box

Compiled executables available [here](https://github.com/teamclouday/PathTracer/releases/tag/cornellbox)

Improved hemisphere sampling adapted from [lwjgl3-demos](https://github.com/LWJGL/lwjgl3-demos/blob/main/res/org/lwjgl/demo/opengl/raytracing/randomCommon.glsl).  
A better random value generator.  
Support unity material `_EMISSION`, `_EmissionColor`, `_Metallic`, `_Glossiness` values.  

<img src="Images/cornellbox.png" width="600" alt="cornellbox">

### Cornell Box & Bunny

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

### Dragon

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

------

### Controls

```
W -> camera forward
S -> camera backward
A -> camera left
D -> camera right
ESC -> quit application
Left click and drag -> camera look around
Scroll up -> move forward
Scroll down -> move backward
Left CTRL + X -> save screenshot in data folder
```

------

Reference:

[GPU Ray Tracing in Unity – Part 1](http://blog.three-eyed-games.com/2018/05/03/gpu-ray-tracing-in-unity-part-1/)  
[GPU Path Tracing in Unity – Part 2](http://three-eyed-games.com/2018/05/12/gpu-path-tracing-in-unity-part-2/)  
[GPU Path Tracing in Unity – Part 3](http://three-eyed-games.com/2019/03/18/gpu-path-tracing-in-unity-part-3/)  
[Physically Based Rendering](https://www.pbr-book.org/3ed-2018/contents)  
[RadeonRays_SDK](https://github.com/GPUOpen-LibrariesAndSDKs/RadeonRays_SDK)  
[Fast-BVH](https://github.com/brandonpelfrey/Fast-BVH)  
[GLSL-PathTracer](https://github.com/knightcrawler25/GLSL-PathTracer)  
[Unity Toy Path Tracer](http://theinstructionlimit.com/unity-toy-path-tracer)  
[Another View on the Classic Ray-AABB Intersection Algorithm for BVH Traversal](https://medium.com/@bromanz/another-view-on-the-classic-ray-aabb-intersection-algorithm-for-bvh-traversal-41125138b525)