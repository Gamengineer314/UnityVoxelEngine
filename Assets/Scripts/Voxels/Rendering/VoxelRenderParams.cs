using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Render params and command buffers
    /// </summary>
    internal class VoxelRenderParams {
        private readonly GraphicsBuffer meshesBuffer;
        private readonly GraphicsBuffer commandsBuffer;
        private readonly GraphicsBuffer offsetsBuffer;
        private readonly RenderParams renderParams;


        /// <summary>
        /// Create render params and command buffers
        /// </summary>
        /// <param name="renderCamera">Camera used for rendering</param>
        /// <param name="renderMaterial">Material used for rendering</param>
        /// <param name="meshesBuffer">Meshes to render</param>
        /// <param name="facesBuffer">Faces to render</param>
        /// <param name="colorsBuffer">Colors to render</param>
        internal unsafe VoxelRenderParams(
            Camera renderCamera, Material renderMaterial,
            GraphicsBuffer meshesBuffer, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer
        ) {
            this.meshesBuffer = meshesBuffer;
            VoxelRenderers voxels = VoxelRenderers.Instance;

            commandsBuffer = new(
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Counter | GraphicsBuffer.Target.Structured,
                meshesBuffer.count, GraphicsBuffer.IndirectDrawIndexedArgs.size
            );
            GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[meshesBuffer.count];
            for (int i = 0; i < meshesBuffer.count; i++) commands[i] = new() { instanceCount = 1 };
            commandsBuffer.SetData(commands);

            offsetsBuffer = new(GraphicsBuffer.Target.Structured, meshesBuffer.count, sizeof(CommandOffset));
            CommandOffset[] positions = new CommandOffset[meshesBuffer.count];
            offsetsBuffer.SetData(positions);

            MaterialPropertyBlock props = new();
            props.SetBuffer(voxels.facesId, facesBuffer);
            props.SetBuffer(voxels.colorsId, colorsBuffer);
            props.SetBuffer(voxels.offsetsId, offsetsBuffer);
            renderParams = new(renderMaterial) {
                camera = renderCamera,
                worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                matProps = props
            };
        }


        /// <summary>
        /// Render the meshes
        /// </summary>
        /// <param name="cullingCamera">Camera used for culling</param>
        /// <param name="cullingShader">Compute shader used for culling</param>
        internal void Render(Camera cullingCamera, ComputeShader cullingShader) {
            VoxelRenderers voxels = VoxelRenderers.Instance;

            // Set camera data
            cullingShader.SetVector(voxels.cameraPositionId, cullingCamera.transform.position);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
            cullingShader.SetVector(voxels.cameraFarPlaneId, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            cullingShader.SetVector(voxels.cameraLeftPlaneId, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            cullingShader.SetVector(voxels.cameraRightPlaneId, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            cullingShader.SetVector(voxels.cameraDownPlaneId, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            cullingShader.SetVector(voxels.cameraUpPlaneId, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));

            // Frustrum culling
            cullingShader.SetBuffer(0, voxels.meshesId, meshesBuffer);
            cullingShader.SetBuffer(0, voxels.commandsId, commandsBuffer);
            cullingShader.SetBuffer(0, voxels.offsetsId, offsetsBuffer);
            commandsBuffer.SetCounterValue(0);
            cullingShader.Dispatch(0, meshesBuffer.count / VoxelRenderers.cullingGroupSize, 1, 1);
            GraphicsBuffer.CopyCount(commandsBuffer, voxels.counterBuffer, 0);
            uint[] count = new uint[1];
            voxels.counterBuffer.GetData(count);
            
            // Draw call
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, VoxelRenderers.Instance.indicesBuffer, commandsBuffer, (int)count[0]);
        }


        internal void Dispose() {
            commandsBuffer.Dispose();
            offsetsBuffer.Dispose();
        }
    }

}