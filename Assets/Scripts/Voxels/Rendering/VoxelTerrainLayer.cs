using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Generates and contains all voxel terrains in a layer
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelTerrainLayer : MonoBehaviour {
        [SerializeField] private int meshSize = 64;
        [SerializeField] private int mergeNormalsThreshold = 256;
        [SerializeField] private int jobHorizontalSize = 1024;

        private TerrainMeshGenerator generator;
        private readonly Queue<VoxelObject> nextObjects = new();
        private readonly Dictionary<VoxelObject, ObjectData> objects = new();

        internal ListBuffer<VoxelMesh> meshesBuffer;
        internal ListBuffer<VoxelFace> facesBuffer;
        internal ListBuffer<Color32> colorsBuffer;
        internal bool IsEmpty => meshesBuffer.Length == 0;

        private static readonly VoxelTerrainLayer[] layers = new VoxelTerrainLayer[32];
        public static VoxelTerrainLayer GetLayer(int layer) => layers[layer];


        internal void Awake() {
            int layer = gameObject.layer;
            if (GetLayer(layer) || VoxelObjectLayer.GetLayer(layer)) {
                throw new InvalidOperationException("Can't create more than one VoxelTerrainLayer or VoxelObjectLayer per layer");
            }
            layers[gameObject.layer] = this;

            generator = new TerrainMeshGenerator(meshSize, mergeNormalsThreshold, jobHorizontalSize);
            meshesBuffer = new ListBuffer<VoxelMesh>(GraphicsBuffer.Target.Structured);
            facesBuffer = new ListBuffer<VoxelFace>(GraphicsBuffer.Target.Structured);
            colorsBuffer = new ListBuffer<Color32>(GraphicsBuffer.Target.Structured);
        }


        internal void OnDestroy() {
            if (nextObjects.Count > 0) generator.Complete();
            generator.Dispose();
            meshesBuffer.Dispose();
            facesBuffer.Dispose();
            colorsBuffer.Dispose();
            nextObjects.Clear();
            objects.Clear();
        }


        private void Update() {
            if (!generator.IsCompleted) return;

            // Update positions
            NativeArray<VoxelMesh> meshes = generator.Meshes;
            foreach (KeyValuePair<VoxelObject, ObjectData> kv in objects) {
                VoxelObject obj = kv.Key;
                ObjectData data = kv.Value;
                Vector3 pos = obj.transform.position;
                if (pos != data.prevPos) {
                    float3 added = pos - data.prevPos;
                    for (int i = data.startMesh; i < data.endMesh; i++) {
                        VoxelMesh mesh = meshes[i];
                        mesh = new(mesh.center + added, mesh.size, mesh.position + added, mesh.Normal, mesh.FaceCount, mesh.StartFace);
                        meshes[i] = mesh;
                        meshesBuffer[i] = mesh;
                    }
                    data.prevPos = pos;
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
            generator.Generate(next.voxels, next.offset + next.transform.position);
        }

        /// <summary>
        /// Complete current object generation
        /// </summary>
        private void EndGenerate() {
            generator.Complete();
            VoxelObject completed = nextObjects.Dequeue();
            objects[completed] = new ObjectData(completed.transform.position, meshesBuffer.Length, generator.Meshes.Length);
            meshesBuffer.AddRange(generator.Meshes.GetSubArray(meshesBuffer.Length, generator.Meshes.Length - meshesBuffer.Length));
            facesBuffer.AddRange(generator.Faces.GetSubArray(facesBuffer.Length, generator.Faces.Length - facesBuffer.Length));
            colorsBuffer.AddRange(generator.Colors.GetSubArray(colorsBuffer.Length, generator.Colors.Length - colorsBuffer.Length));

            // Add zeros until multiple of group size
            int lastGroup = meshesBuffer.Length % TerrainLayerRenderer.terrainCullingGroupSize;
            if (lastGroup > 0) {
                meshesBuffer.AddRange(new VoxelMesh[TerrainLayerRenderer.terrainCullingGroupSize - lastGroup]);
                meshesBuffer.Length = generator.Meshes.Length;
            }
        }


        private class ObjectData {
            public Vector3 prevPos;
            public int startMesh;
            public int endMesh;

            public ObjectData(Vector3 prevPos, int startMesh, int endMesh) {
                this.prevPos = prevPos;
                this.startMesh = startMesh;
                this.endMesh = endMesh;
            }
        }
    }

}