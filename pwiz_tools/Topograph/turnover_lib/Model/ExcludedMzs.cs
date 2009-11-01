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

namespace pwiz.Topograph.Model
{
    public class ExcludedMzs
    {
        private HashSet<int> _excludedMasses = new HashSet<int>();

        public ExcludedMzs(PeptideAnalysis peptideAnalysis)
        {
            PeptideAnalysis = peptideAnalysis;
        }
        public ExcludedMzs(ExcludedMzs that) : this(that.PeptideAnalysis)
        {
            _excludedMasses = new HashSet<int>(that._excludedMasses);
        }

        public PeptideAnalysis PeptideAnalysis { get; private set; }

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
            var excludedArray =
                new bool[PeptideAnalysis.GetMassCount()];
            for (int i = 0; i < PeptideAnalysis.GetMassCount(); i++)
            {
                excludedArray[i] = _excludedMasses.Contains(i);
            }
            var bytes = new byte[Buffer.ByteLength(excludedArray)];
            Buffer.BlockCopy(excludedArray, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        public void SetByteArray(byte[] bytes)
        {
            var excludedArray = new bool[PeptideAnalysis.GetMassCount()];
            if (bytes.Length != excludedArray.Length)
            {
                return;
            }
            _excludedMasses.Clear();
            Buffer.BlockCopy(bytes, 0, excludedArray, 0, bytes.Length);
            for (int massIndex = 0; massIndex < PeptideAnalysis.GetMassCount(); massIndex++)
            {
                if (excludedArray[massIndex])
                {
                    _excludedMasses.Add(massIndex);
                }
            }
        }
        public bool IsMassExcluded(int massIndex)
        {
            return _excludedMasses.Contains(massIndex);
        }
        public event Action<ExcludedMzs> ChangedEvent;
    }
}
