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
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Topograph.Data;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Model.Data
{
    public class ChromatogramSetData
    {
        public ChromatogramSetData(DbChromatogramSet chromatogramSet, IEnumerable<DbChromatogram> chromatograms)
        {
            Times = ImmutableList.ValueOf(chromatogramSet.Times);
            ScanIndexes = ImmutableList.ValueOf(chromatogramSet.ScanIndexes);
            Chromatograms =
                ImmutableSortedList.FromValues(
                    chromatograms.Select(
                        chromatogram =>
                        new KeyValuePair<MzKey, Chromatogram>(chromatogram.MzKey, new Chromatogram(chromatogram))));
        }
        public ChromatogramSetData(AnalysisChromatograms analysisChromatograms)
        {
            Times = ImmutableList.ValueOf(analysisChromatograms.Times);
            ScanIndexes = ImmutableList.ValueOf(analysisChromatograms.ScanIndexes);
            Chromatograms = ImmutableSortedList.FromValues(analysisChromatograms.Chromatograms.Select(
                chromatogram => 
                    new KeyValuePair<MzKey,Chromatogram>(
                        chromatogram.MzKey,
                        new Chromatogram(chromatogram))));
        }
        public IList<double> Times { get; private set; }
        public int IndexFromTime(double time)
        {
            if (Times.Count == 0)
            {
                return 0;
            }
            int index = CollectionUtil.BinarySearch(Times, time);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, Times.Count - 1);
            return index;
        }
        public int ScanIndexFromTime(double time)
        {
            if (Times == null || Times.Count == 0)
            {
                return 0;
            }
            int index = IndexFromTime(time);
            return ScanIndexes[index];
        }
        public double TimeFromScanIndex(int scanIndex)
        {
            if (Times == null || Times.Count == 0)
            {
                return 0;
            }
            int index = CollectionUtil.BinarySearch(ScanIndexes, scanIndex);
            if (index < 0)
            {
                index = ~index;
            }
            index = Math.Min(index, Times.Count - 1);
            return Times[index];
        }

        public IList<int> ScanIndexes { get; private set; }
        public ImmutableSortedList<MzKey, Chromatogram> Chromatograms { get; private set; }

        public struct Chromatogram
        {
            public Chromatogram(DbChromatogram dbChromatogram)
                : this()
            {
                MzMin = dbChromatogram.MzMin;
                MzMax = dbChromatogram.MzMax;
                ChromatogramPoints = ImmutableList.ValueOf(dbChromatogram.ChromatogramPoints);
            }
            public Chromatogram(ChromatogramGenerator.Chromatogram chromatogram) : this()
            {
                MzMin = chromatogram.MzRange.Min;
                MzMax = chromatogram.MzRange.Max;
                ChromatogramPoints = ImmutableList.ValueOf(chromatogram.Points);
            }
            public double MzMin { get; set; }
            public double MzMax { get; set; }
            public IList<ChromatogramPoint> ChromatogramPoints;
        }
    }
}