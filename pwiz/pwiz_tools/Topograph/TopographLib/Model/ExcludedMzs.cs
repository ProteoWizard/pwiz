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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Util;

namespace pwiz.Topograph.Model
{
    public class ExcludedMzs
    {
        private HashSet<int> _excludedMasses = new HashSet<int>();

        public ExcludedMzs()
        {
        }
        public ExcludedMzs(ExcludedMzs that)
        {
            _excludedMasses = new HashSet<int>(that._excludedMasses);
        }

        public bool IsExcluded(int massIndex)
        {
            return _excludedMasses.Contains(massIndex);
        }
        public void SetExcluded(int massIndex, bool excluded)
        {
            if (excluded == IsExcluded(massIndex))
            {
                return;
            }
            if (excluded)
            {
                _excludedMasses.Add(massIndex);
            }
            else
            {
                _excludedMasses.Remove(massIndex);
            }
            var changedEvent = ChangedEvent;
            if (changedEvent != null)
            {
                changedEvent.Invoke(this);
            }
        }
        public byte[] ToByteArray()
        {
            if (_excludedMasses.Count == 0)
            {
                return new byte[0];
            }
            var excludedArray = new bool[_excludedMasses.Max()];
            for (int i = 0; i < excludedArray.Length; i++)
            {
                excludedArray[i] = _excludedMasses.Contains(i);
            }
            var bytes = new byte[Buffer.ByteLength(excludedArray)];
            Buffer.BlockCopy(excludedArray, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        public void SetByteArray(byte[] bytes)
        {
            var excludedArray = new bool[bytes.Length];
            if (Lists.EqualsDeep(bytes, ToByteArray()))
            {
                return;
            }
            _excludedMasses.Clear();
            Buffer.BlockCopy(bytes, 0, excludedArray, 0, bytes.Length);
            for (int massIndex = 0; massIndex < excludedArray.Length; massIndex++)
            {
                if (excludedArray[massIndex])
                {
                    _excludedMasses.Add(massIndex);
                }
            }
            if (ChangedEvent != null)
            {
                ChangedEvent.Invoke(this);
            }
        }
        public bool IsMassExcluded(int massIndex)
        {
            return _excludedMasses.Contains(massIndex);
        }
        public event Action<ExcludedMzs> ChangedEvent;
    }
}
