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

namespace pwiz.Skyline.Model.Results
{

    public class TransitionGroupResults : Immutable
    {
        public TransitionGroupResults(ChromFileIds fileIds, IEnumerable<float> areas, IEnumerable<float> retentionTimes)
        {
            ChromFileIds = fileIds;
            Areas = areas.ToImmutable();
            RetentionTimes = retentionTimes.ToImmutable();
        }
        public ChromFileIds ChromFileIds { get; private set; }
        public ImmutableList<float> Areas { get; private set; }
        public ImmutableList<float> RetentionTimes { get; private set; }
        public ImmutableList<int> CandidatePeakIndexes { get; private set; }

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

        public TransitionGroupResults ChangeCandidatePeakIndexes(ImmutableList<int> value)
        {
            return ChangeProp(ImClone(this), im => im.CandidatePeakIndexes = value);
        }

        public TransitionGroupResults ChangeUserSets(IEnumerable<UserSet> value)
        {
            return ChangeProp(ImClone(this), im => im.UserSets = ImmutableList.ValueOf(value).MaybeConstant());
        }

        public TransitionGroupResults ChangeCustomPeaks(IEnumerable<CustomPeak> value)
        {
            return ChangeProp(ImClone(this), im => im.CustomPeaks = ImmutableList.ValueOf(value));
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

    public class TransitionResults : Immutable
    {
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
        /// Set only when the peak boundaries are not those of a candidate peak, which is what
        /// happens when the user sets them. The values which depend on the boundaries get
        /// recalculated by integrating the chromatogram again between these times.
        /// </summary>
        public float? StartTime { get; private set; }
        public float? EndTime { get; private set; }

        public bool HasPeakBounds
        {
            get { return StartTime.HasValue && EndTime.HasValue; }
        }

        public CustomPeak ChangeAnnotations(Annotations value)
        {
            return ChangeProp(ImClone(this), im => im.Annotations = value ?? Annotations.EMPTY);
        }

        public CustomPeak ChangePeakBounds(float? startTime, float? endTime)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.StartTime = startTime;
                im.EndTime = endTime;
            });
        }

        public static CustomPeak FindAtPosition(IEnumerable<CustomPeak> customPeaks, int position)
        {
            return customPeaks?.FirstOrDefault(customPeak => customPeak.Position == position);
        }

        protected bool Equals(CustomPeak other)
        {
            return Position == other.Position && Equals(Annotations, other.Annotations) &&
                   Nullable.Equals(StartTime, other.StartTime) && Nullable.Equals(EndTime, other.EndTime);
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
                return result;
            }
        }
    }
}
