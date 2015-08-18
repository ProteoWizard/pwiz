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

using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model
{
    public class ExplicitTransitionGroupValues : Immutable
    {
        /// <summary>
        /// Helper class of attributes we normally calculate or get from a library, but which may
        /// be specified in an imported transition list or by some other means.
        /// </summary>

        public static readonly ExplicitTransitionGroupValues EMPTY = new ExplicitTransitionGroupValues(null);
        public static readonly ExplicitTransitionGroupValues TEST = new ExplicitTransitionGroupValues(1.23, 2.34, -.345, 4.56, 5.67, 6.78, 7.89); // Using this helps catch untested functionality as we add members

        public ExplicitTransitionGroupValues(double? explicitCollisionEnergy,
            double? explicitDriftTimeMsec,
            double? explicitDriftTimeHighEnergyOffsetMsec,
            double? explicitSLens,
            double? explicitConeVoltage,
            double? explicitDeclusteringPotential,
            double? explicitCompensationVoltage)
        {
            CollisionEnergy = explicitCollisionEnergy;
            DriftTimeMsec = explicitDriftTimeMsec;
            DriftTimeHighEnergyOffsetMsec = explicitDriftTimeHighEnergyOffsetMsec;
            SLens = explicitSLens;
            ConeVoltage = explicitConeVoltage;
            DeclusteringPotential = explicitDeclusteringPotential;
            CompensationVoltage = explicitCompensationVoltage;
        }

        public ExplicitTransitionGroupValues(ExplicitTransitionGroupValues other)
            : this(
                (other == null) ? null : other.CollisionEnergy,
                (other == null) ? null : other.DriftTimeMsec,
                (other == null) ? null : other.DriftTimeHighEnergyOffsetMsec,
                (other == null) ? null : other.SLens,
                (other == null) ? null : other.ConeVoltage,
                (other == null) ? null : other.DeclusteringPotential,
                (other == null) ? null : other.CompensationVoltage)
        {
        }

        public double? CollisionEnergy { get; private set; } // For import formats with explicit values for CE
        public double? DriftTimeMsec { get; private set; } // For import formats with explicit values for DT
        public double? DriftTimeHighEnergyOffsetMsec { get; private set; } // For import formats with explicit values for DT
        public double? SLens { get; private set; } // For Thermo
        public double? ConeVoltage { get; private set; } // For Waters
        public double? DeclusteringPotential { get; private set; } // For import formats with explicit values for DP
        public double? CompensationVoltage { get; private set; } // For import formats with explicit values for CV

        public ExplicitTransitionGroupValues ChangeCollisionEnergy(double? ce)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CollisionEnergy = v, ce);
        }

        public ExplicitTransitionGroupValues ChangeDriftTimeHighEnergyOffsetMsec(double? dtOffset)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DriftTimeHighEnergyOffsetMsec = v, dtOffset);
        }

        public ExplicitTransitionGroupValues ChangeDriftTime(double? dt)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DriftTimeMsec = v, dt);
        }

        public ExplicitTransitionGroupValues ChangeSLens(double? slens)
        {
            return ChangeProp(ImClone(this), (im, v) => im.SLens = v, slens);
        }

        public ExplicitTransitionGroupValues ChangeConeVoltage(double? coneVoltage)
        {
            return ChangeProp(ImClone(this), (im, v) => im.ConeVoltage = v, coneVoltage);
        }

        public ExplicitTransitionGroupValues ChangeDeclusteringPotential(double? dp)
        {
            return ChangeProp(ImClone(this), (im, v) => im.DeclusteringPotential = v, dp);
        }

        public ExplicitTransitionGroupValues ChangeCompensationVoltage(double? cv)
        {
            return ChangeProp(ImClone(this), (im, v) => im.CompensationVoltage = v, cv);
        }

        protected bool Equals(ExplicitTransitionGroupValues other)
        {
            return Equals(CollisionEnergy, other.CollisionEnergy) &&
                   Equals(DriftTimeMsec, other.DriftTimeMsec) &&
                   Equals(DriftTimeHighEnergyOffsetMsec, other.DriftTimeHighEnergyOffsetMsec) &&
                   Equals(SLens, other.SLens) &&
                   Equals(ConeVoltage, other.ConeVoltage) &&
                   CompensationVoltage.Equals(other.CompensationVoltage) &&
                   DeclusteringPotential.Equals(other.DeclusteringPotential);
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
                int hashCode = CollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeHighEnergyOffsetMsec.GetHashCode();
                hashCode = (hashCode * 397) ^ SLens.GetHashCode();
                hashCode = (hashCode * 397) ^ ConeVoltage.GetHashCode();
                hashCode = (hashCode * 397) ^ DeclusteringPotential.GetHashCode();
                hashCode = (hashCode * 397) ^ CompensationVoltage.GetHashCode();
                return hashCode;
            }
        }
    }
}