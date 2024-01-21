using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Storage
{
    public struct ByteList : IReadOnlyList<int>
    {
        private readonly byte[] _bytes;

        public ByteList(IEnumerable<byte> bytes)
        {
            var array = bytes?.ToArray();
            _bytes = array?.Length > 0 ? array : null;
        }

        private byte[] Bytes
        {
            get { return _bytes ?? Array.Empty<byte>(); }
        }
        
        public int Count
        {
            get { return Bytes.Length; }
        }

        public IEnumerator GetEnumerator()
        {
            return Bytes.GetEnumerator();
        }

        IEnumerator<int> IEnumerable<int>.GetEnumerator()
        {
            return Bytes.Select(b => (int)b).GetEnumerator();
        }

        int IReadOnlyList<int>.this[int index] => Bytes[index];

        public static int GetOverhead(int length)
        {
            return (length + 31) / 16 * 16;
        }
    }
}
