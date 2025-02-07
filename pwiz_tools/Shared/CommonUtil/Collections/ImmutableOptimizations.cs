using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    public static class ImmutableOptimizations
    {
        private static readonly ImmutableList<bool> _booleanLevels = new[] { false, true }.ToImmutable();
        public static ImmutableList<T> MaybeConstant<T>(this ImmutableList<T> list)
        {
            if (list == null)
            {
                return null;
            }
            if (list is ConstantList<T>)
            {
                return list;
            }
            if (list.Count <= 1)
            {
                return list;
            }

            var first = list[0];
            if (list.Skip(1).All(item => Equals(first, item)))
            {
                return new ConstantList<T>(list.Count, first);
            }
            return list;
        }

        public static ImmutableList<T?> Nullables<T>(this IEnumerable<T?> list) where T:struct
        {
            return list as NullableList<T> ?? new NullableList<T>(list);
        }

        public static ImmutableList<bool> Booleans(this IEnumerable<bool> booleans)
        {
            var indexes = IntList.ValueOf(booleans.Select(b => b ? 1 : 0));
            return new Factor<bool>(_booleanLevels, indexes).MaybeConstant();
        }

        public static Factor<T> Factorize<T>(this IEnumerable<T> items)
        {
            return Factor<T>.FromItems(items);
        }

        public static Factor<T> ToFactor<T>(IEnumerable<T> items)
        {
            if (items is Factor<T> factor)
            {
                return factor;
            }
            var levelsDict = new Dictionary<ValueTuple<T>, int>();
            var levelIndices = new List<int>();
            foreach (var item in items)
            {
                if (!levelsDict.TryGetValue(ValueTuple.Create(item), out int levelIndex))
                {
                    levelIndex = levelsDict.Count;
                    levelsDict.Add(ValueTuple.Create(item), levelIndex);
                }

                levelIndices.Add(levelIndex);
            }

            var levels = levelsDict.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key.Item1).ToImmutable();
            return new Factor<T>(levels, IntList.Of(levelIndices));
        }
    }
}
