/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Efficient storage of integer values.
    /// If the values are only 0 or 1, then stored as <see cref="Bits"/>.
    /// May also be stored as bytes, or shorts.
    /// </summary>
    public abstract class IntegerList : ImmutableList<int>
    {
        public static ImmutableList<int> FromIntegers(IEnumerable<int> values)
        {
            var list = ImmutableList.ValueOf(values);

            if (list.Count <= 1)
            {
                return list;
            }

            int min = list[0];
            int max = list[0];
            foreach (var item in list.Skip(1))
            {
                min = Math.Min(min, item);
                max = Math.Max(max, item);
                if (min != max && (min < short.MinValue || max > short.MaxValue))
                {
                    return list;
                }
            }
            if (min == max)
            {
                return new ConstantList<int>(list.Count, min);
            }
            if (min == 0 && max == 1)
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

        private class Bits : IntegerList
        {
            private readonly int _count;
            private readonly int[] _bits;

            public Bits(int count, IEnumerable<bool> boolValues)
            {
                _count = count;
                _bits = new int[(count + 31) / 32];
                int index = 0;
                foreach (var boolValue in boolValues)
                {
                    if (boolValue)
                    {
                        _bits[index / 32] |= 1 << (index % 32);
                    }
                    index++;
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

                    var mask = 1 << (index % 32);
                    return 0 == (_bits[index / 32] & mask) ? 0 : 1;
                }
            }

            protected override bool SameTypeEquals(ImmutableList<int> obj)
            {
                var that = (Bits)obj;
                return Count == that.Count && _bits.SequenceEqual(that._bits);
            }
        }

        private class Bytes : IntegerList
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

        private class Shorts : IntegerList
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
}
