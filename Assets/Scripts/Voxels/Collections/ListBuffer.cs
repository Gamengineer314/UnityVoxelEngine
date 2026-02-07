using System;
using UnityEngine;
using Unity.Collections;

namespace Voxels.Collections {

    /// <summary>
    /// GraphicsBuffer that allows adding items.
    /// Capacity is doubled when needed.
    /// <typeparam name="T">Type of the elements in the buffer</typeparam>
    /// </summary>
    public unsafe class ListBuffer<T> where T : unmanaged {
        public GraphicsBuffer buffer { get; private set; }
        private int length;
        private int capacity;
        private readonly GraphicsBuffer.Target target;


        /// <summary>
        /// Create a new ListBuffer
        /// </summary>
        public ListBuffer(GraphicsBuffer.Target target, int initialCapacity = 16) {
            buffer = new(target, initialCapacity, sizeof(T));
            length = 0;
            capacity = initialCapacity;
            this.target = target;
        }

        public void Dispose() => buffer.Dispose();


        public int Length {
            get => length;
            set {
                length = value;
                if (length > capacity) {
                    while (length > capacity) capacity <<= 1;
                    Resize();
                }
            }
        }

        private void Resize() {
            GraphicsBuffer newBuffer = new(target, capacity, sizeof(T));
            T[] data = new T[buffer.count];
            buffer.GetData(data);
            newBuffer.SetData(data);
            buffer.Dispose();
            buffer = newBuffer;
        }


        public T[] GetData(int start, int count) {
            T[] data = new T[count];
            buffer.GetData(data, 0, start, count);
            return data;
        }

        public void SetData(T[] data, int start, int count) => buffer.SetData(data, 0, start, count);
        public void SetData(NativeArray<T> data, int start, int count) => buffer.SetData(data, 0, start, count);

        public T[] this[Range range] {
            get {
                int start = range.Start.IsFromEnd ? length - range.Start.Value : range.Start.Value;
                int end = range.End.IsFromEnd ? length - range.End.Value : range.End.Value;
                return GetData(start, end - start);
            }
            set {
                int start = range.Start.IsFromEnd ? length - range.Start.Value : range.Start.Value;
                int end = range.End.IsFromEnd ? length - range.End.Value : range.End.Value;
                SetData(value, start, end - start);
            }
        }

        public T this[int index] {
            get => this[index..(index+1)][0];
            set => this[index..(index+1)] = new T[] { value };
        }


        public void AddRange(T[] elements) {
            Length += elements.Length;
            buffer.SetData(elements, 0, length - elements.Length, elements.Length);
        }

        public void AddRange(NativeArray<T> elements) {
            Length += elements.Length;
            buffer.SetData(elements, 0, length - elements.Length, elements.Length);
        }

        public void Add(T element) => AddRange(new T[] { element });
    }

}