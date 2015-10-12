/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Text;
using pwiz.Common.Collections;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Handles storing and retrieving abbreviated scan ids from a <see cref="MsDataFileImpl"/>
    /// </summary>
    public class MsDataFileScanIds
    {
        private readonly int[] _startBytes;
        private readonly int[] _lengths;
        private readonly byte[] _idBytes;

        public MsDataFileScanIds(int[] startBytes, int[] lengths, byte[] idBytes)
        {
            _startBytes = startBytes;
            _lengths = lengths;
            _idBytes = idBytes;
        }

        /// <summary>
        /// Retrieve an indexed entry from our internal table that MsDataFileImpl can use to retrieve raw scan data
        /// </summary>
        public string GetMsDataFileSpectrumId(int index)
        {
            return Encoding.UTF8.GetString(_idBytes, _startBytes[index], _lengths[index]);
        }

        public static byte[] ToBytes(IEnumerable<string> scanIds)
        {
            var listStartBytes = new List<int>();
            var listLengths = new List<int>();
            var listIdBytes = new List<byte>();
            foreach (var scanId in scanIds)
            {
                int len = Encoding.UTF8.GetByteCount(scanId);
                var buffer = new byte[len];
                Encoding.UTF8.GetBytes(scanId, 0, scanId.Length, buffer, 0);
                listStartBytes.Add(listIdBytes.Count);
                listLengths.Add(len);
                listIdBytes.AddRange(buffer);
            }
            if (listStartBytes.Count == 0)
                return new byte[0];

            var listEntryBytes = new List<byte>();
            listEntryBytes.AddRange(PrimitiveArrays.ToBytes(listStartBytes.ToArray()));
            listEntryBytes.AddRange(PrimitiveArrays.ToBytes(listLengths.ToArray()));
            var entryBytes = listEntryBytes.ToArray();
            var entryBytesCompressed = entryBytes.Compress();
            var scanIdBytes = listIdBytes.ToArray();
            var scanIdBytesCompressed = scanIdBytes.Compress();

            var listResultBytes = new List<byte>();
            listResultBytes.AddRange(BitConverter.GetBytes(entryBytes.Length));
            listResultBytes.AddRange(BitConverter.GetBytes(entryBytesCompressed.Length));
            listResultBytes.AddRange(BitConverter.GetBytes(scanIdBytes.Length));
            listResultBytes.AddRange(BitConverter.GetBytes(scanIdBytesCompressed.Length));
            listResultBytes.AddRange(entryBytesCompressed);
            listResultBytes.AddRange(scanIdBytesCompressed);
            return listResultBytes.ToArray();
        }

        public static MsDataFileScanIds FromBytes(byte[] byteArray)
        {
            if (byteArray.Length == 0)
                return null;

            int i = 0;
            int entryBytesCount = GetInt(i++, byteArray);
            int entryBytesCompressedCount = GetInt(i++, byteArray);
            int scanIdBytesCount = GetInt(i++, byteArray);
            int scanIdBytesCompressedCount = GetInt(i++, byteArray);

            var entryBytesCompressed = new byte[entryBytesCompressedCount];
            Array.Copy(byteArray, i*sizeof(int), entryBytesCompressed, 0, entryBytesCompressedCount);
            var entryBytes = entryBytesCompressed.Uncompress(entryBytesCount);
            var startBytesArray = new byte[entryBytes.Length/2];
            Array.Copy(entryBytes, 0, startBytesArray, 0, startBytesArray.Length);
            var startBytes = PrimitiveArrays.FromBytes<int>(startBytesArray);
            var lengthBytesArray = new byte[entryBytes.Length/2];
            Array.Copy(entryBytes, startBytesArray.Length, lengthBytesArray, 0, lengthBytesArray.Length);
            var lengths = PrimitiveArrays.FromBytes<int>(lengthBytesArray);
            Assume.IsTrue(startBytesArray.Length + lengthBytesArray.Length == entryBytes.Length);

            var scanIdBytesCompressed = new byte[scanIdBytesCompressedCount];
            Array.Copy(byteArray, i*sizeof(int) + entryBytesCompressedCount, scanIdBytesCompressed,
                0, scanIdBytesCompressedCount);
            var scanIdBytes = scanIdBytesCompressed.Uncompress(scanIdBytesCount);

            return new MsDataFileScanIds(startBytes, lengths, scanIdBytes);
        }

        private static int GetInt(int i, byte[] byteArray)
        {
            return BitConverter.ToInt32(byteArray, i * sizeof(int));
        }
    }
}
