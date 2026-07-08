using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Unity.Collections.LowLevel.Unsafe {

    /// <summary>
    /// Binary search tree of ordered items
    /// </summary>
    /// <typeparam name="TKey">Type of keys used to query the items</typeparam>
    /// <typeparam name="TItem">Type of the items</typeparam>
    public unsafe struct UnsafeTree<TKey, TItem> : IDisposable
        where TKey : unmanaged, IComparable<TItem>
        where TItem : unmanaged {
        public const int stackSize = 64;

        [NativeDisableUnsafePtrRestriction] public Node* buffer;
        public readonly AllocatorManager.AllocatorHandle allocator;
        public int length;
        public int capacity;
        public Edge reusable; // Index of the first reusable item


        public UnsafeTree(AllocatorManager.AllocatorHandle allocator, int initialCapacity = 1) {
            this.allocator = allocator;
            length = 0;
            capacity = math.max(1, initialCapacity);
            reusable = Edge.Null;
            buffer = AllocatorManager.Allocate<Node>(allocator, capacity);
            buffer[0] = new Node(default, Edge.Null, Edge.Null);
        }

        public readonly void Dispose() => AllocatorManager.Free(allocator, buffer);

        /// <summary>
        /// Double capacity if needed to add an item
        /// </summary>
        public void Grow() {
            if (length + 2 >= capacity) {
                capacity *= 2;
                Node* newBuffer = AllocatorManager.Allocate<Node>(allocator, capacity);
                UnsafeUtility.MemCpy(newBuffer, buffer, (length + 1) * sizeof(Node));
                AllocatorManager.Free(allocator, buffer);
                buffer = newBuffer;
            }
        }


        /// <summary>
        /// Get an item
        /// </summary>
        /// <param name="key">Key of the item</param>
        /// <returns>Pointer to the item (usable until next added item), null if not found</returns>
        public readonly TItem* Ref(TKey key) {
            Edge edge = buffer[0].children.right;
            while (!edge.IsNull) {
                int index = edge.Index;
                int cmp = key.CompareTo(buffer[index].item);
                if (cmp == 0) return &buffer[index].item;
                edge = buffer[index].children[cmp > 0];
            }
            return null;
        }


        /// <summary>
        /// Get an item or create it if not found
        /// </summary>
        /// <param name="key">Key of the item</param>
        /// <returns>Pointer to the value of the item (usable until next added item)</returns>
        public ref TItem RefOrEmpty(TKey key) {
            Grow();
            Frame* stack = stackalloc Frame[stackSize];
            stack[0] = new(0, true);
            int top = 1;

            // Find item
            ref Node node = ref buffer[0];
            bool dir = true;
            Edge edge = node.children.right;
            while (!edge.IsNull) {
                int index = edge.Index;
                node = ref buffer[index];
                int cmp = key.CompareTo(node.item);
                if (cmp == 0) return ref node.item;
                dir = cmp > 0;
                stack[top++] = new(index, dir);
                edge = node.children[dir];
            }
            
            // Add item
            length++;
            int childIndex;
            if (!reusable.IsNull) {
                childIndex = reusable.Index;
                reusable = buffer[childIndex].children.left;
            }
            else childIndex = length;
            buffer[childIndex].children = new(Edge.Null, Edge.Null);
            node.children[dir] = new(childIndex, true);
            MaintainAdd(stack, top);
            return ref buffer[childIndex].item;
        }


        /// <summary>
        /// Maintain tree invariant after adding an item
        /// </summary>
        /// <param name="stack">Stack containing the path to the item</param>
        /// <param name="top">Top of the stack</param>
        private void MaintainAdd(Frame* stack, int top) {
            while (top > 2) {
                int parentIndex = stack[top-2].Index;
                bool dir = stack[top-2].Direction;
                bool invDir = !dir;
                ref Node parent = ref buffer[parentIndex];
                if (!parent.children[dir].IsRed) break;
                int nodeIndex = stack[top-1].Index;
                ref Node node = ref buffer[nodeIndex];
                ref Edge parentEdge = ref Children.GetChild(ref buffer[stack[top-3].Index].children, stack[top-3].Direction);
                top -= 2;

                if (parent.children[invDir].IsRed) { // Swap colors and continue
                    parentEdge = parentEdge.ToRed();
                    parent.children.left = parent.children.left.ToBlack();
                    parent.children.right = parent.children.right.ToBlack();
                    continue;
                }
                
                // Rotate
                if (dir == stack[top+1].Direction) {
                    parentEdge = new(nodeIndex, false);
                    parent.children[dir] = node.children[invDir];
                    node.children[invDir] = new(parentIndex, true);
                }
                else {
                    int childIndex = node.children[invDir].Index;
                    ref Node child = ref buffer[childIndex];
                    parentEdge = new(childIndex, false);
                    parent.children[dir] = child.children[invDir];
                    child.children[invDir] = new(parentIndex, true);
                    node.children[invDir] = child.children[dir];
                    child.children[dir] = new(nodeIndex, true);
                }
                break;
            }
        }


        /// <summary>
        /// Remove an item
        /// </summary>
        /// <param name="key">Key of the item</param>
        /// <returns>Pointer to the item (usable until next added item), null if not found</returns>
        public TItem* Remove(TKey key) {
            Frame* stack = stackalloc Frame[stackSize];
            int top = 0;

            // Find item
            int index = 0;
            ref Node node = ref buffer[0];
            int cmp = 0;
            bool dir = true;
            do {
                stack[top++] = new(index, dir);
                Edge next = node.children[dir];
                if (next.IsNull) return null;
                index = next.Index;
                node = ref buffer[index];
                cmp = key.CompareTo(node.item);
                dir = cmp > 0;
            } while (cmp != 0);
            
            // Replace node by successor if more than one child
            int removedIndex = index;
            ref Edge removedParent = ref Children.GetChild(ref buffer[stack[top-1].Index].children, stack[top-1].Direction);
            bool mustFix;
            if (node.children.left.IsNull) {
                mustFix = !removedParent.IsRed && !node.children.right.IsRed;
                removedParent = node.children.right.ToBlack();
            }
            else if (node.children.right.IsNull) {
                mustFix = !removedParent.IsRed && !node.children.left.IsRed;
                removedParent = node.children.left.ToBlack();
            }
            else {
                ref Children removedChildren = ref node.children;
                ref Frame removedFrame = ref stack[top];
                Edge next = node.children.right;
                do {
                    stack[top++] = new(index, false);
                    index = next.Index;
                    node = ref buffer[index];
                    next = node.children.left;
                } while (!next.IsNull);
                removedFrame = new(removedFrame.Index, true);
                ref Edge edge = ref Children.GetChild(ref buffer[stack[top-1].Index].children, stack[top-1].Direction);
                removedFrame = new(index, true);
                mustFix = !edge.IsRed && !node.children.right.IsRed;
                edge = node.children.right.ToBlack();
                node.children = removedChildren;
                removedParent = new(index, removedParent.IsRed);
            }

            // Remove and maintain
            buffer[removedIndex].children.left = reusable;
            reusable = new(removedIndex, false);
            length--;
            if (mustFix) MaintainRemove(stack, top);
            return &buffer[removedIndex].item;
        }


        /// <summary>
        /// Maintain tree invariant after removing an item
        /// </summary>
        /// <param name="stack">Stack containing the path to the item</param>
        /// <param name="top">Top of the stack</param>
        private void MaintainRemove(Frame* stack, int top) {
            while (--top > 0) {
                int parentIndex = stack[top].Index;
                bool dir = stack[top].Direction;
                bool invDir = !dir;
                ref Node parent = ref buffer[parentIndex];
                int siblingIndex = parent.children[invDir].Index;
                ref Node sibling = ref buffer[siblingIndex];
                ref Edge parentEdge = ref Children.GetChild(ref buffer[stack[top-1].Index].children, stack[top-1].Direction);

                if (parent.children[invDir].IsRed) { // Rotate and continue
                    parentEdge = new(siblingIndex, false);
                    parent.children[invDir] = sibling.children[dir];
                    sibling.children[dir] = new(parentIndex, true);
                    parentEdge = ref Children.GetChild(ref sibling.children, dir);
                    siblingIndex = parent.children[invDir].Index;
                    sibling = ref buffer[siblingIndex];
                }
                
                if (sibling.children[invDir].IsRed) { // Rotate
                    parentEdge = new(siblingIndex, parentEdge.IsRed);
                    parent.children[invDir] = sibling.children[dir];
                    sibling.children[dir] = new(parentIndex, false);
                    sibling.children[invDir] = sibling.children[invDir].ToBlack();
                    break;
                }
                
                if (sibling.children[dir].IsRed) { // Rotate
                    int siblingChildIndex = sibling.children[dir].Index;
                    ref Node siblingChild = ref buffer[siblingChildIndex];
                    parentEdge = new(siblingChildIndex, parentEdge.IsRed);
                    parent.children[invDir] = siblingChild.children[dir];
                    siblingChild.children[dir] = new(parentIndex, false);
                    sibling.children[dir] = siblingChild.children[invDir];
                    siblingChild.children[invDir] = new(siblingIndex, false);
                    break;
                }
                
                // Swap colors and continue if parent is black
                parent.children[invDir] = parent.children[invDir].ToRed();
                if (parentEdge.IsRed) {
                    parentEdge = parentEdge.ToBlack();
                    break;
                }
            }
        }


        /// <summary>
        /// Get the closest item with a key smaller or equal to a given key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>Pointer to the item (usable until next added item), null if none</returns>
        public readonly TItem* Floor(TKey key) {
            TItem* floor = null;
            Edge edge = buffer[0].children.right;
            while (!edge.IsNull) {
                int index = edge.Index;
                int cmp = key.CompareTo(buffer[index].item);
                if (cmp < 0) edge = buffer[index].children.left;
                else {
                    floor = &buffer[index].item;
                    edge = buffer[index].children.right;
                }
            }
            return floor;
        }


        /// <summary>
        /// Get the closest item with a key greater or equal to a given key
        /// </summary>
        /// <param name="key">The key</param>
        /// <returns>Pointer to the item (usable until next added item), null if none</returns>
        public readonly TItem* Ceil(TKey key) {
            TItem* ceil = null;
            Edge edge = buffer[0].children.right;
            while (!edge.IsNull) {
                int index = edge.Index;
                int cmp = key.CompareTo(buffer[index].item);
                if (cmp > 0) edge = buffer[index].children.right;
                else {
                    ceil = &buffer[index].item;
                    edge = buffer[index].children.left;
                }
            }
            return ceil;
        }


        public struct Node {
            public TItem item;
            public Children children; // Children of the node. left can also be the index of the next reusable node if the node is reusable

            public Node(TItem item, Edge left, Edge right) {
                this.item = item;
                children = new(left, right);
            }
        }

        public struct Children {
            public Edge left, right;

            public Children(Edge left, Edge right) {
                this.left = left;
                this.right = right;
            }

            public Edge this[bool direction] {
                readonly get => direction ? right : left;
                set {
                    if (direction) right = value;
                    else left = value;
                }
            }

            public static ref Edge GetChild(ref Children children, bool direction)
                => ref direction ? ref children.right : ref children.left;
        }

        public readonly struct Edge {
            private readonly int edge;

            public readonly int Index => edge & ~(1 << 31);
            public readonly bool IsRed => ((uint)edge >> 31) != 0;
            public readonly bool IsNull => edge == 0;

            public static Edge Null => new();

            public Edge(int index, bool isRed) => edge = index | (isRed ? 1 : 0) << 31;

            public Edge ToRed() => new(edge, true);
            public Edge ToBlack() => new(Index, false);
        }

        private readonly struct Frame {
            private readonly int frame;

            public readonly int Index => frame & ~(1 << 31);
            public readonly bool Direction => (uint)frame >> 31 != 0;

            public Frame(int index, bool direction) => frame = index | (direction ? 1 : 0) << 31;
        }


        /// <summary>
        /// Enumerator of all items in a tree
        /// </summary>
        public struct Enumerator : IEnumerator<TItem> {
            [NativeDisableUnsafePtrRestriction] public readonly Node* buffer;
            public fixed int stack[stackSize];
            public int top;

            public Enumerator(Node* buffer) {
                this.buffer = buffer;
                top = 0;
                stack[0] = 0;
            }

            public readonly TItem Current => buffer[stack[top]].item;
            readonly object IEnumerator.Current => Current;

            public readonly void Dispose() {}

            public bool MoveNext() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (top < 0) throw new InvalidOperationException($"MoveNext call after it already returned false");
#endif
                int index = stack[top--];
                Edge edge = buffer[index].children.right;
                if (!edge.IsNull) {
                    do {
                        index = edge.Index;
                        stack[++top] = index;
                        edge = buffer[index].children.left;
                    } while (!edge.IsNull);
                    return true;
                }
                else if (top >= 0) return true;
                return false;
            }

            public void Reset() => top = 0;
        }


        /// <summary>
        /// Enumerator of all items before a key in a tree
        /// </summary>
        public struct EnumeratorBefore : IEnumerator<TItem> {
            public Enumerator enumerator;
            public readonly TKey end;

            public EnumeratorBefore(Node* buffer, TKey end) {
                enumerator = new Enumerator(buffer);
                this.end = end;
            }

            public readonly TItem Current => enumerator.Current;
            readonly object IEnumerator.Current => Current;

            public bool MoveNext() {
                bool hasNext = enumerator.MoveNext();
                return hasNext && end.CompareTo(enumerator.Current) > 0;
            }

            public void Reset() => enumerator.Reset();

            public readonly void Dispose() {}
        }


        /// <summary>
        /// Enumerator of all items after a key in a tree
        /// </summary>
        public struct EnumeratorAfter : IEnumerator<TItem> {
            public Enumerator enumerator;
            public readonly TKey start;

            public EnumeratorAfter(Node* buffer, TKey start) {
                enumerator = new Enumerator(buffer);
                this.start = start;
                Reset();
            }

            public readonly TItem Current => enumerator.Current;
            readonly object IEnumerator.Current => Current;

            public bool MoveNext() => enumerator.MoveNext();

            public void Reset() {
                enumerator.Reset();
                int top = -1;
                int prevIndex = 0;
                Edge edge = enumerator.buffer[0].children.right;
                while (!edge.IsNull) {
                    int index = edge.Index;
                    int cmp = start.CompareTo(enumerator.buffer[index].item);
                    if (cmp <= 0) enumerator.stack[++top] = index;
                    if (cmp > 0) {
                        enumerator.top = top + 1;
                        prevIndex = index;
                    }
                    edge = enumerator.buffer[index].children[cmp > 0];
                }
                enumerator.stack[enumerator.top] = prevIndex;
            }

            public readonly void Dispose() {}
        }


        /// <summary>
        /// Enumerator of all items in a range of keys in a tree
        /// </summary>
        public struct EnumeratorBetween : IEnumerator<TItem> {
            public EnumeratorAfter enumerator;
            public readonly TKey end;

            public EnumeratorBetween(Node* buffer, TKey start, TKey end) {
                enumerator = new EnumeratorAfter(buffer, start);
                this.end = end;
            }

            public readonly TItem Current => enumerator.Current;
            readonly object IEnumerator.Current => Current;

            public bool MoveNext() {
                bool hasNext = enumerator.MoveNext();
                return hasNext && end.CompareTo(enumerator.Current) > 0;
            }

            public void Reset() => enumerator.Reset();

            public readonly void Dispose() {}
        }
    }

}