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
            double? explicitCollisionalCrossSectionSqA,
            double? explicitCompensationVoltage,
            ExplicitTransitionValues explicitTransitionValueDefaults)
        {
            if (explicitIonMobility.HasValue || explicitCollisionalCrossSectionSqA.HasValue || explicitCompensationVoltage.HasValue || 
                (explicitTransitionValueDefaults != null && !explicitTransitionValueDefaults.Equals(ExplicitTransitionValues.EMPTY)))
            {
                return new ExplicitTransitionGroupValues(explicitIonMobility, explicitIonMobilityUnits,
                    explicitCollisionalCrossSectionSqA, explicitCompensationVoltage, explicitTransitionValueDefaults);
            }

            return EMPTY;
        }

        private ExplicitTransitionGroupValues(double? explicitIonMobility,
            eIonMobilityUnits explicitIonMobilityUnits,
            double? explicitCollisionalCrossSectionSqA,
            double? explicitCompensationVoltage,
            ExplicitTransitionValues explicitTransitionValueDefaults) 
        {
            IonMobility = explicitIonMobility;
            IonMobilityUnits = explicitIonMobilityUnits;
            CollisionalCrossSectionSqA = explicitCollisionalCrossSectionSqA;
            CompensationVoltage = explicitCompensationVoltage;
            ExplicitTransitionValueDefaults = explicitTransitionValueDefaults;
        }

        public ExplicitTransitionGroupValues(ExplicitTransitionGroupValues other)
            : this(
                (other == null) ? null : other.IonMobility,
                (other == null) ? eIonMobilityUnits.none : other.IonMobilityUnits,
                (other == null) ? null : other.CollisionalCrossSectionSqA,
                (other == null) ? null : other.CompensationVoltage,
                (other == null) ? ExplicitTransitionValues.EMPTY : other.ExplicitTransitionValueDefaults)
        {
        }

        public ExplicitTransitionGroupValues Merge(ExplicitTransitionValues other)
        {
            return ChangeExplicitTransitionValueDefaults(other.Merge(ExplicitTransitionValueDefaults));
        }

        public ExplicitTransitionValues ExplicitTransitionValueDefaults { get; private set; }

        [Track]
        public double? CollisionEnergy => ExplicitTransitionValueDefaults.CollisionEnergy; // For import formats with explicit values for CE
        [Track]
        public double? DeclusteringPotential => ExplicitTransitionValueDefaults.DeclusteringPotential; // For import formats with explicit values for DP
        [Track]
        public double? SLens => ExplicitTransitionValueDefaults.SLens; // For Thermo
        [Track]
        public double? ConeVoltage => ExplicitTransitionValueDefaults.ConeVoltage; // For Waters
        [Track]
        public double? CompensationVoltage // For import formats with explicit values for CV, which is actually an ion mobility value
        {
            get
            {
                return IonMobilityUnits == eIonMobilityUnits.compensation_V ? IonMobility : null;
            }
            private set
            {
                if (!value.HasValue && IonMobilityUnits != eIonMobilityUnits.compensation_V)
                {
                    return; // This changes nothing
                }
                IonMobility = value;
                IonMobilityUnits = value.HasValue ? eIonMobilityUnits.compensation_V : eIonMobilityUnits.none;
            }
        } 
        [Track]
        public double? CollisionalCrossSectionSqA { get; private set; } // For import formats with explicit values for CCS
        [Track]
        public double? IonMobility { get; private set; } // For import formats with explicit values for DT
        [Track]
        public double? IonMobilityHighEnergyOffset => ExplicitTransitionValueDefaults.IonMobilityHighEnergyOffset; // For import formats with explicit values for DT
        [Track]
        public eIonMobilityUnits IonMobilityUnits { get; private set; } // For import formats with explicit values for DT

        public ExplicitTransitionGroupValues ChangeCollisionEnergy(double? ce)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = im.ExplicitTransitionValueDefaults.ChangeCollisionEnergy(v), ce);
        }

        public ExplicitTransitionGroupValues ChangeIonMobilityHighEnergyOffset(double? dtOffset)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = im.ExplicitTransitionValueDefaults.ChangeIonMobilityHighEnergyOffset(v), dtOffset);
        }

        public ExplicitTransitionGroupValues ChangeIonMobility(double? imNew, eIonMobilityUnits unitsNew)
        {
            var explicitTransitionGroupValues = ChangeProp(ImClone(this), (im, v) => im.IonMobility = v, imNew);
            return ChangeProp(ImClone(explicitTransitionGroupValues), (im, v) => im.IonMobilityUnits = v, unitsNew);
        }

        public ExplicitTransitionGroupValues ChangeCollisionalCrossSection(double? ccs)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CollisionalCrossSectionSqA = v, ccs);
        }

        public ExplicitTransitionGroupValues ChangeSLens(double? slens)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = im.ExplicitTransitionValueDefaults.ChangeSLens(v), slens);
        }

        public ExplicitTransitionGroupValues ChangeConeVoltage(double? coneVoltage)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = im.ExplicitTransitionValueDefaults.ChangeConeVoltage(v), coneVoltage);
        }

        public ExplicitTransitionGroupValues ChangeDeclusteringPotential(double? dp)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = im.ExplicitTransitionValueDefaults.ChangeDeclusteringPotential(v), dp);
        }

        public ExplicitTransitionGroupValues ChangeCompensationVoltage(double? cv)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CompensationVoltage = v, cv);
        }

        public ExplicitTransitionGroupValues ChangeExplicitTransitionValueDefaults(ExplicitTransitionValues ev)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ExplicitTransitionValueDefaults = v, ev);
        }

        protected bool Equals(ExplicitTransitionGroupValues other)
        {
            return Equals(IonMobility, other.IonMobility) &&
                   Equals(IonMobilityUnits, other.IonMobilityUnits) &&
                   Equals(CollisionalCrossSectionSqA, other.CollisionalCrossSectionSqA) &&
                   CompensationVoltage.Equals(other.CompensationVoltage) &&
                   ExplicitTransitionValueDefaults.Equals(other.ExplicitTransitionValueDefaults);
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
                hashCode = (hashCode * 397) ^ CompensationVoltage.GetHashCode();
                hashCode = (hashCode * 397) ^ ExplicitTransitionValueDefaults.GetHashCode();
                return hashCode;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }
    }
}