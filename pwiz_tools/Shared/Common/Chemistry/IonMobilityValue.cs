/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

namespace pwiz.Common.Chemistry
{
    // N.B this order (starting with none=0) should agree with:
    //    enum IONMOBILITY_TYPE in pwiz_tools\BiblioSpec\src\BlibUtils.h
    // and
    //    enum IonMobilityUnits in pwiz\analysis\spectrum_processing\SpectrumList_IonMobility.hpp
    public enum eIonMobilityUnits
    {
        waters_sonar = -1, // Not really ion mobility, but SONAR uses IMS hardware for precursor isolation
        none,
        drift_time_msec,
        inverse_K0_Vsec_per_cm2,
        compensation_V,
        unknown // Keep this as last in list, used only in XML deserialization of older Skyline document
    }

    public sealed class IonMobilityValue : Immutable, IComparable<IonMobilityValue>
    {
        public static IonMobilityValue EMPTY = new IonMobilityValue(null, eIonMobilityUnits.none);

        public static bool IsNullOrEmpty(IonMobilityValue val) { return val == null || Equals(val, EMPTY); }

        // Private so we can issue EMPTY in the common case of no ion mobility info
        private IonMobilityValue(double? mobility, eIonMobilityUnits units)
        {
            Mobility = mobility;
            Units = units;
        }

        public static IonMobilityValue GetIonMobilityValue(double mobility, eIonMobilityUnits units)
        {
            return (units == eIonMobilityUnits.none || double.IsNaN(mobility))
                ? EMPTY
                : new IonMobilityValue(mobility, units);
        }


        public static IonMobilityValue GetIonMobilityValue(double? value, eIonMobilityUnits units)
        {
            return GetIonMobilityValue(value ?? double.NaN, units);
        }

        /// <summary>
        /// With drift time, we expect value to go up with each bin. With TIMS we expect it to go down.
        /// </summary>
        public static bool IsExpectedValueOrdering(IonMobilityValue left, IonMobilityValue right)
        {
            if (!left.HasValue)
            {
                return true; // Anything orders after nothing
            }
            if (left.Units == eIonMobilityUnits.inverse_K0_Vsec_per_cm2)
            {
                return (right.Mobility??0) < (left.Mobility??0);
            }
            return (left.Mobility??0) < (right.Mobility??0);
        }
        public IonMobilityValue ChangeIonMobility(double? value, eIonMobilityUnits units)
        {
            return value == Mobility && units == Units ? this : GetIonMobilityValue(value, units);
        }
        public IonMobilityValue ChangeIonMobility(double? value)
        {
            return value == Mobility ? this : GetIonMobilityValue(value, Units);
        }
        public IonMobilityValue ChangeIonMobilityUnits(eIonMobilityUnits units)
        {
            if (Equals(units, Units))
            {
                return this;
            }
            if (Equals(units, eIonMobilityUnits.none))
            {
                return EMPTY;
            }
            return ChangeProp(ImClone(this), im => im.Units = units);
        }

        /// <summary>
        /// Merge non-empty parts of other into a copy of this
        /// </summary>
        public IonMobilityValue Merge(IonMobilityValue other)
        {
            var val = this;
            if (other.Units != eIonMobilityUnits.none)
            {
                if (Equals(other.Units, eIonMobilityUnits.unknown))
                {
                    if (other.HasValue)
                        val = val.ChangeIonMobility(other.Mobility, Units);
                }
                else if (other.HasValue)
                {
                    val = other;
                }
                else
                {
                    val = val.ChangeIonMobility(Mobility, other.Units);
                }
            }
            else if (other.HasValue)
            {
                val = val.ChangeIonMobility(other.Mobility);
            }
            return val;
        }

        [Track]
        public double? Mobility { get; private set; }
        public eIonMobilityUnits Units { get; private set; }
        public bool HasValue { get { return Mobility.HasValue; } }

        public static string GetUnitsString(eIonMobilityUnits units)
        {
            switch (units)
            {
                case eIonMobilityUnits.none:
                    return @"#N/A";
                case eIonMobilityUnits.drift_time_msec:
                    return @"msec";
                case eIonMobilityUnits.inverse_K0_Vsec_per_cm2:
                    return @"Vs/cm^2";
                case eIonMobilityUnits.compensation_V:
                    return @"V";
                case eIonMobilityUnits.waters_sonar:
                    return @"m/z";
            }
            return @"unknown ion mobility type";
        }
        public string UnitsString
        {
            get { return GetUnitsString(Units); }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(IonMobilityValue)) return false;
            return Equals((IonMobilityValue)obj);
        }

        public bool Equals(IonMobilityValue other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.Units, Units) &&
                   Equals(other.Mobility, Mobility);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Mobility.GetHashCode();
                result = (result * 397) ^ Units.GetHashCode();
                return result;
            }
        }
        public override string ToString()
        {
            return Mobility+UnitsString;
        }

        public int CompareTo(IonMobilityValue other)
        {
            if (ReferenceEquals(this, other)) return 0;
            if (ReferenceEquals(null, other)) return 1;
            var valueComparison = Nullable.Compare(Mobility, other.Mobility);
            if (valueComparison != 0) return valueComparison;
            return Units.CompareTo(other.Units);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj)) return 1;
            if (ReferenceEquals(this, obj)) return 0;
            if (!(obj is IonMobilityValue)) throw new ArgumentException(@"Object must be of type IonMobilityValue");
            return CompareTo((IonMobilityValue) obj);
        }
    }
}
