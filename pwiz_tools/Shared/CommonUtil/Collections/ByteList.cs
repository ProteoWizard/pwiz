/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace pwiz.Common.Collections
{
    public class ByteList : IReadOnlyList<int>
    {
        private readonly byte[] _bytes;

        public static ByteList FromInts(IEnumerable<int> values)
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
