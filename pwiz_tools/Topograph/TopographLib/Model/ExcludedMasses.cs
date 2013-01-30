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
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Model
{
    public class ExcludedMasses : ValueSet<ExcludedMasses, int>
    {
        public bool IsExcluded(int massIndex)
        {
            return Contains(massIndex);
        }
        public ExcludedMasses SetExcluded(int massIndex, bool excluded)
        {
            if (excluded)
            {
                return Except(massIndex);
            }
            return Union(massIndex);
        }
        public byte[] ToByteArray()
        {
            if (Count == 0)
            {
                return new byte[0];
            }
            var excludedArray = new bool[this.Max()];
            for (int i = 0; i < excludedArray.Length; i++)
            {
                excludedArray[i] = Contains(i);
            }
            var bytes = new byte[Buffer.ByteLength(excludedArray)];
            Buffer.BlockCopy(excludedArray, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        public static ExcludedMasses FromByteArray(byte[] bytes)
        {
            if (bytes == null || 0 == bytes.Length)
            {
                return EMPTY;
            }
            var excludedArray = new bool[bytes.Length];
            Buffer.BlockCopy(bytes, 0, excludedArray, 0, bytes.Length);
            var indexes = Enumerable.Range(0, excludedArray.Length)
                .Where(i => excludedArray[i]);
            return OfValues(indexes);
        }
        public bool IsMassExcluded(int massIndex)
        {
            return Contains(massIndex);
        }
    }
}
