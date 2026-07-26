using UnityEngine;
using Unity.Collections;

namespace Voxels.Rendering {

    /// <summary>
    /// Graphics buffers utility functions
    /// </summary>
    public static class BufferUtility {
        public const int minLength = 4096;

        /// <summary>
        /// Increase or decrease the capacity of a buffer and synchronize its content with an array
        /// </summary>
        /// <typeparam name="T">Type of the items in the array and the buffer</typeparam>
        /// <param name="buffer">The buffer</param>
        /// <param name="array">The array</param>
        public static unsafe void Resize<T>(ref GraphicsBuffer buffer, NativeArray<T> array) where T : unmanaged {
            int newSize = UpdateSize(buffer.count, array.Length);
            GraphicsBuffer.Target target = buffer.target;
            buffer.Dispose();
            buffer = new GraphicsBuffer(target, newSize, sizeof(T));
            buffer.SetData(array, 0, 0, array.Length);
        }

        /// <summary>
        /// Get the new size of a buffer to synchronize it with an array
        /// </summary>
        /// <param name="bufferSize">Current size of the buffer</param>
        /// <param name="arraySize">Size of the array</param>
        /// <returns>New size of the buffer</returns>
        public static int UpdateSize(int bufferSize, int arraySize) {
            int newSize = bufferSize;
            while (newSize < arraySize) newSize *= 2;
            while (arraySize * 4 < newSize && newSize / 2 > minLength) newSize /= 2;
            return newSize;
        }
        
        /// <summary>
        /// Check if a buffer must be resized to synchronize it with an array
        /// </summary>
        /// <param name="bufferSize">Size of the buffer</param>
        /// <param name="arraySize">Size of the array</param>
        /// <returns>Whether the buffer must be resized</returns>
        public static bool MustResize(int bufferSize, int arraySize)
            => arraySize > bufferSize || arraySize * 4 < bufferSize && bufferSize / 2 > minLength;
    }

}