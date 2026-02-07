using UnityEngine;

namespace Voxels.Rendering {

    public class VoxelTerrainRenderer : MonoBehaviour {
        public VoxelTerrain terrain; // Terrain to render
        public Camera target; // Camera to render the terrain on

        private VoxelRenderParams renderParams;
        public bool Rendering => renderParams is not null;

        private void LateUpdate() {
            if (!Rendering && terrain.Created) {
                renderParams = new VoxelRenderParams(
                    target, VoxelRenderers.Instance.terrainMaterial,
                    terrain.meshesBuffer, terrain.facesBuffer, terrain.colorsBuffer
                );
            }
            if (Rendering) {
                renderParams.Cull(target, VoxelRenderers.Instance.terrainCulling, VoxelRenderers.terrainCullingGroupSize);
                renderParams.Render();
            }
        }

        private void OnDestroy() {
            renderParams?.Dispose();
        }
    }

}