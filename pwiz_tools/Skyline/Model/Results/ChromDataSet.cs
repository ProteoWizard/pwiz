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
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Chromatogram data and peak lists for all transitions in a transition group
    /// </summary>
    internal sealed class ChromDataSet
    {
        /// <summary>
        /// List of chromatogram data, one for each transition
        /// </summary>
        private readonly List<ChromData> _listChromData = new List<ChromData>();

        /// <summary>
        /// True if area is time normalized, as it has been since v0.7.  Before that
        /// area was simply based on the number of points across the peak, which could
        /// yield higher areas for for higher sampling rates.
        /// </summary>
        private readonly bool _isTimeNormalArea;

        /// <summary>
        /// List of peak groups, one peak per transition
        /// </summary>
        private List<ChromDataPeakList> _listPeakSets = new List<ChromDataPeakList>();

        public ChromDataSet(bool isTimeNormalArea, FullScanAcquisitionMethod fullScanAcquisitionMethod, ChromData chromData)
            :this(isTimeNormalArea, null, null, fullScanAcquisitionMethod, new []{chromData})
        {
            OverrideTextId = false;
        }

        public ChromDataSet(bool isTimeNormalArea, PeptideDocNode peptideDocNode,
            TransitionGroupDocNode transitionGroupDocNode, FullScanAcquisitionMethod fullScanAcquisitionMethod,
            IEnumerable<ChromData> arrayChromData)
        {
            _isTimeNormalArea = isTimeNormalArea;
            FullScanAcquisitionMethod = fullScanAcquisitionMethod;

            AddRange(arrayChromData);
            if (transitionGroupDocNode != null)
            {
                NodeGroups = ImmutableList.Singleton(Tuple.Create(peptideDocNode, transitionGroupDocNode));
            }
            else
            {
                NodeGroups = ImmutableList<Tuple<PeptideDocNode, TransitionGroupDocNode>>.EMPTY;
            }

            OverrideTextId = true;
        }

        public void ClearDataDocNodes()
        {
            foreach (var chromData in Chromatograms)
                chromData.DocNode = null;
        }

        public ChromData BestChromatogram { get { return _listChromData[0]; } }
//        private ChromData BestNonFragmentChromatogram { get { return _listChromData.FirstOrDefault(t => t.Key.Source != ChromSource.fragment); } }
//        private ChromData BestFragmentChromatogram { get { return _listChromData.FirstOrDefault(t => t.Key.Source == ChromSource.fragment); } }
        public IList<ChromData> Chromatograms { get { return _listChromData; } }

        /// <summary>
        /// The number of transitions or chromatograms associated with this transition group
        /// </summary>
        public int Count { get { return _listChromData.Count; } }

        public int TranCount { get { return _listChromData.Count(d => d.DocNode != null); } }

        public int UniqueTranCount
        {
            get
            {
                return _listChromData.Where(d => d.DocNode != null).Select(d => d.DocNode.Id)
                    .Distinct(new IdentityEqualityComparer<Identity>()).Count();
            }
        }

        public InterpolationParams InterpolationParams { get; set; }

        public void Add(ChromData chromData)
        {
            AddRange(new []{chromData});
        }

        public void Remove(ChromData chromData)
        {
            _listChromData.Remove(chromData);
        }

        public void AddRange(IEnumerable<ChromData> chromDatas)
        {
            foreach (var chromData in chromDatas)
            {
                _listChromData.Add(chromData.CloneForWrite());
            }
        }

        public void RemovePeak(ChromDataPeakList peakGroup)
        {
            _listPeakSets.Remove(peakGroup);
        }

        /// <summary>
        /// Offset applied to transform StartIndex, EndIndex and TimeIndex to peptide
        /// coordinate system shared by all transition groups of a peptide.
        /// </summary>
        public int PeptideIndexOffset { get; set; }

        /// <summary>
        /// True if area is time normalized, as it has been since v0.7.  Before that
        /// area was simply based on the number of points across the peak, which could
        /// yield higher areas for for higher sampling rates.
        /// </summary>
        public bool IsTimeNormalArea { get { return _isTimeNormalArea; } }

        public FullScanAcquisitionMethod FullScanAcquisitionMethod { get; private set; }

        /// <summary>
        /// Enumerates the peak groups associated with this transiton group
        /// </summary>
        public IEnumerable<ChromDataPeakList> PeakSets { get { return _listPeakSets; } }

        /// <summary>
        /// Removes all but the first count peaks.  For use when the peak groups
        /// are sorted by a score.  If fewer there are fewer than the number of peaks
        /// specified, then the peaks are left unchanged.
        /// </summary>
        /// <param name="count">Maximum number of peaks to retain</param>
        public void TruncatePeakSets(int count)
        {
            if (count < _listPeakSets.Count)
                _listPeakSets.RemoveRange(count, _listPeakSets.Count - count);
        }
        public TransitionGroupDocNode NodeGroup
        {
            get { return NodeGroups.FirstOrDefault()?.Item2; }
        }
        public ImmutableList<Tuple<PeptideDocNode, TransitionGroupDocNode>> NodeGroups { get; set; }
        public IEnumerable<IsotopeLabelType> LabelTypes
        {
            get { return NodeGroups.Select(g => g.Item2.LabelType); }
        }

        /// <summary>
        /// True if the transition group is an isotope labeled internal standard
        /// </summary>
        public bool IsStandard { get; set; }

        /// <summary>
        /// Indicates that this ChromDataSet needs a ChromatogramTextId corresponding to
        /// the PeptideDocNode. If this is false, then ChromDataSet will use whatever TextId
        /// is in the ChromKey (which will be blank for SRM files, or will be the same as the PeptideDocNode
        /// TextId if this was full scan extraction.
        /// </summary>
        public bool OverrideTextId { get; }

        public Target ModifiedSequence
        {
            get
            {
                if (OverrideTextId)
                {
                    var peptideDocNode = NodeGroups.FirstOrDefault()?.Item1;
                    if (peptideDocNode != null)
                    {
                        return peptideDocNode.ModifiedTarget;
                    }
                }
                return _listChromData.Count > 0 ? BestChromatogram.Key.Target : null;
            }
        }

        public SignedMz PrecursorMz
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Key.Precursor : SignedMz.ZERO; }
        }

        public double? CollisionalCrossSectionSqA
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Key.CollisionalCrossSectionSqA : null; }
        }

        public eIonMobilityUnits IonMobilityUnits
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Key.IonMobilityUnits : eIonMobilityUnits.none; }
        }

        public bool IsSonarData
        {
            get { return _listChromData.Count > 0 && BestChromatogram.Key.IonMobilityFilter.IonMobilityUnits == eIonMobilityUnits.waters_sonar; }
        }

        public ChromExtractor Extractor
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Key.Extractor : ChromExtractor.summed; }
        }

        public float ExtractionWidth
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Key.ExtractionWidth : 0; }
        }

        public bool HasCalculatedMzs
        {
            get { return _listChromData.Count > 0 && BestChromatogram.Key.HasCalculatedMzs; }
        }

        public int StatusId
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Extra.StatusId : -1; }
        }

        public int StatusRank
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Extra.StatusRank : -1; }
        }

        public int CountPeaks
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Peaks.Count : 0; }
        }

        public int MaxPeakIndex
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.MaxPeakIndex : -1; }
        }

        public IList<float> Times
        {
            get { return _listChromData.Count > 0 ? BestChromatogram.Times : new float[0]; }
        }

        public IList<float>[] Intensities
        {
            get { return _listChromData.ConvertAll(data => data.Intensities).ToArray(); }
        }

        public IEnumerable<int> ProviderIds { get { return _listChromData.Select(c => c.ProviderId); } }

        public void Merge(ChromDataSet chromDataSet)
        {
            var setKeys = new HashSet<ChromKey>(_listChromData.Select(d => d.Key));
            bool anyAdded = false;
            foreach (var chromData in chromDataSet._listChromData)
            {
                if (setKeys.Add(chromData.Key))
                {
                    anyAdded = true;
                    Add(chromData);
                }
            }

            if (anyAdded)
            {
                // Enforce expected sorting if product ions are coming from different groups
                _listChromData.Sort();
                NodeGroups = ImmutableList.ValueOf(NodeGroups.Concat(chromDataSet.NodeGroups));
            }
        }

        public bool Load(ChromDataProvider provider, Target modifiedSequence, Color peptideColor)
        {
            foreach (var chromData in _listChromData.ToArray())
            {
                if (!chromData.Load(provider, modifiedSequence, peptideColor))
                {
                    Remove(chromData);
                }
            }
            return _listChromData.Count > 0;
        }

        internal float MinRawTime
        {
            get
            {
                float min = Single.MaxValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Count > 0)
                        min = Math.Min(min, chromData.RawTimes[0]);
                }
                return min;
            }
        }

        private float MaxStartTime
        {
            get
            {
                float max = Single.MinValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Count > 0)
                        max = Math.Max(max, chromData.RawTimes[0]);
                }
                return max;
            }
        }

        internal float MaxRawTime
        {
            get
            {
                float max = Single.MinValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Count > 0)
                        max = Math.Max(max, chromData.RawTimes[chromData.RawTimes.Count - 1]);
                }
                return max;
            }
        }

        private float MinEndTime
        {
            get
            {
                float min = Single.MaxValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Count > 0)
                        min = Math.Min(min, chromData.RawTimes[chromData.RawTimes.Count - 1]);
                }
                return min;
            }
        }

        public ChromKey FirstKey
        {
            get { return _listChromData.Count > 0 ? _listChromData[0].Key : ChromKey.EMPTY; }
        }

        /// <summary>
        /// If the minimum time is greater than two cycles from the maximum start,
        /// then use the minimum, and interpolate other transitions from it.
        /// Otherwise, try to avoid zeros at the edges, since they can create
        /// change that look like a peak.
        /// </summary>
        /// <param name="interval">Interval that will be used for interpolation</param>
        /// <returns>Value to use as the start time for chromatograms that do not infer zeros</returns>
        private double GetNonZeroStart(double interval)
        {
            float min = MinRawTime;
            float max = MaxStartTime;
            if (max - min > interval * 2 || max > MinEndTime)
                return min;
            return (max != float.MinValue ? max : double.MaxValue);
        }

        /// <summary>
        /// If the maximum time is greater than two cycles from the minimum end,
        /// then use the maximum, and interpolate other transitions to it.
        /// Otherwise, try to avoid zeros at the edges, since they can create
        /// change that looks like a peak.
        /// </summary>
        /// <param name="interval">Interval that will be used for interpolation</param>
        /// <returns>Value to use as the end time for chromatograms that do not infer zeros</returns>
        private double GetNonZeroEnd(double interval)
        {
            float min = MinEndTime;
            float max = MaxRawTime;
            if (max - min > interval * 2 || min < MaxStartTime)
                return max;
            return (min != float.MaxValue ? min : double.MinValue);
        }

        public void GetExtents(bool inferZeros, double intervalDelta, out double start, out double end)
        {
            if (inferZeros)
            {
                // If infering zeros, make sure values start and end with zero.
                start = MinRawTime - intervalDelta * 2;
                end = MaxRawTime + intervalDelta * 2;
            }
            else
            {
                // Otherwise, do best to use a non-zero start
                start = GetNonZeroStart(intervalDelta);
                end = GetNonZeroEnd(intervalDelta);
            }
        }

        public void GetExtents(bool inferZeros, double intervalDelta, float[] timesNew, out int start, out int end)
        {
            // Get the extent times
            double startTime, endTime;
            GetExtents(inferZeros, intervalDelta, out startTime, out endTime);

            // If there is no valid interval, return the entire array
            if (startTime > endTime)
            {
                start = 0;
                end = timesNew.Length - 1;
                return;
            }

            // Search forward for the time that best matches the start time.
            int i;
            for (i = 0; i < timesNew.Length; i++)
            {
                float time = timesNew[i];
                if (time == startTime)
                    break;
                if (time > startTime)
                {
                    if (i > 0)
                    {
                        if (inferZeros || time - startTime > startTime - timesNew[i - 1])
                            i--;
                    }
                    break;
                }
            }
            start = Math.Min(i, timesNew.Length - 1);
            // If there is only one time with an intensity, pick the time point
            // in the new time array closest to it.
            if (startTime == endTime)
            {
                if (i < timesNew.Length &&
                        (i == 0 || timesNew[i] - startTime < startTime - timesNew[i - 1]))
                    end = start;
                else
                    end = start = i - 1;
                return;
            }
            // Search backward from the end for the time that best matches the end time.
            int lastTime = timesNew.Length - 1;
            for (i = lastTime; i >= 0; i--)
            {
                float time = timesNew[i];
                if (time == endTime)
                    break;
                if (time < endTime)
                {
                    if (i < lastTime)
                    {
                        if (inferZeros || endTime - time < timesNew[i + 1] - endTime)
                            i++;
                    }
                    break;
                }
            }
            end = Math.Max(i, 0);
            if (end == start - 1)
            {
                // With some chromatograms with only one point in them, it is possible to end up with 
                // end before start.
                end = start;
            }
            // Make sure the final time interval contains at least one time.
            if (start > end)
                throw new InvalidOperationException(string.Format(Resources.ChromDataSet_GetExtents_The_time_interval__0__to__1__is_not_valid, start, end));
        }

        private const double NOISE_CORRELATION_THRESHOLD = 0.95;
        private const int MINIMUM_PEAKS = 3;
        private const int MAX_PEAKS_CHECKED = 20;

        public void SetExplicitPeakBounds(ExplicitPeakBounds peakBounds)
        {
            foreach (var chromData in _listChromData)
            {
                chromData.SetExplicitPeakBounds(peakBounds);

            }
            var firstChromData = _listChromData.First();
            if (firstChromData.RawPeaks.Any())
            {
                var firstPeak = new ChromDataPeak(firstChromData, firstChromData.RawPeaks.First());
                ChromDataPeakList chromDataPeakList = new ChromDataPeakList(FullScanAcquisitionMethod, firstPeak, _listChromData);
                _listPeakSets.Add(chromDataPeakList);
            }
        }
        
        /// <summary>
        /// Do initial grouping of and ranking of peaks using the Crawdad
        /// peak detector.
        /// </summary>
        public void PickChromatogramPeaks(double[] retentionTimes, bool isAlignedTimes, ExplicitRetentionTimeInfo explicitRT)
        {
            // Make sure chromatograms are in sorted order
            _listChromData.Sort();

            // Mark all optimization chromatograms
            MarkOptimizationData();

//            if (Math.Round(_listChromData[0].Key.Precursor) == 585)
//                Console.WriteLine("Issue");

            // First use Crawdad to find the peaks
            // If any chromatograms have an associated transition, then only find peaks
            // in chromatograms with transitions.  It is too confusing to the user to
            // score peaks based on chromatograms for hidden transitions.
            bool hasDocNode = _listChromData.Any(chromData => chromData.DocNode != null);
            foreach (var chromData in _listChromData)
            {
                // But only for fragment ions to allow hidden MS1 isotopes to participate
                bool doFindPeaks;
                if (!hasDocNode)
                {
                    doFindPeaks = true;
                }
                else
                {
                    if (chromData.Key.Source == ChromSource.fragment)
                    {
                        doFindPeaks = !FullScanAcquisitionMethod.DDA.Equals(FullScanAcquisitionMethod) &&
                                      null != chromData.DocNode;
                    }
                    else
                    {
                        doFindPeaks = true;
                    }
                }
                if (doFindPeaks)
                {
                    chromData.FindPeaks(retentionTimes, TimeIntervals, explicitRT);
                }
                else
                {
                    chromData.SkipFindingPeaks(retentionTimes);
                }
            }

            // Merge sort all peaks into a single list
            IList<ChromDataPeak> allPeaks = SplitMS(MergePeaks());

            // Inspect 20 most intense peak regions
            var listRank = new List<double>();
            Assume.IsTrue(_listPeakSets.Count == 0);
            for (int i = 0; i < MAX_PEAKS_CHECKED || retentionTimes.Length > 0; i++)
            {
                if (allPeaks.Count == 0)
                    break;

                ChromDataPeak peak = allPeaks[0];
                allPeaks.RemoveAt(0);

                // If peptide ID retention times are present, allow
                // peaks greater than 20, but only if they contain
                // an ID retention time.
                if (i >= MAX_PEAKS_CHECKED && !peak.Peak.Identified)
                    continue;

                ChromDataPeakList peakSet = FindCoelutingPeaks(peak, allPeaks);
                peakSet.SetIdentified(retentionTimes, isAlignedTimes);

                _listPeakSets.Add(peakSet);
                listRank.Add(i);
            }

            if (_listPeakSets.Count == 0)
                return;

            // Sort by total area descending
            _listPeakSets.Sort((p1, p2) => Comparer<double>.Default.Compare(p2.TotalArea, p1.TotalArea));

            // The peak will be a signigificant spike above the norm for this
            // data.  Find a cut-off by removing peaks until the remaining
            // peaks correlate well in a linear regression.
            var listAreas = _listPeakSets.ConvertAll(set => set.TotalArea);
            // Keep at least 3 peaks
            listRank.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listRank.Count));
            listAreas.RemoveRange(0, Math.Min(MINIMUM_PEAKS, listAreas.Count));
            Assume.IsTrue(listRank.Count == listAreas.Count);
            int iRemove = 0;
            // Keep all peaks for summary chromatograms
            if (PrecursorMz != 0)
            {
                // And there must be at least 5 peaks in the line to qualify for removal
                for (int i = 0, len = listAreas.Count; i < len - 4; i++)
                {
                    var statsRank = new Statistics(listRank);
                    var statsArea = new Statistics(listAreas);
                    double rvalue = statsArea.R(statsRank);
                    //                Console.WriteLine("i = {0}, r = {1}", i, rvalue);
                    if (Math.Abs(rvalue) > NOISE_CORRELATION_THRESHOLD)
                    {
                        iRemove = i + MINIMUM_PEAKS;
                        break;
                    }
                    listRank.RemoveAt(0);
                    listAreas.RemoveAt(0);
                }
            }
            if (iRemove == 0)
            {
                iRemove = _listPeakSets.Count;

                if (retentionTimes.Length == 0)
                {
                    // Backward compatibility: before peptide IDs were integrated
                    // this sorting happened before peaks were extended.
                    _listPeakSets.Sort(ComparePeakLists);
                }
            }
            else if (retentionTimes.Length == 0)
            {
                // Be sure not to remove anything with a higher combined score than
                // what happen to look visually like the biggest peaks.
                double minKeepScore = _listPeakSets.Take(iRemove).Min(peakSet => peakSet.CombinedScore);

                // Backward compatibility: before peptide IDs were integrated
                // this sorting happened before peaks were extended.
                _listPeakSets.Sort(ComparePeakLists);

                iRemove = Math.Max(iRemove, _listPeakSets.IndexOf(peakSet => peakSet.CombinedScore == minKeepScore));
            }
            else
            {
                // Make sure no identified peaks are removed.
                int identIndex = _listPeakSets.LastIndexOf(peakSet => peakSet.IsIdentified);
                if (identIndex >= iRemove)
                    iRemove = identIndex + 1;
                // Or, if there were identifications, but no peaks
                // peaks that appear to contain them, keep all peak sets
                else if (identIndex != -1)
                    iRemove = _listPeakSets.Count;
            }

            RemoveNonOverlappingPeaks(_listPeakSets, iRemove);

            // Since Crawdad can have a tendency to pick peaks too narrow,
            // use the peak group information to extend the peaks to make
            // them wider.
            // This does not handle reintegration, because peaks get reintegrated
            // before they are stored, taking the entire peptide into account.
            _listPeakSets = ExtendPeaks(_listPeakSets, retentionTimes, isAlignedTimes);

            // Sort by whether a peak contains an ID and then product score
            // This has to be done after peak extending, since extending may
            // change the state of whether the peak contains an ID.
            if (retentionTimes.Length > 0)
                _listPeakSets.Sort(ComparePeakLists);

//            if (retentionTimes.Length > 0 && !_listPeakSets[0].Identified)
//                Console.WriteLine("Idenifications outside peaks.");
        }

        /// <summary>
        /// Takes sorted list of peaks and puts MS1 peaks after MS2 peaks, preserving the order within
        /// the groups.
        /// </summary>
        private IList<ChromDataPeak> SplitMS(IList<ChromDataPeak> allPeaks)
        {
            int len = allPeaks.Count;
            var allPeaksNew = new List<ChromDataPeak>(len);
            // First add back all MS/MS peaks
            foreach (var peak in allPeaks)
            {
                if (IsMs2(peak))
                    allPeaksNew.Add(peak);
            }
            // Then add all MS1 peaks
            foreach (var peak in allPeaks)
            {
                if (!IsMs2(peak))
                    allPeaksNew.Add(peak);
            }
            return allPeaksNew;
        }

        public bool IsMs2(ChromDataPeak peak)
        {
            return peak.Data.Key.Source == ChromSource.fragment;
        }

        public int ComparePeakLists(ChromDataPeakList p1, ChromDataPeakList p2)
        {
            // Then order by PeakScore descending
            return Comparer<double>.Default.Compare(p2.CombinedScore, p1.CombinedScore);
        }

        public void SetPeakSet(ChromDataPeakList peakSet, int index)
        {
            _listPeakSets[index] = peakSet;
        }

        /// <summary>
        /// Generate <see cref="ChromDataPeak"/> objects to make peaks scorable
        /// </summary>
        public void GeneratePeakData(TimeIntervals intersectedTimeIntervals)
        {
            // Set the processed peaks back to the chromatogram data
            HashSet<ChromKey> primaryPeakKeys = new HashSet<ChromKey>();
            for (int i = 0, len = _listPeakSets.Count; i < len; i++)
            {
                var peakSet = _listPeakSets[i];
                var peakMax = peakSet[0].Peak;
                int optimizationStepMax = peakSet[0].Data.OptimizationStep;

                // Store the primary peaks that are part of this group.
                primaryPeakKeys.Clear();
                foreach (var peak in peakSet)
                {
                    if (peak.Peak != null)
                        primaryPeakKeys.Add(peak.Data.PrimaryKey);
                }

                foreach (var peak in peakSet)
                {
                    // Reintegrate a new peak based on the max peak
                    ChromPeak.FlagValues flags = 0;
                    // If the entire peak set is a result of forced integration from peptide
                    // peak matching, then flag each peak
                    if (peakSet.IsForcedIntegration)
                        flags |= ChromPeak.FlagValues.forced_integration;
                    else if (peak.Peak == null)
                    {
                        // Mark the peak as forced integration, if it was not part of the original
                        // coeluting set, unless it is optimization data for which the primary peak
                        // was part of the original set
                        if (peak.Data.OptimizationStep == optimizationStepMax || !primaryPeakKeys.Contains(peak.Data.PrimaryKey))
                            flags |= ChromPeak.FlagValues.forced_integration;
                    }
                    // Use correct time normalization flag (backward compatibility with v0.5)
                    if (_isTimeNormalArea)
                        flags |= ChromPeak.FlagValues.time_normalized;
                    if (peakSet.IsIdentified)
                        flags |= ChromPeak.FlagValues.contains_id;
                    if (peakSet.IsAlignedIdentified)
                        flags |= ChromPeak.FlagValues.used_id_alignment;
                    if (intersectedTimeIntervals != null)
                    {
                        if (!intersectedTimeIntervals.ContainsTime(peak.StartTime) || !intersectedTimeIntervals.ContainsTime(peak.EndTime))
                        {
                            flags |= ChromPeak.FlagValues.peak_truncated;
                        }
                    }

                    peak.CalcChromPeak(peakMax, flags, FullScanAcquisitionMethod, intersectedTimeIntervals);

                    if (intersectedTimeIntervals != null && peakMax != null)
                    {
                        float startTime = Times[peakMax.StartIndex];
                        float endTime = Times[peakMax.EndIndex];
                        var intervalIndex = intersectedTimeIntervals.IndexOfIntervalEndingAfter(startTime);
                        if (intervalIndex >= 0 && intervalIndex < intersectedTimeIntervals.Count)
                        {
                            startTime = Math.Max(startTime, intersectedTimeIntervals.Starts[intervalIndex]);
                            endTime = Math.Min(endTime, intersectedTimeIntervals.Ends[intervalIndex]);
                        }

                        var chromPeak = ChromPeak.IntegrateWithoutBackground(peak.Data.RawTimeIntensities, startTime, endTime, flags);
                        peak.SetChromPeak(chromPeak);
                    }
                }
            }
        }

        /// <summary>
        /// Sort the final peaks by retention time and make a pointer to the best peak
        /// </summary>
        public void StorePeaks()
        {
            // If there are no peaks to store, do nothing.
            if (_listPeakSets.Count == 0)
                return;

            // Pick the maximum peak by the product score
            ChromDataPeakList peakSetMax = _listPeakSets[0];

            // Sort them back into retention time order
            _listPeakSets.Sort((l1, l2) =>
                (l1[0].Peak != null ? l1[0].Peak.StartIndex : 0) -
                (l2[0].Peak != null ? l2[0].Peak.StartIndex : 0));

            // Set the processed peaks back to the chromatogram data
            int maxPeakIndex = _listPeakSets.IndexOf(peakSetMax);
            int i = 0;
            foreach (var peakSet in _listPeakSets)
            {
                foreach (var peak in peakSet)
                {
                    peak.Data.Peaks.Add(peak.DataPeak);
                    // Set the max peak index on the data for each transition
                    if (i == 0)
                        peak.Data.MaxPeakIndex = maxPeakIndex;
                }
                ++i;
            }   
        }

        private static List<ChromDataPeakList> ExtendPeaks(IEnumerable<ChromDataPeakList> listPeakSets,
                                                           double[] retentionTimes,
                                                           bool isAlignedTimes)
        {
            var listExtendedSets = new List<ChromDataPeakList>();
            foreach (var peakSet in listPeakSets)
            {
                if (!peakSet.Extend())
                    continue;
                peakSet.SetIdentified(retentionTimes, isAlignedTimes);
                listExtendedSets.Add(peakSet);
            }
            return listExtendedSets;
        }

        private IList<ChromDataPeak> MergePeaks()
        {
            List<ChromDataPeak> allPeaks = new List<ChromDataPeak>();
            var listEnumerators = _listChromData.ConvertAll(item => item.RawPeaks.GetEnumerator());
            // Merge with list of chrom data that will match the enumerators
            // list, as completed enumerators are removed.
            var listUnmerged = new List<ChromData>(_listChromData);
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
                float maxIntensity = 0;
                int maxId = 0;
                int iMaxEnumerator = -1;

                for (int i = 0; i < listEnumerators.Count; i++)
                {
                    var peak = listEnumerators[i].Current;
                    if (peak == null)
                        throw new InvalidOperationException(Resources.ChromDataSet_MergePeaks_Unexpected_null_peak);
                    float intensity = peak.Area;
                    int isId = peak.Identified ? 1 : 0;
                    if (isId > maxId  || (isId == maxId && intensity > maxIntensity))
                    {
                        maxId = isId;
                        maxIntensity = intensity;
                        iMaxEnumerator = i;
                    }
                }

                // If only zero area peaks left, stop looping.
                if (iMaxEnumerator == -1)
                    break;

                var maxData = listUnmerged[iMaxEnumerator];
                var maxEnumerator = listEnumerators[iMaxEnumerator];
                var maxPeak = maxEnumerator.Current;
                Debug.Assert(maxPeak != null);
                // Discard peaks that occur at the edge of their range.
                // These are not useful in SRM.
                // TODO: Fix Crawdad peak detection to make this unnecessary
// ReSharper disable ConditionIsAlwaysTrueOrFalse
                if (maxPeak != null && maxPeak.StartIndex != maxPeak.TimeIndex && maxPeak.EndIndex != maxPeak.TimeIndex)
// ReSharper restore ConditionIsAlwaysTrueOrFalse
                    allPeaks.Add(new ChromDataPeak(maxData, maxPeak));
                if (!maxEnumerator.MoveNext())
                {
                    listEnumerators.RemoveAt(iMaxEnumerator);
                    listUnmerged.RemoveAt(iMaxEnumerator);
                }
            }
            return allPeaks;
        }

        private static void RemoveNonOverlappingPeaks(IList<ChromDataPeakList> listPeakSets, int iRemove)
        {
            for (int i = listPeakSets.Count - 1; i >= iRemove; i--)
            {
                if (!IsOverlappingPeak(listPeakSets[i][0], listPeakSets, iRemove))
                    listPeakSets.RemoveAt(i);
            }
        }

        private static bool IsOverlappingPeak(ChromDataPeak peak,
                                              IList<ChromDataPeakList> listPeakSets, int count)
        {
            var peak1 = peak.Peak;
            int overlapThreshold = (int)Math.Round((peak1.EndIndex - peak1.StartIndex) / 2.0);
            for (int i = 0; i < count; i++)
            {
                var peak2 = listPeakSets[i][0].Peak;
                if (Math.Min(peak1.EndIndex, peak2.EndIndex) - Math.Max(peak1.StartIndex, peak2.StartIndex) >= overlapThreshold)
                    return true;
            }
            return false;
        }

        private void MarkOptimizationData()
        {
            int iFirst = 0;
            for (int i = 0; i < _listChromData.Count; i++)
            {
                if (i < _listChromData.Count - 1 &&
                    ChromatogramInfo.IsOptimizationSpacing(_listChromData[i].Key.Product, _listChromData[i + 1].Key.Product))
                {
                    // CONSIDER: This is no longer possible, since IsOptimizationSpacing checked for order
                    //           optimization spacing could happen at a boundary changing between ion types
                    if (_listChromData[i + 1].Key.Product < _listChromData[i].Key.Product)
                    {
                        throw new InvalidDataException(string.Format(Resources.ChromDataSet_MarkOptimizationData_Incorrectly_sorted_chromatograms__0__1__,
                                                                     _listChromData[i + 1].Key.Product, _listChromData[i].Key.Product));
                    }
                }
                else
                {
                    if (iFirst != i)
                    {
                        // The middle element in the run is the regression value.
                        // Mark it as not optimization data.
                        int middleIndex = (i - iFirst)/2 + iFirst;
                        var primaryData = _listChromData[middleIndex];
                        // Set the primary key for all members of this group.
                        for (int j = iFirst; j <= i; j++)
                        {
                            _listChromData[j].OptimizationStep = middleIndex - j;
                            _listChromData[j].PrimaryKey = primaryData.Key;
                        }
                    }
                    // Start a new run with the next value
                    iFirst = i + 1;
                }
            }
        }

        // Moved to ProteoWizard
        public bool WiffZerosFix()
        {
            if (!HasFlankingZeros)
                return false;

            // Remove flagging zeros
            foreach (var chromData in _listChromData)
            {
                var times = chromData.Times;
                var intensities = chromData.Intensities;
                int start = 0;
                while (start < intensities.Count - 1 && intensities[start] == 0)
                    start++;
                int end = intensities.Count;
                while (end > 0 && intensities[end - 1] == 0)
                    end--;

                // Leave at least one bounding zero
                if (start > 0)
                    start--;
                if (end < intensities.Count)
                    end++;

                var timesNew = new float[end - start];
                var intensitiesNew = new float[end - start];
                CollectionUtil.Copy(times, start, timesNew, 0, timesNew.Length);
                CollectionUtil.Copy(intensities, start, intensitiesNew, 0, intensitiesNew.Length);
                Assume.IsNull(chromData.TimeIntensities.ScanIds);
                chromData.FixChromatogram(timesNew, intensitiesNew, null);
            }
            return true;
        }

        private bool HasFlankingZeros
        {
            get
            {
                // Check for case where all chromatograms have at least
                // 10 zero intensity entries on either side of the real data.
                foreach (var chromData in _listChromData)
                {
                    var intensities = chromData.Intensities;
                    if (intensities.Count < 10)
                        return false;
                    for (int i = 0; i < 10; i++)
                    {
                        if (intensities[i] != 0)
                            return false;
                    }
                    for (int i = intensities.Count - 1; i < 10; i++)
                    {
                        if (intensities[i] != 0)
                            return false;
                    }
                }
                return true;
            }
        }

        public bool ThermoZerosFix()
        {
            if (_listChromData.Any(chromData => null != chromData.TimeIntensities.ScanIds))
            {
                return false;
            }
            bool fixedZeros = false;
            // Remove interleaving zeros
            foreach (var chromData in _listChromData.Where(HasThermoZerosBug))
            {
                fixedZeros = true;

                var times = chromData.Times;
                var intensities = chromData.Intensities;
                var timesNew = new float[intensities.Count / 2];
                var intensitiesNew = new float[intensities.Count / 2];
                for (int i = (intensities.Count > 0 && intensities[0] == 0 ? 1 : 0), iNew = 0; iNew < timesNew.Length; i += 2, iNew++)
                {
                    timesNew[iNew] = times[i];
                    intensitiesNew[iNew] = intensities[i];
                }
                Assume.IsNull(chromData.TimeIntensities.ScanIds);
                chromData.FixChromatogram(timesNew, intensitiesNew, null);
            }
            return fixedZeros;
        }

        private bool HasThermoZerosBug(ChromData chromData)
        {
            // Make sure the intensity arrays are not just empty to avoid
            // an infinite loop.
            // Check for interleaving zeros and non-zero values
            bool seenData = false;
            var intensities = chromData.Intensities;
            for (int i = (intensities.Count > 0 && intensities[0] == 0 ? 0 : 1); i < intensities.Count; i += 2)
            {
                if (intensities[i] != 0)
                    return false;
                // Because WIFF files have lots of zeros
                if (i < intensities.Count - 1 && intensities[i + 1] == 0)
                    return false;
                seenData = true;
            }
            return seenData;
        }

        public const double MIN_PERCENT_OF_MAX = 0.01;
        public const double MIN_PERCENT_OF_MAX_PRECURSOR = 0.001;

        private ChromDataPeakList FindCoelutingPeaks(ChromDataPeak dataPeakMax,
                                                     IList<ChromDataPeak> allPeaks)
        {
            IFoundPeak peakMax = dataPeakMax.Peak;
            float areaMax = peakMax.Area;
            int centerMax = peakMax.TimeIndex;
            int startMax = peakMax.StartIndex;
            int endMax = peakMax.EndIndex;
            int widthMax = peakMax.Length;
            int deltaMax = (int)Math.Round(widthMax / 4.0, 0);
            var listPeaks = new ChromDataPeakList(FullScanAcquisitionMethod, dataPeakMax);

            // Allow peaks in the group to be smaller, if the max peak is the precursor.
            // Really this should be checking to see if the precursor data came from MS1
            // scans, since this is the main case it is intended to handle, where MS1
            // intensities can be quite a bit higher than MS/MS fragments.
            var keyMax = dataPeakMax.Data.Key;
            double minPrecentMax = (keyMax.Precursor != keyMax.Product
                                        ? MIN_PERCENT_OF_MAX
                                        : MIN_PERCENT_OF_MAX_PRECURSOR);

            foreach (var chromData in _listChromData)
            {
                if (ReferenceEquals(chromData, dataPeakMax.Data))
                    continue;

                int iPeakNearest = -1;
                int deltaNearest = deltaMax;

                // Find nearest peak in remaining set that is less than 1/4 length
                // from the primary peak's center
                for (int i = 0, len = allPeaks.Count; i < len; i++)
                {
                    var peak = allPeaks[i];
                    if (!ReferenceEquals(peak.Data, chromData))
                        continue;

                    // Exclude peaks where the apex is not inside the max peak,
                    // or apex is at one end of the peak
                    int timeIndex = peak.Peak.TimeIndex;
                    int startPeak = peak.Peak.StartIndex;
                    int endPeak = peak.Peak.EndIndex;
                    if (startMax >= timeIndex || timeIndex >= endMax ||
                        startPeak == timeIndex || timeIndex == endPeak)
                        continue;
                    // or peak area is less than 1% of max peak area (or 0.1% if max is precursor)
                    if (peak.Peak.Area < areaMax * minPrecentMax)
                        continue;
                    // or when FWHM is very narrow, usually a good indicator of noise
                    if (/* peak.Peak.Fwhm < 1.2 too agressive || */ peak.Peak.Fwhm * 12 < widthMax)
                        continue;
                    // or where the peak does not overlap at least 50% of the max peak
                    int intersect = Math.Min(endMax, peak.Peak.EndIndex) -
                                    Math.Max(startMax, peak.Peak.StartIndex) + 1;   // +1 for inclusive end
                    int lenPeak = peak.Peak.Length;
                    // Allow 25% coverage, if the peak is entirely inside the max, since
                    // sometimes Crawdad breaks smaller peaks up.
                    int factor = (intersect == lenPeak ? 4 : 2);
                    if (intersect * factor < widthMax)
                        continue;
                    int delta = Math.Abs(timeIndex - centerMax);
                    // If apex delta and FWHM are not very close to the max peak, make further checks
                    if (delta * 4.0 > deltaMax || Math.Abs(peak.Peak.Fwhm - peakMax.Fwhm) / peakMax.Fwhm > 0.05)
                    {
                        // If less than 2/3 of the peak is inside the max peak, or 1/2 if the
                        // peak entirely contains the max peak.
                        double dFactor = (intersect == widthMax ? 2.0 : 1.5);
                        if (intersect * dFactor < lenPeak)
                            continue;
                        // or where either end is more than 2/3 of the intersect width outside
                        // the max peak.
                        if (intersect != lenPeak)
                        {
                            dFactor = 1.5;
                            if ((startMax - peak.Peak.StartIndex) * dFactor > intersect ||
                                (peak.Peak.EndIndex - endMax) * dFactor > intersect)
                                continue;
                        }
                    }

                    if (delta <= deltaNearest)
                    {
                        deltaNearest = delta;
                        iPeakNearest = i;
                    }
                }

                if (iPeakNearest == -1)
                    listPeaks.Add(new ChromDataPeak(chromData, null));
                else
                {
                    listPeaks.Add(new ChromDataPeak(chromData, allPeaks[iPeakNearest].Peak));
                    allPeaks.RemoveAt(iPeakNearest);
                }
            }
            return listPeaks;
        }

        public ChromDataPeakList SetBestPeak(ChromDataPeakList peakSet, PeptideChromDataPeak bestPeptidePeak, int indexSet)
        {
            ChromDataPeakList peakSetAdd = null;
            int startIndex, endIndex;
            // TODO: Need to do something more reasonable for Deuterium elution time shifts
            bool matchBounds = GetLocalPeakBounds(bestPeptidePeak, out startIndex, out endIndex);

            if (peakSet != null)
            {
                // If the peak ranked by peptide matching is not in its proper position,
                // then move it there
                if (!ReferenceEquals(peakSet, _listPeakSets[indexSet]))
                {
                    _listPeakSets.Remove(peakSet);
                    _listPeakSets.Insert(indexSet, peakSet);
                }
                // If there is a different best peptide peak, and it should have
                // the same retention time charachteristics, then reset the integration
                // boundaries of this peak set
                if (matchBounds)
                {
                    var peak = peakSet[0].Peak;
                    // If this peak and the best peak for this peptide are allowed to be shifted in
                    // relationship to each other, then make that shift be the delta between the apex
                    // times of the two peaks.  They still need to be the same width.
                    if (!IsSameRT(bestPeptidePeak.Data))
                    {
                        int deltaBounds = GetLocalIndex(bestPeptidePeak.TimeIndex) - peak.TimeIndex;
                        startIndex = Math.Max(0, startIndex + deltaBounds);
                        endIndex = Math.Min(Times.Count - 1, endIndex + deltaBounds);
                    }

                    // Reset the range of the best peak, if the chromatograms for this peak group
                    // overlap with the best peptide peak at all.  Resetting the range of the best
                    // peak for this peak group will cause it and all other peaks in the peak group
                    // to be reintegrated to this range
                    if (startIndex < endIndex)
                    {
                        peak.ResetBoundaries(startIndex, endIndex);
                    }
                    // In a peak set with mutiple charge states and light-heavy pairs, it is
                    // possible that a peak may not overlap with the best peak in its
                    // charge group.  If this is the case, and the best peak is completely
                    // outside the bounds of the current chromatogram, then insert an
                    // empty peak.
                    else
                    {
                        var peakAdd = new ChromDataPeak(BestChromatogram, null);
                        peakSetAdd = new ChromDataPeakList(FullScanAcquisitionMethod, peakAdd, _listChromData) { IsForcedIntegration = true };
                    }
                }
            }
            // If no peak was found at the peptide level for this data set,
            // but there is a best peak for the peptide
            else if (bestPeptidePeak != null && matchBounds)
            {
                // And the chromatograms for this transition group overlap with the best peptide
                // peak, then create a peak with the same extents as the best peak.  This peak will
                // appear as missing, if Integrate All is not selected.
                ChromDataPeak peakAdd;
                if (startIndex < endIndex)
                {
                    // CONSIDER: Not that great for peaks that are expected to be shifted in relationship
                    //           to each other, since this will force them to have the same boundaries,
                    //           but if no evidence of a peak was found, it is hard to understand what
                    //           could be done better.
                    var chromData = BestChromatogram;
                    peakAdd = new ChromDataPeak(chromData, chromData.CalcPeak(startIndex, endIndex));
                }
                // Otherwise, create an empty peak
                else
                {
                    peakAdd = new ChromDataPeak(BestChromatogram, null);
                }

                peakSetAdd = new ChromDataPeakList(FullScanAcquisitionMethod, peakAdd, _listChromData) {IsForcedIntegration = true};
            }
            else
            {
                // Otherwise, insert an empty peak
                var peakAdd = new ChromDataPeak(BestChromatogram, null);
                peakSetAdd = new ChromDataPeakList(FullScanAcquisitionMethod, peakAdd, _listChromData) { IsForcedIntegration = true };
            }

            if (peakSetAdd != null)
            {
                if (indexSet < _listPeakSets.Count)
                    _listPeakSets.Insert(indexSet, peakSetAdd);
                else
                    _listPeakSets.Add(peakSetAdd);
                peakSet = peakSetAdd;
            }
            return peakSet;
        }

        public void NarrowPeak(ChromDataPeakList peakSet, PeptideChromDataPeak narrowestPeptidePeak, int indexSet)
        {
            var peak = peakSet[0].Peak;
            if (peak != null)
            {
                int len = narrowestPeptidePeak.Length;
                if (narrowestPeptidePeak.IsRightBound)
                    peak.ResetBoundaries(peak.EndIndex - len + 1, peak.EndIndex);
                else // if (narrowestPeptidePeak.IsLeftBound)  Need to make sure they are the same length
                    peak.ResetBoundaries(peak.StartIndex, peak.StartIndex + len - 1);
            }
        }

        /// <summary>
        /// Returns true if the peak boundaries should be reset to the startIndex and endIndex
        /// values output from this function.
        /// </summary>
        /// <param name="bestPeptidePeak">The best peak for this peptide, which will be in its own scale</param>
        /// <param name="startIndex">The start index this peak should have, if return is true</param>
        /// <param name="endIndex">The end index this peak should have, if return is false</param>
        /// <returns>True if peak bounds should be reset</returns>
        private bool GetLocalPeakBounds(PeptideChromDataPeak bestPeptidePeak, out int startIndex, out int endIndex)
        {
            if (bestPeptidePeak == null)
            {
                startIndex = endIndex = -1;
                return false;
            }
            startIndex = Math.Max(0, GetLocalIndex(bestPeptidePeak.StartIndex));
            endIndex = Math.Min(Times.Count - 1, GetLocalIndex(bestPeptidePeak.EndIndex));
            return IsSameRT(bestPeptidePeak.Data) || IsShiftedRT(bestPeptidePeak.Data);
        }

        private int GetLocalIndex(int indexPeptide)
        {
            return indexPeptide - PeptideIndexOffset;
        }

        private bool IsSameRT(ChromDataSet chromDataSet)
        {
            return (RelativeRT == RelativeRT.Matching && chromDataSet.RelativeRT == RelativeRT.Matching) ||
                   LabelTypes.Intersect(chromDataSet.LabelTypes).Any();
        }

        private bool IsShiftedRT(ChromDataSet chromDataSet)
        {
            return RelativeRT == RelativeRT.Preceding ||
                chromDataSet.RelativeRT == RelativeRT.Preceding ||
                RelativeRT == RelativeRT.Overlapping ||
                chromDataSet.RelativeRT == RelativeRT.Overlapping;
        }

        public RelativeRT RelativeRT
        {
            get
            {
                return MergeRelativeRTs(NodeGroups.Select(group => group.Item2.RelativeRT));
            }
        }

        /// <summary>
        /// Return the strongest relativeRT from a list. Returns any RelativeRT in the list
        /// is "Matching", then returns "Matching". Returns "Unknown" only if all RelativeRTs
        /// in the list are "Unknown".
        /// </summary>
        public static RelativeRT MergeRelativeRTs(IEnumerable<RelativeRT> relativeRts)
        {
            var result = RelativeRT.Unknown;
            foreach (var relativeRT in relativeRts)
            {
                if (relativeRT == RelativeRT.Matching)
                {
                    return RelativeRT.Matching;
                }

                if (result == RelativeRT.Unknown)
                {
                    result = relativeRT;
                }
                else if (result != relativeRT)
                {
                    return RelativeRT.Matching;
                }
            }

            return result;
        }

        public override string ToString()
        {
            return Count > 0 ? _listChromData[0].ToString() : Resources.ChromDataSet_ToString_empty;
        }

        public void Truncate(double startTime, double endTime)
        {
            for (int i = 0; i < _listChromData.Count; i++)
            {
                var chromData = _listChromData[i];
                _listChromData[i] = chromData.Truncate(startTime, endTime);
            }
        }

        /// <summary>
        /// If this dataset contains both MS1 and MS2 chromatograms, then shorten the MS1 chromatograms
        /// so that they are the same length as the MS2 chromatograms.
        /// </summary>
        public static void TruncateMs1ForScheduledMs2(ICollection<ChromDataSet> chromDataSets)
        {
            // Only truncate if every ChromDataSet has at least one MS2 chromatogram and one MS1 chromatogram
            foreach (var chromDataSet in chromDataSets)
            {
                if (chromDataSet._listChromData.All(data => data.Key.Source != ChromSource.ms1)
                    || chromDataSet._listChromData.All(data => data.Key.Source != ChromSource.fragment))
                {
                    return;
                }
                if (chromDataSet.TimeIntervals != null)
                {
                    return;
                }
            }
            float minFragmentTime = float.MaxValue;
            float maxFragmentTime = float.MinValue;
            foreach (var chromData in chromDataSets.SelectMany(dataSet=>dataSet._listChromData))
            {
                if (chromData.Key.Source == ChromSource.fragment && chromData.Times.Any())
                {
                    minFragmentTime = Math.Min(minFragmentTime, chromData.Times[0]);
                    maxFragmentTime = Math.Max(maxFragmentTime, chromData.Times[chromData.Times.Count - 1]);
                }
            }
            if (minFragmentTime >= maxFragmentTime)
            {
                return;
            }
            foreach (var chromDataSet in chromDataSets)
            {
                for (int i = 0; i < chromDataSet._listChromData.Count; i++)
                {
                    var chromData = chromDataSet._listChromData[i];
                    if (chromData.Key.Source != ChromSource.ms1 || !chromData.Times.Any())
                    {
                        continue;
                    }

                    if (chromData.Times[0] > minFragmentTime ||
                        chromData.Times[chromData.Times.Count - 1] < maxFragmentTime)
                    {
                        // Do not truncate the MS1 chromatogram if it overlaps with the
                        // MS2 chromatograms in an unexpected, non superset, way.
                        continue;
                    }
                    chromDataSet._listChromData[i] = chromData.Truncate(minFragmentTime, maxFragmentTime);
                }
            }
        }

        public TimeIntensitiesGroup ToGroupOfTimeIntensities(bool useRawTimes)
        {
            if (useRawTimes)
            {
                var rawTimeIntensities = new RawTimeIntensities(_listChromData.Select(chromData => chromData.RawTimeIntensities),
                    InterpolationParams);
                if (TimeIntervals != null)
                {
                    rawTimeIntensities = rawTimeIntensities.ChangeTimeIntervals(TimeIntervals);
                }

                return rawTimeIntensities;
            }
            return new InterpolatedTimeIntensities(_listChromData.Select(chromData=>chromData.TimeIntensities), 
                _listChromData.Select(chromData=>chromData.PrimaryKey.Source));
        }

        public TimeIntervals TimeIntervals { get; set; }
    }
}

