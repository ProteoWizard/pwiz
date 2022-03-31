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
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results.Crawdad;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromData : IComparable<ChromData>
    {
        /// <summary>
        /// Maximum number of peaks to label on a graph
        /// </summary>
        private const int MAX_PEAKS = 20;

        /// <summary>
        /// Minimum number of points required to determine a forced peak integration
        /// actually coelutes with the dominant peak.
        /// </summary>
        private const int MIN_COELUTION_RESCUE_POINTS = 5;

        /// <summary>
        /// Minimum value of R (Pearson's) to determine a forced peak integration
        /// actually coelutes with the dominant peak.
        /// </summary>
        private const double MIN_COELUTION_RESCUE_CORRELATION = 0.95;


        public ChromData(ChromKey key, int providerId)
        {
            Key = PrimaryKey = key;
            ProviderId = providerId;
            Peaks = new List<ChromPeak>();
            MaxPeakIndex = -1;
        }

        /// <summary>
        /// Clone the object, and create a new list of peaks, since the peaks are
        /// calculated on the write thread, and may be calculated differently for multiple
        /// transition groups.
        /// </summary>
        public ChromData CloneForWrite()
        {
            var clone = (ChromData)MemberwiseClone();
            clone.Peaks = new List<ChromPeak>(Peaks);
            return clone;
        }

        public bool Load(ChromDataProvider provider, Target modifiedSequence, Color peptideColor)
        {
            ChromExtra extra;
            TimeIntensities timeIntensities;
            bool result = provider.GetChromatogram(
                ProviderId, modifiedSequence, peptideColor,
                out extra, out timeIntensities);
            Extra = extra;
            TimeIntensities = RawTimeIntensities = timeIntensities;
            if (result && RawTimes.Any())
            {
                Key = Key.ChangeOptionalTimes(RawTimes.First(), RawTimes.Last());
            }
            return result;
        }

        public ChromData Truncate(double minTime, double maxTime)
        {
            if (!ReferenceEquals(Times, RawTimes))
            {
                throw new InvalidOperationException(@"Cannot truncate data set after interpolation");
            }
            if (Peaks.Count > 0)
            {
                throw new InvalidOperationException(@"Cannot truncate after peak detection");
            }
            // Avoid truncating chromatograms down to something less than half the window width.
            double minLength = (maxTime - minTime)/2;
            minTime = Math.Min(minTime, Times[Times.Count - 1] - minLength);
            maxTime = Math.Max(maxTime, Times[0] + minLength);
            int firstIndex = CollectionUtil.BinarySearch(Times, (float) minTime);
            if (firstIndex < 0)
            {
                firstIndex = ~firstIndex;
                firstIndex = Math.Max(firstIndex, 0);
            }
            int lastIndex = CollectionUtil.BinarySearch(Times, (float)maxTime);
            if (lastIndex < 0)
            {
                lastIndex = ~lastIndex + 1;
                lastIndex = Math.Min(lastIndex, Times.Count- 1);
            }
            if (firstIndex >= lastIndex)
            {
                return this;
            }
            if (firstIndex == 0 && lastIndex == Times.Count - 1)
            {
                return this;
            }
            var newChromData = new ChromData(Key, ProviderId)
            {
                Extra = Extra,
            };
            newChromData.TimeIntensities = newChromData.RawTimeIntensities =
                    RawTimeIntensities.Truncate(Times[firstIndex], Times[lastIndex]);
            newChromData.DocNode = DocNode;
            return newChromData;
        }

        public void FindPeaks(double[] retentionTimes, TimeIntervals timeIntervals, ExplicitRetentionTimeInfo explicitRT)
        {
            Finder = Crawdads.NewCrawdadPeakFinder();
            Finder.SetChromatogram(Times, Intensities);
            if (timeIntervals == null)
            {
                RawPeaks = Finder.CalcPeaks(MAX_PEAKS, TimesToIndices(retentionTimes));
            }
            else
            {
                var identifiedIndices = TimesToIndices(retentionTimes);
                var allPeaks = timeIntervals.Intervals.SelectMany(interval =>
                    FindIntervalPeaks(interval.Key, interval.Value, identifiedIndices));
                RawPeaks = allPeaks.OrderByDescending(peak => Tuple.Create(peak.Identified, peak.Area))
                    .Take(MAX_PEAKS).ToArray();
            }
            // Calculate smoothing for later use in extending the Crawdad peaks
            IntensitiesSmooth = ChromatogramInfo.SavitzkyGolaySmooth(Intensities.ToArray());

            // Accept only peaks within the user-provided RT window, if any
            if (explicitRT != null)
            {
                var winLow = (float)(explicitRT.RetentionTime - 0.5 * (explicitRT.RetentionTimeWindow ?? 0));
                var winHigh = winLow + (float)(explicitRT.RetentionTimeWindow ?? 0);
                RawPeaks = RawPeaks.Where(rp =>
                {
                    var t = Times[rp.TimeIndex];
                    return winLow <= t && t <= winHigh;
                });
            }
        }

        private IEnumerable<IFoundPeak> FindIntervalPeaks(float intervalStart, float intervalEnd, IList<int> identifiedIndices)
        {
            int startIndex = Times.BinarySearch(intervalStart);
            if (startIndex < 0)
            {
                startIndex = Math.Max(0, ~startIndex - 1);
            }

            int endIndex = Times.BinarySearch(intervalEnd);
            if (endIndex < 0)
            {
                endIndex = Math.Min(Times.Count - 1, ~endIndex);
            }

            if (endIndex <= startIndex)
            {
                yield break;
            }

            var times = Times.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
            var intensities = Intensities.Skip(startIndex).Take(endIndex - startIndex + 1).ToArray();
            var subIdentifiedIndices = identifiedIndices.Select(index => index - startIndex).ToArray();
            var subFinder = Crawdads.NewCrawdadPeakFinder();
            subFinder.SetChromatogram(times, intensities);
            foreach (var peak in subFinder.CalcPeaks(MAX_PEAKS, subIdentifiedIndices))
            {
                yield return Finder.GetPeak(peak.StartIndex + startIndex, peak.EndIndex + startIndex);
            }
        }

        public void SkipFindingPeaks(double[] retentionTimes)
        {
            Finder = Crawdads.NewCrawdadPeakFinder();
            Finder.SetChromatogram(Times, Intensities);
            RawPeaks = new IFoundPeak[0];
        }

        public void SetExplicitPeakBounds(ExplicitPeakBounds peakBounds)
        {
            Finder = Crawdads.NewCrawdadPeakFinder();
            Finder.SetChromatogram(Times, Intensities);
            if (peakBounds.IsEmpty)
            {
                RawPeaks = new IFoundPeak[0];
            }
            else
            {
                RawPeaks = new[] { Finder.GetPeak(TimeToIndex(peakBounds.StartTime), TimeToIndex(peakBounds.EndTime)) };
            }
        }

        private int[] TimesToIndices(double[] retentionTimes)
        {
            var indices = new int[retentionTimes.Length];
            for (int i = 0; i < retentionTimes.Length; i++)
                indices[i] = TimeToIndex(retentionTimes[i]);
            return indices;
        }

        private int TimeToIndex(double retentionTime)
        {
            var index = CollectionUtil.BinarySearch(Times, (float) retentionTime);
            if (index < 0)
            {
                index = ~index;
                if (index > 0 && index < Times.Count &&
                        retentionTime - Times[index - 1] < Times[index] - retentionTime)
                    index--;
            }
            return index;
        }

        private IPeakFinder Finder { get; set; }

        public PeakIntegrator MakePeakIntegrator(FullScanAcquisitionMethod acquisitionMethod,
            TimeIntervals timeIntervals)
        {
            return new PeakIntegrator(acquisitionMethod, timeIntervals, Key.Source, RawTimeIntensities, TimeIntensities,
                Finder);
        }

        public ChromKey Key { get; private set; }
        public ChromExtra Extra { get; private set; }
        public TransitionDocNode DocNode { get; set; }
        public int ProviderId { get; private set; }
        public TimeIntensities RawTimeIntensities { get; private set; }
        public TimeIntensities TimeIntensities { get; private set; }
        public IList<float> RawTimes { get{return RawTimeIntensities == null ? null : RawTimeIntensities.Times;} }
        public IList<float> RawIntensities { get { return RawTimeIntensities == null ? null : RawTimeIntensities.Intensities; } }
        public IList<float> RawMassErrors { get { return RawTimeIntensities == null ? null : RawTimeIntensities.MassErrors; } }
        public IList<int> RawScanIds { get { return RawTimeIntensities == null ? null : RawTimeIntensities.ScanIds; } }
        public IEnumerable<IFoundPeak> RawPeaks { get; private set; }

        public float RawCenterTime
        {
            get { return (float) (0.5*(RawTimes.Last() + RawTimes.First())); }
        }

        /// <summary>
        /// Many chromatograms are largely empty at one end or the other, this finds the time at the center of gravity,
        /// which is a cheap way of getting close to some big peak
        /// </summary>
        public float RawCenterOfGravityTime
        {
            get
            {
                var sum = RawIntensities.Sum();
                if (sum <= 0)
                    return RawCenterTime;
                var mi = RawTimes.Zip(RawIntensities, (d1, d2) => d1 * d2).Sum();
                return mi / sum;
            }
        }
        
        /// <summary>
        /// Time array shared by all transitions of a precursor, and on the
        /// same scale as all other precursors of a peptide.
        /// </summary>
        public IList<float> Times { get { return TimeIntensities.Times; } }

        /// <summary>
        /// Intensity array linear-interpolated to the shared time scale.
        /// </summary>
        public IList<float> Intensities { get { return TimeIntensities.Intensities; } }

        /// <summary>
        /// Intensities with Savitzky-Golay smoothing applied.
        /// </summary>
        public float[] IntensitiesSmooth { get; private set; }

        public IList<ChromPeak> Peaks { get; private set; }
        public int MaxPeakIndex { get; set; }
        public int OptimizationStep { get; set; }
        public ChromKey PrimaryKey { get; set; }

        public void FixChromatogram(float[] timesNew, float[] intensitiesNew, int[] scanIndexesNew)
        {
            TimeIntensities = RawTimeIntensities = new TimeIntensities(timesNew, intensitiesNew, null, scanIndexesNew);
        }

        public IFoundPeak CalcPeak(int startIndex, int endIndex)
        {
            return Finder.GetPeak(startIndex, endIndex);
        }

        public ChromPeak CalcChromPeak(IFoundPeak peakMax, ChromPeak.FlagValues flags, FullScanAcquisitionMethod acquisitionMethod, TimeIntervals timeIntervals, out IFoundPeak peak)
        {
            // Reintegrate all peaks to the max peak, even the max peak itself, since its boundaries may
            // have been extended from the Crawdad originals.
            if (peakMax == null)
            {
                peak = null;
                return ChromPeak.EMPTY;
            }

            var peakIntegrator = MakePeakIntegrator(acquisitionMethod, timeIntervals);
            var tuple = peakIntegrator.IntegrateFoundPeak(peakMax, flags);
            peak = tuple.Item2;
            return tuple.Item1;
        }

        public static bool AreCoeluting(IFoundPeak peakMax, IFoundPeak peak)
        {
            if (peak.Area == 0)
                return false;
            int start = peakMax.StartIndex, end = peakMax.EndIndex;
            if (peak.StartIndex != start || peak.EndIndex != end)
                return false;
            int len = peakMax.Length;
            if (len < MIN_COELUTION_RESCUE_POINTS)
                return false;
            double maxBaseline = Math.Min(peakMax.SafeGetIntensity(start), peakMax.SafeGetIntensity(end));
            double peakBaseline = Math.Min(peak.SafeGetIntensity(start), peak.SafeGetIntensity(end));
            double[] maxIntens = new double[len];
            double[] peakIntens = new double[len];
            bool seenUnequal = false;
            for (int i = 0; i < len; i++)
            {
                double maxI = maxIntens[i] = peakMax.SafeGetIntensity(i + start) - maxBaseline;
                double peakI = peakIntens[i] = peak.SafeGetIntensity(i + start) - peakBaseline;
                if (maxI != peakI)
                    seenUnequal = true;
            }
            // Avoid self-rescue
            if (!seenUnequal)
                return false;
            var statMax = new Statistics(maxIntens);
            var statPeak = new Statistics(peakIntens);
            double r = statMax.R(statPeak);
            if (r < MIN_COELUTION_RESCUE_CORRELATION)
                return false;

            return true;    // For debugging
        }

        public void Interpolate(float[] timesNew, bool inferZeros)
        {
            var chromatogramTimeIntensities = new TimeIntensities(RawTimes, RawIntensities, RawMassErrors, RawScanIds);
            TimeIntensities = chromatogramTimeIntensities.Interpolate(timesNew, inferZeros);
        }

        public int CompareTo(ChromData other)
        {
            if (null == other)
            {
                return 1;
            }
            var result = Key.CompareTo(other.Key);
            if (result == 0)
            {
                result = ProviderId.CompareTo(other.ProviderId);
            }
            return result;
        }

        public override string ToString()
        {
            return Key + string.Format(@" ({0})", ProviderId);
        }
    }

    internal sealed class ChromDataPeak : ITransitionPeakData<IDetailedPeakData>, IDetailedPeakData
    {
        private ChromPeak _chromPeak;
        private IFoundPeak _crawPeak;

        public ChromDataPeak(ChromData data, IFoundPeak peak)
        {
            Data = data;
            _crawPeak = peak;
        }

        public ChromData Data { get; private set; }
        public ChromPeak DataPeak {get { return _chromPeak; }
            set { _chromPeak = value; }
        }
        public IFoundPeak Peak { get { return _crawPeak; } }

        public TransitionDocNode NodeTran { get { return Data.DocNode; } }
        public IDetailedPeakData PeakData { get { return this; } }

        public override string ToString()
        {
            return Peak == null ? Data.Key.ToString() :
                String.Format(@"{0} - area = {1:F0}{2}{3}, start = {4}, end = {5}, rt = {6}-{7}>{8}",  // : For debugging
                    Data.Key, Peak.Area,
                    Peak.Identified ? @"+" : string.Empty,
                    DataPeak.IsForcedIntegration ? @"*" : string.Empty,
                    Peak.StartIndex, Peak.EndIndex,
                    Data.Times[Peak.StartIndex], Data.Times[Peak.EndIndex], Data.Times[Peak.TimeIndex]);
        }

        public ChromPeak CalcChromPeak(IFoundPeak peakMax, ChromPeak.FlagValues flags, FullScanAcquisitionMethod acquisitionMethod, TimeIntervals timeIntervals)
        {
            _chromPeak = Data.CalcChromPeak(peakMax, flags, acquisitionMethod, timeIntervals, out _crawPeak);
            return _chromPeak;
        }

        public void SetChromPeak(ChromPeak chromPeak)
        {
            _chromPeak = chromPeak;
        }

        public void ChangeChromPeak(ChromPeak chromPeak)
        {
            _chromPeak = chromPeak;
        }

        public bool IsIdentifiedTime(double[] retentionTimes)
        {
            double startTime = Data.Times[Peak.StartIndex];
            double endTime = Data.Times[Peak.EndIndex];

            return retentionTimes.Any(time => startTime <= time && time <= endTime);
        }

        public float RetentionTime
        {
            get { return _chromPeak.RetentionTime; }
        }

        public float StartTime
        {
            get { return _chromPeak.StartTime; }
        }

        public float EndTime
        {
            get { return _chromPeak.EndTime; }
        }

        public float Area
        {
            get { return _chromPeak.Area; }
        }

        public float BackgroundArea
        {
            get { return _chromPeak.BackgroundArea; }
        }

        public float Height
        {
            get { return _chromPeak.Height; }
        }

        public float Fwhm
        {
            get { return _chromPeak.Fwhm; }
        }

        public bool IsFwhmDegenerate
        {
            get { return _chromPeak.IsFwhmDegenerate; }
        }

        public bool IsEmpty
        {
            get { return _chromPeak.IsEmpty; }
        }

        public bool IsForcedIntegration
        {
            get { return _chromPeak.IsForcedIntegration; }
        }

        public PeakIdentification Identified
        {
            get { return _chromPeak.Identified; }
        }

        public bool? IsTruncated
        {
            get { return _chromPeak.IsTruncated; }
        }

        public int TimeIndex
        {
            get { return Peak != null ? Peak.TimeIndex : -1; }
        }

        public int EndIndex
        {
            get { return Peak != null ? Peak.EndIndex : -1; }
        }

        public int StartIndex
        {
            get { return Peak != null ? Peak.StartIndex : -1; }
        }

        public int Length
        {
            get { return Peak != null ? Peak.Length : 0; }
        }

        public bool IsLeftBound
        {
            get { return StartIndex == 0; }
        }

        public bool IsRightBound
        {
            get { return EndIndex == Times.Count - 1; }
        }

        public IList<float> Times
        {
            get { return Data.Times; }
        }

        public IList<float> RawTimes { get { return Data.RawTimes; } }

        public IList<float> Intensities
        {
            get { return Data.Intensities; }
        }

        public float? MassError
        {
            get { return _chromPeak.MassError; }
        }
    }

    /// <summary>
    /// A single set of peaks for all transitions in a transition group
    /// </summary>
    internal sealed class ChromDataPeakList : Collection<ChromDataPeak>, IList<ITransitionPeakData<IDetailedPeakData>>
    {
        public static readonly ChromDataPeakList EMPTY = new ChromDataPeakList();

        private ChromDataPeakList()
        {
            AcquisitionMethod = FullScanAcquisitionMethod.None;
        }
        
        public ChromDataPeakList(FullScanAcquisitionMethod acquisitionMethod, ChromDataPeak peak)
        {
            AcquisitionMethod = acquisitionMethod;
            Add(peak);
        }

        public ChromDataPeakList(FullScanAcquisitionMethod acquisitionMethod, ChromDataPeak peak, IEnumerable<ChromData> listChromData)
            : this(acquisitionMethod, peak)
        {
            foreach (var chromData in listChromData)
            {
                if (!ReferenceEquals(chromData, peak.Data))
                    Add(new ChromDataPeak(chromData, null));
            }
        }

        public ChromDataPeakList FilterToIndices(ISet<int> indexes)
        {
            if (indexes.Count == 0)
            {
                return EMPTY;
            }
            var newPeaks = this.Cast<ChromDataPeak>().Select((peak, index) => Tuple.Create(peak, index))
                .Where(tuple => indexes.Contains(tuple.Item2))
                .ToArray();
            var chromDataPeakList = new ChromDataPeakList(AcquisitionMethod, newPeaks[0].Item1);
            for (int i = 1; i < newPeaks.Length; i++)
            {
                chromDataPeakList.Add(newPeaks[i].Item1);
            }
            return chromDataPeakList;
        }

        public FullScanAcquisitionMethod AcquisitionMethod { get; }
        /// <summary>
        /// True if this set of peaks was created to satisfy forced integration
        /// rules.
        /// </summary>
        public bool IsForcedIntegration { get; set; }

        /// <summary>
        /// True if the peak contains a scan that has been identified as the
        /// peptide of interest by a peptide search engine.
        /// </summary>
        public bool IsIdentified { get; set; }

        /// <summary>
        /// True if the peak contains a time derived from retention time alignment
        /// of a scan that has been identified as the peptide of interest by a
        /// peptide search engine.
        /// </summary>
        public bool IsAlignedIdentified { get; set; }

        /// <summary>
        /// A count of peaks included in this peak group
        /// </summary>
        public int PeakCount { get; set; }

        /// <summary>
        /// Scores computed using available <see cref="DetailedPeakFeatureCalculator"/>
        /// implementations
        /// </summary>
        public float[] DetailScores { get; set; }
        
        /// <summary>
        /// Use proportion of total peaks found to avoid picking super small peaks
        /// in unrefined data
        /// </summary>
        public double PeakCountScore { get { return LegacyCountScoreCalc.GetPeakCountScore(PeakCount, Count); } }
        public double MS1Area { get; private set; }
        public double MS2Area { get; private set; }
        public double CombinedScore { get; private set; }
        public double MaxHeight { get; private set; }

        private const int MIN_TOLERANCE_LEN = 4;
        private const int MIN_TOLERANCE_SMOOTH_FWHM = 3;
        private const float FRACTION_FWHM_LEN = 0.5F;
        private const float DESCENT_TOL = 0.005f;
        private const float ASCENT_TOL = 0.50f;

        public bool IsAllMS1 { get { return Items.All(peak => IsMs1(peak.Data.Key.Source)); } }

        private bool IsMs1(ChromSource source)
        {
            return source != ChromSource.fragment; // TODO: source == ChromSource.ms1 || source == ChromSource.sim;
        }

        public double TotalArea
        {
            get
            {
                if (FullScanAcquisitionMethod.DDA.Equals(AcquisitionMethod) || IsAllMS1)
                {
                    return MS1Area;
                }
                return MS2Area;
            }
        }

        public void SetIdentified(double[] retentionTimes, bool isAlignedTimes)
        {
            IsIdentified = Count > 0 && this[0].IsIdentifiedTime(retentionTimes);
            IsAlignedIdentified = IsIdentified && isAlignedTimes;
            UpdateCombinedScore();
        }

        public bool Extend()
        {
            // Only extend for peak groups with at least one peak
            if (Count < 1)
                return true;

            var peakPrimary = this[0];

            // Look a number of steps dependent on the width of the peak, since interval width
            // may vary.
            int toleranceLen = Math.Max(MIN_TOLERANCE_LEN, (int)Math.Round(peakPrimary.Peak.Fwhm * FRACTION_FWHM_LEN));
            int startIndex = peakPrimary.Peak.StartIndex;
            int endIndex = peakPrimary.Peak.EndIndex;
            peakPrimary.Peak.ResetBoundaries(ExtendBoundary(peakPrimary, startIndex, endIndex, -1, toleranceLen),
                                             ExtendBoundary(peakPrimary, endIndex, startIndex, 1, toleranceLen));
            // Convex peaks can result in single point peaks after boundaries are extended
            return peakPrimary.Peak.StartIndex < peakPrimary.Peak.EndIndex;
        }

        private int ExtendBoundary(ChromDataPeak peakPrimary, int indexBoundary, int indexOpposite,
                                   int increment, int toleranceLen)
        {
            int indexAdjusted = indexBoundary;
            if (peakPrimary.Peak.Fwhm >= MIN_TOLERANCE_SMOOTH_FWHM)
            {
                indexAdjusted = ExtendBoundary(peakPrimary, false, indexBoundary, increment, toleranceLen);
            }
            // Because smoothed data can have a tendency to reach baseline one
            // interval sooner than the raw data, do a final check to choose the
            // boundary correctly for the raw data.
            indexAdjusted = RetractBoundary(peakPrimary, true, indexAdjusted, -increment);
            indexAdjusted = ExtendBoundary(peakPrimary, true, indexAdjusted, increment, toleranceLen);
            // Avoid backing up over the original boundary
            int indexLimit = (indexBoundary + indexOpposite) / 2;
            indexAdjusted = increment > 0
                                ? Math.Max(indexLimit, indexAdjusted)
                                : Math.Min(indexLimit, indexAdjusted);
            return indexAdjusted;
        }

        private int ExtendBoundary(ChromDataPeak peakPrimary, bool useRaw, int indexBoundary, int increment, int toleranceLen)
        {
            float maxIntensity, deltaIntensity;
            GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);

            int lenIntensities = peakPrimary.Data.Intensities.Count;
            // Look for a descent proportional to the height of the peak.  Because, SRM data is
            // so low noise, just looking for any descent can lead to boundaries very far away from
            // the peak.
            float height = peakPrimary.Peak.Height;
            double minDescent = height * DESCENT_TOL;
            // Put a limit on how high intensity can go before the search is terminated
            double maxHeight = ((height - maxIntensity) * ASCENT_TOL) + maxIntensity;

            // Extend the index in the direction of the increment
            for (int i = indexBoundary + increment;
                 i >= 0 && i < lenIntensities && Math.Abs(indexBoundary - i) < toleranceLen;
                 i += increment)
            {
                float maxIntensityCurrent, deltaIntensityCurrent;
                GetIntensityMetrics(i, useRaw, out maxIntensityCurrent, out deltaIntensityCurrent);

                // If intensity goes above the maximum, stop looking
                if (maxIntensityCurrent > maxHeight)
                    break;

                // If descent greater than tolerance, step until it no longer is
                while (maxIntensity - maxIntensityCurrent > minDescent)
                {
                    indexBoundary += increment;
                    if (indexBoundary == i)
                        maxIntensity = maxIntensityCurrent;
                    else
                        GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);
                }
            }

            return indexBoundary;
        }

        private int RetractBoundary(ChromDataPeak peakPrimary, bool useRaw, int indexBoundary, int increment)
        {
            float maxIntensity, deltaIntensity;
            GetIntensityMetrics(indexBoundary, useRaw, out maxIntensity, out deltaIntensity);

            int lenIntensities = peakPrimary.Data.Intensities.Count;
            // Look for a descent proportional to the height of the peak.  Because, SRM data is
            // so low noise, just looking for any descent can lead to boundaries very far away from
            // the peak.
            float height = peakPrimary.Peak.Height;
            double maxAscent = height * DESCENT_TOL;
            // Put a limit on how high intensity can go before the search is terminated
            double maxHeight = ((height - maxIntensity) * ASCENT_TOL) + maxIntensity;

            // Extend the index in the direction of the increment
            for (int i = indexBoundary + increment; i > 0 && i < lenIntensities - 1; i += increment)
            {
                float maxIntensityCurrent, deltaIntensityCurrent;
                GetIntensityMetrics(i, useRaw, out maxIntensityCurrent, out deltaIntensityCurrent);

                // If intensity goes above the maximum, stop looking
                if (maxIntensityCurrent > maxHeight || maxIntensityCurrent - maxIntensity > maxAscent)
                    break;

                maxIntensity = maxIntensityCurrent;
                indexBoundary = i;
            }

            return indexBoundary;
        }

        private void GetIntensityMetrics(int i, bool useRaw, out float maxIntensity, out float deltaIntensity)
        {
            var peakData = this[0];
            var intensities = (useRaw ? peakData.Data.Intensities
                                      : peakData.Data.IntensitiesSmooth);
            float minIntensity = maxIntensity = intensities[i];
            for (int j = 1; j < Count; j++)
            {
                peakData = this[j];
                // If this transition doesn't have a measured peak, then skip it.
                if (peakData.Peak == null)
                    continue;

                float currentIntensity = (useRaw ? peakData.Data.Intensities[i]
                                                 : peakData.Data.IntensitiesSmooth[i]);
                if (currentIntensity > maxIntensity)
                    maxIntensity = currentIntensity;
                else if (currentIntensity < minIntensity)
                    minIntensity = currentIntensity;
            }
            deltaIntensity = maxIntensity - minIntensity;
        }

        private void AddPeak(ChromDataPeak dataPeak)
        {
            // Avoid using optimization data from other optimization steps in scoring
            if (dataPeak.Peak != null && (PeakCount == 0 || this[0].Data.OptimizationStep == dataPeak.Data.OptimizationStep))
            {
                MaxHeight = Math.Max(MaxHeight, dataPeak.Peak.Height);
                double area = dataPeak.Peak.Area;
                if (IsMs1(dataPeak.Data.Key.Source))
                    MS1Area += area;
                else
                    MS2Area += area;
                PeakCount++;
            }
            UpdateCombinedScore();
        }

        private void SubtractPeak(ChromDataPeak dataPeak)
        {
            // Avoid using optimization data in scoring
            if (dataPeak.Peak != null && (PeakCount == 0 || this[0].Data.OptimizationStep == dataPeak.Data.OptimizationStep))
            {
                double area = dataPeak.Peak.Area;
                PeakCount--;
                if (IsMs1(dataPeak.Data.Key.Source))
                    MS1Area = (PeakCount == 0) ? 0 : MS1Area - area;
                else
                    MS2Area = (PeakCount == 0) ? 0 : MS2Area - area;
            }
            UpdateCombinedScore();
        }

        private void UpdateCombinedScore()
        {
            CombinedScore = ScorePeak(TotalArea, PeakCountScore, IsIdentified);
        }

        public static double ScorePeak(double totalArea, double peakCount, bool isIdentified)
        {
            double logUnforcedArea = LegacyLogUnforcedAreaCalc.Score(totalArea, 0);
            return LegacyScoringModel.Score(logUnforcedArea, peakCount, 0, isIdentified ? 1 : 0);
        }

        protected override void ClearItems()
        {
            PeakCount = 0;
            MS1Area = 0;
            MS2Area = 0;
            CombinedScore = 0;
            MaxHeight = 0;

            base.ClearItems();
        }

        protected override void InsertItem(int index, ChromDataPeak item)
        {
            base.InsertItem(index, item);
            AddPeak(item);
        }

        protected override void RemoveItem(int index)
        {
            var peak = this[index];
            base.RemoveItem(index);
            SubtractPeak(peak);
        }

        protected override void SetItem(int index, ChromDataPeak item)
        {
            var peak = this[index];
            base.SetItem(index, item);
            SubtractPeak(peak);
            AddPeak(item);
        }

        #region Implement read-only IList<ITransitionPeakData<IDetailedPeakData>> for peak scoring
        
        IEnumerator<ITransitionPeakData<IDetailedPeakData>> IEnumerable<ITransitionPeakData<IDetailedPeakData>>.GetEnumerator()
        {
            return GetEnumerator();
        }

        bool ICollection<ITransitionPeakData<IDetailedPeakData>>.IsReadOnly { get { return true; } }

        int IList<ITransitionPeakData<IDetailedPeakData>>.IndexOf(ITransitionPeakData<IDetailedPeakData> item)
        {
            return IndexOf((ChromDataPeak)item);
        }

        ITransitionPeakData<IDetailedPeakData> IList<ITransitionPeakData<IDetailedPeakData>>.this[int index]
        {
            get { return this[index]; }
            set { throw new InvalidOperationException(); }
        }

        void ICollection<ITransitionPeakData<IDetailedPeakData>>.CopyTo(ITransitionPeakData<IDetailedPeakData>[] array, int arrayIndex)
        {
            foreach (var pd in this)
                array[arrayIndex++] = pd;
        }

        void ICollection<ITransitionPeakData<IDetailedPeakData>>.Add(ITransitionPeakData<IDetailedPeakData> item)
        { throw new InvalidOperationException(); }
        bool ICollection<ITransitionPeakData<IDetailedPeakData>>.Contains(ITransitionPeakData<IDetailedPeakData> item)
        { throw new InvalidOperationException(); }
        bool ICollection<ITransitionPeakData<IDetailedPeakData>>.Remove(ITransitionPeakData<IDetailedPeakData> item)
        { throw new InvalidOperationException(); }
        void IList<ITransitionPeakData<IDetailedPeakData>>.Insert(int index, ITransitionPeakData<IDetailedPeakData> item)
        { throw new InvalidOperationException(); }

        #endregion
    }
}
