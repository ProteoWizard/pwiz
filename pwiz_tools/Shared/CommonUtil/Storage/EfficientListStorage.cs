using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Storage
{
    public static class EfficientListStorage
    {
        public static IEnumerable<IReadOnlyList<T>> StoreLists<T>(IEnumerable<IReadOnlyList<T>> lists)
        {
            return EfficientListStorage<T>.StoreLists(lists);
        }
    }

    public static class EfficientListStorage<T>
    {
        public static IEnumerable<IReadOnlyList<T>> StoreLists(IEnumerable<IReadOnlyList<T>> lists)
        {
            var valueCache = new ValueCache();
            var immutableLists = lists.Select(list=>valueCache.CacheValue(HashedImmutableList(list))).ToList();
            var storedLists = StoreUniqueLists(immutableLists.Distinct())
                .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
            foreach (var hashedImmutableList in immutableLists)
            {
                if (hashedImmutableList == null || !storedLists.TryGetValue(hashedImmutableList, out var storedList))
                {
                    yield return hashedImmutableList?.Value;
                }
                else
                {
                    yield return storedList;
                }
            }
        }

        public static IEnumerable<Tuple<HashedObject<ImmutableList<T>>, IReadOnlyList<T>>> StoreUniqueLists(IEnumerable<HashedObject<ImmutableList<T>>> lists)
        {
            var remainingLists = new List<ListInfo>();
            foreach (var list in lists)
            {
                if (list.Value.Count <= 2)
                {
                    continue;
                }

                var listInfo = new ListInfo(list);
                if (listInfo.UniqueValues.Count == 1)
                {
                    yield return CreateTuple(list,
                        new ConstantList<T>(list.Value[0], list.Value.Count));
                    continue;
                }
                remainingLists.Add(listInfo);
            }

            if (remainingLists.Count == 0)
            {
                yield break;
            }

            var allUniqueItems = remainingLists.SelectMany(listInfo => listInfo.UniqueValues)
                .Where(v => !Equals(v, default(T))).ToList();
            if (allUniqueItems.Count >= byte.MaxValue)
            {
                yield break;
            }

            var factorListBuilder = new FactorList<T>.Builder(remainingLists.SelectMany(listInfo=>listInfo.UniqueValues));
            foreach (var listInfo in remainingLists)
            {
                yield return CreateTuple(listInfo.OriginalList,
                    factorListBuilder.MakeFactorList(listInfo.OriginalList.Value));
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

        private static HashedObject<ImmutableList<T>> HashedImmutableList(IEnumerable<T> list)
        {
            return HashedObject.ValueOf(ImmutableList.ValueOf(list));
        }

        private static Tuple<HashedObject<ImmutableList<T>>, IReadOnlyList<T>> CreateTuple(
            HashedObject<ImmutableList<T>> key, IReadOnlyList<T> value)
        {
            return Tuple.Create(key, value);
        }
    }
}
