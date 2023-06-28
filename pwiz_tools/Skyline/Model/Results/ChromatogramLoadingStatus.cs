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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using pwiz.Common.SystemUtil;

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
        public const float TIME_RESOLUTION = 0.2f;    // 12 seconds
        public const int MAX_PEAKS_PER_BIN = 3;                // how many peaks to graph per bin
        public const double DISPLAY_FILTER_PERCENT = 0.01;     // filter peaks less than this percentage of maximum intensity

        public ChromatogramLoadingStatus(MsDataFileUri filePath, IEnumerable<string> replicateNames) :
            base(SampleHelp.GetFileName(filePath))
        {
            Transitions = new TransitionData();
            FilePath = filePath;
            ReplicateNames = replicateNames;
        }

        public TransitionData Transitions { get; private set; }
        public MsDataFileUri FilePath { get; private set; }
        public bool Importing { get; private set; }
        public IEnumerable<string> ReplicateNames { get; private set; }

        public ChromatogramLoadingStatus ChangeFilePath(MsDataFileUri filePath)
        {
            return ChangeProp(ImClone(this), s => s.FilePath = filePath);
        }

        public ChromatogramLoadingStatus ChangeImporting(bool importing)
        {
            return ChangeProp(ImClone(this), s => s.Importing = importing);
        }

        // Accumulate points in bins so that there isn't an overwhelming amount of data to display in the graph.
        public static int GetBinIndex(float time)
        {
            return (int) ((time+TIME_RESOLUTION/2)/TIME_RESOLUTION);
        }

        /// <summary>
        /// Binned chromatogram data appropriate for display in the chromatogram graph.
        /// </summary>
        public class TransitionData
        {
            private float _maxImportedIntensity;
            private List<Peak> _bin;
            private int _lastBinIndex;

            public ConcurrentQueue<List<Peak>> BinnedPeaks { get; private set; } 
            public float MaxIntensity { get; set; }
            public float MaxRetentionTime { get; set; }
            public bool MaxRetentionTimeKnown { get; set; }
            public float CurrentTime { get; set; }
            public bool Progressive { get; set; }

            public TransitionData()
            {
                BinnedPeaks = new ConcurrentQueue<List<Peak>>();
            }

            /// <summary>
            /// Add transition points (partial data for multiple transitions) to AllChromatogramsGraph.
            /// </summary>
            public void Add(ChromatogramGroupId chromatogramGroupId, Color color, int filterIndex, float time, float[] intensities)
            {
                MaxRetentionTime = Math.Max(MaxRetentionTime, time);
                float intensity = 0;
                for (int i = 0; i < intensities.Length; i++)
                    intensity += intensities[i];
                AddIntensity(chromatogramGroupId, color, filterIndex, time, intensity);
            }

            /// <summary>
            /// Finish display of peaks for a file.
            /// </summary>
            public void Flush()
            {
                if (_bin != null)
                {
                    BinnedPeaks.Enqueue(_bin);
                    _bin = null;
                }
            }

            /// <summary>
            /// Add a complete transition to AllChromatogramsGraph.
            /// </summary>
            public void AddTransition(ChromatogramGroupId chromatogramGroupId, Color color, int index, int rank, IList<float> times, IList<float> intensities)
            {
                if (rank == 0 || times.Count == 0)
                    return;

                float maxTime = times[times.Count - 1];
                MaxRetentionTime = Math.Max(MaxRetentionTime, maxTime);

                for (int i = 0; i < times.Count; i++)
                    AddIntensity(chromatogramGroupId, color, index, times[i], intensities[i]);
            }

            private void AddIntensity(ChromatogramGroupId chromatogramGroupId, Color color, int filterIndex, float time, float intensity)
            {
                // Filter out small intensities quickly.
                if (intensity < _maxImportedIntensity*DISPLAY_FILTER_PERCENT)
                    return;

                // If we just finished a bin, queue it for progress thread.
                int binIndex = GetBinIndex(time);
                if (_lastBinIndex != binIndex)
                {
                    _lastBinIndex = binIndex;
                    if (_bin != null)
                    {
                        BinnedPeaks.Enqueue(_bin);
                        _bin = null;
                    }
                }

                // Create a new bin of peaks.
                if (_bin == null)
                {
                    _bin = new List<Peak>(MAX_PEAKS_PER_BIN) {new Peak(intensity, chromatogramGroupId, color, filterIndex, binIndex)};
                    _maxImportedIntensity = Math.Max(_maxImportedIntensity, intensity);
                    return;
                }

                // If this peak is already in the bin, just update its intensity.
                foreach (var peak in _bin)
                {
                    if (filterIndex == peak.FilterIndex)
                    {
                        peak.Intensity = Math.Max(intensity, peak.Intensity);
                        _maxImportedIntensity = Math.Max(_maxImportedIntensity, intensity);
                        return;
                    }
                }

                // If bin isn't full yet, add this peak.
                if (_bin.Count < MAX_PEAKS_PER_BIN)
                {
                    _bin.Add(new Peak(intensity, chromatogramGroupId, color, filterIndex, binIndex));
                    _maxImportedIntensity = Math.Max(_maxImportedIntensity, intensity);
                    return;
                }

                // Find the lowest intensity peak in the bin.
                var minPeak = _bin[0];
                for (int i = 1; i < _bin.Count; i++)
                {
                    if (_bin[i].Intensity < minPeak.Intensity)
                        minPeak = _bin[i];
                }

                // If this peak has lower intensity than the minimum, skip it.
                if (intensity <= minPeak.Intensity)
                    return;

                // Overwrite lowest peak with new higher intensity peak.
                minPeak.Intensity = intensity;
                minPeak.ChromatogramGroupId = chromatogramGroupId;
                minPeak.Color = color;
                minPeak.FilterIndex = filterIndex;
                minPeak.BinIndex = binIndex;
                _maxImportedIntensity = Math.Max(_maxImportedIntensity, intensity);
            }

            public class Peak
            {
                public Peak(float intensity, ChromatogramGroupId chromatogramGroupId, Color color, int filterIndex, int binIndex)
                {
                    Intensity = intensity;
                    ChromatogramGroupId = chromatogramGroupId;
                    Color = color;
                    FilterIndex = filterIndex;
                    BinIndex = binIndex;
                }

                public ChromatogramGroupId ChromatogramGroupId;
                public Color Color;
                public int FilterIndex;
                public int BinIndex;
                public float Intensity;
            }
        }
    }
}