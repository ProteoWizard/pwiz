/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Represents a parsed number with its detected decimal precision.
    /// When a user types "3.14" in a filter, this detects 2 decimal places
    /// and provides tolerance-aware comparisons so that values rounding to 3.14
    /// at 2 decimal places will match.
    /// </summary>
    public struct PrecisionNumber : IEquatable<PrecisionNumber>, IComparable<PrecisionNumber>
    {
        // Matches optional sign, digits, optional decimal part, optional exponent
        private static readonly Regex SCIENTIFIC_REGEX = new Regex(
            @"^[+-]?(\d+)(\.(\d+))?([eE][+-]?\d+)?$",
            RegexOptions.Compiled);

        public PrecisionNumber(double value, int decimalPlaces)
        {
            Value = value;
            DecimalPlaces = decimalPlaces;
        }

        public double Value { get; }
        public int DecimalPlaces { get; }

        /// <summary>
        /// Half a unit in the last decimal place.
        /// For "3.14" (DecimalPlaces=2): 0.005
        /// For "300" (DecimalPlaces=0): 0.5
        /// </summary>
        public double Tolerance
        {
            get { return 0.5 * Math.Pow(10, -DecimalPlaces); }
        }

        public static PrecisionNumber Parse(string text)
        {
            if (TryParse(text, out var result))
                return result;
            throw new FormatException(string.Format(@"Unable to parse '{0}' as a PrecisionNumber", text));
        }

        public static bool TryParse(string text, out PrecisionNumber result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            // Try InvariantCulture first, then CurrentCulture
            if (TryParseWithCulture(text, CultureInfo.InvariantCulture, out result))
                return true;
            if (!Equals(CultureInfo.CurrentCulture, CultureInfo.InvariantCulture) &&
                TryParseWithCulture(text, CultureInfo.CurrentCulture, out result))
                return true;

            return false;
        }

        private static bool TryParseWithCulture(string text, CultureInfo culture, out PrecisionNumber result)
        {
            result = default;

            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, culture, out double value))
                return false;

            int decimalPlaces = CountDecimalPlaces(text, culture);
            result = new PrecisionNumber(value, decimalPlaces);
            return true;
        }

        private static int CountDecimalPlaces(string text, CultureInfo culture)
        {
            // Normalize: replace culture decimal separator with '.' for regex matching
            string decimalSep = culture.NumberFormat.NumberDecimalSeparator;
            string normalized = text.Replace(decimalSep, @".");

            var match = SCIENTIFIC_REGEX.Match(normalized);
            if (!match.Success)
                return 0;

            int mantissaDecimalDigits = match.Groups[3].Success ? match.Groups[3].Value.Length : 0;

            if (match.Groups[4].Success)
            {
                // Scientific notation: e.g. "1.5e3" has mantissa decimals=1, exponent=3
                // Effective decimal places = mantissa decimals - exponent
                string exponentStr = match.Groups[4].Value.Substring(1); // Remove 'e' or 'E'
                int exponent = int.Parse(exponentStr, CultureInfo.InvariantCulture);
                return mantissaDecimalDigits - exponent;
            }

            return mantissaDecimalDigits;
        }

        /// <summary>
        /// Returns true if the given value is within the precision range of this number.
        /// For "3.14" (Tolerance=0.005), this returns true for values in [3.135, 3.145).
        /// </summary>
        public bool EqualsWithinPrecision(double value)
        {
            return Math.Abs(value - Value) < Tolerance;
        }

        #region IEquatable, IComparable, operators

        public bool Equals(PrecisionNumber other)
        {
            return Value.Equals(other.Value) && DecimalPlaces == other.DecimalPlaces;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is PrecisionNumber other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ DecimalPlaces;
            }
        }

        public int CompareTo(PrecisionNumber other)
        {
            int cmp = Value.CompareTo(other.Value);
            if (cmp != 0) return cmp;
            return DecimalPlaces.CompareTo(other.DecimalPlaces);
        }

        public static bool operator ==(PrecisionNumber left, PrecisionNumber right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PrecisionNumber left, PrecisionNumber right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(PrecisionNumber left, PrecisionNumber right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(PrecisionNumber left, PrecisionNumber right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(PrecisionNumber left, PrecisionNumber right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(PrecisionNumber left, PrecisionNumber right)
        {
            return left.CompareTo(right) >= 0;
        }

        #endregion

        public override string ToString()
        {
            return Value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
