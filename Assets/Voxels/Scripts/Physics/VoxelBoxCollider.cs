using System;
using Unity.Mathematics;
using UnityEngine;

namespace Voxels.Physics {
    
    public class VoxelBoxCollider : VoxelCollider {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size;
        
        /// <summary>
        /// Get the box in world space
        /// </summary>
        public Box Box {
            get {
                float3 min = center - size / 2;
                float3 max = center + size / 2;
                float4x4 toWorld = transform.localToWorldMatrix;
                min = math.mul(toWorld, new float4(min, 1)).xyz;
                max = math.mul(toWorld, new float4(max, 1)).xyz;
                return new Box(math.min(min, max), math.max(min, max));
            }
        }


        public Vector3 Center {
            get => center;
            set {
                center = value;
                if (isActiveAndEnabled) Reinsert();
            }
        }

        public Vector3 Size {
            get => size;
            set {
                size = value;
                if (isActiveAndEnabled) Reinsert();
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (VoxelPhysics.Instance && isActiveAndEnabled) Reinsert();
        }
#endif


        private void Start() {
            if (index == -1) VoxelPhysics.Instance.AddBoxCollider(this);
        }

        private void OnEnable() {
            if (index == -1 && VoxelPhysics.Instance) VoxelPhysics.Instance.AddBoxCollider(this);
        }

        private void OnDisable() {
            if (VoxelPhysics.Instance) VoxelPhysics.Instance.RemoveBoxCollider(this);
        }


        /// <summary>
        /// Reinsert the collider in the physics octree after updating its transform.
        /// Subsequent physics queries will reflect the new transform.
        /// </summary>
        /// <remarks>This is done automatically each frame in the LateUpdate</remarks>
        public void Reinsert() {
            if (!isActiveAndEnabled) throw new InvalidOperationException("The collider isn't active");
            VoxelPhysics.Instance.ReinsertBoxCollider(this);
        }
    }

}