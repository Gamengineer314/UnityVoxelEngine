using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering data for a layer
    /// </summary>
    internal class LayerBuffers {
        public GraphicsBuffer chunksBuffer;
        public GraphicsBuffer transformsBuffer;
        public NativeList<VoxelChunk> chunks;
        public NativeList<Matrix4x4> transforms;


        public unsafe LayerBuffers(ShaderParameters parameters) {
            chunksBuffer = new(GraphicsBuffer.Target.Structured, 4096, sizeof(VoxelChunk));
            transformsBuffer = parameters.transform ? new(GraphicsBuffer.Target.Structured, 4096, sizeof(Matrix4x4)) : null;
            chunks = new(Allocator.Persistent);
            transforms = parameters.transform ? new(Allocator.Persistent) : default;
        }

        public void Dispose() {
            chunksBuffer.Dispose();
            transformsBuffer?.Dispose();
            chunks.Dispose();
            transforms.Dispose();
        }


        /// <summary>
        /// Synchronize a range of the chunks buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        public void SynchronizeChunks(int start, int count) {
            if (chunks.Length > chunksBuffer.count) {
                BufferUtility.Grow(ref chunksBuffer, chunks.AsArray());
            }
            else chunksBuffer.SetData(chunks.AsArray(), start, start, count);
        }

        /// <summary>
        /// Synchronize a range of the transforms buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        public void SynchronizeTransforms(int start, int count) {
            if (transforms.Length > transformsBuffer.count) {
                BufferUtility.Grow(ref transformsBuffer, transforms.AsArray());
            }
            else transformsBuffer.SetData(transforms.AsArray(), start, start, count);
        }
    }

}