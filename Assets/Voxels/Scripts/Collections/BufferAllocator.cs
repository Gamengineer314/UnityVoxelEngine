using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Voxels.Collections {

    /// <summary>
    /// Allocator for a GPU buffer
    /// </summary>
    public struct BufferAllocator : IDisposable {
        private NativeTreeSet<ChunkReference> freeChunks;
        private NativeLinkedList<MemoryChunk> chunks;

        /// <summary>
        /// Total size of the allocated chunks without the free chunks between them
        /// </summary>
        public int compactSize { get; private set; }

        /// <summary>
        /// Total size of the allocated chunks and the free chunks between them
        /// </summary>
        public int TotalSize {
            get {
                if (chunks[chunks.Last].HasPrev) {
                    MemoryChunk last = chunks[chunks[chunks.Last].prev].value;
                    return last.start + last.size;
                }
                else return 0;
            }
        }


        public BufferAllocator(AllocatorManager.AllocatorHandle allocator) {
            freeChunks = new(allocator);
            chunks = new(allocator);
            compactSize = 0;
            int index = chunks.AddLast(new MemoryChunk(0, int.MaxValue, false));
            freeChunks.Add(new ChunkReference(index, int.MaxValue));
        }

        public void Dispose() {
            freeChunks.Dispose();
            chunks.Dispose();
        }

        public override string ToString() {
            string s = "";
            foreach (MemoryChunk chunk in chunks) {
                s += $"[{chunk.start} {chunk.size} {chunk.isAllocated}] ";
            }
            s += "\n";
            foreach (ChunkReference chunk in freeChunks) {
                s += $"[{chunk.index} {chunk.size}] ";
            }
            return s;
        }


        /// <summary>
        /// Allocate a memory chunk
        /// </summary>
        /// <param name="size">Size of the chunk</param>
        /// <returns>Index of the chunk</returns>
        public int Allocate(int size) {
            compactSize += size;
            freeChunks.Ceil(new(0, size), out ChunkReference freeRef);
            freeChunks.Remove(freeRef);
            MemoryChunk free = chunks[freeRef.index].value;
            chunks.SetValue(freeRef.index, new MemoryChunk(free.start, size, true));
            int remaining = free.size - size;
            if (remaining > 0) {
                int newIndex = chunks.AddAfter(freeRef.index, new MemoryChunk(free.start + size, remaining, false));
                freeChunks.Add(new ChunkReference(newIndex, remaining));
            }
            return freeRef.index;
        }


        /// <summary>
        /// Reallocate a memory chunk
        /// </summary>
        /// <param name="index">Index of the chunk</param>
        /// <param name="size">New size of the chunk</param>
        /// <returns>Index of the new chunk</returns>
        public int Reallocate(int index, int size) {
            NativeLinkedList<MemoryChunk>.Node node = chunks[index];
            if (size == node.value.size) return index;

            // Decrease current chunk
            if (size < node.value.size) {
                chunks.SetValue(index, new MemoryChunk(node.value.start, size, true));
                int remaining = node.value.size - size;
                compactSize -= remaining;

                // Try to increase next free chunk
                if (node.HasNext) {
                    MemoryChunk next = chunks[node.next].value;
                    if (!next.isAllocated) {
                        chunks.SetValue(node.next, new MemoryChunk(node.value.start + size, next.size + remaining, false));
                        freeChunks.Remove(new ChunkReference(node.next, next.size));
                        freeChunks.Add(new ChunkReference(node.next, next.size + remaining));
                        return index;
                    }
                }
                
                // New free chunk
                int newIndex = chunks.AddAfter(index, new MemoryChunk(node.value.start + size, remaining, false));
                freeChunks.Add(new ChunkReference(newIndex, remaining));
                return index;
            }

            // Try to increase current chunk
            if (node.HasNext) {
                MemoryChunk next = chunks[node.next].value;
                int increase = size - node.value.size;
                if (!next.isAllocated && next.size >= increase) {
                    compactSize += increase;
                    chunks.SetValue(index, new MemoryChunk(node.value.start, size, true));
                    chunks.Remove(node.next);
                    freeChunks.Remove(new ChunkReference(node.next, next.size));
                    int remaining = next.size - increase;
                    if (remaining > 0) {
                        int newIndex = chunks.AddAfter(index, new MemoryChunk(node.value.start + size, remaining, false));
                        freeChunks.Add(new ChunkReference(newIndex, remaining));
                    }
                    return index;
                }
            }

            // Move chunk
            Free(index);
            return Allocate(size);
        }


        /// <summary>
        /// Free a memory chunk
        /// </summary>
        /// <param name="index">Index of the chunk</param>
        public void Free(int index) {
            NativeLinkedList<MemoryChunk>.Node node = chunks[index];
            int start = node.value.start;
            int size = node.value.size;
            compactSize -= size;

            // Try to combine free chunks
            if (node.HasNext) {
                MemoryChunk next = chunks[node.next].value;
                if (!next.isAllocated) {
                    size += next.size;
                    chunks.Remove(node.next);
                    freeChunks.Remove(new ChunkReference(node.next, next.size));
                }
            }
            if (node.HasPrev) {
                MemoryChunk prev = chunks[node.prev].value;
                if (!prev.isAllocated) {
                    start = prev.start;
                    size += prev.size;
                    chunks.Remove(node.prev);
                    freeChunks.Remove(new ChunkReference(node.prev, prev.size));
                }
            }

            chunks.SetValue(index, new MemoryChunk(start, size, false));
            freeChunks.Add(new ChunkReference(index, size));
        }


        /// <summary>
        /// Get an allocated chunk
        /// </summary>
        /// <param name="index">Index of the chunk</param>
        /// <returns></returns>
        public MemoryChunk this[int index] => chunks[index].value;


        /// <summary>
        /// Remove free chunks by moving allocated chunks
        /// </summary>
        public void Compact() {
            int start = 0;
            int index = chunks.First;
            NativeLinkedList<MemoryChunk>.Node node;
            do {
                node = chunks[index];
                if (node.value.isAllocated) {
                    chunks.SetValue(index, new MemoryChunk(start, node.value.size, true));
                    start += node.value.size;
                }
                else chunks.Remove(index);
                index = node.next;
            } while (node.HasNext);

            freeChunks.Clear();
            index = chunks.AddLast(new MemoryChunk(start, int.MaxValue - start, false));
            freeChunks.Add(new ChunkReference(index, int.MaxValue - start));
        }


        /// <summary>
        /// Reallocate a memory chunk.
        /// Move the data corresponding to the chunk in a list.
        /// </summary>
        /// <typeparam name="T">Type of the items in the list</typeparam>
        /// <param name="index">Index of the chunk</param>
        /// <param name="size">New size of the chunk</param>
        /// <param name="list">The list</param>
        /// <returns>Index of the new chunk</returns>
        public unsafe int Reallocate<T>(int index, int size, NativeList<T> list) where T : unmanaged {
            MemoryChunk oldChunk = chunks[index].value;
            int newIndex = Reallocate(index, size);
            list.Length = TotalSize;
            if (newIndex != index) {
                UnsafeUtility.MemMove(list.GetUnsafePtr() + chunks[newIndex].value.start, list.GetUnsafePtr() + oldChunk.start, oldChunk.size * sizeof(T));
            }
            return newIndex;
        } 


        /// <summary>
        /// Remove free chunks by moving allocated chunks.
        /// Move the data corresponding to moved chunks in a list.
        /// </summary>
        /// <typeparam name="T">Type of the items in the list</typeparam>
        /// <param name="list">The list</param>
        public unsafe void Compact<T>(NativeList<T> list) where T : unmanaged {
            int start = 0;
            foreach (MemoryChunk chunk in chunks) {
                if (chunk.isAllocated) {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (start + chunk.size > list.Length)
                        throw new IndexOutOfRangeException($"Chunk ({chunk.start}, {chunk.size}) is out of range of array of length {list.Length}");
#endif
                    UnsafeUtility.MemMove(list.GetUnsafePtr() + start, list.GetUnsafePtr() + chunk.start, chunk.size * sizeof(T));
                    start += chunk.size;
                }
            }
            list.Length = start;
            Compact();
        }


        private readonly struct ChunkReference : IComparable<ChunkReference> {
            public readonly int index;
            public readonly int size;

            public ChunkReference(int index, int size) {
                this.index = index;
                this.size = size;
            }

            public int CompareTo(ChunkReference other) {
                int cmp = size.CompareTo(other.size);
                if (cmp != 0) return cmp;
                return index.CompareTo(other.index);
            }
        }
    }


    /// <summary>
    /// Memory chunk in a BufferAllocator
    /// </summary>
    public readonly struct MemoryChunk {
        public readonly int start;
        public readonly int size;
        public readonly bool isAllocated;

        public MemoryChunk(int start, int size, bool isAllocated) {
            this.start = start;
            this.size = size;
            this.isAllocated = isAllocated;
        }
    }

}