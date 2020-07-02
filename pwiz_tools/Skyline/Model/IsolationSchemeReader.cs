/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    class IsolationSchemeReader
    {
        private readonly MsDataFileUri[] _dataSources;

        public IsolationSchemeReader(MsDataFileUri[] dataSources)
        {
            _dataSources = dataSources;
        }

        public IsolationScheme Import(string name, IProgressMonitor progressMonitor)
        {
            var isolationRanges = ReadIsolationRangesFromFiles(progressMonitor);
            var isolationWindows = isolationRanges.Select((r, i) => new IsolationWindow(r.Start, r.End, null, CalculateMargin(isolationRanges, i))).ToArray();
            var specialHandling = HasOverlapMultiplexing(isolationRanges)
                ? IsolationScheme.SpecialHandlingType.OVERLAP_MULTIPLEXED
                : IsolationScheme.SpecialHandlingType.NONE;
            return new IsolationScheme(name, isolationWindows, specialHandling);
        }

        private IList<IsolationRange> ReadIsolationRangesFromFiles(IProgressMonitor progressMonitor)
        {
            var isolationRangesResult = new IsolationRange[0];
            IProgressStatus status = new ProgressStatus();

            for (int i = 0; i < _dataSources.Length; i++)
            {
                var dataSource = _dataSources[i];
                progressMonitor.UpdateProgress(status = status.ChangeMessage(string.Format(Resources.IsolationSchemeReader_ReadIsolationRangesFromFiles_Reading_isolation_scheme_from__0_, dataSource)).ChangePercentComplete(i*100/_dataSources.Length));

                isolationRangesResult = ReadIsolationRanges(dataSource, isolationRangesResult);
            }

            progressMonitor.UpdateProgress(status.Complete());

            return isolationRangesResult;
        }

        private bool HasOverlapMultiplexing(IList<IsolationRange> isolationRanges)
        {
            // 4 is the absolute minimum possible number of ranges for overlap demux
            if (isolationRanges.Count < 4)
                return false;

            int halfCount = isolationRanges.Count / 2;
            int overlapCount = 0;
            foreach (var range in isolationRanges.Skip(halfCount))
            {
                if (HasOverlappingRanges(range, isolationRanges.Take(halfCount)))
                    overlapCount++;
            }
            // If at least 1/4 of the ranges overlap in a demultiplexible way, then
            // guess that overlap multiplexing was attempted. Ideally 1/2 would overlap
            // with the other half, but mistakes have been seen, and they are much harder
            // to figure out, if graphed without overlap.
            return overlapCount > halfCount / 2;
        }

        private bool HasOverlappingRanges(IsolationRange range, IEnumerable<IsolationRange> rangesFirstHalf)
        {
            bool seenLesser = false, seenGreater = false, leftOverlap = false, rightOverlap = false;
            double middleOfRange = (range.Start + range.End) / 2;
            foreach (var rangeCheck in rangesFirstHalf)
            {
                if (rangeCheck.Start < range.Start)
                {
                    seenLesser = true;
                    if (Math.Abs(middleOfRange - rangeCheck.End) <= 0.1)
                        leftOverlap = true;
                }
                if (rangeCheck.End > range.End)
                {
                    seenGreater = true;
                    if (Math.Abs(middleOfRange - rangeCheck.Start) <= 0.1)
                        rightOverlap = true;
                }
            }
            return (leftOverlap && rightOverlap) ||
                   (leftOverlap && !seenGreater) ||
                   (rightOverlap && !seenLesser);
        }

        private double? CalculateMargin(IList<IsolationRange> isolationRanges, int i)
        {
            if (isolationRanges.Count < 2)
                return null;
            double? overlapLeft = null;
            if (i > 0)
                overlapLeft = isolationRanges[i - 1].End - isolationRanges[i].Start;
            double? overlapRight = null;
            if (i < isolationRanges.Count - 1)
                overlapRight = isolationRanges[i].End - isolationRanges[i + 1].Start;
            double overlap;
            if (!overlapLeft.HasValue || !overlapRight.HasValue)
                overlap = overlapLeft ?? overlapRight ?? 0;
            else if (Math.Abs(overlapLeft.Value - overlapRight.Value) > 0.1)
            {
                return null;
            }
            else
            {
                overlap = overlapLeft.Value;
            }
            if (Math.Round(overlap) < 1)
                return null;
            return Math.Round(overlap / 2, 4);
        }

        private const int MAX_SPECTRA_PER_CYCLE = 200; // SCIEX has used 100 and Thermo MSX can use 20 * 5
        private const int MAX_MULTI_CYCLE = MAX_SPECTRA_PER_CYCLE * 3;

        private IsolationRange[] ReadIsolationRanges(MsDataFileUri dataSource, IsolationRange[] isolationRanges)
        {
            var dictRangeCounts = isolationRanges.ToDictionary(r => r, r => 0);
            var listRanges = new List<IsolationRange>(isolationRanges);
            double minStart = double.MaxValue, maxStart = double.MinValue;

            string path = dataSource.GetFilePath();
            bool isPasef = Equals(DataSourceUtil.GetSourceType(new DirectoryInfo(path)), DataSourceUtil.TYPE_BRUKER);
            using (var dataFile = new MsDataFileImpl(path, simAsSpectra: true))
            {
                int lookAheadCount = Math.Min(MAX_MULTI_CYCLE, dataFile.SpectrumCount);
                for (int i = 0; i < lookAheadCount; i++)
                {
                    if (dataFile.GetMsLevel(i) != 2)
                        continue;

                    var spectrum = dataFile.GetSpectrum(i);
                    isPasef = isPasef && spectrum.IonMobilities != null;
                    foreach (var precursor in spectrum.Precursors)
                    {
                        if (!precursor.IsolationWindowLower.HasValue || !precursor.IsolationWindowUpper.HasValue)
                            throw new IOException(string.Format(Resources.EditIsolationSchemeDlg_ReadIsolationRanges_Missing_isolation_range_for_the_isolation_target__0__m_z_in_the_file__1_, precursor.IsolationWindowTargetMz, dataSource));
                        double start = precursor.IsolationWindowTargetMz.Value - precursor.IsolationWindowLower.Value;
                        double end = precursor.IsolationWindowTargetMz.Value + precursor.IsolationWindowUpper.Value;
                        var range = new IsolationRange(start, end);
                        int count;
                        if (!dictRangeCounts.TryGetValue(range, out count))
                        {
                            count = 0;
                            dictRangeCounts.Add(range, count);
                            listRanges.Add(range);
                        }
                        if (count == 2)
                        {
                            // Repeating for the third time
                            i = lookAheadCount;
                            break;
                        }
                        dictRangeCounts[range] = count + 1;
                        minStart = Math.Min(minStart, range.Start);
                        maxStart = Math.Max(maxStart, range.Start);
                    }
                }
            }
            if (dictRangeCounts.Values.Any(c => c == 1))
            {
                if (dictRangeCounts.Count > 2)
                {
                    // Sometime demux of overlapping schemes leaves wings that repeat only every other cycle
                    RemoveRangeSingleton(minStart, dictRangeCounts, listRanges);
                    RemoveRangeSingleton(maxStart, dictRangeCounts, listRanges);
                }

                if (dictRangeCounts.Values.Any(c => c == 1))
                    throw new IOException(string.Format(Resources.EditIsolationSchemeDlg_ReadIsolationRanges_No_repeating_isolation_scheme_found_in__0_, dataSource));
            }
            // diaPASEF comes in out of order and will be misinterpreted unless ordered
            // Multiplexing, however, requires that the acquired order by maintained
            if (isPasef)
                listRanges.Sort((r1, r2) => r1.Start.CompareTo(r2.Start));
            return listRanges.ToArray();
        }

        private static void RemoveRangeSingleton(double rangeStart, Dictionary<IsolationRange, int> dictRangeCounts, List<IsolationRange> listRanges)
        {
            var rangeMin = dictRangeCounts.First(p => p.Key.Start == rangeStart).Key;
            if (dictRangeCounts[rangeMin] == 1)
            {
                dictRangeCounts.Remove(rangeMin);
                listRanges.Remove(rangeMin);
            }
        }

        private class IsolationRange
        {
            public IsolationRange(double start, double end)
            {
                Start = start;
                End = end;
            }

            public double Start { get; private set; }
            public double End { get; private set; }

            #region object overrides

            protected bool Equals(IsolationRange other)
            {
                return Start.Equals(other.Start) && End.Equals(other.End);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((IsolationRange)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Start.GetHashCode() * 397) ^ End.GetHashCode();
                }
            }

            #endregion
        }
    }
}
