using System.Collections;
using System.Collections.Generic;

namespace Unity.Collections {

    /// <summary>
    /// Simple enumerable implementation that returns a copy of a given enumerator
    /// </summary>
    /// <typeparam name="TItem">Type of the items</typeparam>
    /// <typeparam name="TEnumerator">Type of the enumerator</typeparam>
    public readonly struct Enumerable<TItem, TEnumerator> : IEnumerable<TItem>
        where TEnumerator : IEnumerator<TItem> {
        private readonly TEnumerator enumerator;

        public Enumerable(TEnumerator enumerator) => this.enumerator = enumerator;

        public readonly TEnumerator GetEnumerator() => enumerator;
        readonly IEnumerator<TItem> IEnumerable<TItem>.GetEnumerator() => GetEnumerator();
        readonly IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

}