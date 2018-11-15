/*
 * Original author: Brian Pratt <bspratt at proteinms dot net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Ion Mobility and Collisional Cross Section information for Transition Groups
    /// </summary>
    public sealed class TransitionGroupIonMobilityInfo : Immutable, IEquatable<TransitionGroupIonMobilityInfo>
    {
        public static TransitionGroupIonMobilityInfo EMPTY = new TransitionGroupIonMobilityInfo
        {
            CollisionalCrossSection = null,
            IonMobilityMS1 = null,
            IonMobilityFragment = null,
            IonMobilityWindow = null,
            IonMobilityUnits = eIonMobilityUnits.none
        };
        private TransitionGroupIonMobilityInfo() { } // This is private to force use of GetTransitionGroupIonMobilityInfo (for memory efficiency, as most uses are empty)


        // Serialization support
        public static TransitionGroupIonMobilityInfo GetTransitionGroupIonMobilityInfo(double? ccs, double? ionMobilityMS1,
            double? ionMobilityFragment, double? ionMobilityWindow, eIonMobilityUnits units)
        {
            if (ccs.HasValue || ionMobilityMS1.HasValue || ionMobilityFragment.HasValue || ionMobilityWindow.HasValue)
                return new TransitionGroupIonMobilityInfo()
                {
                    CollisionalCrossSection = ccs,
                    IonMobilityMS1 = ionMobilityMS1,
                    IonMobilityFragment = ionMobilityFragment,
                    IonMobilityWindow = ionMobilityWindow,
                    IonMobilityUnits = units
                };
            return EMPTY;
        }

        public eIonMobilityUnits IonMobilityUnits { get; private set; }
        public double? CollisionalCrossSection { get; private set; }
        public double? IonMobilityMS1 { get; private set; }
        public double? IonMobilityFragment { get; private set; }
        public double? IonMobilityWindow { get; private set; }

        public bool IsEmpty { get { return Equals(EMPTY); } }

        public double? DriftTimeMS1 { get { return IonMobilityUnits == eIonMobilityUnits.drift_time_msec ? IonMobilityMS1 : null; } }
        public double? DriftTimeFragment { get { return IonMobilityUnits == eIonMobilityUnits.drift_time_msec ? IonMobilityFragment : null; } }
        public double? DriftTimeWindow { get { return IonMobilityUnits == eIonMobilityUnits.drift_time_msec ? IonMobilityWindow : null; } }


        // Used by TransitionGroupDocNode.AddChromInfo to aggregate ion mobility information from all transitions
        public TransitionGroupIonMobilityInfo AddIonMobilityFilterInfo(IonMobilityFilter ionMobility, bool isMs1)
        {
            var val = Equals(ionMobility.IonMobilityExtractionWindowWidth, IonMobilityWindow) ? this : ChangeProp(ImClone(this), im => im.IonMobilityWindow = ionMobility.IonMobilityExtractionWindowWidth);

            if (ionMobility.IonMobility.Units != IonMobilityUnits &&  ionMobility.IonMobility.Units != eIonMobilityUnits.none)
               val = ChangeProp(ImClone(val), im => im.IonMobilityUnits = ionMobility.IonMobility.Units);

            // Filling in MS1 or MS2 data, or just more of what we already know
            if (isMs1)
            {
                // We expect these all to be the same for MS1, but can't assert that here because we
                // may be in the process of re-import with a different filter setting.
                val = Equals(ionMobility.IonMobility.Mobility, val.IonMobilityMS1) ? val : ChangeProp(ImClone(val), im => im.IonMobilityMS1 = ionMobility.IonMobility.Mobility);
            }
            else
            {
                // We expect these all to be the same for MS/MS, but can't assert that here because we
                // may be in the process of re-import with a different filter setting.
                val = Equals(ionMobility.IonMobility.Mobility, val.IonMobilityFragment) ? val : ChangeProp(ImClone(val), im => im.IonMobilityFragment = ionMobility.IonMobility.Mobility);
            }

            if (ionMobility.CollisionalCrossSectionSqA.HasValue && !Equals(ionMobility.CollisionalCrossSectionSqA, val.CollisionalCrossSection))
                val = ChangeProp(ImClone(val), im => im.CollisionalCrossSection = ionMobility.CollisionalCrossSectionSqA);
            return val;
        }

        public bool Equals(TransitionGroupIonMobilityInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return IonMobilityMS1.Equals(other.IonMobilityMS1) && 
                IonMobilityFragment.Equals(other.IonMobilityFragment) &&
                CollisionalCrossSection.Equals(other.CollisionalCrossSection) &&
                IonMobilityWindow.Equals(other.IonMobilityWindow) &&
                IonMobilityUnits.Equals(other.IonMobilityUnits);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is TransitionGroupIonMobilityInfo && Equals((TransitionGroupIonMobilityInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IonMobilityMS1.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityFragment.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityUnits.GetHashCode();
                hashCode = (hashCode * 397) ^ CollisionalCrossSection.GetHashCode();
                hashCode = (hashCode * 397) ^ IonMobilityWindow.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(TransitionGroupIonMobilityInfo left, TransitionGroupIonMobilityInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TransitionGroupIonMobilityInfo left, TransitionGroupIonMobilityInfo right)
        {
            return !Equals(left, right);
        }

    }
}