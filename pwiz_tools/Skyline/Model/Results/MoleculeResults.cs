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

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// One <see cref="PeptideDocNode"/> together with all of its result information, read from
    /// the chromatogram cache the first time anything is asked for. Callers calculate whatever
    /// they need from this and then release it.
    /// <para>
    /// This exists because a <see cref="DocNode"/> is to stop being the place complete result
    /// information comes from. A <see cref="TransitionResults"/> holds only the areas, and a
    /// <see cref="TransitionGroupResults"/> only the areas, retention times, chosen candidate
    /// peak indexes and the few things which cannot be derived from the .skyd file.
    /// </para>
    /// <para>
    /// The granularity of the reading is deliberately a whole molecule across all replicates.
    /// Reading that much every time something is needed is not efficient, which is accepted for
    /// now: make it correct first, then find out where the cost actually lands.
    /// </para>
    /// <para>
    /// One instance is not meant to be used from more than one thread, since it reads on demand.
    /// </para>
    /// </summary>
    public class MoleculeResults
    {
        private Dictionary<ReferenceValue<Transition>, TransitionPeaks> _transitions;
        private Dictionary<GroupReplicateKey, ImmutableList<ChromatogramGroupInfo>> _chromatogramGroupInfos;
        private int? _replicateIndex;
        private ValueCache _valueCache;

        public MoleculeResults(SrmSettings settings, PeptideDocNode peptideDocNode)
        {
            Settings = settings;
            PeptideDocNode = peptideDocNode;
        }

        public SrmSettings Settings { get; }
        public PeptideDocNode PeptideDocNode { get; }

        public float MzMatchTolerance
        {
            get { return (float) Settings.TransitionSettings.Instrument.MzMatchTolerance; }
        }

        /// <summary>
        /// Restricts the reading to one replicate. Null reads every replicate.
        /// <para>
        /// The positions still run over all of the replicates either way, with the ones that
        /// were skipped contributing none, so that <see cref="GetPositions"/> means the same
        /// thing in both cases.
        /// </para>
        /// </summary>
        public int? ReplicateIndex
        {
            get { return _replicateIndex; }
            set
            {
                AssertNotRead();
                _replicateIndex = value;
            }
        }

        /// <summary>
        /// Used to avoid holding on to many identical <see cref="ChromFileIds"/>. A fresh one is
        /// used when left null, which still shares within the one molecule being read.
        /// </summary>
        public ValueCache ValueCache
        {
            get { return _valueCache; }
            set
            {
                AssertNotRead();
                _valueCache = value;
            }
        }

        /// <summary>
        /// The chromatograms which were read, kept because code such as GraphChromatogram and
        /// the on demand feature calculator needs the chromatogram itself and not only the peaks
        /// taken from it.
        /// </summary>
        public ImmutableList<ChromatogramGroupInfo> GetChromatogramGroupInfos(TransitionGroupDocNode nodeGroup,
            int replicateIndex)
        {
            EnsureRead();
            _chromatogramGroupInfos.TryGetValue(new GroupReplicateKey(nodeGroup.TransitionGroup, replicateIndex),
                out var result);
            return result ?? ImmutableList<ChromatogramGroupInfo>.EMPTY;
        }

        public TransitionPeaks GetTransitionPeaks(TransitionDocNode nodeTran)
        {
            EnsureRead();
            _transitions.TryGetValue(nodeTran.Transition, out var transitionPeaks);
            return transitionPeaks;
        }

        public int GetPositionCount(TransitionDocNode nodeTran)
        {
            return GetTransitionPeaks(nodeTran)?.PositionCount ?? 0;
        }

        /// <summary>
        /// The candidate peak that Skyline detected, or null when there is no such peak.
        /// </summary>
        public ChromPeak? GetPeak(TransitionDocNode nodeTran, int position, int peakIndex)
        {
            var peaks = GetTransitionPeaks(nodeTran)?.Peaks;
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
        /// The positions of one replicate, which is the range a caller needs when it is
        /// rebuilding one replicate at a time.
        /// </summary>
        public IEnumerable<int> GetPositions(TransitionDocNode nodeTran, int replicateIndex)
        {
            var replicatePositions = GetTransitionPeaks(nodeTran)?.ChromFileIds.ReplicatePositions;
            if (replicatePositions == null)
            {
                return Array.Empty<int>();
            }

            return Enumerable.Range(replicatePositions.GetStart(replicateIndex),
                replicatePositions.GetCount(replicateIndex));
        }

        /// <summary>
        /// Rebuilds the complete <see cref="TransitionChromInfo"/> for one position, which is
        /// what code that needs more than the area has to go through. The ion mobility comes
        /// from the chromatogram rather than from the document, because the document is to stop
        /// holding it.
        /// </summary>
        public TransitionChromInfo MakeTransitionChromInfo(TransitionDocNode nodeTran, int position,
            int candidatePeakIndex, UserSet userSet, Annotations annotations)
        {
            var transitionPeaks = GetTransitionPeaks(nodeTran);
            if (transitionPeaks == null || position < 0 || position >= transitionPeaks.PositionCount)
            {
                return null;
            }

            var peak = GetPeak(nodeTran, position, candidatePeakIndex) ?? ChromPeak.EMPTY;
            return new TransitionChromInfo(transitionPeaks.ChromFileIds.FileIds[position],
                transitionPeaks.OptimizationSteps[position], peak, transitionPeaks.IonMobilities[position],
                annotations ?? Annotations.EMPTY, userSet);
        }

        /// <summary>
        /// Finds the candidate peak whose boundaries match those recorded on
        /// <paramref name="chromInfo"/>. Returns -1 when the peak did not come from the cache,
        /// which is what happens when the user set the boundaries themselves.
        /// <para>
        /// This is the bridge for as long as the document still holds the peak boundaries. Once
        /// the chosen index is stored on <see cref="TransitionGroupResults"/> it is read from
        /// there instead of searched for.
        /// </para>
        /// </summary>
        public int FindCandidatePeakIndex(TransitionDocNode nodeTran, int position, TransitionChromInfo chromInfo)
        {
            var peaks = GetTransitionPeaks(nodeTran)?.Peaks;
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

        /// <summary>
        /// Rebuilds the group level values for one replicate by driving the same calculator that
        /// <see cref="TransitionGroupDocNode.ChangeResults"/> uses, so that there is only one
        /// implementation of the aggregation.
        /// <para>
        /// <paramref name="previousChromInfos"/> supplies the values which are carried forward
        /// rather than recalculated - the scores, the annotations and the reintegrated peak.
        /// Those come from <see cref="TransitionGroupResults"/> once the document holds them
        /// there.
        /// </para>
        /// </summary>
        public TransitionGroupChromInfos MakeTransitionGroupChromInfos(TransitionGroupDocNode nodeGroup,
            int replicateIndex, ChromInfoList<TransitionGroupChromInfo> previousChromInfos,
            Func<TransitionDocNode, IList<TransitionChromInfo>> getTransitionChromInfos)
        {
            var nodeTrans = nodeGroup.Transitions.ToArray();
            var chromInfoLists = new IList<TransitionChromInfo>[nodeTrans.Length];
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                chromInfoLists[iTran] = getTransitionChromInfos(nodeTrans[iTran]);
            }

            // Rank first, because the ranks live on the transition chrom infos, and gather the
            // areas each dot product needs while walking the same file and optimization steps.
            var correlations = new List<FileStepCorrelation>();
            foreach (var fileStep in GetFileSteps(chromInfoLists))
            {
                var correlation = RankFileStep(nodeGroup, nodeTrans, chromInfoLists, fileStep);
                if (correlation != null)
                {
                    correlations.Add(correlation);
                }
            }

            var listCalculator = new TransitionGroupDocNode.TransitionGroupChromInfoListCalculator(Settings,
                PeptideDocNode, replicateIndex, nodeGroup.TransitionCount, previousChromInfos);
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                listCalculator.AddChromInfoList(nodeTrans[iTran], chromInfoLists[iTran]);
            }

            // Recalculated from the chromatogram rather than carried forward, which is what
            // ChangeResults does, so it never has to be stored.
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromGroupInfo in GetChromatogramGroupInfos(nodeGroup, replicateIndex))
            {
                var fileId = chromatograms.FindFile(chromGroupInfo);
                if (fileId != null)
                {
                    listCalculator.SetOriginalPeak(fileId, nodeGroup.GetOriginalPeak(chromGroupInfo, MzMatchTolerance));
                }
            }

            // Has to come after AddChromInfoList, which is what creates the calculators these
            // look up by file and optimization step.
            foreach (var correlation in correlations)
            {
                if (correlation.PeakAreas != null)
                {
                    listCalculator.SetLibInfo(correlation.FileId, correlation.OptimizationStep,
                        correlation.PeakAreas, correlation.LibIntensities);
                }

                if (correlation.PeakAreasMs != null)
                {
                    listCalculator.SetIsotopeDistInfo(correlation.FileId, correlation.OptimizationStep,
                        correlation.PeakAreasMs, correlation.IsotopeProportionsMs);
                }
            }

            return new TransitionGroupChromInfos(listCalculator.CalcChromInfoList(), nodeTrans, chromInfoLists);
        }

        /// <summary>
        /// The distinct file and optimization steps present, in the order they first appear.
        /// </summary>
        private static IEnumerable<FileStep> GetFileSteps(IList<TransitionChromInfo>[] chromInfoLists)
        {
            var fileSteps = new List<FileStep>();
            foreach (var chromInfoList in chromInfoLists)
            {
                if (chromInfoList == null)
                {
                    continue;
                }

                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo != null && !fileSteps.Any(fileStep => fileStep.Matches(chromInfo)))
                    {
                        fileSteps.Add(new FileStep(chromInfo.FileId, chromInfo.OptimizationStep));
                    }
                }
            }

            return fileSteps;
        }

        /// <summary>
        /// Assigns the ranks for one file and optimization step, and gathers the areas the dot
        /// products are calculated from. This mirrors RankAndCorrelateTransitions in
        /// <see cref="TransitionGroupDocNode"/>.
        /// <para>
        /// The library intensities and isotope proportions come off the
        /// <see cref="TransitionDocNode"/> itself, not from the library file. They do not vary by
        /// replicate, so they are not part of what the document stops holding.
        /// </para>
        /// </summary>
        private FileStepCorrelation RankFileStep(TransitionGroupDocNode nodeGroup, TransitionDocNode[] nodeTrans,
            IList<TransitionChromInfo>[] chromInfoLists, FileStep fileStep)
        {
            bool isFullScanMs = Settings.TransitionSettings.FullScan.IsEnabledMs;
            var correlation = new FileStepCorrelation(fileStep.FileId, fileStep.OptimizationStep);
            if (nodeGroup.HasLibInfo)
            {
                int countTransMsMs = nodeGroup.GetMsMsTransitions(isFullScanMs).Count(t => t.ParticipatesInScoring);
                if (countTransMsMs >= TransitionGroupDocNode.MIN_DOT_PRODUCT_TRANSITIONS)
                {
                    correlation.PeakAreas = new double[countTransMsMs];
                    correlation.LibIntensities = new double[countTransMsMs];
                }
            }

            if (nodeGroup.HasIsotopeDist)
            {
                int countTransMs = nodeGroup.GetMsTransitions(isFullScanMs).Count();
                if (countTransMs >= TransitionGroupDocNode.MIN_DOT_PRODUCT_MS1_TRANSITIONS)
                {
                    correlation.PeakAreasMs = new double[countTransMs];
                    correlation.IsotopeProportionsMs = new double[countTransMs];
                }
            }

            var ranked = new List<RankedTransition>();
            int countInfo = 0, countLibTrans = 0, countIsoTrans = 0;
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                var nodeTran = nodeTrans[iTran];
                var chromInfo = FindChromInfo(chromInfoLists[iTran], fileStep);
                ranked.Add(new RankedTransition(iTran, nodeTran.IsMs1, chromInfo, nodeTran.ParticipatesInScoring));
                if (chromInfo != null)
                {
                    countInfo++;
                }

                if (correlation.PeakAreas != null && (!isFullScanMs || !nodeTran.IsMs1) &&
                    nodeTran.ParticipatesInScoring)
                {
                    correlation.PeakAreas[countLibTrans] = chromInfo?.Area ?? 0;
                    correlation.LibIntensities[countLibTrans] = nodeTran.HasLibInfo ? nodeTran.LibInfo.Intensity : 0;
                    countLibTrans++;
                }

                if (correlation.PeakAreasMs != null && nodeTran.IsMs1)
                {
                    correlation.PeakAreasMs[countIsoTrans] = chromInfo?.Area ?? 0;
                    correlation.IsotopeProportionsMs[countIsoTrans] =
                        nodeTran.HasDistInfo ? nodeTran.IsotopeDistInfo.Proportion : 0;
                    countIsoTrans++;
                }
            }

            if (countInfo == 0)
            {
                return null;
            }

            ranked.Sort((first, second) => Comparer<float>.Default.Compare(second.RankArea, first.RankArea));
            short rankMs = 0, rankMsMs = 0;
            for (int iRank = 0; iRank < ranked.Count; iRank++)
            {
                var rankedTransition = ranked[iRank];
                if (rankedTransition.ChromInfo == null)
                {
                    continue;
                }

                short rank = 0, rankByLevel = 0;
                if (rankedTransition.ChromInfo.Area > 0)
                {
                    rank = (short) (iRank + 1);
                    rankByLevel = rankedTransition.IsMs1 ? ++rankMs : ++rankMsMs;
                }

                // These were read by this object and are not shared, so they can be changed
                // without copying, the same as during results calculation.
                rankedTransition.ChromInfo.ChangeRank(false, rank, rankByLevel);
            }

            return correlation;
        }

        private static TransitionChromInfo FindChromInfo(IList<TransitionChromInfo> chromInfoList, FileStep fileStep)
        {
            if (chromInfoList == null)
            {
                return null;
            }

            return chromInfoList.FirstOrDefault(chromInfo => chromInfo != null && fileStep.Matches(chromInfo));
        }

        private void AssertNotRead()
        {
            if (_transitions != null)
            {
                throw new InvalidOperationException(@"The chromatograms have already been read");
            }
        }

        /// <summary>
        /// Reads everything about the peaks of this molecule out of the chromatogram cache. This
        /// happens once, the first time anything is asked for.
        /// </summary>
        private void EnsureRead()
        {
            if (_transitions != null)
            {
                return;
            }

            var valueCache = ValueCache ?? new ValueCache();
            var transitions = new Dictionary<ReferenceValue<Transition>, TransitionPeaks>();
            var chromatogramGroupInfos = new Dictionary<GroupReplicateKey, ImmutableList<ChromatogramGroupInfo>>();
            var measuredResults = Settings.MeasuredResults;
            if (measuredResults != null)
            {
                foreach (var nodeGroup in PeptideDocNode.TransitionGroups)
                {
                    ReadTransitionGroup(measuredResults, nodeGroup, valueCache, transitions, chromatogramGroupInfos);
                }
            }

            _chromatogramGroupInfos = chromatogramGroupInfos;
            _transitions = transitions;
        }

        private void ReadTransitionGroup(MeasuredResults measuredResults, TransitionGroupDocNode nodeGroup,
            ValueCache valueCache, IDictionary<ReferenceValue<Transition>, TransitionPeaks> transitions,
            IDictionary<GroupReplicateKey, ImmutableList<ChromatogramGroupInfo>> chromatogramGroupInfos)
        {
            var builders = nodeGroup.Transitions.ToDictionary(
                nodeTran => ReferenceValue.Of(nodeTran.Transition), nodeTran => new TransitionPeaksBuilder());
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                if (!ReplicateIndex.HasValue || ReplicateIndex.Value == replicateIndex)
                {
                    var chromGroupInfos = ReadReplicate(measuredResults,
                        measuredResults.Chromatograms[replicateIndex], nodeGroup, builders);
                    if (chromGroupInfos.Count > 0)
                    {
                        chromatogramGroupInfos[new GroupReplicateKey(nodeGroup.TransitionGroup, replicateIndex)] =
                            ImmutableList.ValueOf(chromGroupInfos);
                    }
                }

                // Even a replicate which was skipped ends here, so that the positions stay
                // aligned with the replicate they belong to.
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

        /// <summary>
        /// Appends the positions contributed by one replicate. The ordering has to match the
        /// order in which <see cref="TransitionGroupDocNode.ChangeResults"/> builds its lists:
        /// file major, optimization step minor, skipping any file where the transition has no
        /// chromatogram.
        /// </summary>
        private IList<ChromatogramGroupInfo> ReadReplicate(MeasuredResults measuredResults,
            ChromatogramSet chromatograms, TransitionGroupDocNode nodeGroup,
            IDictionary<ReferenceValue<Transition>, TransitionPeaksBuilder> builders)
        {
            float tolerance = MzMatchTolerance;
            if (!measuredResults.TryLoadChromatogram(chromatograms, PeptideDocNode, nodeGroup, tolerance,
                    out var chromGroupInfos))
            {
                return Array.Empty<ChromatogramGroupInfo>();
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

                    var builder = builders[nodeTran.Transition];
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

            return chromGroupInfos;
        }

        private class TransitionPeaksBuilder
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

            public TransitionPeaks Build(ValueCache valueCache)
            {
                var chromFileIds = ChromFileIds.Intern(valueCache, ReplicatePositions.FromCounts(_counts), _fileIds);
                return new TransitionPeaks(chromFileIds, OptimizationSteps, IonMobilities, Peaks);
            }
        }

        /// <summary>
        /// Identifies the chromatograms of one transition group in one replicate.
        /// </summary>
        private struct GroupReplicateKey
        {
            public GroupReplicateKey(TransitionGroup transitionGroup, int replicateIndex)
            {
                TransitionGroup = transitionGroup;
                ReplicateIndex = replicateIndex;
            }

            public ReferenceValue<TransitionGroup> TransitionGroup { get; }
            public int ReplicateIndex { get; }

            public override bool Equals(object obj)
            {
                return obj is GroupReplicateKey other && Equals(TransitionGroup, other.TransitionGroup) &&
                       ReplicateIndex == other.ReplicateIndex;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (TransitionGroup.GetHashCode() * 397) ^ ReplicateIndex;
                }
            }
        }

        private struct FileStep
        {
            public FileStep(ChromFileInfoId fileId, int optimizationStep)
            {
                FileId = fileId;
                OptimizationStep = optimizationStep;
            }

            public ChromFileInfoId FileId { get; }
            public int OptimizationStep { get; }

            public bool Matches(TransitionChromInfo chromInfo)
            {
                return ReferenceEquals(FileId, chromInfo.FileId) && OptimizationStep == chromInfo.OptimizationStep;
            }
        }

        private class FileStepCorrelation
        {
            public FileStepCorrelation(ChromFileInfoId fileId, int optimizationStep)
            {
                FileId = fileId;
                OptimizationStep = optimizationStep;
            }

            public ChromFileInfoId FileId { get; }
            public int OptimizationStep { get; }
            public double[] PeakAreas { get; set; }
            public double[] LibIntensities { get; set; }
            public double[] PeakAreasMs { get; set; }
            public double[] IsotopeProportionsMs { get; set; }
        }

        private class RankedTransition
        {
            public RankedTransition(int index, bool isMs1, TransitionChromInfo chromInfo, bool participatesInScoring)
            {
                Index = index;
                IsMs1 = isMs1;
                ChromInfo = chromInfo;
                ParticipatesInScoring = participatesInScoring;
            }

            public int Index { get; }
            public bool IsMs1 { get; }
            public TransitionChromInfo ChromInfo { get; }
            public bool ParticipatesInScoring { get; }

            public float RankArea
            {
                get { return ParticipatesInScoring ? ChromInfo?.Area ?? 0 : -1.0f; }
            }
        }
    }

    /// <summary>
    /// One replicate of one transition group, rebuilt from the chromatogram cache: the group
    /// level values, and the transition chrom infos with their ranks assigned.
    /// </summary>
    public class TransitionGroupChromInfos
    {
        private readonly Dictionary<ReferenceValue<Transition>, IList<TransitionChromInfo>> _transitionChromInfos;

        public TransitionGroupChromInfos(IList<TransitionGroupChromInfo> chromInfos,
            IList<TransitionDocNode> nodeTrans, IList<IList<TransitionChromInfo>> transitionChromInfos)
        {
            ChromInfos = chromInfos;
            _transitionChromInfos = new Dictionary<ReferenceValue<Transition>, IList<TransitionChromInfo>>();
            for (int iTran = 0; iTran < nodeTrans.Count; iTran++)
            {
                _transitionChromInfos[nodeTrans[iTran].Transition] = transitionChromInfos[iTran];
            }
        }

        public IList<TransitionGroupChromInfo> ChromInfos { get; }

        public IList<TransitionChromInfo> GetTransitionChromInfos(TransitionDocNode nodeTran)
        {
            _transitionChromInfos.TryGetValue(nodeTran.Transition, out var chromInfos);
            return chromInfos;
        }
    }

    /// <summary>
    /// Everything read for one transition: which file and optimization step each flat position
    /// belongs to, and every candidate peak found there.
    /// </summary>
    public class TransitionPeaks
    {
        public TransitionPeaks(ChromFileIds chromFileIds, IEnumerable<int> optimizationSteps,
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
}
