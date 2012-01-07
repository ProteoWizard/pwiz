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
using zlib;

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
            return reader.GetString(reader.GetOrdinal(columnName));
        }

        public static double GetDouble(this SQLiteDataReader reader, Enum columnName)
        {
            return reader.GetDouble(reader.GetOrdinal(columnName));
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

        public static byte[] Uncompress(this byte[] compressed, int sizeUncompressed)
        {
            if (compressed.Length == sizeUncompressed)
                return compressed;

            byte[] uncompressed = new byte[sizeUncompressed];
            ZOutputStream zstream = new ZOutputStream(new MemoryStream(uncompressed));
            zstream.Write(compressed, 0, compressed.Length);
            zstream.finish();
            if (zstream.TotalOut != sizeUncompressed)
                throw new IOException("Failure uncompressing data.");
            return uncompressed;
        }

// ReSharper disable InconsistentNaming
        private enum Levels { Z_DEFAULT_COMPRESSION = -1 }
// ReSharper restore InconsistentNaming

        public static byte[] Compress(this byte[] uncompressed)
        {
            return uncompressed.Compress((int) Levels.Z_DEFAULT_COMPRESSION);
        }

        public static byte[] Compress(this byte[] uncompressed, int level)
        {
            if (level == 0)
                return uncompressed;

            MemoryStream stream = new MemoryStream(uncompressed.Length);
            ZOutputStream zstream = new ZOutputStream(stream, level);
            zstream.Write(uncompressed, 0, uncompressed.Length);
            zstream.finish();

            int total = (int) zstream.TotalOut;
            // If compression did not improve the situation, then use
            // uncompressed bytes.
            if (total >= uncompressed.Length)
                return uncompressed;

            byte[] result = new byte[total];
            Array.Copy(stream.GetBuffer(), result, total);

            return result;
        }
    }
}
