/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;

namespace pwiz.Topograph.Model.Data
{
    public class PsmTimes : IEnumerable<KeyValuePair<PsmKey, IList<double>>>
    {
        private readonly ImmutableSortedList<PsmKey, IList<double>> _sortedList;

        public PsmTimes(IEnumerable<DbPeptideSpectrumMatch> psms)
        {
            var values = new List<KeyValuePair<PsmKey, IList<double>>>();
            foreach (var grouping in psms.ToLookup(psm => new PsmKey(psm), psm => psm.RetentionTime))
            {
                var times = grouping.Distinct().ToArray();
                if (times.Length == 0)
                {
                    continue;
                }
                Array.Sort(times);
                values.Add(new KeyValuePair<PsmKey, IList<double>>(grouping.Key, times));
            }
            _sortedList = ImmutableSortedList.FromValues(values);
        }
        public IList<double> GetTimes(PsmKey psmKey)
        {
            IList<double> times;
            if (_sortedList.TryGetValue(psmKey, out times))
            {
                return times;
            }
            return new double[0];
        }
        public IList<double> GetTimes(string modifiedSequence)
        {
            int index = CollectionUtil.BinarySearch(_sortedList.Keys, psmKey => psmKey.CompareTo(modifiedSequence), true);
            if (index < 0)
            {
                return new double[0];
            }
            IEnumerable<double> times = _sortedList.Values[index];
            for (index++; index < _sortedList.Count && modifiedSequence == _sortedList[index].Key.ModifiedSequence; index++)
            {
                times = times.Concat(_sortedList.Values[index]);
            }
            var array = times.Distinct().ToArray();
            Array.Sort(array);
            return array;
        }
        public int TotalCount
        {
            get { return _sortedList.Values.Sum(value => value.Count); }
        }

        public double MinTime
        {
            get { return _sortedList.Values.Min(list => list[0]); }
        }
        public double MaxTime
        {
            get { return _sortedList.Values.Max(list => list[list.Count - 1]); }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<KeyValuePair<PsmKey, IList<double>>> GetEnumerator()
        {
            return _sortedList.GetEnumerator();
        }
    }
}
