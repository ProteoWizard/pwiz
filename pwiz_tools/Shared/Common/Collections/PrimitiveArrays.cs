/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.IO;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Methods for converting between arrays of primitive types (char, int, double, etc.) and
    /// bytes.  Note that this conversion is always done in the "Host" byte order (the
    /// byte order of the machine this code is running on).  
    /// </summary>
    public static class PrimitiveArrays
    {
        public static T[] FromBytes<T>(byte[] bytes) 
        {
            if (null == bytes)
            {
                return null;
            }
            T[] result = new T[bytes.Length / Buffer.ByteLength(new T[1])];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        public static byte[] ToBytes<T>(T[] array)
        {
            byte[] result = new byte[Buffer.ByteLength(array)];
            Buffer.BlockCopy(array, 0, result, 0, result.Length);
            return result;
        }

        public static T[] Read<T>(Stream stream, int count)
        {
            var result = new T[count];
            byte[] byteArray = new byte[Buffer.ByteLength(result)];
            stream.Read(byteArray, 0, byteArray.Length);
            Buffer.BlockCopy(byteArray, 0, result, 0, byteArray.Length);
            return result;
        }

        public static T ReadOneValue<T>(Stream stream)
        {
            return Read<T>(stream, 1)[0];
        }

        public static void Write<T>(Stream stream, T[] array)
        {
            var bytes = ToBytes(array);
            stream.Write(bytes, 0, bytes.Length);
        }

        public static void WriteOneValue<T>(Stream stream, T value)
        {
            Write(stream, new[] {value});
        }

        /// <summary>
        /// Changes whether a byte array is big-endian or little-endian by
        /// reversing the elements of the specified size.
        /// </summary>
        public static byte[] ReverseBytesInBlocks(byte[] bytes, int blockSize)
        {
            byte[] result = new byte[bytes.Length];
            for (int i = 0; i < result.Length; i++)
            {
                int sourceIndex = (i/blockSize*blockSize) + blockSize - 1 - i%blockSize;
                result[i] = bytes[sourceIndex];
            }
            return result;
        }
    }
}
