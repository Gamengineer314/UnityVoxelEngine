using System;
using Unity.Mathematics;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Voxel objects global data
    /// </summary>
    [ExecuteInEditMode]
    public class VoxelObjects : MonoBehaviour {
        internal static VoxelObjects Instance { get; private set; }

        public int meshSize = 64;
        public int mergeNormalsThreshold = 256;

        private ListBuffer<VoxelFace> facesBuffer;
        private ListBuffer<ObjectMesh> meshesBuffer;
        private ListBuffer<Color32> colorsBuffer;
        private ListBuffer<Matrix4x4> transformsBuffer;

        internal GraphicsBuffer FacesBuffer => facesBuffer.buffer;
        internal GraphicsBuffer MeshesBuffer => meshesBuffer.buffer;
        internal GraphicsBuffer ColorsBuffer => colorsBuffer.buffer;
        internal GraphicsBuffer TransformsBuffer => transformsBuffer.buffer;
        internal event Action objectAdded;

        internal int InstanceCount => transformsBuffer.Length;

        private ObjectMeshGenerator generator;


        internal void Awake() {
            Instance = this;
            facesBuffer = new ListBuffer<VoxelFace>(GraphicsBuffer.Target.Structured);
            meshesBuffer = new ListBuffer<ObjectMesh>(GraphicsBuffer.Target.Structured);
            colorsBuffer = new ListBuffer<Color32>(GraphicsBuffer.Target.Structured);
            transformsBuffer = new ListBuffer<Matrix4x4>(GraphicsBuffer.Target.Structured);
            generator = new(meshSize, mergeNormalsThreshold);
        }


        internal void OnDestroy() {
            facesBuffer.Dispose();
            meshesBuffer.Dispose();
            colorsBuffer.Dispose();
            transformsBuffer.Dispose();
            generator.Dispose();
        }


        /// <summary>
        /// Add an object
        /// </summary>
        /// <param name="voxels">Voxels of the object</param>
        /// <param name="offset">Position offset</param>
        /// <param name="transform">Transform of the object</param>
        /// <returns>Identifier of the object</returns>
        internal int AddObject(VoxelColumns voxels, float3 offset, Matrix4x4 transform) {
            int startInstance = transformsBuffer.Length;
            generator.Generate(voxels, offset, startInstance);
            generator.Complete();
            facesBuffer.AddRange(generator.Faces);
            meshesBuffer.AddRange(generator.Meshes);
            colorsBuffer.AddRange(generator.Colors);
            transformsBuffer.Add(transform);
            generator.Clear();
            objectAdded?.Invoke();
            return startInstance;
        }


        /// <summary>
        /// Modify the transform of an object
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        /// <param name="transform">Transform of the object</param>
        internal void SetTransform(int id, Matrix4x4 transform) {
            transformsBuffer[id] = transform;
        }
    }

}