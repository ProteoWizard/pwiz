using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using pwiz.Common.Collections;

namespace pwiz.Common.Storage
{
    public class EfficientListStorage
    {
        
    }

    public static class EfficientListStorage<T>
    {
        static EfficientListStorage()
        {
            ItemSize = IntPtr.Size;
            ItemSize = Marshal.SizeOf<T>();
        }
        public static int ItemSize
        {
            get; private set;
        }

        public static IEnumerable<IReadOnlyList<T>> StoreLists(IEnumerable<IReadOnlyList<T>> lists)
        {
            var immutableLists = lists.Select(ImmutableList.ValueOf);
            var lookup = immutableLists.Where(list=>null != list).ToLookup(immutableList => immutableList);
            var uniqueLists = lookup.Select(group => group.Key).ToList();
        }

        public static IEnumerable<KeyValuePair<ImmutableList<T>, IReadOnlyList<T>>> StoreUniqueLists(IList<ImmutableList<T>> lists)
        {
            var remainingLists = new List<ListInfo>();
            foreach (var list in lists)
            {
                if (list.Count <= 2)
                {
                    yield return new KeyValuePair<ImmutableList<T>, IReadOnlyList<T>>(list, list);
                    continue;
                }

                var listInfo = new ListInfo(list);
                if (listInfo.UniqueValues.Count == 1)
                {
                    yield return new KeyValuePair<ImmutableList<T>, IReadOnlyList<T>>(list,
                        new ConstantList<T>(list[0], list.Count));
                }
                remainingLists.Add(listInfo);
            }

            if (remainingLists.Count == 0)
            {
                yield break;
            }

            var allUniqueItems = remainingLists.SelectMany(listInfo => listInfo.UniqueValues)
                .Where(v => !Equals(v, default(T))).ToList();
            
        }

        public static int GetListByteSize(int itemCount)
        {
            return (itemCount * ItemSize + 31) / 32 * 32;
        }

        class ListInfo
        {
            public ListInfo(IList<T> originalList)
            {
                OriginalList = originalList;
                UniqueValues = OriginalList.Distinct().ToList();
            }
            
            public IList<T> OriginalList { get; }
            public IList<T> UniqueValues { get; }
        }
    }
}
