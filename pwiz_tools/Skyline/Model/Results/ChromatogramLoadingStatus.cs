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
using System.Linq;
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
        public const float TIME_RESOLUTION = 0.1f;    // 6 seconds
        public const float INTENSITY_THRESHOLD_PERCENT = 0.03f;
        public const float MAX_PEAKS = 1000;

        public ChromatogramLoadingStatus(string message) :
            base(message)
        {
            Transitions = new TransitionData();
        }

        public TransitionData Transitions { get; private set; }
        public string FilePath { get; private set; }

        public ChromatogramLoadingStatus ChangeFilePath(string filePath)
        {
            return ChangeProp(ImClone(this), s => s.FilePath = filePath);
        }

        /// <summary>
        /// Binned chromatogram data appropriate for display in the chromatogram graph.
        /// </summary>
        public class TransitionData
        {
            private List<Peak[]> _peaks;
            private List<Peak> _finishedPeaks = new List<Peak>();
            private static readonly int SOURCE_INDEX_COUNT = Helpers.CountEnumValues<ChromSource>() - 1;

            public float MaxRetentionTime { get; set; }
            public bool MaxRetentionTimeKnown { get; set; }
            // TODO: This MaxIntensity (used to save the axes dimensions in AllChromatogramsGraph) must
            // TODO: be calculated on the processing thread, because that is where SRM peaks are summed.
            // TODO: We need to move that calculation into this file (but on the processing thread),
            // TODO: because this value should be retrieved from the Model, not the Graphs.
            public float MaxIntensity { get; set; }
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
                    _finishedPeaks = new List<Peak>();
                }
            }

            // Accumulate points in bins so that there isn't an overwhelming amount of data to display in the graph.
            private static float GetTimeBin(float time)
            {
                return (float)Math.Floor(time / TIME_RESOLUTION) * TIME_RESOLUTION;
            }

            /// <summary>
            /// Add transition points (partial data for multiple transitions) to AllChromatogramsGraph.
            /// </summary>
            /// <param name="filterIndex"></param>
            /// <param name="chromSource"></param>
            /// <param name="time"></param>
            /// <param name="intensities"></param>
            public void Add(int filterIndex, ChromSource chromSource, float time, float[] intensities)
            {
                if (!Progressive)
                    MaxRetentionTime = Math.Max(MaxRetentionTime, time);
                var timeBin = GetTimeBin(time);

                // Create transition list for this filter.
                var sourceIndex = (int) chromSource;
                while (filterIndex >= _peaks.Count)
                    _peaks.Add(new Peak[SOURCE_INDEX_COUNT]);
                var peak = _peaks[filterIndex][sourceIndex];
                if (peak == null)
                    _peaks[filterIndex][sourceIndex] = peak = new Peak(filterIndex, false);

                var intensity = 0.0f;
                for (int i = 0; i < intensities.Count(); i++)
                    intensity += intensities[i];

                // Add point to current time bin.
                var lastIndex = peak.Times.Count - 1;
                if (lastIndex >= 0 && timeBin <= peak.Times[lastIndex])
                {
                    float intensityAvg = peak.Intensities[lastIndex];
                    peak.PointCount++;
                    float newAvg = intensityAvg + (intensity - intensityAvg)/peak.PointCount;
                    peak.Intensities[lastIndex] = newAvg;
                    return;
                }

                // Remove last bin if intensity was too low.
                if (lastIndex >= 0)
                {
                    var lastIntensity = peak.Intensities[lastIndex];
                    if (peak.MaxIntensity < lastIntensity)
                    {
                        peak.MaxIntensity = lastIntensity;
                        if (MaxIntensity < lastIntensity)
                            MaxIntensity = lastIntensity;
                    }

                    // Finish a peak if intensity falls too low.
                    if (lastIntensity < MaxIntensity*INTENSITY_THRESHOLD_PERCENT)
                    {
                        // If the peak was above our current threshold, send to display.
                        if (peak.Intensities.Count > 1 && peak.MaxIntensity > MaxIntensity*INTENSITY_THRESHOLD_PERCENT)
                        {
                            lock (this)
                            {
                                _finishedPeaks.Add(peak);
                            }
                        }

                        _peaks[filterIndex][sourceIndex] = peak = new Peak(filterIndex, false);
                    }
                }

                // Create a new time bin.
                peak.Times.Add(timeBin);
                peak.Intensities.Add(intensity);
                peak.PointCount = 1;
            }

            /// <summary>
            /// Add a complete transition to AllChromatogramsGraph.
            /// </summary>
            public void AddTransition(int index, int rank, float[] times, float[] intensities)
            {
                if (rank == 0)
                    return;

                var maxTime = times[times.Length - 1];
                if (MaxRetentionTime < maxTime)
                    MaxRetentionTime = maxTime;
                var thresholdIntensity = MaxIntensity*INTENSITY_THRESHOLD_PERCENT;

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

                    AddTransition(index, times, intensities, startIndex, endIndex);

                    startIndex = endIndex;
                }
            }

            private void AddTransition(int index, float[] times, float[] intensities, int startIndex, int endIndex)
            {
                MaxRetentionTime = Math.Max(MaxRetentionTime, times[times.Length - 1]);

                var peak = new Peak(index, true);
                var lastTimeBin = GetTimeBin(times[startIndex]);
                var binIntensity = 0.0f;
                var binSampleCount = 0;

                // Average intensity values into each time bin.
                for (int i = startIndex; i <= endIndex; i++)
                {
                    var timeBin = 0.0f;
                    if (i < endIndex)
                    {
                        timeBin = GetTimeBin(times[i]);
                        if (lastTimeBin == timeBin)
                        {
                            binIntensity += intensities[i];
                            binSampleCount++;
                            continue;
                        }
                    }

                    peak.Times.Add(lastTimeBin);
                    var averageIntensity = binIntensity/binSampleCount;
                    peak.Intensities.Add(averageIntensity);
                    peak.MaxIntensity = Math.Max(peak.MaxIntensity, averageIntensity);

                    if (i == endIndex)
                        break;

                    lastTimeBin = timeBin;
                    binIntensity = intensities[i];
                    binSampleCount = 1;
                }

                // Update max intensity.
                if (MaxIntensity < peak.MaxIntensity)
                    MaxIntensity = peak.MaxIntensity;

                // Add to list of peaks for display.
                lock (this)
                {
                    _finishedPeaks.Add(peak);
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
                public Peak(int filterIndex, bool mayOverlap)
                {
                    FilterIndex = filterIndex;
                    Times = new List<float>();
                    Intensities = new List<float>();
                    MayOverlap = mayOverlap;
                }

                public readonly int FilterIndex;
                public List<float> Times;
                public List<float> Intensities;
                public int PointCount;
                public float MaxIntensity;
                public readonly bool MayOverlap;

                /// <summary>
                /// Returns true if time points in another peak overlap this one.
                /// </summary>
                /// <param name="peak"></param>
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
                    var newMaxIntensity = Math.Max(MaxIntensity, peak.MaxIntensity);
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
                                var sum = Intensities[i] + peak.Intensities[j];
                                newMaxIntensity = Math.Max(newMaxIntensity, sum);
                                newIntensities.Add(sum);
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
                    MaxIntensity = newMaxIntensity;
                }
            }
        }
    }
}