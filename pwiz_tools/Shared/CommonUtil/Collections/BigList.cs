using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace pwiz.Common.Collections
{
    public static class BigLists
    {
        public static int GetDefaultChunkSize<T>()
        {
            int itemSize = typeof(T).IsValueType ? Marshal.SizeOf<T>() : IntPtr.Size;
            return Math.Max((1 << 29) / itemSize, 1);
        }

        public static BigList<T> ToBigList<T>(this IEnumerable<T> source)
        {
            if (source is BigList<T> bigList)
            {
                return bigList;
            }

            if (source is ImmutableList<T> immutableList)
            {
                return new BigList<T>(ImmutableList.Singleton(immutableList));
            }
            return ToBigList(source, GetDefaultChunkSize<T>());
        }

        public static BigList<T> ToBigList<T>(this IEnumerable<T> source, int chunkSize)
        {
            var lists = new List<ImmutableList<T>>();
            var currentList = new List<T>();
            foreach (var item in source)
            {
                currentList.Add(item);
                if (currentList.Count >= chunkSize)
                {
                    lists.Add(currentList.ToImmutable());
                }
            }

            if (currentList.Count > 0)
            {
                lists.Add(currentList.ToImmutable());
            }

            if (lists.Count == 0)
            {
                return BigList<T>.Empty;
            }

            return new BigList<T>(lists.ToImmutable());
        }
    }

    public class BigList<T> : IEnumerable<T>
    {
        public static readonly BigList<T> Empty = new BigList<T>(ImmutableList.Empty<ImmutableList<T>>());
        private readonly ImmutableList<ImmutableList<T>> _lists;

        public BigList(ImmutableList<ImmutableList<T>> lists)
        {
            _lists = lists;
            Count = _lists.Sum(list => (long) list.Count);
        }

        public long Count { get; }
        public long Length
        {
            get { return Count; }
        }

        public T this[long index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw new IndexOutOfRangeException();
                }

                foreach (var list in _lists)
                {
                    if (index < list.Count)
                    {
                        return list[(int)index];
                    }

                    index -= list.Count;
                }

                throw new IndexOutOfRangeException();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _lists.SelectMany(list => list).GetEnumerator();
        }

        public ImmutableList<T> Truncate(int count)
        {
            if (_lists.Count == 0)
            {
                return ImmutableList<T>.EMPTY;
            }

            if (count <= _lists[0].Count)
            {
                return _lists[0];
            }

            return ImmutableList.ValueOf(this.Take(count));
        }

        public BigList<T> Sort(IComparer<T> comparer)
        {
            if (_lists.Count == 0)
            {
                return Empty;
            }

            if (_lists.Count == 1)
            {
                // Simple case: just sort the single list
                var sortedList = _lists[0].OrderBy(item => item, comparer).ToImmutable();
                return new BigList<T>(ImmutableList.Singleton(sortedList));
            }

            // Multiple lists: need to merge sort
            // First, sort each individual list
            var sortedLists = _lists.Select(list => (IEnumerable<T>) list.OrderBy(item => item, comparer)).ToList();

            // Now merge all sorted lists using k-way merge
            var merged = MergeSortedLists(sortedLists, comparer);

            // Convert back to BigList with appropriate chunking
            return merged.ToBigList(BigLists.GetDefaultChunkSize<T>());
        }

        private IEnumerable<T> MergeSortedLists(List<IEnumerable<T>> sortedLists, IComparer<T> comparer)
        {
            // Create enumerators for each list
            var enumerators = sortedLists.Select(list => list.GetEnumerator()).ToList();
            var currentValues = new T[enumerators.Count];
            var hasValue = new bool[enumerators.Count];

            // Initialize by moving all enumerators to their first element
            for (int i = 0; i < enumerators.Count; i++)
            {
                if (enumerators[i].MoveNext())
                {
                    currentValues[i] = enumerators[i].Current;
                    hasValue[i] = true;
                }
            }

            try
            {
                while (true)
                {
                    int minIndex = -1;
                    T minValue = default(T);

                    // Find the minimum element across all enumerators
                    for (int i = 0; i < enumerators.Count; i++)
                    {
                        if (hasValue[i])
                        {
                            if (minIndex == -1 || comparer.Compare(currentValues[i], minValue) < 0)
                            {
                                minIndex = i;
                                minValue = currentValues[i];
                            }
                        }
                    }

                    if (minIndex == -1)
                    {
                        // All lists exhausted
                        break;
                    }

                    yield return minValue;

                    // Advance the enumerator that had the minimum value
                    if (enumerators[minIndex].MoveNext())
                    {
                        currentValues[minIndex] = enumerators[minIndex].Current;
                    }
                    else
                    {
                        hasValue[minIndex] = false;
                    }
                }
            }
            finally
            {
                // Dispose all enumerators
                foreach (var enumerator in enumerators)
                {
                    enumerator.Dispose();
                }
            }
        }
    }
}
