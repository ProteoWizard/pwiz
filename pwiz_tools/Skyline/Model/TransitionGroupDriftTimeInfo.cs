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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Drift Time and Collisional Cross Section information for Transition Groups
    /// </summary>
    public sealed class TransitionGroupDriftTimeInfo : Immutable, IEquatable<TransitionGroupDriftTimeInfo>
    {
        public static TransitionGroupDriftTimeInfo EMPTY = new TransitionGroupDriftTimeInfo();

        private TransitionGroupDriftTimeInfo() { } // This is private to force use of GetTransitionGroupDriftTimeInfo (for memory efficiency, as most uses are empty)

        public static TransitionGroupDriftTimeInfo GetTransitionGroupIonMobilityInfo(float? collisionalCrossSection, float? driftTimeMS1,
            float? driftTimeFragment, float? driftTimeWindow)
        {
            if (collisionalCrossSection.HasValue || driftTimeMS1.HasValue || driftTimeFragment.HasValue || driftTimeWindow.HasValue)
            {
                return new TransitionGroupDriftTimeInfo
                {
                    CollisionalCrossSection = collisionalCrossSection,
                    DriftTimeMS1 = driftTimeMS1,
                    DriftTimeFragment = driftTimeFragment,
                    DriftTimeWindow = driftTimeWindow
                };
            }
            return EMPTY;
        }

        public float? CollisionalCrossSection { get; private set; }
        public float? DriftTimeMS1 { get; private set; }
        public float? DriftTimeFragment { get; private set; }
        public float? DriftTimeWindow { get; private set; }

        public bool IsEmpty { get { return Equals(EMPTY); } }

        // Used by TransitionGroupDocNode.AddChromInfo to aggregate ion mobility information from all transitions
        public TransitionGroupDriftTimeInfo AddDriftTimeFilterInfo(DriftTimeFilter driftTimeFilter, bool isMs1)
        {
            if (driftTimeFilter != null && driftTimeFilter.DriftTimeMsec.HasValue)
            {
                var driftTime = (float)driftTimeFilter.DriftTimeMsec.Value;

                if (IsEmpty)
                {
                    // First seen, expect to set window width and one or other of MS1/MS2, and CCS if we have it
                    return GetTransitionGroupIonMobilityInfo(
                            (float?)driftTimeFilter.CollisionalCrossSectionSqA,
                            isMs1 ? driftTime : (float?)null,
                            isMs1 ? (float?)null : driftTime,
                            (float?)driftTimeFilter.DriftTimeExtractionWindowWidthMsec);
                }

                // Sanity check: being based on the precursor m/z, CCS and DT window really should all be the same for all transitions
                Assume.IsTrue(Equals(DriftTimeWindow, (float?)driftTimeFilter.DriftTimeExtractionWindowWidthMsec));
                if (driftTimeFilter.CollisionalCrossSectionSqA.HasValue)
                {
                    // We will not see repeated CCS values from transition drift time filters when deserializing, but when we do they must agree
                    Assume.IsTrue(Equals(CollisionalCrossSection, (float?)driftTimeFilter.CollisionalCrossSectionSqA));
                }

                // Filling in MS1 or MS2 data, or just more of what we already know
                if (isMs1)
                {
                    // We expect these all to be the same for MS1
                    if (!DriftTimeMS1.HasValue)
                    {
                        return ChangeDriftTimeMS1(driftTime);
                    }
                    else
                    {
                        Assume.IsTrue(Equals(DriftTimeMS1, driftTime));
                    }
                }
                else
                {
                    // We expect these all to be the same for MS/MS
                    if (!DriftTimeFragment.HasValue)
                    {
                        return ChangeDriftTimeFragment(driftTime);
                    }
                    else
                    {
                        Assume.IsTrue(Equals(DriftTimeFragment, driftTime));
                    }
                }

            }
            return this; // No change
        }

        public TransitionGroupDriftTimeInfo ChangeCollisionalCrossSection(float? val)
        {
            return Equals(val, CollisionalCrossSection) ? this : ChangeProp(ImClone(this), im => im.CollisionalCrossSection = val);
        }
        public TransitionGroupDriftTimeInfo ChangeDriftTimeMS1(float? val)
        {
            return Equals(val, DriftTimeMS1) ? this : ChangeProp(ImClone(this), im => im.DriftTimeMS1 = val);
        }
        public TransitionGroupDriftTimeInfo ChangeDriftTimeFragment(float? val)
        {
            return Equals(val, DriftTimeFragment) ? this : ChangeProp(ImClone(this), im => im.DriftTimeFragment = val);
        }
        public TransitionGroupDriftTimeInfo ChangeDriftTimeWindow(float? val)
        {
            return Equals(val, DriftTimeWindow) ? this : ChangeProp(ImClone(this), im => im.DriftTimeWindow = val);
        }

        public bool Equals(TransitionGroupDriftTimeInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return CollisionalCrossSection.Equals(other.CollisionalCrossSection) && 
                DriftTimeMS1.Equals(other.DriftTimeMS1) && 
                DriftTimeFragment.Equals(other.DriftTimeFragment) && 
                DriftTimeWindow.Equals(other.DriftTimeWindow);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is TransitionGroupDriftTimeInfo && Equals((TransitionGroupDriftTimeInfo)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = CollisionalCrossSection.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeMS1.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeFragment.GetHashCode();
                hashCode = (hashCode * 397) ^ DriftTimeWindow.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(TransitionGroupDriftTimeInfo left, TransitionGroupDriftTimeInfo right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(TransitionGroupDriftTimeInfo left, TransitionGroupDriftTimeInfo right)
        {
            return !Equals(left, right);
        }

    }
}