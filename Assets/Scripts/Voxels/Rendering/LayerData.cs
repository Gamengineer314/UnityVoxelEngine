using UnityEngine;
using Unity.Collections;
using Voxels.Collections;

namespace Voxels.Rendering {
    /// <summary>
    /// Rendering data for a layer
    /// </summary>
    internal class LayerData {
        public CPULayerData cpu;
        public GPULayerData gpu;

        public LayerData(ShaderParameters parameters) {
            cpu = new CPULayerData(parameters);
            gpu = new GPULayerData(parameters);
        }

        public void Dispose() {
            cpu.Dispose();
            gpu.Dispose();
        }
    }



    /// <summary>
    /// CPU-side data
    /// </summary>
    internal struct CPULayerData {
        public NativeList<VoxelChunk> chunks;
        public NativeList<VoxelFace> faces;
        public NativeList<Color32> colors;
        public NativeList<Matrix4x4> transforms;
        public NativeHashMap<uint, int> colorIndices;

        public CPULayerData(ShaderParameters parameters) {
            faces = new(0, Allocator.Persistent);
            chunks = new(0, Allocator.Persistent);
            colors = new(0, Allocator.Persistent);
            transforms = parameters.transform ? new(0, Allocator.Persistent) : default;
            colorIndices = parameters.texture ? default : new(0, Allocator.Persistent);
        }

        public void Dispose() {
            faces.Dispose();
            chunks.Dispose();
            colors.Dispose();
            transforms.Dispose();
            colorIndices.Dispose();
        }
    }



    /// <summary>
    /// GPU-side data
    /// </summary>
    internal readonly struct GPULayerData {
        public readonly ListBuffer<VoxelChunk> chunks;
        public readonly ListBuffer<VoxelFace> faces;
        public readonly ListBuffer<Color32> colors;
        public readonly ListBuffer<Matrix4x4> transforms;

        public GPULayerData(ShaderParameters parameters) {
            chunks = new ListBuffer<VoxelChunk>(GraphicsBuffer.Target.Structured);
            faces = new ListBuffer<VoxelFace>(GraphicsBuffer.Target.Structured);
            colors = new ListBuffer<Color32>(GraphicsBuffer.Target.Structured);
            transforms = parameters.transform ? new ListBuffer<Matrix4x4>(GraphicsBuffer.Target.Structured) : null;
        }

        public void Dispose() {
            chunks.Dispose();
            faces.Dispose();
            colors.Dispose();
            transforms?.Dispose();
        }
    }
}