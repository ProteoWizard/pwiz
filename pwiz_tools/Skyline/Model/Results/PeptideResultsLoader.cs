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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Reads everything about the peaks of one <see cref="PeptideDocNode"/>, across every
    /// replicate, from the chromatogram cache.
    /// <para>
    /// This exists because a <see cref="DocNode"/> is to stop being the place complete
    /// result information comes from. A <see cref="TransitionResults"/> holds only the
    /// areas, and a <see cref="TransitionGroupResults"/> only the areas, retention times,
    /// chosen candidate peak indexes and the few things which cannot be derived from the
    /// .skyd file. Code which needs anything else loads everything belonging to one
    /// peptide, calculates what it needs, and then lets it go.
    /// </para>
    /// <para>
    /// The granularity is deliberately a whole peptide across all replicates. Reading that
    /// much every time something is needed is not efficient, which is accepted for now:
    /// make it correct first, then find out where the cost actually lands.
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

        /// <summary>
        /// Used to avoid holding on to many identical <see cref="ChromFileIds"/>. A fresh one
        /// is used when left null, which still shares within the one peptide being loaded.
        /// </summary>
        public ValueCache ValueCache { get; set; }

        public LoadedPeptideResults Load()
        {
            var valueCache = ValueCache ?? new ValueCache();
            var transitions = new Dictionary<int, LoadedTransition>();
            var measuredResults = Settings?.MeasuredResults;
            if (measuredResults == null || PeptideDocNode == null)
            {
                return new LoadedPeptideResults(PeptideDocNode, transitions);
            }

            float tolerance = MzMatchTolerance ??
                              (float) Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var nodeGroup in PeptideDocNode.TransitionGroups)
            {
                var builders = nodeGroup.Transitions.ToDictionary(nodeTran => nodeTran.Id.GlobalIndex,
                    nodeTran => new LoadedTransitionBuilder());
                for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
                {
                    LoadReplicate(measuredResults, measuredResults.Chromatograms[replicateIndex], nodeGroup,
                        tolerance, builders);
                    foreach (var builder in builders.Values)
                    {
                        builder.EndReplicate();
                    }
                }

                foreach (var entry in builders)
                {
                    transitions[entry.Key] = entry.Value.Build(valueCache);
                }
            }

            return new LoadedPeptideResults(PeptideDocNode, transitions);
        }

        /// <summary>
        /// Appends the positions contributed by one replicate. The ordering has to match the
        /// order in which <see cref="TransitionGroupDocNode.ChangeResults"/> builds its lists:
        /// file major, optimization step minor, skipping any file where the transition has no
        /// chromatogram.
        /// </summary>
        private void LoadReplicate(MeasuredResults measuredResults, ChromatogramSet chromatograms,
            TransitionGroupDocNode nodeGroup, float tolerance, IDictionary<int, LoadedTransitionBuilder> builders)
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
                    if (optStepChromatograms.IsEmpty)
                    {
                        continue;
                    }

                    var builder = builders[nodeTran.Id.GlobalIndex];
                    for (int step = -optStepChromatograms.StepCount; step <= optStepChromatograms.StepCount; step++)
                    {
                        // A position gets added for every step even when there is no
                        // chromatogram for it, because ChangeResults adds an empty peak there.
                        var chromatogramInfo = optStepChromatograms.GetChromatogramForStep(step);
                        if (chromatogramInfo == null)
                        {
                            builder.AddPosition(fileId, step, IonMobilityFilter.EMPTY, new ChromPeak[0]);
                            continue;
                        }

                        var peaks = new ChromPeak[chromatogramInfo.NumPeaks];
                        for (int peakIndex = 0; peakIndex < peaks.Length; peakIndex++)
                        {
                            peaks[peakIndex] = chromatogramInfo.GetPeak(peakIndex);
                        }

                        builder.AddPosition(fileId, step, chromatogramInfo.GetIonMobilityFilter(), peaks);
                    }
                }
            }
        }

        private class LoadedTransitionBuilder
        {
            private readonly List<ChromFileInfoId> _fileIds = new List<ChromFileInfoId>();
            private readonly List<int> _counts = new List<int>();
            private int _countThisReplicate;

            public List<int> OptimizationSteps { get; } = new List<int>();
            public List<IonMobilityFilter> IonMobilities { get; } = new List<IonMobilityFilter>();
            public List<ImmutableList<ChromPeak>> Peaks { get; } = new List<ImmutableList<ChromPeak>>();

            public void AddPosition(ChromFileInfoId fileId, int optimizationStep, IonMobilityFilter ionMobility,
                IEnumerable<ChromPeak> peaks)
            {
                _fileIds.Add(fileId);
                OptimizationSteps.Add(optimizationStep);
                IonMobilities.Add(ionMobility ?? IonMobilityFilter.EMPTY);
                Peaks.Add(ImmutableList.ValueOf(peaks));
                _countThisReplicate++;
            }

            public void EndReplicate()
            {
                _counts.Add(_countThisReplicate);
                _countThisReplicate = 0;
            }

            public LoadedTransition Build(ValueCache valueCache)
            {
                var chromFileIds = ChromFileIds.Intern(valueCache, ReplicatePositions.FromCounts(_counts), _fileIds);
                return new LoadedTransition(chromFileIds, OptimizationSteps, IonMobilities, Peaks);
            }
        }
    }

    /// <summary>
    /// Everything read for one transition: which file and optimization step each flat
    /// position belongs to, and every candidate peak found there.
    /// </summary>
    public class LoadedTransition
    {
        public LoadedTransition(ChromFileIds chromFileIds, IEnumerable<int> optimizationSteps,
            IEnumerable<IonMobilityFilter> ionMobilities, IEnumerable<ImmutableList<ChromPeak>> peaks)
        {
            ChromFileIds = chromFileIds;
            OptimizationSteps = ImmutableList.ValueOf(optimizationSteps);
            IonMobilities = ImmutableList.ValueOf(ionMobilities);
            Peaks = ImmutableList.ValueOf(peaks);
        }

        public ChromFileIds ChromFileIds { get; }
        public ImmutableList<int> OptimizationSteps { get; }
        public ImmutableList<IonMobilityFilter> IonMobilities { get; }
        public ImmutableList<ImmutableList<ChromPeak>> Peaks { get; }

        public int PositionCount
        {
            get { return Peaks.Count; }
        }
    }

    /// <summary>
    /// One <see cref="PeptideDocNode"/> together with all of its result information for all
    /// replicates. Callers calculate whatever they need from this and then release it.
    /// </summary>
    public class LoadedPeptideResults
    {
        private readonly Dictionary<int, LoadedTransition> _transitions;

        public LoadedPeptideResults(PeptideDocNode peptideDocNode, Dictionary<int, LoadedTransition> transitions)
        {
            PeptideDocNode = peptideDocNode;
            _transitions = transitions;
        }

        public PeptideDocNode PeptideDocNode { get; }

        public LoadedTransition GetTransition(TransitionDocNode nodeTran)
        {
            _transitions.TryGetValue(nodeTran.Id.GlobalIndex, out var loadedTransition);
            return loadedTransition;
        }

        public int GetPositionCount(TransitionDocNode nodeTran)
        {
            return GetTransition(nodeTran)?.PositionCount ?? 0;
        }

        /// <summary>
        /// The candidate peak that Skyline detected, or null when there is no such peak.
        /// </summary>
        public ChromPeak? GetPeak(TransitionDocNode nodeTran, int position, int peakIndex)
        {
            var peaks = GetTransition(nodeTran)?.Peaks;
            if (peaks == null || position < 0 || position >= peaks.Count)
            {
                return null;
            }

            var peaksAtPosition = peaks[position];
            if (peakIndex < 0 || peakIndex >= peaksAtPosition.Count)
            {
                return null;
            }

            return peaksAtPosition[peakIndex];
        }

        /// <summary>
        /// Rebuilds the complete <see cref="TransitionChromInfo"/> for one position, which is
        /// what code that needs more than the area has to go through. The ion mobility comes
        /// from the chromatogram rather than from the document, because the document is to
        /// stop holding it.
        /// </summary>
        public TransitionChromInfo MakeTransitionChromInfo(TransitionDocNode nodeTran, int position,
            int candidatePeakIndex, UserSet userSet, Annotations annotations)
        {
            var loadedTransition = GetTransition(nodeTran);
            if (loadedTransition == null || position < 0 || position >= loadedTransition.PositionCount)
            {
                return null;
            }

            var peak = GetPeak(nodeTran, position, candidatePeakIndex) ?? ChromPeak.EMPTY;
            return new TransitionChromInfo(loadedTransition.ChromFileIds.FileIds[position],
                loadedTransition.OptimizationSteps[position], peak, loadedTransition.IonMobilities[position],
                annotations ?? Annotations.EMPTY, userSet);
        }

        /// <summary>
        /// Finds the candidate peak whose boundaries match those recorded on
        /// <paramref name="chromInfo"/>. Returns -1 when the peak did not come from the cache,
        /// which is what happens when the user set the boundaries themselves.
        /// <para>
        /// This is the bridge for as long as the document still holds the peak boundaries.
        /// Once the chosen index is stored on <see cref="TransitionGroupResults"/> it is read
        /// from there instead of searched for.
        /// </para>
        /// </summary>
        public int FindCandidatePeakIndex(TransitionDocNode nodeTran, int position, TransitionChromInfo chromInfo)
        {
            var peaks = GetTransition(nodeTran)?.Peaks;
            if (peaks == null || position < 0 || position >= peaks.Count)
            {
                return -1;
            }

            var peaksAtPosition = peaks[position];
            for (int peakIndex = 0; peakIndex < peaksAtPosition.Count; peakIndex++)
            {
                if (peaksAtPosition[peakIndex].StartTime == chromInfo.StartRetentionTime &&
                    peaksAtPosition[peakIndex].EndTime == chromInfo.EndRetentionTime)
                {
                    return peakIndex;
                }
            }

            return -1;
        }
    }
}
