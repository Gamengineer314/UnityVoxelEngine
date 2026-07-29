using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using Voxels.Collections;
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
        public bool IsCreated => arrays.chunks.IsCreated;


        public unsafe LayerBuffers(ShaderParameters parameters) {
            chunksBuffer = new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(VoxelChunk));
            transformsBuffer = new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(Matrix4x4));
            renderedTransformsSize = BufferUtility.minLength;
            arrays.chunks = new(Allocator.Persistent);
            arrays.chunkLinks = new(Allocator.Persistent);
            arrays.firstChunks = new(Allocator.Persistent);
            arrays.transforms = new(Allocator.Persistent);
            arrays.transformsAllocator = parameters.instanced ? new(Allocator.Persistent) : default;
            arrays.renderedTransformsAllocator = parameters.instanced ? new(Allocator.Persistent) : default;
            arrays.allocatorIndices = parameters.instanced ? new(Allocator.Persistent) : default;
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
        /// <param name="buffers">Buffers that contain the mesh</param>
        /// <param name="command">Generation command for the mesh</param>
        /// </summary>
        public void AddMesh(MeshBuffers buffers, GenerationCommand command) {
            // Allocate transforms
            NativeList<VoxelChunk> chunks = buffers.GetChunks(command);
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
                startInstance = arrays.transforms.Length++;
                startRenderedInstance = 0;
            }

            // Add chunks
            int startChunk = arrays.chunks.Length;
            foreach (VoxelChunk chunk in chunks) {
                arrays.chunks.Add(buffers.GetChunk(chunk, startInstance, startRenderedInstance, 1));
                arrays.chunkLinks.Add(new int2(arrays.chunks.Length - 2, arrays.chunks.Length));
                startRenderedInstance++;
            }
            arrays.chunkLinks[startChunk] = new int2(-arrays.firstChunks.Length, arrays.chunkLinks[startChunk].y);
            arrays.chunkLinks[^1] = new int2(arrays.chunkLinks[^1].x, -1);
            arrays.firstChunks.Add(startChunk);
            
            if (BufferUtility.MustGrow(chunksBuffer.count, arrays.chunks.Length)) ResizeChunks();
            else chunksBuffer.SetData(arrays.chunks.AsArray(), startChunk, startChunk, arrays.chunks.Length - startChunk);
            if (BufferUtility.MustGrow(transformsBuffer.count, arrays.transforms.Length)) ResizeTransforms();
            if (arrays.allocatorIndices.IsCreated) {
                if (BufferUtility.MustGrow(renderedTransformsSize, arrays.renderedTransformsAllocator.TotalSize)) ResizeRenderedTransforms();
            }
            else {
                renderedTransformsSize = transformsBuffer.count;
            }
        }


        /// <summary>
        /// Remove a mesh
        /// </summary>
        /// <param name="index">Index of the mesh</param>
        public void RemoveMesh(int index) {
            if (arrays.allocatorIndices.IsCreated) {
                int2 indices = arrays.allocatorIndices[index];
                arrays.transformsAllocator.Free(indices.x);
                arrays.transforms.Length = arrays.transformsAllocator.TotalSize;
                arrays.renderedTransformsAllocator.Free(indices.y);
                arrays.allocatorIndices.RemoveAtSwapBack(index);
            }
            else arrays.transforms.RemoveAtSwapBack(index);

            // Remove chunks
            int i = arrays.firstChunks[index];
            while (i != -1) {
                int next = arrays.chunkLinks[i].y;
                arrays.chunks.RemoveAtSwapBack(i);
                arrays.chunkLinks.RemoveAtSwapBack(i);
                if (arrays.chunkLinks.Length == next) next = i; // Swapped chunk was next
                else if (i != arrays.chunkLinks.Length) { // Update swapped chunk
                    int prev = arrays.chunkLinks[i].x;
                    if (prev < 0) arrays.firstChunks[-prev] = i;
                    else arrays.chunkLinks[prev] = new int2(arrays.chunkLinks[prev].x, i);
                    chunksBuffer.SetData(arrays.chunks.AsArray(), i, i, 1);
                }
                i = next;
            }
            arrays.firstChunks.RemoveAtSwapBack(index);

            // Update chunks
            if (!arrays.allocatorIndices.IsCreated && index != arrays.transforms.Length) {
                for (i = arrays.firstChunks[index]; i != -1; i = arrays.chunkLinks[i].y) {
                    VoxelChunk chunk = arrays.chunks[i];
                    arrays.chunks[i] = new VoxelChunk(
                        chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                        chunk.Normal, chunk.StartFace, chunk.FaceCount, index, chunk.StartRenderedInstance, 1
                    );
                    chunksBuffer.SetData(arrays.chunks.AsArray(), i, i, 1);
                }
            }

            if (BufferUtility.MustShrink(chunksBuffer.count, arrays.chunks.Length)) ResizeChunks();
            if (BufferUtility.MustShrink(transformsBuffer.count, arrays.transformsAllocator.compactSize)) ResizeTransforms();
            if (arrays.allocatorIndices.IsCreated) {
                if (BufferUtility.MustShrink(renderedTransformsSize, arrays.renderedTransformsAllocator.compactSize)) ResizeRenderedTransforms();
            }
            else {
                renderedTransformsSize = transformsBuffer.count;
            }
        }


        /// <summary>
        /// Update the chunks of a mesh without synchronizing the chunks buffer
        /// </summary>
        /// <param name="buffers">Buffers that contain the mesh</param>
        /// <param name="command">Generation command for the mesh</param>
        /// <param name="index">Index of the mesh</param>
        public void UpdateChunks(MeshBuffers buffers, GenerationCommand command, int index) {
            NativeList<VoxelChunk> chunks = buffers.GetChunks(command);
            for (int i = 0, j = arrays.firstChunks[index]; j != -1; i++, j = arrays.chunkLinks[j].y) {
                VoxelChunk chunk = arrays.chunks[j];
                arrays.chunks[j] = buffers.GetChunk(chunks[i], chunk.StartInstance, chunk.StartRenderedInstance, chunk.InstanceCount);
            }
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
            if (instanceCount > chunkSize) {
                int newIndex = arrays.transformsAllocator.Reallocate(indices.x, chunkSize * 2, arrays.transforms);
                if (BufferUtility.MustGrow(transformsBuffer.count, arrays.transforms.Length)) ResizeTransforms();
                else if (newIndex != indices.x) {
                    int start = arrays.transformsAllocator[indices.x].start;
                    transformsBuffer.SetData(arrays.transforms.AsArray(), start, start, chunkSize);
                }
                indices.x = newIndex;
            }
            else if (chunkSize > instanceCount * 4) {
                arrays.transformsAllocator.Reallocate(indices.x, chunkSize / 2, arrays.transforms);
            }
            if (BufferUtility.MustShrink(transformsBuffer.count, arrays.transformsAllocator.compactSize)) ResizeTransforms();

            // Reallocate rendered transforms
            chunkSize = arrays.transformsAllocator[indices.y].size;
            int newSize = instanceCount * chunkCount;
            if (newSize > chunkSize) {
                indices.y = arrays.renderedTransformsAllocator.Reallocate(indices.y, chunkSize * 2);
                if (BufferUtility.MustGrow(renderedTransformsSize, arrays.renderedTransformsAllocator.TotalSize)) ResizeRenderedTransforms();
            }
            else if (chunkSize > newSize * 4) {
                arrays.renderedTransformsAllocator.Reallocate(indices.y, newSize / 2);
            }
            if (BufferUtility.MustShrink(renderedTransformsSize, arrays.renderedTransformsAllocator.compactSize)) ResizeRenderedTransforms();

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
                chunksBuffer.SetData(arrays.chunks.AsArray(), i, i, 1);
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
                transformsBuffer.SetData(arrays.transforms.AsArray(), index, index, 1);
            }
        }


        /// <summary>
        /// Synchronize the chunks buffer with its array
        /// </summary>
        public void SynchronizeChunks() => chunksBuffer.SetData(arrays.chunks.AsArray(), 0, 0, arrays.chunks.Length);
        
        private void ResizeChunks() {
            BufferUtility.Resize(ref chunksBuffer, arrays.chunks.AsArray());
            Debug.Log($"[Voxels] Chunks length = {arrays.chunks.Length}, buffer resized to {chunksBuffer.count}");
        }

        private void ResizeTransforms() {
            if (arrays.allocatorIndices.IsCreated) {
                int length = arrays.transforms.Length;
                CompactTransforms(ref arrays);
                if (BufferUtility.MustGrow(transformsBuffer.count, arrays.transforms.Length) || BufferUtility.MustShrink(transformsBuffer.count, arrays.transforms.Length)) {
                    BufferUtility.Resize(ref transformsBuffer, arrays.transforms.AsArray());   
                }
                else transformsBuffer.SetData(arrays.transforms.AsArray(), 0, 0, arrays.transforms.Length);
                Debug.Log($"[Voxels] Transforms length = {length}, compacted to {arrays.transforms.Length}, buffer resized to {transformsBuffer.count}");
                SynchronizeChunks();
            }
            else {
                BufferUtility.Resize(ref transformsBuffer, arrays.transforms.AsArray());
                Debug.Log($"[Voxels] Transforms length = {arrays.transforms.Length}, buffer resized to {transformsBuffer.count}");
            }
        }

        private void ResizeRenderedTransforms() {
            int length = arrays.renderedTransformsAllocator.TotalSize;
            CompactRenderedTransforms(ref arrays);
            renderedTransformsSize = BufferUtility.UpdateSize(arrays.renderedTransformsAllocator.TotalSize);
            Debug.Log($"[Voxels] Rendered transforms length = {length}, compacted to {arrays.renderedTransformsAllocator.TotalSize}, buffer resized to {renderedTransformsSize}");
            SynchronizeChunks();
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