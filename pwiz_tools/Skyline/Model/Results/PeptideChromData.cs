/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.Linq;
using pwiz.Crawdad;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class PeptideChromDataSets
    {
        private const double TIME_DELTA_VARIATION_THRESHOLD = 0.001;
        // No longer necessary, since mzWiff mzXML is the only thing marked
        // as IsProcessedScans
//        private const double TIME_DELTA_MAX_RATIO_THRESHOLD = 25;
//        private const int MINIMUM_DELTAS_PER_CHROM = 4;

        private readonly List<ChromDataSet> _dataSets = new List<ChromDataSet>();
        private readonly double[] _retentionTimes;
        private readonly bool _isProcessedScans;

        public PeptideChromDataSets(double[] retentionTimes, bool isProcessedScans)
        {
            _retentionTimes = retentionTimes;
            _isProcessedScans = isProcessedScans;
        }

        public PeptideChromDataSets(double[] retentionTimes, bool isProcessedScans, ChromDataSet chromDataSet)
            : this(retentionTimes, isProcessedScans)
        {
            DataSets.Add(chromDataSet);
        }

        public IList<ChromDataSet> DataSets { get { return _dataSets; } }

        private IEnumerable<ChromDataSet> ComparableDataSets
        {
            get
            {
                return from dataSet in DataSets
                       where dataSet.DocNode != null && dataSet.DocNode.RelativeRT != RelativeRT.Unknown
                       select dataSet;
            }
        }

        private IEnumerable<ChromData> ChromDatas
        {
            get
            {
                foreach (var chromDataSet in _dataSets)
                {
                    foreach (var chromData in chromDataSet.Chromatograms)
                        yield return chromData;
                }
            }
        }

        public void Load(ChromDataProvider provider)
        {
            foreach (var set in _dataSets)
                set.Load(provider);
        }

        public void PickChromatogramPeaks()
        {
            // Make sure times are evenly spaced before doing any peak detection.
            EvenlySpaceTimes();

            foreach (var chromDataSet in _dataSets)
                chromDataSet.PickChromatogramPeaks(_retentionTimes);

            PickPeptidePeaks();

            foreach (var chromDataSet in _dataSets)
                chromDataSet.StorePeaks();
        }

        private bool ThermoZerosFix()
        {
            bool fixApplied = false;
            foreach (var chromDataSet in DataSets)
                fixApplied = chromDataSet.ThermoZerosFix() || fixApplied;
            return fixApplied;
        }

        // Moved to ProteoWizard
        // ReSharper disable UnusedMember.Local
        private bool WiffZerosFix()
        // ReSharper restore UnusedMember.Local
        {
            bool fixApplied = false;
            foreach (var chromDataSet in DataSets)
                fixApplied = chromDataSet.WiffZerosFix() || fixApplied;
            return fixApplied;
        }

        private void EvenlySpaceTimes()
        {
            // Handle an issue where the ProteoWizard Reader_Thermo returns chromatograms
            // with alternating zero intensity scans with real data
            if (ThermoZerosFix())
            {
                EvenlySpaceTimes();
                return;
            }
            // Moved to ProteoWizard
            //                else if (WiffZerosFix())
            //                {
            //                    EvenlySpaceTimes();
            //                    return;
            //                }

            // Accumulate time deltas looking for variation that violates our ability
            // to do valid peak detection with Crawdad.
            bool foundVariation = false;

            List<double> listDeltas = new List<double>();
            List<double> listMaxDeltas = new List<double>();
            double maxIntensity = 0;
            float[] firstTimes = null;
            double expectedTimeDelta = 0;
            //                int countChromData = 0;
            foreach (var chromData in ChromDatas)
            {
                //                    countChromData++;
                if (firstTimes == null)
                {
                    firstTimes = chromData.Times;
                    if (firstTimes.Length == 0)
                        continue;
                    expectedTimeDelta = (firstTimes[firstTimes.Length - 1] - firstTimes[0]) / firstTimes.Length;
                }
                if (firstTimes.Length != chromData.Times.Length)
                    foundVariation = true;

                double lastTime = 0;
                var times = chromData.Times;
                if (times.Length > 0)
                    lastTime = times[0];
                for (int i = 1, len = chromData.Times.Length; i < len; i++)
                {
                    double time = times[i];
                    double delta = time - lastTime;
                    lastTime = time;
                    listDeltas.Add(Math.Round(delta, 4));

                    // Collect the 10 deltas after the maximum peak
                    if (chromData.Intensities[i] > maxIntensity)
                    {
                        maxIntensity = chromData.Intensities[i];
                        listMaxDeltas.Clear();
                        listMaxDeltas.Add(delta);
                    }
                    else if (0 < listMaxDeltas.Count && listMaxDeltas.Count < 10)
                    {
                        listMaxDeltas.Add(delta);
                    }

                    if (!foundVariation && (time != firstTimes[i] ||
                                            Math.Abs(delta - expectedTimeDelta) > TIME_DELTA_VARIATION_THRESHOLD))
                    {
                        foundVariation = true;
                    }
                }
            }

            // If time deltas are sufficiently evenly spaced, then no further processing
            // is necessary.
            if (!foundVariation && listDeltas.Count > 0)
                return;

            // Interpolate the existing points onto time intervals evently spaced
            // by the minimum interval observed in the measuered data.
            double intervalDelta = 0;
            var statDeltas = new Statistics(listDeltas);
            if (statDeltas.Length > 0)
            {
                double[] bestDeltas = statDeltas.Modes();
                if (bestDeltas.Length == 0 || bestDeltas.Length > listDeltas.Count / 2)
                    intervalDelta = statDeltas.Min();
                else if (bestDeltas.Length == 1)
                    intervalDelta = bestDeltas[0];
                else
                {
                    var statIntervals = new Statistics(bestDeltas);
                    intervalDelta = statIntervals.Min();
                }
            }

            intervalDelta = EnsureMinDelta(intervalDelta);

            bool inferZeros = false;
            if (_isProcessedScans)  // only mzWiff mzXML has this set now
//             if (_isProcessedScans &&
//                 (statDeltas.Length < countChromData * MINIMUM_DELTAS_PER_CHROM ||
//                  statDeltas.Max() / intervalDelta > TIME_DELTA_MAX_RATIO_THRESHOLD))
            {
                inferZeros = true; // Verbose expression for easy breakpoint placement

                // Try really hard to use a delta that will work for the maximum peak
                intervalDelta = EnsureMinDelta(GetIntervalMaxDelta(listMaxDeltas, intervalDelta));
            }

            // Create a master set of time intervals that all points for
            // this peptide will be mapped onto.
            double start, end;
            GetExtents(inferZeros, intervalDelta, out start, out end);

            var listTimesNew = new List<float>();
            for (double t = start; t <= end; t += intervalDelta)
                listTimesNew.Add((float)t);
            float[] timesNew = listTimesNew.ToArray();

            // Perform interpolation onto the new times
            foreach (var chromDataSet in DataSets)
            {
                // Determine what segment of the new time intervals array covers this precursor
                int startSet, endSet;
                chromDataSet.GetExtents(inferZeros, intervalDelta, timesNew, out startSet, out endSet);

                float[] timesNewPrecursor = timesNew;
                int countTimes = endSet - startSet + 1;  // +1 because endSet is inclusive
                if (countTimes != timesNewPrecursor.Length)
                {
                    // Copy the segment into a new array for this precursor only
                    timesNewPrecursor = new float[countTimes];
                    Array.Copy(timesNew, startSet, timesNewPrecursor, 0, countTimes);
                }

                foreach (var chromData in chromDataSet.Chromatograms)
                {
                    chromData.Interpolate(timesNewPrecursor, intervalDelta, inferZeros);
                }
                chromDataSet.Offset = startSet;
            }
        }

        /// <summary>
        /// Gets extents that can contain all of the precursor sets.
        /// </summary>
        private void GetExtents(bool inferZeros, double intervalDelta, out double start, out double end)
        {
            start = double.MaxValue;
            end = double.MinValue;
            foreach (var chromDataSet in DataSets)
            {
                double startSet, endSet;
                chromDataSet.GetExtents(inferZeros, intervalDelta, out startSet, out endSet);

                start = Math.Min(start, startSet);
                end = Math.Max(end, endSet);
            }
        }

        private static double EnsureMinDelta(double intervalDelta)
        {
            // Never go smaller than 1/5 a second.
            if (intervalDelta < 0.2 / 60)
                intervalDelta = 0.2 / 60;  // For breakpoint setting
            return intervalDelta;
        }

        private static double GetIntervalMaxDelta(IList<double> listMaxDeltas, double intervalDelta)
        {
            const int magnitude = 8;    // 8x counted as an order of magnitude difference
            // Allow larger differences, if there are 4 in a row with relatively consistent spacing
            if (listMaxDeltas.Count > 0 && (listMaxDeltas[0] / magnitude < intervalDelta || IsRegular(listMaxDeltas, 4)))
            {
                intervalDelta = listMaxDeltas[0];
                for (int i = 1; i < listMaxDeltas.Count; i++)
                {
                    double delta = listMaxDeltas[i];
                    // If an order of magnitude change in time interval is encountered stop
                    if (intervalDelta / magnitude > delta || delta > intervalDelta * magnitude)
                        break;
                    // Calculate a weighted mean
                    intervalDelta = (intervalDelta * i + delta) / (i + 1);
                }
            }
//            else if (listMaxDeltas.Count > 0 && listMaxDeltas[0] / magnitude > intervalDelta)
//            {
//                Console.WriteLine("Max delta {0} too much larger than {1}", listMaxDeltas[0], intervalDelta);
//            }
            return intervalDelta;
        }

        /// <summary>
        /// Returns true if n values are relatively consistent.
        /// </summary>
        private static bool IsRegular(IList<double> deltas, int n)
        {
            var deltasCompare = new double[n];
            n = Math.Min(n, deltas.Count);
            for (int i = 0; i < n; i++)
                deltasCompare[i] = deltas[i];
            var statDeltas = new Statistics(deltasCompare);
            double cv = statDeltas.StdDev() / statDeltas.Mean();
            if (cv < 0.1)
                return true;
            return false;
        }

        private void PickPeptidePeaks()
        {
            // Only possible to improve upon individual precursor peak picking,
            // if there are more than one precursor
            if (ComparableDataSets.Count() < 2)
                return;

            // Merge all the peaks into a single set
            var allPeakGroups = MergePeakGroups();
            if (allPeakGroups.Count == 0)
                return;

            // Create coeluting groups
            var listPeakSets = new List<PeptideChromDataPeakList>();
            while (allPeakGroups.Count > 0)
            {
                PeptideChromDataPeak peak = allPeakGroups[0];
                allPeakGroups.RemoveAt(0);

                PeptideChromDataPeakList peakSet = FindCoelutingPeptidePeaks(peak, allPeakGroups);

                listPeakSets.Add(peakSet);
            }

            // Sort descending by the peak picking score
            listPeakSets.Sort(ComparePeakLists);

            // Reset best picked peaks and reintegrate if necessary
            var peakSetBest = listPeakSets[0];
            PeptideChromDataPeak peakBest = null;
            foreach (var peak in peakSetBest.OrderedPeaks)
            {
                // Ignore precursors with unknown relative RT. They do not participate
                // in peptide peak matching.
                if (peak.Data.DocNode.RelativeRT == RelativeRT.Unknown)
                    continue;

                peak.Data.SetBestPeak(peak.PeakGroup, peakBest);
                if (peakBest == null)
                    peakBest = peak;
            }
        }

        public int ComparePeakLists(PeptideChromDataPeakList p1, PeptideChromDataPeakList p2)
        {
            // All identified peaks come first
            if (p1.IsIdentified != p2.IsIdentified)
                return p1.IsIdentified ? -1 : 1;

            // Then order by ProductArea descending
            return Comparer<double>.Default.Compare(p2.ProductArea, p1.ProductArea);
        }

        private PeptideChromDataPeakList FindCoelutingPeptidePeaks(PeptideChromDataPeak dataPeakMax, IList<PeptideChromDataPeak> allPeakGroups)
        {
            TransitionGroupDocNode nodeGroupMax = dataPeakMax.Data.DocNode;
            CrawdadPeak peakMax = dataPeakMax.PeakGroup[0].Peak;
            int offset = dataPeakMax.Data.Offset;
            int startMax = peakMax.StartIndex + offset;
            int endMax = peakMax.EndIndex + offset;
            int timeMax = peakMax.TimeIndex + offset;

            var listPeaks = new PeptideChromDataPeakList(dataPeakMax);
            foreach (var chromData in _dataSets)
            {
                if (ReferenceEquals(chromData, dataPeakMax.Data))
                    continue;

                int iPeakBest = -1;
                double bestProduct = 0;

                // Find nearest peak in remaining set that is less than 1/4 length
                // from the primary peak's center
                for (int i = 0, len = allPeakGroups.Count; i < len; i++)
                {
                    var peakGroup = allPeakGroups[i];
                    if (!ReferenceEquals(peakGroup.Data, chromData))
                        continue;

                    // Exclude peaks that do not overlap with the maximum peak
                    TransitionGroupDocNode nodeGroup = peakGroup.Data.DocNode;
                    var peak = peakGroup.PeakGroup[0];
                    offset = peakGroup.Data.Offset;
                    int startPeak = peak.Peak.StartIndex + offset;
                    int endPeak = peak.Peak.EndIndex + offset;
                    if (Math.Min(endPeak, endMax) - Math.Max(startPeak, startMax) <= 0)
                        continue;

                    if (nodeGroup.TransitionGroup.PrecursorCharge == nodeGroupMax.TransitionGroup.PrecursorCharge)
                    {
                        int timeIndex = peak.Peak.TimeIndex + offset;
                        if (nodeGroup.RelativeRT == RelativeRT.Matching && nodeGroupMax.RelativeRT == RelativeRT.Matching)
                        {
                            // If the peaks are supposed to have the same elution time,
                            // then be more strict about how they overlap
                            if (startMax >= timeIndex || timeIndex >= endMax)
                                continue;
                        }
                        else if (nodeGroup.RelativeRT == RelativeRT.Matching && nodeGroupMax.RelativeRT == RelativeRT.Preceding)
                        {
                            // If the maximum is supposed to precede this, look for any
                            // indication that this relationship holds, by testing the peak apex
                            // and the peak center.
                            if (timeIndex < timeMax && (startPeak + endPeak) / 2 < (startMax + endMax) / 2)
                                continue;
                        }
                        else if (nodeGroup.RelativeRT == RelativeRT.Preceding && nodeGroupMax.RelativeRT == RelativeRT.Matching)
                        {
                            // If this peak is supposed to precede the maximum, look for any
                            // indication that this relationship holds, by testing the peak apex
                            // and the peak center.
                            if (timeIndex > timeMax && (startPeak + endPeak) / 2 > (startMax + endMax) / 2)
                                continue;
                        }
                    }

                    // Choose the next best peak that overlaps
                    if (peakGroup.PeakGroup.ProductArea > bestProduct)
                    {
                        iPeakBest = i;
                        bestProduct = peakGroup.PeakGroup.ProductArea;
                    }
                }

                if (iPeakBest == -1)
                    listPeaks.Add(new PeptideChromDataPeak(chromData, null));
                else
                {
                    listPeaks.Add(new PeptideChromDataPeak(chromData, allPeakGroups[iPeakBest].PeakGroup));
                    allPeakGroups.RemoveAt(iPeakBest);
                }
            }
            return listPeaks;
        }

        private IList<PeptideChromDataPeak> MergePeakGroups()
        {
            List<PeptideChromDataPeak> allPeaks = new List<PeptideChromDataPeak>();
            var listEnumerators = ComparableDataSets.ToList().ConvertAll(
                dataSet => dataSet.PeakSets.GetEnumerator());

            // Merge with list of chrom data that will match the enumerators
            // list, as completed enumerators are removed.
            var listUnmerged = new List<ChromDataSet>(_dataSets);
            // Initialize an enumerator for each set of raw peaks, or remove
            // the set, if the list is found to be empty
            for (int i = listEnumerators.Count - 1; i >= 0; i--)
            {
                if (!listEnumerators[i].MoveNext())
                {
                    listEnumerators.RemoveAt(i);
                    listUnmerged.RemoveAt(i);
                }
            }

            while (listEnumerators.Count > 0)
            {
                double maxIntensity = 0;
                int iMaxEnumerator = -1;

                for (int i = 0; i < listEnumerators.Count; i++)
                {
                    var dataPeakList = listEnumerators[i].Current;
                    if (dataPeakList == null)
                        throw new InvalidOperationException("Unexptected null peak list");
                    double intensity = dataPeakList.ProductArea;
                    if (intensity > maxIntensity)
                    {
                        maxIntensity = intensity;
                        iMaxEnumerator = i;
                    }
                }

                // If no peaks left, stop looping.
                if (iMaxEnumerator == -1)
                    break;

                var maxData = listUnmerged[iMaxEnumerator];
                var maxEnumerator = listEnumerators[iMaxEnumerator];
                var maxPeak = maxEnumerator.Current;
                Debug.Assert(maxPeak != null);

                allPeaks.Add(new PeptideChromDataPeak(maxData, maxPeak));
                if (!maxEnumerator.MoveNext())
                {
                    listEnumerators.RemoveAt(iMaxEnumerator);
                    listUnmerged.RemoveAt(iMaxEnumerator);
                }
            }
            return allPeaks;
        }
    }

    internal sealed class PeptideChromDataPeak
    {
        public PeptideChromDataPeak(ChromDataSet data, ChromDataPeakList peakGroup)
        {
            Data = data;
            PeakGroup = peakGroup;
        }

        public ChromDataSet Data { get; private set; }
        public ChromDataPeakList PeakGroup { get; private set; }
    }

    internal sealed class PeptideChromDataPeakList : Collection<PeptideChromDataPeak>
    {
        public PeptideChromDataPeakList(PeptideChromDataPeak peak)
        {
            Add(peak);
        }

        private int PeakCount { get; set; }
        private double TotalArea { get; set; }
        private int IdentifiedCount { get; set; }

        public double ProductArea { get; private set; }

        public bool IsIdentified { get { return IdentifiedCount > 0; } }

        public IEnumerable<IGrouping<int, PeptideChromDataPeak>> ChargeGroups
        {
            get
            {
                return from peak in this
                       orderby peak.PeakGroup != null ? peak.PeakGroup.ProductArea : 0 descending
                       group peak by peak.Data.DocNode.TransitionGroup.PrecursorCharge into g
                       select g;
            }
        }

        public IEnumerable<PeptideChromDataPeak> OrderedPeaks
        {
            get
            {
                return from peak in this
                       orderby peak.PeakGroup != null ? peak.PeakGroup.ProductArea : 0 descending
                       select peak;
            }
        }

        private void AddPeak(PeptideChromDataPeak dataPeak)
        {
            if (dataPeak.PeakGroup != null)
            {
                PeakCount++;

                TotalArea += dataPeak.PeakGroup.ProductArea;

                ProductArea = TotalArea * Math.Pow(10.0, PeakCount);

                if (dataPeak.PeakGroup.IsIdentified)
                    IdentifiedCount++;
            }
        }

        private void SubtractPeak(PeptideChromDataPeak dataPeak)
        {
            if (dataPeak.PeakGroup != null)
            {
                PeakCount--;

                if (PeakCount == 0)
                    TotalArea = 0;
                else
                    TotalArea -= dataPeak.PeakGroup.ProductArea;

                ProductArea = TotalArea * Math.Pow(10.0, PeakCount);

                if (dataPeak.PeakGroup.IsIdentified)
                    IdentifiedCount--;
            }
        }

        protected override void ClearItems()
        {
            PeakCount = 0;
            TotalArea = 0;
            ProductArea = 0;

            base.ClearItems();
        }

        protected override void InsertItem(int index, PeptideChromDataPeak item)
        {
            AddPeak(item);
            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            SubtractPeak(this[index]);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, PeptideChromDataPeak item)
        {
            SubtractPeak(this[index]);
            AddPeak(item);
            base.SetItem(index, item);
        }
    }
}
