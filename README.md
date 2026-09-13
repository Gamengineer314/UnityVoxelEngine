# Optimized voxel engine in Unity

## Installation

- Clone or download the repository.
- Add Unity's `Collections` package to your project using the package manager.
- Copy the `Assets/Collections` and `Assets/Voxels` folders into your project's `Assets` folder.


## Rendering components

- `VoxelRenderer` : global voxel renderer. There should be one instance of this component in each scene.
- `VoxelMesh` : generates and renders a voxel mesh with a voxel material.


## Voxel assets

Voxel models are represented by the `VoxelColumns` struct. `VoxelColumnsAsset` is a scriptable object wrapper for a `VoxelColumns` struct that can be dragged and dropped in the editor. Voxel models can be imported from `.vox` or `.ply` files, which can be exported from [Magica Voxel](https://ephtracy.github.io/) (`vox` or `cube` options respectively). Support for other file formats can easily be added by extending the `VoxelImporter` class.


## Voxel shaders

The default voxel shader renders the models without modification or effect. It uses a very simple lighting system that only depends on face orientation. The `Texture` property of a default material must be set to the same value as the `textured` parameter used to generate meshes that will be rendered with that material.

Custom voxel shaders can be written using the functions defined in `VoxelShader.cginc`. Here is a template shader :
```c
Shader "Voxels/Template" {
    Properties {

    }
    SubShader {
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // #define _VOXEL_TEXTURE_ON // Define _VOXEL_TEXTURE_ON if meshes are generated with the [textured] parameter
            #include "Assets/Voxels/Shaders/VoxelShader.cginc"

            struct v2f {
                VoxelV2F voxel;
                // Custom data passed to the fragment shader
            };

            v2f vert(uint vertexID: SV_VertexID, uint instanceID: SV_InstanceID) {
                VoxelData v = unpackVertex(vertexID, instanceID);
                // Vertex data modifications in [v]
                v2f o;
                o.voxel = voxelVertex(v);
                // Assign custom data to [o]
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                fixed4 color = voxelFragment(i.voxel);
                // Color modifications
                return color;
            }
            ENDCG
        }
    }
}
```


## Rendering optimizations

- GPU frustum culling : the engine uses a compute shader to determine which objects or chunks are visible to the camera and therefore need to be rendered. This can be more efficient, particularly when there are many objects.
- Indirect rendering : since frustum culling is performed on the GPU, the list of objects to be rendered can be made available directly in GPU memory. Using indirect rendering, this allows the CPU to perform a single draw call that instructs the GPU to read the output of the culling shader and render all requested objects. This minimizes CPU-GPU interactions. Each frame, the CPU only needs to send one compute shader dispatch call and one draw call to the GPU to render all objects in the same layer that share the same material.
- Chunks : large objects are automatically split into several chunks when generating their mesh. This way, if only a small part of the object is visible, only that part will be rendered. The size of the chunks is a trade-off between minimising per-chunk overhead and minimising the number of triangles rendered. Large chunks lead to fewer chunks being processed, but a lot of triangles could be rendered unnecessarily if only a small part of a chunk is visible. Small chunks mean more chunks must be processed, but the culling is more accurate, so fewer triangles are rendered unnecessarily. Since culling is performed on the GPU and indirect rendering is used, the per-chunk overhead is much smaller than it would be with a traditional renderer. This allows smaller chunks to be used, meaning fewer triangles need to be rendered.
- Back-face culling : since voxel faces can only have 6 different orientations, chunks can be further split into one for each orientation. This allows the culling shader to discard entire chunks that face away from the camera, significantly reducing the number of rendered triangles. It also increases the number of chunks, but this isn't a problem since per-chunk overhead is low.
- Greedy meshing : when generating a voxel mesh, adjacent faces can be merged to reduce the number of triangles in the mesh. Face merging is done with a greedy algorithm that is heavily optimized using bitwise operations. A great explanation of the algorithm can be found in [this video](https://youtu.be/qnGoGq7DWMc).
- Quads interleaving : a disadvantage of greedy meshing is that we lose the property that adjacent triangles always share two vertices. This means that the rasterizer can't guarantee that each pixel on the line separating the two triangles will belong to either the first or the second triangle. This can result in 1-pixel gaps or overlaps between faces. Overlaps are almost invisible, but gaps are very noticeable if the background color is very different from the color of the faces. To fix this, the vertex shader slightly increases the size of each triangle in screen space, to interleave the faces by a fixed pixel amount. This amount can be set in the `VoxelRenderer` component. Basically, it should be set to the smallest value that eliminates all gaps. This may depend on the hardware and/or graphics API used.
- Data packing : memory consumption is reduced in two ways. Firstly, mesh data is stored per face rather than per vertex. The vertex shader receives the vertex index, divides it by 4, and reads the corresponding face data from a buffer. It then computes the vertex position based on the face data and the vertex index modulo 4, which only requires a few basic instructions. Secondly, instead of using floating-point numbers, the data fields are stored as very small integers and packed together into two 32-bit integers per face, with per-chunk offsets when necessary. The position uses 3 x 10 bits with an offset, the size uses 2 x 6 bits, the normal uses 3 bits, and the color index uses 16 bits with an offset.


## Physics components

- `VoxelPhysics` : global physics data. There should be one instance of this component in each scene. Colliders are stored in a sparse octree. This component can be used to configure the size, position, and maximum depth of the octree.
- `VoxelCharacterController` : kinematic character controller that checks collisions between the `VoxelBoxCollider` attached to the same `GameObject` and other voxel colliders. It supports moving, falling, jumping, and auto-jumping. Collisions are checked using a `MoveBox` physics query, which is similar to a raycast but moves a box instead of a single point.
- `VoxelBoxCollider` : equivalent to a static `BoxCollider` that can collide with a `VoxelCharacterController`. Only 90-degree rotations are supported.
- `VoxelMeshCollider` : equivalent to a static `MeshCollider` that can collide with a `VoxelCharacterController`. Only 90-degree rotations are supported.


## Generation parameters

Some mesh generation parameters can be supplied to the `VoxelMesh` component with an instance of the `GenerationParameters` scriptable object. Here are its fields :

- `chunkSize` : maximum size for mesh chunks. Multiple chunks can be generated from the same voxel model if it exceeds this size. The generator will perform best if `chunkSize` is a multiple of 64.
- `mergeNormalsThreshold` : number of faces below which chunks at the same position with different normals must be merged. Objects smaller than this threshold will use a single chunk but can't be partially culled based on normals.
- `seenFromAbove` : whether objects can only be seen from above and inside their horizontal bounds. This allows to remove faces below the objects and on their sides.
- `jobHorizontalSize` : maximum horizontal size that a generator job can process. Multiple jobs will be used to generate the chunks in parallel if a voxel collection exceeds this size. `jobHorizontalSize` should be a multiple of `chunkSize`.
- `asynchronousGeneration` : whether meshes can be generated asynchronously over multiple frames. If set to false, the main thread will block during the late update until all scheduled generations are completed so that meshes can be rendered as soon as they're instantiated.
- `textured` : if set to true, the greedy mesher can combine faces with different colors, but the color of each individual face must be stored in a texture. Otherwise, the greedy mesher can only combines faces with the same color, but each color must only stored once in the texture.
- `instanced` : whether to use GPU instancing.

The `VoxelMeshCollider` component also takes parameters for generating physics data, but it only uses the `jobHorizontalSize` and `asynchronousGeneration` fields.


## Coming soon

- Pathfinding
- LOD