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
using System.IO;
using System.Linq;
using pwiz.Crawdad;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromDataSet
    {
        private readonly List<ChromData> _listChromData = new List<ChromData>();
        private readonly bool _isTimeNormalArea;

        private List<ChromDataPeakList> _listPeakSets = new List<ChromDataPeakList>();

        public ChromDataSet(bool isTimeNormalArea, params ChromData[] arrayChromData)
        {
            _isTimeNormalArea = isTimeNormalArea;
            _listChromData.AddRange(arrayChromData);
        }

        public IEnumerable<ChromData> Chromatograms { get { return _listChromData; } }

        public int Count { get { return _listChromData.Count; } }

        public void Add(ChromData chromData)
        {
            _listChromData.Add(chromData);
        }

        public int Offset { get; set; }

        public bool IsTimeNormalArea { get { return _isTimeNormalArea; } }

        public IEnumerable<ChromDataPeakList> PeakSets { get { return _listPeakSets; } }

        public TransitionGroupDocNode DocNode { get; set; }

        public float PrecursorMz
        {
            get { return _listChromData.Count > 0 ? _listChromData[0].Key.Precursor : 0; }
        }

        public int CountPeaks
        {
            get { return _listChromData.Count > 0 ? _listChromData[0].Peaks.Count : 0; }
        }

        public int MaxPeakIndex
        {
            get { return _listChromData.Count > 0 ? _listChromData[0].MaxPeakIndex : 0; }
        }

        public float[] Times
        {
            get { return _listChromData.Count > 0 ? _listChromData[0].Times : new float[0]; }
        }

        public float[][] Intensities
        {
            get { return _listChromData.ConvertAll(data => data.Intensities).ToArray(); }
        }

        public void Load(ChromDataProvider provider)
        {
            foreach (var chromData in Chromatograms)
                chromData.Load(provider);
        }

        private float MinRawTime
        {
            get
            {
                float min = Single.MaxValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Length > 0)
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
                    if (chromData.RawTimes.Length > 0)
                        max = Math.Max(max, chromData.RawTimes[0]);
                }
                return max;
            }
        }

        private float MaxRawTime
        {
            get
            {
                float max = Single.MinValue;
                foreach (var chromData in _listChromData)
                {
                    if (chromData.RawTimes.Length > 0)
                        max = Math.Max(max, chromData.RawTimes[chromData.RawTimes.Length - 1]);
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
                    if (chromData.RawTimes.Length > 0)
                        min = Math.Min(min, chromData.RawTimes[chromData.RawTimes.Length - 1]);
                }
                return min;
            }
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
            if (max - min > interval * 2)
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
            if (max - min > interval * 2)
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
                    if (inferZeros)
                        i = Math.Max(0, i - 1);
                    break;
                }
            }
            start = i;
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
                    if (inferZeros)
                        i = Math.Min(lastTime, i + 1);
                    break;
                }
            }
            end = i;

            // Make sure the final time interval contains at least one time.
            if (start > end)
                throw new InvalidOperationException(string.Format("The time interval {0} to {1} is not valid.", start, end));
        }

        private const double NOISE_CORRELATION_THRESHOLD = 0.95;
        private const int MINIMUM_PEAKS = 3;

        /// <summary>
        /// Do initial grouping of and ranking of peaks using the Crawdad
        /// peak detector.
        /// </summary>
        public void PickChromatogramPeaks(double[] retentionTimes)
        {
            // Make sure chromatograms are in sorted order
            _listChromData.Sort((c1, c2) => c1.Key.CompareTo(c2.Key));

            // Mark all optimization chromatograms
            MarkOptimizationData();

//            if (Math.Round(_listChromData[0].Key.Precursor) == 585)
//                Console.WriteLine("Issue");

            // First use Crawdad to find the peaks
            _listChromData.ForEach(chromData => chromData.FindPeaks(retentionTimes));

            // Merge sort all peaks into a single list
            IList<ChromDataPeak> allPeaks = MergePeaks();

            // Inspect 20 most intense peak regions
            var listRank = new List<double>();
            for (int i = 0; i < 20 || retentionTimes.Length > 0; i++)
            {
                if (allPeaks.Count == 0)
                    break;

                ChromDataPeak peak = allPeaks[0];
                allPeaks.RemoveAt(0);

                // If peptide ID retention times are present, allow
                // peaks greater than 20, but only if they contain
                // an ID retention time.
                if (i >= 20 && !peak.IsIdentified(retentionTimes))
                    continue;

                ChromDataPeakList peakSet = FindCoelutingPeaks(peak, allPeaks);
                peakSet.SetIdentified(retentionTimes);

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
            int iRemove = 0;
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
            if (iRemove == 0)
                iRemove = _listPeakSets.Count;
            else if (retentionTimes.Length > 0)
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

            // Add small peaks under the chosen peaks, to make adding them easier
            foreach (var peak in allPeaks)
            {
                if (IsOverlappingPeak(peak, _listPeakSets, iRemove))
                    _listPeakSets.Add(new ChromDataPeakList(peak, _listChromData));
            }

            // Backward compatibility: before peptide IDs were integrated
            // this sorting happened before peaks were extended.
            if (retentionTimes.Length == 0)
                _listPeakSets.Sort(ComparePeakLists);

            // Since Crawdad can have a tendency to pick peaks too narrow,
            // use the peak group information to extend the peaks to make
            // them wider.
            // This does not handle reintegration, because peaks get reintegrated
            // before they are stored, taking the entire peptide into account.
            _listPeakSets = ExtendPeaks(_listPeakSets, retentionTimes);

            // Sort by whether a peak contains an ID and then product score
            // This has to be done after peak extending, since extending may
            // change the state of whether the peak contains an ID.
            if (retentionTimes.Length > 0)
                _listPeakSets.Sort(ComparePeakLists);

//            if (retentionTimes.Length > 0 && !_listPeakSets[0].IsIdentified)
//                Console.WriteLine("Idenifications outside peaks.");
        }

        public int ComparePeakLists(ChromDataPeakList p1, ChromDataPeakList p2)
        {
            // All identified peaks come first
            if (p1.IsIdentified != p2.IsIdentified)
                return p1.IsIdentified ? -1 : 1;

            // Then order by ProductArea descending
            return Comparer<double>.Default.Compare(p2.ProductArea, p1.ProductArea);
        }

        /// <summary>
        /// Store the final peaks back on the individual <see cref="ChromDataSet"/> objects
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
            HashSet<ChromKey> primaryPeakKeys = new HashSet<ChromKey>();
            for (int i = 0, len = _listPeakSets.Count; i < len; i++)
            {
                var peakSet = _listPeakSets[i];
                var peakMax = peakSet[0].Peak;

                // Store the primary peaks that are part of this group.
                primaryPeakKeys.Clear();
                foreach (var peak in peakSet)
                {
                    if (peak.Peak != null)
                        primaryPeakKeys.Add(peak.Data.Key);
                }

                foreach (var peak in peakSet)
                {
                    // Set the max peak index on the data for each transition,
                    // but only the first time through.
                    if (i == 0)
                        peak.Data.MaxPeakIndex = maxPeakIndex;

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
                        if (!peak.Data.IsOptimizationData || !primaryPeakKeys.Contains(peak.Data.PrimaryKey))
                            flags |= ChromPeak.FlagValues.forced_integration;
                    }
                    // Use correct time normalization flag (backward compatibility with v0.5)
                    if (_isTimeNormalArea)
                        flags |= ChromPeak.FlagValues.time_normalized;
                    if (peakSet.IsIdentified)
                        flags |= ChromPeak.FlagValues.contains_id;
                    peak.Data.Peaks.Add(peak.CalcChromPeak(peakMax, flags));
                }
            }
        }

        private static List<ChromDataPeakList> ExtendPeaks(IEnumerable<ChromDataPeakList> listPeakSets,
                                                           double[] retentionTimes)
        {
            var listExtendedSets = new List<ChromDataPeakList>();
            foreach (var peakSet in listPeakSets)
            {
                peakSet.Extend();
                peakSet.SetIdentified(retentionTimes);
                if (!PeaksRedundant(peakSet, listExtendedSets))
                    listExtendedSets.Add(peakSet);
            }
            return listExtendedSets;
        }

        private static bool PeaksRedundant(ChromDataPeakList peakSetTest, IEnumerable<ChromDataPeakList> peakSets)
        {
            foreach (var peakSet in peakSets)
            {
                if (PeaksOverlap(peakSet[0].Peak, peakSetTest[0].Peak) &&
                    // The peaks are not redundant, if they are identified and
                    // the peaks they overlap with are not.
                    (!peakSetTest.IsIdentified || peakSet.IsIdentified))
                {
                    // Check peaks where their largest peaks overlap to make
                    // sure they have transitions with measured signal in common.
                    var sharedPeaks = from dataPeak in peakSet
                                      join dataPeakTest in peakSetTest on
                                          dataPeak.Data.Key equals dataPeakTest.Data.Key
                                      where dataPeak.Peak != null && dataPeakTest.Peak != null
                                      select dataPeak;
                    return sharedPeaks.Any();
                }
            }
            return false;
        }

        private static bool PeaksOverlap(CrawdadPeak peak1, CrawdadPeak peak2)
        {
            // Peaks overlap, if they have intersecting area.
            return Math.Min(peak1.EndIndex, peak2.EndIndex) -
                   Math.Max(peak1.StartIndex, peak2.StartIndex) > 0;
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
                int iMaxEnumerator = -1;

                for (int i = 0; i < listEnumerators.Count; i++)
                {
                    var peak = listEnumerators[i].Current;
                    if (peak == null)
                        throw new InvalidOperationException("Unexpected null peak");
                    float intensity = peak.Area;
                    if (intensity > maxIntensity)
                    {
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
                if (maxPeak != null && maxPeak.StartIndex != maxPeak.TimeIndex && maxPeak.EndIndex != maxPeak.TimeIndex)
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
                    if (_listChromData[i + 1].Key.Product < _listChromData[i].Key.Product)
                    {
                        throw new InvalidDataException(String.Format("Incorrectly sorted chromatograms {0} > {1}",
                                                                     _listChromData[i + 1].Key.Product, _listChromData[i].Key.Product));
                    }
                }
                else
                {
                    if (iFirst != i)
                    {
                        // The middle element in the run is the regression value.
                        // Mark it as not optimization data.
                        var primaryData = _listChromData[(i - iFirst) / 2 + iFirst];
                        // Set the primary key for all members of this group.
                        for (int j = iFirst; j <= i; j++)
                        {
                            _listChromData[j].IsOptimizationData = true;
                            _listChromData[j].PrimaryKey = primaryData.Key;
                        }
                        primaryData.IsOptimizationData = false;
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
                while (start < intensities.Length - 1 && intensities[start] == 0)
                    start++;
                int end = intensities.Length;
                while (end > 0 && intensities[end - 1] == 0)
                    end--;

                // Leave at least one bounding zero
                if (start > 0)
                    start--;
                if (end < intensities.Length)
                    end++;

                var timesNew = new float[end - start];
                var intensitiesNew = new float[end - start];
                Array.Copy(times, start, timesNew, 0, timesNew.Length);
                Array.Copy(intensities, start, intensitiesNew, 0, intensitiesNew.Length);
                chromData.FixChromatogram(timesNew, intensitiesNew);
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
                    if (intensities.Length < 10)
                        return false;
                    for (int i = 0; i < 10; i++)
                    {
                        if (intensities[i] != 0)
                            return false;
                    }
                    for (int i = intensities.Length - 1; i < 10; i++)
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
            // Check for interleaving zeros
            if (!HasThermZerosBug)
                return false;
            // Remove interleaving zeros
            foreach (var chromData in _listChromData)
            {
                var times = chromData.Times;
                var intensities = chromData.Intensities;
                var timesNew = new float[intensities.Length / 2];
                var intensitiesNew = new float[intensities.Length / 2];
                for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 1 : 0), iNew = 0; iNew < timesNew.Length; i += 2, iNew++)
                {
                    timesNew[iNew] = times[i];
                    intensitiesNew[iNew] = intensities[i];
                }
                chromData.FixChromatogram(timesNew, intensitiesNew);
            }
            return true;
        }

        private bool HasThermZerosBug
        {
            get
            {
                // Make sure the intensity arrays are not just empty to avoid
                // an infinite loop.
                bool seenData = false;
                // Check for interleaving zeros and non-zero values
                foreach (var chromData in _listChromData)
                {
                    var intensities = chromData.Intensities;
                    for (int i = (intensities.Length > 0 && intensities[0] == 0 ? 0 : 1); i < intensities.Length; i += 2)
                    {
                        if (intensities[i] != 0)
                            return false;
                        // Because WIFF files have lots of zeros
                        if (i < intensities.Length - 1 && intensities[i + 1] == 0)
                            return false;
                        seenData = true;
                    }
                }
                return seenData;
            }
        }

        public const double MIN_PERCENT_OF_MAX = 0.01;
        public const double MIN_PERCENT_OF_MAX_PRECURSOR = 0.001;

        private ChromDataPeakList FindCoelutingPeaks(ChromDataPeak dataPeakMax,
                                                     IList<ChromDataPeak> allPeaks)
        {
            CrawdadPeak peakMax = dataPeakMax.Peak;
            float areaMax = peakMax.Area;
            int centerMax = peakMax.TimeIndex;
            int startMax = peakMax.StartIndex;
            int endMax = peakMax.EndIndex;
            int widthMax = peakMax.Length;
            int deltaMax = (int)Math.Round(widthMax / 4.0, 0);
            var listPeaks = new ChromDataPeakList(dataPeakMax);

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

        public void SetBestPeak(ChromDataPeakList peakSet, PeptideChromDataPeak bestPeptidePeak)
        {
            if (peakSet != null)
            {
                // If the best peak by peptide matching is not already at the
                // head of the list, then move it there
                if (peakSet != _listPeakSets[0])
                {
                    _listPeakSets.Remove(peakSet);
                    _listPeakSets.Insert(0, peakSet);
                }
                // If there is a different best peptide peak, and it should have
                // the same retention time charachteristics, then reset the integration
                // boundaries of this peak set
                if (bestPeptidePeak != null && IsSameRT(bestPeptidePeak.Data))
                {
                    var peak = peakSet[0].Peak;
                    var peakBest = bestPeptidePeak.PeakGroup[0].Peak;
                    int offsetBest = bestPeptidePeak.Data.Offset;
                    int startIndex = Math.Max(0, GetIndex(peakBest.StartIndex + offsetBest));
                    int endIndex = Math.Min(Times.Length - 1, GetIndex(peakBest.EndIndex + offsetBest));

                    // In a peak set with mutiple charge states and light-heavy pairs, it is
                    // possible that a peak may not overlap with the best peak in its
                    // charge group.  If this is the case, and the best peak is completely
                    // outside the bounds of the current chromatogram, then insert an
                    // empty peak.
                    if (startIndex > endIndex)
                    {
                        var peakAdd = new ChromDataPeak(_listChromData[0], null);
                        _listPeakSets.Insert(0,
                            new ChromDataPeakList(peakAdd, _listChromData) { IsForcedIntegration = true });
                    }
                    // Otherwise, reset the best peak
                    else
                    {
                        peak.StartIndex = startIndex;
                        peak.EndIndex = endIndex;
                    }
                }
            }
            // If no peak was found at the peptide level for this data set,
            // but there is a best peak for the peptide
            else if (bestPeptidePeak != null && bestPeptidePeak.PeakGroup != null)
            {
                ChromDataPeak peakAdd = null;

                // If no overlapping peak was found for this precursor, then create
                // a peak with the same extents as the best peak.  This peak will
                // appear as missing, if Integrate All is not selected.
                var peakBest = bestPeptidePeak.PeakGroup[0].Peak;
                int offsetBest = bestPeptidePeak.Data.Offset;
                int startIndex = Math.Max(0, GetIndex(peakBest.StartIndex + offsetBest));
                int endIndex = Math.Min(Times.Length - 1, GetIndex(peakBest.EndIndex + offsetBest));
                if (startIndex < endIndex)
                {
                    var chromData = _listChromData[0];
                    peakAdd = new ChromDataPeak(chromData, chromData.CalcPeak(startIndex, endIndex));
                }

                // If there is still no peak to add, create an empty one
                if (peakAdd == null)
                {
                    peakAdd = new ChromDataPeak(_listChromData[0], null);
                }

                _listPeakSets.Insert(0,
                    new ChromDataPeakList(peakAdd, _listChromData) { IsForcedIntegration = true });
            }
        }

        private int GetIndex(int indexPeptide)
        {
            return indexPeptide - Offset;
        }

        private bool IsSameRT(ChromDataSet chromDataSet)
        {
            return DocNode.RelativeRT == RelativeRT.Matching &&
                chromDataSet.DocNode.RelativeRT == RelativeRT.Matching;
        }

        public override string ToString()
        {
            return Count > 0 ? _listChromData[0].ToString() : "empty";
        }
    }
}
