using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Storage
{
    public class ByteList : IReadOnlyList<int>
    {
        private readonly byte[] _bytes;

        public static ByteList FromValues(IEnumerable<int> values)
        {
            return new ByteList(values.Select(CheckByte));
        }

        public ByteList(IEnumerable<byte> bytes)
        {
            var array = bytes?.ToArray();
            _bytes = array?.Length > 0 ? array : Array.Empty<byte>();
        }

        public int Count
        {
            get { return _bytes.Length; }
        }

        public IEnumerator GetEnumerator()
        {
            return _bytes.GetEnumerator();
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return _bytes.Select(b => (int)b).GetEnumerator();
        }

        int IReadOnlyList<int>.this[int index] => _bytes[index];

        public static byte CheckByte(int v)
        {
            if (v < 0 || v >= byte.MaxValue)
            {
                throw new ArgumentException();
            }

            return (byte)v;
        }
    }
}
