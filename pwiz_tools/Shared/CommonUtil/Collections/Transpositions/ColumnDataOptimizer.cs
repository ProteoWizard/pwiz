using pwiz.Common.SystemUtil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace pwiz.Common.Collections.Transpositions
{
    public class ColumnDataOptimizer<T>
    {
        public ColumnDataOptimizer() : this(null, ComputeItemSize())
        {
        }

        public ColumnDataOptimizer(ValueCache valueCache, int itemSize)
        {
            ValueCache = valueCache;
            ItemSize = itemSize;
        }
        
        public int ItemSize { get; private set; }

        public static int ComputeItemSize()
        {
            if (typeof(T).IsClass)
            {
                return IntPtr.Size;
            }

            return Marshal.ReadInt32(typeof(T).TypeHandle.Value, 4);
        }
        
        public ValueCache ValueCache { get; private set; }
        
        public IEnumerable<ColumnData> StoreLists(IEnumerable<ColumnData> lists)
        {
            var localValueCache = new ValueCache();
            var columnInfos = new List<ColumnDataInfo>();
            foreach (var list in lists)
            {
                var immutableList = list.ToImmutableList<T>();
                if (immutableList == null)
                {
                    columnInfos.Add(new ColumnDataInfo(list, null));
                }
                else if (true == ValueCache?.TryGetCachedValue(ref immutableList))
                {
                    columnInfos.Add(new ColumnDataInfo(ColumnData.Immutable(immutableList), null));
                }
                else
                {
                    columnInfos.Add(new ColumnDataInfo(list, localValueCache.CacheValue(HashedObject.ValueOf(immutableList))));
                }
            }
            var storedLists = StoreUniqueLists(columnInfos.Where(col => col.ImmutableList != null).Select(col => col.ImmutableList).Distinct())
                .ToDictionary(tuple => tuple.Item1, tuple => tuple.Item2);
            foreach (var columnInfo in columnInfos)
            {
                if (columnInfo.ImmutableList == null || !storedLists.TryGetValue(columnInfo.ImmutableList, out var storedList))
                {
                    yield return columnInfo.OriginalColumnData;
                }
                else
                {
                    if (ValueCache != null && storedList.IsImmutableList<T>())
                    {
                        yield return ColumnData.Immutable(ValueCache.CacheValue(storedList.ToImmutableList<T>()));
                    }
                    else
                    {
                        yield return storedList;
                    }
                }
            }
        }

        public IEnumerable<Tuple<HashedObject<ImmutableList<T>>, ColumnData>> StoreUniqueLists(IEnumerable<HashedObject<ImmutableList<T>>> lists)
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

            int totalItemCount = remainingLists.Sum(list => list.ImmutableList.Value.Count);
            var potentialSavings = (totalItemCount - allUniqueItems.Count) * (ItemSize - 1) - IntPtr.Size * remainingLists.Count;
            if (potentialSavings <= 0)
            {
                yield break;
            }

            var factorListBuilder = new FactorList<T>.Builder(remainingLists.SelectMany(listInfo => listInfo.UniqueValues));
            foreach (var listInfo in remainingLists)
            {
                yield return Tuple.Create(listInfo.ImmutableList,
                    ColumnData.FactorList(factorListBuilder.MakeFactorList(listInfo.ImmutableList.Value)));
            }
        }

        class ColumnDataInfo
        {
            public ColumnDataInfo(ColumnData columnData, HashedObject<ImmutableList<T>> immutableList)
            {
                OriginalColumnData = columnData;
                ImmutableList = immutableList;
            }

            public ColumnData OriginalColumnData { get; }
            public HashedObject<ImmutableList<T>> ImmutableList { get; }
        }

        class ListInfo
        {
            public ListInfo(HashedObject<ImmutableList<T>> list)
            {
                ImmutableList = list;
                UniqueValues = ImmutableList.Value.Distinct().ToList();
            }

            public HashedObject<ImmutableList<T>> ImmutableList { get; }
            public List<T> UniqueValues { get; }
        }
    }
}
