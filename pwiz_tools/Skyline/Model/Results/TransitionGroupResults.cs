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
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Where one precursor peak is, and which of the candidate peaks in the .skyd it is.
    /// <para>
    /// These four go together because every peak has all four. Held as one
    /// <see cref="ChromFileIdMap{T}"/> of this rather than four maps of one value each, which is
    /// the same bytes per peak but one object and one indirection instead of four. The values only
    /// some peaks have - the scores, the annotations, whether a user set it - stay a map each,
    /// where being absent or uniform collapses to nothing.
    /// </para>
    /// </summary>
    public struct PrecursorPeak
    {
        /// <summary>
        /// What <see cref="ChosenPeakIndex"/> says when nothing has worked out which candidate peak
        /// this is, which is what the paths that put results on a node without reading a
        /// chromatogram leave behind.
        /// </summary>
        public const int NO_PEAK_INDEX = -1;

        public PrecursorPeak(float retentionTime, float startTime, float endTime, int chosenPeakIndex)
        {
            RetentionTime = retentionTime;
            StartTime = startTime;
            EndTime = endTime;
            ChosenPeakIndex = chosenPeakIndex;
        }

        /// <summary>
        /// The apex, and the boundaries the peak was integrated between. Zero means there is no
        /// value, the way NaN does for the scores: no measured peak is at time zero, so the two do
        /// not overlap, and this keeps a time to four bytes rather than the eight a nullable float
        /// would take.
        /// </summary>
        public float RetentionTime { get; private set; }
        public float StartTime { get; private set; }
        public float EndTime { get; private set; }

        /// <summary>
        /// Which of the candidate peaks in the .skyd this is, or <see cref="NO_PEAK_INDEX"/>. One
        /// index covers every transition of the precursor: a transition whose peak is a different
        /// one has <see cref="CustomPeakBounds"/> of its own.
        /// </summary>
        public int ChosenPeakIndex { get; private set; }

        public PrecursorPeak ChangeChosenPeakIndex(int value)
        {
            var peak = this;
            peak.ChosenPeakIndex = value;
            return peak;
        }

        public bool Equals(PrecursorPeak other)
        {
            return RetentionTime.Equals(other.RetentionTime) && StartTime.Equals(other.StartTime) &&
                   EndTime.Equals(other.EndTime) && ChosenPeakIndex == other.ChosenPeakIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is PrecursorPeak other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = RetentionTime.GetHashCode();
                result = (result * 397) ^ StartTime.GetHashCode();
                result = (result * 397) ^ EndTime.GetHashCode();
                result = (result * 397) ^ ChosenPeakIndex;
                return result;
            }
        }
    }

    /// <summary>
    /// Everything one transition peak has: its area, and the flags which have to be answered over
    /// the whole document without reading a chromatogram - quantification asks for
    /// <see cref="IsTruncated"/> and <see cref="IsEmpty"/>, the peak count ratio for
    /// <see cref="IsForcedIntegration"/>, and <see cref="PeptideDocNode.BestResult"/> for
    /// <see cref="Identified"/>.
    /// <para>
    /// Twelve bytes: the area, two byte-wide enums, the three-state truncated flag and two bools,
    /// padded. The flags are separate fields for now; packing them into one would take it to eight.
    /// </para>
    /// </summary>
    public struct TransitionPeak
    {
        public TransitionPeak(float area, UserSet userSet, bool? isTruncated, bool isEmpty,
            PeakIdentification identified, bool isForcedIntegration)
        {
            Area = area;
            UserSet = userSet;
            IsTruncated = isTruncated;
            IsEmpty = isEmpty;
            Identified = identified;
            IsForcedIntegration = isForcedIntegration;
        }

        public float Area { get; private set; }

        /// <summary>
        /// Almost always <see cref="Results.UserSet.FALSE"/>.
        /// </summary>
        public UserSet UserSet { get; private set; }

        /// <summary>
        /// Whether the peak ran off the end of the chromatogram. Three states, as on
        /// <see cref="TransitionChromInfo.IsTruncated"/>: null means nothing worked it out.
        /// </summary>
        public bool? IsTruncated { get; private set; }

        /// <summary>
        /// No peak at all, which is not the same as a peak whose area is zero: quantification
        /// counts the first as missing and the second as measured, and <see cref="Area"/> is zero
        /// either way.
        /// </summary>
        public bool IsEmpty { get; private set; }

        public PeakIdentification Identified { get; private set; }

        /// <summary>
        /// Integrated only because integration was forced, which
        /// <see cref="TransitionChromInfo.IsGoodPeak"/> excludes from the peak count.
        /// </summary>
        public bool IsForcedIntegration { get; private set; }

        /// <summary>
        /// Whether this counts towards the peak count ratio. See
        /// <see cref="TransitionChromInfo.IsGoodPeak"/>, which decides the same thing from a chrom
        /// info.
        /// </summary>
        public bool IsGoodPeak(bool integrateAll)
        {
            if (IsEmpty || !(Area > 0))
            {
                return false;
            }

            return integrateAll || !IsForcedIntegration;
        }

        public bool Equals(TransitionPeak other)
        {
            return Area.Equals(other.Area) && UserSet == other.UserSet &&
                   IsTruncated == other.IsTruncated && IsEmpty == other.IsEmpty &&
                   Identified == other.Identified && IsForcedIntegration == other.IsForcedIntegration;
        }

        public override bool Equals(object obj)
        {
            return obj is TransitionPeak other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Area.GetHashCode();
                result = (result * 397) ^ (int) UserSet;
                result = (result * 397) ^ IsTruncated.GetHashCode();
                result = (result * 397) ^ IsEmpty.GetHashCode();
                result = (result * 397) ^ (int) Identified;
                result = (result * 397) ^ IsForcedIntegration.GetHashCode();
                return result;
            }
        }
    }

    public class TransitionGroupResults : Immutable
    {
        /// <summary>
        /// Builds the columnar form from the chrom infos a document already holds. This is
        /// how both forms can be carried at once while the readers are converted one at a
        /// time. Which candidate peak in the .skyd each peak is stays unknown, because only a
        /// caller which has read the chromatograms can say, and this one has only the chrom
        /// infos: see <see cref="MoleculeResults.ConvertResults"/>, which works them out.
        /// <para>
        /// Only optimization step zero is stored. Nothing here can differ between the steps of
        /// one file: the user cannot set peak boundaries or annotations for one step on its own,
        /// and everything else is read back from the .skyd, which has every step.
        /// </para>
        /// </summary>
        public static TransitionGroupResults FromChromInfos(Results<TransitionGroupChromInfo> results)
        {
            if (results == null)
            {
                return null;
            }

            var fileIds = new List<ChromFileInfoId>();
            var counts = new List<int>();
            var peaks = new List<PrecursorPeak>();
            var userSets = new List<UserSet>();
            var qValues = new List<float>();
            var zScores = new List<float>();
            var annotations = new List<Annotations>();
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                int count = 0;
                foreach (var chromInfo in results[replicateIndex])
                {
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }

                    annotations.Add(chromInfo.Annotations ?? Model.Annotations.EMPTY);
                    fileIds.Add(chromInfo.FileId);
                    peaks.Add(new PrecursorPeak(chromInfo.RetentionTime ?? 0,
                        chromInfo.StartRetentionTime ?? 0, chromInfo.EndRetentionTime ?? 0,
                        PrecursorPeak.NO_PEAK_INDEX));
                    userSets.Add(chromInfo.UserSet);
                    qValues.Add(chromInfo.QValue ?? float.NaN);
                    zScores.Add(chromInfo.ZScore ?? float.NaN);
                    count++;
                }

                counts.Add(count);
            }

            var transitionGroupResults =
                new TransitionGroupResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), peaks)
                    .ChangeUserSets(userSets)
                    .ChangeQValues(qValues)
                    .ChangeZScores(zScores)
                    .ChangeAnnotations(annotations)
                    // Nothing here read the chromatograms, so which candidate peak any of these is
                    // is still to be worked out.
                    .ChangeNeedsPeakIndexes(true);

            // Kept whatever the caller knows, because the precursor level still holds values which
            // have no home in the columnar form yet - PeakCountRatio, the ion mobility info, the dot
            // products. Dropping them waits until every reader of them goes through MoleculeResults.
            return transitionGroupResults.ChangeLegacyChromInfos(results);
        }

        public TransitionGroupResults(ChromFileIds fileIds, IEnumerable<PrecursorPeak> peaks)
        {
            Peaks = new ChromFileIdMap<PrecursorPeak>(fileIds, peaks);
        }

        /// <summary>
        /// A precursor with no peaks of its own, which is what a precursor whose transitions have
        /// results but which has none itself starts from.
        /// </summary>
        public static readonly TransitionGroupResults Empty =
            new TransitionGroupResults(ChromFileIds.Empty, new PrecursorPeak[0]);

        /// <summary>
        /// Where each peak is and which candidate peak it is. Every peak has all of that, so it is
        /// one map of a struct rather than a map per value: the same bytes, but one object and one
        /// indirection per peak instead of four. This is also the map the positions come from,
        /// since it is the one which is always there.
        /// <para>
        /// There is deliberately no Areas here. A precursor's area is the sum of its transitions'
        /// areas, which <see cref="TransitionResults.Areas"/> already holds, and storing one number
        /// for it could not answer the questions callers actually ask - the MS1 area and the MS2
        /// area are different sums over different transitions.
        /// </para>
        /// </summary>
        public ChromFileIdMap<PrecursorPeak> Peaks { get; private set; }

        public ChromFileIds ChromFileIds
        {
            get { return Peaks.ChromFileIds; }
        }

        /// <summary>
        /// The results of the precursor's transitions, one entry per transition, in the order of
        /// <see cref="TransitionGroupDocNode.Children"/>. Null when no transition has any, and a
        /// null entry for a transition which has none.
        /// <para>
        /// These live here rather than on <see cref="TransitionDocNode"/> so that everything a
        /// precursor and its transitions repeat - the files, the replicate layout, the flags which
        /// are the same all the way down - can be stored once. The .sky file already writes the
        /// transition areas once per precursor; holding them per node was where that sharing got
        /// undone.
        /// </para>
        /// <para>
        /// Positional, so it only means anything alongside the precursor it came from.
        /// <see cref="TransitionGroupDocNode.OnChangingChildren"/> keeps it in step with the
        /// children, which is the only reason a caller can trust the index.
        /// </para>
        /// </summary>
        private ImmutableList<TransitionResults> Transitions { get; set; }

        /// <summary>
        /// Which position each of the precursor's transitions sits at, which is what turns the
        /// <see cref="Transition"/> a caller has into an index into <see cref="Transitions"/>.
        /// <para>
        /// The very same instance the precursor's <see cref="DocNodeChildren"/> holds, so this is
        /// a reference rather than a copy, and the two cannot drift: replacing a child in place
        /// leaves the order alone and both go on sharing it.
        /// </para>
        /// </summary>
        public IdentityIndex TransitionIndexes { get; private set; } = IdentityIndex.EMPTY;

        /// <summary>
        /// The precursor's transitions, in the order these results hold them. How a caller which
        /// has the results but not the precursor walks them.
        /// </summary>
        public IEnumerable<Transition> GetTransitions()
        {
            return TransitionIndexes.Identities.Cast<Transition>();
        }

        /// <summary>
        /// These results with a new set of transition results and the <see cref="IdentityIndex"/>
        /// which says which transition each one belongs to. The two always change together, so that
        /// these results are never holding results they cannot name.
        /// </summary>
        private TransitionGroupResults ChangeTransitions(IdentityIndex indexes,
            IEnumerable<TransitionResults> value)
        {
            indexes = indexes ?? IdentityIndex.EMPTY;
            var transitions = ImmutableList.ValueOf(value);
            if (transitions != null && transitions.All(results => results == null))
            {
                transitions = null;
            }

            if (transitions != null && transitions.Count > indexes.Count)
            {
                throw new ArgumentException(
                    string.Format(@"Results for {0} transitions cannot be held by a precursor with {1}.",
                        transitions.Count, indexes.Count));
            }

            return ChangeProp(ImClone(this), im =>
            {
                im.Transitions = transitions;
                im.TransitionIndexes = indexes;
            });
        }

        /// <summary>
        /// The results of one transition, or null when it has none.
        /// </summary>
        private TransitionResults GetTransitionResults(Transition transition)
        {
            return GetTransitionResults(TransitionIndexes.IndexOf(transition));
        }

        private TransitionResults GetTransitionResults(int transitionIndex)
        {
            if (Transitions == null || transitionIndex < 0 || transitionIndex >= Transitions.Count)
            {
                return null;
            }

            return Transitions[transitionIndex];
        }

        /// <summary>
        /// These results with one transition's replaced. Returns this when it is already the same,
        /// so that a document which does not change stays reference equal.
        /// </summary>
        private TransitionGroupResults ChangeTransitionResults(Transition transition, TransitionResults value)
        {
            int transitionIndex = TransitionIndexes.IndexOf(transition);
            return transitionIndex < 0 ? this : ChangeTransitionResults(transitionIndex, value);
        }

        private TransitionGroupResults ChangeTransitionResults(int transitionIndex, TransitionResults value)
        {
            if (Equals(GetTransitionResults(transitionIndex), value))
            {
                return this;
            }

            int count = Math.Max(Transitions?.Count ?? 0, transitionIndex + 1);
            return ChangeTransitions(TransitionIndexes, Enumerable.Range(0, count)
                .Select(i => i == transitionIndex ? value : GetTransitionResults(i)));
        }

        /// <summary>
        /// Whether any of the precursor's transitions has results, which is the cheap question
        /// to ask before doing anything per transition.
        /// </summary>
        public bool HasAnyTransitionResults
        {
            get { return Transitions != null; }
        }

        /// <summary>
        /// How many transitions of the precursor these belong to have an entry, which is all of
        /// them once any of them has results. Zero when none does.
        /// </summary>
        public int TransitionCount
        {
            get { return Transitions?.Count ?? 0; }
        }

        /// <summary>
        /// These results with their transitions put in the order <paramref name="transitions"/>
        /// gives, which is the precursor's children after a change. Each one keeps whatever results
        /// it already had, matched by identity, and one which was not there before starts with
        /// none. See <see cref="TransitionGroupDocNode.OnChangingChildren"/>.
        /// </summary>
        public TransitionGroupResults ReorderTransitions(IEnumerable<Transition> transitions)
        {
            return ReorderTransitions(new IdentityIndex(transitions));
        }

        /// <summary>
        /// These results with their transitions put in the order <paramref name="transitionIndexes"/>
        /// gives. The only way the index changes, and it never changes on its own: every transition's
        /// results move with it, matched by identity, so a transition which is not in the new index
        /// loses its results rather than having another transition's handed to it.
        /// </summary>
        public TransitionGroupResults ReorderTransitions(IdentityIndex transitionIndexes)
        {
            transitionIndexes = transitionIndexes ?? IdentityIndex.EMPTY;
            if (ReferenceEquals(TransitionIndexes, transitionIndexes))
            {
                return this;
            }

            return ChangeTransitions(transitionIndexes, Transitions == null
                ? null
                : transitionIndexes.Identities.Select(identity => GetTransitionResults((Transition) identity)));
        }

        /// <summary>
        /// These results with each transition's handed to the transition at the same position in
        /// <paramref name="transitionIndexes"/>, matched through <paramref name="transitionsInOrder"/>
        /// rather than by identity.
        /// <para>
        /// This is for a copy of a precursor whose transitions are new objects standing for the old
        /// ones - converting a document to small molecules is the only thing which does that - where
        /// <see cref="ReorderTransitions(IdentityIndex)"/> would match nothing and drop every
        /// transition's results. <paramref name="transitionsInOrder"/> is the old transition each
        /// new one was made from, in the order the new ones are children.
        /// </para>
        /// </summary>
        public TransitionGroupResults MapTransitions(IEnumerable<Transition> transitionsInOrder,
            IdentityIndex transitionIndexes)
        {
            transitionIndexes = transitionIndexes ?? IdentityIndex.EMPTY;
            var ordered = ReorderTransitions(new IdentityIndex(transitionsInOrder));
            return ordered.ChangeTransitions(transitionIndexes,
                Enumerable.Range(0, transitionIndexes.Count).Select(ordered.GetTransitionResults));
        }

        /// <summary>
        /// Where one position of a combined set of results comes from: a replicate the pass could
        /// read, or one it could not and so keeps what the precursor already had.
        /// </summary>
        private readonly struct PositionSource
        {
            public PositionSource(bool fromOld, int replicateIndex, int position)
            {
                FromOld = fromOld;
                ReplicateIndex = replicateIndex;
                Position = position;
            }

            public bool FromOld { get; }

            /// <summary>
            /// The replicate this came from, in the results it came from. What the maps which are
            /// not laid out by the same positions as the peaks have to be looked up by - see
            /// <see cref="TransitionResults.Annotations"/>.
            /// </summary>
            public int ReplicateIndex { get; }
            public int Position { get; }
        }

        /// <summary>
        /// These results with the replicates a pass could not read taken from
        /// <paramref name="oldResults"/> instead. Entry i of <paramref name="oldReplicateIndexes"/>
        /// is the replicate of <paramref name="oldResults"/> which holds new replicate i's peaks,
        /// or -1 for a replicate the pass read and so worked out for itself.
        /// <para>
        /// A pass which cannot read a replicate's chromatograms has nothing to say about its peaks,
        /// and the peaks the precursor already had stand. They have to be moved as well as kept,
        /// because the replicate they belong to need not be at the same index: importing one
        /// document into another appends the imported document's replicates, so the peaks the
        /// imported precursors bring with them belong to the appended ones. Without this, importing
        /// a document into another drops every peak the imported document brought.
        /// </para>
        /// <para>
        /// No peak changes here. A peak belongs to a file, and moving the file from one document to
        /// another does not remeasure it.
        /// </para>
        /// </summary>
        public TransitionGroupResults KeepReplicates(TransitionGroupResults oldResults,
            IList<int> oldReplicateIndexes)
        {
            if (oldResults == null || !oldReplicateIndexes.Any(index => index >= 0))
            {
                return this;
            }

            var sources = CombinePositions(ChromFileIds.ReplicatePositions,
                oldResults.ChromFileIds.ReplicatePositions, oldReplicateIndexes, out var counts);
            var chromFileIds = new ChromFileIds(ReplicatePositions.FromCounts(counts),
                sources.Select(source => (source.FromOld ? oldResults : this).ChromFileIds
                    .FileIds[source.Position].Value));
            var results = new TransitionGroupResults(chromFileIds,
                    CombineValues(Peaks, oldResults.Peaks, sources, default(PrecursorPeak)))
                .ChangeUserSets(CombineValues(UserSets, oldResults.UserSets, sources, UserSet.FALSE))
                .ChangeQValues(CombineValues(QValues, oldResults.QValues, sources, float.NaN))
                .ChangeZScores(CombineValues(ZScores, oldResults.ZScores, sources, float.NaN))
                .ChangeAnnotations(CombineValues(Annotations, oldResults.Annotations, sources,
                    Model.Annotations.EMPTY))
                .ChangeOriginalPeakIndexes(CombineValues(OriginalPeakIndexes, oldResults.OriginalPeakIndexes,
                    sources, PrecursorPeak.NO_PEAK_INDEX))
                .ChangeReintegratedPeakIndexes(CombineValues(ReintegratedPeakIndexes,
                    oldResults.ReintegratedPeakIndexes, sources, PrecursorPeak.NO_PEAK_INDEX))
                // The kept peaks are as unconverted as they ever were: nothing read a chromatogram
                // to work out which candidate peak they are.
                .ChangeNeedsPeakIndexes(NeedsPeakIndexes || oldResults.NeedsPeakIndexes)
                .ChangeTransitions(TransitionIndexes, TransitionIndexes.Identities
                    .Select((identity, index) => CombineTransitionResults(GetTransitionResults(index),
                        oldResults.GetTransitionResults((Transition) identity), oldReplicateIndexes)));
            if (LegacyChromInfos != null || oldResults.LegacyChromInfos != null)
            {
                results = results.ChangeLegacyChromInfos(new Results<TransitionGroupChromInfo>(
                    oldReplicateIndexes.Select((oldReplicateIndex, replicateIndex) => oldReplicateIndex < 0
                        ? GetLegacyChromInfos(LegacyChromInfos, replicateIndex)
                        : GetLegacyChromInfos(oldResults.LegacyChromInfos, oldReplicateIndex)).ToArray()));
            }

            return results;
        }

        private static ChromInfoList<TransitionGroupChromInfo> GetLegacyChromInfos(
            Results<TransitionGroupChromInfo> chromInfos, int replicateIndex)
        {
            return chromInfos == null || replicateIndex < 0 || replicateIndex >= chromInfos.Count
                ? default
                : chromInfos[replicateIndex];
        }

        private static TransitionResults CombineTransitionResults(TransitionResults newResults,
            TransitionResults oldResults, IList<int> oldReplicateIndexes)
        {
            if (oldResults == null)
            {
                return newResults;
            }

            return (newResults ?? new TransitionResults(ChromFileIds.Empty, new TransitionPeak[0]))
                .KeepReplicates(oldResults, oldReplicateIndexes);
        }

        /// <summary>
        /// Which position of which of the two sets of results holds each position of the combined
        /// results, and how many of them land in each replicate. A replicate the pass read comes
        /// from the new results at the same index; one it could not comes from the old results at
        /// the index <paramref name="oldReplicateIndexes"/> gives.
        /// </summary>
        private static IList<PositionSource> CombinePositions(ReplicatePositions newPositions,
            ReplicatePositions oldPositions, IList<int> oldReplicateIndexes, out IList<int> counts)
        {
            var sources = new List<PositionSource>();
            var countList = new List<int>();
            for (int replicateIndex = 0; replicateIndex < oldReplicateIndexes.Count; replicateIndex++)
            {
                int oldReplicateIndex = oldReplicateIndexes[replicateIndex];
                bool fromOld = oldReplicateIndex >= 0;
                var positions = fromOld ? oldPositions : newPositions;
                int index = fromOld ? oldReplicateIndex : replicateIndex;
                int count = index >= positions.ReplicateCount ? 0 : positions.GetCount(index);
                for (int i = 0; i < count; i++)
                {
                    sources.Add(new PositionSource(fromOld, index, positions.GetStart(index) + i));
                }

                countList.Add(count);
            }

            counts = countList;
            return sources;
        }

        private static IEnumerable<TValue> CombineValues<TValue>(ChromFileIdMap<TValue> newMap,
            ChromFileIdMap<TValue> oldMap, IList<PositionSource> sources, TValue defaultValue)
        {
            if (newMap == null && oldMap == null)
            {
                return null;
            }

            return sources.Select(source =>
            {
                var map = source.FromOld ? oldMap : newMap;
                return map == null ? defaultValue : map.FlatValues[source.Position];
            });
        }

        /// <summary>
        /// Whether one of the precursor's transitions has any results.
        /// </summary>
        public bool HasTransitionResults(Transition transition)
        {
            return GetTransitionResults(transition) != null;
        }

        /// <summary>
        /// The files and replicate layout of one transition's results, or null when it has none.
        /// <para>
        /// These are the transition's own, which are not the precursor's: a transition can be
        /// missing from a file the precursor was found in.
        /// </para>
        /// </summary>
        public ChromFileIds GetTransitionChromFileIds(Transition transition)
        {
            return GetTransitionResults(transition)?.ChromFileIds;
        }

        /// <summary>
        /// One transition's peaks in one replicate, each with the file it belongs to. This is how a
        /// caller walks a transition's results: the file is what everything else about that peak is
        /// found by, and a position of these results means nothing anywhere else. Empty when the
        /// transition has no results at all.
        /// </summary>
        public IEnumerable<KeyValuePair<ChromFileInfoId, TransitionPeak>> GetTransitionPeaks(Transition transition,
            int replicateIndex)
        {
            var results = GetTransitionResults(transition);
            return results == null
                ? Array.Empty<KeyValuePair<ChromFileInfoId, TransitionPeak>>()
                : results.Peaks[replicateIndex];
        }

        /// <summary>
        /// Every one of a transition's peaks, in no order a caller can rely on. This is what an
        /// average over all of them needs, which is the one question that does not care which
        /// replicate or file each peak came from. Empty when the transition has no results.
        /// </summary>
        public IEnumerable<TransitionPeak> GetAllTransitionPeaks(Transition transition)
        {
            return (IEnumerable<TransitionPeak>) GetTransitionResults(transition)?.Peaks.FlatValues ??
                   Array.Empty<TransitionPeak>();
        }

        /// <summary>
        /// What quantification needs to know about one transition's peaks in one replicate. Empty
        /// when the transition has no results. See
        /// <see cref="TransitionResults.GetQuantifiablePeaks"/>.
        /// </summary>
        public IEnumerable<QuantifiablePeak> GetQuantifiablePeaks(Transition transition, int replicateIndex)
        {
            var results = GetTransitionResults(transition);
            return results == null
                ? Array.Empty<QuantifiablePeak>()
                : results.GetQuantifiablePeaks(replicateIndex);
        }

        /// <summary>
        /// One transition's peak in one file of one replicate, found by file rather than by
        /// position, which is how a caller which has neither the transition's positions nor a
        /// reason to learn them asks.
        /// </summary>
        public bool TryGetTransitionPeak(Transition transition, int replicateIndex, ChromFileInfoId fileId,
            out TransitionPeak peak)
        {
            var results = GetTransitionResults(transition);
            if (results == null)
            {
                peak = default;
                return false;
            }

            return results.Peaks.TryGetValue(replicateIndex, fileId, out peak);
        }

        /// <summary>
        /// The annotations of one transition's peak in one file of one replicate, which are empty
        /// for nearly every peak. See <see cref="TryGetTransitionPeak"/>.
        /// </summary>
        public Annotations FindTransitionAnnotations(Transition transition, int replicateIndex, ChromFileInfoId fileId)
        {
            return GetTransitionResults(transition)?.FindAnnotations(replicateIndex, fileId) ??
                   Model.Annotations.EMPTY;
        }

        /// <summary>
        /// The boundaries one transition's peak was integrated between, when they are not the ones
        /// the rest of the precursor's transitions used, and otherwise null - which is nearly every
        /// peak. See <see cref="FindPrecursorPeakBounds"/> for the boundaries they share.
        /// </summary>
        public CustomPeakBounds? FindTransitionCustomPeakBounds(Transition transition, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return GetTransitionResults(transition)?.FindCustomPeakBounds(replicateIndex, fileId);
        }

        /// <summary>
        /// What one transition's peak keeps because integrating between its boundaries again cannot
        /// find it, or null when it is one of the candidate peaks and the .skyd has it all.
        /// </summary>
        public CustomPeakMetrics FindTransitionCustomPeakMetrics(Transition transition, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return GetTransitionResults(transition)?.FindCustomPeakMetrics(replicateIndex, fileId);
        }

        /// <summary>
        /// The boundaries one transition's peak was integrated between in one file: its own when
        /// its peak was moved on its own, and otherwise the precursor's, which is what nearly
        /// every peak used. Null when there is no peak there.
        /// </summary>
        public CustomPeakBounds? FindTransitionPeakBounds(Transition transition, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return FindTransitionCustomPeakBounds(transition, replicateIndex, fileId) ??
                   FindPrecursorPeakBounds(replicateIndex, fileId);
        }

        /// <summary>
        /// The boundaries of the precursor's peak in one file, which are the ones its transitions
        /// were integrated between unless one of them says otherwise. Null when there is no peak
        /// there, or when nothing worked the boundaries out: see <see cref="PrecursorPeak"/> for
        /// why zero at both ends is what that looks like.
        /// </summary>
        public CustomPeakBounds? FindPrecursorPeakBounds(int replicateIndex, ChromFileInfoId fileId)
        {
            if (!Peaks.TryGetValue(replicateIndex, fileId, out var peak) ||
                (peak.StartTime == 0 && peak.EndTime == 0))
            {
                return null;
            }

            return new CustomPeakBounds(peak.StartTime, peak.EndTime);
        }

        /// <summary>
        /// Whether boundaries are the precursor's own, which is what a transition's are unless its
        /// peak was moved on its own.
        /// </summary>
        private bool IsPrecursorPeakBounds(int replicateIndex, ChromFileInfoId fileId, CustomPeakBounds bounds)
        {
            var precursorBounds = FindPrecursorPeakBounds(replicateIndex, fileId);
            return precursorBounds.HasValue && precursorBounds.Value.Equals(bounds);
        }

        /// <summary>
        /// These results with the annotations of one transition's peak in one file replaced.
        /// </summary>
        public TransitionGroupResults ChangeTransitionAnnotations(Transition transition, int replicateIndex,
            ChromFileInfoId fileId, Annotations annotations)
        {
            var results = GetTransitionResults(transition);
            if (results == null)
            {
                return this;
            }

            return ChangeTransitionResults(transition,
                results.ChangeAnnotations(replicateIndex, fileId, annotations));
        }

        /// <summary>
        /// The areas of every transition of the precursor, at each of the precursor's positions, or
        /// null at a position where any transition has something else to say: no peak there at all,
        /// a user set peak, annotations, or boundaries which are not a candidate peak's. Then they
        /// each need an element of their own.
        /// <para>
        /// Worked out once for the whole precursor. Doing it a position at a time, and looking up
        /// each transition's entry across all of its positions, is quadratic in the number of
        /// replicates, which is enough to make saving a large document look like a hang.
        /// </para>
        /// </summary>
        public float[][] GetSharedTransitionAreas(int transitionCount)
        {
            var replicatePositions = ChromFileIds.ReplicatePositions;
            var areasByPosition = new float[ChromFileIds.FileIds.Count][];
            for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
            {
                foreach (int position in replicatePositions[replicateIndex])
                {
                    var fileId = ChromFileIds.FileIds[position].Value;
                    var areas = new float[transitionCount];
                    for (int iTran = 0; iTran < transitionCount; iTran++)
                    {
                        // Asked by file rather than by position: the position in hand is the
                        // precursor's, and a transition's positions are its own.
                        var results = GetTransitionResults(iTran);
                        if (results?.TryGetPlainArea(replicateIndex, fileId, out float area) != true)
                        {
                            areas = null;
                            break;
                        }

                        areas[iTran] = area;
                    }

                    areasByPosition[position] = areas;
                }
            }

            return areasByPosition;
        }

        /// <summary>
        /// Whether every area one transition has is already in the precursor's shared transition
        /// areas, so that the transition can be left out of the file altogether.
        /// </summary>
        public bool IsTransitionCoveredBySharedAreas(Transition transition,
            ICollection<ReferenceValue<ChromFileInfoId>> sharedAreaFiles)
        {
            var results = GetTransitionResults(transition);
            if (sharedAreaFiles == null || results == null)
            {
                return false;
            }

            var fileIds = results.ChromFileIds.FileIds;
            return fileIds.Count == sharedAreaFiles.Count && fileIds.All(sharedAreaFiles.Contains);
        }

        /// <summary>
        /// These results with every <see cref="ChromFileInfoId"/> replaced by null, at both levels.
        /// See <see cref="ChromFileIds.ClearFileIds"/>, which says why comparing two documents is
        /// the one thing allowed to look past which file id objects they hold.
        /// <para>
        /// <see cref="LegacyChromInfos"/> is left alone: a <see cref="ChromInfo"/> compares its
        /// file id with <see cref="Identity"/> equality, which never told two of them apart.
        /// </para>
        /// </summary>
        public TransitionGroupResults ClearChromFileIds()
        {
            var peaks = Peaks.ClearFileIds();
            if (ReferenceEquals(peaks, Peaks))
                return this;

            return ChangeProp(ImClone(this), im =>
            {
                im.Peaks = peaks;
                im.UserSets = UserSets?.ClearFileIds();
                im.QValues = QValues?.ClearFileIds();
                im.ZScores = ZScores?.ClearFileIds();
                im.Annotations = Annotations?.ClearFileIds();
                im.OriginalPeakIndexes = OriginalPeakIndexes?.ClearFileIds();
                im.ReintegratedPeakIndexes = ReintegratedPeakIndexes?.ClearFileIds();
                im.Transitions = Transitions == null
                    ? null
                    : ImmutableList.ValueOf(Transitions.Select(results => results?.ClearChromFileIds()));
            });
        }

        /// <summary>
        /// The peak a transition which was left out of the document has, given the area its
        /// precursor carried for it in <see cref="ATTR.transition_areas"/>. A transition is only
        /// left out when every one of its peaks says nothing beyond its area, so this is what each
        /// of them said - see <see cref="GetSharedTransitionAreas"/>, which is what decides that.
        /// </summary>
        public static TransitionPeak MakePlainPeak(float area)
        {
            return new TransitionPeak(area, UserSet.FALSE, false, false, PeakIdentification.FALSE, false);
        }

        /// <summary>
        /// These results with one transition's built from chrom infos, which are let go: what a
        /// peak needs until the .skyd says which candidate peak it is goes onto the columnar
        /// results instead. This is what reading the compact encoding does.
        /// </summary>
        public TransitionGroupResults ChangeTransitionFromChromInfos(Transition transition,
            Results<TransitionChromInfo> chromInfos)
        {
            return ChangeTransitionResults(transition,
                    DropSharedPeakBounds(TransitionResults.FromChromInfos(chromInfos)))
                // A chrom info says where its peak is and nothing about which candidate peak in the
                // .skyd that makes it, so that is still to be worked out.
                .ChangeNeedsPeakIndexes(true);
        }

        /// <summary>
        /// These results with every one of the precursor's transitions built from chrom infos, one
        /// entry per transition of <paramref name="transitionIndexes"/> and in that order.
        /// <para>
        /// The whole set at once, rather than a transition at a time, because the index and the
        /// results it names have to arrive together: naming the transitions first and then filling
        /// them in leaves these results, in between, saying they hold something they do not.
        /// </para>
        /// </summary>
        public TransitionGroupResults ChangeTransitionsFromChromInfos(IdentityIndex transitionIndexes,
            IEnumerable<Results<TransitionChromInfo>> chromInfos)
        {
            var results = ReorderTransitions(transitionIndexes);
            foreach (var entry in transitionIndexes.Identities.Zip(chromInfos, Tuple.Create))
            {
                results = results.ChangeTransitionFromChromInfos((Transition) entry.Item1, entry.Item2);
            }

            return results;
        }

        /// <summary>
        /// One transition's results with the boundaries which are the precursor's own dropped, so
        /// that the map holds only the transitions whose peak is not where the rest of the
        /// precursor's are.
        /// <para>
        /// Building a transition's results cannot tell: a chrom info says what its own boundaries
        /// were and nothing about what the other transitions used. Only the precursor knows, so
        /// this is where the map is narrowed to what it is supposed to hold.
        /// </para>
        /// </summary>
        private TransitionResults DropSharedPeakBounds(TransitionResults results)
        {
            var peakBounds = results?.CustomPeakBounds;
            if (peakBounds == null)
            {
                return results;
            }

            var resultsNew = results;
            for (int replicateIndex = 0; replicateIndex < peakBounds.Count; replicateIndex++)
            {
                foreach (var entry in peakBounds[replicateIndex])
                {
                    if (IsPrecursorPeakBounds(replicateIndex, entry.Key, entry.Value))
                    {
                        resultsNew = resultsNew.ChangeCustomPeakBounds(replicateIndex, entry.Key, null);
                    }
                }
            }

            return resultsNew;
        }

        /// <summary>
        /// These results with one transition's built from the columnar values a document was
        /// written with. Each of the sparse lists has one entry per position, or is null when the
        /// transition had no element of its own - which is what nearly every transition has.
        /// </summary>
        public TransitionGroupResults ChangeTransitionResults(Transition transition, ChromFileIds chromFileIds,
            IEnumerable<TransitionPeak> peaks, IEnumerable<Annotations> annotations,
            IEnumerable<CustomPeakBounds> peakBounds, IEnumerable<CustomPeakMetrics> peakMetrics)
        {
            return ChangeTransitionResults(transition,
                DropSharedPeakBounds(new TransitionResults(chromFileIds, peaks, annotations, peakBounds,
                    peakMetrics)));
        }

        /// <summary>
        /// These results with one transition's worked out again from the chrom infos a results
        /// calculation produced. Returns this when the transition already says the same, which is
        /// what keeps a pass that changed nothing from making the whole molecule convert again.
        /// <para>
        /// What comes out of the chrom infos has every peak keeping its boundaries and its metrics,
        /// because nothing yet knows which of them the .skyd can give back, while what is already
        /// there has been through conversion and kept only the ones it could not. So the comparison
        /// is against what conversion would leave behind, which the chosen peak indexes already on
        /// hand are enough to work out. Getting this wrong makes every pass read all of the
        /// molecule's chromatograms, which is enough to make loading a large document look like a
        /// hang.
        /// </para>
        /// </summary>
        /// <summary>
        /// These results with every one of the precursor's transitions worked out again, one entry
        /// per transition of <paramref name="transitionIndexes"/> and in that order. Returns this
        /// when no transition had anything new to say, which is also how a precursor with no results
        /// at all avoids being given an empty set of them.
        /// <para>
        /// The whole set at once, for the reason
        /// <see cref="ChangeTransitionsFromChromInfos"/> gives.
        /// </para>
        /// </summary>
        public TransitionGroupResults UpdateTransitionsFromChromInfos(IdentityIndex transitionIndexes,
            IEnumerable<Results<TransitionChromInfo>> chromInfos)
        {
            var reordered = ReorderTransitions(transitionIndexes);
            var results = reordered;
            foreach (var entry in transitionIndexes.Identities.Zip(chromInfos, Tuple.Create))
            {
                results = results.UpdateTransitionFromChromInfos((Transition) entry.Item1, entry.Item2);
            }

            return ReferenceEquals(results, reordered) ? this : results;
        }

        private TransitionGroupResults UpdateTransitionFromChromInfos(Transition transition,
            Results<TransitionChromInfo> chromInfos)
        {
            var calculated = DropSharedPeakBounds(TransitionResults.FromChromInfos(chromInfos));

            // Only when this pass actually worked something out. A pass which read no chromatogram
            // - because none is loaded yet - has nothing to say, and must not replace what a
            // document was read with.
            if (!(calculated?.Peaks.FlatValues.Count > 0))
            {
                return this;
            }

            var existing = GetTransitionResults(transition);
            if (existing != null && !NeedsPeakIndexes && Equals(existing, DropChosenPeakCustomPeaks(calculated)))
            {
                return this;
            }

            // The peaks are new, so which candidate peak each of them is has to be worked out again.
            return ChangeTransitionResults(transition, calculated).ChangeNeedsPeakIndexes(true);
        }

        /// <summary>
        /// One transition's results with what a peak keeps for being integrated again dropped
        /// wherever the precursor already knows which candidate peak it is. This is what
        /// <see cref="MoleculeResults.ConvertResults"/> leaves behind, worked out here without
        /// reading a chromatogram.
        /// </summary>
        private TransitionResults DropChosenPeakCustomPeaks(TransitionResults results)
        {
            var resultsNew = results;
            var replicatePositions = ChromFileIds.ReplicatePositions;
            for (int replicateIndex = 0; replicateIndex < replicatePositions.ReplicateCount; replicateIndex++)
            {
                foreach (int position in replicatePositions[replicateIndex])
                {
                    if (GetChosenPeakIndex(position).HasValue)
                    {
                        resultsNew = resultsNew.DropCustomPeak(replicateIndex, ChromFileIds.FileIds[position].Value);
                    }
                }
            }

            return resultsNew;
        }

        /// <summary>
        /// These results with each transition's merged from <paramref name="other"/>, where
        /// <paramref name="otherIndexes"/> says which of the other's transitions matches each of
        /// these, or -1 when none does. The caller works that out, since only it knows how the two
        /// precursors' transitions line up.
        /// </summary>
        public TransitionGroupResults MergeTransitions(TransitionGroupResults other, IList<int> otherIndexes)
        {
            if (Transitions == null && other?.Transitions == null)
            {
                return this;
            }

            var results = this;
            for (int iTran = 0; iTran < otherIndexes.Count; iTran++)
            {
                var resultsMerge = other?.GetTransitionResults(otherIndexes[iTran]);
                if (resultsMerge == null)
                {
                    continue;
                }

                // What is already here came from this side, which wins where both have a peak.
                var existing = results.GetTransitionResults(iTran);
                results = results.ChangeTransitionResults(iTran,
                    existing == null ? resultsMerge : existing.MergeUserInfo(resultsMerge));
            }

            return results;
        }

        /// <summary>
        /// These results with what one file's peaks kept for being integrated again dropped from
        /// every transition. Once the peak turns out to be one of the candidate peaks after all,
        /// the index reproduces it, and the .skyd says everything the peak kept for itself.
        /// <para>
        /// The annotations stay: nothing in the .skyd knows those.
        /// </para>
        /// </summary>
        public TransitionGroupResults DropTransitionCustomPeaks(int replicateIndex, ChromFileInfoId fileId)
        {
            var results = this;
            for (int iTran = 0; iTran < TransitionCount; iTran++)
            {
                var transitionResults = results.GetTransitionResults(iTran);
                var newResults = transitionResults?.DropCustomPeak(replicateIndex, fileId);
                if (newResults != null && !ReferenceEquals(newResults, transitionResults))
                {
                    results = results.ChangeTransitionResults(iTran, newResults);
                }
            }

            return results;
        }

        /// <summary>
        /// The peak Skyline originally picked, and the peak reintegration chose. Both are
        /// kept for the parts which need to know where a peak came from rather than only
        /// where it is now - retention time alignment and peak imputation.
        /// <para>
        /// A missing entry - <see cref="PrecursorPeak.NO_PEAK_INDEX"/> at a position, or a null map
        /// altogether - means the same peak as <see cref="PrecursorPeak.ChosenPeakIndex"/>, which is
        /// what nearly every position says. Read them with <see cref="GetOriginalPeakIndex"/> and
        /// <see cref="GetReintegratedPeakIndex"/> rather than indexing, which would give the
        /// sentinel instead of the peak.
        /// </para>
        /// </summary>
        public ChromFileIdMap<int> OriginalPeakIndexes { get; private set; }
        public ChromFileIdMap<int> ReintegratedPeakIndexes { get; private set; }

        /// <summary>
        /// Which candidate peak Skyline originally picked at one position, which is the chosen peak
        /// unless something moved it. Null when neither is known.
        /// </summary>
        public int? GetOriginalPeakIndex(int position)
        {
            return GetPeakIndexOrChosen(OriginalPeakIndexes, position);
        }

        public int? GetReintegratedPeakIndex(int position)
        {
            return GetPeakIndexOrChosen(ReintegratedPeakIndexes, position);
        }

        private int? GetPeakIndexOrChosen(ChromFileIdMap<int> indexes, int position)
        {
            int index = indexes?.FlatValues[position] ?? PrecursorPeak.NO_PEAK_INDEX;
            return index < 0 ? GetChosenPeakIndex(position) : index;
        }

        /// <summary>
        /// Almost always all <see cref="UserSet.FALSE"/>, which is why this gets stored
        /// through <see cref="ImmutableListFactory.MaybeConstant{T}"/>.
        /// </summary>
        public ChromFileIdMap<UserSet> UserSets { get; private set; }

        /// <summary>
        /// Scores which come from the peak scoring model and so cannot be derived from the
        /// .skyd file. Held as one value per position rather than sparsely because a scored
        /// document has one for nearly every position.
        /// <para>
        /// NaN means there is no value, which keeps these four bytes per position instead of
        /// the eight a nullable float would take, and lets a document with no scoring model
        /// collapse to a constant list.
        /// </para>
        /// </summary>
        public ChromFileIdMap<float> QValues { get; private set; }
        public ChromFileIdMap<float> ZScores { get; private set; }

        /// <summary>
        /// One entry per position, Annotations.EMPTY where a peak has none, which is nearly always.
        /// A precursor peak has nothing else which cannot be derived from the .skyd: its boundaries
        /// are on <see cref="PrecursorPeak"/>, where a transition whose peak is somewhere else
        /// reaches past them with a <see cref="CustomPeakBounds"/>, and everything else is worked
        /// out from the chromatogram.
        /// <para>
        /// Stored through <see cref="ImmutableListFactory.MaybeConstant{T}"/>, so a document with no
        /// precursor annotations - almost every document - pays for one entry rather than one for
        /// every position.
        /// </para>
        /// </summary>
        public ChromFileIdMap<Annotations> Annotations { get; private set; }

        /// <summary>
        /// The precursor level values which have no home in the columnar form yet - the peak count
        /// ratio, the ion mobility info, the dot products. Null once they have been given up.
        /// <para>
        /// Kept as a <see cref="Results{TItem}"/> rather than flattened because that is the shape
        /// every reader of <see cref="TransitionGroupDocNode.Results"/> still expects: while these
        /// are here, the node can hand them straight back. There is no transition level
        /// counterpart - a transition's peaks keep <see cref="CustomPeakMetrics"/> and nothing
        /// more - and these go the same way once every reader of them goes through
        /// <see cref="MoleculeResults"/>.
        /// </para>
        /// </summary>
        public Results<TransitionGroupChromInfo> LegacyChromInfos { get; private set; }

        public bool IsConverted
        {
            get { return LegacyChromInfos == null; }
        }

        public TransitionGroupResults ChangeLegacyChromInfos(Results<TransitionGroupChromInfo> value)
        {
            return ChangeProp(ImClone(this), im => im.LegacyChromInfos = value);
        }

        /// <summary>
        /// Whether which candidate peak in the .skyd each of these peaks is has still to be worked
        /// out, which is what a document written before the chosen peak indexes were part of the
        /// format leaves behind. Until it is, every peak is treated as one whose boundaries were
        /// set by hand: the transitions keep what integrating between them cannot find again.
        /// <para>
        /// Reading such a document is what sets this, and
        /// <see cref="MoleculeResults.ConvertResults"/> is what clears it. It cannot be worked out
        /// from the peaks themselves, because a peak which really is not one of the candidate peaks
        /// keeps <see cref="PrecursorPeak.NO_PEAK_INDEX"/> for good.
        /// </para>
        /// </summary>
        public bool NeedsPeakIndexes { get; private set; }

        public TransitionGroupResults ChangeNeedsPeakIndexes(bool value)
        {
            return ChangeProp(ImClone(this), im => im.NeedsPeakIndexes = value);
        }

        /// <summary>
        /// These take an <see cref="IEnumerable{T}"/> rather than an
        /// <see cref="ImmutableList{T}"/> so that <see cref="ImmutableListFactory.ToImmutable{T}"/>
        /// gets the chance to store the indexes as bytes or shorts. A document which had its
        /// peaks picked normally has around ten candidate peaks, so these fit in a byte.
        /// Passing an <see cref="ImmutableList{T}"/> makes that a no-op, on the assumption
        /// that it has already been optimized.
        /// </summary>
        /// <summary>
        /// The peaks with their chosen indexes replaced. Null puts back the negative index which
        /// means "not known", which is what a caller who has not read a chromatogram leaves behind.
        /// </summary>
        public TransitionGroupResults ChangeChosenPeakIndexes(IEnumerable<int> value)
        {
            var indexes = value?.ToArray();
            return ChangeProp(ImClone(this), im => im.Peaks = new ChromFileIdMap<PrecursorPeak>(ChromFileIds,
                Peaks.FlatValues.Select((peak, position) =>
                    peak.ChangeChosenPeakIndex(indexes == null ? PrecursorPeak.NO_PEAK_INDEX : indexes[position]))));
        }

        public TransitionGroupResults ChangeOriginalPeakIndexes(IEnumerable<int> value)
        {
            return ChangeProp(ImClone(this), im => im.OriginalPeakIndexes = MakePeakIndexMap(value));
        }

        public TransitionGroupResults ChangeReintegratedPeakIndexes(IEnumerable<int> value)
        {
            return ChangeProp(ImClone(this), im => im.ReintegratedPeakIndexes = MakePeakIndexMap(value));
        }

        /// <summary>
        /// A map holding only the indexes which differ from the chosen peak, the rest being
        /// <see cref="PrecursorPeak.NO_PEAK_INDEX"/>, and null when none of them differs - which is
        /// nearly every document, since a peak Skyline picked and never had moved is all three.
        /// <para>
        /// The values go through <see cref="ImmutableListFactory.ToImmutable{T}"/> so that a
        /// document whose peaks were picked normally, with around ten candidates, stores each index
        /// in a byte.
        /// </para>
        /// </summary>
        private ChromFileIdMap<int> MakePeakIndexMap(IEnumerable<int> value)
        {
            if (value == null)
            {
                return null;
            }

            var indexes = value.Select((index, position) =>
                index == GetChosenPeakIndex(position) ? PrecursorPeak.NO_PEAK_INDEX : index).ToArray();
            if (indexes.All(index => index == PrecursorPeak.NO_PEAK_INDEX))
            {
                return null;
            }

            return new ChromFileIdMap<int>(ChromFileIds, indexes.ToImmutable());
        }

        /// <summary>
        /// A map over the same positions as <see cref="Peaks"/>, with the values stored through
        /// <see cref="ImmutableListFactory.MaybeConstant{T}"/> so that one of these which says the
        /// same thing everywhere - a document with no scoring model, or no annotations, or no peak
        /// a user set - costs one entry rather than one for every position. Null values give a null
        /// map, which is what a value nothing has worked out looks like.
        /// <para>
        /// These are the sparse values, each its own map. The values every peak has are in
        /// <see cref="PrecursorPeak"/> instead, where being uniform would save nothing.
        /// </para>
        /// </summary>
        private ChromFileIdMap<TValue> MakeMap<TValue>(IEnumerable<TValue> value)
        {
            return value == null
                ? null
                : new ChromFileIdMap<TValue>(ChromFileIds, ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionGroupResults ChangeQValues(IEnumerable<float> value)
        {
            return ChangeProp(ImClone(this), im => im.QValues = MakeMap(value));
        }

        public TransitionGroupResults ChangeZScores(IEnumerable<float> value)
        {
            return ChangeProp(ImClone(this), im => im.ZScores = MakeMap(value));
        }

        public TransitionGroupResults ChangeUserSets(IEnumerable<UserSet> value)
        {
            return ChangeProp(ImClone(this), im => im.UserSets = MakeMap(value));
        }

        public TransitionGroupResults ChangeAnnotations(IEnumerable<Annotations> value)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = MakeMap(value));
        }

        /// <summary>
        /// These results with every annotation not named removed. Returns this when there was
        /// nothing to remove, so an unchanged document stays reference equal.
        /// </summary>
        public TransitionGroupResults StripAnnotationValues(ICollection<string> annotationNamesToKeep)
        {
            var results = this;
            var newAnnotations = StripAnnotations.FromAnnotations(annotationNamesToKeep, Annotations?.FlatValues);
            if (!ReferenceEquals(newAnnotations, Annotations?.FlatValues))
                results = ChangeAnnotations(newAnnotations);

            // The transitions' peak annotations too, since a precursor owns them now.
            if (Transitions != null)
            {
                var newTransitions = Transitions
                    .Select(transition => transition?.StripAnnotationValues(annotationNamesToKeep)).ToArray();
                if (!ArrayUtil.ReferencesEqual(newTransitions, Transitions))
                    results = results.ChangeTransitions(TransitionIndexes, newTransitions);
            }

            return results;
        }

        /// <summary>
        /// These results with the user's work in <paramref name="other"/> merged in: where both have
        /// a peak for a file, the other's replaces this one's when the user set it, and where only
        /// the other has one, it is added. Returns this when the other has nothing to contribute, so
        /// that a document which does not change stays reference equal.
        /// <para>
        /// This is what merging one document's user info into another comes to now that the peaks
        /// themselves live in the .skyd. Every column moves together, so the merge is worked out
        /// once as a list of where each position of the answer comes from.
        /// </para>
        /// </summary>
        public TransitionGroupResults MergeUserInfo(TransitionGroupResults other)
        {
            var sources = MergeSource.Build(ChromFileIds, other?.ChromFileIds,
                position => GetUserSet(position) != UserSet.FALSE, out var counts);
            if (sources == null)
            {
                return this;
            }

            var results = new TransitionGroupResults(
                new ChromFileIds(ReplicatePositions.FromCounts(counts),
                    sources.Select(source => source.Pick(ChromFileIds, other.ChromFileIds).FileIds[source.Position].Value)),
                sources.Select(source => source.Pick(this, other).Peaks.FlatValues[source.Position]));
            if (UserSets != null || other.UserSets != null)
            {
                results = results.ChangeUserSets(
                    sources.Select(source => source.Pick(this, other).GetUserSet(source.Position)));
            }

            results = results
                .ChangeOriginalPeakIndexes(MergeIndexes(sources, other, r => r.OriginalPeakIndexes,
                    (r, position) => r.GetOriginalPeakIndex(position)))
                .ChangeReintegratedPeakIndexes(MergeIndexes(sources, other, r => r.ReintegratedPeakIndexes,
                    (r, position) => r.GetReintegratedPeakIndex(position)));
            if (QValues != null || other.QValues != null)
            {
                results = results.ChangeQValues(sources.Select(source =>
                    source.Pick(this, other).GetQValue(source.Position) ?? float.NaN));
            }

            if (ZScores != null || other.ZScores != null)
            {
                results = results.ChangeZScores(sources.Select(source =>
                    source.Pick(this, other).GetZScore(source.Position) ?? float.NaN));
            }

            return results.ChangeAnnotations(
                sources.Select(source => source.Pick(this, other).GetAnnotations(source.Position)));
        }

        /// <summary>
        /// The merged indexes, read through <paramref name="getIndex"/> so that a position where
        /// the map says nothing gives back the chosen peak rather than the sentinel. What
        /// <see cref="MakePeakIndexMap"/> stores is worked out again against the merged chosen
        /// peaks, which are not the same as either side's.
        /// </summary>
        private IEnumerable<int> MergeIndexes(IList<MergeSource> sources, TransitionGroupResults other,
            Func<TransitionGroupResults, ChromFileIdMap<int>> getIndexes,
            Func<TransitionGroupResults, int, int?> getIndex)
        {
            if (getIndexes(this) == null && getIndexes(other) == null)
            {
                return null;
            }

            return sources.Select(source =>
                getIndex(source.Pick(this, other), source.Position) ?? PrecursorPeak.NO_PEAK_INDEX);
        }

        /// <summary>
        /// Which candidate peak one file of one replicate is on, or null when there is no peak there
        /// or nothing has worked out which one it is. This is how a caller which has a file rather
        /// than a position of these results asks: a position means nothing away from the map it
        /// came from.
        /// </summary>
        public int? FindChosenPeakIndex(int replicateIndex, ChromFileInfoId fileId)
        {
            if (!Peaks.TryGetValue(replicateIndex, fileId, out var peak))
            {
                return null;
            }

            return peak.ChosenPeakIndex < 0 ? (int?) null : peak.ChosenPeakIndex;
        }

        /// <summary>
        /// The q value of one file of one replicate, or null when there is none. See
        /// <see cref="FindChosenPeakIndex"/>.
        /// </summary>
        public float? FindQValue(int replicateIndex, ChromFileInfoId fileId)
        {
            if (!QValues.TryGetValue(replicateIndex, fileId, out float qValue))
            {
                return null;
            }

            return float.IsNaN(qValue) ? (float?) null : qValue;
        }

        /// <summary>
        /// The annotations of one file of one replicate. See <see cref="FindChosenPeakIndex"/>.
        /// </summary>
        public Annotations FindAnnotations(int replicateIndex, ChromFileInfoId fileId)
        {
            if (!Annotations.TryGetValue(replicateIndex, fileId, out var annotations))
            {
                return Model.Annotations.EMPTY;
            }

            return annotations;
        }

        /// <summary>
        /// Which of the candidate peaks in the .skyd is the chosen one, or null when that is not
        /// known. One index covers every transition of the precursor: a transition whose peak is a
        /// different one has <see cref="CustomPeakBounds"/> of its own.
        /// <para>
        /// A negative index reads back as null rather than as "no candidate peak". The paths which
        /// put results on a node without looking at any chromatogram cannot know an index, and this
        /// is what they leave behind. A peak which really is not one of the candidate peaks is
        /// reproduced by integrating between <see cref="FindPrecursorPeakBounds"/> instead.
        /// </para>
        /// </summary>
        public int? GetChosenPeakIndex(int position)
        {
            int chosenPeakIndex = Peaks.FlatValues[position].ChosenPeakIndex;
            return chosenPeakIndex < 0 ? (int?) null : chosenPeakIndex;
        }

        public Annotations GetAnnotations(int position)
        {
            return Annotations == null ? Model.Annotations.EMPTY : Annotations.FlatValues[position];
        }

        public UserSet GetUserSet(int position)
        {
            return UserSets == null ? UserSet.FALSE : UserSets.FlatValues[position];
        }

        /// <summary>
        /// The flat positions belonging to one replicate. How a caller walks a replicate without
        /// counting: the entries of one are in no order it can rely on.
        /// </summary>
        public IEnumerable<int> GetPositions(int replicateIndex)
        {
            return ChromFileIds.ReplicatePositions[replicateIndex];
        }

        /// <summary>
        /// The apex and the boundaries of one peak, or null where there is no value. See
        /// <see cref="RetentionTimes"/> for why zero is what that looks like in the lists.
        /// </summary>
        public float? GetRetentionTime(int position)
        {
            return NullIfZero(Peaks.FlatValues[position].RetentionTime);
        }

        public float? GetStartTime(int position)
        {
            return NullIfZero(Peaks.FlatValues[position].StartTime);
        }

        public float? GetEndTime(int position)
        {
            return NullIfZero(Peaks.FlatValues[position].EndTime);
        }

        private static float? NullIfZero(float time)
        {
            return time == 0 ? (float?) null : time;
        }

        public float? GetQValue(int position)
        {
            return GetScore(QValues?.FlatValues, position);
        }

        public float? GetZScore(int position)
        {
            return GetScore(ZScores?.FlatValues, position);
        }

        private static float? GetScore(ImmutableList<float> scores, int position)
        {
            if (scores == null)
            {
                return null;
            }

            var score = scores[position];
            return float.IsNaN(score) ? (float?) null : score;
        }

        /// <summary>
        /// Compared by value, so that recalculating results which have not changed can leave the
        /// document alone. Reference equality of an unchanged document is relied on all over
        /// Skyline, and these are set from the results calculation.
        /// </summary>
        protected bool Equals(TransitionGroupResults other)
        {
            // No ChromFileIds of its own: Peaks carries it and is always there.
            return Equals(Peaks, other.Peaks) &&
                   Equals(Transitions, other.Transitions) &&
                   Equals(OriginalPeakIndexes, other.OriginalPeakIndexes) &&
                   Equals(ReintegratedPeakIndexes, other.ReintegratedPeakIndexes) &&
                   Equals(UserSets, other.UserSets) && Equals(QValues, other.QValues) &&
                   Equals(ZScores, other.ZScores) && Equals(Annotations, other.Annotations) &&
                   NeedsPeakIndexes == other.NeedsPeakIndexes &&
                   Results<TransitionGroupChromInfo>.EqualsDeep(LegacyChromInfos, other.LegacyChromInfos);
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((TransitionGroupResults) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Peaks.GetHashCode();
                result = (result * 397) ^ (Transitions?.GetHashCode() ?? 0);
                result = (result * 397) ^ (OriginalPeakIndexes?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ReintegratedPeakIndexes?.GetHashCode() ?? 0);
                result = (result * 397) ^ (UserSets?.GetHashCode() ?? 0);
                result = (result * 397) ^ (QValues?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ZScores?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Annotations?.GetHashCode() ?? 0);
                result = (result * 397) ^ NeedsPeakIndexes.GetHashCode();
                result = (result * 397) ^ (LegacyChromInfos?.GetHashCode() ?? 0);
                return result;
            }
        }

        /// <summary>
        /// Note that this deliberately holds no retention times. The apex of an individual
        /// transition matters much less than the apex of the transition group, so code which
        /// wants it reads it back from the .skyd file instead.
        /// <para>
        /// There is one entry per file per replicate: optimization step zero only. Nothing stored
        /// here can differ between the steps of one file.
        /// </para>
        /// </summary>
        private class TransitionResults : Immutable
        {
            /// <summary>
            /// Builds the columnar form from chrom infos and lets them go. Every peak keeps the
            /// boundaries it was integrated between and what integrating between them again cannot
            /// find, because nothing is left to carry any of it: a whole
            /// <see cref="TransitionChromInfo"/> is around a hundred bytes, and what has to survive
            /// until the .skyd says which candidate peak the peak is comes to a handful.
            /// <para>
            /// <see cref="MoleculeResults.ConvertResults"/> is what gets rid of them again, wherever
            /// the peak turns out to be one of the candidate peaks after all.
            /// </para>
            /// </summary>
            public static TransitionResults FromChromInfos(Results<TransitionChromInfo> results)
            {
                if (results == null)
                {
                    return null;
                }

                var fileIds = new List<ChromFileInfoId>();
                var counts = new List<int>();
                var peaks = new List<TransitionPeak>();
                var annotations = new List<Annotations>();
                var peakBounds = new List<CustomPeakBounds>();
                var peakMetrics = new List<CustomPeakMetrics>();
                foreach (var chromInfoList in results)
                {
                    int count = 0;
                    foreach (var chromInfo in chromInfoList)
                    {
                        // Only step zero, the same as everything else here. The other steps say
                        // nothing which is not read back from the .skyd along with them.
                        if (chromInfo.OptimizationStep != 0)
                        {
                            continue;
                        }

                        fileIds.Add(chromInfo.FileId);
                        peaks.Add(new TransitionPeak(chromInfo.Area, chromInfo.UserSet, chromInfo.IsTruncated,
                            chromInfo.IsEmpty, chromInfo.Identified, chromInfo.IsForcedIntegration));
                        annotations.Add(chromInfo.Annotations ?? Model.Annotations.EMPTY);

                        // The boundaries the peak was integrated between, and what integrating
                        // between them again cannot work out. An empty peak has neither.
                        peakBounds.Add(chromInfo.IsEmpty
                            ? default
                            : new CustomPeakBounds(chromInfo.StartRetentionTime, chromInfo.EndRetentionTime));
                        // Qualified, because the property of that name here is the map of them.
                        peakMetrics.Add(chromInfo.IsEmpty
                            ? null
                            : Model.Results.CustomPeakMetrics.Create(chromInfo.MassError, chromInfo.Identified));
                        count++;
                    }

                    counts.Add(count);
                }

                return new TransitionResults(
                    new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), peaks, annotations, peakBounds,
                    peakMetrics);
            }

            public TransitionResults(ChromFileIds chromFileIds, IEnumerable<TransitionPeak> peaks)
            {
                Peaks = new ChromFileIdMap<TransitionPeak>(chromFileIds, peaks);
            }

            /// <summary>
            /// The sparse values as one entry per position, which is the shape they are read and
            /// written in. Any of them may be null, meaning no position has one. They do not become
            /// one map of a struct here: what is stored is a map each, so that a value nothing has
            /// costs nothing at all.
            /// </summary>
            public TransitionResults(ChromFileIds chromFileIds, IEnumerable<TransitionPeak> peaks,
                IEnumerable<Annotations> annotations, IEnumerable<CustomPeakBounds> peakBounds,
                IEnumerable<CustomPeakMetrics> peakMetrics)
                : this(chromFileIds, peaks)
            {
                Annotations = MakeSparseMap(chromFileIds, annotations, Model.Annotations.EMPTY);
                CustomPeakBounds = MakeSparseMap(chromFileIds, peakBounds, default(CustomPeakBounds));
                CustomPeakMetrics = MakeSparseMap(chromFileIds, peakMetrics, (CustomPeakMetrics) null);
            }

            /// <summary>
            /// Everything every peak has: its area, and the handful of flags quantification and the
            /// peak count ratio ask for over the whole document. One map of a struct rather than a map
            /// per value, and the map the transition's own files come from since it is always there.
            /// </summary>
            public ChromFileIdMap<TransitionPeak> Peaks { get; private set; }

            public ChromFileIds ChromFileIds
            {
                get { return Peaks.ChromFileIds; }
            }

            /// <summary>
            /// The peaks which have an annotation, which is nearly none of them. Null when no peak
            /// does.
            /// <para>
            /// This and the two below are each a map in their own right: how many entries one has
            /// says nothing about how many another has, and none of them lines up with
            /// <see cref="Peaks"/>. A value is found by replicate and file, never by carrying a
            /// position across from one to another.
            /// </para>
            /// </summary>
            public ChromFileIdMap<Annotations> Annotations { get; private set; }

            /// <summary>
            /// The peaks which were integrated between boundaries the rest of the precursor's
            /// transitions did not share, which is what a transition whose peak the user moved on
            /// its own has. Null when no peak does, which is nearly every transition: usually the
            /// whole peak group has the boundaries on <see cref="PrecursorPeak"/>.
            /// </summary>
            public ChromFileIdMap<CustomPeakBounds> CustomPeakBounds { get; private set; }

            /// <summary>
            /// What the peaks which are not candidate peaks in the .skyd keep, because integrating
            /// between their boundaries again cannot find it. Null when every peak is one of the
            /// candidate peaks.
            /// <para>
            /// This and <see cref="CustomPeakBounds"/> are the whole of what a peak whose candidate
            /// peak is not known yet costs. No chrom info is kept for it: a document read before
            /// its .skyd holds a few bytes a peak rather than a hundred.
            /// </para>
            /// </summary>
            public ChromFileIdMap<CustomPeakMetrics> CustomPeakMetrics { get; private set; }

            /// <summary>
            /// The transition level counterpart of
            /// <see cref="TransitionGroupResults.ClearChromFileIds"/>. Each of the four is a map in
            /// its own right, over a layout of its own, so each has to be cleared separately.
            /// </summary>
            public TransitionResults ClearChromFileIds()
            {
                var peaks = Peaks.ClearFileIds();
                if (ReferenceEquals(peaks, Peaks))
                    return this;

                return ChangeProp(ImClone(this), im =>
                {
                    im.Peaks = peaks;
                    im.Annotations = Annotations?.ClearFileIds();
                    im.CustomPeakBounds = CustomPeakBounds?.ClearFileIds();
                    im.CustomPeakMetrics = CustomPeakMetrics?.ClearFileIds();
                });
            }

            /// <summary>
            /// The transition level counterpart of <see cref="TransitionGroupResults.KeepReplicates"/>,
            /// combined through their own positions rather than the precursor's, because a
            /// transition need not have a peak everywhere the precursor does.
            /// </summary>
            public TransitionResults KeepReplicates(TransitionResults oldResults,
                IList<int> oldReplicateIndexes)
            {
                var sources = CombinePositions(ChromFileIds.ReplicatePositions,
                    oldResults.ChromFileIds.ReplicatePositions, oldReplicateIndexes, out var counts);
                var chromFileIds = new ChromFileIds(ReplicatePositions.FromCounts(counts),
                    sources.Select(source => GetFileId(source, oldResults)));
                return new TransitionResults(chromFileIds,
                    CombineValues(Peaks, oldResults.Peaks, sources, default(TransitionPeak)),
                    // By replicate and file rather than by position: these three are maps in their
                    // own right, and a position in one of them means nothing in the peaks.
                    sources.Select(source => Find(source, Annotations, oldResults.Annotations,
                        oldResults, Model.Annotations.EMPTY)),
                    sources.Select(source => Find(source, CustomPeakBounds, oldResults.CustomPeakBounds,
                        oldResults, default(CustomPeakBounds))),
                    sources.Select(source => Find(source, CustomPeakMetrics, oldResults.CustomPeakMetrics,
                        oldResults, (CustomPeakMetrics) null)));
            }

            private ChromFileInfoId GetFileId(PositionSource source, TransitionResults oldResults)
            {
                return (source.FromOld ? oldResults : this).ChromFileIds.FileIds[source.Position].Value;
            }

            private TValue Find<TValue>(PositionSource source, ChromFileIdMap<TValue> newMap,
                ChromFileIdMap<TValue> oldMap, TransitionResults oldResults, TValue defaultValue)
            {
                var map = source.FromOld ? oldMap : newMap;
                if (map == null)
                {
                    return defaultValue;
                }

                return map.TryGetValue(source.ReplicateIndex, GetFileId(source, oldResults), out var value)
                    ? value
                    : defaultValue;
            }

            /// <summary>
            /// What quantification needs to know about the peaks of one replicate.
            /// <para>
            /// This is deliberately everything quantification needs and nothing else, so that it can
            /// run over a whole document without a chromatogram being read. Only optimization step zero
            /// is here, which is the step quantification uses.
            /// </para>
            /// </summary>
            public IEnumerable<QuantifiablePeak> GetQuantifiablePeaks(int replicateIndex)
            {
                foreach (var entry in Peaks[replicateIndex])
                {
                    var peak = entry.Value;
                    yield return new QuantifiablePeak(entry.Key, peak.Area, peak.IsTruncated, peak.IsEmpty);
                }
            }

            /// <summary>
            /// A map holding only the values which say something, or null when none of them does -
            /// which is what nearly every one of these has. The values come in one per position of
            /// <paramref name="chromFileIds"/>, and what survives keeps the file it belongs to, so
            /// the result has positions of its own which are nobody else's.
            /// </summary>
            private static ChromFileIdMap<TValue> MakeSparseMap<TValue>(ChromFileIds chromFileIds,
                IEnumerable<TValue> values, TValue defaultValue)
            {
                if (values == null)
                {
                    return null;
                }

                return new ChromFileIdMap<TValue>(chromFileIds, values).WithoutDefault(defaultValue)?.Normalize();
            }

            /// <summary>
            /// The map with one file's value replaced, and no entry at all where the value is the
            /// one an absent entry already means. Null when that leaves nothing.
            /// </summary>
            private static ChromFileIdMap<TValue> SetValue<TValue>(ChromFileIdMap<TValue> map, int replicateIndex,
                ChromFileInfoId fileId, TValue value, TValue defaultValue)
            {
                if (map == null)
                {
                    if (EqualityComparer<TValue>.Default.Equals(value, defaultValue))
                    {
                        return null;
                    }

                    map = ChromFileIdMap<TValue>.Empty;
                }

                return map.Set(replicateIndex, fileId, value).WithoutDefault(defaultValue)?.Normalize();
            }

            public Annotations FindAnnotations(int replicateIndex, ChromFileInfoId fileId)
            {
                if (Annotations == null || !Annotations.TryGetValue(replicateIndex, fileId, out var annotations))
                {
                    return Model.Annotations.EMPTY;
                }

                return annotations;
            }

            public CustomPeakBounds? FindCustomPeakBounds(int replicateIndex, ChromFileInfoId fileId)
            {
                if (CustomPeakBounds == null ||
                    !CustomPeakBounds.TryGetValue(replicateIndex, fileId, out var peakBounds))
                {
                    return null;
                }

                return peakBounds;
            }

            public CustomPeakMetrics FindCustomPeakMetrics(int replicateIndex, ChromFileInfoId fileId)
            {
                if (CustomPeakMetrics == null ||
                    !CustomPeakMetrics.TryGetValue(replicateIndex, fileId, out var peakMetrics))
                {
                    return null;
                }

                return peakMetrics;
            }

            /// <summary>
            /// Whether one file's peak has anything of its own: an annotation, boundaries which are
            /// not the precursor's, or something integrating between them could not find again.
            /// </summary>
            public bool HasCustomPeak(int replicateIndex, ChromFileInfoId fileId)
            {
                return !FindAnnotations(replicateIndex, fileId).IsEmpty ||
                       FindCustomPeakBounds(replicateIndex, fileId).HasValue ||
                       FindCustomPeakMetrics(replicateIndex, fileId) != null;
            }

            private TransitionResults ChangeAnnotations(ChromFileIdMap<Annotations> value)
            {
                return ChangeProp(ImClone(this), im => im.Annotations = value);
            }

            public TransitionResults ChangeAnnotations(int replicateIndex, ChromFileInfoId fileId, Annotations value)
            {
                return ChangeAnnotations(SetValue(Annotations, replicateIndex, fileId,
                    value ?? Model.Annotations.EMPTY, Model.Annotations.EMPTY));
            }

            public TransitionResults ChangeCustomPeakBounds(int replicateIndex, ChromFileInfoId fileId,
                CustomPeakBounds? value)
            {
                return ChangeProp(ImClone(this), im => im.CustomPeakBounds = SetValue(CustomPeakBounds,
                    replicateIndex, fileId, value ?? default, default(CustomPeakBounds)));
            }

            public TransitionResults ChangeCustomPeakMetrics(int replicateIndex, ChromFileInfoId fileId,
                CustomPeakMetrics value)
            {
                return ChangeProp(ImClone(this), im => im.CustomPeakMetrics = SetValue(CustomPeakMetrics,
                    replicateIndex, fileId, value, null));
            }

            /// <summary>
            /// See <see cref="TransitionGroupResults.StripAnnotationValues"/>.
            /// </summary>
            public TransitionResults StripAnnotationValues(ICollection<string> annotationNamesToKeep)
            {
                var newAnnotations = StripAnnotations.FromAnnotations(annotationNamesToKeep, Annotations?.FlatValues);
                if (ReferenceEquals(newAnnotations, Annotations?.FlatValues))
                {
                    return this;
                }

                return ChangeAnnotations(MakeSparseMap(Annotations.ChromFileIds, newAnnotations,
                    Model.Annotations.EMPTY));
            }

            /// <summary>
            /// These results with what one file's peak kept for being integrated again dropped,
            /// which is what there is to do once it turns out to be one of the candidate peaks after
            /// all: the index reproduces it, and the .skyd has everything about it. The annotations
            /// stay, since nothing in the .skyd knows those.
            /// </summary>
            public TransitionResults DropCustomPeak(int replicateIndex, ChromFileInfoId fileId)
            {
                if (!FindCustomPeakBounds(replicateIndex, fileId).HasValue &&
                    FindCustomPeakMetrics(replicateIndex, fileId) == null)
                {
                    return this;
                }

                return ChangeCustomPeakBounds(replicateIndex, fileId, null)
                    .ChangeCustomPeakMetrics(replicateIndex, fileId, null);
            }

            /// <summary>
            /// Whether one file of one replicate has a peak here which says nothing beyond its area,
            /// and if so what that area is. Answered together so that a caller working across objects
            /// - a precursor's positions are not its transitions' - never has to hold a position of
            /// this one.
            /// </summary>
            public bool TryGetPlainArea(int replicateIndex, ChromFileInfoId fileId, out float area)
            {
                area = 0;
                if (!Peaks.TryGetValue(replicateIndex, fileId, out var peak) || !IsPlainPeak(peak) ||
                    HasCustomPeak(replicateIndex, fileId))
                {
                    return false;
                }

                area = peak.Area;
                return true;
            }

            /// <summary>
            /// Whether a peak says nothing beyond its area, so that it can ride its precursor's
            /// shared transition areas and not be written at all. Asked by comparing against the
            /// peak the reader puts back for a transition which was left out, so that the two
            /// cannot drift apart. A peak whose truncation was never worked out is not ordinary:
            /// saying it was not truncated would be claiming something the document does not know.
            /// </summary>
            private static bool IsPlainPeak(TransitionPeak peak)
            {
                return Equals(peak, MakePlainPeak(peak.Area));
            }

            /// <summary>
            /// The transition level counterpart of
            /// <see cref="TransitionGroupResults.MergeUserInfo"/>, worked out the same way. Each of
            /// the sparse maps is rebuilt by file rather than by position: the merged results have
            /// positions of their own, and so does every one of the maps being merged.
            /// </summary>
            public TransitionResults MergeUserInfo(TransitionResults other)
            {
                var sources = MergeSource.Build(ChromFileIds, other?.ChromFileIds,
                    position => other.Peaks.FlatValues[position].UserSet != UserSet.FALSE, out var counts);
                if (sources == null)
                {
                    return this;
                }

                var chromFileIds = new ChromFileIds(ReplicatePositions.FromCounts(counts),
                    sources.Select(source =>
                        source.Pick(ChromFileIds, other.ChromFileIds).FileIds[source.Position].Value));
                var results = new TransitionResults(chromFileIds,
                    sources.Select(source => source.Pick(this, other).Peaks.FlatValues[source.Position]));
                return results
                    .ChangeAnnotations(MergeMap(sources, chromFileIds, other,
                        (from, replicateIndex, fileId) => from.FindAnnotations(replicateIndex, fileId),
                        Model.Annotations.EMPTY))
                    .ChangeCustomPeakBounds(MergeMap(sources, chromFileIds, other,
                        (from, replicateIndex, fileId) => from.FindCustomPeakBounds(replicateIndex, fileId) ?? default,
                        default(CustomPeakBounds)))
                    .ChangeCustomPeakMetrics(MergeMap(sources, chromFileIds, other,
                        (from, replicateIndex, fileId) => from.FindCustomPeakMetrics(replicateIndex, fileId), null));
            }

            /// <summary>
            /// One of the sparse maps of the merged results, read by replicate and file out of
            /// whichever side each merged position came from.
            /// </summary>
            private ChromFileIdMap<TValue> MergeMap<TValue>(IList<MergeSource> sources, ChromFileIds chromFileIds,
                TransitionResults other, Func<TransitionResults, int, ChromFileInfoId, TValue> getValue,
                TValue defaultValue)
            {
                return MakeSparseMap(chromFileIds, sources.Select((source, position) =>
                    getValue(source.Pick(this, other), source.ReplicateIndex,
                        chromFileIds.FileIds[position].Value)), defaultValue);
            }

            private TransitionResults ChangeCustomPeakBounds(ChromFileIdMap<CustomPeakBounds> value)
            {
                return ChangeProp(ImClone(this), im => im.CustomPeakBounds = value);
            }

            private TransitionResults ChangeCustomPeakMetrics(ChromFileIdMap<CustomPeakMetrics> value)
            {
                return ChangeProp(ImClone(this), im => im.CustomPeakMetrics = value);
            }

            /// <summary>
            /// Compared by value. See <see cref="TransitionGroupResults.Equals(TransitionGroupResults)"/>.
            /// </summary>
            protected bool Equals(TransitionResults other)
            {
                // No ChromFileIds of its own: every map carries it, and Peaks is always there.
                return Equals(Peaks, other.Peaks) &&
                       Equals(Annotations, other.Annotations) &&
                       Equals(CustomPeakBounds, other.CustomPeakBounds) &&
                       Equals(CustomPeakMetrics, other.CustomPeakMetrics);
            }

            public override bool Equals(object obj)
            {
                if (obj is null)
                {
                    return false;
                }

                if (ReferenceEquals(this, obj))
                {
                    return true;
                }

                return obj.GetType() == GetType() && Equals((TransitionResults) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = Peaks.GetHashCode();
                    result = (result * 397) ^ (Annotations?.GetHashCode() ?? 0);
                    result = (result * 397) ^ (CustomPeakBounds?.GetHashCode() ?? 0);
                    result = (result * 397) ^ (CustomPeakMetrics?.GetHashCode() ?? 0);
                    return result;
                }
            }
        }
    }

    /// <summary>
    /// Where one position of a merged set of results comes from: the results being merged into, or
    /// the ones being merged in, and which position of it.
    /// <para>
    /// Working the merge out once as a list of these and then projecting every column through it is
    /// what keeps the columns in step, and is why the precursor and the transition level can share
    /// the arithmetic even though their results have different columns.
    /// </para>
    /// </summary>
    public class MergeSource
    {
        private MergeSource(bool fromOther, int replicateIndex, int position)
        {
            FromOther = fromOther;
            ReplicateIndex = replicateIndex;
            Position = position;
        }

        public bool FromOther { get; }

        /// <summary>
        /// Which replicate this position belongs to, which is half of what a value is found by once
        /// the results being merged do not all have the same positions.
        /// </summary>
        public int ReplicateIndex { get; }
        public int Position { get; }

        public T Pick<T>(T mine, T other)
        {
            return FromOther ? other : mine;
        }

        /// <summary>
        /// One entry per position of the merged results, or null when <paramref name="otherFileIds"/>
        /// has nothing the caller does not already have, which is the usual case and the one where
        /// the caller should hand back what it was given.
        /// <para>
        /// <paramref name="otherIsUserSet"/> decides whether the other results win at a position both
        /// have: the point of the merge is to keep what a user did.
        /// </para>
        /// </summary>
        public static IList<MergeSource> Build(ChromFileIds fileIds, ChromFileIds otherFileIds,
            Func<int, bool> otherIsUserSet, out IList<int> counts)
        {
            counts = null;
            if (otherFileIds == null)
            {
                return null;
            }

            var sources = new List<MergeSource>();
            var newCounts = new List<int>();
            bool anyFromOther = false;
            int replicateCount = Math.Max(fileIds.ReplicatePositions.ReplicateCount,
                otherFileIds.ReplicatePositions.ReplicateCount);
            for (int replicateIndex = 0; replicateIndex < replicateCount; replicateIndex++)
            {
                int count = 0;
                foreach (var position in fileIds.ReplicatePositions[replicateIndex])
                {
                    int otherPosition = otherFileIds.IndexOfFile(replicateIndex, fileIds.FileIds[position].Value);
                    if (otherPosition >= 0 && otherIsUserSet(otherPosition))
                    {
                        sources.Add(new MergeSource(true, replicateIndex, otherPosition));
                        anyFromOther = true;
                    }
                    else
                    {
                        sources.Add(new MergeSource(false, replicateIndex, position));
                    }

                    count++;
                }

                // A peak the other results have for a file these have none for.
                foreach (var otherPosition in otherFileIds.ReplicatePositions[replicateIndex])
                {
                    if (fileIds.IndexOfFile(replicateIndex, otherFileIds.FileIds[otherPosition].Value) >= 0)
                    {
                        continue;
                    }

                    sources.Add(new MergeSource(true, replicateIndex, otherPosition));
                    anyFromOther = true;
                    count++;
                }

                newCounts.Add(count);
            }

            if (!anyFromOther)
            {
                return null;
            }

            counts = newCounts;
            return sources;
        }
    }

    /// <summary>
    /// Removing annotations from the columnar results, which is where they live now: a map of them
    /// on the precursor, and one on each of its transitions.
    /// </summary>
    public static class StripAnnotations
    {
        /// <summary>
        /// The annotations with every name not in <paramref name="annotationNamesToKeep"/> removed,
        /// or the same list when there was nothing to remove, so that a document which does not
        /// change stays reference equal.
        /// </summary>
        public static ImmutableList<Annotations> FromAnnotations(ICollection<string> annotationNamesToKeep,
            ImmutableList<Annotations> annotationsList)
        {
            if (annotationsList == null)
            {
                return null;
            }

            List<Annotations> newAnnotationsList = null;
            for (int i = 0; i < annotationsList.Count; i++)
            {
                var annotations = annotationsList[i];
                if (!Strip(annotationNamesToKeep, ref annotations))
                {
                    newAnnotationsList?.Add(annotationsList[i]);
                    continue;
                }

                if (newAnnotationsList == null)
                {
                    newAnnotationsList = new List<Annotations>(annotationsList.Take(i));
                }

                newAnnotationsList.Add(annotations);
            }

            return newAnnotationsList == null ? annotationsList : ImmutableList.ValueOf(newAnnotationsList);
        }

        private static bool Strip(ICollection<string> annotationNamesToKeep, ref Annotations annotations)
        {
            bool stripped = false;
            foreach (var entry in annotations.ListAnnotations())
            {
                if (annotationNamesToKeep.Contains(entry.Key))
                {
                    continue;
                }

                annotations = annotations.ChangeAnnotation(entry.Key, null);
                stripped = true;
            }

            return stripped;
        }
    }

    /// <summary>
    /// One transition peak, as quantification sees it. Built from
    /// <see cref="TransitionResults"/> rather than from a <see cref="TransitionChromInfo"/>, so
    /// that quantifying a document reads no chromatograms.
    /// </summary>
    public class QuantifiablePeak
    {
        public QuantifiablePeak(ChromFileInfoId fileId, float area, bool? isTruncated, bool isEmpty)
        {
            FileId = fileId;
            Area = area;
            IsTruncated = isTruncated;
            IsEmpty = isEmpty;
        }

        public ChromFileInfoId FileId { get; }
        public float Area { get; }
        public bool? IsTruncated { get; }

        /// <summary>
        /// No peak at all, as opposed to a peak whose area is zero. Quantification counts the
        /// first as missing and the second as measured.
        /// </summary>
        public bool IsEmpty { get; }
    }

    /// <summary>
    /// The boundaries one transition's peak was integrated between, when they are not the ones the
    /// rest of the precursor's transitions used.
    /// <para>
    /// Nearly every transition of a peak group was integrated between the same two times, which are
    /// the precursor's own and live on its <see cref="PrecursorPeak"/>. Only a transition whose peak
    /// the user moved on its own needs one of these, so
    /// <see cref="TransitionGroupResults.FindTransitionCustomPeakBounds"/> gives back null for
    /// almost every peak there is.
    /// </para>
    /// </summary>
    public struct CustomPeakBounds
    {
        public CustomPeakBounds(float startTime, float endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public float StartTime { get; private set; }
        public float EndTime { get; private set; }

        public bool Equals(CustomPeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime);
        }

        public override bool Equals(object obj)
        {
            return obj is CustomPeakBounds other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartTime.GetHashCode() * 397) ^ EndTime.GetHashCode();
            }
        }
    }

    /// <summary>
    /// What one transition peak keeps because integrating a chromatogram between its boundaries
    /// again cannot find it.
    /// <para>
    /// These are normally read straight off the <see cref="ChromPeak"/> in the .skyd file. A peak
    /// whose <see cref="PrecursorPeak.ChosenPeakIndex"/> is
    /// <see cref="PrecursorPeak.NO_PEAK_INDEX"/> is not one of the candidate peaks there, so there
    /// is no ChromPeak to read, and what integrating cannot work out for itself has to be stored.
    /// </para>
    /// <para>
    /// Only these two: everything else about such a peak - the area, the height, the background,
    /// the peak shape - comes out of integrating between the boundaries again, and is worth
    /// recomputing rather than storing.
    /// </para>
    /// </summary>
    public class CustomPeakMetrics : Immutable
    {
        /// <summary>
        /// One of these, or null when there is nothing to keep, which is what a peak with no mass
        /// error and no identification would be storing an object for.
        /// </summary>
        public static CustomPeakMetrics Create(float? massError, PeakIdentification identified)
        {
            if (!massError.HasValue && identified == PeakIdentification.FALSE)
            {
                return null;
            }

            return new CustomPeakMetrics().ChangeMassError(massError).ChangeIdentified(identified);
        }

        /// <summary>
        /// How far off the expected m/z the peak was, weighted by intensity. Integrating again
        /// could work this out from the mass errors in the chromatogram, but only when the .skyd
        /// has them, so the value the peak was given keeps.
        /// </summary>
        public float? MassError { get; private set; }

        /// <summary>
        /// Whether the peak contains an identification, which is not a property of the boundaries
        /// and so cannot be found by integrating between them.
        /// </summary>
        public PeakIdentification Identified { get; private set; }

        public CustomPeakMetrics ChangeMassError(float? value)
        {
            return ChangeProp(ImClone(this), im => im.MassError = value);
        }

        public CustomPeakMetrics ChangeIdentified(PeakIdentification value)
        {
            return ChangeProp(ImClone(this), im => im.Identified = value);
        }

        protected bool Equals(CustomPeakMetrics other)
        {
            return Nullable.Equals(MassError, other.MassError) && Identified == other.Identified;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj.GetType() == GetType() && Equals((CustomPeakMetrics) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (MassError.GetHashCode() * 397) ^ (int) Identified;
            }
        }
    }
}
