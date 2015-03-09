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
        
        public static readonly ExplicitTransitionGroupValues EMPTY = new ExplicitTransitionGroupValues();

        public ExplicitTransitionGroupValues(double? explicitCollisionEnergy,
            double? explicitDriftTimeMsec,
            double? explicitDriftTimeHighEnergyOffsetMsec)
        {
            CollisionEnergy = explicitCollisionEnergy;
            DriftTimeMsec = explicitDriftTimeMsec;
            DriftTimeHighEnergyOffsetMsec = explicitDriftTimeHighEnergyOffsetMsec;
        }

        public ExplicitTransitionGroupValues(ExplicitTransitionGroupValues other)
            : this(
                (other == null) ? null : other.CollisionEnergy,
                (other == null) ? null : other.DriftTimeMsec,
                (other == null) ? null : other.DriftTimeHighEnergyOffsetMsec)
        {
        }

        public ExplicitTransitionGroupValues()
            : this(null)
        {
        }

        public double? CollisionEnergy { get; private set; } // For import formats with explicit values for CE
        public double? DriftTimeMsec { get; private set; } // For import formats with explicit values for DT
        public double? DriftTimeHighEnergyOffsetMsec { get; private set; } // For import formats with explicit values for DT

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

        protected bool Equals(ExplicitTransitionGroupValues other)
        {
            return CollisionEnergy.Equals(other.CollisionEnergy) &&
                   DriftTimeMsec.Equals(other.DriftTimeMsec) &&
                   DriftTimeHighEnergyOffsetMsec.Equals(other.DriftTimeHighEnergyOffsetMsec);
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
                return hashCode;
            }
        }
    }
}