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
            var areas = new List<float>();
            var retentionTimes = new List<float>();
            var userSets = new List<UserSet>();
            var qValues = new List<float>();
            var zScores = new List<float>();
            var chosenPeakIndexes = getChosenPeakIndex == null ? null : new List<int>();
            List<CustomPeak> customPeaks = null;
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                int count = 0;
                foreach (var chromInfo in results[replicateIndex])
                {
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }

                    CustomPeak.Collect(ref customPeaks, MakeCustomPeak(areas.Count, chromInfo));
                    fileIds.Add(chromInfo.FileId);
                    areas.Add(chromInfo.Area ?? 0);
                    retentionTimes.Add(chromInfo.RetentionTime ?? 0);
                    userSets.Add(chromInfo.UserSet);
                    qValues.Add(chromInfo.QValue ?? float.NaN);
                    zScores.Add(chromInfo.ZScore ?? float.NaN);
                    chosenPeakIndexes?.Add(getChosenPeakIndex(replicateIndex, chromInfo.FileId));
                    count++;
                }

                counts.Add(count);
            }

            var transitionGroupResults =
                new TransitionGroupResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), areas,
                        retentionTimes)
                    .ChangeUserSets(userSets)
                    .ChangeQValues(qValues)
                    .ChangeZScores(zScores);
            if (chosenPeakIndexes != null)
            {
                transitionGroupResults = transitionGroupResults.ChangeChosenPeakIndexes(chosenPeakIndexes);
            }

            if (customPeaks != null)
            {
                transitionGroupResults = transitionGroupResults.ChangeCustomPeaks(customPeaks);
            }

            // Kept whatever the caller knows, because the precursor level still holds values which
            // have no home in the columnar form yet - PeakCountRatio, the ion mobility info, the dot
            // products. Dropping them waits until every reader of them goes through MoleculeResults.
            return transitionGroupResults.ChangeChromInfos(results);
        }

        /// <summary>
        /// The entry for one position, or null when it has nothing which cannot be derived from
        /// the .skyd. Only the annotations are kept here. The precursor level values are all
        /// aggregated from the transitions, so the boundaries a user set are needed only at the
        /// transition level, where <see cref="TransitionResults"/> keeps them.
        /// </summary>
        private static CustomPeak MakeCustomPeak(int position, TransitionGroupChromInfo chromInfo)
        {
            if (chromInfo.Annotations == null || chromInfo.Annotations.IsEmpty)
            {
                return null;
            }

            return new CustomPeak(position).ChangeAnnotations(chromInfo.Annotations);
        }

        public TransitionGroupResults(ChromFileIds fileIds, IEnumerable<float> areas, IEnumerable<float> retentionTimes)
        {
            ChromFileIds = fileIds;
            Areas = areas.ToImmutable();
            RetentionTimes = retentionTimes.ToImmutable();
        }
        public ChromFileIds ChromFileIds { get; private set; }
        public ImmutableList<float> Areas { get; private set; }
        public ImmutableList<float> RetentionTimes { get; private set; }
        /// <summary>
        /// The peak currently chosen at each position.
        /// </summary>
        public ImmutableList<int> ChosenPeakIndexes { get; private set; }

        /// <summary>
        /// The peak Skyline originally picked, and the peak reintegration chose. Both are
        /// kept for the parts which need to know where a peak came from rather than only
        /// where it is now - retention time alignment and peak imputation.
        /// <para>
        /// In many documents all three of these lists hold the same indexes, so an incoming
        /// list which equals one already here is stored as the same instance rather than as
        /// a second copy.
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
        /// The positions which have something that cannot be derived from the .skyd file.
        /// Sparse: most positions have no entry.
        /// </summary>
        public ImmutableList<CustomPeak> CustomPeaks { get; private set; }

        /// <summary>
        /// The chrom infos which have not been worked out from the .skyd file yet. Null once they
        /// have been. The precursor level counterpart of <see cref="TransitionResults.ChromInfos"/>,
        /// and kept as a <see cref="Results{TItem}"/> rather than flattened because that is the shape
        /// every reader of <see cref="TransitionGroupDocNode.Results"/> still expects: while these
        /// are here, the node can hand them straight back.
        /// </summary>
        public Results<TransitionGroupChromInfo> ChromInfos { get; private set; }

        public bool IsConverted
        {
            get { return ChromInfos == null; }
        }

        public TransitionGroupResults ChangeChromInfos(Results<TransitionGroupChromInfo> value)
        {
            return ChangeProp(ImClone(this), im => im.ChromInfos = value);
        }

        /// <summary>
        /// These take an <see cref="IEnumerable{T}"/> rather than an
        /// <see cref="ImmutableList{T}"/> so that <see cref="ImmutableListFactory.ToImmutable{T}"/>
        /// gets the chance to store the indexes as bytes or shorts. A document which had its
        /// peaks picked normally has around ten candidate peaks, so these fit in a byte.
        /// Passing an <see cref="ImmutableList{T}"/> makes that a no-op, on the assumption
        /// that it has already been optimized.
        /// </summary>
        public TransitionGroupResults ChangeChosenPeakIndexes(IEnumerable<int> value)
        {
            return ChangeProp(ImClone(this), im => im.ChosenPeakIndexes = im.ShareEqualIndexes(value?.ToImmutable()));
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

            if (Equals(value, ChosenPeakIndexes))
            {
                return ChosenPeakIndexes;
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

        public TransitionGroupResults ChangeCustomPeaks(IEnumerable<CustomPeak> value)
        {
            return ChangeProp(ImClone(this), im => im.CustomPeaks = ImmutableList.ValueOf(value));
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
            if (ChosenPeakIndexes == null)
            {
                return null;
            }

            int chosenPeakIndex = ChosenPeakIndexes[position];
            return chosenPeakIndex < 0 ? (int?) null : chosenPeakIndex;
        }

        public CustomPeak GetCustomPeak(int position)
        {
            return CustomPeak.FindAtPosition(CustomPeaks, position);
        }

        public UserSet GetUserSet(int position)
        {
            return UserSets == null ? UserSet.FALSE : UserSets[position];
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
            return Equals(ChromFileIds, other.ChromFileIds) && Equals(Areas, other.Areas) &&
                   Equals(RetentionTimes, other.RetentionTimes) &&
                   Equals(ChosenPeakIndexes, other.ChosenPeakIndexes) &&
                   Equals(OriginalPeakIndexes, other.OriginalPeakIndexes) &&
                   Equals(ReintegratedPeakIndexes, other.ReintegratedPeakIndexes) &&
                   Equals(UserSets, other.UserSets) && Equals(QValues, other.QValues) &&
                   Equals(ZScores, other.ZScores) && Equals(CustomPeaks, other.CustomPeaks) &&
                   Results<TransitionGroupChromInfo>.EqualsDeep(ChromInfos, other.ChromInfos);
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
                int result = ChromFileIds.GetHashCode();
                result = (result * 397) ^ Areas.GetHashCode();
                result = (result * 397) ^ RetentionTimes.GetHashCode();
                result = (result * 397) ^ (ChosenPeakIndexes?.GetHashCode() ?? 0);
                result = (result * 397) ^ (OriginalPeakIndexes?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ReintegratedPeakIndexes?.GetHashCode() ?? 0);
                result = (result * 397) ^ (UserSets?.GetHashCode() ?? 0);
                result = (result * 397) ^ (QValues?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ZScores?.GetHashCode() ?? 0);
                result = (result * 397) ^ (CustomPeaks?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ChromInfos?.GetHashCode() ?? 0);
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
        /// chrom infos themselves in <see cref="ChromInfos"/>. See
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
            var chromInfos = keepChromInfos ? new List<TransitionChromInfo>() : null;
            List<CustomPeak> customPeaks = null;
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

                    CustomPeak.Collect(ref customPeaks, MakeCustomPeak(areas.Count, chromInfo));
                    fileIds.Add(chromInfo.FileId);
                    areas.Add(chromInfo.Area);
                    userSets.Add(chromInfo.UserSet);
                    truncated.Add(chromInfo.IsTruncated);
                    emptyPeaks.Add(chromInfo.IsEmpty);
                    count++;
                }

                counts.Add(count);
            }

            var transitionResults =
                new TransitionResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), areas)
                    .ChangeUserSets(userSets)
                    .ChangeTruncated(truncated)
                    .ChangeEmptyPeaks(emptyPeaks);
            if (customPeaks != null)
            {
                transitionResults = transitionResults.ChangeCustomPeaks(customPeaks);
            }

            return chromInfos == null ? transitionResults : transitionResults.ChangeChromInfos(chromInfos);
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
        private static CustomPeak MakeCustomPeak(int position, TransitionChromInfo chromInfo)
        {
            bool hasAnnotations = chromInfo.Annotations != null && !chromInfo.Annotations.IsEmpty;
            bool isUserSet = chromInfo.UserSet != UserSet.FALSE && !chromInfo.IsEmpty;
            if (!hasAnnotations && !isUserSet)
            {
                return null;
            }

            var customPeak = new CustomPeak(position);
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
            ChromFileIds = chromFileIds;
            Areas = areas.ToImmutable();
        }
        public ChromFileIds ChromFileIds { get; private set; }
        public ImmutableList<float> Areas { get; private set; }

        /// <summary>
        /// Almost always all <see cref="UserSet.FALSE"/>, which is why this gets stored
        /// through <see cref="ImmutableListFactory.MaybeConstant{T}"/>.
        /// </summary>
        public ImmutableList<UserSet> UserSets { get; private set; }

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
        public ImmutableList<bool?> Truncated { get; private set; }

        /// <summary>
        /// Whether each position has no peak at all, which is not the same as a peak whose area is
        /// zero: quantification counts the first as missing and the second as measured. This is
        /// what <see cref="TransitionChromInfo.IsEmpty"/> says, and it cannot be told from
        /// <see cref="Areas"/>, which is zero either way.
        /// </summary>
        public ImmutableList<bool> EmptyPeaks { get; private set; }

        /// <summary>
        /// The positions which have something that cannot be derived from the .skyd file.
        /// Sparse: most positions have no entry.
        /// </summary>
        public ImmutableList<CustomPeak> CustomPeaks { get; private set; }

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
        public ImmutableList<TransitionChromInfo> ChromInfos { get; private set; }

        public bool IsConverted
        {
            get { return ChromInfos == null; }
        }

        public TransitionResults ChangeChromInfos(IEnumerable<TransitionChromInfo> value)
        {
            return ChangeProp(ImClone(this), im => im.ChromInfos = value == null ? null : ImmutableList.ValueOf(value));
        }

        /// <summary>
        /// The chrom info for one file and optimization step which has not been converted, or null.
        /// A file belongs to one replicate, so the file and the step identify it on their own.
        /// </summary>
        public TransitionChromInfo FindChromInfo(ChromFileInfoId fileId, int optimizationStep)
        {
            if (ChromInfos == null)
            {
                return null;
            }

            foreach (var chromInfo in ChromInfos)
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
            return ChangeProp(ImClone(this), im => im.UserSets = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionResults ChangeTruncated(IEnumerable<bool?> value)
        {
            return ChangeProp(ImClone(this), im => im.Truncated = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionResults ChangeEmptyPeaks(IEnumerable<bool> value)
        {
            return ChangeProp(ImClone(this), im => im.EmptyPeaks = ImmutableList.ValueOf(value).MaybeConstant());
        }

        /// <summary>
        /// Whether the peak at one position ran off the end of the chromatogram, or null when
        /// nothing worked that out. See <see cref="Truncated"/>.
        /// </summary>
        public bool? GetTruncated(int position)
        {
            return Truncated?[position];
        }

        /// <summary>
        /// Whether there is no peak at one position at all. See <see cref="EmptyPeaks"/>.
        /// </summary>
        public bool IsEmptyPeak(int position)
        {
            return EmptyPeaks != null && EmptyPeaks[position];
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
            var replicatePositions = ChromFileIds.ReplicatePositions;
            if (replicateIndex < 0 || replicateIndex >= replicatePositions.ReplicateCount)
            {
                yield break;
            }

            int start = replicatePositions.GetStart(replicateIndex);
            for (int position = start; position < start + replicatePositions.GetCount(replicateIndex); position++)
            {
                yield return new QuantifiablePeak(ChromFileIds.FileIds[position].Value, Areas[position],
                    GetTruncated(position), IsEmptyPeak(position));
            }
        }

        public TransitionResults ChangeCustomPeaks(IEnumerable<CustomPeak> value)
        {
            return ChangeProp(ImClone(this), im => im.CustomPeaks = ImmutableList.ValueOf(value));
        }

        /// <summary>
        /// Records the boundaries of the peak at one position, keeping whatever else is already
        /// known about it. Used when the peak turns out not to be one of the candidate peaks, and
        /// so can only be got back by integrating between its boundaries.
        /// </summary>
        public TransitionResults ChangeCustomPeakBounds(int position, float startTime, float endTime,
            PeakIdentification identified)
        {
            var customPeaks = CustomPeaks?.ToList() ?? new List<CustomPeak>();
            int index = customPeaks.FindIndex(customPeak => customPeak.Position == position);
            var newCustomPeak = (index < 0 ? new CustomPeak(position) : customPeaks[index])
                .ChangePeakBounds(startTime, endTime, identified);
            if (index < 0)
            {
                customPeaks.Add(newCustomPeak);
            }
            else
            {
                customPeaks[index] = newCustomPeak;
            }

            return ChangeCustomPeaks(customPeaks);
        }

        /// <summary>
        /// The position of one file's entry in one replicate, or -1. See
        /// <see cref="TransitionGroupResults.IndexOfFile"/>.
        /// </summary>
        public int IndexOfFile(int replicateIndex, ChromFileInfoId fileId)
        {
            return ChromFileIds.IndexOfFile(replicateIndex, fileId);
        }

        public CustomPeak GetCustomPeak(int position)
        {
            return CustomPeak.FindAtPosition(CustomPeaks, position);
        }

        public UserSet GetUserSet(int position)
        {
            return UserSets == null ? UserSet.FALSE : UserSets[position];
        }

        /// <summary>
        /// Compared by value. See <see cref="TransitionGroupResults.Equals(TransitionGroupResults)"/>.
        /// </summary>
        protected bool Equals(TransitionResults other)
        {
            return Equals(ChromFileIds, other.ChromFileIds) && Equals(Areas, other.Areas) &&
                   Equals(UserSets, other.UserSets) && Equals(Truncated, other.Truncated) &&
                   Equals(EmptyPeaks, other.EmptyPeaks) && Equals(CustomPeaks, other.CustomPeaks) &&
                   Equals(ChromInfos, other.ChromInfos);
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
                int result = ChromFileIds.GetHashCode();
                result = (result * 397) ^ Areas.GetHashCode();
                result = (result * 397) ^ (UserSets?.GetHashCode() ?? 0);
                result = (result * 397) ^ (Truncated?.GetHashCode() ?? 0);
                result = (result * 397) ^ (EmptyPeaks?.GetHashCode() ?? 0);
                result = (result * 397) ^ (CustomPeaks?.GetHashCode() ?? 0);
                result = (result * 397) ^ (ChromInfos?.GetHashCode() ?? 0);
                return result;
            }
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
    /// Everything about the peak at one position which cannot be read back out of the .skyd
    /// file: the annotations, and the peak boundaries when the user chose them instead of
    /// accepting one of the candidate peaks that Skyline found.
    /// <para>
    /// These are expected to be rare, so they are held as a sparse list whose entries know
    /// their own position, rather than as one entry per position.
    /// </para>
    /// </summary>
    public class CustomPeak : Immutable
    {
        public CustomPeak(int position)
        {
            Position = position;
            Annotations = Annotations.EMPTY;
        }

        public int Position { get; private set; }
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
        /// Adds an entry for <paramref name="position"/> when there is anything to keep there,
        /// leaving <paramref name="customPeaks"/> null while there is nothing, which is the usual
        /// case.
        /// </summary>
        public static void Collect(ref List<CustomPeak> customPeaks, CustomPeak customPeak)
        {
            if (customPeak == null)
            {
                return;
            }

            customPeaks = customPeaks ?? new List<CustomPeak>();
            customPeaks.Add(customPeak);
        }

        public static CustomPeak FindAtPosition(IEnumerable<CustomPeak> customPeaks, int position)
        {
            return customPeaks?.FirstOrDefault(customPeak => customPeak.Position == position);
        }

        protected bool Equals(CustomPeak other)
        {
            return Position == other.Position && Equals(Annotations, other.Annotations) &&
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
                int result = Position;
                result = (result * 397) ^ Annotations.GetHashCode();
                result = (result * 397) ^ StartTime.GetHashCode();
                result = (result * 397) ^ EndTime.GetHashCode();
                result = (result * 397) ^ (int) Identified;
                return result;
            }
        }
    }
}
