using UnityEngine;
using Voxels.Rendering;

namespace Voxels.Physics {
    
    [RequireComponent(typeof(VoxelMesh))]
    public class VoxelMeshCollider : MonoBehaviour {
        internal VoxelMesh mesh;
        internal int index; // Index of the collider in physics data


        private void Start() {
            mesh = GetComponent<VoxelMesh>();
            VoxelPhysics.Instance.AddMeshCollider(this);
        }

        private void OnDestroy() {
            
        }
    }

}