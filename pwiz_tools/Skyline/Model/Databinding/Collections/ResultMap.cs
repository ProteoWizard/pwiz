using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Databinding.Collections
{
    public class ResultMap<TResult> : IDictionary<ResultKey, TResult> where TResult : Result
    {
        public static readonly ResultMap<TResult> EMPTY = new ResultMap<TResult>(ImmutableList.Empty<TResult>());

        private ReplicatePositions _replicatePositions;
        private ImmutableList<TResult> _results;

        public ResultMap(IEnumerable<TResult> resultEnumerable)
        {
            _results = EnsureSorted(resultEnumerable.ToImmutable());
            _replicatePositions = GetReplicatePositions(_results);
        }

        public ICollection<ResultKey> Keys
        {
            get
            {
                return ReadOnlyList.Create(Count, GetResultKey);
            }
        }

        public ICollection<TResult> Values
        {
            get
            {
                return _results;
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<ResultKey, TResult>> GetEnumerator()
        {
            return Enumerable.Range(0, Count)
                .Select(i => new KeyValuePair<ResultKey, TResult>(GetResultKey(i), _results[i])).GetEnumerator();
        }

        void ICollection<KeyValuePair<ResultKey, TResult>>.Add(KeyValuePair<ResultKey, TResult> item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<KeyValuePair<ResultKey, TResult>>.Clear()
        {
            throw new InvalidOperationException();
        }

        bool ICollection<KeyValuePair<ResultKey, TResult>>.Remove(KeyValuePair<ResultKey, TResult> item)
        {
            throw new InvalidOperationException();
        }

        void IDictionary<ResultKey, TResult>.Add(ResultKey key, TResult value)
        {
            throw new InvalidOperationException();
        }

        bool IDictionary<ResultKey, TResult>.Remove(ResultKey key)
        {
            throw new InvalidOperationException();
        }

        public int Count
        {
            get { return _results.Count; }
        }

        protected int GetReplicateIndex(TResult result)
        {
            return result.GetResultFile().Replicate.ReplicateIndex;
        }

        private ImmutableList<TResult> EnsureSorted(ImmutableList<TResult> list)
        {
            if (list.Count <= 1 || Enumerable.Range(0, list.Count - 1)
                    .All(i =>
                        GetReplicateIndex(list[i]) <=
                        GetReplicateIndex(list[i + 1])))
            {
                return list;
            }

            return list.OrderBy(GetReplicateIndex).ToImmutable();
        }

        private ReplicatePositions GetReplicatePositions(ImmutableList<TResult> list)
        {
            var counts = new List<int>();
            bool simple = true;
            foreach (var result in list)
            {
                int replicateIndex = GetReplicateIndex(result);
                if (replicateIndex == counts.Count)
                {
                    counts.Add(1);
                    continue;
                }
                simple = false;
                if (replicateIndex < counts.Count - 1)
                {
                    throw new ArgumentException(@"Must be sorted by replicate index");
                }
                while (replicateIndex > counts.Count)
                {
                    counts.Add(0);
                }
                Assume.AreEqual(replicateIndex, counts.Count - 1);
                counts[replicateIndex]++;
            }

            if (simple)
            {
                return null;
            }

            return ReplicatePositions.FromCounts(counts);
        }

        protected ResultKey GetResultKey(int index)
        {
            var result = _results[index];
            var replicate = result.GetResultFile().Replicate;
            int fileIndex = 0;
            if (_replicatePositions != null)
            {
                int replicateIndex = replicate.ReplicateIndex;
                fileIndex = index - _replicatePositions.GetStart(replicateIndex);
            }
            return new ResultKey(replicate, fileIndex);
        }

        public bool TryGetValue(ResultKey key, out TResult value)
        {
            int position = IndexOfKey(key);
            if (position < 0)
            {
                value = null;
                return false;
            }

            value = _results[position];
            return true;
        }

        public bool Contains(KeyValuePair<ResultKey, TResult> item)
        {
            int index = IndexOfKey(item.Key);
            return index >= 0 && Equals(item.Value, _results[index]);
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public bool ContainsKey(ResultKey key)
        {
            return IndexOfKey(key) >= 0;
        }

        public TResult this[ResultKey key]
        {
            get
            {
                int index = IndexOfKey(key);
                if (index < 0)
                {
                    throw new KeyNotFoundException();
                }
                return _results[index];
            }
            set => throw new InvalidOperationException();
        }

        public void CopyTo(KeyValuePair<ResultKey, TResult>[] array, int arrayIndex)
        {
            foreach (var entry in this)
            {
                array[arrayIndex] = entry;
                arrayIndex++;
            }
        }

        private int IndexOfKey(ResultKey key)
        {
            var replicateIndex = key.ReplicateIndex;
            if (replicateIndex < 0)
            {
                return -1;
            }
            if (_replicatePositions == null)
            {
                if (key.FileIndex != 0)
                {
                    return -1;
                }

                if (replicateIndex >= _results.Count)
                {
                    return -1;
                }

                return replicateIndex;
            }

            if (replicateIndex >= _replicatePositions.ReplicateCount || key.FileIndex >= _replicatePositions.GetCount(replicateIndex))
            {
                return -1;
            }

            return _replicatePositions.GetStart(replicateIndex) + key.FileIndex;
        }
    }
}
