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
    public enum eIonMobilityUnits
    {
        none,
        drift_time_msec,
        inverse_K0_Vsec_per_cm2,
        compensation_V
    }

    public sealed class IonMobilityValue : IComparable<IonMobilityValue>, IComparable
    {
        public static IonMobilityValue EMPTY = new IonMobilityValue(null, eIonMobilityUnits.none);

        // Private so we can issue EMPTY in the common case of no ion mobility info
        private IonMobilityValue(double? mobility, eIonMobilityUnits units)
        {
            Mobility = mobility;
            Units = units;
        }

        public static IonMobilityValue GetIonMobilityValue(double mobility, eIonMobilityUnits units)
        {
            return (units == eIonMobilityUnits.none)
                ? EMPTY
                : new IonMobilityValue(mobility, units);
        }


        public static IonMobilityValue GetIonMobilityValue(double? value, eIonMobilityUnits units)
        {
            return (units == eIonMobilityUnits.none || !value.HasValue)
                ? EMPTY
                : new IonMobilityValue(value, units);
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
            return value == Mobility  ?this : GetIonMobilityValue(value, Units);
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
