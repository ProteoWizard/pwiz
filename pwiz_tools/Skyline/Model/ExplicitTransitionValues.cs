/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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

using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model
{
    public class ExplicitTransitionValues : Immutable, IAuditLogComparable
    {
        /// <summary>
        /// Helper class of attributes we normally calculate or get from a library, but which may
        /// be specified in an imported transition list or by some other means.
        /// This is a subset of attributes found in in ExplicitTransitionGroupValue
        /// </summary>

        public static readonly ExplicitTransitionValues EMPTY = new ExplicitTransitionValues(null);

        public static ExplicitTransitionValues Get(TransitionDocNode node) {return node == null ? EMPTY : node.ExplicitValues; } // Convenience function

        public static ExplicitTransitionValues Create(double? explicitCollisionEnergy,
            double? explicitIonMobilityHighEnergyOffset,
            double? explicitSLens,
            double? explicitConeVoltage,
            double? explicitDeclusteringPotential)
        {
            if (explicitCollisionEnergy.HasValue || explicitIonMobilityHighEnergyOffset.HasValue
                                                 || explicitSLens.HasValue || explicitConeVoltage.HasValue ||
                                                 explicitDeclusteringPotential.HasValue)
                return new ExplicitTransitionValues(explicitCollisionEnergy,
                    explicitIonMobilityHighEnergyOffset,
                    explicitSLens,
                    explicitConeVoltage,
                    explicitDeclusteringPotential);
            return EMPTY;
        }

        private ExplicitTransitionValues(double? explicitCollisionEnergy,
            double? explicitIonMobilityHighEnergyOffset,
            double? explicitSLens,
            double? explicitConeVoltage,
            double? explicitDeclusteringPotential)
        {
            CollisionEnergy = explicitCollisionEnergy;
            IonMobilityHighEnergyOffset = explicitIonMobilityHighEnergyOffset;
            SLens = explicitSLens;
            ConeVoltage = explicitConeVoltage;
            DeclusteringPotential = explicitDeclusteringPotential;
        }


        /// <summary>
        /// return a new object with values taken from other, only where this lacks values
        /// </summary>
        public ExplicitTransitionValues Merge(ExplicitTransitionValues other)
        {
            if (other == null || other.Equals(EMPTY))
            {
                return this;
            }

            return Create(CollisionEnergy.HasValue ? CollisionEnergy : other.CollisionEnergy,
                IonMobilityHighEnergyOffset.HasValue ? IonMobilityHighEnergyOffset : other.IonMobilityHighEnergyOffset,
                SLens.HasValue ? SLens : other.SLens,
                ConeVoltage.HasValue ? ConeVoltage : other.ConeVoltage,
                DeclusteringPotential.HasValue ? DeclusteringPotential : other.DeclusteringPotential);
        }

        /// <summary>
        /// return a new object with values set only where they differ betwwen this and other, using the values of other
        /// </summary>
        public ExplicitTransitionValues Diff(ExplicitTransitionValues other)
        {
            if (other == null || other.Equals(EMPTY))
            {
                return this;
            }

            return Create(Equals(other.CollisionEnergy, CollisionEnergy) ? null : other.CollisionEnergy,
                Equals(other.IonMobilityHighEnergyOffset, IonMobilityHighEnergyOffset) ? null : other.IonMobilityHighEnergyOffset, 
                Equals(other.SLens, SLens) ? null : other.SLens,
                Equals(other.ConeVoltage, ConeVoltage) ? null : other.ConeVoltage,
                Equals(other.DeclusteringPotential, DeclusteringPotential) ? null : other.DeclusteringPotential);
        }

        public ExplicitTransitionValues(ExplicitTransitionValues other)
            : this(
                (other == null) ? null : other.CollisionEnergy,
                (other == null) ? null : other.IonMobilityHighEnergyOffset,
                (other == null) ? null : other.SLens,
                (other == null) ? null : other.ConeVoltage,
                (other == null) ? null : other.DeclusteringPotential)
        {
        }

        [Track]
        public double? CollisionEnergy { get; private set; } // For import formats with explicit values for CE
        [Track]
        public double? DeclusteringPotential { get; private set; } // For import formats with explicit values for DP
        [Track]
        public double? SLens { get; private set; } // For Thermo
        [Track]
        public double? ConeVoltage { get; private set; } // For Waters
        [Track]
        public double? IonMobilityHighEnergyOffset { get; private set; } // For import formats with explicit values for DT

        public ExplicitTransitionValues ChangeCollisionEnergy(double? ce)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CollisionEnergy = v, ce);
        }

        public ExplicitTransitionValues ChangeIonMobilityHighEnergyOffset(double? dtOffset)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IonMobilityHighEnergyOffset = v, dtOffset);
        }

        public ExplicitTransitionValues ChangeSLens(double? slens)
        {
            return ChangeProp(ImClone(this), (im, v) => im.SLens = v, slens);
        }

        public ExplicitTransitionValues ChangeConeVoltage(double? coneVoltage)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ConeVoltage = v, coneVoltage);
        }

        public ExplicitTransitionValues ChangeDeclusteringPotential(double? dp)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DeclusteringPotential = v, dp);
        }

        protected bool Equals(ExplicitTransitionValues other)
        {
            return Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Equals(IonMobilityHighEnergyOffset, other.IonMobilityHighEnergyOffset) &&
                   Equals(SLens, other.SLens) &&
                   Equals(ConeVoltage, other.ConeVoltage) &&
                   DeclusteringPotential.Equals(other.DeclusteringPotential);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ExplicitTransitionValues)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityHighEnergyOffset.GetHashCode();
                hashCode = (hashCode * 397) ^ SLens.GetHashCode();
                hashCode = (hashCode * 397) ^ ConeVoltage.GetHashCode();
                hashCode = (hashCode * 397) ^ DeclusteringPotential.GetHashCode();
                return hashCode;
            }
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }
    }
}