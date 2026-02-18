using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Generates and contains all voxel objects in a layer
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelObjectLayer : MonoBehaviour {
        [SerializeField] private int meshSize = 64;
        [SerializeField] private int mergeNormalsThreshold = 256;
        [SerializeField] private int jobHorizontalSize = 1024;

        private ObjectMeshGenerator generator;
        private NativeList<Matrix4x4> transforms;
        private readonly Queue<VoxelObject> nextObjects = new();
        private readonly Dictionary<VoxelObject, ObjectData> objects = new();

        internal ListBuffer<ObjectMesh> meshesBuffer;
        internal ListBuffer<VoxelFace> facesBuffer;
        internal ListBuffer<Color32> colorsBuffer;
        internal ListBuffer<Matrix4x4> transformsBuffer;
        internal bool IsEmpty => meshesBuffer.Length == 0;

        private static readonly VoxelObjectLayer[] layers = new VoxelObjectLayer[32];
        public static VoxelObjectLayer GetLayer(int layer) => layers[layer];


        internal void Awake() {
            int layer = gameObject.layer;
            if (GetLayer(layer) || VoxelTerrainLayer.GetLayer(layer)) {
                throw new InvalidOperationException("Can't create more than one VoxelTerrainLayer or VoxelObjectLayer per layer");
            }
            layers[gameObject.layer] = this;
            
            generator = new ObjectMeshGenerator(meshSize, mergeNormalsThreshold, jobHorizontalSize);
            transforms = new NativeList<Matrix4x4>(Allocator.Persistent);
            meshesBuffer = new ListBuffer<ObjectMesh>(GraphicsBuffer.Target.Structured);
            facesBuffer = new ListBuffer<VoxelFace>(GraphicsBuffer.Target.Structured);
            colorsBuffer = new ListBuffer<Color32>(GraphicsBuffer.Target.Structured);
            transformsBuffer = new ListBuffer<Matrix4x4>(GraphicsBuffer.Target.Structured);
        }


        internal void OnDestroy() {
            if (nextObjects.Count > 0) generator.Complete();
            generator.Dispose();
            transforms.Dispose();
            meshesBuffer.Dispose();
            facesBuffer.Dispose();
            colorsBuffer.Dispose();
            transformsBuffer.Dispose();
            nextObjects.Clear();
            objects.Clear();
        }


        private void Update() {
            if (!generator.IsCompleted) return;

            // Update transforms
            foreach (KeyValuePair<VoxelObject, ObjectData> kv in objects) {
                VoxelObject obj = kv.Key;
                ObjectData data = kv.Value;
                Matrix4x4 transform = obj.transform.localToWorldMatrix;
                if (transform != data.prevTransform) {
                    transforms[data.startInstance] = transform;
                    transformsBuffer[data.startInstance] = transform;
                    data.prevTransform = transform;
                }
            }

            // Next generation
            if (nextObjects.Count > 0) {
                EndGenerate();
                if (nextObjects.Count > 0) StartGenerate();
            }
        }


        /// <summary>
        /// Add an object to this layer
        /// </summary>
        /// <param name="obj">The object</param>
        internal void AddObject(VoxelObject obj) {
            nextObjects.Enqueue(obj);
            if (nextObjects.Count == 1) StartGenerate();
        }


        /// <summary>
        /// Complete all queued generations
        /// </summary>
        public void Complete() {
            while (nextObjects.Count > 0) {
                EndGenerate();
                if (nextObjects.Count > 0) StartGenerate();
            }
        }


        /// <summary>
        /// Start generating next object
        /// </summary>
        private void StartGenerate() {
            VoxelObject next = nextObjects.Peek();
            generator.Generate(next.voxels, next.offset, transformsBuffer.Length);
        }

        /// <summary>
        /// Complete current object generation
        /// </summary>
        private void EndGenerate() {
            generator.Complete();
            VoxelObject completed = nextObjects.Dequeue();
            Matrix4x4 transform = completed.transform.localToWorldMatrix;
            objects[completed] = new ObjectData(transform, meshesBuffer.Length, generator.Meshes.Length, transformsBuffer.Length);
            meshesBuffer.AddRange(generator.Meshes.GetSubArray(meshesBuffer.Length, generator.Meshes.Length - meshesBuffer.Length));
            facesBuffer.AddRange(generator.Faces.GetSubArray(facesBuffer.Length, generator.Faces.Length - facesBuffer.Length));
            colorsBuffer.AddRange(generator.Colors.GetSubArray(colorsBuffer.Length, generator.Colors.Length - colorsBuffer.Length));
            transforms.Add(transform);
            transformsBuffer.Add(transform);
        }


        private class ObjectData {
            public Matrix4x4 prevTransform;
            public int startMesh;
            public int endMesh;
            public int startInstance;

            public ObjectData(Matrix4x4 prevTransform, int startMesh, int endMesh, int startInstance) {
                this.prevTransform = prevTransform;
                this.startMesh = startMesh;
                this.endMesh = endMesh;
                this.startInstance = startInstance;
            }
        }
    }

}