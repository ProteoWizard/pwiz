/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// A derivation of ProgressStatus that carries information about partially-loaded chromatograms
    /// so that the incomplete chromatogram graph can be displayed while the data file is still being
    /// imported.
    /// </summary>
    public class ChromatogramLoadingStatus : ProgressStatus
    {
        // Resolution of binning.  Set too low, this will consume a lot of memory.  Set too high, the graph
        // gets coarse and looks less like Skyline's other chromatogram graphs.
        public const double TIME_RESOLUTION = 0.2;    // 12 seconds
        public const float INTENSITY_THRESHOLD_PERCENT = 0.002f;

        public ChromatogramLoadingStatus(string message) :
            base(message)
        {
            Transitions = new TransitionData();
        }

        public TransitionData Transitions { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public bool Importing { get; private set; }

        public ChromatogramLoadingStatus ChangeFilePath(MsDataFileUri filePath)
        {
            return ChangeProp(ImClone(this), s => s.FilePath = filePath);
        }

        public ChromatogramLoadingStatus ChangeImporting(bool importing)
        {
            return ChangeProp(ImClone(this), s => s.Importing = importing);
        }

        /// <summary>
        /// Binned chromatogram data appropriate for display in the chromatogram graph.
        /// </summary>
        public class TransitionData
        {
            private List<Peak[]> _peaks;
            private readonly List<Peak> _accumulatePeaks = new List<Peak>();
            private DateTime _lastDisplayTime;
            private List<Peak> _finishedPeaks = new List<Peak>();
            private static readonly int SOURCE_INDEX_COUNT = Helpers.CountEnumValues<ChromSource>() - 1;
            private float _maxImportedIntensity;

            public bool FromCache { get; set; }
            public float MaxIntensity { get; set; }
            public float MaxRetentionTime { get; set; }
            public bool MaxRetentionTimeKnown { get; set; }
            public float CurrentTime { get; set; }
            public bool Progressive { get; set; }

            public int FilterCount
            {
                set
                {
                    // Allocate list for holding partially constructed peaks.
                    _peaks = new List<Peak[]>(value > 0 ? value : 1000);
                    MaxRetentionTime = 0.0f;
                    MaxIntensity = 0.0f;
                    CurrentTime = 0.0f;
                    Progressive = false;
                    lock (this)
                    {
                        _finishedPeaks = new List<Peak>();
                    }
                    _maxImportedIntensity = 0.0f;
                }
            }

            // Accumulate points in bins so that there isn't an overwhelming amount of data to display in the graph.
            private static float GetTimeBin(float time)
            {
                return (float)(Math.Round(time / TIME_RESOLUTION) * TIME_RESOLUTION);
            }

            private static int GetBinIndex(float time)
            {
                return (int) Math.Round(time/TIME_RESOLUTION);
            }

            /// <summary>
            /// Add transition points (partial data for multiple transitions) to AllChromatogramsGraph.
            /// </summary>
            public void Add(string modifiedSequence, int filterIndex, ChromSource chromSource, float time, float[] intensities)
            {
                if (!Progressive)
                    MaxRetentionTime = Math.Max(MaxRetentionTime, time);
                double intensity = 0;
                for (int i = 0; i < intensities.Length; i++)
                    intensity += intensities[i];
                if (intensity <= 0.0)
                    return;
                float timeBin = GetTimeBin(time);

                // Create transition list for this filter.
                int sourceIndex = (int) chromSource;
                while (filterIndex >= _peaks.Count)
                    _peaks.Add(new Peak[SOURCE_INDEX_COUNT]);
                var peak = _peaks[filterIndex][sourceIndex];
                if (peak != null)
                {
                    // Add point to current time bin.
                    int lastIndex = peak.Times.Count - 1;
                    float lastTime = peak.Times[lastIndex];
                    float lastIntensity = peak.Intensities[lastIndex];
                    if (timeBin <= lastTime)
                    {
                        peak.PointCount++;
                        double newAvg = lastIntensity + (intensity - lastIntensity) / peak.PointCount;
                        peak.Intensities[lastIndex] = (float)newAvg;
                        return;
                    }

                    _maxImportedIntensity = Math.Max(_maxImportedIntensity, lastIntensity);

                    // Send to display if we skipped a time bin, or if we have a long enough peak that has dipped sufficiently.
                    bool skippedBin = GetBinIndex(timeBin) > GetBinIndex(lastTime) + 1;
                    if (skippedBin ||
                        (lastTime - peak.Times[0] > TIME_RESOLUTION * 10 && (_maxImportedIntensity == 0.0f || lastIntensity < _maxImportedIntensity / 10)))
                    {
                        // Find highest intensity/time for this peak.
                        for (int i = 0; i < peak.Intensities.Count; i++)
                        {
                            float peakIntensity = peak.Intensities[i];
                            if (peak.PeakIntensity < peakIntensity)
                            {
                                peak.PeakIntensity = peakIntensity;
                                peak.PeakTime = peak.Times[i];
                            }
                        }

                        // Ignore the peak if its intensity is below a reasonable threshold.
                        if (peak.PeakIntensity > _maxImportedIntensity * INTENSITY_THRESHOLD_PERCENT)
                        {
                            peak.NoTrailingZero = !skippedBin;
                            DisplayPeak(peak);
                        }

                        peak = null;
                    }
                }

                // Create a new time bin.
                if (peak == null)
                {
                    _peaks[filterIndex][sourceIndex] = peak = new Peak(modifiedSequence, filterIndex, false);
                    peak.PointCount = 1;
                }
                peak.Times.Add(timeBin);
                peak.Intensities.Add((float)intensity);
            }

            /// <summary>
            /// Add a complete transition to AllChromatogramsGraph.
            /// </summary>
            public void AddTransition(string modifiedSequence, int index, int rank, float[] times, float[] intensities)
            {
                if (rank == 0 || times.Length == 0)
                    return;

                float maxTime = times[times.Length - 1];
                if (MaxRetentionTime < maxTime)
                    MaxRetentionTime = maxTime;
                float thresholdIntensity = _maxImportedIntensity * INTENSITY_THRESHOLD_PERCENT;

                // Find start of transition above the threshold intensity value.
                int startIndex = 0;
                while (true)
                {
                    while (startIndex < intensities.Length && intensities[startIndex] < thresholdIntensity)
                        startIndex++;
                    if (startIndex == intensities.Length)
                        return;

                    // Find end of transition below the threshold intensity value.
                    int endIndex = startIndex + 1;
                    while (endIndex < intensities.Length && intensities[endIndex] >= thresholdIntensity)
                        endIndex++;

                    AddTransition(modifiedSequence, index, times, intensities, startIndex, endIndex);

                    startIndex = endIndex;
                }
            }

            private void AddTransition(string modifiedSequence, int index, float[] times, float[] intensities, int startIndex, int endIndex)
            {
                MaxRetentionTime = Math.Max(MaxRetentionTime, times[times.Length - 1]);

                var peak = new Peak(modifiedSequence, index, true);
                float lastTimeBin = GetTimeBin(times[startIndex]);
                double binIntensity = 0.0;
                int binSampleCount = 0;

                // Average intensity values into each time bin.
                for (int i = startIndex; ; i++)
                {
                    float timeBin = 0.0f;
                    if (i < endIndex)
                    {
                        timeBin = GetTimeBin(times[i]);
                        if (timeBin == lastTimeBin)
                        {
                            binIntensity += intensities[i];
                            binSampleCount++;
                            continue;
                        }
                    }

                    peak.Times.Add(lastTimeBin);
                    float averageIntensity = (float)(binIntensity / binSampleCount);
                    peak.Intensities.Add(averageIntensity);
                    if (peak.PeakIntensity < averageIntensity)
                    {
                        peak.PeakIntensity = averageIntensity;
                        peak.PeakTime = lastTimeBin;
                    }

                    if (i == endIndex)
                        break;

                    lastTimeBin = timeBin;
                    binIntensity = intensities[i];
                    binSampleCount = 1;
                }

                // Update max intensity.
                if (_maxImportedIntensity < peak.PeakIntensity)
                    _maxImportedIntensity = peak.PeakIntensity;

                // Add to list of peaks for display.
                DisplayPeak(peak);
            }

            /// <summary>
            /// Add peak to display list.
            /// </summary>
            private void DisplayPeak(Peak peak)
            {
                _accumulatePeaks.Add(peak);

                // Has enough time elapsed to send this to the display?
                var now = DateTime.Now;
                if ((now - _lastDisplayTime).TotalMilliseconds > 200)
                {
                    lock (this)
                    {
                        _finishedPeaks.AddRange(_accumulatePeaks);
                    }
                    _accumulatePeaks.Clear();
                    _lastDisplayTime = now;
                }
            }

            /// <summary>
            /// Return list of finished peaks.
            /// </summary>
            public IEnumerable<Peak> GetPeaks()
            {
                lock (this)
                {
                    var peaks = _finishedPeaks;
                    _finishedPeaks = new List<Peak>();
                    return peaks;
                }
            }

            public int GetRank(int id)
            {
                // TODO: how to get rank from AllChromatogramsGraph (information must be moved to Model!)
                return 1;
            }

            public class Peak
            {
                public Peak(string modifiedSequence, int filterIndex, bool mayOverlap)
                {
                    ModifiedSequence = modifiedSequence;
                    FilterIndex = filterIndex;
                    Times = new List<float>();
                    Intensities = new List<float>();
                    MayOverlap = mayOverlap;
                }

                public readonly string ModifiedSequence;
                public readonly int FilterIndex;
                public List<float> Times;
                public List<float> Intensities;
                public float PeakTime;
                public float PeakIntensity;
                public int PointCount;
                public readonly bool MayOverlap;
                public object CurveInfo;
                public bool NoTrailingZero;

                /// <summary>
                /// Returns true if time points in another peak overlap this one.
                /// </summary>
                public bool Overlaps(Peak peak)
                {
                    return (Times[0] <= peak.Times[peak.Times.Count - 1] &&
                            Times[Times.Count - 1] >= peak.Times[0]);
                }

                /// <summary>
                /// Add another peak to this one.
                /// </summary>
                public void Add(Peak peak)
                {
                    var newTimes = new List<float>();
                    var newIntensities = new List<float>();
                    if (PeakIntensity < peak.PeakIntensity)
                    {
                        PeakIntensity = peak.PeakIntensity;
                        PeakTime = peak.PeakTime;
                    }
                    var i = 0;
                    var j = 0;

                    while (true)
                    {
                        if (i < Times.Count)
                        {
                            // Use points from this peak.
                            if (j == peak.Times.Count || Times[i] < peak.Times[j])
                            {
                                newTimes.Add(Times[i]);
                                newIntensities.Add(Intensities[i]);
                                i++;
                            }

                            // Use points from other peak.
                            else if (Times[i] > peak.Times[j])
                            {
                                newTimes.Add(peak.Times[j]);
                                newIntensities.Add(peak.Intensities[j]);
                                j++;
                            }

                            // Add overlapping points.
                            else
                            {
                                newTimes.Add(Times[i]);
                                float sum = Intensities[i] + peak.Intensities[j];
                                newIntensities.Add(sum);
                                if (PeakIntensity < sum)
                                {
                                    PeakIntensity = sum;
                                    PeakTime = Times[i];
                                }
                                i++;
                                j++;
                            }
                        }

                        // Use remaining points from other peak.
                        else if (j < peak.Times.Count)
                        {
                            newTimes.Add(peak.Times[j]);
                            newIntensities.Add(peak.Intensities[j]);
                            j++;
                        }

                        // Both peaks exhausted.
                        else
                            break;
                    }

                    Times = newTimes;
                    Intensities = newIntensities;
                }
            }
        }
    }
}