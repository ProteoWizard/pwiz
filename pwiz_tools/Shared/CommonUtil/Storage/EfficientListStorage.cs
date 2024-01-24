using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Storage
{
    public static class EfficientListStorage
    {
        public static IEnumerable<ColumnData> StoreLists<T>(IEnumerable<ColumnData> lists)
        {
            return EfficientListStorage<T>.StoreLists(lists);
        }
    }

    public static class EfficientListStorage<T>
    {
        private static int ItemSize
        {
            get
            {
                return Marshal.ReadInt32(typeof(T).TypeHandle.Value, 4);
            }
        }

        public static IEnumerable<ColumnData> StoreLists(IEnumerable<ColumnData> lists)
        {
            var valueCache = new ValueCache();
            var immutableLists = lists.Select(list=>Tuple.Create(list, valueCache.CacheValue(HashedImmutableList(list)))).ToList();
            var storedLists = StoreUniqueLists(immutableLists.Select(tuple=>tuple.Item2).Distinct())
                .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
            foreach (var tuple in immutableLists)
            {
                if (tuple.Item2.Value == null || !storedLists.TryGetValue(tuple.Item2, out var storedList))
                {
                    yield return tuple.Item1;
                }
                else
                {
                    yield return storedList;
                }
            }
        }

        public static IEnumerable<Tuple<HashedObject<ImmutableList<T>>, ColumnData>> StoreUniqueLists(IEnumerable<HashedObject<ImmutableList<T>>> lists)
        {
            var remainingLists = new List<ListInfo>();
            foreach (var list in lists)
            {
                if (list == null)
                {
                    continue;
                }

                var listInfo = new ListInfo(list);
                if (listInfo.UniqueValues.Count == 1)
                {
                    if (Equals(list.Value[0], default(T)))
                    {
                        yield return Tuple.Create(list, default(ColumnData));
                    }
                    else if (list.Value.Count > 1)
                    {
                        yield return Tuple.Create(list, ColumnData.Constant(list.Value[0]));
                    }
                    continue;
                }
                remainingLists.Add(listInfo);
            }

            if (remainingLists.Count == 0)
            {
                yield break;
            }

            if (ItemSize <= 1)
            {
                yield break;
            }

            var mostUniqueItems = remainingLists.Max(list => list.UniqueValues.Count);
            if (mostUniqueItems >= byte.MaxValue)
            {
                yield break;
            }
            
            var allUniqueItems = remainingLists.SelectMany(listInfo => listInfo.UniqueValues)
                .Where(v => !Equals(v, default(T))).ToList();
            if (allUniqueItems.Count >= byte.MaxValue)
            {
                yield break;
            }

            int totalItemCount = remainingLists.Sum(list => list.OriginalList.Value.Count);
            var potentialSavings = (totalItemCount - allUniqueItems.Count) * (ItemSize - 1) - IntPtr.Size * remainingLists.Count;
            if (potentialSavings <= 0)
            {
                yield break;
            }

            var factorListBuilder = new FactorList<T>.Builder(remainingLists.SelectMany(listInfo=>listInfo.UniqueValues));
            foreach (var listInfo in remainingLists)
            {
                yield return Tuple.Create(listInfo.OriginalList,
                    new ColumnData(factorListBuilder.MakeFactorList(listInfo.OriginalList.Value)));
            }
        }

        class ListInfo
        {
            public ListInfo(HashedObject<ImmutableList<T>> originalList)
            {
                OriginalList = originalList;
                UniqueValues = OriginalList.Value.Distinct().ToList();
            }
            
            public HashedObject<ImmutableList<T>> OriginalList { get; }
            public IList<T> UniqueValues { get; }
        }

        private static HashedObject<ImmutableList<T>> HashedImmutableList(ColumnData list)
        {
            return HashedObject.ValueOf(list.ToImmutableList<T>());
        }

        private static Tuple<HashedObject<ImmutableList<T>>, IReadOnlyList<T>> CreateTuple(
            HashedObject<ImmutableList<T>> key, IReadOnlyList<T> value)
        {
            return Tuple.Create(key, value);
        }
    }
}
