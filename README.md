# Optimized voxel engine in Unity

## Features
- Fast and multithreadable greedy mesher (using Unity's jobs and Burst compiler)
- Frustum and back-face culling in a compute shader
- Packed mesh data (8 bytes per rectangle + 32 bytes per mesh)
- Indirect rendering (using Graphics.RenderPrimitivesIndexedIndirect) for minimal CPU-GPU interactions
- Random slight color variation for each voxel in the fragment shader

## Coming soon
- Instancing
- Colliders