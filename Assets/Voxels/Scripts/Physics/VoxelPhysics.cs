using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Physics {
    
    /// <summary>
    /// Global physics data
    /// </summary>
    public class VoxelPhysics : MonoBehaviour {
        public static VoxelPhysics Instance { get; private set; }

        [SerializeField] private float size;
        [SerializeField] private Vector3 offset;
        [SerializeField] private int maxDepth = 10;

        private PhysicsData data;
        private readonly Dictionary<VoxelColumns, BinaryOctree> meshOctrees = new();
        private readonly List<VoxelMeshCollider> meshColliders = new();
        private readonly List<VoxelBoxCollider> boxColliders = new();

        
        private void Awake() {
            if (Instance) throw new InvalidOperationException("Can't create more than one VoxelPhysics in a scene");
            Instance = this;
            data = new PhysicsData(maxDepth, offset, size);
        }

        private void OnDestroy() {
            Instance = null;
            data.Dispose();
            foreach (BinaryOctree octree in meshOctrees.Values) {
                octree.Dispose();
            }
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="ray">The ray</param>
        /// <param name="maxDistance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="hitInfo">Information about the hit point if the ray hit a collider</param>
        /// <returns>Whether the ray hit a collider</returns>
        public bool Raycast(Ray ray, float maxDistance, int layerMask, out VoxelRaycastHit hitInfo) {
            bool hit = data.Raycast(ray.origin, ray.direction, maxDistance, out float3 point, out float3 normal, out ColliderType type, out int index);
            GameObject collider = type switch {
                ColliderType.Mesh => meshColliders[index].gameObject,
                ColliderType.Box => boxColliders[index].gameObject,
                _ => null  
            };
            hitInfo = new VoxelRaycastHit(point, normal, collider);
            return hit;
        }


        /// <summary>
        /// Add a mesh collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void AddMeshCollider(VoxelMeshCollider collider) {
            meshColliders.Add(collider);
            if (!meshOctrees.TryGetValue(collider.mesh.Voxels, out BinaryOctree octree)) {
                octree = new BinaryOctree(collider.mesh.Voxels);
                meshOctrees[collider.mesh.Voxels] = octree;
            }
            data.AddMeshCollider(new BinaryOctree(octree, collider.transform), collider.gameObject.layer);
        }

        /// <summary>
        /// Add a box collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void AddBoxCollider(VoxelBoxCollider collider) {
            boxColliders.Add(collider);
            Vector3 center = collider.Center + collider.transform.position;
            Box box = new(center - collider.Size / 2, center + collider.Size / 2);
            data.AddBoxCollider(box, collider.gameObject.layer);
        }
    }

}