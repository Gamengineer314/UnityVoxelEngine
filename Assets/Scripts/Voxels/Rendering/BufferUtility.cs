using UnityEngine;
using Unity.Collections;

namespace Voxels.Rendering {

    /// <summary>
    /// Graphics buffers utility functions
    /// </summary>
    public static class BufferUtility {
        /// <summary>
        /// Double the capacity of a buffer if needed to synchronize its content with an array
        /// </summary>
        /// <typeparam name="T">Type of the items in the array and the buffer</typeparam>
        /// <param name="buffer">The buffer</param>
        /// <param name="array">The array</param>
        public static unsafe void Grow<T>(ref GraphicsBuffer buffer, NativeArray<T> array) where T : unmanaged {
            int newSize = buffer.count;
            while (newSize < array.Length) newSize *= 2;
            GraphicsBuffer.Target target = buffer.target;
            buffer.Dispose();
            buffer = new GraphicsBuffer(target, newSize, sizeof(T));
            buffer.SetData(array, 0, 0, array.Length);
        }
    }

}