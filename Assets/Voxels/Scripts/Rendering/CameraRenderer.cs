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

        public void Render() {
            MeshBuffers meshBuffers = VoxelRenderer.Instance.meshBuffers;
            foreach ((int layer, Material material, VoxelLayer voxelLayer) in VoxelLayer.GetLayers(camera.cullingMask)) {
                if (!renderers.TryGetValue((layer, material), out LayerRenderer renderer)) {
                    renderer = new LayerRenderer(material, camera, layer, voxelLayer.parameters);
                    renderers[(layer, material)] = renderer;
                }
                renderer.SetBuffers(voxelLayer.layerBuffers.chunksBuffer, meshBuffers.facesBuffer, meshBuffers.colorsBuffer, voxelLayer.layerBuffers.transformsBuffer);
                renderer.Cull(voxelLayer.layerBuffers.chunks.Length);
                renderer.Render();
            }
        }
    }

}