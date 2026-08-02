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
        /// one has boundaries the user set, and so a <see cref="CustomPeak"/> of its own.
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
        /// time. The peak index lists stay null, because which candidate peak was chosen is
        /// only knowable by reading the .skyd.
        /// <para>
        /// Only optimization step zero is stored. Nothing here can differ between the steps of
        /// one file: the user cannot set peak boundaries or annotations for one step on its own,
        /// and everything else is read back from the .skyd, which has every step.
        /// </para>
        /// </summary>
        public static TransitionGroupResults FromChromInfos(Results<TransitionGroupChromInfo> results)
        {
            return FromChromInfos(results, null);
        }

        /// <summary>
        /// <paramref name="getChosenPeakIndex"/> says which of the candidate peaks in the .skyd the
        /// peak of one replicate and file is. Only a caller which has the chromatograms can know
        /// that, so it is null when the columnar form is being derived from a document which
        /// already holds its chrom infos.
        /// </summary>
        public static TransitionGroupResults FromChromInfos(Results<TransitionGroupChromInfo> results,
            Func<int, ChromFileInfoId, int> getChosenPeakIndex)
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
                        getChosenPeakIndex?.Invoke(replicateIndex, chromInfo.FileId) ??
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
                    .ChangeAnnotations(annotations);

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

        private TransitionGroupResults ChangeTransitions(IEnumerable<TransitionResults> value)
        {
            var transitions = ImmutableList.ValueOf(value);
            if (transitions != null && transitions.All(results => results == null))
            {
                transitions = null;
            }

            return ChangeProp(ImClone(this), im => im.Transitions = transitions);
        }

        /// <summary>
        /// The results of one transition, or null when it has none. Takes the index of the
        /// transition among the precursor's children, which is the only thing these are in the
        /// order of.
        /// </summary>
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
        private TransitionGroupResults ChangeTransitionResults(int transitionIndex, TransitionResults value)
        {
            if (Equals(GetTransitionResults(transitionIndex), value))
            {
                return this;
            }

            int count = Math.Max(Transitions?.Count ?? 0, transitionIndex + 1);
            return ChangeTransitions(Enumerable.Range(0, count)
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
        /// These results with their transitions put in a new order, where
        /// <paramref name="oldIndexes"/> gives the index each transition used to be at, or -1 for
        /// one which was not there before. See
        /// <see cref="TransitionGroupDocNode.OnChangingChildren"/>, which is the only thing that
        /// knows how a precursor's transitions moved.
        /// </summary>
        public TransitionGroupResults ReorderTransitions(IList<int> oldIndexes)
        {
            if (Transitions == null)
            {
                return this;
            }

            return ChangeTransitions(oldIndexes.Select(GetTransitionResults));
        }

        /// <summary>
        /// Whether one of the precursor's transitions has any results.
        /// </summary>
        public bool HasTransitionResults(int transitionIndex)
        {
            return GetTransitionResults(transitionIndex) != null;
        }

        /// <summary>
        /// The files and replicate layout of one transition's results, or null when it has none.
        /// <para>
        /// These are the transition's own, which are not the precursor's: a transition can be
        /// missing from a file the precursor was found in.
        /// </para>
        /// </summary>
        public ChromFileIds GetTransitionChromFileIds(int transitionIndex)
        {
            return GetTransitionResults(transitionIndex)?.ChromFileIds;
        }

        /// <summary>
        /// The flat positions of one transition's results belonging to one replicate. Empty when
        /// the transition has no results at all.
        /// </summary>
        public IEnumerable<int> GetTransitionPositions(int transitionIndex, int replicateIndex)
        {
            var results = GetTransitionResults(transitionIndex);
            return results == null ? Array.Empty<int>() : results.GetPositions(replicateIndex);
        }

        /// <summary>
        /// The peak area of one transition at one of its positions. See
        /// <see cref="GetTransitionPositions"/> for where a position comes from: one of these
        /// means nothing anywhere but in the transition it came from.
        /// </summary>
        public float GetTransitionArea(int transitionIndex, int position)
        {
            return GetTransitionResults(transitionIndex).Peaks.FlatValues[position].Area;
        }

        public UserSet GetTransitionUserSet(int transitionIndex, int position)
        {
            return GetTransitionResults(transitionIndex).GetUserSet(position);
        }

        public CustomPeak GetTransitionCustomPeak(int transitionIndex, int position)
        {
            return GetTransitionResults(transitionIndex).GetCustomPeak(position);
        }

        public PeakIdentification GetTransitionIdentified(int transitionIndex, int position)
        {
            return GetTransitionResults(transitionIndex).GetIdentified(position);
        }

        /// <summary>
        /// Whether one transition's peak counts towards the peak count ratio. See
        /// <see cref="TransitionResults.IsGoodPeak"/>.
        /// </summary>
        public bool IsGoodTransitionPeak(int transitionIndex, int position, bool integrateAll)
        {
            return GetTransitionResults(transitionIndex).IsGoodPeak(position, integrateAll);
        }

        /// <summary>
        /// What quantification needs to know about one transition's peaks in one replicate. Empty
        /// when the transition has no results. See
        /// <see cref="TransitionResults.GetQuantifiablePeaks"/>.
        /// </summary>
        public IEnumerable<QuantifiablePeak> GetQuantifiablePeaks(int transitionIndex, int replicateIndex)
        {
            var results = GetTransitionResults(transitionIndex);
            return results == null
                ? Array.Empty<QuantifiablePeak>()
                : results.GetQuantifiablePeaks(replicateIndex);
        }

        /// <summary>
        /// One transition's peak in one file of one replicate, found by file rather than by
        /// position, which is how a caller which has neither the transition's positions nor a
        /// reason to learn them asks.
        /// </summary>
        public bool TryGetTransitionPeak(int transitionIndex, int replicateIndex, ChromFileInfoId fileId,
            out TransitionPeak peak)
        {
            var results = GetTransitionResults(transitionIndex);
            if (results == null)
            {
                peak = default;
                return false;
            }

            return results.Peaks.TryGetValue(replicateIndex, fileId, out peak);
        }

        /// <summary>
        /// One transition's custom peak in one file of one replicate, or null when it has none
        /// there - which is nearly every peak. See <see cref="TryGetTransitionPeak"/>.
        /// </summary>
        public CustomPeak FindTransitionCustomPeak(int transitionIndex, int replicateIndex, ChromFileInfoId fileId)
        {
            var customPeaks = GetTransitionResults(transitionIndex)?.CustomPeaks;
            if (customPeaks == null || !customPeaks.TryGetValue(replicateIndex, fileId, out var customPeak))
            {
                return null;
            }

            return customPeak;
        }

        /// <summary>
        /// Whether one transition's results are still carrying chrom infos which have not been
        /// worked out from the .skyd. False when it has no results at all, which is nothing to
        /// convert either way.
        /// </summary>
        public bool IsTransitionConverted(int transitionIndex)
        {
            return GetTransitionResults(transitionIndex)?.IsConverted != false;
        }

        /// <summary>
        /// One transition's unconverted chrom infos for a single replicate, or null when it has
        /// none there. Optimization step zero only, which is all that is kept.
        /// <para>
        /// This is how a results pass which is not going to read the .skyd - because nothing it
        /// cares about changed, or because the chromatograms are not loaded yet - gets back what
        /// the document already knows, without a transition node having to keep a second copy.
        /// </para>
        /// </summary>
        public IList<TransitionChromInfo> GetTransitionLegacyChromInfos(int transitionIndex, int replicateIndex)
        {
            var legacyChromInfos = GetTransitionResults(transitionIndex)?.LegacyChromInfos;
            if (legacyChromInfos == null || replicateIndex < 0 ||
                replicateIndex >= legacyChromInfos.ReplicatePositions.ReplicateCount)
            {
                return null;
            }

            var chromInfos = legacyChromInfos.Values[replicateIndex].ToList();
            return chromInfos.Count == 0 ? null : chromInfos;
        }

        /// <summary>
        /// How many unconverted chrom infos one transition's results are still carrying, which is
        /// one per file. Zero once they have been worked out.
        /// </summary>
        public int GetTransitionLegacyChromInfoCount(int transitionIndex)
        {
            return GetTransitionResults(transitionIndex)?.LegacyChromInfos?.FlatValues.Count ?? 0;
        }

        /// <summary>
        /// One transition's chrom info for a file which has not been converted yet, or null. See
        /// <see cref="TransitionResults.FindChromInfo"/>.
        /// </summary>
        public TransitionChromInfo FindTransitionChromInfo(int transitionIndex, int replicateIndex,
            ChromFileInfoId fileId)
        {
            return GetTransitionResults(transitionIndex)?.FindChromInfo(replicateIndex, fileId);
        }

        /// <summary>
        /// Records the boundaries of one transition's peak in one file. See
        /// <see cref="TransitionResults.ChangeCustomPeakBounds"/>.
        /// </summary>
        public TransitionGroupResults ChangeTransitionCustomPeakBounds(int transitionIndex, ChromFileInfoId fileId,
            float startTime, float endTime, PeakIdentification identified)
        {
            var results = GetTransitionResults(transitionIndex);
            if (results == null)
            {
                return this;
            }

            return ChangeTransitionResults(transitionIndex,
                results.ChangeCustomPeakBounds(fileId, startTime, endTime, identified));
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
        public bool IsTransitionCoveredBySharedAreas(int transitionIndex,
            ICollection<ReferenceValue<ChromFileInfoId>> sharedAreaFiles)
        {
            var results = GetTransitionResults(transitionIndex);
            if (sharedAreaFiles == null || results == null)
            {
                return false;
            }

            var fileIds = results.ChromFileIds.FileIds;
            return fileIds.Count == sharedAreaFiles.Count && fileIds.All(sharedAreaFiles.Contains);
        }

        /// <summary>
        /// How many entries one transition's results have, which is how many positions there are
        /// to walk. Zero when it has none.
        /// </summary>
        public int GetTransitionPositionCount(int transitionIndex)
        {
            return GetTransitionResults(transitionIndex)?.Peaks.FlatValues.Count ?? 0;
        }

        /// <summary>
        /// One entry per position of one transition's results, null where the peak there has
        /// nothing which cannot be derived from the .skyd - which is nearly every one.
        /// </summary>
        public IEnumerable<CustomPeak> GetTransitionCustomPeaks(int transitionIndex)
        {
            var results = GetTransitionResults(transitionIndex);
            return results?.CustomPeaks?.FlatValues ??
                   Enumerable.Repeat((CustomPeak) null, GetTransitionPositionCount(transitionIndex));
        }

        public TransitionGroupResults ChangeTransitionCustomPeaks(int transitionIndex,
            IEnumerable<CustomPeak> customPeaks)
        {
            var results = GetTransitionResults(transitionIndex);
            if (results == null)
            {
                return this;
            }

            return ChangeTransitionResults(transitionIndex, results.ChangeCustomPeaks(customPeaks));
        }

        /// <summary>
        /// These results with one transition's built from chrom infos, keeping the chrom infos
        /// themselves until which candidate peak each peak is has been worked out. This is what
        /// reading a document written the old way does.
        /// </summary>
        public TransitionGroupResults ChangeTransitionFromChromInfos(int transitionIndex,
            Results<TransitionChromInfo> chromInfos)
        {
            return ChangeTransitionResults(transitionIndex, TransitionResults.FromChromInfos(chromInfos));
        }

        /// <summary>
        /// These results with one transition's built from the columnar values a document was
        /// written with. <paramref name="customPeaks"/> may be null, which is what nearly every
        /// transition has.
        /// </summary>
        public TransitionGroupResults ChangeTransitionResults(int transitionIndex, ChromFileIds chromFileIds,
            IEnumerable<TransitionPeak> peaks, IEnumerable<CustomPeak> customPeaks)
        {
            var results = new TransitionResults(chromFileIds, peaks);
            if (customPeaks != null)
            {
                results = results.ChangeCustomPeaks(customPeaks);
            }

            return ChangeTransitionResults(transitionIndex, results);
        }

        /// <summary>
        /// These results with one transition's worked out again from the chrom infos a results
        /// calculation produced. Returns this when the transition already says the same, which is
        /// what keeps a pass that changed nothing from making the whole molecule convert again.
        /// <para>
        /// The two are never equal outright: what is already there has been converted, having had
        /// its candidate peaks worked out, while what comes out of the chrom infos still carries
        /// them. Replacing one with the other would make every pass read all of the molecule's
        /// chromatograms, which is enough to make loading a large document look like a hang.
        /// </para>
        /// </summary>
        public TransitionGroupResults UpdateTransitionFromChromInfos(int transitionIndex,
            Results<TransitionChromInfo> chromInfos)
        {
            var calculated = TransitionResults.FromChromInfos(chromInfos);

            // Only when this pass actually worked something out. A pass which read no chromatogram
            // - because none is loaded yet - has nothing to say, and must not replace what a
            // document was read with.
            if (!(calculated?.Peaks.FlatValues.Count > 0))
            {
                return this;
            }

            var existing = GetTransitionResults(transitionIndex);
            if (existing != null && existing.IsConverted &&
                Equals(existing, calculated.ChangeLegacyChromInfos(null)))
            {
                return this;
            }

            return ChangeTransitionResults(transitionIndex, calculated);
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
        /// These results with the peak boundaries of one file dropped from every transition. A
        /// peak the user set keeps its boundaries whether or not they match a candidate peak, and
        /// when they do the index says the same thing for nothing.
        /// </summary>
        public TransitionGroupResults DropTransitionPeakBounds(ChromFileInfoId fileId)
        {
            var results = this;
            for (int iTran = 0; iTran < TransitionCount; iTran++)
            {
                var transitionResults = results.GetTransitionResults(iTran);
                var newResults = transitionResults?.DropCustomPeakBounds(fileId);
                if (newResults != null && !ReferenceEquals(newResults, transitionResults))
                {
                    results = results.ChangeTransitionResults(iTran, newResults);
                }
            }

            return results;
        }

        /// <summary>
        /// These results with every transition's unconverted chrom infos let go, which is what
        /// there is to do once every one of the precursor's files has been read.
        /// </summary>
        public TransitionGroupResults ClearTransitionLegacyChromInfos()
        {
            if (Transitions == null)
            {
                return this;
            }

            return ChangeTransitions(Transitions.Select(results => results?.ChangeLegacyChromInfos(null)));
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
        /// .skyd file. Held as one value per position rather than in <see cref="CustomPeak"/>
        /// because a scored document has one for nearly every position.
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
        /// A precursor peak has nothing else which cannot be derived from the .skyd: the boundaries
        /// a user set live on the transitions, where <see cref="TransitionResults.CustomPeaks"/>
        /// keeps them, and everything else is worked out from the chromatogram.
        /// <para>
        /// Stored through <see cref="ImmutableListFactory.MaybeConstant{T}"/>, so a document with no
        /// precursor annotations - almost every document - pays for one entry rather than one for
        /// every position.
        /// </para>
        /// </summary>
        public ChromFileIdMap<Annotations> Annotations { get; private set; }

        /// <summary>
        /// The chrom infos which have not been worked out from the .skyd file yet. Null once they
        /// have been. The precursor level counterpart of <see cref="TransitionResults.LegacyChromInfos"/>,
        /// and kept as a <see cref="Results{TItem}"/> rather than flattened because that is the shape
        /// every reader of <see cref="TransitionGroupDocNode.Results"/> still expects: while these
        /// are here, the node can hand them straight back.
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
                    results = results.ChangeTransitions(newTransitions);
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
        /// different one has boundaries the user set, and so a <see cref="CustomPeak"/> of its own.
        /// <para>
        /// A negative index reads back as null rather than as "no candidate peak". The paths which
        /// put results on a node without looking at any chromatogram cannot know an index, and this
        /// is what they leave behind. A peak which really is not one of the candidate peaks is the
        /// user's, and says so by having a <see cref="CustomPeak"/> with boundaries.
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
            /// Builds the columnar form from the chrom infos a document already holds, keeping the
            /// chrom infos themselves in <see cref="LegacyChromInfos"/>. See
            /// <see cref="TransitionGroupResults.FromChromInfos"/>.
            /// </summary>
            public static TransitionResults FromChromInfos(Results<TransitionChromInfo> results)
            {
                return FromChromInfos(results, true);
            }

            /// <summary>
            /// <paramref name="keepChromInfos"/> false means the caller knows which candidate peak each
            /// peak is, so the chrom infos can be rebuilt from the .skyd and none of them needs to be
            /// kept. Only a caller with the chromatograms can know that, which is why the plain
            /// overload keeps them.
            /// </summary>
            public static TransitionResults FromChromInfos(Results<TransitionChromInfo> results, bool keepChromInfos)
            {
                if (results == null)
                {
                    return null;
                }

                var fileIds = new List<ChromFileInfoId>();
                var counts = new List<int>();
                var peaks = new List<TransitionPeak>();
                var chromInfos = keepChromInfos ? new List<TransitionChromInfo>() : null;
                var customPeaks = new List<CustomPeak>();
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

                        chromInfos?.Add(chromInfo);
                        customPeaks.Add(MakeCustomPeak(chromInfo));
                        fileIds.Add(chromInfo.FileId);
                        peaks.Add(new TransitionPeak(chromInfo.Area, chromInfo.UserSet, chromInfo.IsTruncated,
                            chromInfo.IsEmpty, chromInfo.Identified, chromInfo.IsForcedIntegration));
                        count++;
                    }

                    counts.Add(count);
                }

                var transitionResults =
                    new TransitionResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), peaks);
                // Null rather than a list of nothing but nulls, which is what nearly every document has.
                if (customPeaks.Any(customPeak => customPeak != null))
                {
                    transitionResults = transitionResults.ChangeCustomPeaks(customPeaks);
                }

                return chromInfos == null ? transitionResults : transitionResults.ChangeLegacyChromInfos(chromInfos);

            }

            /// <summary>
            /// The entry for one position, or null when it has nothing which cannot be derived from the
            /// .skyd.
            /// <para>
            /// A peak the user set is not one of the candidate peaks Skyline found, so its boundaries
            /// have to be kept: everything else about it is recovered by integrating the chromatogram
            /// again between them. A peak Skyline chose is one of the candidate peaks, and is found
            /// again by its area.
            /// </para>
            /// </summary>
            private static CustomPeak MakeCustomPeak(TransitionChromInfo chromInfo)
            {
                bool hasAnnotations = chromInfo.Annotations != null && !chromInfo.Annotations.IsEmpty;
                bool isUserSet = chromInfo.UserSet != UserSet.FALSE && !chromInfo.IsEmpty;
                if (!hasAnnotations && !isUserSet)
                {
                    return null;
                }

                var customPeak = new CustomPeak();
                if (hasAnnotations)
                {
                    customPeak = customPeak.ChangeAnnotations(chromInfo.Annotations);
                }

                if (isUserSet)
                {
                    customPeak = customPeak.ChangePeakBounds(chromInfo.StartRetentionTime, chromInfo.EndRetentionTime,
                        chromInfo.Identified);
                }

                return customPeak;
            }

            public TransitionResults(ChromFileIds chromFileIds, IEnumerable<TransitionPeak> peaks)
            {
                Peaks = new ChromFileIdMap<TransitionPeak>(chromFileIds, peaks);
            }

            /// <summary>
            /// Everything every peak has: its area, and the handful of flags quantification and the
            /// peak count ratio ask for over the whole document. One map of a struct rather than a map
            /// per value, and the map the positions come from since it is always there.
            /// </summary>
            public ChromFileIdMap<TransitionPeak> Peaks { get; private set; }

            public ChromFileIds ChromFileIds
            {
                get { return Peaks.ChromFileIds; }
            }

            /// <summary>
            /// One entry per position, null where a peak has nothing which cannot be derived from the
            /// .skyd file. Null altogether when no position has anything, which is nearly every
            /// document.
            /// </summary>
            public ChromFileIdMap<CustomPeak> CustomPeaks { get; private set; }

            /// <summary>
            /// The chrom infos which have not been worked out from the .skyd file yet, each knowing its
            /// own file and optimization step. Null once they have been.
            /// <para>
            /// A document read from a file arrives with these, because which candidate peak each peak
            /// is cannot be told without the chromatograms, and until that is known nothing here can be
            /// rebuilt. Loading the chromatogram cache is what gets rid of them: see
            /// <see cref="TransitionGroupDocNode.UpdateResults"/>, which works out
            /// <see cref="TransitionGroupResults.ChosenPeakIndexes"/> and then has no need of them.
            /// </para>
            /// <para>
            /// So this is the whole of what the columnar form costs before conversion, and nothing
            /// while it is converted. It is the reason a document can be read at all before its .skyd
            /// is available.
            /// </para>
            /// </summary>
            /// <para>
            /// Over the same positions as <see cref="Peaks"/>, since both are optimization step
            /// zero of each file, so a chrom info is found by replicate and file rather than by
            /// walking the list.
            /// </para>
            /// </summary>
            public ChromFileIdMap<TransitionChromInfo> LegacyChromInfos { get; private set; }

            public bool IsConverted
            {
                get { return LegacyChromInfos == null; }
            }

            public TransitionResults ChangeLegacyChromInfos(IEnumerable<TransitionChromInfo> value)
            {
                return ChangeProp(ImClone(this), im => im.LegacyChromInfos =
                    value == null ? null : new ChromFileIdMap<TransitionChromInfo>(ChromFileIds, value));
            }

            /// <summary>
            /// The chrom info of one file which has not been converted, or null. Optimization step
            /// zero, which is the only one kept: the rest are read back from the .skyd with it.
            /// </summary>
            public TransitionChromInfo FindChromInfo(int replicateIndex, ChromFileInfoId fileId)
            {
                if (LegacyChromInfos == null ||
                    !LegacyChromInfos.TryGetValue(replicateIndex, fileId, out var chromInfo))
                {
                    return null;
                }

                return chromInfo;
            }

            public TransitionResults ChangePeaks(IEnumerable<TransitionPeak> value)
            {
                return ChangeProp(ImClone(this),
                    im => im.Peaks = new ChromFileIdMap<TransitionPeak>(ChromFileIds, value));
            }

            /// <summary>
            /// Whether the peak at one position ran off the end of the chromatogram, or null when
            /// nothing worked that out. See <see cref="Truncated"/>.
            /// </summary>
            public bool? GetTruncated(int position)
            {
                return Peaks.FlatValues[position].IsTruncated;
            }

            /// <summary>
            /// Whether there is no peak at one position at all. See <see cref="EmptyPeaks"/>.
            /// </summary>
            public bool IsEmptyPeak(int position)
            {
                return Peaks.FlatValues[position].IsEmpty;
            }

            /// <summary>
            /// Whether the peak at one position contains an identification. See
            /// <see cref="Identified"/>.
            /// </summary>
            public PeakIdentification GetIdentified(int position)
            {
                return Peaks.FlatValues[position].Identified;
            }

            /// <summary>
            /// Whether the peak at one position counts towards the peak count ratio, which is what
            /// <see cref="TransitionChromInfo.IsGoodPeak"/> decides. Everything it looks at is stored,
            /// so this needs no chromatogram.
            /// </summary>
            public bool IsGoodPeak(int position, bool integrateAll)
            {
                return Peaks.FlatValues[position].IsGoodPeak(integrateAll);
            }

            /// <summary>
            /// What quantification needs to know about the peaks of one replicate, in position order.
            /// <para>
            /// This is deliberately everything quantification needs and nothing else, so that it can
            /// run over a whole document without a chromatogram being read. Only optimization step zero
            /// is here, which is the step quantification uses.
            /// </para>
            /// </summary>
            public IEnumerable<QuantifiablePeak> GetQuantifiablePeaks(int replicateIndex)
            {
                foreach (int position in ChromFileIds.ReplicatePositions[replicateIndex])
                {
                    var peak = Peaks.FlatValues[position];
                    yield return new QuantifiablePeak(ChromFileIds.FileIds[position].Value, peak.Area,
                        peak.IsTruncated, peak.IsEmpty);
                }
            }

            public TransitionResults ChangeCustomPeaks(IEnumerable<CustomPeak> value)
            {
                return ChangeProp(ImClone(this), im => im.CustomPeaks = value == null ? null : new ChromFileIdMap<CustomPeak>(ChromFileIds, value));
            }

            /// <summary>
            /// See <see cref="TransitionGroupResults.StripAnnotationValues"/>.
            /// </summary>
            public TransitionResults StripAnnotationValues(ICollection<string> annotationNamesToKeep)
            {
                var newCustomPeaks = StripAnnotations.FromCustomPeaks(annotationNamesToKeep, CustomPeaks?.FlatValues);
                if (ReferenceEquals(newCustomPeaks, CustomPeaks?.FlatValues))
                    return this;
                return ChangeCustomPeaks(newCustomPeaks);
            }

            /// <summary>
            /// Records the boundaries of the peak at one position, keeping whatever else is already
            /// known about it. Used when the peak turns out not to be one of the candidate peaks, and
            /// so can only be got back by integrating between its boundaries.
            /// </summary>
            public TransitionResults ChangeCustomPeakBounds(int position, float startTime, float endTime,
                PeakIdentification identified)
            {
                var newCustomPeak = (GetCustomPeak(position) ?? new CustomPeak())
                    .ChangePeakBounds(startTime, endTime, identified);
                return ChangeCustomPeaks(
                    CustomPeak.SetAtPosition(CustomPeaks?.FlatValues, Peaks.FlatValues.Count, position, newCustomPeak));
            }

            /// <summary>
            /// The position of one file's entry in one replicate, or -1. See
            /// the positions of these results, which is where it means something.
            /// </summary>
            private int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
            {
                return ChromFileIds.IndexOfFile(replicateIndex, fileId);
            }

            /// <summary>
            /// Records the boundaries of one file's peak, found by file. A file belongs to one
            /// replicate, so it says which peak it is on its own, and the caller never holds a position
            /// of these results.
            /// </summary>
            public TransitionResults ChangeCustomPeakBounds(ChromFileInfoId fileId, float startTime, float endTime,
                PeakIdentification identified)
            {
                int position = ChromFileIds.IndexOfFile(fileId);
                return position < 0 ? this : ChangeCustomPeakBounds(position, startTime, endTime, identified);
            }

            /// <summary>
            /// These results with the boundaries of one file's peak dropped, keeping whatever else
            /// the custom peak had. Used once the peak turns out to be one of the candidate peaks
            /// after all, when the index reproduces it and the boundaries say the same thing again.
            /// </summary>
            public TransitionResults DropCustomPeakBounds(ChromFileInfoId fileId)
            {
                int position = ChromFileIds.IndexOfFile(fileId);
                if (position < 0)
                {
                    return this;
                }

                var customPeak = GetCustomPeak(position);
                if (customPeak?.HasPeakBounds != true)
                {
                    return this;
                }

                var newCustomPeak = customPeak.ChangePeakBounds(null, null, PeakIdentification.FALSE);
                return ChangeCustomPeaks(CustomPeak.SetAtPosition(CustomPeaks?.FlatValues,
                    Peaks.FlatValues.Count, position, newCustomPeak.IsEmpty ? null : newCustomPeak));
            }

            /// <summary>
            /// Whether one file of one replicate has a peak here, and if so its area and whether
            /// anything about it was the user's. Answered together so that a caller working across
            /// objects - a precursor's positions are not its transitions' - never has to hold a
            /// position of this one.
            /// </summary>
            public bool TryGetPlainArea(int replicateIndex, ChromFileInfoId fileId, out float area)
            {
                area = 0;
                int position = ChromFileIds.IndexOfFile(replicateIndex, fileId);
                if (position < 0 || GetUserSet(position) != UserSet.FALSE || GetCustomPeak(position) != null)
                {
                    return false;
                }

                area = Peaks.FlatValues[position].Area;
                return true;
            }

            public CustomPeak GetCustomPeak(int position)
            {
                return CustomPeaks?.FlatValues[position];
            }

            public UserSet GetUserSet(int position)
            {
                return Peaks.FlatValues[position].UserSet;
            }

            /// <summary>
            /// The transition level counterpart of
            /// <see cref="TransitionGroupResults.MergeUserInfo"/>, worked out the same way.
            /// </summary>
            public TransitionResults MergeUserInfo(TransitionResults other)
            {
                var sources = MergeSource.Build(ChromFileIds, other?.ChromFileIds,
                    position => other.GetUserSet(position) != UserSet.FALSE, out var counts);
                if (sources == null)
                {
                    return this;
                }

                var results = new TransitionResults(
                    new ChromFileIds(ReplicatePositions.FromCounts(counts),
                        sources.Select(source =>
                            source.Pick(ChromFileIds, other.ChromFileIds).FileIds[source.Position].Value)),
                    sources.Select(source => source.Pick(this, other).Peaks.FlatValues[source.Position]));
                var customPeaks = MergeSource.MergeCustomPeaks(sources,
                    source => source.Pick(this, other).GetCustomPeak(source.Position));
                if (customPeaks != null)
                {
                    results = results.ChangeCustomPeaks(customPeaks);
                }

                return results;
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
            /// Compared by value. See <see cref="TransitionGroupResults.Equals(TransitionGroupResults)"/>.
            /// </summary>
            protected bool Equals(TransitionResults other)
            {
                // No ChromFileIds of its own: every map carries it, and Peaks is always there.
                return Equals(Peaks, other.Peaks) &&
                       Equals(CustomPeaks, other.CustomPeaks) &&
                       Equals(LegacyChromInfos, other.LegacyChromInfos);
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
                    result = (result * 397) ^ (CustomPeaks?.GetHashCode() ?? 0);
                    result = (result * 397) ^ (LegacyChromInfos?.GetHashCode() ?? 0);
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
        private MergeSource(bool fromOther, int position)
        {
            FromOther = fromOther;
            Position = position;
        }

        public bool FromOther { get; }
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
                        sources.Add(new MergeSource(true, otherPosition));
                        anyFromOther = true;
                    }
                    else
                    {
                        sources.Add(new MergeSource(false, position));
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

                    sources.Add(new MergeSource(true, otherPosition));
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

        /// <summary>
        /// The custom peaks of the merged results, one entry per merged position, or null when no
        /// position has one.
        /// </summary>
        public static ImmutableList<CustomPeak> MergeCustomPeaks(IList<MergeSource> sources,
            Func<MergeSource, CustomPeak> getCustomPeak)
        {
            var customPeaks = sources.Select(getCustomPeak).ToList();
            return customPeaks.All(customPeak => customPeak == null) ? null : ImmutableList.ValueOf(customPeaks);
        }
    }

    /// <summary>
    /// Removing annotations from the columnar results, which is where they live now. The
    /// annotations of a peak are on its <see cref="CustomPeak"/>, and a peak whose annotations all
    /// go and which has nothing else to say stops needing one at all.
    /// </summary>
    public static class StripAnnotations
    {
        /// <summary>
        /// The custom peaks with every annotation not in <paramref name="annotationNamesToKeep"/>
        /// removed, or the same list when there was nothing to remove, so that a document which
        /// does not change stays reference equal.
        /// </summary>
        public static ImmutableList<CustomPeak> FromCustomPeaks(ICollection<string> annotationNamesToKeep,
            ImmutableList<CustomPeak> customPeaks)
        {
            if (customPeaks == null)
            {
                return null;
            }

            List<CustomPeak> newCustomPeaks = null;
            for (int i = 0; i < customPeaks.Count; i++)
            {
                var customPeak = customPeaks[i];
                var annotations = customPeak?.Annotations ?? Model.Annotations.EMPTY;
                if (!Strip(annotationNamesToKeep, ref annotations))
                {
                    newCustomPeaks?.Add(customPeak);
                    continue;
                }

                if (newCustomPeaks == null)
                {
                    newCustomPeaks = new List<CustomPeak>(customPeaks.Take(i));
                }

                // A peak with no annotations left and no boundaries of its own has nothing which
                // cannot be read back from the .skyd, so it stops being a custom peak.
                var newCustomPeak = customPeak.ChangeAnnotations(annotations);
                newCustomPeaks.Add(newCustomPeak.IsEmpty ? null : newCustomPeak);
            }

            if (newCustomPeaks == null)
            {
                return customPeaks;
            }

            return newCustomPeaks.All(customPeak => customPeak == null)
                ? null
                : ImmutableList.ValueOf(newCustomPeaks);
        }

        /// <summary>
        /// The annotations with every name not in <paramref name="annotationNamesToKeep"/> removed,
        /// or the same list when there was nothing to remove. This is the precursor level
        /// counterpart of <see cref="FromCustomPeaks"/>, where the annotations are the whole of what
        /// a peak keeps.
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
    /// Everything about one transition peak which cannot be read back out of the .skyd file: the
    /// annotations, and the peak boundaries when the user chose them instead of accepting one of
    /// the candidate peaks that Skyline found.
    /// <para>
    /// A peak with none of that has no CustomPeak at all, and its entry in
    /// <see cref="TransitionResults.CustomPeaks"/> is null. That list has one entry per position, so
    /// a CustomPeak neither knows nor needs to know where it sits.
    /// </para>
    /// </summary>
    public class CustomPeak : Immutable
    {
        public CustomPeak()
        {
            Annotations = Annotations.EMPTY;
        }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set when the peak boundaries may not be those of a candidate peak, which is what
        /// happens when the user sets them. The values which depend on the boundaries get
        /// recalculated by integrating the chromatogram again between these times.
        /// </summary>
        public float? StartTime { get; private set; }
        public float? EndTime { get; private set; }

        /// <summary>
        /// Whether the peak contains an identification, which cannot be derived from the
        /// boundaries alone and so has to be kept alongside them.
        /// </summary>
        public PeakIdentification Identified { get; private set; }

        public bool HasPeakBounds
        {
            get { return StartTime.HasValue && EndTime.HasValue; }
        }

        public CustomPeak ChangeAnnotations(Annotations value)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = value ?? Annotations.EMPTY);
        }

        public CustomPeak ChangePeakBounds(float? startTime, float? endTime, PeakIdentification identified)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.StartTime = startTime;
                im.EndTime = endTime;
                im.Identified = identified;
            });
        }

        /// <summary>
        /// Whether this holds anything at all. One that does not is left out of the list, as a null.
        /// </summary>
        public bool IsEmpty
        {
            get { return Annotations.IsEmpty && !HasPeakBounds; }
        }

        /// <summary>
        /// The list of <paramref name="count"/> entries with the one at <paramref name="position"/>
        /// replaced. Null when no position has anything, which is the usual document, and which is
        /// why every caller has to be ready for a null list.
        /// </summary>
        public static ImmutableList<CustomPeak> SetAtPosition(ImmutableList<CustomPeak> customPeaks, int count,
            int position, CustomPeak customPeak)
        {
            if (customPeak?.IsEmpty != false && customPeaks == null)
            {
                return null;
            }

            var newCustomPeaks = customPeaks?.ToList() ?? Enumerable.Repeat((CustomPeak) null, count).ToList();
            newCustomPeaks[position] = customPeak?.IsEmpty == false ? customPeak : null;
            return newCustomPeaks.All(entry => entry == null) ? null : ImmutableList.ValueOf(newCustomPeaks);
        }

        protected bool Equals(CustomPeak other)
        {
            return Equals(Annotations, other.Annotations) &&
                   Nullable.Equals(StartTime, other.StartTime) && Nullable.Equals(EndTime, other.EndTime) &&
                   Identified == other.Identified;
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

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((CustomPeak)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Annotations.GetHashCode();
                result = (result * 397) ^ StartTime.GetHashCode();
                result = (result * 397) ^ EndTime.GetHashCode();
                result = (result * 397) ^ (int) Identified;
                return result;
            }
        }
    }
}
