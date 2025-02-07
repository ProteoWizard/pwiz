using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

namespace pwiz.Common.Collections
{
    public abstract class IntList : ImmutableList<int>
    {
        public static ImmutableList<int> Of(IEnumerable<int> values)
        {
            var list = ImmutableList.ValueOf(values);

            if (list.Count == 0)
            {
                return list;
            }

            int max = list.Max();
            int min = list.Min();
            if (min == max)
            {
                return new ConstantList<int>(list.Count, min);
            }
            if (min >= 0 && max <= 1)
            {
                return new Bits(list.Count, list.Select(v => v != 0));
            }

            if (min >= byte.MinValue && max <= byte.MaxValue)
            {
                return new Bytes(list.Count, list.Select(v => (byte)v));
            }

            if (min >= short.MinValue && max <= short.MaxValue)
            {
                return new Shorts(list.Count, list.Select(v => (short)v));
            }

            return list;
        }

        private class Bits : IntList
        {
            private readonly int _count;
            private readonly BitVector32[] _bits;

            public Bits(int count, IEnumerable<bool> boolValues)
            {
                _count = count;
                _bits = new BitVector32[(count + 31) / 32];
                int index = 0;
                foreach (var boolValue in boolValues)
                {
                    _bits[index / 32][index % 32] = boolValue;
                }
            }

            public override IEnumerator<int> GetEnumerator()
            {
                return Enumerable.Range(0, Count).Select(i => this[i]).GetEnumerator();
            }

            public override int Count
            {
                get { return _count; }
            }

            public override int this[int index]
            {
                get
                {
                    if (index < 0 || index >= Count)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    return _bits[index / 32][index % 32] ? 1 : 0;
                }
            }

            protected override bool SameTypeEquals(ImmutableList<int> obj)
            {
                var that = (Bits)obj;
                return Count == that.Count && _bits.SequenceEqual(that._bits);
            }
        }

        private class Bytes : IntList
        {
            private readonly byte[] _bytes;

            public Bytes(int count, IEnumerable<byte> bytes)
            {
                _bytes = new byte[count];
                int index = 0;
                foreach (var b in bytes)
                {
                    _bytes[index++] = b;
                }
            }

            public override IEnumerator<int> GetEnumerator()
            {
                return _bytes.Select(b => (int)b).GetEnumerator();
            }

            public override int Count
            {
                get { return _bytes.Length; }
            }

            public override int this[int index] => _bytes[index];
        }

        private class Shorts : IntList
        {
            private readonly short[] _shorts;

            public Shorts(int count, IEnumerable<short> shorts)
            {
                _shorts = new short[count];
                int index = 0;
                foreach (short s in shorts)
                {
                    _shorts[index++] = s;
                }
            }

            public override IEnumerator<int> GetEnumerator()
            {
                return _shorts.Select(s => (int)s).GetEnumerator();
            }

            public override int Count
            {
                get { return _shorts.Length; }
            }

            public override int this[int index] => _shorts[index];
        }
    }

    public static class Factor
    {
        public static Factor<T> ToFactor<T>(this IEnumerable<T> items)
        {
            return ToFactorIncludingLevels(items, ImmutableList<T>.EMPTY);
        }

        public static Factor<T> ToFactorIncludingLevels<T>(this IEnumerable<T> items, ImmutableList<T> startingLevels)
        {
            if (items is Factor<T> factor && factor.Levels.Take(startingLevels.Count).SequenceEqual(startingLevels))
            {
                return factor;
            }
            var levelsDict = new Dictionary<ValueTuple<T>, int>();
            foreach (var level in startingLevels)
            {
                levelsDict.Add(ValueTuple.Create(level), levelsDict.Count);
            }
            var levelIndices = new List<int>();
            foreach (var item in items)
            {
                var key = ValueTuple.Create(item);
                if (!levelsDict.TryGetValue(key, out int levelIndex))
                {
                    levelIndex = levelsDict.Count;
                    levelsDict.Add(key, levelIndex);
                }

                levelIndices.Add(levelIndex);
            }

            ImmutableList<T> levels;
            if (levelsDict.Count == startingLevels.Count)
            {
                levels = startingLevels;
            }
            else
            {
                levels = levelsDict.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key.Item1).ToImmutable();
            }
            return new Factor<T>(levels, IntList.Of(levelIndices));
        }
    }

    public class Factor<T> : ImmutableList<T>
    {
        public static Factor<T> FromItems(IEnumerable<T> items)
        {
            var levelsDict = new Dictionary<T, int>();
            var levelIndices = new List<int>();
            foreach (var item in items)
            {
                if (!levelsDict.TryGetValue(item, out int levelIndex))
                {
                    levelIndex = levelsDict.Count;
                    levelsDict.Add(item, levelIndex);
                }

                levelIndices.Add(levelIndex);
            }

            var levels = levelsDict.OrderBy(kvp => kvp.Value).Select(kvp => kvp.Key).ToImmutable();
            return new Factor<T>(levels, IntList.Of(levelIndices));
        }

        public Factor(ImmutableList<T> levels, ImmutableList<int> levelIndices)
        {
            Levels = levels;
            LevelIndices = levelIndices;
        }

        public ImmutableList<T> Levels { get; }
        public ImmutableList<int> LevelIndices { get; }

        public override int Count
        {
            get { return LevelIndices.Count; }
        }
        public override IEnumerator<T> GetEnumerator()
        {
            return LevelIndices.Select(i => Levels[i]).GetEnumerator();
        }

        public override T this[int index]
        {
            get
            {
                return Levels[LevelIndices[index]];
            }
        }
    }
}
