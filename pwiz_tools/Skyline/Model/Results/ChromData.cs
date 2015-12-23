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
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    internal sealed class ChromData
    {
        /// <summary>
        /// Maximum number of peaks to label on a graph
        /// </summary>
        private const int MAX_PEAKS = 20;

        public ChromData(ChromKey key, int providerId)
        {
            Key = PrimaryKey = key;
            ProviderId = providerId;
            Peaks = new List<ChromPeak>();
            MaxPeakIndex = -1;
        }

        /// <summary>
        /// Clone the object, and create a new list of peaks, since the peaks are
        /// calculated on the write thread, and may be calulated differently for multiple
        /// transition groups.
        /// </summary>
        public ChromData CloneForWrite()
        {
            var clone = (ChromData)MemberwiseClone();
            clone.Peaks = new List<ChromPeak>(Peaks);
            return clone;
        }

        public bool Load(ChromDataProvider provider, string modifiedSequence, Color peptideColor)
        {
            ChromExtra extra;
            float[] times, intensities;
            float[] massErrors;
            int[] scanIds;
            bool result = provider.GetChromatogram(
                ProviderId, modifiedSequence, peptideColor,
                out extra, out times, out scanIds, out intensities, out massErrors);
            Extra = extra;
            RawTimes = Times = times;
            RawIntensities = Intensities = intensities;
            RawMassErrors = massErrors;
            RawScanIds = ScanIndexes = scanIds;
            return result;
        }

        public ChromData Truncate(double minTime, double maxTime)
        {
            if (!ReferenceEquals(Times, RawTimes))
            {
                throw new InvalidOperationException("Cannot truncate data set after interpolation"); // Not L10N
            }
            if (Peaks.Count > 0)
            {
                throw new InvalidOperationException("Cannot truncate after peak detection"); // Not L10N
            }
            // Avoid truncating chromatograms down to something less than half the window width.
            double minLength = (maxTime - minTime)/2;
            minTime = Math.Min(minTime, Times[Times.Length - 1] - minLength);
            maxTime = Math.Max(maxTime, Times[0] + minLength);
            int firstIndex = Array.BinarySearch(Times, (float) minTime);
            if (firstIndex < 0)
            {
                firstIndex = ~firstIndex;
                firstIndex = Math.Max(firstIndex, 0);
            }
            int lastIndex = Array.BinarySearch(Times, (float) maxTime);
            if (lastIndex < 0)
            {
                lastIndex = ~lastIndex + 1;
                lastIndex = Math.Min(lastIndex, Times.Length - 1);
            }
            if (firstIndex >= lastIndex)
            {
                return this;
            }
            if (firstIndex == 0 && lastIndex == Times.Length - 1)
            {
                return this;
            }
            var newChromData = new ChromData(Key, ProviderId)
            {
                Extra = Extra,
            };
            newChromData.Times = newChromData.RawTimes = SubArray(RawTimes, firstIndex, lastIndex);
            newChromData.ScanIndexes = newChromData.RawScanIds = SubArray(RawScanIds, firstIndex, lastIndex);
            newChromData.Intensities = newChromData.RawIntensities = SubArray(RawIntensities, firstIndex, lastIndex);
            newChromData.RawMassErrors = SubArray(RawMassErrors, firstIndex, lastIndex);
            newChromData.DocNode = DocNode;
            return newChromData;
        }

        private T[] SubArray<T>(T[] array, int firstIndex, int lastIndex)
        {
            if (null == array)
            {
                return null;
            }
            T[] result = new T[lastIndex - firstIndex + 1];
            Array.Copy(array, firstIndex, result, 0, result.Length);
            return result;
        }

        public void FindPeaks(double[] retentionTimes, bool requireDocNode)
        {
            Finder = new CrawdadPeakFinder();
            Finder.SetChromatogram(Times, Intensities);
            if (requireDocNode && DocNode == null)
                RawPeaks = new CrawdadPeak[0];
            else
            {
                RawPeaks = Finder.CalcPeaks(MAX_PEAKS, TimesToIndices(retentionTimes));
                // Calculate smoothing for later use in extending the Crawdad peaks
                IntensitiesSmooth = ChromatogramInfo.SavitzkyGolaySmooth(Intensities);
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
            var index = Array.BinarySearch(Times, (float) retentionTime);
            if (index < 0)
            {
                index = ~index;
                if (index > 0 && index < Times.Length &&
                        retentionTime - Times[index - 1] < Times[index] - retentionTime)
                    index--;
            }
            return index;
        }

        private CrawdadPeakFinder Finder { get; set; }

        public ChromKey Key { get; private set; }
        public ChromExtra Extra { get; private set; }
        public TransitionDocNode DocNode { get; set; }
        public int ProviderId { get; private set; }
        public float[] RawTimes { get; private set; }
        private float[] RawIntensities { get; set; }
        private float[] RawMassErrors { get; set; }
        private int[] RawScanIds { get; set; }
        public IEnumerable<CrawdadPeak> RawPeaks { get; private set; }

        public float RawCenterTime
        {
            get { return (float) (0.5*(RawTimes.Last() + RawTimes.First())); }
        }

        /// <summary>
        /// Time array shared by all transitions of a precursor, and on the
        /// same scale as all other precursors of a peptide.
        /// </summary>
        public float[] Times { get; private set; }

        /// <summary>
        /// Intensity array linear-interpolated to the shared time scale.
        /// </summary>
        public float[] Intensities { get; private set; }

        /// <summary>
        /// Intensities with Savitzky-Golay smoothing applied.
        /// </summary>
        public float[] IntensitiesSmooth { get; private set; }

        /// <summary>
        /// Mass error array averaged base on interpolated intensities
        /// to the shared time scale.  Defered setting backing variable
        /// to avoid doing unnecessary work when interpolation is necessary.
        /// When no interpolation is necessary, field will be calculated on
        /// the first access and stored.
        /// </summary>
        public short[] MassErrors10X
        {
            get
            {
                if (_massErrors10X == null && RawMassErrors != null)
                {
                    int len = RawMassErrors.Length;
                    _massErrors10X = new short[len];
                    for (int i = 0; i < len; i++)
                        _massErrors10X[i] = ChromPeak.To10x(RawMassErrors[i]);
                }
                return _massErrors10X;
            }
            private set { _massErrors10X = value; }
        }
        private short[] _massErrors10X;

        public int[] ScanIndexes { get; private set; }

        public IList<ChromPeak> Peaks { get; private set; }
        public int MaxPeakIndex { get; set; }
        public int OptimizationStep { get; set; }
        public ChromKey PrimaryKey { get; set; }

        public void FixChromatogram(float[] timesNew, float[] intensitiesNew, int[] scanIndexesNew)
        {
            RawTimes = Times = timesNew;
            RawIntensities = Intensities = intensitiesNew;
            RawScanIds = ScanIndexes = scanIndexesNew;
        }

        public CrawdadPeak CalcPeak(int startIndex, int endIndex)
        {
            return Finder.GetPeak(startIndex, endIndex);
        }

        public ChromPeak CalcChromPeak(CrawdadPeak peakMax, ChromPeak.FlagValues flags, out CrawdadPeak peak)
        {
            // Reintegrate all peaks to the max peak, even the max peak itself, since its boundaries may
            // have been extended from the Crawdad originals.
            if (peakMax == null)
            {
                peak = null;
                return ChromPeak.EMPTY;
            }

            peak = CalcPeak(peakMax.StartIndex, peakMax.EndIndex);
            return new ChromPeak(Finder, peak, flags, Times, Intensities, MassErrors10X);
        }

        public void Interpolate(float[] timesNew, double intervalDelta, bool inferZeros)
        {
            if (timesNew.Length == 0)
                return;

            var timesMeasured = RawTimes;
            var intensMeasured = RawIntensities;
            var massErrorsMeasured = RawMassErrors;

            var intensNew = new List<float>();
            var massErrorsNew = massErrorsMeasured != null ? new List<short>() : null;

            int iTime = 0;
            double timeLast = timesNew[0];
            double intenLast = 0;
            double massErrorLast = 0;
            if (!inferZeros && intensMeasured.Length != 0)
            {
                intenLast = intensMeasured[0];
                if (massErrorsMeasured != null)
                    massErrorLast = massErrorsMeasured[0];
            }
            for (int i = 0; i < timesMeasured.Length && iTime < timesNew.Length; i++)
            {
                double intenNext;
                float time = timesMeasured[i];
                float inten = intensMeasured[i];
                double totalInten = inten;
                double massError = 0;
                if (massErrorsMeasured != null)
                    massError = massErrorsMeasured[i];

                // Continue enumerating points until one is encountered
                // that has a greater time value than the point being assigned.
                while (i < timesMeasured.Length - 1 && time < timesNew[iTime])
                {
                    i++;
                    time = timesMeasured[i];
                    inten = intensMeasured[i];

                    if (massErrorsMeasured != null)
                    {
                        // Average the mass error in these points weigthed by intensity
                        // into the next mass error value
                        totalInten += inten;
                        // TODO: Figure out whether this is an appropriate estimation method
                        massError += (massErrorsMeasured[i] - massError)*inten/totalInten;
                    }
                }

                if (i >= timesMeasured.Length)
                    break;

                // If the next measured intensity is more than the new delta
                // away from the intensity being assigned, then interpolate
                // the next point toward zero, and set the last intensity to
                // zero.
                if (inferZeros && intenLast > 0 && timesNew[iTime] + intervalDelta < time)
                {
                    intenNext = intenLast + (timesNew[iTime] - timeLast) * (0 - intenLast) / (timesNew[iTime] + intervalDelta - timeLast);
                    intensNew.Add((float)intenNext);
                    AddMassError(massErrorsNew, massError);
                    timeLast = timesNew[iTime++];
                    intenLast = 0;
                }

                if (inferZeros)
                {
                    // If the last intensity was zero, and the next measured time
                    // is more than a delta away, assign zeros until within a
                    // delta of the measured intensity.
                    while (intenLast == 0 && iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                    {
                        intensNew.Add(0);
                        AddMassError(massErrorsNew, massError);
                        timeLast = timesNew[iTime++];
                    }
                }
                else
                {
                    // Up to just before the current point, project the line from the
                    // last point to the current point at each interval.
                    while (iTime < timesNew.Length && timesNew[iTime] + intervalDelta < time)
                    {
                        intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                        intensNew.Add((float)intenNext);
                        AddMassError(massErrorsNew, massError);
                        iTime++;
                    }
                }

                if (iTime >= timesNew.Length)
                    break;

                // Interpolate from the last intensity toward the measured
                // intenisty now within a delta of the point being assigned.
                if (time == timeLast)
                    intenNext = intenLast;
                else
                    intenNext = intenLast + (timesNew[iTime] - timeLast) * (inten - intenLast) / (time - timeLast);
                intensNew.Add((float)intenNext);
                massErrorLast = AddMassError(massErrorsNew, massError);
                iTime++;
                intenLast = inten;
                timeLast = time;
            }

            // Fill any unassigned intensities with zeros.
            while (intensNew.Count < timesNew.Length)
            {
                intensNew.Add(0);
                AddMassError(massErrorsNew, massErrorLast);
            }

            // Reassign times and intensities.
            Times = timesNew;
            Intensities = intensNew.ToArray();
            MassErrors10X = massErrorsNew != null ? massErrorsNew.ToArray() : null;

            // Replicate scan ids to match new times.
            if (RawScanIds != null)
            {
                ScanIndexes = new int[timesNew.Length];
                int rawIndex = 0;
                for (int i = 0; i < timesNew.Length; i++)
                {
                    // Choose the RawScanId corresponding to the closest RawTime to the new time.
                    float newTime = Times[i];
                    while (rawIndex < RawTimes.Length && RawTimes[rawIndex] <= newTime)
                        rawIndex++;
                    if (rawIndex >= RawTimes.Length)
                        rawIndex--;
                    if (rawIndex > 0 && newTime - RawTimes[rawIndex - 1] < RawTimes[rawIndex] - newTime)
                        rawIndex--;
                    ScanIndexes[i] = RawScanIds[rawIndex];
                }
            }
        }

        private static short AddMassError(ICollection<short> massErrors10X, double massError)
        {
            if (massErrors10X != null)
            {
                short massError10X = ChromPeak.To10x(massError);
                massErrors10X.Add(massError10X);
                return massError10X;
            }
            return 0;
        }
    }

    internal sealed class ChromDataPeak : ITransitionPeakData<IDetailedPeakData>, IDetailedPeakData
    {
        private ChromPeak _chromPeak;
        private CrawdadPeak _crawPeak;

        public ChromDataPeak(ChromData data, CrawdadPeak peak)
        {
            Data = data;
            _crawPeak = peak;
        }

        public ChromData Data { get; private set; }
        public ChromPeak DataPeak {get { return _chromPeak; }}
        public CrawdadPeak Peak { get { return _crawPeak; } }

        public TransitionDocNode NodeTran { get { return Data.DocNode; } }
        public IDetailedPeakData PeakData { get { return this; } }

        public override string ToString()
        {
            return Peak == null ? Data.Key.ToString() :
                String.Format("{0} - area = {1:F0}{2}{3}, start = {4}, end = {5}, rt = {6}-{7}",  // Not L10N : For debugging
                    Data.Key, Peak.Area,
                    Peak.Identified ? "+" : string.Empty, // Not L10N
                    DataPeak.IsForcedIntegration ? "*" : string.Empty, // Not L10N
                    Peak.StartIndex, Peak.EndIndex,
                    Data.Times[Peak.StartIndex], Data.Times[Peak.EndIndex]);
        }

        public ChromPeak CalcChromPeak(CrawdadPeak peakMax, ChromPeak.FlagValues flags)
        {
            _chromPeak = Data.CalcChromPeak(peakMax, flags, out _crawPeak);
            return _chromPeak;
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
            get { return EndIndex == Times.Length - 1; }
        }

        public float[] Times
        {
            get { return Data.Times; }
        }

        public float[] Intensities
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
        }
        
        public ChromDataPeakList(ChromDataPeak peak)
        {
            Add(peak);
        }

        public ChromDataPeakList(ChromDataPeak peak, IEnumerable<ChromData> listChromData)
            : this(peak)
        {
            foreach (var chromData in listChromData)
            {
                if (!ReferenceEquals(chromData, peak.Data))
                    Add(new ChromDataPeak(chromData, null));
            }
        }

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

        public double TotalArea { get { return IsAllMS1 ? MS1Area : MS2Area; } }

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

            int lenIntensities = peakPrimary.Data.Intensities.Length;
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

            int lenIntensities = peakPrimary.Data.Intensities.Length;
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
