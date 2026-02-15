/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

namespace pwiz.ProteowizardWrapper
{
    /// <summary>
    /// For Waters lockmass correction
    /// </summary>
    public class LockMassParameters : IComparable
    {
        private LockMassParameters(double? lockmassPositive, double? lockmassNegative, double? lockmassTolerance)
        {
            LockmassPositive = lockmassPositive;
            LockmassNegative = lockmassNegative;
            if (LockmassPositive.HasValue || LockmassNegative.HasValue)
            {
                LockmassTolerance = lockmassTolerance ?? LOCKMASS_TOLERANCE_DEFAULT;
            }
            else
            {
                LockmassTolerance = null;  // Means nothing when no mz is given
            }
        }

        public static LockMassParameters Create(double? positive, double? negative, double? tolerance)
        {
            if (positive.HasValue || negative.HasValue || tolerance.HasValue)
            {
                return new LockMassParameters(positive, negative, tolerance);
            }

            return EMPTY;
        }

        public double? LockmassPositive { get; private set; }
        public double? LockmassNegative { get; private set; }
        public double? LockmassTolerance { get; private set; }

        public static readonly double LOCKMASS_TOLERANCE_DEFAULT = 0.1; // Per Will T
        public static readonly double LOCKMASS_TOLERANCE_MAX = 10.0;
        public static readonly double LOCKMASS_TOLERANCE_MIN = 0;

        public static readonly LockMassParameters EMPTY = new LockMassParameters(null, null, null);

        public bool IsEmpty
        {
            get
            {
                return (0 == (LockmassNegative ?? 0)) &&
                       (0 == (LockmassPositive ?? 0));
                // Ignoring tolerance here, which means nothing when no mz is given
            }
        }

        private bool Equals(LockMassParameters other)
        {
            return LockmassPositive.Equals(other.LockmassPositive) && 
                   LockmassNegative.Equals(other.LockmassNegative) &&
                   LockmassTolerance.Equals(other.LockmassTolerance);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is LockMassParameters && Equals((LockMassParameters) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = LockmassPositive.GetHashCode();
                result = (result * 397) ^ LockmassNegative.GetHashCode();
                result = (result * 397) ^ LockmassTolerance.GetHashCode();
                return result;
            }
        }

        public int CompareTo(LockMassParameters other)
        {
            if (ReferenceEquals(null, other)) 
                return -1;
            var result = Nullable.Compare(LockmassPositive, other.LockmassPositive);
            if (result != 0)
                return result;
            result = Nullable.Compare(LockmassNegative, other.LockmassNegative);
            if (result != 0)
                return result;
            return Nullable.Compare(LockmassTolerance, other.LockmassTolerance);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return -1;
            if (ReferenceEquals(this, obj)) return 0;
            if (obj.GetType() != GetType()) return -1;
            return CompareTo((LockMassParameters)obj);
        }
    }
}