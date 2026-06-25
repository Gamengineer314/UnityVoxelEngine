using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Voxel renderer for a layer, a shader, and a camera
    /// </summary>
    internal class LayerRenderer {
        private GraphicsBuffer commandsBuffer;
        private GraphicsBuffer offsetsBuffer;
        private GraphicsBuffer renderedTransformsBuffer;
        private readonly RenderParams renderParams;
        private readonly ShaderParameters parameters;
        private readonly int cullingGroupSize;
        private readonly uint[] count = new uint[1];


        internal LayerRenderer(Material material, Camera camera, int layer, ShaderParameters parameters) {
            renderParams = new(material) {
                camera = camera,
                layer = layer,
                worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                matProps = new()
            };
            SetCullingKeywords();
            VoxelRenderer.Instance.cullingShader.GetKernelThreadGroupSizes(0, out uint size, out _, out _);
            cullingGroupSize = (int)size;
            this.parameters = parameters;
        }


        internal virtual void Dispose() {
            commandsBuffer?.Dispose();
            offsetsBuffer?.Dispose();
            renderedTransformsBuffer?.Dispose();
        }


        /// <summary>
        /// Set the buffers used for culling and rendering
        /// </summary>
        internal unsafe void SetBuffers(GraphicsBuffer chunksBuffer, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer, GraphicsBuffer transformsBuffer) {
            ComputeShader cullingShader = VoxelRenderer.Instance.cullingShader;
            renderParams.matProps.SetBuffer(ShaderID.faces, facesBuffer);
            renderParams.matProps.SetBuffer(ShaderID.colors, colorsBuffer);
            if (commandsBuffer == null || chunksBuffer.count != offsetsBuffer.count) { // Create corresponding commands and offsets buffers
                commandsBuffer?.Dispose();
                commandsBuffer = new(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, chunksBuffer.count * 5, sizeof(uint));
                GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[chunksBuffer.count];
                for (int i = 0; i < chunksBuffer.count; i++) commands[i] = new() { instanceCount = 1 };
                commandsBuffer.SetData(commands);
                offsetsBuffer?.Dispose();
                offsetsBuffer = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter, chunksBuffer.count, sizeof(CommandOffset));
                renderParams.matProps.SetBuffer(ShaderID.offsets, offsetsBuffer);
                if (parameters.transform) {
                    renderedTransformsBuffer?.Dispose();
                    renderedTransformsBuffer = new(GraphicsBuffer.Target.Structured, chunksBuffer.count, sizeof(Matrix4x4));
                    renderParams.matProps.SetBuffer(ShaderID.renderedTransforms, renderedTransformsBuffer);
                }
            }
            cullingShader.SetBuffer(0, ShaderID.chunks, chunksBuffer);
            cullingShader.SetBuffer(0, ShaderID.commands, commandsBuffer);
            cullingShader.SetBuffer(0, ShaderID.offsets, offsetsBuffer);
            if (parameters.transform) {
                cullingShader.SetBuffer(0, ShaderID.transforms, transformsBuffer);
                cullingShader.SetBuffer(0, ShaderID.renderedTransforms, renderedTransformsBuffer);
            }
        }


        /// <summary>
        /// Frustum and back-face culling
        /// </summary>
        internal virtual void Cull(int nChunks) {
            VoxelRenderer renderer = VoxelRenderer.Instance;
            ComputeShader cullingShader = renderer.cullingShader;
            Camera camera = renderParams.camera;
            int nGroups = parameters.instance ? nChunks : Mathf.CeilToInt((float)nChunks / cullingGroupSize);

            // Set camera data
            SetCullingKeywords();
            cullingShader.SetVector(ShaderID.cameraPosition, camera.transform.position);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            cullingShader.SetVector(ShaderID.cameraFarPlane, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            cullingShader.SetVector(ShaderID.cameraLeftPlane, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            cullingShader.SetVector(ShaderID.cameraRightPlane, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            cullingShader.SetVector(ShaderID.cameraDownPlane, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            cullingShader.SetVector(ShaderID.cameraUpPlane, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));
            if (!parameters.instance) cullingShader.SetInt(ShaderID.nChunks, nChunks);

            // Frustum culling
            offsetsBuffer.SetCounterValue(0);
            cullingShader.Dispatch(0, nGroups, 1, 1);
            GraphicsBuffer.CopyCount(offsetsBuffer, renderer.counterBuffer, 0);
            renderer.counterBuffer.GetData(count);
        }

        private void SetCullingKeywords() {
            VoxelRenderer.Instance.cullingShader.SetKeyword(in ShaderID.cullingInstance, parameters.instance);
            VoxelRenderer.Instance.cullingShader.SetKeyword(in ShaderID.cullingTransform, parameters.transform);
        }


        /// <summary>
        /// Render the meshes
        /// </summary>
        internal void Render() {
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, VoxelRenderer.Instance.indicesBuffer, commandsBuffer, (int)count[0]);
        }
    }
}