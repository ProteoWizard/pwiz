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
            List<CustomPeak> customPeaks = null;
            foreach (var chromInfoList in results)
            {
                int count = 0;
                foreach (var chromInfo in chromInfoList)
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
            return customPeaks == null
                ? transitionGroupResults
                : transitionGroupResults.ChangeCustomPeaks(customPeaks);
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
        /// Builds the columnar form from the chrom infos a document already holds. See
        /// <see cref="TransitionGroupResults.FromChromInfos"/>.
        /// </summary>
        public static TransitionResults FromChromInfos(Results<TransitionChromInfo> results)
        {
            if (results == null)
            {
                return null;
            }

            var fileIds = new List<ChromFileInfoId>();
            var counts = new List<int>();
            var areas = new List<float>();
            var userSets = new List<UserSet>();
            List<CustomPeak> customPeaks = null;
            foreach (var chromInfoList in results)
            {
                int count = 0;
                foreach (var chromInfo in chromInfoList)
                {
                    if (chromInfo.OptimizationStep != 0)
                    {
                        continue;
                    }

                    CustomPeak.Collect(ref customPeaks, MakeCustomPeak(areas.Count, chromInfo));
                    fileIds.Add(chromInfo.FileId);
                    areas.Add(chromInfo.Area);
                    userSets.Add(chromInfo.UserSet);
                    count++;
                }

                counts.Add(count);
            }

            var transitionResults =
                new TransitionResults(new ChromFileIds(ReplicatePositions.FromCounts(counts), fileIds), areas)
                    .ChangeUserSets(userSets);
            return customPeaks == null ? transitionResults : transitionResults.ChangeCustomPeaks(customPeaks);
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
        /// The positions which have something that cannot be derived from the .skyd file.
        /// Sparse: most positions have no entry.
        /// </summary>
        public ImmutableList<CustomPeak> CustomPeaks { get; private set; }

        public TransitionResults ChangeUserSets(IEnumerable<UserSet> value)
        {
            return ChangeProp(ImClone(this), im => im.UserSets = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionResults ChangeCustomPeaks(IEnumerable<CustomPeak> value)
        {
            return ChangeProp(ImClone(this), im => im.CustomPeaks = ImmutableList.ValueOf(value));
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
