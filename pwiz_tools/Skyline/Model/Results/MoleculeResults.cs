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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Answers questions about the results of one <see cref="PeptideDocNode"/>, in any
    /// replicate, by reading the chromatogram cache.
    /// <para>
    /// This exists because a <see cref="DocNode"/> is to stop being the place complete result
    /// information comes from. A <see cref="TransitionResults"/> holds only the areas, and a
    /// <see cref="TransitionGroupResults"/> only the areas, retention times, chosen candidate
    /// peak indexes and the few things which cannot be derived from the .skyd file. Code which
    /// really does want the chrom infos comes here for them.
    /// </para>
    /// <para>
    /// A peak which is one of the candidate peaks Skyline found costs only the
    /// <see cref="ChromPeak"/> records. A peak whose boundaries the user set is not in the cache
    /// at all and has to be integrated again, which means decompressing the chromatogram - far
    /// more expensive, and why the <see cref="TransitionGroupIntegrator"/> for a file is kept
    /// once it has been made.
    /// </para>
    /// <para>
    /// Only the chromatograms are held. The chrom infos are rebuilt on each call, and asking for
    /// one transition rebuilds its whole transition group, because the ranks and the dot products
    /// are calculated from all of the transitions together. Making that fast comes later, and the
    /// way it gets fast is by fewer callers needing these objects at all: the retention times and
    /// areas that most of them want will be on the doc nodes.
    /// </para>
    /// <para>
    /// One of these may use as much memory as it likes. Few exist at a time: the currently
    /// selected molecule, and short lived ones made per molecule while results are being
    /// recalculated.
    /// </para>
    /// <para>
    /// One instance is not meant to be used from more than one thread, since it reads on demand.
    /// </para>
    /// </summary>
    public class MoleculeResults
    {
        /// <summary>
        /// One entry per transition group, in the order the doc node's children are in, since
        /// <see cref="DocNodeParent.FindNodeIndex(Identity)"/> makes that a fast lookup.
        /// </summary>
        private ImmutableList<ChromFileIdMap<ChromatogramGroupInfo>> _chromatogramGroupInfos;

        private readonly Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator> _integrators =
            new Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator>();

        /// <summary>
        /// What has been worked out so far, one entry per transition group, indexed the same way.
        /// Rebuilding a precursor means rebuilding every one of its transitions, so it is worth
        /// keeping: whoever asked for one of them usually goes on to ask for the rest.
        /// </summary>
        private GroupResults[] _groupResults;

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
        /// The complete transition level results, rebuilt from the chromatogram cache. This is what
        /// a caller uses instead of <see cref="TransitionDocNode.Results"/>, which always reports
        /// empty now.
        /// <para>
        /// Named for what it returns rather than "results", which at every level here now means the
        /// columnar form: <see cref="TransitionResults"/> is a class of its own, and a
        /// GetTransitionResults would read as returning one.
        /// </para>
        /// </summary>
        public Results<TransitionChromInfo> GetTransitionChromInfos(TransitionGroup transitionGroup,
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
            var results = GetTransitionChromInfos(transitionGroup, transition);
            if (results == null || replicateIndex < 0 || replicateIndex >= results.Count)
            {
                return default;
            }

            return results[replicateIndex];
        }

        /// <summary>
        /// The complete precursor level results, aggregated from the transition level values the
        /// same way <see cref="TransitionGroupDocNode.ChangeResults"/> aggregates them. This is what
        /// a caller uses instead of <see cref="TransitionGroupDocNode.Results"/>.
        /// </summary>
        public Results<TransitionGroupChromInfo> GetTransitionGroupChromInfos(TransitionGroup transitionGroup)
        {
            return GetGroupResults(transitionGroup)?.ChromInfos;
        }

        public ChromInfoList<TransitionGroupChromInfo> GetTransitionGroupChromInfos(TransitionGroup transitionGroup,
            int replicateIndex)
        {
            var results = GetTransitionGroupChromInfos(transitionGroup);
            if (results == null || replicateIndex < 0 || replicateIndex >= results.Count)
            {
                return default;
            }

            return results[replicateIndex];
        }

        /// <summary>
        /// Everything for one precursor, worked out once. Both levels come out of the same pass:
        /// the ranks and the dot products are calculated from all of the transitions together, and
        /// the precursor values are aggregated from the transition values.
        /// </summary>
        private GroupResults GetGroupResults(TransitionGroup transitionGroup)
        {
            EnsureRead();
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            if (groupIndex < 0 || ReplicateCount == 0)
            {
                return null;
            }

            // Nothing else returns null, so there is no need to remember that a precursor has
            // already been worked out.
            return _groupResults[groupIndex] ??=
                CalcGroupResults((TransitionGroupDocNode) PeptideDocNode.Children[groupIndex]);
        }

        private GroupResults CalcGroupResults(TransitionGroupDocNode nodeGroup)
        {
            var groupChromInfoLists = new List<ChromInfoList<TransitionGroupChromInfo>>(ReplicateCount);
            var transitionChromInfoLists = Enumerable.Range(0, nodeGroup.TransitionCount)
                .Select(iTran => new List<ChromInfoList<TransitionChromInfo>>(ReplicateCount)).ToArray();
            for (int replicateIndex = 0; replicateIndex < ReplicateCount; replicateIndex++)
            {
                var chromInfoLists = CalcTransitionChromInfos(nodeGroup, replicateIndex, out var correlations);
                groupChromInfoLists.Add(CalcTransitionGroupChromInfos(nodeGroup, replicateIndex, chromInfoLists,
                    correlations));
                for (int iTran = 0; iTran < transitionChromInfoLists.Length; iTran++)
                {
                    transitionChromInfoLists[iTran]
                        .Add(new ChromInfoList<TransitionChromInfo>(chromInfoLists[iTran]));
                }
            }

            return new GroupResults(new Results<TransitionGroupChromInfo>(groupChromInfoLists),
                transitionChromInfoLists.Select(lists => new Results<TransitionChromInfo>(lists)));
        }

        /// <summary>
        /// Everything worked out for one precursor, which is calculated for all of the replicates
        /// at once because the precursor values need all of the transitions.
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

        /// <summary>
        /// The complete molecule level results, aggregated from the precursor level values the
        /// same way <see cref="PeptideDocNode.ChangeSettings"/> aggregates them. This is what a
        /// caller uses instead of <see cref="PeptideDocNode.Results"/>.
        /// </summary>
        public Results<PeptideChromInfo> GetPeptideChromInfos()
        {
            if (ReplicateCount == 0)
            {
                return null;
            }

            var chromInfoLists = new List<ChromInfoList<PeptideChromInfo>>(ReplicateCount);
            for (int replicateIndex = 0; replicateIndex < ReplicateCount; replicateIndex++)
            {
                chromInfoLists.Add(GetPeptideChromInfos(replicateIndex));
            }

            return new Results<PeptideChromInfo>(chromInfoLists);
        }

        public ChromInfoList<PeptideChromInfo> GetPeptideChromInfos(int replicateIndex)
        {
            if (replicateIndex < 0 || replicateIndex >= ReplicateCount)
            {
                return default;
            }

            var listCalculator = new PeptideDocNode.PeptideChromInfoListCalculator(Settings, replicateIndex);
            int transitionGroupCount = 0;
            foreach (var nodeGroup in PeptideDocNode.TransitionGroups)
            {
                transitionGroupCount++;
                var groupChromInfos = GetTransitionGroupChromInfos(nodeGroup.TransitionGroup, replicateIndex);
                if (groupChromInfos.Count == 0)
                {
                    continue;
                }

                listCalculator.AddChromInfoList(nodeGroup, groupChromInfos);
                foreach (TransitionDocNode nodeTran in nodeGroup.GetQuantitativeTransitions(Settings))
                {
                    listCalculator.AddChromInfoList(nodeGroup, nodeTran,
                        GetTransitionChromInfos(nodeGroup.TransitionGroup, nodeTran.Transition, replicateIndex));
                }
            }

            return new ChromInfoList<PeptideChromInfo>(
                CarryPeptideAttributes(listCalculator.CalcChromInfoList(transitionGroupCount), replicateIndex));
        }

        /// <summary>
        /// Works out which candidate peak each of a precursor's peaks is, and gets rid of the
        /// chrom infos which <see cref="TransitionResults.LegacyChromInfos"/> was holding on to because
        /// nothing yet knew. Returns the precursor unchanged when there is nothing to convert.
        /// <para>
        /// A file is converted only when the boundaries of every one of its transition peaks match
        /// a candidate peak, and the same one. If any of them does not, all of them are treated as
        /// peaks whose boundaries the user set, and are recovered by integrating between those
        /// boundaries instead. Empty peaks say nothing either way: they come back empty.
        /// </para>
        /// </summary>
        /// <summary>
        /// Whether any of a precursor's transitions is still holding chrom infos which have not
        /// been worked out. Static, and asked before a <see cref="MoleculeResults"/> is made, since
        /// making one reads every chromatogram of the molecule.
        /// </summary>
        public static bool NeedsConverting(TransitionGroupDocNode nodeGroup)
        {
            return nodeGroup.Transitions.Any(nodeTran => nodeTran.AbbreviatedResults?.IsConverted == false);
        }

        public TransitionGroupDocNode ConvertResults(TransitionGroupDocNode nodeGroup)
        {
            var groupResults = nodeGroup.AbbreviatedResults;
            var nodeTrans = nodeGroup.Transitions.ToArray();
            if (groupResults == null || !NeedsConverting(nodeGroup))
            {
                return nodeGroup;
            }

            var replicatePositions = groupResults.ChromFileIds.ReplicatePositions;
            var chosenPeakIndexes = new int[groupResults.ChromFileIds.FileIds.Count];
            var transitionResults = nodeTrans.Select(nodeTran => nodeTran.AbbreviatedResults).ToArray();
            bool everyFileRead = true;
            for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
            {
                // A replicate which is still being imported or rescored has nothing settled to look
                // at, and asking would read chromatograms which are about to be replaced.
                if (!Settings.MeasuredResults.Chromatograms[replicateIndex].IsLoaded)
                {
                    everyFileRead = false;
                    continue;
                }

                foreach (int position in replicatePositions[replicateIndex])
                {
                    var fileId = groupResults.ChromFileIds.FileIds[position].Value;
                    var chromGroupInfo = FindChromatogramGroupInfo(nodeGroup, replicateIndex, fileId);
                    if (chromGroupInfo == null)
                    {
                        // Its chromatograms have not been read, so nothing can be said about it and
                        // nothing of it can be given up.
                        everyFileRead = false;
                        chosenPeakIndexes[position] = -1;
                        continue;
                    }

                    chosenPeakIndexes[position] = FindChosenPeakIndex(nodeGroup, nodeTrans, transitionResults,
                        replicateIndex, chromGroupInfo, fileId);
                    if (chosenPeakIndexes[position] < 0)
                    {
                        CarryPeakBounds(nodeTrans, transitionResults, fileId);
                    }
                }
            }

            var childrenNew = new List<DocNode>(nodeTrans.Length);
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                childrenNew.Add(nodeTrans[iTran].ChangeAbbreviatedResults(
                    everyFileRead ? transitionResults[iTran]?.ChangeLegacyChromInfos(null) : transitionResults[iTran]));
            }

            var groupResultsNew = groupResults.ChangeChosenPeakIndexes(chosenPeakIndexes);
            if (everyFileRead)
            {
                // The precursor's chrom infos go the same way its transitions' do. Everything they
                // said is either in the columnar results or rebuilt by GetTransitionGroupChromInfos,
                // which drives the same calculator the settings pass does - the aggregates, the
                // ranks and the dot products alike.
                //
                // This has to happen in the same pass that works out the peak indexes, not later:
                // the indexes are found by matching the transitions' peak boundaries, and once the
                // transitions are converted there are no boundaries left to match.
                groupResultsNew = groupResultsNew.ChangeLegacyChromInfos(null);
            }

            return (TransitionGroupDocNode) nodeGroup
                .ChangeAbbreviatedResults(groupResultsNew)
                .ChangeChildrenChecked(childrenNew);
        }

        /// <summary>
        /// The candidate peak which every one of the precursor's transition peaks is, in one file,
        /// or -1 when they are not all the same one.
        /// </summary>
        private ChromatogramGroupInfo FindChromatogramGroupInfo(TransitionGroupDocNode nodeGroup, int replicateIndex,
            ChromFileInfoId fileId)
        {
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            return GetChromatogramGroupInfos(nodeGroup.TransitionGroup, replicateIndex)
                .FirstOrDefault(info => ReferenceEquals(fileId, chromatograms.FindFile(info)));
        }

        private int FindChosenPeakIndex(TransitionGroupDocNode nodeGroup, TransitionDocNode[] nodeTrans,
            TransitionResults[] transitionResults, int replicateIndex, ChromatogramGroupInfo chromGroupInfo,
            ChromFileInfoId fileId)
        {
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            int chosenPeakIndex = -1;
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                var chromInfo = transitionResults[iTran]?.FindChromInfo(fileId, 0);
                if (chromInfo == null || chromInfo.IsEmpty)
                {
                    continue;
                }

                var chromatogramInfo = chromGroupInfo.GetAllTransitionInfo(nodeTrans[iTran], MzMatchTolerance,
                    chromatograms.OptimizationFunction, TransformChrom.interpolated).GetChromatogramForStep(0);
                int peakIndex = IndexOfPeak(chromatogramInfo, chromInfo);
                if (peakIndex < 0 || (chosenPeakIndex >= 0 && peakIndex != chosenPeakIndex))
                {
                    return -1;
                }

                chosenPeakIndex = peakIndex;
            }

            return chosenPeakIndex;
        }

        private static int IndexOfPeak(ChromatogramInfo chromatogramInfo, TransitionChromInfo chromInfo)
        {
            if (chromatogramInfo == null)
            {
                return -1;
            }

            for (int peakIndex = 0; peakIndex < chromatogramInfo.NumPeaks; peakIndex++)
            {
                var peak = chromatogramInfo.GetPeak(peakIndex);
                if (peak.StartTime == chromInfo.StartRetentionTime && peak.EndTime == chromInfo.EndRetentionTime)
                {
                    return peakIndex;
                }
            }

            return -1;
        }

        /// <summary>
        /// Records the peak boundaries of every transition in one file, which is what happens when
        /// the peaks are not all the same candidate peak. Integrating between them is then the only
        /// way any of them comes back.
        /// </summary>
        private static void CarryPeakBounds(TransitionDocNode[] nodeTrans, TransitionResults[] transitionResults,
            ChromFileInfoId fileId)
        {
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                var chromInfo = transitionResults[iTran]?.FindChromInfo(fileId, 0);
                if (chromInfo == null || chromInfo.IsEmpty)
                {
                    continue;
                }

                int position = transitionResults[iTran].ChromFileIds.IndexOfFile(fileId);
                if (position >= 0)
                {
                    transitionResults[iTran] = transitionResults[iTran].ChangeCustomPeakBounds(position,
                        chromInfo.StartRetentionTime, chromInfo.EndRetentionTime, chromInfo.Identified);
                }
            }
        }

        /// <summary>
        /// The chromatograms which were read, kept because code such as GraphChromatogram and the
        /// on demand feature calculator needs the chromatogram itself and not only the peaks
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

        private TransitionGroupDocNode FindTransitionGroup(TransitionGroup transitionGroup)
        {
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            return groupIndex < 0 ? null : (TransitionGroupDocNode) PeptideDocNode.Children[groupIndex];
        }

        /// <summary>
        /// Carries forward the values a <see cref="PeptideChromInfo"/> has which say nothing about
        /// the chromatogram: the user's decision to leave a replicate out of the calibration
        /// curve, and the concentration they entered for it.
        /// </summary>
        private IList<PeptideChromInfo> CarryPeptideAttributes(IList<PeptideChromInfo> chromInfos,
            int replicateIndex)
        {
            // From the columnar results, which is where a molecule keeps these. Null there means
            // there is nothing to carry, which is the usual case, and costs nothing to find out.
            var peptideResults = PeptideDocNode.AbbreviatedResults;
            if (chromInfos == null || peptideResults == null)
            {
                return chromInfos;
            }

            return chromInfos.Select(chromInfo =>
            {
                int position = peptideResults.IndexOfFile(replicateIndex, chromInfo.FileId);
                if (position < 0)
                {
                    return chromInfo;
                }

                return chromInfo
                    .ChangeExcludeFromCalibration(peptideResults.GetExcludeFromCalibration(position))
                    .ChangeAnalyteConcentration(peptideResults.GetAnalyteConcentration(position));
            }).ToArray();
        }

        /// <summary>
        /// Rebuilds the chrom infos of every transition of one transition group in one replicate,
        /// with the ranks and the dot products assigned. One transition cannot be done on its own,
        /// because both of those come from all of the transitions together.
        /// <para>
        /// <paramref name="correlations"/> receives the areas each dot product is calculated from,
        /// which the ranking pass has already had to gather. Only the group level values need them.
        /// </para>
        /// </summary>
        private IList<TransitionChromInfo>[] CalcTransitionChromInfos(TransitionGroupDocNode nodeGroup,
            int replicateIndex, out List<FileStepCorrelation> correlations)
        {
            var nodeTrans = nodeGroup.Transitions.ToArray();
            var chromInfoLists = new IList<TransitionChromInfo>[nodeTrans.Length];
            var lists = new List<TransitionChromInfo>[nodeTrans.Length];
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                lists[iTran] = new List<TransitionChromInfo>();
                chromInfoLists[iTran] = lists[iTran];
            }

            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromGroupInfo in GetChromatogramGroupInfos(nodeGroup.TransitionGroup, replicateIndex))
            {
                var fileId = chromatograms.FindFile(chromGroupInfo);
                if (fileId != null)
                {
                    ReadFile(nodeGroup, nodeTrans, lists, chromatograms, chromGroupInfo, fileId, replicateIndex);
                }
            }

            // The ranks live on the transition chrom infos, so they get assigned before anything
            // sees them.
            correlations = new List<FileStepCorrelation>();
            foreach (var fileStep in GetFileSteps(chromInfoLists))
            {
                var correlation = RankFileStep(nodeGroup, nodeTrans, chromInfoLists, fileStep);
                if (correlation != null)
                {
                    correlations.Add(correlation);
                }
            }

            return chromInfoLists;
        }

        /// <summary>
        /// Rebuilds the chrom infos of one transition in one replicate, one per flat position. The
        /// order has to match the order in which <see cref="TransitionGroupDocNode.ChangeResults"/>
        /// builds its lists: file major, optimization step minor, skipping any file where the
        /// transition has no chromatogram.
        /// </summary>
        private void ReadFile(TransitionGroupDocNode nodeGroup, TransitionDocNode[] nodeTrans,
            List<TransitionChromInfo>[] lists, ChromatogramSet chromatograms,
            ChromatogramGroupInfo chromGroupInfo, ChromFileInfoId fileId, int replicateIndex)
        {
            // Optimization steps are separate chromatograms of the same transition, and each one
            // has its own set of candidate peaks.
            var optStepChromatograms = new OptStepChromatograms[nodeTrans.Length];
            var customPeaks = new CustomPeak[nodeTrans.Length];
            var positions = new int[nodeTrans.Length];
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                optStepChromatograms[iTran] = chromGroupInfo.GetAllTransitionInfo(nodeTrans[iTran], MzMatchTolerance,
                    chromatograms.OptimizationFunction, TransformChrom.interpolated);

                // One entry per file, holding the values of optimization step zero, found by file
                // rather than by counting.
                var results = nodeTrans[iTran].AbbreviatedResults;
                positions[iTran] = results?.IndexOfFile(replicateIndex, fileId) ?? -1;
                customPeaks[iTran] = positions[iTran] < 0 ? null : results.GetCustomPeak(positions[iTran]);
            }

            int chosenPeakIndex = GetChosenPeakIndex(nodeGroup, replicateIndex, fileId, nodeTrans,
                optStepChromatograms, customPeaks, positions);
            for (int iTran = 0; iTran < nodeTrans.Length; iTran++)
            {
                if (optStepChromatograms[iTran].IsEmpty)
                {
                    continue;
                }

                var results = nodeTrans[iTran].AbbreviatedResults;
                var annotations = customPeaks[iTran]?.Annotations ?? Annotations.EMPTY;
                var userSet = positions[iTran] < 0 ? UserSet.FALSE : results.GetUserSet(positions[iTran]);
                int stepCount = optStepChromatograms[iTran].StepCount;
                for (int step = -stepCount; step <= stepCount; step++)
                {
                    // A chrom info gets added for every step even when there is no chromatogram
                    // for it, because ChangeResults adds an empty peak there.
                    var chromatogramInfo = optStepChromatograms[iTran].GetChromatogramForStep(step);
                    var chromPeak = GetChromPeak(nodeGroup, nodeTrans[iTran], replicateIndex, fileId, step,
                        chromatogramInfo, chosenPeakIndex, customPeaks[iTran]);
                    lists[iTran].Add(new TransitionChromInfo(fileId, step, chromPeak,
                        chromatogramInfo?.GetIonMobilityFilter() ?? IonMobilityFilter.EMPTY, annotations, userSet));
                }
            }
        }

        /// <summary>
        /// Which of the candidate peaks in the .skyd was chosen in one file, or -1 when no peak was
        /// chosen. This is a property of the peak group: one index covers every transition and
        /// every optimization step, because a transition whose peak is a different one has
        /// boundaries the user set instead, and so a <see cref="CustomPeak"/> of its own.
        /// </summary>
        private int GetChosenPeakIndex(TransitionGroupDocNode nodeGroup, int replicateIndex, ChromFileInfoId fileId,
            TransitionDocNode[] nodeTrans, OptStepChromatograms[] optStepChromatograms, CustomPeak[] customPeaks,
            int[] positions)
        {
            var results = nodeGroup.AbbreviatedResults;
            int position = results?.IndexOfFile(replicateIndex, fileId) ?? -1;
            if (position >= 0)
            {
                int? chosenPeakIndex = results.GetChosenPeakIndex(position);
                if (chosenPeakIndex.HasValue)
                {
                    return chosenPeakIndex.Value;
                }
            }

            return SearchForChosenPeakIndex(nodeTrans, optStepChromatograms, customPeaks, positions);
        }

        /// <summary>
        /// Works out which candidate peak was chosen while the document does not carry
        /// <see cref="TransitionGroupResults.ChosenPeakIndexes"/> yet.
        /// <para>
        /// The area is the only thing about the chosen peak the columnar results keep, so the index
        /// is the one whose area matches at every transition of the precursor. One transition is
        /// not enough on its own: a transition with little or no signal has an area which several
        /// of the candidate peaks could produce, and a zero area peak inside a chosen peak group is
        /// ordinary. Together they pin it down.
        /// </para>
        /// </summary>
        private static int SearchForChosenPeakIndex(TransitionDocNode[] nodeTrans,
            OptStepChromatograms[] optStepChromatograms, CustomPeak[] customPeaks, int[] positions)
        {
            // A peak the user set is not one of the candidate peaks, so it says nothing about which
            // one was chosen.
            var eligible = Enumerable.Range(0, nodeTrans.Length).Where(iTran =>
                !optStepChromatograms[iTran].IsEmpty && positions[iTran] >= 0 &&
                customPeaks[iTran]?.HasPeakBounds != true).ToArray();
            if (eligible.Length == 0)
            {
                return -1;
            }

            var chromatograms = eligible
                .Select(iTran => optStepChromatograms[iTran].GetChromatogramForStep(0)).ToArray();
            int numPeaks = chromatograms.Max(chromatogramInfo => chromatogramInfo?.NumPeaks ?? 0);
            for (int peakIndex = 0; peakIndex < numPeaks; peakIndex++)
            {
                bool matchesEvery = true;
                for (int i = 0; i < eligible.Length && matchesEvery; i++)
                {
                    float area = nodeTrans[eligible[i]].AbbreviatedResults.Peaks.Values[positions[eligible[i]]].Area;
                    matchesEvery = chromatograms[i] != null && peakIndex < chromatograms[i].NumPeaks &&
                                   chromatograms[i].GetPeak(peakIndex).Area == area;
                }

                if (matchesEvery)
                {
                    return peakIndex;
                }
            }

            return -1;
        }

        private ChromPeak GetChromPeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran,
            int replicateIndex, ChromFileInfoId fileId, int step, ChromatogramInfo chromatogramInfo, int peakIndex,
            CustomPeak customPeak)
        {
            if (chromatogramInfo == null)
            {
                return ChromPeak.EMPTY;
            }

            if (customPeak?.HasPeakBounds == true)
            {
                return IntegratePeak(nodeGroup, nodeTran, replicateIndex, fileId, step, customPeak);
            }

            return peakIndex < 0 || peakIndex >= chromatogramInfo.NumPeaks
                ? ChromPeak.EMPTY
                : chromatogramInfo.GetPeak(peakIndex);
        }

        /// <summary>
        /// Integrates the chromatogram again between boundaries which are not those of any
        /// candidate peak, which is what the user setting the boundaries produces. This is the
        /// expensive path: it decompresses the chromatogram, which is why the integrator for a
        /// file is kept once it has been made.
        /// </summary>
        private ChromPeak IntegratePeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran,
            int replicateIndex, ChromFileInfoId fileId, int step, CustomPeak customPeak)
        {
            var integrator = GetIntegrator(nodeGroup, replicateIndex, fileId);
            if (integrator == null)
            {
                return ChromPeak.EMPTY;
            }

            // Identification is not a property of the boundaries, so integrating again cannot
            // find it. It is carried on the custom peak, the same as the boundaries themselves.
            var flags = default(ChromPeak.FlagValues);
            if (customPeak.Identified != PeakIdentification.FALSE)
            {
                flags |= ChromPeak.FlagValues.contains_id;
            }

            if (customPeak.Identified == PeakIdentification.ALIGNED)
            {
                flags |= ChromPeak.FlagValues.used_id_alignment;
            }

            return integrator.CalcPeak(nodeTran.Transition, step, customPeak.StartTime.Value,
                customPeak.EndTime.Value, flags);
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
        /// Rebuilds the group level values for one replicate by driving the same calculator that
        /// <see cref="TransitionGroupDocNode.ChangeResults"/> uses, so that there is only one
        /// implementation of the aggregation.
        /// <para>
        /// The chrom infos the doc node holds supply the values which are carried forward rather
        /// than recalculated - the scores, the annotations and the reintegrated peak. Those come
        /// from <see cref="TransitionGroupResults"/> once the document holds them there.
        /// </para>
        /// </summary>
        private ChromInfoList<TransitionGroupChromInfo> CalcTransitionGroupChromInfos(
            TransitionGroupDocNode nodeGroup, int replicateIndex, IList<TransitionChromInfo>[] chromInfoLists,
            List<FileStepCorrelation> correlations)
        {
            var nodeTrans = nodeGroup.Transitions.ToArray();
            // What the precursor is still carrying, which is where the values this pass does not
            // work out - the annotations and the scores - are carried forward from.
            var chromInfos = nodeGroup.AbbreviatedResults?.LegacyChromInfos;
            var previousChromInfos = chromInfos != null && replicateIndex < chromInfos.Count
                ? chromInfos[replicateIndex]
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

            // Has to come after AddChromInfoList, which is what creates the calculators these look
            // up by file and optimization step.
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

            return new ChromInfoList<TransitionGroupChromInfo>(listCalculator.CalcChromInfoList() ??
                                                              new TransitionGroupChromInfo[0]);
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
                ranked.Add(new RankedTransition(nodeTran.IsMs1, chromInfo, nodeTran.ParticipatesInScoring));
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
        /// Loads the chromatograms of this molecule. This happens once, the first time anything is
        /// asked for.
        /// </summary>
        private void EnsureRead()
        {
            if (_chromatogramGroupInfos != null)
            {
                return;
            }

            var measuredResults = Settings.MeasuredResults;
            _chromatogramGroupInfos = ImmutableList.ValueOf(PeptideDocNode.TransitionGroups.Select(nodeGroup =>
                measuredResults == null
                    ? EmptyChromatogramGroupInfos()
                    : ReadTransitionGroup(measuredResults, nodeGroup)));
            _groupResults = new GroupResults[_chromatogramGroupInfos.Count];
        }

        private ChromFileIdMap<ChromatogramGroupInfo> ReadTransitionGroup(MeasuredResults measuredResults,
            TransitionGroupDocNode nodeGroup)
        {
            var chromatogramGroupInfos = new List<IList<ChromatogramGroupInfo>>();
            var fileIds = new List<ChromFileInfoId>();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var chromatograms = measuredResults.Chromatograms[replicateIndex];
                var replicateGroupInfos = ReadReplicate(measuredResults, chromatograms, nodeGroup);
                chromatogramGroupInfos.Add(replicateGroupInfos);
                // One group info per file - ReadReplicate makes sure of that - so the files of the
                // replicate are what its positions are keyed by.
                fileIds.AddRange(replicateGroupInfos.Select(groupInfo => chromatograms.FindFile(groupInfo.FilePath)));
            }

            return new ChromFileIdMap<ChromatogramGroupInfo>(chromatogramGroupInfos, fileIds);
        }

        /// <summary>
        /// What a molecule with no measured results has for each of its precursors: no replicates
        /// and so no group infos.
        /// </summary>
        private static ChromFileIdMap<ChromatogramGroupInfo> EmptyChromatogramGroupInfos()
        {
            return new ChromFileIdMap<ChromatogramGroupInfo>(
                new ChromFileIds(ReplicatePositions.FromCounts(new int[0]), new ChromFileInfoId[0]),
                new ChromatogramGroupInfo[0]);
        }

        private IList<ChromatogramGroupInfo> ReadReplicate(MeasuredResults measuredResults,
            ChromatogramSet chromatograms, TransitionGroupDocNode nodeGroup)
        {
            if (!measuredResults.TryLoadChromatogram(chromatograms, PeptideDocNode, nodeGroup, MzMatchTolerance,
                    out var chromGroupInfos))
            {
                return ImmutableList<ChromatogramGroupInfo>.EMPTY;
            }

            // A file should only appear once. See the same guard in TransitionGroupDocNode.ChangeResults.
            if (chromGroupInfos.Length > 1)
            {
                return chromGroupInfos.Distinct(ChromatogramGroupInfo.PathComparer).ToArray();
            }

            return chromGroupInfos;
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
            public RankedTransition(bool isMs1, TransitionChromInfo chromInfo, bool participatesInScoring)
            {
                IsMs1 = isMs1;
                ChromInfo = chromInfo;
                ParticipatesInScoring = participatesInScoring;
            }

            public bool IsMs1 { get; }
            public TransitionChromInfo ChromInfo { get; }
            public bool ParticipatesInScoring { get; }

            public float RankArea
            {
                get { return ParticipatesInScoring ? ChromInfo?.Area ?? 0 : -1.0f; }
            }
        }
    }
}
