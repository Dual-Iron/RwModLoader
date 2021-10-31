using System.Collections;
using System.Collections.Generic;

namespace VirtualEnums;

/// <summary>
/// A bijective dictionary.
/// </summary>
public sealed class Map<T1, T2>
{
    private readonly Dictionary<T1, T2> forward = new();
    private readonly Dictionary<T2, T1> reverse = new();

    /// <summary>
    /// Creates a new <see cref="Map{T1, T2}"/>.
    /// </summary>
    public Map()
    {
        Forward = new Indexer<T1, T2>(forward);
        Reverse = new Indexer<T2, T1>(reverse);
    }

    /// <summary>
    /// Accesses a value mapped from <typeparamref name="T1"/> to <typeparamref name="T2"/>.
    /// </summary>
    public Indexer<T1, T2> Forward { get; private set; }

    /// <summary>
    /// Accesses a value mapped from <typeparamref name="T2"/> to <typeparamref name="T1"/>.
    /// </summary>
    public Indexer<T2, T1> Reverse { get; private set; }

    /// <summary>
    /// Creates a new <see cref="Map{T1, T2}"/> from the specified dict.
    /// </summary>
    public Map(IEnumerable<KeyValuePair<T1, T2>> vals) : this()
    {
        foreach (var item in vals) {
            forward.Add(item.Key, item.Value);
            reverse.Add(item.Value, item.Key);
        }
    }

    /// <summary>
    /// The number of elements in this collection.
    /// </summary>
    public int Count => forward.Count;

    /// <summary>
    /// Acts as an indexer for a dict.
    /// </summary>
    public sealed class Indexer<T3, T4> : IEnumerable<KeyValuePair<T3, T4>>
    {
        private readonly Dictionary<T3, T4> dictionary;

        internal Indexer(Dictionary<T3, T4> dictionary)
        {
            this.dictionary = dictionary;
        }

        /// <inheritdoc/>
        public T4 this[T3 index] {
            get => dictionary[index];
            set => dictionary[index] = value;
        }

        /// <inheritdoc/>
        public bool TryGetValue(T3 index, out T4 value)
        {
            return dictionary.TryGetValue(index, out value);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Enumerates each pair of values.
        /// </summary>
        public IEnumerator<KeyValuePair<T3, T4>> GetEnumerator()
        {
            return dictionary.GetEnumerator();
        }
    }

    /// <inheritdoc/>
    public void Add(T1 t1, T2 t2)
    {
        forward.Add(t1, t2);
        reverse.Add(t2, t1);
    }

    /// <inheritdoc/>
    public void Set(T1 t1, T2 t2)
    {
        forward[t1] = t2;
        reverse[t2] = t1;
    }

    /// <inheritdoc/>
    public void Remove(T1 t1)
    {
        reverse.Remove(forward[t1]);
        forward.Remove(t1);
    }

    /// <inheritdoc/>
    public void Remove(T2 t2)
    {
        forward.Remove(reverse[t2]);
        reverse.Remove(t2);
    }

    /// <inheritdoc/>
    public void Clear()
    {
        forward.Clear();
        reverse.Clear();
    }
}
