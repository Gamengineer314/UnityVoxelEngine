# Optimized voxel engine in Unity

## Features
- Fast and multithreadable greedy mesher (using Unity's jobs and Burst compiler)
- Frustum and back-face culling in a compute shader
- Packed mesh data (8 bytes per rectangle + 44 or 52 bytes per mesh)
- Indirect rendering (using Graphics.RenderPrimitivesIndexedIndirect) for minimal CPU-GPU interactions (1 compute dispatch + 1 draw call per layer and camera)

## Coming soon
- Custom shaders
- Instancing
- LOD
- Collisions
- Pathfinding