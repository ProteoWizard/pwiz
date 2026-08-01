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
        /// The peak Skyline originally picked, and the peak reintegration chose. Both are
        /// kept for the parts which need to know where a peak came from rather than only
        /// where it is now - retention time alignment and peak imputation.
        /// <para>
        /// These two often hold the same indexes, so an incoming list which equals the other one
        /// is stored as that same instance rather than as a second copy. The chosen peak index
        /// used to share with them as well, before it moved into <see cref="PrecursorPeak"/>.
        /// </para>
        /// </summary>
        public ImmutableList<int> OriginalPeakIndexes { get; private set; }
        public ImmutableList<int> ReintegratedPeakIndexes { get; private set; }

        /// <summary>
        /// Almost always all <see cref="UserSet.FALSE"/>, which is why this gets stored
        /// through <see cref="ImmutableListFactory.MaybeConstant{T}"/>.
        /// </summary>
        public ImmutableList<UserSet> UserSets { get; private set; }

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
        public ImmutableList<float> QValues { get; private set; }
        public ImmutableList<float> ZScores { get; private set; }

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
        public ImmutableList<Annotations> Annotations { get; private set; }

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
                Peaks.Values.Select((peak, position) =>
                    peak.ChangeChosenPeakIndex(indexes == null ? PrecursorPeak.NO_PEAK_INDEX : indexes[position]))));
        }

        public TransitionGroupResults ChangeOriginalPeakIndexes(IEnumerable<int> value)
        {
            return ChangeProp(ImClone(this),
                im => im.OriginalPeakIndexes = im.ShareEqualIndexes(value?.ToImmutable()));
        }

        public TransitionGroupResults ChangeReintegratedPeakIndexes(IEnumerable<int> value)
        {
            return ChangeProp(ImClone(this),
                im => im.ReintegratedPeakIndexes = im.ShareEqualIndexes(value?.ToImmutable()));
        }

        /// <summary>
        /// Returns whichever of the peak index lists already here holds the same indexes as
        /// <paramref name="value"/>, so that the common case of the chosen, original and
        /// reintegrated peaks all being the same costs one list instead of three.
        /// <see cref="ImmutableList{T}"/> compares by contents, which is what makes this work.
        /// </summary>
        private ImmutableList<int> ShareEqualIndexes(ImmutableList<int> value)
        {
            if (value == null)
            {
                return null;
            }

            if (Equals(value, OriginalPeakIndexes))
            {
                return OriginalPeakIndexes;
            }

            if (Equals(value, ReintegratedPeakIndexes))
            {
                return ReintegratedPeakIndexes;
            }

            return value;
        }

        public TransitionGroupResults ChangeQValues(IEnumerable<float> value)
        {
            return ChangeProp(ImClone(this), im => im.QValues = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionGroupResults ChangeZScores(IEnumerable<float> value)
        {
            return ChangeProp(ImClone(this), im => im.ZScores = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionGroupResults ChangeUserSets(IEnumerable<UserSet> value)
        {
            return ChangeProp(ImClone(this), im => im.UserSets = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionGroupResults ChangeAnnotations(IEnumerable<Annotations> value)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = ImmutableList.ValueOf(value).MaybeConstant());
        }

        /// <summary>
        /// These results with every annotation not named removed. Returns this when there was
        /// nothing to remove, so an unchanged document stays reference equal.
        /// </summary>
        public TransitionGroupResults StripAnnotationValues(ICollection<string> annotationNamesToKeep)
        {
            var newAnnotations = StripAnnotations.FromAnnotations(annotationNamesToKeep, Annotations);
            if (ReferenceEquals(newAnnotations, Annotations))
                return this;
            return ChangeAnnotations(newAnnotations);
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
                sources.Select(source => source.Pick(this, other).Peaks.Values[source.Position]));
            if (UserSets != null || other.UserSets != null)
            {
                results = results.ChangeUserSets(
                    sources.Select(source => source.Pick(this, other).GetUserSet(source.Position)));
            }

            results = results
                .ChangeOriginalPeakIndexes(MergeIndexes(sources, other, r => r.OriginalPeakIndexes))
                .ChangeReintegratedPeakIndexes(MergeIndexes(sources, other, r => r.ReintegratedPeakIndexes));
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

        private IEnumerable<int> MergeIndexes(IList<MergeSource> sources, TransitionGroupResults other,
            Func<TransitionGroupResults, ImmutableList<int>> getIndexes)
        {
            if (getIndexes(this) == null && getIndexes(other) == null)
            {
                return null;
            }

            return sources.Select(source =>
            {
                var indexes = getIndexes(source.Pick(this, other));
                return indexes == null ? -1 : indexes[source.Position];
            });
        }

        /// <summary>
        /// The position of one file's entry in one replicate, or -1. Callers find a position this
        /// way rather than counting, since the entries of a replicate are in no order they can
        /// rely on.
        /// </summary>
        public int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
        {
            return ChromFileIds.IndexOfFile(replicateIndex, fileId);
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
            int chosenPeakIndex = Peaks.Values[position].ChosenPeakIndex;
            return chosenPeakIndex < 0 ? (int?) null : chosenPeakIndex;
        }

        public Annotations GetAnnotations(int position)
        {
            return Annotations == null ? Model.Annotations.EMPTY : Annotations[position];
        }

        public UserSet GetUserSet(int position)
        {
            return UserSets == null ? UserSet.FALSE : UserSets[position];
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
            return NullIfZero(Peaks.Values[position].RetentionTime);
        }

        public float? GetStartTime(int position)
        {
            return NullIfZero(Peaks.Values[position].StartTime);
        }

        public float? GetEndTime(int position)
        {
            return NullIfZero(Peaks.Values[position].EndTime);
        }

        private static float? NullIfZero(float time)
        {
            return time == 0 ? (float?) null : time;
        }

        public float? GetQValue(int position)
        {
            return GetScore(QValues, position);
        }

        public float? GetZScore(int position)
        {
            return GetScore(ZScores, position);
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
    public class TransitionResults : Immutable
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
            var areas = new List<float>();
            var userSets = new List<UserSet>();
            var truncated = new List<bool?>();
            var emptyPeaks = new List<bool>();
            var identified = new List<PeakIdentification>();
            var forcedIntegration = new List<bool>();
            var chromInfos = keepChromInfos ? new List<TransitionChromInfo>() : null;
            var customPeaks = new List<CustomPeak>();
            foreach (var chromInfoList in results)
            {
                int count = 0;
                foreach (var chromInfo in chromInfoList)
                {
                    // Every step is kept, because the ones which are not step zero can only be got
                    // back from the .skyd, and this is the state of not having looked at it yet.
                    chromInfos?.Add(chromInfo);
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }

                    customPeaks.Add(MakeCustomPeak(chromInfo));
                    fileIds.Add(chromInfo.FileId);
                    areas.Add(chromInfo.Area);
                    userSets.Add(chromInfo.UserSet);
                    truncated.Add(chromInfo.IsTruncated);
                    emptyPeaks.Add(chromInfo.IsEmpty);
                    identified.Add(chromInfo.Identified);
                    forcedIntegration.Add(chromInfo.IsForcedIntegration);
                    count++;
                }

                counts.Add(count);
            }

            var transitionResults =
                new TransitionResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), areas)
                    .ChangeUserSets(userSets)
                    .ChangeTruncated(truncated)
                    .ChangeEmptyPeaks(emptyPeaks)
                    .ChangeIdentified(identified)
                    .ChangeForcedIntegration(forcedIntegration);
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

        public TransitionResults(ChromFileIds chromFileIds, IEnumerable<float> areas)
        {
            Areas = new ChromFileIdMap<float>(chromFileIds, areas);
        }

        /// <summary>
        /// The area of each peak. Every value of a peak is its own map over the same
        /// <see cref="Results.ChromFileIds"/>, and this is the one which is always there, so it is
        /// also where the positions come from.
        /// </summary>
        public ChromFileIdMap<float> Areas { get; private set; }

        public ChromFileIds ChromFileIds
        {
            get { return Areas.ChromFileIds; }
        }

        /// <summary>
        /// A map over the same positions as <see cref="Areas"/>, with the values stored through
        /// <see cref="ImmutableListFactory.MaybeConstant{T}"/> so that a column saying the same
        /// thing everywhere costs one entry. Null values give a null map, which is what a column
        /// nothing has worked out looks like.
        /// </summary>
        private ChromFileIdMap<TValue> MakeMap<TValue>(IEnumerable<TValue> values)
        {
            return values == null
                ? null
                : new ChromFileIdMap<TValue>(ChromFileIds, ImmutableList.ValueOf(values).MaybeConstant());
        }

        /// <summary>
        /// Almost always all <see cref="UserSet.FALSE"/>, which is why the values get stored
        /// through <see cref="ImmutableListFactory.MaybeConstant{T}"/>.
        /// </summary>
        public ChromFileIdMap<UserSet> UserSets { get; private set; }

        /// <summary>
        /// Whether the peak at each position ran off the end of the chromatogram. Three states,
        /// as on <see cref="TransitionChromInfo.IsTruncated"/>: null means nothing was worked out.
        /// <para>
        /// Kept per position rather than only for the peaks whose boundaries the user set, unlike
        /// the other things which could be read back from the .skyd, because quantification asks
        /// for it over the whole document and must not have to read a chromatogram to get it.
        /// Nearly always uniform, so it collapses to a constant list.
        /// </para>
        /// </summary>
        public ChromFileIdMap<bool?> Truncated { get; private set; }

        /// <summary>
        /// Whether each position has no peak at all, which is not the same as a peak whose area is
        /// zero: quantification counts the first as missing and the second as measured. This is
        /// what <see cref="TransitionChromInfo.IsEmpty"/> says, and it cannot be told from
        /// <see cref="Areas"/>, which is zero either way.
        /// </summary>
        public ChromFileIdMap<bool> EmptyPeaks { get; private set; }

        /// <summary>
        /// Whether each peak contains an identification. Kept per position for the same reason as
        /// <see cref="Truncated"/>: <see cref="PeptideDocNode.BestResult"/> scores every replicate
        /// of every molecule with it, so it must not have to read a chromatogram to get it.
        /// </summary>
        public ChromFileIdMap<PeakIdentification> Identified { get; private set; }

        /// <summary>
        /// Whether each peak was integrated only because integration was forced, which
        /// <see cref="TransitionChromInfo.IsGoodPeak"/> excludes from the peak count. Kept for the
        /// same reason as <see cref="Identified"/>: the peak count ratio is shown for every
        /// molecule in the tree, and must not cost a chromatogram read.
        /// </summary>
        public ChromFileIdMap<bool> ForcedIntegration { get; private set; }

        /// <summary>
        /// The positions which have something that cannot be derived from the .skyd file.
        /// Sparse: most positions have no entry.
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
        public ImmutableList<TransitionChromInfo> LegacyChromInfos { get; private set; }

        public bool IsConverted
        {
            get { return LegacyChromInfos == null; }
        }

        public TransitionResults ChangeLegacyChromInfos(IEnumerable<TransitionChromInfo> value)
        {
            return ChangeProp(ImClone(this), im => im.LegacyChromInfos = value == null ? null : ImmutableList.ValueOf(value));
        }

        /// <summary>
        /// The chrom info for one file and optimization step which has not been converted, or null.
        /// A file belongs to one replicate, so the file and the step identify it on their own.
        /// </summary>
        public TransitionChromInfo FindChromInfo(ChromFileInfoId fileId, int optimizationStep)
        {
            if (LegacyChromInfos == null)
            {
                return null;
            }

            foreach (var chromInfo in LegacyChromInfos)
            {
                if (ReferenceEquals(chromInfo.FileId, fileId) && chromInfo.OptimizationStep == optimizationStep)
                {
                    return chromInfo;
                }
            }

            return null;
        }

        public TransitionResults ChangeUserSets(IEnumerable<UserSet> value)
        {
            return ChangeProp(ImClone(this), im => im.UserSets = MakeMap(value));
        }

        public TransitionResults ChangeTruncated(IEnumerable<bool?> value)
        {
            return ChangeProp(ImClone(this), im => im.Truncated = MakeMap(value));
        }

        public TransitionResults ChangeEmptyPeaks(IEnumerable<bool> value)
        {
            return ChangeProp(ImClone(this), im => im.EmptyPeaks = MakeMap(value));
        }

        public TransitionResults ChangeIdentified(IEnumerable<PeakIdentification> value)
        {
            return ChangeProp(ImClone(this), im => im.Identified = MakeMap(value));
        }

        public TransitionResults ChangeForcedIntegration(IEnumerable<bool> value)
        {
            return ChangeProp(ImClone(this), im => im.ForcedIntegration = MakeMap(value));
        }

        /// <summary>
        /// Whether the peak at one position ran off the end of the chromatogram, or null when
        /// nothing worked that out. See <see cref="Truncated"/>.
        /// </summary>
        public bool? GetTruncated(int position)
        {
            return Truncated == null ? null : Truncated.Values[position];
        }

        /// <summary>
        /// Whether there is no peak at one position at all. See <see cref="EmptyPeaks"/>.
        /// </summary>
        public bool IsEmptyPeak(int position)
        {
            return EmptyPeaks != null && EmptyPeaks.Values[position];
        }

        /// <summary>
        /// Whether the peak at one position contains an identification. See
        /// <see cref="Identified"/>.
        /// </summary>
        public PeakIdentification GetIdentified(int position)
        {
            return Identified == null ? PeakIdentification.FALSE : Identified.Values[position];
        }

        /// <summary>
        /// Whether the peak at one position counts towards the peak count ratio, which is what
        /// <see cref="TransitionChromInfo.IsGoodPeak"/> decides. Everything it looks at is stored,
        /// so this needs no chromatogram.
        /// </summary>
        public bool IsGoodPeak(int position, bool integrateAll)
        {
            if (IsEmptyPeak(position) || !(Areas.Values[position] > 0))
            {
                return false;
            }

            return integrateAll || ForcedIntegration == null || !ForcedIntegration.Values[position];
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
                yield return new QuantifiablePeak(ChromFileIds.FileIds[position].Value, Areas.Values[position],
                    GetTruncated(position), IsEmptyPeak(position));
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
            var newCustomPeaks = StripAnnotations.FromCustomPeaks(annotationNamesToKeep, CustomPeaks?.Values);
            if (ReferenceEquals(newCustomPeaks, CustomPeaks?.Values))
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
                CustomPeak.SetAtPosition(CustomPeaks?.Values, Areas.Values.Count, position, newCustomPeak));
        }

        /// <summary>
        /// The position of one file's entry in one replicate, or -1. See
        /// <see cref="TransitionGroupResults.IndexOfFile"/>.
        /// </summary>
        public int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
        {
            return ChromFileIds.IndexOfFile(replicateIndex, fileId);
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

            area = Areas.Values[position];
            return true;
        }

        public CustomPeak GetCustomPeak(int position)
        {
            return CustomPeaks?.Values[position];
        }

        public UserSet GetUserSet(int position)
        {
            return UserSets == null ? UserSet.FALSE : UserSets.Values[position];
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
                sources.Select(source => source.Pick(this, other).Areas.Values[source.Position]));
            if (UserSets != null || other.UserSets != null)
            {
                results = results.ChangeUserSets(
                    sources.Select(source => source.Pick(this, other).GetUserSet(source.Position)));
            }

            if (Truncated != null || other.Truncated != null)
            {
                results = results.ChangeTruncated(
                    sources.Select(source => source.Pick(this, other).GetTruncated(source.Position)));
            }

            if (EmptyPeaks != null || other.EmptyPeaks != null)
            {
                results = results.ChangeEmptyPeaks(
                    sources.Select(source => source.Pick(this, other).IsEmptyPeak(source.Position)));
            }

            if (Identified != null || other.Identified != null)
            {
                results = results.ChangeIdentified(
                    sources.Select(source => source.Pick(this, other).GetIdentified(source.Position)));
            }

            if (ForcedIntegration != null || other.ForcedIntegration != null)
            {
                results = results.ChangeForcedIntegration(sources.Select(source =>
                {
                    var forcedIntegration = source.Pick(this, other).ForcedIntegration;
                    return forcedIntegration != null && forcedIntegration.Values[source.Position];
                }));
            }

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
            // No ChromFileIds of its own: every map carries it, and Areas is always there.
            return Equals(Areas, other.Areas) &&
                   Equals(UserSets, other.UserSets) && Equals(Truncated, other.Truncated) &&
                   Equals(EmptyPeaks, other.EmptyPeaks) && Equals(Identified, other.Identified) &&
                   Equals(ForcedIntegration, other.ForcedIntegration) &&
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
                int result = Areas.GetHashCode();
                result = (result * 397) ^ (UserSets?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Truncated?.GetHashCode() ?? 0);
                result = (result * 397) ^ (EmptyPeaks?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Identified?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ForcedIntegration?.GetHashCode() ?? 0);
                result = (result * 397) ^ (CustomPeaks?.GetHashCode() ?? 0);
                result = (result * 397) ^ (LegacyChromInfos?.GetHashCode() ?? 0);
                return result;
            }
        }
    }

    /// <summary>
    /// Removing annotations from the columnar results, which is where they live now. The
    /// annotations of a peak are on its <see cref="CustomPeak"/>, and a peak whose annotations all
    /// go and which has nothing else to say stops needing one at all.
    /// </summary>
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
