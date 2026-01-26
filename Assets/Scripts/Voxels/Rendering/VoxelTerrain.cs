using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelTerrain : MonoBehaviour {
        [SerializeField] private TextAsset voxelsAsset;
        public int maxHorizontalSize = 64;
        public int mergeNormalsThreshold = 256;
        [NonSerialized] public VoxelColumns<char> voxels;

        internal GraphicsBuffer facesBuffer { get; private set; }
        internal GraphicsBuffer meshesBuffer { get; private set; }

        private TerrainMeshGenerator generator;
        private bool generating;
        internal bool Created => facesBuffer != null;
        internal int MeshCount => meshesBuffer.count;


        private void Update() {
            if (!voxels.Created && voxelsAsset) voxels = new(voxelsAsset);
            if (voxels.Created && !generating && !Created) {
                generator = new(maxHorizontalSize, mergeNormalsThreshold, 1024);
                generator.Generate(voxels);
                generating = true;
            }
            if (generating && generator.handle.IsCompleted) {
                generator.handle.Complete();
                FinishGenerate();
            }
        }

        private void OnDestroy() {
            Dispose();
        }

        internal void Dispose() {
            if (Created) {
                facesBuffer.Dispose();
                meshesBuffer.Dispose();
            }
            else if (generating) {
                generator.handle.Complete();
                generator.Dispose();
                generating = false;
            }
            if (voxels.Created) voxels.Dispose();
        }


        /// <summary>
        /// Complete terrain generation now
        /// </summary>
        public void CompleteGenerate() {
            if (Created) throw new InvalidOperationException("Can't call CompleteGenerate : terrain already generated");
            if (!voxels.Created) throw new InvalidOperationException("Can't call CompleteGenerate : voxels not set");
            if (!generating) generator.Generate(voxels);
            generator.handle.Complete();
            FinishGenerate();
        }

        private unsafe void FinishGenerate() {
            while (generator.meshes.Length % VoxelData.terrainCullingGroupSize != 0) generator.meshes.Add(default);
            facesBuffer = new(GraphicsBuffer.Target.Structured, generator.faces.Length, sizeof(VoxelTerrainFace));
            facesBuffer.SetData(generator.faces.AsArray());
            meshesBuffer = new(GraphicsBuffer.Target.Structured, generator.meshes.Length, sizeof(VoxelMesh));
            meshesBuffer.SetData(generator.meshes.AsArray());
            generator.Dispose();
            generating = false;
        }
    }

}