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
            foreach ((int layer, Material material, VoxelLayer voxelLayer) in VoxelLayer.GetLayers(camera.cullingMask)) {
                if (!renderers.TryGetValue((layer, material), out LayerRenderer renderer)) {
                    renderer = new LayerRenderer(material, camera, layer, voxelLayer.shaderParams);
                    renderers[(layer, material)] = renderer;
                }
                renderer.SetBuffers(voxelLayer.data.gpu.chunks.buffer, voxelLayer.data.gpu.faces.buffer, voxelLayer.data.gpu.colors.buffer, voxelLayer.data.gpu.transforms?.buffer);
                renderer.Cull(voxelLayer.data.gpu.chunks.Length);
                renderer.Render();
            }
        }
    }

}