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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Model
{
    public class Chromatograms : EntityModelCollection<DbChromatogramSet, MzKey, DbChromatogram, ChromatogramData>
    {
        private double[] _times;
        private int[] _scanIndexes;
        public Chromatograms(PeptideFileAnalysis peptideFileAnalysis, DbChromatogramSet dbChromatogramSet) 
            : base(peptideFileAnalysis.Workspace, dbChromatogramSet)
        {
            Parent = peptideFileAnalysis;
        }

        public Chromatograms(PeptideFileAnalysis peptideFileAnalysis, double[] times, int[] scanIndexes) : base(peptideFileAnalysis.Workspace)
        {
            Parent = peptideFileAnalysis;
            _times = times;
            _scanIndexes = scanIndexes;
        }

        protected override void Load(DbChromatogramSet parent)
        {
            base.Load(parent);
            _times = parent.Times;
            _scanIndexes = parent.ScanIndexes;
        }

        public PeptideFileAnalysis PeptideFileAnalysis { get { return (PeptideFileAnalysis) Parent; } }
        protected override IEnumerable<KeyValuePair<MzKey, DbChromatogram>> GetChildren(DbChromatogramSet parent)
        {
            foreach (var dbChromatogram in parent.Chromatograms)
            {
                yield return new KeyValuePair<MzKey, DbChromatogram>(dbChromatogram.MzKey, dbChromatogram);
            }
        }

        public override ChromatogramData WrapChild(DbChromatogram entity)
        {
            return new ChromatogramData(PeptideFileAnalysis, entity);
        }

        protected override int GetChildCount(DbChromatogramSet parent)
        {
            return parent.ChromatogramCount;
        }

        protected override void SetChildCount(DbChromatogramSet parent, int childCount)
        {
            parent.ChromatogramCount = childCount;
        }

        public IList<ChromatogramData> GetFilteredChromatograms()
        {
            var result = new List<ChromatogramData>();
            foreach (var chromatogram in ListChildren())
            {
                if (PeptideFileAnalysis.ExcludedMzs.IsExcluded(chromatogram.MzKey.MassIndex))
                {
                    continue;
                }
                if (chromatogram.Charge < PeptideFileAnalysis.MinCharge || chromatogram.Charge > PeptideFileAnalysis.MaxCharge)
                {
                    continue;
                }
                result.Add(chromatogram);
            }
            return result;
        }
        public int ScanIndexFromTime(double time)
        {
            if (Times == null || Times.Count == 0)
            {
                return 0;
            }
            int index = IndexFromTime(time);
            return _scanIndexes[index];
        }
        public double TimeFromScanIndex(int scanIndex)
        {
            if (_times == null || _times.Length == 0)
            {
                return 0;
            }
            int index = Array.BinarySearch(_scanIndexes, scanIndex);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, _times.Length - 1);
            return _times[index];
        }
        public int IndexFromScanIndex(int scanIndex)
        {
            int index = Array.BinarySearch(_scanIndexes, scanIndex);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, _scanIndexes.Length - 1);
            return index;
        }
        public int IndexFromTime(double time)
        {
            if (Times == null || Times.Count == 0)
            {
                return 0;
            }
            int index = Array.BinarySearch(_times, time);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, _times.Length - 1);
            return index;
        }
        public IList<double> Times
        {
            get
            {
                return new ReadOnlyCollection<double>(_times ?? new double[0]);
            }
        }
        public IList<int> ScanIndexes { get { return new ReadOnlyCollection<int>(_scanIndexes); } }
        public double[] TimesArray { get { return _times; } }
        public int[] ScanIndexesArray { get { return _scanIndexes; } }
        public double FirstTime { get { return _times[0]; } }
        public double LastTime { get { return _times[_times.Length - 1]; } }
    }
}
