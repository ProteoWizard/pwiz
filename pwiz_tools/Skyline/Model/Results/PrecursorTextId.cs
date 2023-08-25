/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Results
{
    public struct PrecursorTextId
    {
        public PrecursorTextId(SignedMz precursorMz, int? optStep, double? collisionEnergy,
            IonMobilityFilter ionMobilityFilter, ChromatogramGroupId chromatogramGroupId, ChromExtractor extractor) : this()
        {
            PrecursorMz = precursorMz;
            OptStep = optStep;
            CollisionEnergy = collisionEnergy;
            IonMobility = ionMobilityFilter ?? IonMobilityFilter.EMPTY;
            ChromatogramGroupId = chromatogramGroupId;
            Extractor = extractor;
        }

        public SignedMz PrecursorMz { get; private set; }
        public int? OptStep { get; }
        public double? CollisionEnergy { get; }
        public IonMobilityFilter IonMobility { get; private set; }
        public ChromatogramGroupId ChromatogramGroupId { get; }
        public ChromExtractor Extractor { get; private set; }

        #region object overrides

        public bool Equals(PrecursorTextId other)
        {
            return PrecursorMz.Equals(other.PrecursorMz) &&
                   Equals(OptStep, other.OptStep) &&
                   Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Equals(IonMobility, other.IonMobility) &&
                   Equals(ChromatogramGroupId, other.ChromatogramGroupId) &&
                   Extractor == other.Extractor;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PrecursorTextId && Equals((PrecursorTextId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = PrecursorMz.GetHashCode();
                hashCode = (hashCode * 397) ^ OptStep.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobility.GetHashCode();
                hashCode = (hashCode * 397) ^ (ChromatogramGroupId != null ? ChromatogramGroupId.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)Extractor;
                return hashCode;
            }
        }

        public override string ToString()
        {
            return string.Format(@"{0} - {1}{2} ({3})", ChromatogramGroupId, PrecursorMz, IonMobility, Extractor);    // For debugging
        }

        private sealed class PrecursorMzTextIdComparer : IComparer<PrecursorTextId>
        {
            public int Compare(PrecursorTextId x, PrecursorTextId y)
            {
                int c = x.PrecursorMz.CompareTo(y.PrecursorMz);
                if (c != 0)
                    return c;
                c = Nullable.Compare(x.OptStep, y.OptStep);
                if (c != 0)
                    return c;
                c = Nullable.Compare(x.CollisionEnergy, y.CollisionEnergy);
                if (c != 0)
                    return c;
                c = x.IonMobility.CompareTo(y.IonMobility);
                if (c != 0)
                    return c;
                c = ValueTuple.Create(x.ChromatogramGroupId).CompareTo(ValueTuple.Create(y.ChromatogramGroupId));
                if (c != 0)
                    return c;
                return x.Extractor - y.Extractor;
            }
        }

        private static readonly IComparer<PrecursorTextId> PRECURSOR_TEXT_ID_COMPARER_INSTANCE = new PrecursorMzTextIdComparer();

        public static IComparer<PrecursorTextId> PrecursorTextIdComparerInstance
        {
            get { return PRECURSOR_TEXT_ID_COMPARER_INSTANCE; }
        }

        #endregion
    }
}
