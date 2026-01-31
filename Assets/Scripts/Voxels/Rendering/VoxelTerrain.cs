using System;
using UnityEngine;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    [ExecuteInEditMode]
    public class VoxelTerrain : MonoBehaviour {
        [SerializeField] private TextAsset voxelsAsset;
        [NonSerialized] public VoxelColumns voxels;
        public Vector3 offset;
        public int meshSize = 64;
        public int mergeNormalsThreshold = 256;

        internal GraphicsBuffer facesBuffer { get; private set; }
        internal GraphicsBuffer meshesBuffer { get; private set; }
        internal GraphicsBuffer colorsBuffer { get; private set; }

        private TerrainMeshGenerator generator;
        private bool generating;
        internal bool Created => facesBuffer != null;
        internal int MeshCount => meshesBuffer.count;


        private void Update() {
            if (!voxels.Created && voxelsAsset) {
                voxels = new(voxelsAsset);
            }
            if (voxels.Created && !generating && !Created) {
                StartGenerate();
                generating = true;
            }
            if (generating && generator.IsCompleted) {
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
                colorsBuffer.Dispose();
            }
            else if (generating) {
                generator.Complete();
                generator.DisposeJobs();
                generator.Dispose();
                generating = false;
            }
            if (voxels.Created) {
                voxels.Dispose();
            }
        }


        private void StartGenerate() {
            generator = new(meshSize, mergeNormalsThreshold, 1024);
            generator.Generate(voxels, offset);
        }

        /// <summary>
        /// Complete terrain generation now
        /// </summary>
        public void CompleteGenerate() {
            if (Created) throw new InvalidOperationException("Can't call CompleteGenerate : terrain already generated");
            if (!voxels.Created) throw new InvalidOperationException("Can't call CompleteGenerate : voxels not set");
            if (!generating) StartGenerate();
            FinishGenerate();
        }

        private unsafe void FinishGenerate() {
            generator.Complete();
            int ceiledMeshes = VoxelRenderers.cullingGroupSize * Mathf.CeilToInt((float)generator.Meshes.Length / VoxelRenderers.cullingGroupSize);
            facesBuffer = new(GraphicsBuffer.Target.Structured, generator.Faces.Length, sizeof(VoxelFace));
            facesBuffer.SetData(generator.Faces);
            meshesBuffer = new(GraphicsBuffer.Target.Structured, ceiledMeshes, sizeof(VoxelMesh));
            meshesBuffer.SetData(generator.Meshes);
            int additional = ceiledMeshes - generator.Meshes.Length;
            if (additional > 0) meshesBuffer.SetData(new VoxelMesh[additional], 0, generator.Meshes.Length, additional);
            colorsBuffer = new(GraphicsBuffer.Target.Structured, generator.Colors.Length, sizeof(Color32));
            colorsBuffer.SetData(generator.Colors);
            generator.DisposeJobs();
            generator.Dispose();
            generating = false;
        }
    }

}