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
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
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
        /// <see cref="DocNodeParent.FindNodeIndex(Identity)"/> makes that a fast lookup. Null until
        /// <see cref="EnsureRead"/> has run, and every entry non-null after it.
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
        /// <summary>
        /// What is known about one precursor, without working anything out. This is what the paths
        /// which run while a precursor is being worked out have to use - see
        /// <see cref="GetIntegrator"/>, which <see cref="CalcGroupResults"/> reaches by way of
        /// <see cref="IntegratePeak"/> - since asking for the results of the precursor being
        /// calculated would not terminate.
        /// </summary>
        private GroupResults FindGroupResults(TransitionGroup transitionGroup)
        {
            EnsureRead();
            int groupIndex = PeptideDocNode.FindNodeIndex(transitionGroup);
            return groupIndex < 0 ? null : _groupResults[groupIndex];
        }

        private GroupResults GetGroupResults(TransitionGroup transitionGroup)
        {
            var groupResults = FindGroupResults(transitionGroup);
            if (groupResults == null || ReplicateCount == 0)
            {
                return null;
            }

            // Both are set together, so either one says whether this has been worked out.
            if (groupResults.ChromInfos == null)
            {
                CalcGroupResults(groupResults);
            }

            return groupResults;
        }

        private void CalcGroupResults(GroupResults groupResults)
        {
            var nodeGroup = groupResults.NodeGroup;
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

            groupResults.TransitionResults = ImmutableList.ValueOf(
                transitionChromInfoLists.Select(lists => new Results<TransitionChromInfo>(lists)));
            // Last, since it is what says the precursor has been worked out.
            groupResults.ChromInfos = new Results<TransitionGroupChromInfo>(groupChromInfoLists);
        }

        /// <summary>
        /// Everything worked out for one precursor, which is calculated for all of the replicates
        /// at once because the precursor values need all of the transitions.
        /// </summary>
        private class GroupResults
        {
            public GroupResults(TransitionGroupDocNode nodeGroup,
                ChromFileIdMap<ChromatogramGroupInfo> chromatogramGroupInfos)
            {
                NodeGroup = nodeGroup;
                ChromatogramGroupInfos = chromatogramGroupInfos;
            }

            /// <summary>
            /// The precursor these belong to, as it was when the molecule was read.
            /// </summary>
            public TransitionGroupDocNode NodeGroup { get; }

            /// <summary>
            /// This precursor's chromatograms in every replicate. Read for every precursor of the
            /// molecule at once rather than when each is first asked for - see
            /// <see cref="EnsureRead"/> for why - so it is here from the start.
            /// </summary>
            public ChromFileIdMap<ChromatogramGroupInfo> ChromatogramGroupInfos { get; }

            /// <summary>
            /// Worked out on the first ask and then kept, because rebuilding a precursor means
            /// rebuilding every one of its transitions: whoever asked for one of them usually goes
            /// on to ask for the rest. Null until then, and the two are always set together.
            /// </summary>
            public Results<TransitionGroupChromInfo> ChromInfos { get; set; }
            public ImmutableList<Results<TransitionChromInfo>> TransitionResults { get; set; }

            /// <summary>
            /// One per file, made when a peak of that file first has to be integrated again. Kept
            /// per precursor rather than per molecule because a chromatogram group belongs to one
            /// precursor, so the entry which addresses this already says which.
            /// </summary>
            public Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator> Integrators { get; }
                = new Dictionary<ReferenceValue<ChromatogramGroupInfo>, TransitionGroupIntegrator>();
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
        /// Works out which candidate peak each of a precursor's peaks is, and gets rid of what the
        /// peaks were keeping because nothing yet knew. Returns the precursor unchanged when there
        /// is nothing to convert.
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
            var groupResults = nodeGroup.AbbreviatedResults;
            if (groupResults == null)
            {
                return false;
            }

            // Either a document read without them, or a results pass which has just worked the
            // peaks out again. Both leave peaks which have not been matched to a candidate peak.
            return groupResults.NeedsPeakIndexes;
        }

        /// <summary>
        /// The document with every precursor's results converted - which candidate peak each peak
        /// is worked out from the .skyd, and the chrom infos it was read with let go. Returns the
        /// same document when there was nothing to convert.
        /// <para>
        /// A document which was opened rather than imported never goes through a results pass: the
        /// chromatogram loader changes the settings without a diff, deliberately, so that opening
        /// one does not recalculate everything. That left nothing to release the chrom infos, which
        /// is what this is for.
        /// </para>
        /// <para>
        /// One <see cref="MoleculeResults"/> per molecule, made and dropped as the walk goes, so a
        /// whole document's worth of them is never held at once.
        /// </para>
        /// </summary>
        public static SrmDocument ConvertDocumentResults(SrmDocument document, IProgressMonitor progressMonitor,
            ref IProgressStatus progressStatus)
        {
            if (document.Settings.MeasuredResults == null || !document.MoleculeTransitionGroups.Any(NeedsConverting))
            {
                return document;
            }

            // Flattened first, so that the unit of work handed to a thread is one molecule rather
            // than one group: a group can hold a single molecule, which would leave most of them
            // idle.
            var moleculeGroups = document.MoleculeGroups.ToArray();
            var molecules = moleculeGroups.SelectMany(nodeMoleculeGroup => nodeMoleculeGroup.Molecules).ToArray();

            var status = progressStatus.ChangeMessage(
                ResultsResources.MoleculeResults_ConvertDocumentResults_Reading_peaks);
            progressMonitor?.UpdateProgress(status);

            var converted = new PeptideDocNode[molecules.Length];
            var settings = document.Settings;
            int completedCount = 0;
            var statusLock = new object();

            // A MoleculeResults caches what it reads, so each one belongs to the thread which made
            // it and is never handed to another. What the threads do share - the settings and the
            // chromatogram cache - they only read, and the cache reads go through
            // ChromatogramGroupInfo.LoadPeaksForAll, which takes a read lock and opens a stream of
            // its own rather than using the shared pooled one.
            ParallelEx.For(0, molecules.Length, iMolecule =>
            {
                if (progressMonitor?.IsCanceled == true)
                {
                    // Leaving a molecule unconverted costs only the memory this would have saved,
                    // so there is nothing to undo and no reason to throw.
                    converted[iMolecule] = molecules[iMolecule];
                    return;
                }

                converted[iMolecule] = ConvertMoleculeResults(settings, molecules[iMolecule]);
                if (progressMonitor == null)
                {
                    return;
                }

                lock (statusLock)
                {
                    int percentComplete = completedCount * 100 / molecules.Length;
                    completedCount++;
                    if (percentComplete != status.PercentComplete)
                    {
                        progressMonitor.UpdateProgress(status = status.ChangePercentComplete(percentComplete));
                    }
                }
            });
            if (true == progressMonitor?.IsCanceled)
            {
                return document;
            }
            progressStatus = status;

            var moleculeGroupsNew = new List<DocNode>(moleculeGroups.Length);
            int iConverted = 0;
            foreach (var nodeMoleculeGroup in moleculeGroups)
            {
                var moleculesNew = new List<DocNode>(nodeMoleculeGroup.Children.Count);
                for (int i = 0; i < nodeMoleculeGroup.Children.Count; i++)
                {
                    moleculesNew.Add(converted[iConverted++]);
                }

                moleculeGroupsNew.Add(nodeMoleculeGroup.ChangeChildrenChecked(moleculesNew));
            }

            return (SrmDocument) document.ChangeChildrenChecked(moleculeGroupsNew);
        }

        private static PeptideDocNode ConvertMoleculeResults(SrmSettings settings, PeptideDocNode nodePep)
        {
            // Asked before making one, since making one reads every chromatogram of the molecule.
            if (!nodePep.TransitionGroups.Any(NeedsConverting))
            {
                return nodePep;
            }

            var moleculeResults = new MoleculeResults(settings, nodePep);
            var childrenNew = nodePep.TransitionGroups
                .Select(nodeGroup => (DocNode) moleculeResults.ConvertResults(nodeGroup)).ToList();
            return (PeptideDocNode) nodePep.ChangeChildrenChecked(childrenNew);
        }

        public TransitionGroupDocNode ConvertResults(TransitionGroupDocNode nodeGroup)
        {
            var groupResults = nodeGroup.AbbreviatedResults;
            if (groupResults == null || !NeedsConverting(nodeGroup))
            {
                return nodeGroup;
            }

            var replicatePositions = groupResults.ChromFileIds.ReplicatePositions;
            var chosenPeakIndexes = new int[groupResults.ChromFileIds.FileIds.Count];
            var resultsNew = groupResults;
            for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
            {
                // A replicate which is still being imported or rescored has nothing settled to look
                // at, and asking would read chromatograms which are about to be replaced. Its peaks
                // still keep their boundaries below, the same as any other peak which is not one of
                // the candidates.
                bool isLoaded = Settings.MeasuredResults.Chromatograms[replicateIndex].IsLoaded;
                foreach (int position in replicatePositions[replicateIndex])
                {
                    var fileId = groupResults.ChromFileIds.FileIds[position].Value;
                    var chromGroupInfo = isLoaded
                        ? FindChromatogramGroupInfo(nodeGroup, replicateIndex, fileId)
                        : null;
                    
                    chosenPeakIndexes[position] = chromGroupInfo == null
                        ? -1
                        : FindChosenPeakIndex(resultsNew, chromGroupInfo, replicateIndex, fileId);
                    if (chosenPeakIndexes[position] >= 0)
                    {
                        // The peak is one of the candidate peaks after all, so the index reproduces
                        // it and everything it was keeping for itself is a second copy of the .skyd.
                        // Anything which could not be matched - because the peak is not one of them,
                        // or because there was nothing to match it against - keeps what it has, and
                        // is reproduced by integrating between its boundaries.
                        resultsNew = resultsNew.DropTransitionCustomPeaks(replicateIndex, fileId);
                    }
                }
            }

            // The precursor's chrom infos go now too. Everything they said is either in the columnar
            // results or rebuilt by GetTransitionGroupChromInfos, which drives the same calculator
            // the settings pass does - the aggregates, the ranks and the dot products alike.
            //
            // Unconditional, because every peak above came away with either the index of the
            // candidate peak it is or the boundaries to integrate between. Letting them go only
            // when every file could be read meant a pass which rebuilt them - see
            // TransitionGroupResultsCalculator.UpdateTransitionGroupNode - and then could not give
            // them up left the document holding more than it started with.
            var groupResultsNew = resultsNew.ChangeChosenPeakIndexes(chosenPeakIndexes)
                .ChangeNeedsPeakIndexes(false)
                .ChangeLegacyChromInfos(null);

            return nodeGroup.ChangeAbbreviatedResults(groupResultsNew);
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

        /// <summary>
        /// Which of the candidate peaks in the .skyd the precursor's peak in one file is, or -1 when
        /// it is not one of them.
        /// <para>
        /// Every peak of a peak group was integrated between the group's boundaries, which are the
        /// precursor's own. A transition whose peak is somewhere else has boundaries of its own,
        /// and then there is no single candidate peak to look for at all.
        /// </para>
        /// <para>
        /// The boundaries are all this needs, so it reads no transition chromatogram: that is where
        /// the cost is, in <see cref="ChromatogramGroupInfo.GetTransitionInfo"/> and
        /// <see cref="ChromatogramGroupInfo.GetAllTransitionInfo"/>, which match an m/z and build a
        /// <see cref="ChromatogramInfo"/>. The group's peaks are searched once rather than once per
        /// transition.
        /// </para>
        /// </summary>
        private static int FindChosenPeakIndex(TransitionGroupResults groupResults,
            ChromatogramGroupInfo chromGroupInfo, int replicateIndex, ChromFileInfoId fileId)
        {
            for (int iTran = 0; iTran < groupResults.TransitionCount; iTran++)
            {
                if (groupResults.FindTransitionCustomPeakBounds(iTran, replicateIndex, fileId).HasValue)
                {
                    return -1;
                }
            }

            var peakBounds = groupResults.FindPrecursorPeakBounds(replicateIndex, fileId);
            return peakBounds.HasValue
                ? chromGroupInfo.FindPeakIndex(peakBounds.Value.StartTime, peakBounds.Value.EndTime)
                : -1;
        }

        /// <summary>
        /// What one file's peak is being changed to. Held as an object rather than a delegate so
        /// that the two things which have to travel with it - which transitions it touches, and
        /// what the peak becomes for a given optimization step - stay together.
        /// </summary>
        private class PeakChange
        {
            /// <summary>
            /// <paramref name="transition"/> null changes every transition of the precursor, which
            /// is what moving a peak group does.
            /// </summary>
            public PeakChange(ChromFileInfoId fileId, Transition transition, UserSet userSet)
            {
                FileId = fileId;
                Transition = transition;
                UserSet = userSet;
            }

            private ChromFileInfoId FileId { get; }
            private Transition Transition { get; }
            public UserSet UserSet { get; }

            /// <summary>
            /// Set to integrate between these boundaries, which is what a peak the user moved is
            /// reproduced by. Null when the peak is being taken from the .skyd or removed.
            /// </summary>
            public CustomPeakBounds? PeakBounds { get; set; }
            public PeakIdentification Identified { get; set; }

            /// <summary>
            /// Which of the candidate peaks in the .skyd to take, or -1 to remove the peak.
            /// Ignored when <see cref="PeakBounds"/> says to integrate instead.
            /// </summary>
            public int PeakIndex { get; set; } = -1;

            /// <summary>
            /// Leaves alone a transition which has no peak here, so that importing boundaries does
            /// not invent peaks where the document says there were none.
            /// </summary>
            public bool PreserveMissingPeaks { get; set; }

            public bool AppliesToFile(ChromFileInfoId fileId)
            {
                return ReferenceEquals(FileId, fileId);
            }

            public bool AppliesToTransition(Transition transition)
            {
                return Transition == null || ReferenceEquals(Transition, transition);
            }

            public ChromPeak GetChromPeak(MoleculeResults moleculeResults, TransitionGroupDocNode nodeGroup,
                TransitionDocNode nodeTran, int replicateIndex, ChromFileInfoId fileId, int step,
                ChromatogramInfo chromatogramInfo)
            {
                if (!PeakBounds.HasValue)
                {
                    return PeakIndex < 0 || chromatogramInfo == null || PeakIndex >= chromatogramInfo.NumPeaks
                        ? ChromPeak.EMPTY
                        : chromatogramInfo.GetPeak(PeakIndex);
                }

                var integrator = moleculeResults.GetIntegrator(nodeGroup, replicateIndex, fileId);
                if (integrator == null)
                {
                    return ChromPeak.EMPTY;
                }

                var flags = default(ChromPeak.FlagValues);
                if (Identified != PeakIdentification.FALSE)
                {
                    flags |= ChromPeak.FlagValues.contains_id;
                }

                if (Identified == PeakIdentification.ALIGNED)
                {
                    flags |= ChromPeak.FlagValues.used_id_alignment;
                }

                return integrator.CalcPeak(nodeTran.Transition, step, PeakBounds.Value.StartTime,
                    PeakBounds.Value.EndTime, flags);
            }
        }

        /// <summary>
        /// The molecule with one precursor's peak in one file integrated between new boundaries.
        /// <paramref name="transition"/> null moves the whole peak group, which is the usual case.
        /// Returns the molecule unchanged when there is no chromatogram to read.
        /// </summary>
        /// <param name="identified">Null to work it out from the settings, which is what a caller
        /// which is not carrying an identification from somewhere else wants. See
        /// <see cref="FindPeakIdentification"/>.</param>
        public PeptideDocNode ChangePeakBounds(TransitionGroup transitionGroup, [CanBeNull] Transition transition,
            int replicateIndex, ChromFileInfoId fileId, float startTime, float endTime,
            PeakIdentification? identified, UserSet userSet, bool preserveMissingPeaks = false)
        {
            return ChangePeak(transitionGroup, replicateIndex, new PeakChange(fileId, transition, userSet)
            {
                PeakBounds = new CustomPeakBounds(Math.Min(startTime, endTime), Math.Max(startTime, endTime)),
                Identified = identified ??
                             FindPeakIdentification(transitionGroup, replicateIndex, fileId, startTime, endTime),
                PreserveMissingPeaks = preserveMissingPeaks
            });
        }

        /// <summary>
        /// The precursors of this molecule whose peaks were picked together with one of them, and
        /// so are meant to sit at the same retention time.
        /// <para>
        /// A precursor whose <see cref="RelativeRT"/> is known to match goes with every other one
        /// which also matches, whatever its charge. One whose relative retention time is unknown
        /// goes only with the precursors of its own label type - a different charge state of the
        /// same label still counts, which is what makes this more than an isotope label check.
        /// This is the same partition <see cref="PeptideChromDataSets"/> picks peaks by.
        /// </para>
        /// </summary>
        public IEnumerable<TransitionGroupDocNode> GetComparableGroups(TransitionGroup transitionGroup)
        {
            var nodeGroup = FindTransitionGroup(transitionGroup);
            if (nodeGroup == null)
            {
                return Array.Empty<TransitionGroupDocNode>();
            }

            if (nodeGroup.RelativeRT == RelativeRT.Unknown)
            {
                return PeptideDocNode.TransitionGroups.Where(other => Equals(other.LabelType, nodeGroup.LabelType));
            }

            return PeptideDocNode.TransitionGroups.Where(other => other.RelativeRT != RelativeRT.Unknown);
        }

        /// <summary>
        /// The molecule with the same peak boundaries put on every precursor which was picked
        /// alongside this one, which is what moving a peak in the chromatogram graph does: the
        /// precursors of a molecule are meant to sit at the same retention time.
        /// <para>
        /// A precursor with no peak in this file is left alone, and so is one whose peak is
        /// missing, since giving it these boundaries would invent a peak the document never had.
        /// </para>
        /// </summary>
        public PeptideDocNode ChangeComparablePeakBounds(TransitionGroup transitionGroup, int replicateIndex,
            ChromFileInfoId fileId, float startTime, float endTime, PeakIdentification? identified, UserSet userSet)
        {
            var moleculeResults = this;
            foreach (var other in GetComparableGroups(transitionGroup).ToArray())
            {
                if (ReferenceEquals(other.TransitionGroup, transitionGroup))
                {
                    continue;
                }

                var nodePepNew = moleculeResults.ChangePeakBounds(other.TransitionGroup, null, replicateIndex,
                    fileId, startTime, endTime, identified, userSet, true);
                if (!ReferenceEquals(nodePepNew, moleculeResults.PeptideDocNode))
                {
                    // A new molecule each turn, and the chromatograms are read again with it. Each
                    // turn changes a different precursor, so none of them changes what the next one
                    // reads, but the results it works from have to be the ones being changed.
                    moleculeResults = new MoleculeResults(Settings, nodePepNew);
                }
            }

            return moleculeResults.PeptideDocNode;
        }

        /// <summary>
        /// Whether a peak between two boundaries contains an identification, which is a question
        /// for the settings rather than the chromatogram: a retention time this molecule was
        /// identified at, from a library or a search result, falling between them.
        /// <para>
        /// <see cref="PeakIdentification.ALIGNED"/> when only a time aligned from another replicate
        /// lands there, which is a weaker claim than a time measured in this file.
        /// </para>
        /// </summary>
        public PeakIdentification FindPeakIdentification(TransitionGroup transitionGroup, int replicateIndex,
            ChromFileInfoId fileId, double startTime, double endTime)
        {
            var chromatogramSet = replicateIndex >= 0 && replicateIndex < ReplicateCount
                ? Settings.MeasuredResults.Chromatograms[replicateIndex]
                : null;
            var filePath = chromatogramSet?.GetFileInfo(fileId)?.FilePath;
            if (filePath == null)
            {
                return PeakIdentification.FALSE;
            }

            Settings.TryGetRetentionTimes(PeptideDocNode, transitionGroup.PrecursorAdduct, filePath, out _,
                out var retentionTimes);
            if (ContainsTime(retentionTimes, startTime, endTime))
            {
                return PeakIdentification.TRUE;
            }

            var alignedRetentionTimes = Settings.GetAlignedRetentionTimes(chromatogramSet, filePath,
                Settings.GetTargets(PeptideDocNode).ToList());
            return ContainsTime(alignedRetentionTimes, startTime, endTime)
                ? PeakIdentification.ALIGNED
                : PeakIdentification.FALSE;
        }

        private static bool ContainsTime(double[] times, double startTime, double endTime)
        {
            return times != null && times.Any(time => startTime <= time && time <= endTime);
        }

        /// <summary>
        /// The molecule with one precursor's peak in one file set to one of the candidate peaks in
        /// the .skyd. See <see cref="FindPeakIndex"/> for how a caller which has a retention time
        /// rather than an index gets one.
        /// <para>
        /// Every transition of the precursor, always. A candidate peak index is a precursor level
        /// thing - see <see cref="PrecursorPeak.ChosenPeakIndex"/> - and one transition sitting on
        /// a different candidate peak has no way to be written down as an index at all. Moving a
        /// single transition is <see cref="ChangePeakBounds"/>, which Skyline deliberately makes
        /// reachable only by dragging while its chromatogram is the only one shown.
        /// </para>
        /// </summary>
        public PeptideDocNode ChoosePeak(TransitionGroup transitionGroup, int replicateIndex,
            ChromFileInfoId fileId, int peakIndex, UserSet userSet)
        {
            return ChangePeak(transitionGroup, replicateIndex,
                new PeakChange(fileId, null, userSet) { PeakIndex = peakIndex });
        }

        /// <summary>
        /// The molecule with one precursor's peak in one file taken away, which leaves an empty
        /// peak rather than no entry: a file the precursor was measured in still has a result.
        /// </summary>
        public PeptideDocNode RemovePeak(TransitionGroup transitionGroup, [CanBeNull] Transition transition,
            int replicateIndex, ChromFileInfoId fileId, UserSet userSet)
        {
            return ChangePeak(transitionGroup, replicateIndex, new PeakChange(fileId, transition, userSet));
        }

        /// <summary>
        /// Which of the candidate peaks in the .skyd has its apex at a retention time, or -1 when
        /// none of them does. This is how a caller which knows where the user clicked rather than
        /// which peak they meant gets an index for <see cref="ChoosePeak"/>.
        /// <para>
        /// Every transition is asked, because each has an apex of its own and the time came off one
        /// of them. <see cref="ChromatogramInfo.IndexOfPeak"/> allows a thousandth of a minute,
        /// which is float precision on a time that came out of the .skyd rather than any real
        /// tolerance - this is matching a peak, not searching near one.
        /// </para>
        /// </summary>
        public int FindPeakIndex(TransitionGroup transitionGroup, int replicateIndex, ChromFileInfoId fileId,
            double retentionTime)
        {
            var nodeGroup = FindTransitionGroup(transitionGroup);
            var chromGroupInfo = nodeGroup == null
                ? null
                : FindChromatogramGroupInfo(nodeGroup, replicateIndex, fileId);
            if (chromGroupInfo == null)
            {
                return -1;
            }

            int peakIndexBest = -1;
            double minDeltaRT = double.MaxValue;
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                var chromatogramInfo = chromGroupInfo.GetTransitionInfo(nodeTran, MzMatchTolerance);
                int peakIndex = chromatogramInfo?.IndexOfPeak(retentionTime) ?? -1;
                if (peakIndex == -1)
                {
                    continue;
                }

                double deltaRT = Math.Abs(retentionTime - chromatogramInfo.GetPeak(peakIndex).RetentionTime);
                if (deltaRT < minDeltaRT)
                {
                    minDeltaRT = deltaRT;
                    peakIndexBest = peakIndex;
                }
            }

            return peakIndexBest;
        }

        /// <summary>
        /// The molecule with one file's peak changed on one of its precursors.
        /// <para>
        /// The change goes in where the chrom infos are made, so the peak it produces is ranked and
        /// aggregated exactly as a peak read from the .skyd would be, and then both levels go back
        /// through the columnar results the way a results pass puts them. Conversion follows, so
        /// the new peak comes away either with the index of the candidate peak it turned out to be
        /// or with the boundaries to integrate between.
        /// </para>
        /// </summary>
        private PeptideDocNode ChangePeak(TransitionGroup transitionGroup, int replicateIndex, PeakChange peakChange)
        {
            var nodeGroup = FindTransitionGroup(transitionGroup);
            if (nodeGroup == null || replicateIndex < 0 || replicateIndex >= ReplicateCount ||
                GetTransitionGroupChromInfos(transitionGroup) == null)
            {
                return PeptideDocNode;
            }

            var chromInfoLists =
                CalcTransitionChromInfos(nodeGroup, replicateIndex, peakChange, out var correlations);
            var groupChromInfos =
                CalcTransitionGroupChromInfos(nodeGroup, replicateIndex, chromInfoLists, correlations);

            // The precursor's first, since replacing them is what discards the columnar results
            // derived from the ones being replaced. Only this replicate is worked out again: the
            // rest are the ones already read.
            var nodeGroupNew = nodeGroup.ChangeResults(new Results<TransitionGroupChromInfo>(
                ReplaceReplicate(GetTransitionGroupChromInfos(transitionGroup), replicateIndex, groupChromInfos)));

            var groupResults = nodeGroupNew.AbbreviatedResults ?? TransitionGroupResults.Empty;
            for (int iTran = 0; iTran < nodeGroup.Children.Count; iTran++)
            {
                var nodeTran = (TransitionDocNode) nodeGroup.Children[iTran];
                var transitionChromInfos = GetTransitionChromInfos(transitionGroup, nodeTran.Transition);
                groupResults = groupResults.UpdateTransitionFromChromInfos(iTran,
                    new Results<TransitionChromInfo>(ReplaceReplicate(transitionChromInfos, replicateIndex,
                        new ChromInfoList<TransitionChromInfo>(chromInfoLists[iTran]))));
            }

            nodeGroupNew = ConvertResults(nodeGroupNew.ChangeAbbreviatedResults(groupResults));
            return ReferenceEquals(nodeGroup, nodeGroupNew)
                ? PeptideDocNode
                : (PeptideDocNode) PeptideDocNode.ReplaceChild(nodeGroupNew);
        }

        /// <summary>
        /// One replicate's entries replaced, the rest left as they were.
        /// </summary>
        private IList<ChromInfoList<TItem>> ReplaceReplicate<TItem>(Results<TItem> results, int replicateIndex,
            ChromInfoList<TItem> replicateResults) where TItem : ChromInfo
        {
            return Enumerable.Range(0, ReplicateCount)
                .Select(i => i == replicateIndex
                    ? replicateResults
                    : results != null && i < results.Count ? results[i] : default).ToList();
        }

        /// <summary>
        /// The chromatograms which were read, kept because code such as GraphChromatogram and the
        /// on demand feature calculator needs the chromatogram itself and not only the peaks
        /// taken from it.
        /// </summary>
        public IEnumerable<ChromatogramGroupInfo> GetChromatogramGroupInfos(TransitionGroup transitionGroup,
            int replicateIndex)
        {
            var groupResults = FindGroupResults(transitionGroup);
            if (groupResults == null || replicateIndex < 0 || replicateIndex >= ReplicateCount)
            {
                return ImmutableList<ChromatogramGroupInfo>.EMPTY;
            }

            return groupResults.ChromatogramGroupInfos.Values[replicateIndex];
        }

        private TransitionGroupDocNode FindTransitionGroup(TransitionGroup transitionGroup)
        {
            return (TransitionGroupDocNode) PeptideDocNode.FindNode(transitionGroup);
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
                return chromInfo
                    .ChangeExcludeFromCalibration(
                        peptideResults.GetExcludeFromCalibration(replicateIndex, chromInfo.FileId))
                    .ChangeAnalyteConcentration(
                        peptideResults.GetAnalyteConcentration(replicateIndex, chromInfo.FileId));
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
            return CalcTransitionChromInfos(nodeGroup, replicateIndex, null, out correlations);
        }

        /// <summary>
        /// <paramref name="peakChange"/> replaces the peak of the one file it names, so that a peak
        /// the user moved goes through the same ranking and aggregation as one which was read.
        /// </summary>
        private IList<TransitionChromInfo>[] CalcTransitionChromInfos(TransitionGroupDocNode nodeGroup,
            int replicateIndex, PeakChange peakChange, out List<FileStepCorrelation> correlations)
        {
            int transitionCount = nodeGroup.Children.Count;
            var chromInfoLists = new IList<TransitionChromInfo>[transitionCount];
            var lists = new List<TransitionChromInfo>[transitionCount];
            for (int iTran = 0; iTran < transitionCount; iTran++)
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
                    ReadFile(nodeGroup, lists, chromGroupInfo, fileId, replicateIndex,
                        peakChange?.AppliesToFile(fileId) == true ? peakChange : null);
                }
            }

            // The ranks live on the transition chrom infos, so they get assigned before anything
            // sees them.
            correlations = new List<FileStepCorrelation>();
            foreach (var fileStep in GetFileSteps(chromInfoLists))
            {
                var correlation = RankFileStep(nodeGroup, chromInfoLists, fileStep);
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
        private void ReadFile(TransitionGroupDocNode nodeGroup, List<TransitionChromInfo>[] lists,
            ChromatogramGroupInfo chromGroupInfo, ChromFileInfoId fileId, int replicateIndex,
            PeakChange peakChange = null)
        {
            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            var groupResults = nodeGroup.AbbreviatedResults;
            int transitionCount = nodeGroup.Children.Count;
            // Optimization steps are separate chromatograms of the same transition, and each one
            // has its own set of candidate peaks.
            var optStepChromatograms = new OptStepChromatograms[transitionCount];
            // Each transition's own peak for this file, or null when it has none there. The values
            // rather than positions in them: a position of one transition's results means nothing
            // in another's, and these are read alongside each other.
            var transitionPeaks = new TransitionPeak?[transitionCount];
            for (int iTran = 0; iTran < transitionCount; iTran++)
            {
                optStepChromatograms[iTran] = chromGroupInfo.GetAllTransitionInfo(
                    (TransitionDocNode) nodeGroup.Children[iTran], MzMatchTolerance,
                    chromatograms.OptimizationFunction, TransformChrom.interpolated);

                // The entry holding the values of optimization step zero, found by file.
                if (groupResults != null &&
                    groupResults.TryGetTransitionPeak(iTran, replicateIndex, fileId, out var transitionPeak))
                {
                    transitionPeaks[iTran] = transitionPeak;
                }
            }

            int chosenPeakIndex = GetChosenPeakIndex(nodeGroup, replicateIndex, fileId, chromGroupInfo);
            // The boundaries the peak group was integrated between, which every transition uses
            // unless it kept boundaries of its own. Only wanted when there is no candidate peak to
            // read instead, which is what a negative index says.
            var precursorBounds = chosenPeakIndex < 0
                ? groupResults?.FindPrecursorPeakBounds(replicateIndex, fileId)
                : null;
            for (int iTran = 0; iTran < transitionCount; iTran++)
            {
                if (optStepChromatograms[iTran].IsEmpty)
                {
                    continue;
                }

                var nodeTran = (TransitionDocNode) nodeGroup.Children[iTran];
                var annotations = groupResults?.FindTransitionAnnotations(iTran, replicateIndex, fileId) ??
                                  Annotations.EMPTY;
                var userSet = transitionPeaks[iTran]?.UserSet ?? UserSet.FALSE;
                var peakBounds = groupResults?.FindTransitionCustomPeakBounds(iTran, replicateIndex, fileId) ??
                                 precursorBounds;
                var peakMetrics = groupResults?.FindTransitionCustomPeakMetrics(iTran, replicateIndex, fileId);

                // The peak being changed is put here rather than after the fact, so that the ranks
                // and the dot products which come from all of the transitions together are worked
                // out from the peak the caller asked for.
                bool isChanging = peakChange?.AppliesToTransition(nodeTran.Transition) == true &&
                                  !(peakChange.PreserveMissingPeaks && transitionPeaks[iTran]?.IsEmpty != false);
                if (isChanging)
                {
                    userSet = peakChange.UserSet;
                }

                int stepCount = optStepChromatograms[iTran].StepCount;
                for (int step = -stepCount; step <= stepCount; step++)
                {
                    // A chrom info gets added for every step even when there is no chromatogram
                    // for it, because ChangeResults adds an empty peak there.
                    var chromatogramInfo = optStepChromatograms[iTran].GetChromatogramForStep(step);
                    var chromPeak = isChanging
                        ? peakChange.GetChromPeak(this, nodeGroup, nodeTran, replicateIndex, fileId, step,
                            chromatogramInfo)
                        : GetChromPeak(nodeGroup, nodeTran, replicateIndex, fileId, step, chromatogramInfo,
                            chosenPeakIndex, peakBounds, peakMetrics);
                    lists[iTran].Add(new TransitionChromInfo(fileId, step, chromPeak,
                        chromatogramInfo?.GetIonMobilityFilter() ?? IonMobilityFilter.EMPTY, annotations, userSet));
                }
            }
        }

        /// <summary>
        /// Which of the candidate peaks in the .skyd was chosen in one file, or -1 when no peak was
        /// chosen. This is a property of the peak group: one index covers every transition and
        /// every optimization step, because a transition whose peak is a different one has
        /// <see cref="CustomPeakBounds"/> of its own instead.
        /// </summary>
        private int GetChosenPeakIndex(TransitionGroupDocNode nodeGroup, int replicateIndex, ChromFileInfoId fileId,
            ChromatogramGroupInfo chromGroupInfo)
        {
            var results = nodeGroup.AbbreviatedResults;
            int? chosenPeakIndex = results?.FindChosenPeakIndex(replicateIndex, fileId);
            if (chosenPeakIndex.HasValue)
            {
                return chosenPeakIndex.Value;
            }

            return SearchForChosenPeakIndex(results, replicateIndex, fileId, chromGroupInfo);
        }

        /// <summary>
        /// Works out which candidate peak was chosen while the document does not carry
        /// <see cref="TransitionGroupResults.ChosenPeakIndexes"/> yet: the one with the precursors
        /// boundaries.
        /// <para>
        /// The boundaries identify it on their own. Integrating a chromatogram between the same two
        /// times cannot give a different peak, so there is nothing to gain by checking the area as
        /// well. A peak whose boundaries the user set is looked for the same way as any other: the
        /// user may have picked one of the candidate peaks, and when the boundaries say so there is
        /// no reason to keep them.
        /// </para>
        /// </summary>
        private static int SearchForChosenPeakIndex(TransitionGroupResults results, int replicateIndex,
            ChromFileInfoId fileId, ChromatogramGroupInfo chromGroupInfo)
        {
            if (results == null || !results.Peaks.TryGetValue(replicateIndex, fileId, out var precursorPeak))
            {
                return -1;
            }

            // Zero at both ends is a peak with no boundaries worked out, which matches nothing.
            if (precursorPeak.StartTime == 0 && precursorPeak.EndTime == 0)
            {
                return -1;
            }

            return chromGroupInfo.FindPeakIndex(precursorPeak.StartTime, precursorPeak.EndTime);
        }
        private ChromPeak GetChromPeak(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran,
            int replicateIndex, ChromFileInfoId fileId, int step, ChromatogramInfo chromatogramInfo, int peakIndex,
            CustomPeakBounds? peakBounds, CustomPeakMetrics peakMetrics)
        {
            if (chromatogramInfo == null)
            {
                return ChromPeak.EMPTY;
            }

            if (peakBounds.HasValue)
            {
                return IntegratePeak(nodeGroup, nodeTran, replicateIndex, fileId, step, peakBounds.Value,
                    peakMetrics);
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
            int replicateIndex, ChromFileInfoId fileId, int step, CustomPeakBounds peakBounds,
            CustomPeakMetrics peakMetrics)
        {
            var integrator = GetIntegrator(nodeGroup, replicateIndex, fileId);
            if (integrator == null)
            {
                return ChromPeak.EMPTY;
            }

            // Neither the identification nor the mass error is a property of the boundaries, so
            // integrating again cannot find them. They are what the peak kept for this.
            var identified = peakMetrics?.Identified ?? PeakIdentification.FALSE;
            var flags = default(ChromPeak.FlagValues);
            if (identified != PeakIdentification.FALSE)
            {
                flags |= ChromPeak.FlagValues.contains_id;
            }

            if (identified == PeakIdentification.ALIGNED)
            {
                flags |= ChromPeak.FlagValues.used_id_alignment;
            }

            var chromPeak = integrator.CalcPeak(nodeTran.Transition, step, peakBounds.StartTime,
                peakBounds.EndTime, flags);
            return peakMetrics?.MassError == null ? chromPeak : chromPeak.ChangeMassError(peakMetrics.MassError);
        }

        private TransitionGroupIntegrator GetIntegrator(TransitionGroupDocNode nodeGroup, int replicateIndex,
            ChromFileInfoId fileId)
        {
            var groupResults = FindGroupResults(nodeGroup.TransitionGroup);
            if (groupResults == null)
            {
                return null;
            }

            var chromatograms = Settings.MeasuredResults.Chromatograms[replicateIndex];
            foreach (var chromGroupInfo in GetChromatogramGroupInfos(nodeGroup.TransitionGroup, replicateIndex))
            {
                if (!ReferenceEquals(fileId, chromatograms.FindFile(chromGroupInfo)))
                {
                    continue;
                }

                if (!groupResults.Integrators.TryGetValue(chromGroupInfo, out var integrator))
                {
                    integrator = new TransitionGroupIntegrator(Settings, nodeGroup, chromatograms, chromGroupInfo);
                    groupResults.Integrators.Add(chromGroupInfo, integrator);
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
            // What the precursor is still carrying, which is where the values this pass does not
            // work out - the annotations and the scores - are carried forward from.
            var chromInfos = nodeGroup.AbbreviatedResults?.LegacyChromInfos;
            var previousChromInfos = chromInfos != null && replicateIndex < chromInfos.Count
                ? chromInfos[replicateIndex]
                : default;
            var listCalculator = new TransitionGroupDocNode.TransitionGroupChromInfoListCalculator(Settings,
                PeptideDocNode, replicateIndex, nodeGroup.TransitionCount, previousChromInfos);
            for (int iTran = 0; iTran < nodeGroup.Children.Count; iTran++)
            {
                listCalculator.AddChromInfoList((TransitionDocNode) nodeGroup.Children[iTran], chromInfoLists[iTran]);
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
        private FileStepCorrelation RankFileStep(TransitionGroupDocNode nodeGroup,
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
            for (int iTran = 0; iTran < nodeGroup.Children.Count; iTran++)
            {
                var nodeTran = (TransitionDocNode) nodeGroup.Children[iTran];
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
            if (_groupResults != null)
            {
                return;
            }

            var measuredResults = Settings.MeasuredResults;
            var groupResults = PeptideDocNode.TransitionGroups.Select(nodeGroup => new GroupResults(nodeGroup,
                measuredResults == null
                    ? EmptyChromatogramGroupInfos()
                    : ReadTransitionGroup(measuredResults, nodeGroup))).ToArray();

            // Every precursor of every replicate in one read, grouped by cache, rather than one
            // read per group info the first time each is asked for its peaks. The molecule is
            // about to want all of them.
            //
            // This is why the chromatograms are read for every precursor here rather than when
            // each is first asked for, unlike everything else a GroupResults holds. Reading them
            // one at a time would give up the batching, and would put the reads on the shared
            // pooled stream through ChromatogramGroupInfo.ReadPeaks, which ConvertDocumentResults
            // could then be doing from several threads at once.
            ChromatogramGroupInfo.LoadPeaksForAll(
                groupResults.SelectMany(results => results.ChromatogramGroupInfos.FlatValues), false);
            _groupResults = groupResults;
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
