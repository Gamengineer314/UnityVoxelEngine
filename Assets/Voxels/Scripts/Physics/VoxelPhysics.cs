using System;
using System.Collections.Generic;
using Unity.Collections;
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
        internal OctreeGenerator generator { get; private set; }
        private readonly Dictionary<VoxelColumns, OctreeBuilder> octrees = new();
        private readonly Dictionary<VoxelColumns, int> referenceCounters = new();
        private readonly List<VoxelMeshCollider> meshColliders = new();
        private readonly List<VoxelBoxCollider> boxColliders = new();

        
        private void Awake() {
            if (Instance) throw new InvalidOperationException("Can't create more than one VoxelPhysics in a scene");
            Instance = this;
            data = new PhysicsData(maxDepth, offset, size);
            generator = new OctreeGenerator(octrees);
        }

        private void OnDestroy() {
            Instance = null;
            data.Dispose();
            generator.Dispose();
            foreach (OctreeBuilder octree in octrees.Values) {
                octree.Dispose();
            }
        }


        private void LateUpdate() {
            generator.Update();
            for (int i = meshColliders.Count - 1; i >= 0; i--) {
                VoxelMeshCollider collider = meshColliders[i];
                if (collider.transform.localToWorldMatrix != collider.prevTransform || collider.gameObject.layer != data.colliders[collider.index].layer) {
                    ReinsertMeshCollider(collider);
                }
            }
            for (int i = boxColliders.Count - 1; i >= 0; i--) {
                VoxelBoxCollider collider = boxColliders[i];
                if (collider.transform.localToWorldMatrix != collider.prevTransform || collider.gameObject.layer != data.colliders[collider.index].layer) {
                    ReinsertBoxCollider(collider);
                }
            }
        }


        /// <summary>
        /// Increment the reference counter of a mesh
        /// </summary>
        /// <param name="voxels">Voxels of the mesh</param>
        internal void AddReference(VoxelColumns voxels) {
            referenceCounters[voxels] = referenceCounters.GetValueOrDefault(voxels, 0) + 1;
        }

        /// <summary>
        /// Decrement the reference counter of a mesh
        /// </summary>
        /// <param name="voxels">Voxels of the mesh</param>
        internal void RemoveReference(VoxelColumns voxels) {
            int counter = referenceCounters[voxels] - 1;
            if (counter == 0) {
                octrees[voxels].Dispose();
                octrees.Remove(voxels);
                referenceCounters.Remove(voxels);
            }
            else referenceCounters[voxels] = counter;
        }


        /// <summary>
        /// Add a mesh collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void AddMeshCollider(VoxelMeshCollider collider) {
            meshColliders.Add(collider);
            TransformedOctree octree = new(octrees[collider.voxels], collider.voxels, collider.transform);
            collider.index = PhysicsData.AddMeshCollider(ref data, octree, collider.gameObject.layer);
            collider.prevTransform = collider.transform.localToWorldMatrix;
        }

        /// <summary>
        /// Add a box collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void AddBoxCollider(VoxelBoxCollider collider) {
            boxColliders.Add(collider);
            collider.index = PhysicsData.AddBoxCollider(ref data, collider.Box, collider.gameObject.layer);
            collider.prevTransform = collider.transform.localToWorldMatrix;
        }

        /// <summary>
        /// Remove a mesh collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void RemoveMeshCollider(VoxelMeshCollider collider) {
            int index = collider.index;
            int swapIndex = meshColliders[^1].index;
            int meshIndex = PhysicsData.RemoveMeshCollider(ref data, index, swapIndex);
            meshColliders.RemoveAtSwapBack(meshIndex);
            collider.index = -1;
        }

        /// <summary>
        /// Remove a box collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void RemoveBoxCollider(VoxelBoxCollider collider) {
            int index = collider.index;
            int swapIndex = boxColliders[^1].index;
            int boxIndex = PhysicsData.RemoveBoxCollider(ref data, index, swapIndex);
            boxColliders.RemoveAtSwapBack(boxIndex);
            collider.index = -1;
        }

        /// <summary>
        /// Reinsert a mesh collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void ReinsertMeshCollider(VoxelMeshCollider collider) {
            TransformedOctree octree = new(octrees[collider.voxels], collider.voxels, collider.transform);
            PhysicsData.ReinsertMeshCollider(ref data, collider.index, octree, collider.gameObject.layer);
            collider.prevTransform = collider.transform.localToWorldMatrix;
        }

        /// <summary>
        /// Reinsert a box collider
        /// </summary>
        /// <param name="collider">The collider</param>
        internal void ReinsertBoxCollider(VoxelBoxCollider collider) {
            PhysicsData.ReinsertBoxCollider(ref data, collider.index, collider.Box, collider.gameObject.layer);
            collider.prevTransform = collider.transform.localToWorldMatrix;
        }


        /// <summary>
        /// Raycast query
        /// </summary>
        /// <param name="origin">Origin of the ray</param>
        /// <param name="direction">Direction of the ray</param>
        /// <param name="maxDistance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="ignoredCollider">Collider to ignore</param>
        /// <param name="hitInfo">Information about the hit point if the ray hit a collider</param>
        /// <returns>Whether the ray hit a collider</returns>
        public bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, VoxelCollider ignoredCollider, out VoxelRaycastHit hitInfo) {
            bool hit = PhysicsData.Raycast(ref data, origin, direction, maxDistance, layerMask, ignoredCollider ? ignoredCollider.index : -1, out PhysicsData.RaycastHit info);
            hitInfo = GetInfo(info);
            return hit;
        }

        /// <summary>
        /// Move query with a box shape
        /// </summary>
        /// <param name="origin">Start position of the box</param>
        /// <param name="direction">Direction of the box</param>
        /// <param name="maxDistance">Maximum distance between the origin and the hit point</param>
        /// <param name="layerMask">Layers of colliders that are considered</param>
        /// <param name="ignoredCollider">Collider to ignore</param>
        /// <param name="hitInfo">Information about the hit point if the box hit a collider</param>
        /// <returns>Whether the box hit a voxel</returns>
        public bool MoveBox(Box origin, float3 direction, float maxDistance, int layerMask, VoxelCollider ignoredCollider, out VoxelRaycastHit hitInfo) {
            bool hit = PhysicsData.MoveBox(ref data, origin, direction, maxDistance, layerMask, ignoredCollider ? ignoredCollider.index : -1, out PhysicsData.RaycastHit info);
            hitInfo = GetInfo(info);
            return hit;
        }

        private VoxelRaycastHit GetInfo(PhysicsData.RaycastHit info)
            => new(info.distance, info.normal, info.type switch {
                ColliderType.Mesh => meshColliders[info.index],
                ColliderType.Box => boxColliders[info.index],
                _ => null
            });
    }

}