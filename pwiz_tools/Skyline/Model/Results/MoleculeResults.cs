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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Answers questions about the results of one <see cref="PeptideDocNode"/>, in any
    /// replicate, reading what it needs from the chromatogram cache and holding on to it.
    /// <para>
    /// This exists because a <see cref="DocNode"/> is to stop being the place complete result
    /// information comes from. A <see cref="TransitionResults"/> holds only the areas, and a
    /// <see cref="TransitionGroupResults"/> only the areas, retention times, chosen candidate
    /// peak indexes and the few things which cannot be derived from the .skyd file. Everything
    /// else is rebuilt here.
    /// </para>
    /// <para>
    /// A peak which is one of the candidate peaks Skyline found costs only the
    /// <see cref="ChromPeak"/> records, which are small and are read for the whole molecule at
    /// once. A peak whose boundaries the user set is not in the cache at all and has to be
    /// integrated again, which means decompressing the chromatogram itself - far more
    /// expensive, and why <see cref="TransitionGroupIntegrator"/> instances are kept.
    /// </para>
    /// <para>
    /// One of these may use as much memory as it likes. Few exist at a time: the currently
    /// selected molecule, and short lived ones made per molecule while results are being
    /// recalculated. Interning is for values on their way somewhere long lived, such as a
    /// <see cref="DocNode"/>, and nothing here is.
    /// </para>
    /// <para>
    /// One instance is not meant to be used from more than one thread, since it reads on
    /// demand.
    /// </para>
    /// </summary>
    public class MoleculeResults
    {
        // Indexed the way the doc node children are, since FindNodeIndex makes that a fast
        // lookup from the identity and there is no need for a dictionary of our own.
        private ImmutableList<ReplicateMap<ChromatogramGroupInfo>> _chromatogramGroupInfos;
        private ImmutableList<ImmutableList<TransitionPeaks>> _transitionPeaks;
        private GroupResults[] _groupResults;
        private readonly Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator> _integrators =
            new Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator>();

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

        private int ReplicateCount
        {
            get { return Settings.MeasuredResults?.Chromatograms.Count ?? 0; }
        }

        /// <summary>
        /// The complete transition level results, rebuilt from the chromatogram cache.
        /// </summary>
        public Results<TransitionChromInfo> GetTransitionResults(TransitionGroup transitionGroup,
            Transition transition)
        {
            var groupResults = GetGroupResults(transitionGroup);
            int transitionIndex = FindTransitionGroup(transitionGroup)?.FindNodeIndex(transition) ?? -1;
            if (groupResults == null || transitionIndex < 0)
            {
                return null;
            }

            return groupResults.TransitionResults[transitionIndex];
        }

        public ChromInfoList<TransitionChromInfo> GetTransitionChromInfos(TransitionGroup transitionGroup,
            Transition transition, int replicateIndex)
        {
            var results = GetTransitionResults(transitionGroup, transition);
            if (results == null || replicateIndex < 0 || replicateIndex >= results.Count)
            {
                return default;
            }

            return results[replicateIndex];
        }

        /// <summary>
        /// The complete precursor level results, aggregated from the transition level values
        /// the same way <see cref="TransitionGroupDocNode.ChangeResults"/> aggregates them.
        /// </summary>
        public Results<TransitionGroupChromInfo> GetTransitionGroupResults(TransitionGroup transitionGroup)
        {
            return GetGroupResults(transitionGroup)?.ChromInfos;
        }

        public ChromInfoList<TransitionGroupChromInfo> GetTransitionGroupChromInfos(TransitionGroup transitionGroup,
            int replicateIndex)
        {
            var results = GetTransitionGroupResults(transitionGroup);
            if (results == null || replicateIndex < 0 || replicateIndex >= results.Count)
            {
                return default;
            }

            return results[replicateIndex];
        }

        /// <summary>
        /// The chromatograms which were read, kept because code such as GraphChromatogram and
        /// the on demand feature calculator needs the chromatogram itself and not only the peaks
        /// taken from it.
        /// </summary>
        public IEnumerable<ChromatogramGroupInfo> GetChromatogramGroupInfos(TransitionGroup transitionGroup,
            int replicateIndex)
        {
            EnsureRead();
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            if (groupIndex < 0 || replicateIndex < 0 || replicateIndex >= ReplicateCount)
            {
                return ImmutableList<ChromatogramGroupInfo>.EMPTY;
            }

            return _chromatogramGroupInfos[groupIndex][replicateIndex];
        }

        public TransitionPeaks GetTransitionPeaks(TransitionDocNode nodeTran)
        {
            EnsureRead();
            int groupIndex = PeptideDocNode.FindNodeIndex(nodeTran.Transition.Group);
            if (groupIndex < 0)
            {
                return null;
            }

            int transitionIndex = ((TransitionGroupDocNode) PeptideDocNode.Children[groupIndex])
                .FindNodeIndex(nodeTran.Transition);
            return transitionIndex < 0 ? null : _transitionPeaks[groupIndex][transitionIndex];
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
            if (replicatePositions == null || replicateIndex >= replicatePositions.ReplicateCount)
            {
                return Array.Empty<int>();
            }

            return Enumerable.Range(replicatePositions.GetStart(replicateIndex),
                replicatePositions.GetCount(replicateIndex));
        }

        private TransitionGroupDocNode FindTransitionGroup(TransitionGroup transitionGroup)
        {
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            return groupIndex < 0 ? null : (TransitionGroupDocNode) PeptideDocNode.Children[groupIndex];
        }

        private GroupResults GetGroupResults(TransitionGroup transitionGroup)
        {
            EnsureRead();
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            if (groupIndex < 0 || ReplicateCount == 0)
            {
                return null;
            }

            // Nothing else returns null, so there is no need to remember that a group has
            // already been calculated.
            return _groupResults[groupIndex] ??=
                CalcGroupResults((TransitionGroupDocNode) PeptideDocNode.Children[groupIndex]);
        }

        /// <summary>
        /// Rebuilds every replicate of one transition group at once, because the group level
        /// values and the ranks are calculated from all of the transitions together.
        /// </summary>
        private GroupResults CalcGroupResults(TransitionGroupDocNode nodeGroup)
        {
            var nodeTrans = nodeGroup.Transitions.ToArray();
            var groupChromInfoLists = new List<ChromInfoList<TransitionGroupChromInfo>>(ReplicateCount);
            var transitionChromInfoLists = nodeTrans
                .Select(nodeTran => new List<ChromInfoList<TransitionChromInfo>>(ReplicateCount)).ToArray();
            for (int replicateIndex = 0; replicateIndex < ReplicateCount; replicateIndex++)
            {
                var chromInfoLists = new IList<TransitionChromInfo>[nodeTrans.Length];
                for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
                {
                    chromInfoLists[iTran] = MakeTransitionChromInfos(nodeGroup, nodeTrans[iTran], replicateIndex);
                }

                groupChromInfoLists.Add(new ChromInfoList<TransitionGroupChromInfo>(
                    MakeTransitionGroupChromInfos(nodeGroup, replicateIndex, chromInfoLists)));
                for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
                {
                    transitionChromInfoLists[iTran]
                        .Add(new ChromInfoList<TransitionChromInfo>(chromInfoLists[iTran] ?? new TransitionChromInfo[0]));
                }
            }

            return new GroupResults(new Results<TransitionGroupChromInfo>(groupChromInfoLists),
                transitionChromInfoLists.Select(lists => new Results<TransitionChromInfo>(lists)));
        }

        /// <summary>
        /// Rebuilds the chrom infos of one transition in one replicate, one per flat position.
        /// Returns null when the positions the cache produced do not line up with the ones the
        /// document holds, which is the only case where the values here cannot be trusted.
        /// </summary>
        private IList<TransitionChromInfo> MakeTransitionChromInfos(TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran, int replicateIndex)
        {
            var transitionPeaks = GetTransitionPeaks(nodeTran);
            if (transitionPeaks == null || nodeTran.Results == null || replicateIndex >= nodeTran.Results.Count)
            {
                return null;
            }

            var documentChromInfos = nodeTran.Results[replicateIndex];
            var positions = GetPositions(nodeTran, replicateIndex).ToArray();
            if (positions.Length != documentChromInfos.Count)
            {
                return null;
            }

            var chromInfos = new List<TransitionChromInfo>(positions.Length);
            for (int i = 0; i < positions.Length; i++)
            {
                chromInfos.Add(MakeTransitionChromInfo(nodeGroup, nodeTran, replicateIndex, positions[i],
                    documentChromInfos[i]));
            }

            return chromInfos;
        }

        /// <summary>
        /// Rebuilds the complete <see cref="TransitionChromInfo"/> for one position. The ion
        /// mobility comes from the chromatogram rather than from the document, because the
        /// document is to stop holding it.
        /// <para>
        /// <paramref name="documentChromInfo"/> is the one thing here still taken off the doc
        /// node: which of the candidate peaks was chosen, or the boundaries the user set when
        /// none of them was. That is exactly what <see cref="TransitionGroupResults.ChosenPeakIndexes"/>
        /// and <see cref="CustomPeak.StartTime"/> are for, and this stops looking at the doc
        /// node once the document carries them.
        /// </para>
        /// </summary>
        private TransitionChromInfo MakeTransitionChromInfo(TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran, int replicateIndex, int position, TransitionChromInfo documentChromInfo)
        {
            var transitionPeaks = GetTransitionPeaks(nodeTran);
            var fileId = transitionPeaks.ChromFileIds.FileIds[position];
            int optimizationStep = transitionPeaks.OptimizationSteps[position];
            var results = nodeTran.AbbreviatedResults;
            var customPeak = results?.GetCustomPeak(position);
            var chromPeak = GetChromPeak(nodeGroup, nodeTran, replicateIndex, position, fileId, optimizationStep,
                documentChromInfo);
            return new TransitionChromInfo(fileId, optimizationStep, chromPeak,
                transitionPeaks.IonMobilities[position], customPeak?.Annotations ?? Annotations.EMPTY,
                results?.GetUserSet(position) ?? UserSet.FALSE);
        }

        private ChromPeak GetChromPeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran,
            int replicateIndex, int position, ChromFileInfoId fileId, int optimizationStep,
            TransitionChromInfo documentChromInfo)
        {
            if (documentChromInfo.IsEmpty)
            {
                return ChromPeak.EMPTY;
            }

            int peakIndex = FindCandidatePeakIndex(nodeTran, position, documentChromInfo);
            if (peakIndex >= 0)
            {
                return GetPeak(nodeTran, position, peakIndex) ?? ChromPeak.EMPTY;
            }

            return IntegratePeak(nodeGroup, nodeTran, replicateIndex, fileId, optimizationStep, documentChromInfo);
        }

        /// <summary>
        /// Integrates the chromatogram again between boundaries which are not those of any
        /// candidate peak, which is what the user setting the boundaries produces. This is the
        /// expensive path: it decompresses the chromatogram, which is why the integrator for a
        /// file is kept once it has been made.
        /// </summary>
        private ChromPeak IntegratePeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran,
            int replicateIndex, ChromFileInfoId fileId, int optimizationStep,
            TransitionChromInfo documentChromInfo)
        {
            var integrator = GetIntegrator(nodeGroup, replicateIndex, fileId);
            if (integrator == null)
            {
                return ChromPeak.EMPTY;
            }

            // Identification is not a property of the boundaries, so integrating again cannot
            // find it. It has to be carried, the same as the boundaries themselves.
            var flags = default(ChromPeak.FlagValues);
            if (documentChromInfo.Identified != PeakIdentification.FALSE)
            {
                flags |= ChromPeak.FlagValues.contains_id;
            }

            if (documentChromInfo.Identified == PeakIdentification.ALIGNED)
            {
                flags |= ChromPeak.FlagValues.used_id_alignment;
            }

            return integrator.CalcPeak(nodeTran.Transition, optimizationStep,
                documentChromInfo.StartRetentionTime, documentChromInfo.EndRetentionTime, flags);
        }

        private TransitionGroupIntegrator GetIntegrator(TransitionGroupDocNode nodeGroup, int replicateIndex,
            ChromFileInfoId fileId)
        {
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromGroupInfo in GetChromatogramGroupInfos(nodeGroup.TransitionGroup, replicateIndex))
            {
                if (!ReferenceEquals(fileId, chromatograms.FindFile(chromGroupInfo)))
                {
                    continue;
                }

                if (!_integrators.TryGetValue(chromGroupInfo, out var integrator))
                {
                    integrator = new TransitionGroupIntegrator(Settings, nodeGroup, chromatograms, chromGroupInfo);
                    _integrators.Add(chromGroupInfo, integrator);
                }

                return integrator;
            }

            return null;
        }

        /// <summary>
        /// Finds the candidate peak whose boundaries match those recorded on
        /// <paramref name="chromInfo"/>. Returns -1 when the peak did not come from the cache,
        /// which is what happens when the user set the boundaries themselves.
        /// </summary>
        private int FindCandidatePeakIndex(TransitionDocNode nodeTran, int position, TransitionChromInfo chromInfo)
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
        /// The chrom infos the doc node holds supply the values which are carried forward rather
        /// than recalculated - the scores, the annotations and the reintegrated peak. Those come
        /// from <see cref="TransitionGroupResults"/> once the document holds them there.
        /// </para>
        /// </summary>
        private IList<TransitionGroupChromInfo> MakeTransitionGroupChromInfos(TransitionGroupDocNode nodeGroup,
            int replicateIndex, IList<TransitionChromInfo>[] chromInfoLists)
        {
            var nodeTrans = nodeGroup.Transitions.ToArray();

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

            var previousChromInfos = nodeGroup.Results != null && replicateIndex < nodeGroup.Results.Count
                ? nodeGroup.Results[replicateIndex]
                : default;
            var listCalculator = new TransitionGroupDocNode.TransitionGroupChromInfoListCalculator(Settings,
                PeptideDocNode, replicateIndex, nodeGroup.TransitionCount, previousChromInfos);
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                listCalculator.AddChromInfoList(nodeTrans[iTran], chromInfoLists[iTran]);
            }

            // Recalculated from the chromatogram rather than carried forward, which is what
            // ChangeResults does, so it never has to be stored.
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromGroupInfo in GetChromatogramGroupInfos(nodeGroup.TransitionGroup, replicateIndex))
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

            return listCalculator.CalcChromInfoList();
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

                // These were made here and are not shared, so they can be changed without
                // copying, the same as during results calculation.
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

        /// <summary>
        /// Reads every candidate peak of this molecule out of the chromatogram cache. This
        /// happens once, the first time anything is asked for.
        /// </summary>
        private void EnsureRead()
        {
            if (_transitionPeaks != null)
            {
                return;
            }

            var chromatogramGroupInfos = new List<ReplicateMap<ChromatogramGroupInfo>>();
            var transitionPeaks = new List<ImmutableList<TransitionPeaks>>();
            var measuredResults = Settings.MeasuredResults;
            foreach (var nodeGroup in PeptideDocNode.TransitionGroups)
            {
                if (measuredResults == null)
                {
                    chromatogramGroupInfos.Add(ReplicateMap<ChromatogramGroupInfo>.EMPTY);
                    transitionPeaks.Add(ImmutableList<TransitionPeaks>.EMPTY);
                    continue;
                }

                chromatogramGroupInfos.Add(ReadTransitionGroup(measuredResults, nodeGroup, out var groupPeaks));
                transitionPeaks.Add(groupPeaks);
            }

            _chromatogramGroupInfos = ImmutableList.ValueOf(chromatogramGroupInfos);
            _groupResults = new GroupResults[transitionPeaks.Count];
            _transitionPeaks = ImmutableList.ValueOf(transitionPeaks);
        }

        private ReplicateMap<ChromatogramGroupInfo> ReadTransitionGroup(MeasuredResults measuredResults,
            TransitionGroupDocNode nodeGroup, out ImmutableList<TransitionPeaks> transitionPeaks)
        {
            var builders = nodeGroup.Transitions.Select(nodeTran => new TransitionPeaksBuilder()).ToArray();
            var chromatogramGroupInfos = new List<IList<ChromatogramGroupInfo>>();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                chromatogramGroupInfos.Add(ReadReplicate(measuredResults,
                    measuredResults.Chromatograms[replicateIndex], nodeGroup, builders));
                foreach (var builder in builders)
                {
                    builder.EndReplicate();
                }
            }

            transitionPeaks = ImmutableList.ValueOf(builders.Select(builder => builder.Build()));
            return new ReplicateMap<ChromatogramGroupInfo>(chromatogramGroupInfos);
        }

        /// <summary>
        /// Appends the positions contributed by one replicate. The ordering has to match the
        /// order in which <see cref="TransitionGroupDocNode.ChangeResults"/> builds its lists:
        /// file major, optimization step minor, skipping any file where the transition has no
        /// chromatogram.
        /// </summary>
        private IList<ChromatogramGroupInfo> ReadReplicate(MeasuredResults measuredResults,
            ChromatogramSet chromatograms, TransitionGroupDocNode nodeGroup, TransitionPeaksBuilder[] builders)
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

                for (int iTran = 0; iTran < nodeGroup.TransitionCount; iTran++)
                {
                    var nodeTran = (TransitionDocNode) nodeGroup.Children[iTran];

                    // Optimization steps are separate chromatograms of the same transition, and
                    // each one has its own set of candidate peaks.
                    var optStepChromatograms = chromGroupInfo.GetAllTransitionInfo(nodeTran, tolerance,
                        chromatograms.OptimizationFunction, TransformChrom.interpolated);
                    if (optStepChromatograms.IsEmpty)
                    {
                        continue;
                    }

                    var builder = builders[iTran];
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

            public TransitionPeaks Build()
            {
                var chromFileIds = new ChromFileIds(ReplicatePositions.FromCounts(_counts), _fileIds);
                return new TransitionPeaks(chromFileIds, OptimizationSteps, IonMobilities, Peaks);
            }
        }

        /// <summary>
        /// Everything rebuilt for one transition group, which is calculated for all of the
        /// replicates at once because the group level values need all of the transitions.
        /// </summary>
        private class GroupResults
        {
            public GroupResults(Results<TransitionGroupChromInfo> chromInfos,
                IEnumerable<Results<TransitionChromInfo>> transitionResults)
            {
                ChromInfos = chromInfos;
                TransitionResults = ImmutableList.ValueOf(transitionResults);
            }

            public Results<TransitionGroupChromInfo> ChromInfos { get; }
            public ImmutableList<Results<TransitionChromInfo>> TransitionResults { get; }
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

        private IEnumerable<ReplicateMap<ChromatogramGroupInfo>> ReadChromatogramGroupInfos()
        {
            if (Settings.MeasuredResults == null)
            {
                return PeptideDocNode.TransitionGroups.Select(tg => ReplicateMap<ChromatogramGroupInfo>.EMPTY);
            }

            return PeptideDocNode.TransitionGroups.Select(tg => new ReplicateMap<ChromatogramGroupInfo>(
                Settings.MeasuredResults.LoadChromatogramsForAllReplicates(PeptideDocNode, tg,
                    MzMatchTolerance)));
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
