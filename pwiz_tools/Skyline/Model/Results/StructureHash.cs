using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.Model.Results
{
    public struct HashValue16 : IReadOnlyList<byte>
    {
        private ulong _lowLong;
        private ulong _highLong;

        public HashValue16(IEnumerable<byte> bytes)
        {
            var byteArray = bytes.ToArray();
            if (byteArray.Length < 16)
            {
                byteArray = byteArray.Concat(Enumerable.Repeat((byte) 0, 16 - byteArray.Length)).ToArray();
            }

            _lowLong = BitConverter.ToUInt64(byteArray, 0);
            _highLong = BitConverter.ToUInt64(byteArray, 8);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<byte> GetEnumerator()
        {
            return BitConverter.GetBytes(_lowLong).Concat(BitConverter.GetBytes(_highLong)).GetEnumerator();
        }

        public int Count
        {
            get { return 16; }
        }

        public byte this[int index]
        {
            get
            {
                if (index < 0 || index > 16)
                {
                    throw new IndexOutOfRangeException();
                }

                if (index < 8)
                {
                    return BitConverter.GetBytes(_lowLong)[index];
                }

                return BitConverter.GetBytes(_highLong)[index - 8];
            }
        }

        public bool IsZero
        {
            get { return _lowLong == 0 && _highLong == 0; }
        }

        public static HashValue16 ComputeHash(byte[] byteArray)
        {
            using (var sha1 = new SHA1CryptoServiceProvider())
            {
                var blockHash = new BlockHash(sha1, byteArray.Length);
                blockHash.ProcessBytes(byteArray);
                return new HashValue16(blockHash.FinalizeHashBytes());
            }
        }
    }
}
