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
using System.Drawing;
using System.Linq;
using pwiz.Crawdad;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class PeptideChromDataSets
    {
        private const double TIME_DELTA_VARIATION_THRESHOLD = 0.001;
        public const double TIME_MIN_DELTA = 0.2 / 60;

        // No longer necessary, since mzWiff mzXML is the only thing marked
        // as IsProcessedScans
//        private const double TIME_DELTA_MAX_RATIO_THRESHOLD = 25;
//        private const int MINIMUM_DELTAS_PER_CHROM = 4;

        private readonly SrmDocument _document;
        private readonly List<ChromDataSet> _dataSets = new List<ChromDataSet>();
        private readonly List<List<PeptideChromDataPeakList>> _listListPeakSets = new List<List<PeptideChromDataPeakList>>();
        private RetentionTimePrediction _predictedRetentionTime;
        private double[] _retentionTimes;
        private bool _isAlignedTimes;
        private readonly bool _isProcessedScans;

        public PeptideChromDataSets(PeptideDocNode nodePep,
                                    SrmDocument document,
                                    ChromFileInfo fileInfo,
                                    IList<DetailedPeakFeatureCalculator> detailedPeakFeatureCalculators,
                                    bool isProcessedScans)
        {
            NodePep = nodePep;
            FileInfo = fileInfo;
            DetailedPeakFeatureCalculators = detailedPeakFeatureCalculators;
            _document = document;
            _retentionTimes = new double[0];
            _isProcessedScans = isProcessedScans;
        }

        public PeptideDocNode NodePep { get; private set; }

        public ChromFileInfo FileInfo { get; private set; }

        public IList<ChromDataSet> DataSets { get { return _dataSets; } }

        public RetentionTimePrediction PredictedRetentionTime { set { _predictedRetentionTime = value; }}

        public double[] RetentionTimes { set { _retentionTimes = value; } }

        public bool IsAlignedTimes { set { _isAlignedTimes = value; } }

        public ChromKey FirstKey
        {
            get { return DataSets.Count > 0 ? DataSets[0].FirstKey : ChromKey.EMPTY; }
        }

        private IEnumerable<IEnumerable<ChromDataSet>> ComparableDataSets
        {
            get
            {
                yield return from dataSet in DataSets
                       where IsComparable(dataSet)
                       select dataSet;

                foreach (var chromDataSets in from dataSet in DataSets
                                             where !IsComparable(dataSet)
                                             group dataSet by GetSafeLabelType(dataSet))
                {
                    yield return chromDataSets;
                }
            }
        }

        private static bool IsComparable(ChromDataSet dataSet)
        {
            return dataSet.NodeGroup != null && dataSet.NodeGroup.RelativeRT != RelativeRT.Unknown;
        }

        private static IsotopeLabelType GetSafeLabelType(ChromDataSet dataSet)
        {
            return dataSet.NodeGroup != null ? dataSet.NodeGroup.TransitionGroup.LabelType : null;
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

        private IList<DetailedPeakFeatureCalculator> DetailedPeakFeatureCalculators { get; set; }

        public IEnumerable<int> ProviderIds { get { return _dataSets.SelectMany(d => d.ProviderIds); } }

        public bool Load(ChromDataProvider provider)
        {
            //Console.Out.WriteLine("Starting {0} {1} {2}", this.NodePep, _dataSets.Count, RuntimeHelpers.GetHashCode(this));
            foreach (var set in _dataSets.ToArray())
            {
                Color peptideColor = NodePep != null ? NodePep.Color : PeptideDocNode.UNKNOWN_COLOR;
                if (!set.Load(provider, NodePep != null ? NodePep.ModifiedSequence : null, peptideColor))
                    _dataSets.Remove(set);
            }
            //Console.Out.WriteLine("Ending {0} {1} {2}", NodePep, _dataSets.Count, RuntimeHelpers.GetHashCode(this));
            return _dataSets.Count > 0;
        }

        public void PickChromatogramPeaks()
        {
            // Make sure times are evenly spaced before doing any peak detection.
            EvenlySpaceTimes();

            // Pick peak groups at the precursor level
            foreach (var chromDataSet in _dataSets)
                chromDataSet.PickChromatogramPeaks(_retentionTimes, _isAlignedTimes);

            // Merge where possible and pick peak groups at the peptide level
            _listListPeakSets.Clear();
            foreach (var dataSets in ComparableDataSets)
                _listListPeakSets.Add(PickPeptidePeaks(dataSets.ToArray()));

            // Adjust peak dimensions based on peak picking
            foreach (var chromDataSet in _dataSets)
                chromDataSet.GeneratePeakData();

            var detailedCalcs = DetailedPeakFeatureCalculators.Select(calc => (IPeakFeatureCalculator)calc).ToList();
            foreach (var listPeakSets in _listListPeakSets)
            {
                // Score the peaks under the legacy model score
                foreach (var peakSet in listPeakSets.Where(peakSet => peakSet != null))
                {
                    var context = new PeakScoringContext(_document);
                    context.AddInfo(_predictedRetentionTime);
                    peakSet.ScorePeptideSets(context, detailedCalcs);
                }

                SortAndLimitPeaks(listPeakSets);
            }

            // Propagate sorting down to precursor level
            UpdatePrecursorsFromPeptidePeaks();

            // Sort transition group level peaks by retention time and record the best peak
            foreach (var chromDataSet in _dataSets)
                chromDataSet.StorePeaks();
        }

        private void SortAndLimitPeaks(List<PeptideChromDataPeakList> listPeakSets)
        {
            // Sort descending by the peak picking score
            listPeakSets.Sort(ComparePeakLists);

            // Remove peaks contained in higher scoring peaks and limit to max peaks
            int i = 0;
            while (i < listPeakSets.Count)
            {
                var peakSet = listPeakSets[i];
                if (i >= MAX_PEAK_GROUPS || ContainedPeak(peakSet, listPeakSets, i))
                {
                    // Remove peaks from their data sets
                    foreach (var chromDataPeak in peakSet)
                        chromDataPeak.Data.RemovePeak(chromDataPeak.PeakGroup);
                    listPeakSets.RemoveAt(i);
                    continue;
                }
                i++;
            }
        }

        private bool ContainedPeak(IEnumerable<PeptideChromDataPeak> peakSet, List<PeptideChromDataPeakList> listPeakSets, int peakIndex)
        {
            // O(n^2) algorithm, but for never more than 10 items
            var peak = peakSet.First();
            for (int i = 0; i < peakIndex; i++)
            {
                var peakBetter = listPeakSets[i].First();
                // If contained by a better peak, or a better peak contains this peak
                if (peakBetter.IsContained(peak) || peak.IsContained(peakBetter))
                    return true;
            }
            return false;
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
                chromDataSet.PeptideIndexOffset = startSet;
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

        private double EnsureMinDelta(double intervalDelta)
        {
            // Never go smaller than 1/5 a second.
            if (intervalDelta < TIME_MIN_DELTA)
                intervalDelta = TIME_MIN_DELTA;  // For breakpoint setting
            double start, end;
            GetExtents(_isProcessedScans, intervalDelta, out start, out end);
            double pointsMinDelta = (end - start)/ChromGroupHeaderInfo5.MAX_POINTS;
            if (intervalDelta < pointsMinDelta)
                intervalDelta = pointsMinDelta;  // For breakpoint setting
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

        private const int MAX_PEAK_GROUPS = 10;

        private List<PeptideChromDataPeakList> PickPeptidePeaks(ICollection<ChromDataSet> dataSets)
        {
            var listPeakSets = new List<PeptideChromDataPeakList>();

            // Only possible to improve upon individual precursor peak picking,
            // if there are more than one precursor
            if (dataSets.Count == 0)
                return listPeakSets;
            if (dataSets.Count == 1)
            {
                // Create peptide peak group data structures for scoring only
                var dataSet = dataSets.First();
                // Truncate to maximum peak groups, unless this is a summary chromatogram
                // where we want to leave more annotated peaks
                listPeakSets.AddRange(dataSet.PeakSets.Select(p =>
                    new PeptideChromDataPeakList(NodePep, FileInfo, new PeptideChromDataPeak(dataSet, p))));
                return listPeakSets;
            }

            // Merge all the peaks into a single set
            var allPeakGroups = MergePeakGroups(dataSets);
            if (allPeakGroups.Count == 0)
                return listPeakSets;

            // Create coeluting groups
            while (allPeakGroups.Count > 0)
            {
                var peakNext = allPeakGroups[0];
                allPeakGroups.RemoveAt(0);

                var peakSetNext = FindCoelutingPeptidePeaks(dataSets, peakNext, allPeakGroups);
                listPeakSets.Add(peakSetNext);
            }

            // Reset best picked peaks and reintegrate if necessary
            for (int i = 0; i < listPeakSets.Count; i++)
            {
                var peakSet = listPeakSets[i];
                PeptideChromDataPeak peakBest = null, peakNarrowest = null;
                int minLength = int.MaxValue;

                foreach (var peak in peakSet.OrderedPeaks)
                {
                    peak.SetBestPeak(peakBest, i);

                    // The first peak seen is the best peak
                    if (peakBest == null)
                        peakBest = peak;
                    int lenPeak = peak.Length;
                    if (0 < lenPeak && lenPeak < minLength)
                    {
                        peakNarrowest = peak;
                        minLength = lenPeak;
                    }
                }

                // If any peak trunctation occurred in peaks that should be matching
                if (peakNarrowest != null && !ReferenceEquals(peakBest, peakNarrowest))
                {
                    foreach (var peak in peakSet)
                    {
                        if (peak.PeakGroup == null || peak.Length == peakNarrowest.Length)
                            continue;
                        peak.Data.NarrowPeak(peak.PeakGroup, peakNarrowest, i);
                    }
                }
            }

            // Discard peaks not included in a peptide peak list
            foreach (var chromDataSet in dataSets)
                chromDataSet.TruncatePeakSets(listPeakSets.Count);

            return listPeakSets;
        }

        public int ComparePeakLists(PeptideChromDataPeakList p1, PeptideChromDataPeakList p2)
        {
            // TODO: Do we want to keep this?
            // All identified peaks come first
            if (p1.IsIdentified != p2.IsIdentified)
                return p1.IsIdentified ? -1 : 1;

            // Then order by CombinedScore descending
            return Comparer<double>.Default.Compare(p2.CombinedScore, p1.CombinedScore);
        }

        /// <summary>
        /// Propagate the best peaks chosen at the peptide level down to the 
        /// precursor level, in the same order
        /// </summary>
        public void UpdatePrecursorsFromPeptidePeaks()
        {
            foreach (var listPeakSets in _listListPeakSets)
            {
                for (int i = 0; i < listPeakSets.Count; i++)
                {
                    var peakSet = listPeakSets[i];
                    foreach (var peptideChromDataPeak in peakSet)
                    {
                        if (peptideChromDataPeak.PeakGroup != null)
                            peptideChromDataPeak.Data.SetPeakSet(peptideChromDataPeak.PeakGroup, i);
                    }
                }
            }
        }

        /// <summary>
        /// Given a high scoring peak precursor peak group, find other lower scoring precursor peak
        /// groups from different precursors for the same peptide, which overlap in time with the
        /// high scoring peak.
        /// </summary>
        /// <param name="dataSets">Chromatogram data sets from which to create peptide peaks</param>
        /// <param name="dataPeakMax">High scoring precursor peak group</param>
        /// <param name="allPeakGroups">List of all lower scoring precursor peak groups for the same peptide</param>
        /// <returns>A list of coeluting precursor peak groups for this peptide</returns>
        private PeptideChromDataPeakList FindCoelutingPeptidePeaks(IEnumerable<ChromDataSet> dataSets,
                                                                   PeptideChromDataPeak dataPeakMax,
                                                                   IList<PeptideChromDataPeak> allPeakGroups)
        {
            TransitionGroupDocNode nodeGroupMax = dataPeakMax.Data.NodeGroup;
            int startMax = dataPeakMax.StartIndex;
            int endMax = dataPeakMax.EndIndex;
            int timeMax = dataPeakMax.TimeIndex;

            // Initialize the collection of peaks with this peak
            var listPeaks = new PeptideChromDataPeakList(NodePep, FileInfo, dataPeakMax);
            // Enumerate the precursors for this peptide
            foreach (var chromData in dataSets)
            {
                // Skip the precursor for the max peak itself
                if (ReferenceEquals(chromData, dataPeakMax.Data))
                    continue;

                int iPeakBest = -1;
                double bestProduct = 0;

                // Find nearest peak in remaining set that is less than 1/4 length
                // from the primary peak's center
                for (int i = 0, len = allPeakGroups.Count; i < len; i++)
                {
                    var peakGroup = allPeakGroups[i];
                    // Consider only peaks for the current precursor
                    if (!ReferenceEquals(peakGroup.Data, chromData))
                        continue;

                    // Exclude peaks that do not overlap with the maximum peak
                    TransitionGroupDocNode nodeGroup = peakGroup.Data.NodeGroup;
                    int startPeak = peakGroup.StartIndex;
                    int endPeak = peakGroup.EndIndex;
                    if (Math.Min(endPeak, endMax) - Math.Max(startPeak, startMax) <= 0)
                        continue;

                    int timeIndex = peakGroup.TimeIndex;
                    if ((nodeGroup.RelativeRT == RelativeRT.Matching && nodeGroupMax.RelativeRT == RelativeRT.Matching) ||
                        // Matching label types (i.e. different charge states of same label type should always match)
                        ReferenceEquals(nodeGroup.TransitionGroup.LabelType, nodeGroupMax.TransitionGroup.LabelType))
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

                    // Choose the next best peak that overlaps
                    if (peakGroup.PeakGroup.CombinedScore > bestProduct)
                    {
                        iPeakBest = i;
                        bestProduct = peakGroup.PeakGroup.CombinedScore;
                    }
                }

                // If no coeluting peaks found, add an empty peak group
                if (iPeakBest == -1)
                    listPeaks.Add(new PeptideChromDataPeak(chromData, null));
                // Otherwise, add the found coeluting peak group, and remove it from the full list
                else
                {
                    listPeaks.Add(new PeptideChromDataPeak(chromData, allPeakGroups[iPeakBest].PeakGroup));
                    allPeakGroups.RemoveAt(iPeakBest);
                }
            }
            return listPeaks;
        }

        /// <summary>
        /// Merge the sorted lists of precursor peak groups into a single sorted list
        /// </summary>
        /// <param name="dataSets"></param>
        private IList<PeptideChromDataPeak> MergePeakGroups(IEnumerable<ChromDataSet> dataSets)
        {
            var allPeaks = new List<PeptideChromDataPeak>();
            var listUnmerged = new List<ChromDataSet>(dataSets);
            var listEnumerators = listUnmerged.Select(dataSet => dataSet.PeakSets.GetEnumerator()).ToList();

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
                double maxScore = 0;
                int iMaxEnumerator = -1;

                // Check each enumerator for the next highest peak score
                for (int i = 0; i < listEnumerators.Count; i++)
                {
                    var dataPeakList = listEnumerators[i].Current;
                    if (dataPeakList == null)
                        throw new InvalidOperationException(Resources.PeptideChromDataSets_MergePeakGroups_Unexpected_null_peak_list);
                    double score = dataPeakList.CombinedScore;
                    if (score > maxScore)
                    {
                        maxScore = score;
                        iMaxEnumerator = i;
                    }
                }

                // If no peaks left, stop looping.
                if (iMaxEnumerator == -1)
                    break;

                var maxData = listUnmerged[iMaxEnumerator];
                var maxEnumerator = listEnumerators[iMaxEnumerator];
                var maxPeak = maxEnumerator.Current;
                Assume.IsNotNull(maxPeak);

                allPeaks.Add(new PeptideChromDataPeak(maxData, maxPeak));
                if (!maxEnumerator.MoveNext())
                {
                    listEnumerators.RemoveAt(iMaxEnumerator);
                    listUnmerged.RemoveAt(iMaxEnumerator);
                }
            }
            return allPeaks;
        }

        public void Add(PeptideDocNode nodePep, ChromDataSet chromDataSet)
        {
            // If this is coming from the same PeptideDocNode, then just add it, otherwise
            // a merged copy of the PeptideDocNode needs to be created that includes
            // this new precursor.
            if (ReferenceEquals(nodePep, NodePep))
            {
                if (!FindAndMerge(chromDataSet))
                {
                    // Not necessary to update children, because found in peptide
                    AddDataSet(chromDataSet);
                }
                else
                {
                    // Merging causes a change in the children
                    UpdatePepChildren();
                }
            }
            // Unless we already have one of these
            else if (!HasEquivalentGroupNode(chromDataSet.NodeGroup))
            {
                if (!FindAndMerge(chromDataSet))
                    AddDataSet(chromDataSet);
                // Change children no matter what, since this was not in the peptide
                UpdatePepChildren();
            }
            // Important not to lose iRT type
            if (nodePep.GlobalStandardType == PeptideDocNode.STANDARD_TYPE_IRT &&
                NodePep.GlobalStandardType != PeptideDocNode.STANDARD_TYPE_IRT)
            {
                NodePep = NodePep.ChangeStandardType(PeptideDocNode.STANDARD_TYPE_IRT);
            }
        }

        private void UpdatePepChildren()
        {
            var childrenNew = DataSets.Select(d => d.NodeGroup).ToArray();
            NodePep = (PeptideDocNode)NodePep.ChangeChildrenChecked(childrenNew);
        }

        private void AddDataSet(ChromDataSet chromDataSet)
        {
            Assume.IsTrue(DataSets.Count == 0 || DataSets[0].FirstKey.OptionalMaxTime == chromDataSet.FirstKey.OptionalMaxTime);
            DataSets.Add(chromDataSet);
        }

        private bool FindAndMerge(ChromDataSet chromDataSet)
        {
            for (int i = 0; i < DataSets.Count; i++)
            {
                var nodeGroup = DataSets[i].NodeGroup;
                if (AreEquivalentGroups(nodeGroup, chromDataSet.NodeGroup))
                {
                    DataSets[i].NodeGroup = nodeGroup.Merge(chromDataSet.NodeGroup);
                    DataSets[i].Merge(chromDataSet);
                    return true;
                }
            }
            return false;
        }

        private bool HasEquivalentGroupNode(TransitionGroupDocNode nodeGroup)
        {
            return DataSets.Any(d => AreEquivalentGroupNodes(d.NodeGroup, nodeGroup));
        }

        private static bool AreEquivalentGroupNodes(TransitionGroupDocNode nodeGroup1, TransitionGroupDocNode nodeGroup2)
        {
            return AreEquivalentGroups(nodeGroup1, nodeGroup2) && nodeGroup1.EquivalentChildren(nodeGroup2);
        }

        private static bool AreEquivalentGroups(TransitionGroupDocNode nodeGroup1, TransitionGroupDocNode nodeGroup2)
        {
            if (nodeGroup1 == null && nodeGroup2 == null)
                return true;
            if (nodeGroup1 == null || nodeGroup2 == null)
                return false;
            return nodeGroup1.TransitionGroup.PrecursorCharge == nodeGroup2.TransitionGroup.PrecursorCharge &&
                   ReferenceEquals(nodeGroup1.TransitionGroup.LabelType, nodeGroup2.TransitionGroup.LabelType);
        }
    }

    /// <summary>
    /// A single set of peaks for all transitions in a transition group, and
    /// a pointer to its entire peak set.
    /// </summary>
    internal sealed class PeptideChromDataPeak : ITransitionGroupPeakData<IDetailedPeakData>
    {
        private ChromDataPeakList _peakGroup;

        public PeptideChromDataPeak(ChromDataSet data, ChromDataPeakList peakGroup)
        {
            Data = data;
            PeakGroup = peakGroup;
        }

        public TransitionGroupDocNode NodeGroup { get { return Data.NodeGroup; } }

        public bool IsStandard { get { return Data.IsStandard; } }

        public IList<ITransitionPeakData<IDetailedPeakData>> TransitionPeakData
        {
            get { return PeakGroup ?? ChromDataPeakList.EMPTY; }
        }

        public IList<ITransitionPeakData<IDetailedPeakData>> Ms1TranstionPeakData { get; private set; }
        public IList<ITransitionPeakData<IDetailedPeakData>> Ms2TranstionPeakData { get; private set; }
        public IList<ITransitionPeakData<IDetailedPeakData>> DefaultTranstionPeakData
        {
            get { return Ms2TranstionPeakData.Count > 0 ? Ms2TranstionPeakData : Ms1TranstionPeakData; }
        }

        /// <summary>
        /// Entire peak set
        /// </summary>
        public ChromDataSet Data { get; private set; }

        /// <summary>
        /// Single peak group
        /// </summary>
        public ChromDataPeakList PeakGroup
        {
            get { return _peakGroup; }
            set
            {
                _peakGroup = value;
                if (_peakGroup == null)
                {
                    Ms1TranstionPeakData = ChromDataPeakList.EMPTY;
                    Ms2TranstionPeakData = ChromDataPeakList.EMPTY;
                }
                else
                {
                    Ms1TranstionPeakData = TransitionPeakData.Where(t => t.NodeTran != null && t.NodeTran.IsMs1).ToArray();
                    Ms2TranstionPeakData = TransitionPeakData.Where(t => t.NodeTran != null && !t.NodeTran.IsMs1).ToArray();
                }
            }
        }

        /// <summary>
        /// Set this peak based on another best peak for a peak group
        /// </summary>
        public void SetBestPeak(PeptideChromDataPeak peakBest, int indexSet)
        {
            PeakGroup = Data.SetBestPeak(PeakGroup, peakBest, indexSet);
        }

        private CrawdadPeak BestCrawPeak { get { return PeakGroup != null ? PeakGroup[0].Peak : null; }}

        /// <summary>
        /// Peptide normalized start index of best peak
        /// </summary>
        public int StartIndex { get { return BestCrawPeak != null ? BestCrawPeak.StartIndex + Data.PeptideIndexOffset : -1; } }

        /// <summary>
        /// Peptide normalized end index of best peak
        /// </summary>
        public int EndIndex { get { return BestCrawPeak != null ? BestCrawPeak.EndIndex + Data.PeptideIndexOffset : -1; } }

        /// <summary>
        /// Peptide normalized peak apex index of best peak
        /// </summary>
        public int TimeIndex { get { return BestCrawPeak != null ? BestCrawPeak.TimeIndex + Data.PeptideIndexOffset : -1; } }

        /// <summary>
        /// Length of the peptide peak (EndIndex - StartIndex + 1)
        /// </summary>
        public int Length { get { return BestCrawPeak != null ? BestCrawPeak.Length : 0; } }

        /// <summary>
        /// True if the peak begins at the first available time in the chromatogram
        /// </summary>
        public bool IsLeftBound { get { return PeakGroup != null && PeakGroup[0].IsLeftBound; } }

        /// <summary>
        /// True if the peak ends at the last available time in the chromatogram
        /// </summary>
        public bool IsRightBound { get { return PeakGroup != null && PeakGroup[0].IsRightBound; } }

        /// <summary>
        /// Returns true if the apex for the given peak is contained in this peak
        /// </summary>
        public bool IsContained(PeptideChromDataPeak peak)
        {
            return StartIndex <= peak.TimeIndex && peak.TimeIndex <= EndIndex;
        }
    }

    /// <summary>
    /// A single set of peaks for all transition groups in a peptide
    /// </summary>
    internal sealed class PeptideChromDataPeakList : Collection<PeptideChromDataPeak>, IPeptidePeakData<IDetailedPeakData>
    {
        public PeptideChromDataPeakList(PeptideDocNode nodePep, ChromFileInfo fileInfo, PeptideChromDataPeak peak)
        {
            NodePep = nodePep;
            FileInfo = fileInfo;
            TransitionGroupPeakData = new List<ITransitionGroupPeakData<IDetailedPeakData>>();
            AnalyteGroupPeakData = new List<ITransitionGroupPeakData<IDetailedPeakData>>();
            StandardGroupPeakData = new List<ITransitionGroupPeakData<IDetailedPeakData>>();
            ScoringModel = LegacyScoringModel.DEFAULT_MODEL;

            Add(peak);
        }

        public PeptideDocNode NodePep { get; private set; }

        public ChromFileInfo FileInfo { get; private set; }

        public IPeakScoringModel ScoringModel { get; private set; }

        public IList<ITransitionGroupPeakData<IDetailedPeakData>> TransitionGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<IDetailedPeakData>> AnalyteGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<IDetailedPeakData>> StandardGroupPeakData { get; private set; }

        public IList<ITransitionGroupPeakData<IDetailedPeakData>> BestAvailableGroupPeakData
        {
            get { return StandardGroupPeakData.Count > 0 ? StandardGroupPeakData : AnalyteGroupPeakData; }
        }

        private int IdentifiedCount { get; set; }

        public double CombinedScore { get; private set; }

        public bool IsIdentified { get { return IdentifiedCount > 0; } }

        public IEnumerable<PeptideChromDataPeak> OrderedPeaks
        {
            get
            {
                return from peak in this
                       orderby peak.PeakGroup != null ? peak.PeakGroup.CombinedScore : 0 descending
                       select peak;
            }
        }

        public void ScorePeptideSets(PeakScoringContext context, IList<IPeakFeatureCalculator> detailFeatureCalculators)
        {
            var modelCalcs = ScoringModel.PeakFeatureCalculators;
            var detailFeatures = new float [detailFeatureCalculators.Count];
            var modelFeatures = new float [modelCalcs.Count];
            // Here we score both the detailFeatureCalculators (for storage) 
            // and the peak calculators of the legacy model (for import-stage peak scoring)
            // This will cause some scores to be calculated multiple times, but score
            // caching should make this fast.
            var allFeatureCalculators = detailFeatureCalculators.Union(modelCalcs);
            // Calculate summary data once for all scores
            var summaryData = new PeptidePeakDataConverter<IDetailedPeakData>(this);
            foreach (var calc in allFeatureCalculators)
            {
                float feature;
                var summaryCalc = calc as SummaryPeakFeatureCalculator;
                if (summaryCalc != null)
                {
                    feature = summaryCalc.Calculate(context, summaryData);
                }
                else
                {
                    feature = calc.Calculate(context, this);
                }
                int detailIndex = detailFeatureCalculators.IndexOf(calc);
                if (detailIndex != -1)
                {
                    detailFeatures[detailIndex] = feature;
                }
                int modelIndex = modelCalcs.IndexOf(calc);
                if (modelIndex != -1)
                {
                    modelFeatures[modelIndex] = double.IsNaN(feature) ? 0 : feature;
                }
            }
            CombinedScore = ScoringModel.Score(modelFeatures);
            foreach (var peak in this.Where(peak => peak.PeakGroup != null))
            {
                peak.PeakGroup.DetailScores = detailFeatures;
            }
        }

        private void AddPeak(PeptideChromDataPeak dataPeak)
        {
            if (dataPeak.PeakGroup != null && dataPeak.PeakGroup.IsIdentified)
                IdentifiedCount++;
        }

        private void SubtractPeak(PeptideChromDataPeak dataPeak)
        {
            if (dataPeak.PeakGroup != null && dataPeak.PeakGroup.IsIdentified)
                IdentifiedCount--;
        }

        protected override void ClearItems()
        {
            CombinedScore = 0;
            IdentifiedCount = 0;
            TransitionGroupPeakData.Clear();
            AnalyteGroupPeakData.Clear();
            StandardGroupPeakData.Clear();
            base.ClearItems();
        }

        protected override void InsertItem(int index, PeptideChromDataPeak item)
        {
            AddPeak(item);

            GetChangeList(item).Add(item);

            TransitionGroupPeakData.Insert(index, item);

            base.InsertItem(index, item);
        }

        protected override void RemoveItem(int index)
        {
            SubtractPeak(this[index]);

            var tranGroupPeakData = TransitionGroupPeakData[index];
            GetChangeList(tranGroupPeakData).Remove(tranGroupPeakData);

            TransitionGroupPeakData.RemoveAt(index);
            base.RemoveItem(index);
        }

        protected override void SetItem(int index, PeptideChromDataPeak item)
        {
            SubtractPeak(this[index]);
            AddPeak(item);

            var tranGroupPeakData = TransitionGroupPeakData[index];
            GetChangeList(tranGroupPeakData).Remove(tranGroupPeakData);
            GetChangeList(item).Add(item);

            TransitionGroupPeakData[index] = item;
            base.SetItem(index, item);
        }

        private IList<ITransitionGroupPeakData<IDetailedPeakData>> GetChangeList(ITransitionGroupPeakData<IDetailedPeakData> peakData)
        {
            return peakData.IsStandard ? StandardGroupPeakData : AnalyteGroupPeakData;
        }
    }
}
