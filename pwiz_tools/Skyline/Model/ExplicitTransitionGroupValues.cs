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

namespace pwiz.Skyline.Model
{
    public class ExplicitTransitionGroupValues : Immutable, IAuditLogComparable
    {
        /// <summary>
        /// Helper class of attributes we normally calculate or get from a library, but which may
        /// be specified in an imported transition list or by some other means.
        /// </summary>

        public static readonly ExplicitTransitionGroupValues EMPTY = new ExplicitTransitionGroupValues(null);

        public static ExplicitTransitionGroupValues Create(double? explicitIonMobility,
            eIonMobilityUnits explicitIonMobilityUnits,
            double? explicitCollisionalCrossSectionSqA)
        {
            if (explicitIonMobility.HasValue || explicitCollisionalCrossSectionSqA.HasValue)
            {
                return new ExplicitTransitionGroupValues(explicitIonMobility, explicitIonMobilityUnits,
                    explicitCollisionalCrossSectionSqA);
            }

            return EMPTY;
        }

        private ExplicitTransitionGroupValues(double? explicitIonMobility,
            eIonMobilityUnits explicitIonMobilityUnits,
            double? explicitCollisionalCrossSectionSqA) 
        {
            IonMobility = explicitIonMobility;
            IonMobilityUnits = explicitIonMobilityUnits;
            CollisionalCrossSectionSqA = explicitCollisionalCrossSectionSqA;
        }

        public ExplicitTransitionGroupValues(ExplicitTransitionGroupValues other)
            : this(
                (other == null) ? null : other.IonMobility,
                (other == null) ? eIonMobilityUnits.none : other.IonMobilityUnits,
                (other == null) ? null : other.CollisionalCrossSectionSqA)
        {
        }

        [Track]
        public double? CollisionalCrossSectionSqA { get; private set; } // For import formats with explicit values for CCS
        [Track]
        public double? IonMobility { get; private set; } // For import formats with explicit values for DT
        [Track]
        public eIonMobilityUnits IonMobilityUnits { get; private set; } // For import formats with explicit values for DT

        public double? CompensationVoltage { get { return Equals(IonMobilityUnits, eIonMobilityUnits.compensation_V) ? IonMobility : null; } } // For backward compatibility, back when we didn't have general ion mobility

        public ExplicitTransitionGroupValues ChangeIonMobility(double? imNew, eIonMobilityUnits unitsNew)
        {
            var explicitTransitionGroupValues = ChangeProp(ImClone(this), (im, v) => im.IonMobility = v, imNew);
            return ChangeProp(ImClone(explicitTransitionGroupValues), (im, v) => im.IonMobilityUnits = v, unitsNew);
        }

        public ExplicitTransitionGroupValues ChangeCollisionalCrossSection(double? ccs)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CollisionalCrossSectionSqA = v, ccs);
        }

        protected bool Equals(ExplicitTransitionGroupValues other)
        {
            return Equals(IonMobility, other.IonMobility) &&
                   Equals(IonMobilityUnits, other.IonMobilityUnits) &&
                   Equals(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExplicitTransitionGroupValues)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = IonMobility.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityUnits.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSectionSqA.GetHashCode();
                return hashCode;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }
    }
}