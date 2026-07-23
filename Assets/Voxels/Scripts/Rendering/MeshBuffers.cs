using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

namespace Voxels.Rendering {
    
    /// <summary>
    /// Global mesh data buffers
    /// </summary>
    internal class MeshBuffers {
        public GraphicsBuffer facesBuffer;
        public GraphicsBuffer colorsBuffer;
        public NativeList<VoxelFace> faces;
        public NativeList<Color32> colors;
        private readonly Dictionary<GenerationCommand, NativeList<VoxelChunk>> chunks = new();


        public unsafe MeshBuffers() {
            facesBuffer = new(GraphicsBuffer.Target.Structured, 4096, sizeof(VoxelFace));
            colorsBuffer = new(GraphicsBuffer.Target.Structured, 4096, sizeof(Color32));
            faces = new(Allocator.Persistent);
            colors = new(Allocator.Persistent);
        }

        public void Dispose() {
            facesBuffer.Dispose();
            colorsBuffer.Dispose();
            faces.Dispose();
            colors.Dispose();
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
        /// Check whether the buffers contain the result of a generation command
        /// </summary>
        /// <param name="command">The command</param>
        /// <returns>Whether the buffers contain the result of the command</returns>
        public bool ContainsCommand(GenerationCommand command) => chunks.ContainsKey(command);


        /// <summary>
        /// Synchronize a range of the faces buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        public void SynchronizeFaces(int start, int count) {
            if (faces.Length > facesBuffer.count) {
                BufferUtility.Grow(ref facesBuffer, faces.AsArray());
            }
            else facesBuffer.SetData(faces.AsArray(), start, start, count);
        }

        /// <summary>
        /// Synchronize a range of the colors buffer with the array
        /// </summary>
        /// <param name="start">Start of the range</param>
        /// <param name="count">Number of items in the range</param>
        public void SynchronizeColors(int start, int count) {
            if (colors.Length > colorsBuffer.count) {
                BufferUtility.Grow(ref colorsBuffer, colors.AsArray());
            }
            else colorsBuffer.SetData(colors.AsArray(), start, start, count);
        }
    }

}