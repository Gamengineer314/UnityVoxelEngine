using System.Collections.Generic;
using UnityEngine;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Renderer for a camera
    /// </summary>
    internal class CameraRenderer {
        private readonly Camera camera;
        private readonly Dictionary<(int, Material), LayerRenderer> renderers = new();

        public CameraRenderer(Camera camera) {
            this.camera = camera;
        }

        public void Dispose() {
            foreach (LayerRenderer renderer in renderers.Values) {
                renderer.Dispose();
            }
            renderers.Clear();
        }

        public void Render(VoxelRenderer voxelRenderer) {
            MeshBuffers meshBuffers = voxelRenderer.meshBuffers;
            foreach (VoxelLayer layer in voxelRenderer.GetLayers(camera.cullingMask)) {
                if (!renderers.TryGetValue((layer.layer, layer.material), out LayerRenderer renderer)) {
                    renderer = new LayerRenderer(voxelRenderer, layer.material, camera, layer.layer, layer.parameters);
                    renderers[(layer.layer, layer.material)] = renderer;
                }
                renderer.SetBuffers(
                    layer.layerBuffers.chunksBuffer,
                    meshBuffers.facesBuffer,
                    meshBuffers.colorsBuffer,
                    layer.layerBuffers.transformsBuffer,
                    layer.layerBuffers.renderedTransformsSize
                );
                renderer.Cull(layer.layerBuffers.ChunkCount);
                renderer.Render();
            }
        }
    }

}