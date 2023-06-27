/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Data.SQLite;
using System.IO;
using pwiz.Skyline.Properties;
using Ionic.Zlib;

namespace pwiz.Skyline.Util.Extensions
{
    public static class UtilDB
    {
        public static int GetOrdinal(this SQLiteDataReader reader, Enum columnName)
        {
// ReSharper disable SpecifyACultureInStringConversionExplicitly
            return reader.GetOrdinal(columnName.ToString());
// ReSharper restore SpecifyACultureInStringConversionExplicitly
        }

        public static string GetString(this SQLiteDataReader reader, Enum columnName)
        {
            var index = reader.GetOrdinal(columnName);
            return reader.IsDBNull(index) ? null : reader.GetString(index);
        }

        public static double GetDouble(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetDouble(reader.GetOrdinal(columnName));
        }

        public static double? GetNullableDouble(SQLiteDataReader reader, int column)
        {
            return reader.IsDBNull(column) ? (double?)null : reader.GetDouble(column);
        }

        public static float GetFloat(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetFloat(reader.GetOrdinal(columnName));
        }

        public static short GetInt16(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetInt16(reader.GetOrdinal(columnName));
        }

        public static int GetInt32(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetInt32(reader.GetOrdinal(columnName));
        }

        public static byte[] GetBytes(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetBytes(reader.GetOrdinal(columnName));
        }

        public static byte[] GetBytes(this SQLiteDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
                return null;

// ReSharper disable AssignNullToNotNullAttribute
            long bufferLength = reader.GetBytes(index, 0, null, 0, 0);
// ReSharper restore AssignNullToNotNullAttribute

            byte[] res = new byte[bufferLength];
            reader.GetBytes(index, 0, res, 0, (int)bufferLength);
            return res;
        }

        public static byte[] Uncompress(this byte[] compressed, int sizeUncompressed, bool checkSizeDifference)
        {
            if (compressed.Length == sizeUncompressed)
                return compressed;
            byte[] uncompressed;
            try
            {
                uncompressed = ZlibStream.UncompressBuffer(compressed);
            }
            catch (Exception x)
            {
                throw new IOException(Resources.UtilDB_Uncompress_Failure_uncompressing_data, x);
            }
            if (checkSizeDifference && (uncompressed.Length != sizeUncompressed))
                throw new IOException(Resources.UtilDB_Uncompress_Failure_uncompressing_data);
            return uncompressed;
        }

        public static byte[] Uncompress(this byte[] compressed, int sizeUncompressed)
        {
            return Uncompress(compressed,sizeUncompressed,true);
        }

        public static byte[] Compress(this byte[] uncompressed)
        {
            return uncompressed.Compress(6);  // The default compression level, with a good balance of speed and compression efficiency.
        }

        [ThreadStatic]
        private static ZlibCodec _zDeflate;
        
        private static ZlibCodec GetZlibCodec(int level, bool decompress = false)
        {
            if (decompress)
            {
                throw new NotImplementedException();
            }
            else
            {
                if (_zDeflate == null || _zDeflate.CompressLevel != CompressionLevel.Level0 + level)
                {
                    _zDeflate = new ZlibCodec();
                    _zDeflate.Strategy = CompressionStrategy.Default;
                    _zDeflate.InitializeDeflate(CompressionLevel.Level0 + level, true);
                }

                return _zDeflate;
            }
        }
        private const int _bufferSize = 16384;

        [ThreadStatic]
        private static byte[] _workingBuffer;
        private static byte[] WorkingBuffer => _workingBuffer ??= new byte[_bufferSize];

        public static byte[] Compress(this byte[] uncompressed, int level)
        {
            if (level == 0)
                return uncompressed;

            byte[] result;
            using (var ms = new MemoryStream())
            {
                var Z = GetZlibCodec(level);
                Z.InputBuffer = uncompressed;
                Z.NextIn = 0;
                Z.AvailableBytesIn = uncompressed.Length;
                Z.OutputBuffer = WorkingBuffer;

                do
                {
                    Z.NextOut = 0;
                    Z.AvailableBytesOut = _workingBuffer.Length;
                    switch (Z.Deflate(FlushType.None))
                    {
                        case 0:
                        case 1:
                            ms.Write(_workingBuffer, 0, _workingBuffer.Length - Z.AvailableBytesOut);
                            continue;
                        default:
                            throw new ZlibException(@"deflating: " + Z.Message);
                    }
                }
                while (Z.AvailableBytesIn > 0 || Z.AvailableBytesOut == 0);

                do
                {
                    Z.NextOut = 0;
                    Z.AvailableBytesOut = _workingBuffer.Length;
                    switch (Z.Deflate(FlushType.Finish))
                    {
                        case 0:
                        case 1:
                            if (_workingBuffer.Length - Z.AvailableBytesOut > 0)
                                ms.Write(_workingBuffer, 0, _workingBuffer.Length - Z.AvailableBytesOut);
                            continue;
                        default:
                            throw new ZlibException(@"deflating: " + Z.Message);
                    }
                }
                while (Z.AvailableBytesIn > 0 || Z.AvailableBytesOut == 0);
                Z.ResetDeflate();

                result =  ms.ToArray();
            }


            // If compression did not improve the situation, then use
            // uncompressed bytes.
            if (result.Length >= uncompressed.Length)
                return uncompressed;

            return result;
        }

    }
}
