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
        private Dictionary<int, HashSet<int>> _excludedMzs = new Dictionary<int, HashSet<int>>();

        public ExcludedMzs(PeptideAnalysis peptideAnalysis)
        {
            PeptideAnalysis = peptideAnalysis;
        }
        public ExcludedMzs(ExcludedMzs that) : this(that.PeptideAnalysis)
        {
            foreach (var entry in that._excludedMzs)
            {
                _excludedMzs.Add(entry.Key, new HashSet<int>(entry.Value));
            }
        }

        public PeptideAnalysis PeptideAnalysis { get; private set; }

        public bool IsExcluded(MzKey mzKey)
        {
            HashSet<int> set;
            if (!_excludedMzs.TryGetValue(mzKey.Charge, out set))
            {
                return false;
            }
            return set.Contains(mzKey.MassIndex);
        }
        public void SetExcluded(MzKey mzKey, bool excluded)
        {
            if (excluded == IsExcluded(mzKey))
            {
                return;
            }
            HashSet<int> set;
            if (!_excludedMzs.TryGetValue(mzKey.Charge, out set))
            {
                set = new HashSet<int>();
                _excludedMzs.Add(mzKey.Charge, set);
            }
            if (excluded)
            {
                set.Add(mzKey.MassIndex);
            }
            else
            {
                set.Remove(mzKey.MassIndex);
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
                new bool[PeptideAnalysis.MaxCharge - PeptideAnalysis.MinCharge + 1, PeptideAnalysis.GetMassCount()];
            foreach (var entry in _excludedMzs)
            {
                if (entry.Key < PeptideAnalysis.MinCharge || entry.Key > PeptideAnalysis.MaxCharge)
                {
                    continue;
                }
                for (int i = 0; i < PeptideAnalysis.GetMassCount(); i++)
                {
                    excludedArray[entry.Key - PeptideAnalysis.MinCharge, i] = entry.Value.Contains(i);
                }
            }
            var bytes = new byte[Buffer.ByteLength(excludedArray)];
            Buffer.BlockCopy(excludedArray, 0, bytes, 0, bytes.Length);
            return bytes;
        }
        public void SetByteArray(byte[] bytes)
        {
            var excludedArray =
                new bool[PeptideAnalysis.MaxCharge - PeptideAnalysis.MinCharge + 1, PeptideAnalysis.GetMassCount()];
            if (bytes.Length != excludedArray.Length)
            {
                return;
            }
            _excludedMzs.Clear();
            Buffer.BlockCopy(bytes, 0, excludedArray, 0, bytes.Length);
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge ++)
            {
                var set = new HashSet<int>();
                _excludedMzs.Add(charge, set);
                for (int massIndex = 0; massIndex < PeptideAnalysis.GetMassCount(); massIndex++)
                {
                    if (excludedArray[charge - PeptideAnalysis.MinCharge, massIndex])
                    {
                        set.Add(massIndex);
                    }
                }
            }
        }
        public bool IsMassExcludedForAnyCharge(int massIndex)
        {
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                if (IsExcluded(new MzKey(charge, massIndex)))
                {
                    return true;
                }
            }
            return false;
        }
        public bool IsMassExcludedForAllCharges(int massIndex)
        {
            for (int charge = PeptideAnalysis.MinCharge; charge <= PeptideAnalysis.MaxCharge; charge++)
            {
                if (!IsExcluded(new MzKey(charge, massIndex)))
                {
                    return false;
                }
            }
            return true;
        }

        public event Action<ExcludedMzs> ChangedEvent;
    }
}
