/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.Globalization;
using System.Text.RegularExpressions;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model
{
    public class MassModification
    {
        /// <summary>
        /// The maximum precision that we look at for matching.
        ///  Even if the library says that the mass of a modification is +57.0214635, 
        /// we still want to match that to our Carbamidomethyl (C) 52.021464.
        /// Also, we want their Sodium: 22.989769 to match our Sodium: 22.989767
        /// </summary>
        public const int MAX_PRECISION_TO_MATCH = 4;
        public const int MAX_PRECISION_FOR_LIB = 5; // BlibBuild uses 5 digit precision
        public const int MAX_PRECISION_TO_KEEP = 6;


        public MassModification(double mass, int precision)
        {
            Mass = mass;
            Precision = precision;
        }

        public double Mass { get; private set; }
        public int Precision { get; private set; }

        public static MassModification FromMass(double mass)
        {
            return new MassModification(mass, InferPrecision(mass));
        }

        public static MassModification FromMassForLib(double mass)
        {
            return FromMass(Math.Round(mass, MAX_PRECISION_FOR_LIB));
        }

        public bool Matches(MassModification that)
        {
            int minPrecision = Math.Min(Math.Min(Precision, that.Precision), MAX_PRECISION_TO_MATCH);
            double thisRound = Math.Round(Mass, minPrecision);
            double thatRound = Math.Round(that.Mass, minPrecision);
            if (Equals(thisRound, thatRound))
            {
                return true;
            }
            if (Math.Abs(Mass - that.Mass) < .0001)
            {
                // Anytime two modifications are within .0001 of each other, that is also
                // considered good enough since there are search results out there where
                // the Cysteine modification differs at that point.
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            string str = Mass.ToString(@"F0" + Precision, CultureInfo.InvariantCulture);
            if (Mass > 0)
            {
                str = @"+" + str;
            }
            return str;
        }

        protected bool Equals(MassModification other)
        {
            return Mass.Equals(other.Mass) && Precision == other.Precision;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((MassModification) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mass.GetHashCode() * 397) ^ Precision;
            }
        }

        private static readonly Regex REGEX_MASS_MODIFICATION = new Regex(@"^[+-]?[0-9]*[.,]?[0-9]*$");

        public static MassModification Parse(string strModification)
        {
            if (!REGEX_MASS_MODIFICATION.Match(strModification).Success)
            {
                return null;
            }
            double mass;
            int ichDot;
            string strDecimal;
            if (!double.TryParse(strModification, NumberStyles.Float, CultureInfo.InvariantCulture, out mass))
            {
                if (!double.TryParse(strModification, NumberStyles.Float, CultureInfo.CurrentCulture, out mass))
                {
                    return null;
                }
                else
                {
                    strDecimal = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
                    ichDot = strModification.IndexOf(strDecimal, StringComparison.CurrentCulture);
                }
            }
            else
            {
                strDecimal = CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator;
                ichDot = strModification.IndexOf(strDecimal, StringComparison.InvariantCulture);
            }
            if (ichDot < 0)
            {
                return new MassModification(mass, 0);
            }
            return new MassModification(mass, strModification.Length - ichDot - strDecimal.Length);
        }

        public static int InferPrecision(double modMass)
        {
            for (int precision = 1; precision < MAX_PRECISION_TO_KEEP; precision++)
            {
                if (Equals(modMass, Math.Round(modMass, precision)))
                {
                    return precision;
                }
            }
            return MAX_PRECISION_TO_KEEP;
        }

        public static readonly ImmutableList<double> POWERS_OF_TEN 
            = ImmutableList.ValueOf(new []{1.0, 10, 100, 1000, 10000, 100000, 1000000, 10000000});
        private static double Pow10(int power)
        {
            int absPower = Math.Abs(power);
            double value;
            if (absPower >= POWERS_OF_TEN.Count)
            {
                value = Math.Pow(10, absPower);
            }
            else
            {
                value = POWERS_OF_TEN[absPower];
            }
            return power < 0 ? 1.0 / value : value;
        }
    }
}
