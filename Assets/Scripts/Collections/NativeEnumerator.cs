using System.Collections;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Collections {

    /// <summary>
    /// Native wrapper of an unsafe enumerator
    /// </summary>
    /// <typeparam name="TItem">Type of the items</typeparam>
    /// <typeparam name="TEnumerator">Type of the unsafe enumerator</typeparam>
    [NativeContainer]
    [NativeContainerIsReadOnly]
    public struct NativeEnumerator<TItem, TEnumerator> : IEnumerator<TItem>
        where TItem : unmanaged
        where TEnumerator : unmanaged, IEnumerator<TItem> {
        private TEnumerator enumerator;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        private AtomicSafetyHandle m_Safety;
#endif

        /// <summary>
        /// Wrap an unsafe enumerator
        /// </summary>
        /// <param name="enumerator">The enumerator</param>
        /// <param name="safety">Safety handle of the enumerated collection</param>
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        public NativeEnumerator(TEnumerator enumerator, AtomicSafetyHandle safety) {
            AtomicSafetyHandle.CheckGetSecondaryDataPointerAndThrow(safety);
            AtomicSafetyHandle.UseSecondaryVersion(ref safety);
            m_Safety = safety;
#else
        public NativeEnumerator(T enumerator) {
#endif
            this.enumerator = enumerator;
        }

        public TItem Current {
            get {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                return enumerator.Current;
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            return enumerator.MoveNext();
        }

        public void Reset() {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            enumerator.Reset();
        }

        public readonly void Dispose() {}
    }

}