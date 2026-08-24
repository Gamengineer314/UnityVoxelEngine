using UnityEngine;

namespace Voxels.Physics {
    
    /// <summary>
    /// Base class for all voxel colliders
    /// </summary>
    public abstract class VoxelCollider : MonoBehaviour {
        internal int index = -1; // Index of the collider in physics data
        internal Matrix4x4 prevTransform; // Last transform processed by physics data
    }

}