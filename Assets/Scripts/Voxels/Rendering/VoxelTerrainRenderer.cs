using Unity.Mathematics;
using UnityEngine;

namespace Voxels.Rendering {

    public class VoxelTerrainRenderer : MonoBehaviour {
        public VoxelTerrain terrain; // Terrain to render
        public Camera target; // Camera to render the terrain on

        private Commands commands;
        private bool rendering;


        private void LateUpdate() {
            if (!rendering && terrain.Created) StartRender();
            if (rendering) {
                int count = PrepareDraw(terrain, target, commands);
                Graphics.RenderPrimitivesIndexedIndirect(commands.renderParams, MeshTopology.Triangles, VoxelRenderers.Instance.indicesBuffer, commands.commandsBuffer, count);
            }
        }


        private void StartRender() {
            rendering = true;
            commands = new Commands(terrain, target);
        }


        /// <summary>
        /// Prepare a draw call
        /// </summary>
        /// <param name="terrain">Terrain to draw</param>
        /// <param name="target">Target camera</param>
        /// <param name="commands">Command buffers to use</param>
        /// <returns>Number of commands to draw</returns>
        internal static int PrepareDraw(VoxelTerrain terrain, Camera target, Commands commands) {
            VoxelRenderers voxels = VoxelRenderers.Instance;

            // Set camera data
            voxels.terrainCulling.SetVector(voxels.cameraPositionId, target.transform.position);
            Plane[] cameraPlanes = GeometryUtility.CalculateFrustumPlanes(target);
            voxels.terrainCulling.SetVector(voxels.cameraFarPlaneId, new Vector4(cameraPlanes[5].normal.x, cameraPlanes[5].normal.y, cameraPlanes[5].normal.z, cameraPlanes[5].distance));
            voxels.terrainCulling.SetVector(voxels.cameraLeftPlaneId, new Vector4(cameraPlanes[0].normal.x, cameraPlanes[0].normal.y, cameraPlanes[0].normal.z, cameraPlanes[0].distance));
            voxels.terrainCulling.SetVector(voxels.cameraRightPlaneId, new Vector4(cameraPlanes[1].normal.x, cameraPlanes[1].normal.y, cameraPlanes[1].normal.z, cameraPlanes[1].distance));
            voxels.terrainCulling.SetVector(voxels.cameraDownPlaneId, new Vector4(cameraPlanes[2].normal.x, cameraPlanes[2].normal.y, cameraPlanes[2].normal.z, cameraPlanes[2].distance));
            voxels.terrainCulling.SetVector(voxels.cameraUpPlaneId, new Vector4(cameraPlanes[3].normal.x, cameraPlanes[3].normal.y, cameraPlanes[3].normal.z, cameraPlanes[3].distance));

            // Frustrum culling
            voxels.terrainCulling.SetBuffer(0, voxels.meshesId, terrain.meshesBuffer);
            voxels.terrainCulling.SetBuffer(0, voxels.commandsId, commands.commandsBuffer);
            voxels.terrainCulling.SetBuffer(0, voxels.positionsId, commands.positionsBuffer);
            commands.commandsBuffer.SetCounterValue(0);
            voxels.terrainCulling.Dispatch(0, terrain.MeshCount / VoxelRenderers.terrainCullingGroupSize, 1, 1);
            GraphicsBuffer.CopyCount(commands.commandsBuffer, voxels.counterBuffer, 0);
            uint[] data = new uint[1];
            voxels.counterBuffer.GetData(data);
            return (int)data[0];
        }


        private void OnDestroy() {
            commands?.Dispose();
        }


        internal class Commands {
            internal readonly GraphicsBuffer commandsBuffer;
            internal readonly GraphicsBuffer positionsBuffer;
            internal readonly RenderParams renderParams;

            /// <summary>
            /// Create command buffers and render params
            /// </summary>
            /// <param name="terrain">Terrain to render</param>
            /// <param name="target">Target camera</param>
            internal Commands(VoxelTerrain terrain, Camera target) {
                VoxelRenderers voxels = VoxelRenderers.Instance;

                commandsBuffer = new(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Counter | GraphicsBuffer.Target.Structured,
                    terrain.MeshCount, GraphicsBuffer.IndirectDrawIndexedArgs.size
                );
                GraphicsBuffer.IndirectDrawIndexedArgs[] commands = new GraphicsBuffer.IndirectDrawIndexedArgs[terrain.MeshCount];
                for (int i = 0; i < terrain.MeshCount; i++) commands[i] = new() { instanceCount = 1 };
                commandsBuffer.SetData(commands);

                positionsBuffer = new(GraphicsBuffer.Target.Structured, terrain.MeshCount, 4 * sizeof(float));
                float4[] positions = new float4[terrain.MeshCount];
                positionsBuffer.SetData(positions);

                MaterialPropertyBlock props = new();
                props.SetBuffer(voxels.facesId, terrain.facesBuffer);
                props.SetBuffer(voxels.colorsId, terrain.colorsBuffer);
                props.SetBuffer(voxels.positionsId, positionsBuffer);
                renderParams = new(voxels.terrainMaterial) {
                    camera = target,
                    worldBounds = new(Vector3.zero, new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)),
                    matProps = props
                };
            }

            internal void Dispose() {
                commandsBuffer.Dispose();
                positionsBuffer.Dispose();
            }
        }
    }

}