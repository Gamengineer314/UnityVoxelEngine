using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Render params and command buffers
    /// </summary>
    internal class VoxelRenderParams {
        private GraphicsBuffer meshesBuffer;
        private GraphicsBuffer commandsBuffer;
        private GraphicsBuffer offsetsBuffer;
        protected readonly RenderParams renderParams;
        private readonly uint[] count = new uint[1];


        public GraphicsBuffer FacesBuffer {
            set => renderParams.matProps.SetBuffer(VoxelRenderers.Instance.facesId, value);
        }

        public GraphicsBuffer ColorsBuffer {
            set => renderParams.matProps.SetBuffer(VoxelRenderers.Instance.colorsId, value);
        }

        public unsafe GraphicsBuffer MeshesBuffer {
            get => meshesBuffer;
            set {
                if (meshesBuffer == value) return;
                meshesBuffer = value;
                commandsBuffer?.Dispose();
                commandsBuffer = new(GraphicsBuffer.Target.IndirectArguments, meshesBuffer.count, GraphicsBuffer.IndirectDrawIndexedArgs.size);
                GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[meshesBuffer.count];
                for (int i = 0; i < meshesBuffer.count; i++) commands[i] = new() { instanceCount = 1 };
                commandsBuffer.SetData(commands);
                offsetsBuffer?.Dispose();
                offsetsBuffer = new(GraphicsBuffer.Target.Structured | GraphicsBuffer.Target.Counter, meshesBuffer.count, sizeof(CommandOffset));
                renderParams.matProps.SetBuffer(VoxelRenderers.Instance.offsetsId, offsetsBuffer);
            }
        }


        internal VoxelRenderParams(
            Camera renderCamera, Material renderMaterial,
            GraphicsBuffer meshesBuffer, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer
        ) {
            renderParams = new(renderMaterial) {
                camera = renderCamera,
                worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                matProps = new()
            };
            MeshesBuffer = meshesBuffer;
            FacesBuffer = facesBuffer;
            ColorsBuffer = colorsBuffer;
        }

        internal virtual void Dispose() {
            commandsBuffer.Dispose();
            offsetsBuffer.Dispose();
        }


        /// <summary>
        /// Frustum and back-face culling
        /// </summary>
        internal virtual void Cull(Camera cullingCamera, ComputeShader cullingShader, int groupSize) {
            VoxelRenderers renderers = VoxelRenderers.Instance;

            // Set camera data
            cullingShader.SetVector(renderers.cameraPositionId, cullingCamera.transform.position);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
            cullingShader.SetVector(renderers.cameraFarPlaneId, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            cullingShader.SetVector(renderers.cameraLeftPlaneId, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            cullingShader.SetVector(renderers.cameraRightPlaneId, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            cullingShader.SetVector(renderers.cameraDownPlaneId, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            cullingShader.SetVector(renderers.cameraUpPlaneId, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));

            // Frustum culling
            cullingShader.SetBuffer(0, renderers.meshesId, meshesBuffer);
            cullingShader.SetBuffer(0, renderers.commandsId, commandsBuffer);
            cullingShader.SetBuffer(0, renderers.offsetsId, offsetsBuffer);
            offsetsBuffer.SetCounterValue(0);
            cullingShader.Dispatch(0, meshesBuffer.count / groupSize, 1, 1);
            GraphicsBuffer.CopyCount(offsetsBuffer, renderers.counterBuffer, 0);
            renderers.counterBuffer.GetData(count);
        }

        /// <summary>
        /// Render the meshes
        /// </summary>
        internal void Render() {
            Graphics.RenderPrimitivesIndexedIndirect(renderParams, MeshTopology.Triangles, VoxelRenderers.Instance.indicesBuffer, commandsBuffer, (int)count[0]);
        }
    }



    /// <summary>
    /// Render params and command buffers for objects
    /// </summary>
    internal class ObjectRenderParams : VoxelRenderParams {
        private GraphicsBuffer transformsBuffer;
        private GraphicsBuffer renderedTransformsBuffer;

        public GraphicsBuffer TransformsBuffer {
            get => transformsBuffer;
            set {
                if (transformsBuffer == value) return;
                transformsBuffer = value;
                renderedTransformsBuffer?.Dispose();
                renderedTransformsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, transformsBuffer.count, transformsBuffer.stride);
                renderParams.matProps.SetBuffer(VoxelRenderers.Instance.transformsId, renderedTransformsBuffer);
            }
        }

        internal ObjectRenderParams(
            Camera renderCamera, Material renderMaterial,
            GraphicsBuffer meshesBuffer, GraphicsBuffer facesBuffer, GraphicsBuffer colorsBuffer, GraphicsBuffer transformsBuffer
        ) : base(renderCamera, renderMaterial, meshesBuffer, facesBuffer, colorsBuffer) {
            TransformsBuffer = transformsBuffer;
        }

        internal void Cull(Camera cullingCamera, ComputeShader cullingShader) {
            cullingShader.SetBuffer(0, VoxelRenderers.Instance.transformsId, transformsBuffer);
            cullingShader.SetBuffer(0, VoxelRenderers.Instance.renderedTransformsId, renderedTransformsBuffer);
            base.Cull(cullingCamera, cullingShader, 1);
        }

        internal override void Dispose() {
            base.Dispose();
            renderedTransformsBuffer.Dispose();
        }
    }

}