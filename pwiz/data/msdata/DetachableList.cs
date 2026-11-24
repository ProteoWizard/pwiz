using System.Collections;

namespace Pwiz.Data.MsData.Spectra;

/// <summary>
/// A growable array of values, like <see cref="List{T}"/>, that can hand its storage to a
/// consumer without copying. Holds the payload of a <see cref="BinaryDataArray"/>.
/// </summary>
/// <remarks>
/// A combined ion mobility spectrum can hold a million or more values, and a reader often learns
/// the final count only after appending the last one. <see cref="List{T}"/> grows the same way
/// but never gives up its storage, so a consumer that wants an array must copy it.
/// <see cref="Detach"/> hands the storage over instead, together with the count of values in use.
/// </remarks>
public sealed class DetachableList<T> : IList<T>, IReadOnlyList<T>
{
    private const int DefaultCapacity = 4;

    private T[] _items = Array.Empty<T>();
    private int _count;

    /// <summary>Number of values in the buffer.</summary>
    public int Count => _count;

    /// <summary>
    /// Number of values the storage holds before it must grow. Setting it reallocates the
    /// storage to exactly that size.
    /// </summary>
    public int Capacity
    {
        get => _items.Length;
        set
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(value, _count);
            if (value == _items.Length)
                return;
            if (value == 0)
            {
                _items = Array.Empty<T>();
                return;
            }
            var items = new T[value];
            Array.Copy(_items, items, _count);
            _items = items;
        }
    }

    /// <summary>Gets or sets the value at <paramref name="index"/>.</summary>
    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _items[index];
        }
        set
        {
            if ((uint)index >= (uint)_count)
                throw new ArgumentOutOfRangeException(nameof(index));
            _items[index] = value;
        }
    }

    bool ICollection<T>.IsReadOnly => false;

    /// <summary>The values in use, for reading or writing in place.</summary>
    /// <remarks>Invalidated by anything that grows the storage.</remarks>
    public Span<T> AsSpan() => _items.AsSpan(0, _count);

    /// <summary>Appends <paramref name="item"/>.</summary>
    public void Add(T item)
    {
        // Same shape as List<T>.Add: locals and one unsigned compare let the JIT drop the
        // bounds check and inline this into per-point loops; the resize stays out of line
        var items = _items;
        int count = _count;
        if ((uint)count < (uint)items.Length)
        {
            _count = count + 1;
            items[count] = item;
        }
        else
        {
            AddWithResize(item);
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void AddWithResize(T item)
    {
        int count = _count;
        Grow(count + 1);
        _count = count + 1;
        _items[count] = item;
    }

    /// <summary>Appends <paramref name="items"/>.</summary>
    public void AddRange(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        switch (items)
        {
            case T[] array:
                AddSpan(array);
                break;
            case DetachableList<T> buffer:
                AddSpan(buffer.AsSpan());
                break;
            case List<T> list:
                AddSpan(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(list));
                break;
            case ICollection<T> collection:
                EnsureCapacity(_count + collection.Count);
                collection.CopyTo(_items, _count);
                _count += collection.Count;
                break;
            case IReadOnlyList<T> readOnlyList:
                EnsureCapacity(_count + readOnlyList.Count);
                for (int i = 0; i < readOnlyList.Count; i++)
                    _items[_count++] = readOnlyList[i];
                break;
            default:
                foreach (var item in items)
                    Add(item);
                break;
        }
    }

    private void AddSpan(ReadOnlySpan<T> items)
    {
        EnsureCapacity(_count + items.Length);
        items.CopyTo(_items.AsSpan(_count));
        _count += items.Length;
    }

    /// <summary>
    /// Sets the number of values to <paramref name="count"/>, for filling in place through
    /// <see cref="AsSpan"/>. Values added by growing the count are zero (default).
    /// </summary>
    public void SetCount(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count > _items.Length)
            Grow(count);
        else if (count > _count)
            Array.Clear(_items, _count, count - _count);
        _count = count;
    }

    /// <summary>Makes room for at least <paramref name="capacity"/> values.</summary>
    public void EnsureCapacity(int capacity)
    {
        if (capacity > _items.Length)
            Grow(capacity);
    }

    private void Grow(int capacity)
    {
        int newCapacity = _items.Length == 0 ? DefaultCapacity : 2 * _items.Length;
        if ((uint)newCapacity > (uint)Array.MaxLength)
            newCapacity = Array.MaxLength;
        Capacity = Math.Max(newCapacity, capacity);
    }

    /// <summary>
    /// Removes the values without copying them: returns the storage, whose first
    /// <paramref name="count"/> elements are the values and which may be longer, and leaves the
    /// buffer empty with no storage. With no values it returns an empty array.
    /// </summary>
    public T[] Detach(out int count)
    {
        var items = _count == 0 ? Array.Empty<T>() : _items;
        count = _count;
        _items = Array.Empty<T>();
        _count = 0;
        return items;
    }

    /// <summary>
    /// Replaces the values with the first <paramref name="count"/> elements of
    /// <paramref name="items"/>, taking the array as storage instead of copying it. The caller
    /// must not use the array afterwards.
    /// </summary>
    public void Attach(T[] items, int count)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, items.Length);
        _items = items;
        _count = count;
    }

    /// <summary>Removes all values, keeping the storage.</summary>
    public void Clear()
    {
        if (System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            Array.Clear(_items, 0, _count);
        _count = 0;
    }

    /// <summary>Copies the values into a new array of exactly <see cref="Count"/> elements.</summary>
    public T[] ToArray() => AsSpan().ToArray();

    /// <inheritdoc/>
    public int IndexOf(T item) => Array.IndexOf(_items, item, 0, _count);

    /// <inheritdoc/>
    public bool Contains(T item) => IndexOf(item) >= 0;

    /// <inheritdoc/>
    public void CopyTo(T[] array, int arrayIndex) => Array.Copy(_items, 0, array, arrayIndex, _count);

    /// <inheritdoc/>
    public void Insert(int index, T item)
    {
        if ((uint)index > (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (_count == _items.Length)
            Grow(_count + 1);
        Array.Copy(_items, index, _items, index + 1, _count - index);
        _items[index] = item;
        _count++;
    }

    /// <inheritdoc/>
    public void RemoveAt(int index)
    {
        if ((uint)index >= (uint)_count)
            throw new ArgumentOutOfRangeException(nameof(index));
        _count--;
        Array.Copy(_items, index + 1, _items, index, _count - index);
    }

    /// <inheritdoc/>
    public bool Remove(T item)
    {
        int index = IndexOf(item);
        if (index < 0)
            return false;
        RemoveAt(index);
        return true;
    }

    /// <summary>Enumerates the values without allocating.</summary>
    public Enumerator GetEnumerator() => new(this);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Enumerator over a <see cref="DetachableList{T}"/>.</summary>
    public struct Enumerator : IEnumerator<T>
    {
        private readonly DetachableList<T> _buffer;
        private int _index;

        internal Enumerator(DetachableList<T> buffer)
        {
            _buffer = buffer;
            _index = -1;
        }

        /// <inheritdoc/>
        public readonly T Current => _buffer._items[_index];

        readonly object? IEnumerator.Current => Current;

        /// <inheritdoc/>
        public bool MoveNext() => ++_index < _buffer._count;

        /// <inheritdoc/>
        public void Reset() => _index = -1;

        /// <inheritdoc/>
        public readonly void Dispose() { }
    }
}
