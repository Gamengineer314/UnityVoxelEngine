using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Voxels.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Global mesh data buffers
    /// </summary>
    [BurstCompile]
    internal class MeshBuffers {
        public GraphicsBuffer facesBuffer;
        public GraphicsBuffer colorsBuffer;

        private NativeList<VoxelFace> faces;
        private NativeList<Color32> colors;
        private BufferAllocator facesAllocator;
        private BufferAllocator colorsAllocator;

        public event Action bufferCompacted;

        private readonly Dictionary<GenerationCommand, NativeList<VoxelChunk>> chunks = new();
        private readonly Dictionary<GenerationCommand, int> referenceCounters = new();


        public unsafe MeshBuffers() {
            facesBuffer = new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(VoxelFace));
            colorsBuffer = new(GraphicsBuffer.Target.Structured, BufferUtility.minLength, sizeof(Color32));
            faces = new(Allocator.Persistent);
            colors = new(Allocator.Persistent);
            facesAllocator = new(Allocator.Persistent);
            colorsAllocator = new(Allocator.Persistent);
        }

        public void Dispose() {
            facesBuffer.Dispose();
            colorsBuffer.Dispose();
            faces.Dispose();
            colors.Dispose();
            facesAllocator.Dispose();
            colorsAllocator.Dispose();
            foreach (NativeList<VoxelChunk> list in chunks.Values) {
                list.Dispose();
            }
        }


        /// <summary>
        /// Get the list of chunks associated with a command.
        /// Create the list if it was never requested before.
        /// </summary>
        /// <param name="command">The command</param>
        /// <returns>The list of chunks</returns>
        public NativeList<VoxelChunk> GetChunks(GenerationCommand command) {
            if (!chunks.TryGetValue(command, out NativeList<VoxelChunk> commandChunks)) {
                commandChunks = new(Allocator.Persistent);
                chunks[command] = commandChunks;
            }
            return commandChunks;
        }

        /// <summary>
        /// Get a full chunk from a partial chunk returned from [GetChunks]
        /// </summary>
        /// <param name="chunk">The partial chunk</param>
        /// <param name="startInstance">Start instance field</param>
        /// <param name="startRenderedInstance">Start rendered instance field</param>
        /// <param name="instanceCount">Instance count field</param>
        /// <returns>The full chunk</returns>
        public VoxelChunk GetChunk(VoxelChunk chunk, int startInstance, int startRenderedInstance, int instanceCount) => new(
            chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color + colorsAllocator[chunk.StartRenderedInstance].start,
            chunk.Normal, chunk.StartFace + facesAllocator[chunk.StartInstance].start, chunk.FaceCount, startInstance, startRenderedInstance, instanceCount
        );

        /// <summary>
        /// Check whether the buffers contain a mesh
        /// </summary>
        /// <param name="command">Generation command for the mesh</param>
        /// <returns>Whether the buffers contain the mesh</returns>
        public bool ContainsCommand(GenerationCommand command) => chunks.ContainsKey(command);


        /// <summary>
        /// Increment the reference counter of a mesh
        /// </summary>
        /// <param name="command">Generation command for the mesh</param>
        public void AddReference(GenerationCommand command) {
            referenceCounters[command] = referenceCounters.GetValueOrDefault(command, 0) + 1;
        }

        /// <summary>
        /// Decrement the reference counter of a mesh
        /// </summary>
        /// <param name="command">Generation command for the mesh</param>
        public void RemoveReference(GenerationCommand command) {
            int counter = referenceCounters[command] - 1;
            if (counter == 0) {
                RemoveMesh(command);
                referenceCounters.Remove(command);
            }
            else referenceCounters[command] = counter;
        }


        /// <summary>
        /// Add part of a mesh
        /// </summary>
        /// <param name="command">Generation command for the mesh</param>
        /// <param name="newChunks">New chunks</param>
        /// <param name="newFaces">New faces</param>
        /// <param name="newColors">New colors</param>
        public void AddData(GenerationCommand command, NativeList<VoxelChunk> newChunks, NativeList<VoxelFace> newFaces, NativeList<Color32> newColors) {
            // Add faces and colors
            int facesIndex = facesAllocator.Allocate(newFaces.Length);
            int colorsIndex = colorsAllocator.Allocate(newColors.Length);
            int startFace = facesAllocator[facesIndex].start;
            int startColor = colorsAllocator[colorsIndex].start;
            faces.Length = facesAllocator.TotalSize;
            colors.Length = colorsAllocator.TotalSize;
            NativeArray<VoxelFace>.Copy(newFaces.AsArray(), 0, faces.AsArray(), startFace, newFaces.Length);
            NativeArray<Color32>.Copy(newColors.AsArray(), 0, colors.AsArray(), startColor, newColors.Length);
            if (BufferUtility.MustGrow(facesBuffer.count, faces.Length)) ResizeFaces();
            else facesBuffer.SetData(faces.AsArray(), startFace, startFace, newFaces.Length);
            if (BufferUtility.MustGrow(colorsBuffer.count, colors.Length)) ResizeColors();
            else colorsBuffer.SetData(colors.AsArray(), startColor, startColor, newColors.Length);

            // Add chunks
            NativeList<VoxelChunk> commandChunks = GetChunks(command);
            commandChunks.Capacity = commandChunks.Length + newChunks.Length;
            foreach (VoxelChunk chunk in newChunks) {
                commandChunks.Add(new VoxelChunk(
                    chunk.center, chunk.size, chunk.offset.position, chunk.offset.Color,
                    chunk.Normal, chunk.StartFace, chunk.FaceCount, facesIndex, colorsIndex, 0
                ));
            }
        }


        private void RemoveMesh(GenerationCommand command) {
            NativeList<VoxelChunk> commandChunks = GetChunks(command);
            int facesIndex = 0, colorsIndex = 0;
            foreach (VoxelChunk chunk in commandChunks) {
                if (chunk.StartInstance != facesIndex) {
                    facesIndex = chunk.StartInstance;
                    facesAllocator.Free(facesIndex);
                    faces.Length = facesAllocator.TotalSize;
                }
                if (chunk.StartRenderedInstance != colorsIndex) {
                    colorsIndex = chunk.StartRenderedInstance;
                    colorsAllocator.Free(colorsIndex);
                    colors.Length = colorsAllocator.TotalSize;
                }
            }
            if (BufferUtility.MustShrink(facesBuffer.count, facesAllocator.compactSize)) ResizeFaces();
            if (BufferUtility.MustShrink(colorsBuffer.count, facesAllocator.compactSize)) ResizeColors();
            commandChunks.Dispose();
            chunks.Remove(command);
        }


        private void ResizeFaces() {
            int length = faces.Length;
            Compact(ref facesAllocator, ref faces);
            if (BufferUtility.MustGrow(facesBuffer.count, faces.Length) || BufferUtility.MustShrink(facesBuffer.count, faces.Length)) {
                BufferUtility.Resize(ref facesBuffer, faces.AsArray());   
            }
            else facesBuffer.SetData(faces.AsArray(), 0, 0, faces.Length);
            bufferCompacted?.Invoke();
            Debug.Log($"[Voxels] Faces length = {length}, compacted to {faces.Length}, buffer resized to {facesBuffer.count}");
        }

        private void ResizeColors() {
            int length = colors.Length;
            Compact(ref colorsAllocator, ref colors);
            if (BufferUtility.MustGrow(colorsBuffer.count, colors.Length) || BufferUtility.MustShrink(colorsBuffer.count, colors.Length)) {
                BufferUtility.Resize(ref colorsBuffer, colors.AsArray());   
            }
            else colorsBuffer.SetData(colors.AsArray(), 0, 0, colors.Length);
            bufferCompacted?.Invoke();
            Debug.Log($"[Voxels] Colors length = {length}, compacted to {colors.Length}, buffer resized to {colorsBuffer.count}");
        }

        [BurstCompile]
        private static void Compact<T>(ref BufferAllocator allocator, ref NativeList<T> list) where T : unmanaged
            => allocator.Compact(list);
    }

}