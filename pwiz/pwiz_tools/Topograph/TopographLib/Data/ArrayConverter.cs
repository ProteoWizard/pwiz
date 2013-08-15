/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;
using zlib;

namespace pwiz.Topograph.Data
{
    public class ArrayConverter
    {
        public static T[] FromBytes<T>(byte[] bytes)
        {
            if (bytes == null)
            {
                return null;
            }
            T[] result = new T[bytes.Length/Buffer.ByteLength(new T[1])];
            Buffer.BlockCopy(bytes, 0, result, 0, bytes.Length);
            return result;
        }

        public static byte[] ToBytes<T>(T[] array)
        {
            if (null == array)
            {
                return null;
            }
            byte[] result = new byte[Buffer.ByteLength(array)];
            Buffer.BlockCopy(array, 0, result, 0, result.Length);
            return result;
        }

        public static float[] ToFloats(double[] array)
        {
            float[] result = new float[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = (float) array[i];
            }
            return result;
        }

        public static double[] ToDoubles(float[] array)
        {
            double[] result = new double[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                result[i] = array[i];
            }
            return result;
        }
        public static byte[] Compress(byte[] uncompressed)
        {
            var stream = new MemoryStream();
            var zstream = new ZOutputStream(stream, 3);
            zstream.Write(uncompressed, 0, uncompressed.Length);
            zstream.finish();
            return stream.ToArray();
        }
        public static byte[] Uncompress(byte[] compressed, int sizeUncompressed)
        {
            var uncompressed = new byte[sizeUncompressed];
            var zstream = new ZOutputStream(new MemoryStream(uncompressed));
            zstream.Write(compressed, 0, compressed.Length);
            zstream.finish();
            if (zstream.TotalOut != sizeUncompressed)
                throw new IOException("Failure uncompressing data.");
            return uncompressed;
        }

        /// <summary>
        /// Workaround for MySQL driver/NHibernate issue.
        /// NHibernate has trouble selecting zero-length arrays from MySQL.
        /// bug #NH-2096
        /// </summary>
        public static T[] ZeroLengthToNull<T>(T[] array)
        {
            if (array == null || array.Length == 0)
            {
                return null;
            }
            return array;
        }
    }
}
