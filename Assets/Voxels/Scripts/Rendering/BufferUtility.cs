using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;

namespace Voxels.Rendering {

    /// <summary>
    /// Graphics buffers utility functions
    /// </summary>
    internal static class BufferUtility {
        public const int minLength = 4096;

        /// <summary>
        /// Resize a buffer and synchronize its content with an array
        /// </summary>
        /// <typeparam name="T">Type of the items in the array and the buffer</typeparam>
        /// <param name="buffer">The buffer</param>
        /// <param name="array">The array</param>
        public static unsafe void Resize<T>(ref GraphicsBuffer buffer, NativeArray<T> array) where T : unmanaged {
            GraphicsBuffer.Target target = buffer.target;
            buffer.Dispose();
            buffer = new GraphicsBuffer(target, UpdateSize(array.Length), sizeof(T));
            buffer.SetData(array, 0, 0, array.Length);
        }

        /// <summary>
        /// Get the size of a buffer synchronized with an array
        /// </summary>
        /// <param name="size">Size of the array</param>
        /// <returns>Size of the buffer</returns>
        public static int UpdateSize(int size) => math.max(minLength, math.ceilpow2(size));
        
        /// <summary>
        /// Check if a buffer's capacity must be increased to synchronize it with an array
        /// </summary>
        /// <param name="bufferSize">Size of the buffer</param>
        /// <param name="arraySize">Size of the array</param>
        /// <returns>Whether the capacity must be increased</returns>
        public static bool MustGrow(int bufferSize, int arraySize) => arraySize > bufferSize;

        /// <summary>
        /// Check if a buffer's capacity must be decreased to synchronize it with an array
        /// </summary>
        /// <param name="bufferSize">Size of the buffer</param>
        /// <param name="arraySize">Size of the array</param>
        /// <returns>Whether the capacity must be decreased</returns>
        public static bool MustShrink(int bufferSize, int arraySize) => arraySize * 4 < bufferSize && bufferSize / 2 >= minLength;
    }

}