/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.Model
{

    public class PrecursorFilter : DefaultValues, IAuditLogComparable
    {
        /// <summary>
        /// Helper class of precursor attributes that further distinguish precursors of same molecule and adduct -
        /// stuff we normally calculate or get from a library, but which may
        /// be specified in an imported transition list or by some other means.
        /// </summary>

        public static readonly PrecursorFilter EMPTY = new PrecursorFilter(null, null);

        public static readonly PrecursorFilter[] ARRAY_EMPTY = { PrecursorFilter.EMPTY };

        private IonMobilityAndCCS _ionMobilityAndCCS;

        public static PrecursorFilter Create(double? collisionEnergy, IonMobilityAndCCS ionMobility)
        {
            if (((collisionEnergy??0) != 0) || !IonMobilityAndCCS.IsNullOrEmpty(ionMobility))
            {
                return new PrecursorFilter(collisionEnergy, ionMobility ?? IonMobilityAndCCS.EMPTY);
            }
            return EMPTY;
        }

        public static PrecursorFilter Create(double? collisionEnergy,
            double? ionMobility,
            eIonMobilityUnits ionMobilityUnits,
            double? collisionalCrossSectionSqA,
            double? highEnergyIonMobilityOffset)
        {
            return Create(collisionEnergy, 
                IonMobilityAndCCS.GetIonMobilityAndCCS(
                    ionMobility, ionMobilityUnits,
                    collisionalCrossSectionSqA,
                    highEnergyIonMobilityOffset));
        }

        private PrecursorFilter(double? collisionEnergy,
            IonMobilityAndCCS ionMobility)
        {
            CollisionEnergy = ((collisionEnergy??0) == 0) ? null : collisionEnergy;
            _ionMobilityAndCCS = ionMobility ?? IonMobilityAndCCS.EMPTY;
        }

        public bool IsEmpty => ReferenceEquals(this, EMPTY) || Equals(EMPTY);
        public static bool IsNullOrEmpty(PrecursorFilter values) => values == null || values.IsEmpty;

        [Track]
        public double? CollisionEnergy { get; private set; } // Can be a transition group thing, but any transition level explicit CE values will override this

        [Track] 
        public double? CollisionalCrossSectionSqA => _ionMobilityAndCCS.CollisionalCrossSectionSqA; // For import formats with explicit values for CCS
        [Track] 
        public double? IonMobility => _ionMobilityAndCCS.IonMobility.Mobility; // For import formats with explicit values for ion mobility

        [Track] 
        public double? IonMobilityHighEnergyOffset => _ionMobilityAndCCS.HighEnergyIonMobilityValueOffset; // For import formats with explicit values for ion mobility
        [Track]
        public eIonMobilityUnits IonMobilityUnits => _ionMobilityAndCCS.IonMobility.Units; // For import formats with explicit values for ion mobility
        
        public IonMobilityAndCCS IonMobilityAndCCS
        {
            get
            {
                return IonMobilityAndCCS.GetIonMobilityAndCCS(IonMobility, IonMobilityUnits, CollisionalCrossSectionSqA, IonMobilityHighEnergyOffset );
            }
        }

        public double? CompensationVoltage { get { return Equals(IonMobilityUnits, eIonMobilityUnits.compensation_V) ? IonMobility : null; } } // For backward compatibility, back when we didn't have general ion mobility

        public PrecursorFilter ChangeIonMobilityAndCCS(IonMobilityAndCCS imNew)
        {
            return Equals(IonMobilityAndCCS, imNew) ? this : Create(CollisionEnergy, imNew);
        }

        public PrecursorFilter ChangeIonMobility(double? valNew, eIonMobilityUnits unitsNew)
        {
            var imNew = _ionMobilityAndCCS.ChangeIonMobility(valNew, unitsNew);
            return ChangeIonMobilityAndCCS(imNew);
        }

        public PrecursorFilter ChangeCollisionalCrossSection(double? ccs)
        {
            var imNew = _ionMobilityAndCCS.ChangeCollisionalCrossSection(ccs);
            return ChangeIonMobilityAndCCS(imNew);
        }

        public PrecursorFilter ChangeCollisionEnergy(double? ce)
        {
            return Equals(ce, CollisionEnergy) ? this : Create(ce, IonMobilityAndCCS);
        }

        /// <summary>
        /// Return a new object with values taken from other, only where this lacks values
        /// </summary>
        public PrecursorFilter Merge(PrecursorFilter other)
        {
            if (PrecursorFilter.IsNullOrEmpty(other) || Equals(this, other))
            {
                return this;
            }

            return Create(CollisionEnergy ?? other.CollisionEnergy,
                IonMobility ?? other.IonMobility,
                (IonMobilityUnits != eIonMobilityUnits.none) ? IonMobilityUnits : other.IonMobilityUnits,
                CollisionalCrossSectionSqA ?? other.CollisionalCrossSectionSqA,
                IonMobilityHighEnergyOffset ?? other.IonMobilityHighEnergyOffset);
        }


        /// <summary>
        /// For XML serialization
        /// </summary>
        public PrecursorFilter() : this(null, null)
        {
        }

        private static readonly LibraryKeyProto.Types.PrecursorFilter EMPTY_PROTO = CreateLibraryProto(PrecursorFilter.EMPTY);
        private static LibraryKeyProto.Types.PrecursorFilter CreateLibraryProto(PrecursorFilter val)
        {
            return new LibraryKeyProto.Types.PrecursorFilter
            {
                CollisionCrossSectionSqA = val.IonMobilityAndCCS.CollisionalCrossSectionSqA,
                HighEnergyIonMobilityValueOffset = val.IonMobilityAndCCS.HighEnergyIonMobilityValueOffset,
                IonMobilityUnits =
                    (LibraryKeyProto.Types.PrecursorFilter.Types.IonMobilityUnits)val.IonMobilityAndCCS.IonMobility.Units,
                IonMobility = val.IonMobilityAndCCS.IonMobility.Mobility,
                CollisionEnergy = val.CollisionEnergy
            };
        }
        public static LibraryKeyProto.Types.PrecursorFilter GetLibraryProto(PrecursorFilter val)
        {
            return PrecursorFilter.IsNullOrEmpty(val)
                ? EMPTY_PROTO
                : CreateLibraryProto(val);
        }

        /// <summary>
        /// Performs comparison, accepting a CCS match even if other IM details are dissimilar
        /// </summary>
        public bool EquivalentPreferringCCS(PrecursorFilter other)
        {
            return Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Equals(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
        }

        protected bool Equals(PrecursorFilter other)
        {
            return EquivalentPreferringCCS(other) && // Compares CCS and everything else not ion mobility related
                   // For full equality we also compare ion mobility details beyond CCS
                   Equals(IonMobility, other.IonMobility) &&
                   Equals(IonMobilityUnits, other.IonMobilityUnits) &&
                   Equals(IonMobilityHighEnergyOffset, other.IonMobilityHighEnergyOffset);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PrecursorFilter)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IonMobility.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityUnits.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityHighEnergyOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionEnergy.GetHashCode();
                return hashCode;
            }
        }

        protected class PrecursorFilterDefaults : DefaultValues
        {
            public override bool IsDefault(object obj, object parentObject)
            {
                return IsNullOrEmpty(obj as PrecursorFilter);
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }

        public override string ToString()
        {
            if (IsEmpty)
            {
                return @"EMPTY"; // For debug convenience - use this.DisplayString for UI
            }
            var im = IonMobilityAndCCS;
            var str = im.IsEmpty ? string.Empty : im.ToDisplayString();
            if ((CollisionEnergy ?? 0) != 0)
            {
                str = $@"{str}{(string.IsNullOrEmpty(str) ? str : @" ")}CE{CollisionEnergy:0.00}";
            }
            return str;
        }

        public string DisplayString => IsEmpty ? string.Empty : ToString();

    }
}