using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;
using System;
using Unity.Burst;

namespace Voxels.Rendering {

    /// <summary>
    /// Rendering data for a layer
    /// </summary>
    [BurstCompile]
    internal class LayerBuffers {
        public GraphicsBuffer chunksBuffer;
        public GraphicsBuffer transformsBuffer;
        public int renderedTransformsSize;

        private struct Arrays {
            public NativeList<VoxelChunk> chunks;
            public NativeList<int2> chunkLinks; // Prev and next chunk of each chunk
            public NativeList<int> firstChunks; // First chunk of each mesh
            public NativeList<Matrix4x4> transforms;
            public BufferAllocator transformsAllocator;
            public BufferAllocator renderedTransformsAllocator;
            public NativeList<int2> allocatorIndices; // Index of the memory chunk in [transformsAllocator] and [renderedTransformsAllocator] for each mesh
        }

        private Arrays arrays;

        public int ChunkCount => arrays.chunks.Length;


        public unsafe LayerBuffers(ShaderParameters parameters) {
            chunksBuffer = new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(VoxelChunk));
            transformsBuffer = parameters.transform ? new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(Matrix4x4)) : null;
            renderedTransformsSize = BufferUtility.minLength;
            arrays.chunks = new(Allocator.Persistent);
            arrays.chunkLinks = new(Allocator.Persistent);
            arrays.firstChunks = new(Allocator.Persistent);
            arrays.transforms = parameters.transform ? new(Allocator.Persistent) : default;
            arrays.transformsAllocator = parameters.instance ? new(Allocator.Persistent) : default;
            arrays.renderedTransformsAllocator = parameters.instance ? new(Allocator.Persistent) : default;
            arrays.allocatorIndices = parameters.instance ? new(Allocator.Persistent) : default;
        }

        public void Dispose() {
            chunksBuffer.Dispose();
            transformsBuffer?.Dispose();
            arrays.chunks.Dispose();
            arrays.chunkLinks.Dispose();
            arrays.firstChunks.Dispose();
            arrays.transforms.Dispose();
            arrays.transformsAllocator.Dispose();
            arrays.renderedTransformsAllocator.Dispose();
            arrays.allocatorIndices.Dispose();
        }


        /// <summary>
        /// Add a mesh
        /// <param name="chunks">Chunks of the mesh</param>
        /// </summary>
        public void AddMesh(NativeList<VoxelChunk> chunks) {
            int startInstance, startRenderedInstance;
            if (arrays.allocatorIndices.IsCreated) {
                int2 indices;
                indices.x = arrays.transformsAllocator.Allocate(1);
                arrays.transforms.Length = arrays.transformsAllocator.TotalSize;
                indices.y = arrays.renderedTransformsAllocator.Allocate(chunks.Length);
                arrays.allocatorIndices.Add(indices);
                startInstance = arrays.transformsAllocator[indices.x].start;
                startRenderedInstance = arrays.renderedTransformsAllocator[indices.y].start;
            }
            else {
                if (arrays.transforms.IsCreated) arrays.transforms.Length++;
                startInstance = arrays.firstChunks.Length;
                startRenderedInstance = 0;
            }

            // Add chunks
            int startChunk = arrays.chunks.Length;
            arrays.firstChunks.Add(startChunk);
            foreach (VoxelChunk chunk in chunks) {
                arrays.chunks.Add(new VoxelChunk(
                    chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                    chunk.Normal, chunk.StartFace, chunk.FaceCount, startInstance, startRenderedInstance, 1
                ));
                arrays.chunkLinks.Add(new int2(arrays.chunks.Length - 2, arrays.chunks.Length));
                startRenderedInstance++;
            }
            arrays.chunkLinks[startChunk] = new int2(-1, arrays.chunkLinks[startChunk].y);
            arrays.chunkLinks[^1] = new int2(arrays.chunkLinks[^1].x, -1);
            SynchronizeChunks(startChunk, arrays.chunks.Length - startChunk);
        }


        /// <summary>
        /// Set the number of instances of a mesh
        /// </summary>
        /// <param name="meshIndex">Index of the mesh</param>
        /// <param name="instanceCount">Number of instances of the mesh</param>
        /// <param name="chunkCount">Number of chunks of the mesh</param>
        public void SetInstances(int meshIndex, int instanceCount, int chunkCount) {
            int2 indices = arrays.allocatorIndices[meshIndex];
            
            // Reallocate transforms
            int chunkSize = arrays.transformsAllocator[indices.x].size;
            if (chunkSize < instanceCount) {
                int newIndex = arrays.transformsAllocator.Reallocate(indices.x, chunkSize * 2, arrays.transforms);
                if (newIndex != indices.x) SynchronizeTransforms(arrays.transformsAllocator[indices.x].start, chunkSize);
                indices.x = newIndex;
            }
            else if (chunkSize > instanceCount * 4) {
                arrays.transformsAllocator.Reallocate(indices.x, chunkSize / 2, arrays.transforms);
            }

            // Reallocate rendered transforms
            chunkSize = arrays.transformsAllocator[indices.y].size;
            if (chunkSize < instanceCount * chunkCount) {
                indices.y = arrays.renderedTransformsAllocator.Reallocate(indices.y, chunkSize * 2);
                SynchronizeRenderedTransforms();
            }
            else if (chunkSize > instanceCount * chunkCount * 4) {
                arrays.renderedTransformsAllocator.Reallocate(indices.y, chunkSize / 2);
            }

            arrays.allocatorIndices[meshIndex] = indices;

            // Update chunks
            int startInstance = arrays.transformsAllocator[indices.x].start;
            int startRenderedInstance = arrays.renderedTransformsAllocator[indices.y].start;
            for (int i = arrays.firstChunks[meshIndex]; i != -1; i = arrays.chunkLinks[i].y) {
                VoxelChunk chunk = arrays.chunks[i];
                arrays.chunks[i] = new VoxelChunk(
                    chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                    chunk.Normal, chunk.StartFace, chunk.FaceCount, startInstance, startRenderedInstance, instanceCount
                );
                startRenderedInstance += instanceCount;
                SynchronizeChunks(i, 1);
            }
        }


        /// <summary>
        /// Update an instance in the transforms buffer
        /// </summary>
        /// <param name="meshIndex">Index of the mesh if instanced, 0 otherwise</param>
        /// <param name="instanceIndex">Index of the instance</param>
        /// <param name="transform">Transform matrix of the instance</param>
        public void UpdateTransform(int meshIndex, int instanceIndex, Matrix4x4 transform) {
            int index = instanceIndex;
            if (arrays.allocatorIndices.IsCreated) index += arrays.transformsAllocator[arrays.allocatorIndices[meshIndex].x].start;
            if (arrays.transforms[index] != transform) {
                arrays.transforms[index] = transform;
                SynchronizeTransforms(index, 1);
            }
        }


        /// <summary>
        /// Synchronize a range of the chunks buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        private void SynchronizeChunks(int start, int count) {
            if (BufferUtility.MustResize(chunksBuffer.count, arrays.chunks.Length)) {
                BufferUtility.Resize(ref chunksBuffer, arrays.chunks.AsArray());
                Debug.Log($"[Voxels] Chunks length = {arrays.chunks.Length}, buffer resized to {chunksBuffer.count}");
            }
            else chunksBuffer.SetData(arrays.chunks.AsArray(), start, start, count);
        }


        /// <summary>
        /// Synchronize a range of the transforms buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        private void SynchronizeTransforms(int start, int count) {
            int length = arrays.transforms.Length;
            if (arrays.allocatorIndices.IsCreated && (BufferUtility.MustResize(transformsBuffer.count, length) || BufferUtility.MustResize(transformsBuffer.count, arrays.transformsAllocator.compactSize))) {
                CompactTransforms(ref arrays);
                if (BufferUtility.MustResize(transformsBuffer.count, arrays.transforms.Length)) {
                    BufferUtility.Resize(ref transformsBuffer, arrays.transforms.AsArray());   
                }
                else transformsBuffer.SetData(arrays.transforms.AsArray(), 0, 0, arrays.transforms.Length);
                Debug.Log($"[Voxels] Transforms length = {length}, compacted to {arrays.transforms.Length}, buffer resized to {transformsBuffer.count}");
                SynchronizeChunks(0, arrays.chunks.Length);
            }
            else if (BufferUtility.MustResize(transformsBuffer.count, arrays.transforms.Length)) {
                BufferUtility.Resize(ref transformsBuffer, arrays.transforms.AsArray());
                Debug.Log($"[Voxels] Transforms length = {arrays.transforms.Length}, buffer resized to {transformsBuffer.count}");
            }
            else transformsBuffer.SetData(arrays.transforms.AsArray(), start, start, count);
        }


        /// <summary>
        /// Update the rendered transforms buffer size if necessary
        /// </summary>
        private void SynchronizeRenderedTransforms() {
            int length = arrays.renderedTransformsAllocator.TotalSize;
            if (BufferUtility.MustResize(renderedTransformsSize, length)) {
                CompactRenderedTransforms(ref arrays);
                renderedTransformsSize = BufferUtility.UpdateSize(renderedTransformsSize, arrays.renderedTransformsAllocator.TotalSize);
                Debug.Log($"[Voxels] Rendered transforms length = {length}, compacted to {arrays.renderedTransformsAllocator.TotalSize}, buffer resized to {renderedTransformsSize}");
                SynchronizeChunks(0, arrays.chunks.Length);
            }
        }


        [BurstCompile]
        private static void CompactTransforms(ref Arrays arrays) {
            arrays.transformsAllocator.Compact(arrays.transforms);
            UpdateChunks(ref arrays);
        }

        [BurstCompile]
        private static void CompactRenderedTransforms(ref Arrays arrays) {
            arrays.renderedTransformsAllocator.Compact();
            UpdateChunks(ref arrays);
        }

        [BurstCompile]
        private static void UpdateChunks(ref Arrays arrays) {
            for (int i = 0; i < arrays.allocatorIndices.Length; i++) {
                int startInstance = arrays.transformsAllocator[arrays.allocatorIndices[i].x].start;
                int startRenderedInstance = arrays.renderedTransformsAllocator[arrays.allocatorIndices[i].y].start;
                for (int j = arrays.firstChunks[i]; j != -1; j = arrays.chunkLinks[j].y) {
                    VoxelChunk chunk = arrays.chunks[j];
                    arrays.chunks[j] = new VoxelChunk(
                        chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                        chunk.Normal, chunk.StartFace, chunk.FaceCount, startInstance, startRenderedInstance, chunk.InstanceCount
                    );
                    startRenderedInstance += chunk.InstanceCount;
                }
            }
        }
    }

}