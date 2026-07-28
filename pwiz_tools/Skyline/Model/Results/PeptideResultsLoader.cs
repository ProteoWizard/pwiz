/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Reads the peak values for every transition of every precursor of one
    /// <see cref="PeptideDocNode"/>, across every replicate, from the chromatogram cache.
    /// <para>
    /// This exists because a <see cref="DocNode"/> is to stop being the place complete
    /// result information comes from. A <see cref="TransitionChromInfo"/> will reliably
    /// hold only retention time, area and a few flags. Code which needs anything else
    /// asks for everything belonging to one peptide, calculates what it needs, and then
    /// lets the returned <see cref="LoadedPeptideResults"/> go.
    /// </para>
    /// <para>
    /// Reading everything for a peptide every time something is needed is not efficient.
    /// That is deliberate for now: make it correct first, then find out where the cost
    /// actually lands and decide what is worth holding onto.
    /// </para>
    /// </summary>
    public class PeptideResultsLoader
    {
        public SrmSettings Settings { get; set; }
        public PeptideDocNode PeptideDocNode { get; set; }

        /// <summary>
        /// Defaults to the value from the instrument settings when left null.
        /// </summary>
        public float? MzMatchTolerance { get; set; }

        public LoadedPeptideResults Load()
        {
            var peaks = new Dictionary<PeakKey, ChromPeak>();
            var measuredResults = Settings?.MeasuredResults;
            if (measuredResults == null || PeptideDocNode == null)
            {
                return new LoadedPeptideResults(PeptideDocNode, peaks);
            }

            float tolerance = MzMatchTolerance ??
                              (float) Settings.TransitionSettings.Instrument.MzMatchTolerance;
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                foreach (var nodeGroup in PeptideDocNode.TransitionGroups)
                {
                    LoadTransitionGroup(measuredResults, chromatograms, replicateIndex, nodeGroup, tolerance, peaks);
                }
            }

            return new LoadedPeptideResults(PeptideDocNode, peaks);
        }

        private void LoadTransitionGroup(MeasuredResults measuredResults, ChromatogramSet chromatograms,
            int replicateIndex, TransitionGroupDocNode nodeGroup, float tolerance,
            IDictionary<PeakKey, ChromPeak> peaks)
        {
            if (!measuredResults.TryLoadChromatogram(chromatograms, PeptideDocNode, nodeGroup, tolerance,
                    out var chromGroupInfos))
            {
                return;
            }

            // A file should only appear once. See the same guard in TransitionGroupDocNode.ChangeResults.
            if (chromGroupInfos.Length > 1)
            {
                chromGroupInfos = chromGroupInfos.Distinct(ChromatogramGroupInfo.PathComparer).ToArray();
            }

            foreach (var chromGroupInfo in chromGroupInfos)
            {
                var fileId = chromatograms.FindFile(chromGroupInfo);
                if (fileId == null)
                {
                    continue;
                }

                foreach (TransitionDocNode nodeTran in nodeGroup.Transitions)
                {
                    // Optimization steps are separate chromatograms of the same transition, and
                    // each one has its own set of candidate peaks.
                    var optStepChromatograms = chromGroupInfo.GetAllTransitionInfo(nodeTran, tolerance,
                        chromatograms.OptimizationFunction, TransformChrom.interpolated);
                    for (int step = -optStepChromatograms.StepCount; step <= optStepChromatograms.StepCount; step++)
                    {
                        var chromInfo = optStepChromatograms.GetChromatogramForStep(step);
                        if (chromInfo == null)
                        {
                            continue;
                        }

                        for (int peakIndex = 0; peakIndex < chromInfo.NumPeaks; peakIndex++)
                        {
                            var key = new PeakKey(nodeTran.Id.GlobalIndex, replicateIndex, fileId.GlobalIndex,
                                step, peakIndex);
                            peaks[key] = chromInfo.GetPeak(peakIndex);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// One <see cref="PeptideDocNode"/> together with all of its result information for all
    /// replicates. Callers calculate whatever they need from this and then release it.
    /// </summary>
    public class LoadedPeptideResults
    {
        private readonly Dictionary<PeakKey, ChromPeak> _peaks;

        public LoadedPeptideResults(PeptideDocNode peptideDocNode, Dictionary<PeakKey, ChromPeak> peaks)
        {
            PeptideDocNode = peptideDocNode;
            _peaks = peaks;
        }

        public PeptideDocNode PeptideDocNode { get; }

        public int Count
        {
            get { return _peaks.Count; }
        }

        /// <summary>
        /// The candidate peak that Skyline detected, or null when the cache has no such peak.
        /// </summary>
        public ChromPeak? GetPeak(TransitionDocNode nodeTran, int replicateIndex, ChromFileInfoId fileId,
            int optimizationStep, int peakIndex)
        {
            var key = new PeakKey(nodeTran.Id.GlobalIndex, replicateIndex, fileId.GlobalIndex, optimizationStep,
                peakIndex);
            if (_peaks.TryGetValue(key, out var peak))
            {
                return peak;
            }

            return null;
        }

        /// <summary>
        /// The candidate peak which <paramref name="chromInfo"/> holds the values of, or null
        /// when its peak did not come from the cache.
        /// </summary>
        public ChromPeak? GetPeak(TransitionDocNode nodeTran, int replicateIndex, TransitionChromInfo chromInfo)
        {
            int peakIndex = FindPeakIndex(nodeTran, replicateIndex, chromInfo);
            if (peakIndex < 0)
            {
                return null;
            }

            return GetPeak(nodeTran, replicateIndex, chromInfo.FileId, chromInfo.OptimizationStep, peakIndex);
        }

        /// <summary>
        /// A <see cref="TransitionChromInfo"/> with every value filled in, built from what the
        /// document still holds plus what had to be read back from the cache. Returns
        /// <paramref name="chromInfo"/> itself when its peak is not one of the candidate peaks,
        /// which is what happens when the user set the boundaries themselves and the document
        /// therefore still has to hold all of the values.
        /// </summary>
        public TransitionChromInfo Materialize(TransitionDocNode nodeTran, int replicateIndex,
            TransitionChromInfo chromInfo)
        {
            var peak = GetPeak(nodeTran, replicateIndex, chromInfo);
            if (!peak.HasValue)
            {
                return chromInfo;
            }

            return new TransitionChromInfo(chromInfo.FileId, chromInfo.OptimizationStep, peak.Value,
                    chromInfo.IonMobility, chromInfo.Annotations, chromInfo.UserSet)
                .ChangeRank(true, chromInfo.Rank, chromInfo.RankByLevel);
        }

        /// <summary>
        /// Finds the candidate peak whose boundaries match those already recorded on
        /// <paramref name="chromInfo"/>. Returns -1 when the peak did not come from the
        /// cache, which is what happens when the user set the boundaries themselves.
        /// <para>
        /// This matches on the peak boundaries because that is all there is to match on while
        /// the document still holds them. Once the chosen peak index is recorded on the
        /// <see cref="TransitionGroupChromInfo"/>, this becomes a lookup instead of a search.
        /// </para>
        /// </summary>
        public int FindPeakIndex(TransitionDocNode nodeTran, int replicateIndex, TransitionChromInfo chromInfo)
        {
            for (int peakIndex = 0;; peakIndex++)
            {
                var peak = GetPeak(nodeTran, replicateIndex, chromInfo.FileId, chromInfo.OptimizationStep, peakIndex);
                if (!peak.HasValue)
                {
                    return -1;
                }

                if (peak.Value.StartTime == chromInfo.StartRetentionTime &&
                    peak.Value.EndTime == chromInfo.EndRetentionTime)
                {
                    return peakIndex;
                }
            }
        }
    }

    /// <summary>
    /// Identifies one candidate peak of one transition chromatogram. The optimization step
    /// is part of the identity because each step is a separate chromatogram with its own
    /// candidate peaks.
    /// </summary>
    public struct PeakKey
    {
        public PeakKey(int transitionGlobalIndex, int replicateIndex, int fileGlobalIndex, int optimizationStep,
            int peakIndex)
        {
            TransitionGlobalIndex = transitionGlobalIndex;
            ReplicateIndex = replicateIndex;
            FileGlobalIndex = fileGlobalIndex;
            OptimizationStep = optimizationStep;
            PeakIndex = peakIndex;
        }

        public int TransitionGlobalIndex { get; }
        public int ReplicateIndex { get; }
        public int FileGlobalIndex { get; }
        public int OptimizationStep { get; }
        public int PeakIndex { get; }

        public bool Equals(PeakKey other)
        {
            return TransitionGlobalIndex == other.TransitionGlobalIndex &&
                   ReplicateIndex == other.ReplicateIndex &&
                   FileGlobalIndex == other.FileGlobalIndex &&
                   OptimizationStep == other.OptimizationStep &&
                   PeakIndex == other.PeakIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PeakKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = TransitionGlobalIndex;
                result = (result * 397) ^ ReplicateIndex;
                result = (result * 397) ^ FileGlobalIndex;
                result = (result * 397) ^ OptimizationStep;
                result = (result * 397) ^ PeakIndex;
                return result;
            }
        }
    }
}
