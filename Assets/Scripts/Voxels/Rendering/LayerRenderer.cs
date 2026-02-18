using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Voxel renderer for a layer and a camera
    /// </summary>
    internal abstract class LayerRenderer {
        private GraphicsBuffer meshesBuffer;
        private int meshCount;
        private GraphicsBuffer commandsBuffer;
        private GraphicsBuffer offsetsBuffer;
        protected readonly RenderParams renderParams;
        private readonly uint[] count = new uint[1];


        internal LayerRenderer(Material material, Camera camera, int layer) {
            renderParams = new(material) {
                camera = camera,
                layer = layer,
                worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                matProps = new()
            };
        }


        internal virtual void Dispose() {
            commandsBuffer?.Dispose();
            offsetsBuffer?.Dispose();
        }


        /// <summary>
        /// Set the buffers used for culling and rendering
        /// </summary>
        protected unsafe void SetBuffers(GraphicsBuffer meshesBuffer, int meshCount, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer) {
            renderParams.matProps.SetBuffer(ShaderID.faces, facesBuffer);
            renderParams.matProps.SetBuffer(ShaderID.colors, colorsBuffer);
            if (meshesBuffer != this.meshesBuffer) { // Create corresponding commands and offsets buffers
                this.meshesBuffer = meshesBuffer;
                this.meshCount = meshCount;
                commandsBuffer?.Dispose();
                commandsBuffer = new(GraphicsBuffer.Target.IndirectArguments, meshesBuffer.count, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[meshesBuffer.count];
                for (int i = 0; i < meshesBuffer.count; i++) commands[i] = new() { instanceCount = 1 };
                commandsBuffer.SetData(commands);
                offsetsBuffer?.Dispose();
                offsetsBuffer = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter, meshesBuffer.count, sizeof(CommandOffset));
                renderParams.matProps.SetBuffer(ShaderID.offsets, offsetsBuffer);
            }
        }


        /// <summary>
        /// Frustum and back-face culling
        /// </summary>
        protected void Cull(ComputeShader cullingShader, int groupSize) {
            VoxelRenderer renderer = VoxelRenderer.Instance;

            // Set camera data
            cullingShader.SetVector(ShaderID.cameraPosition, renderParams.camera.transform.position);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(renderParams.camera);
            cullingShader.SetVector(ShaderID.cameraFarPlane, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            cullingShader.SetVector(ShaderID.cameraLeftPlane, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            cullingShader.SetVector(ShaderID.cameraRightPlane, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            cullingShader.SetVector(ShaderID.cameraDownPlane, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            cullingShader.SetVector(ShaderID.cameraUpPlane, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));

            // Frustum culling
            cullingShader.SetBuffer(0, ShaderID.meshes, meshesBuffer);
            cullingShader.SetBuffer(0, ShaderID.commands, commandsBuffer);
            cullingShader.SetBuffer(0, ShaderID.offsets, offsetsBuffer);
            offsetsBuffer.SetCounterValue(0);
            cullingShader.Dispatch(0, meshCount / groupSize, 1, 1);
            GraphicsBuffer.CopyCount(offsetsBuffer, renderer.counterBuffer, 0);
            renderer.counterBuffer.GetData(count);
        }


        /// <summary>
        /// Render the meshes
        /// </summary>
        internal void Render() {
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, VoxelRenderer.Instance.indicesBuffer, commandsBuffer, (int)count[0]);
        }
    }



    /// <summary>
    /// Voxel terrain renderer for a layer and a camera
    /// </summary>
    internal class TerrainLayerRenderer : LayerRenderer {
        internal const int terrainCullingGroupSize = 64;

        internal TerrainLayerRenderer(Camera camera, int layer) :
            base(VoxelRenderer.Instance.terrainMaterial, camera, layer) {}

        internal void SetBuffers(VoxelTerrainLayer layer)
            => SetBuffers(layer.meshesBuffer.buffer, layer.meshesBuffer.Length, layer.facesBuffer.buffer, layer.colorsBuffer.buffer);

        internal void Cull() => Cull(VoxelRenderer.Instance.terrainCulling, terrainCullingGroupSize);
    }



    /// <summary>
    /// Voxel object renderer for a layer and a camera
    /// </summary>
    internal class ObjectLayerRenderer : LayerRenderer {
        private GraphicsBuffer transformsBuffer;
        private GraphicsBuffer renderedTransformsBuffer;

        internal ObjectLayerRenderer(Camera camera, int layer) :
            base(VoxelRenderer.Instance.objectsMaterial, camera, layer) {}

        internal void SetBuffers(VoxelObjectLayer layer) {
            SetBuffers(layer.meshesBuffer.buffer, layer.meshesBuffer.Length, layer.facesBuffer.buffer, layer.colorsBuffer.buffer);
            if (layer.transformsBuffer.buffer != transformsBuffer) {
                transformsBuffer = layer.transformsBuffer.buffer;
                renderedTransformsBuffer?.Dispose();
                renderedTransformsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, transformsBuffer.count, transformsBuffer.stride);
                renderParams.matProps.SetBuffer(ShaderID.transforms, renderedTransformsBuffer);
            }
        }

        internal void Cull() {
            ComputeShader cullingShader = VoxelRenderer.Instance.objectsCulling;
            cullingShader.SetBuffer(0, ShaderID.transforms, transformsBuffer);
            cullingShader.SetBuffer(0, ShaderID.renderedTransforms, renderedTransformsBuffer);
            Cull(cullingShader, 1);
        }

        internal override void Dispose() {
            base.Dispose();
            renderedTransformsBuffer.Dispose();
        }
    }
}