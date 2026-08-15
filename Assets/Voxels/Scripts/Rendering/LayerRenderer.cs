using UnityEngine;
using Unity.Mathematics;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Voxel renderer for a layer, a shader, and a camera
    /// </summary>
    internal class LayerRenderer {
        private readonly VoxelRenderer renderer;
        private GraphicsBuffer commandsBuffer;
        private GraphicsBuffer offsetsBuffer;
        private GraphicsBuffer renderedTransformsBuffer;
        private readonly RenderParams renderParams;
        private readonly ShaderParameters parameters;
        private readonly int cullingGroupSize;
        private readonly uint[] count = new uint[1];


        internal LayerRenderer(VoxelRenderer renderer, Material material, Camera camera, int layer, ShaderParameters parameters) {
            this.renderer = renderer;
            renderParams = new(material) {
                camera = camera,
                layer = layer,
                worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                matProps = new()
            };
            renderer.CullingShader.SetKeyword(in ShaderID.cullingInstance, parameters.instanced);
            renderer.CullingShader.GetKernelThreadGroupSizes(0, out uint size, out _, out _);
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
        internal unsafe void SetBuffers(GraphicsBuffer chunksBuffer, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer, GraphicsBuffer transformsBuffer, int renderedTransformsSize) {
            renderParams.matProps.SetBuffer(ShaderID.faces, facesBuffer);
            renderParams.matProps.SetBuffer(ShaderID.colors, colorsBuffer);
            if (commandsBuffer == null || chunksBuffer.count != offsetsBuffer.count) { // Create corresponding commands and offsets buffers
                commandsBuffer?.Dispose();
                commandsBuffer = new(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, chunksBuffer.count * 5, sizeof(uint));
                offsetsBuffer?.Dispose();
                offsetsBuffer = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter, chunksBuffer.count, sizeof(CommandOffset));
                renderParams.matProps.SetBuffer(ShaderID.offsets, offsetsBuffer);
            }
            if (renderedTransformsBuffer == null || renderedTransformsBuffer.count != renderedTransformsSize) {
                renderedTransformsBuffer?.Dispose();
                renderedTransformsBuffer = new(GraphicsBuffer.Target.Structured, renderedTransformsSize, sizeof(float4x4));
                renderParams.matProps.SetBuffer(ShaderID.renderedTransforms, renderedTransformsBuffer);
            }

            ComputeShader cullingShader = renderer.CullingShader;
            cullingShader.SetBuffer(0, ShaderID.chunks, chunksBuffer);
            cullingShader.SetBuffer(0, ShaderID.commands, commandsBuffer);
            cullingShader.SetBuffer(0, ShaderID.offsets, offsetsBuffer);
            cullingShader.SetBuffer(0, ShaderID.transforms, transformsBuffer);
            cullingShader.SetBuffer(0, ShaderID.renderedTransforms, renderedTransformsBuffer);
        }


        /// <summary>
        /// Frustum and back-face culling
        /// </summary>
        internal virtual void Cull(int nChunks) {
            ComputeShader cullingShader = renderer.CullingShader;
            Camera camera = renderParams.camera;
            int nGroups = parameters.instanced ? nChunks : Mathf.CeilToInt((float)nChunks / cullingGroupSize);

            // Set camera data
            renderer.CullingShader.SetKeyword(in ShaderID.cullingInstance, parameters.instanced);
            renderer.CullingShader.SetKeyword(in ShaderID.cullingOrthographic, camera.orthographic);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(camera);
            cullingShader.SetVector(ShaderID.cameraFarPlane, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            cullingShader.SetVector(ShaderID.cameraLeftPlane, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            cullingShader.SetVector(ShaderID.cameraRightPlane, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            cullingShader.SetVector(ShaderID.cameraDownPlane, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            cullingShader.SetVector(ShaderID.cameraUpPlane, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));
            if (!camera.orthographic) cullingShader.SetVector(ShaderID.cameraPosition, camera.transform.position);
            if (!parameters.instanced) cullingShader.SetInt(ShaderID.nChunks, nChunks);

            // Frustum culling
            offsetsBuffer.SetCounterValue(0);
            cullingShader.Dispatch(0, nGroups, 1, 1);
            GraphicsBuffer.CopyCount(offsetsBuffer, renderer.counterBuffer, 0);
            renderer.counterBuffer.GetData(count);
        }


        /// <summary>
        /// Render the meshes
        /// </summary>
        internal void Render() {
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, renderer.indicesBuffer, commandsBuffer, (int)count[0]);
        }


#if UNITY_EDITOR
        internal void RenderWireframe() {
            renderer.wireframeMaterial.SetBuffer(ShaderID.offsets, offsetsBuffer);
            renderer.wireframeMaterial.SetBuffer(ShaderID.renderedTransforms, renderedTransformsBuffer);
            for (int i = 0; i < count[0]; i++) {
                renderer.wireframeMaterial.SetInteger(ShaderID.baseCommandID, i);
                renderer.wireframeMaterial.SetPass(0);
                Graphics.DrawProceduralIndirectNow(MeshTopology.Triangles, renderer.indicesBuffer, commandsBuffer, i * 5 * sizeof(uint));
            }
        }
#endif
    }
}