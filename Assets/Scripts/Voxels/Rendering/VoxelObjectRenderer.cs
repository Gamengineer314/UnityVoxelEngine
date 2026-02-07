using UnityEngine;
using UnityEngine.PlayerLoop;

namespace Voxels.Rendering {
    
    public class VoxelObjectRenderer : MonoBehaviour {
        public VoxelObjects objects; // Objects to render
        public Camera target; // Camera to render the objects on

        private ObjectRenderParams renderParams;
        public bool Rendering => renderParams is not null;


        private void Start() {
            renderParams = new ObjectRenderParams(
                target, VoxelRenderers.Instance.objectsMaterial,
                objects.MeshesBuffer, objects.FacesBuffer, objects.ColorsBuffer, objects.TransformsBuffer
            );
            objects.objectAdded += UpdateBuffers;
        }

        private void OnDestroy() {
            objects.objectAdded -= UpdateBuffers;
            renderParams.Dispose();
        }

        private void UpdateBuffers() {
            renderParams.MeshesBuffer = objects.MeshesBuffer;
            renderParams.FacesBuffer = objects.FacesBuffer;
            renderParams.ColorsBuffer = objects.ColorsBuffer;
            renderParams.TransformsBuffer = objects.TransformsBuffer;
        }


        private void Update() {
            if (objects.InstanceCount > 0) {
                renderParams.Cull(target, VoxelRenderers.Instance.objectsCulling);
                renderParams.Render();
            }
        }
    }

}