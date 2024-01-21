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
            var remainingLists = new List<ImmutableList<T>>();
            foreach (var list in lists)
            {
                if (list.Count <= 2)
                {
                    yield return new KeyValuePair<ImmutableList<T>, IReadOnlyList<T>>(list, list);
                    continue;
                }

                if (list.Skip(1).All(x => Equals(x, list[0])))
                {
                    yield return new KeyValuePair<ImmutableList<T>, IReadOnlyList<T>>(list, new ConstantList<T>(list[0], list.Count));
                    continue;
                }
                remainingLists.Add(list);
            }

            int totalItemCount = remainingLists.Sum(list => list.Count);
            var factorLevels = remainingLists.SelectMany(list => list).
                Where(item=>!Equals(item, default(T))).Distinct().ToList();
            var factorListSize = GetListByteSize(factorLevels.Count);
        }

        public static int GetListByteSize(int itemCount)
        {
            return (itemCount * ItemSize + 31) / 32 * 32;
        }
    }
}
