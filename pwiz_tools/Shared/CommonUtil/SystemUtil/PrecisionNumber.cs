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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Represents a parsed number with its detected decimal precision.
    /// When a user types "3.14" in a filter, this detects 2 decimal places
    /// and provides tolerance-aware comparisons so that values rounding to 3.14
    /// at 2 decimal places will match.
    /// </summary>
    public readonly struct PrecisionNumber : IEquatable<PrecisionNumber>, IFormattable
    {
        /// <summary>
        /// Maximum decimal places used when wrapping a raw double that has no
        /// known textual precision. This gives an effectively zero tolerance.
        /// </summary>
        private const int MAX_DECIMAL_PLACES = 15;

        public PrecisionNumber(double value, int decimalPlaces)
        {
            Value = value;
            DecimalPlaces = decimalPlaces;
        }

        /// <summary>
        /// Wraps a raw double with maximum precision (effectively zero tolerance).
        /// Use this for column values that have no known textual precision.
        /// </summary>
        public PrecisionNumber(double value) : this(value, MAX_DECIMAL_PLACES)
        {
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

        public static PrecisionNumber Parse(string text, CultureInfo cultureInfo, bool scientificPrecisionOnly)
        {
            if (TryParse(text, cultureInfo, scientificPrecisionOnly, out var result))
                return result;
            throw new FormatException(string.Format(@"Unable to parse '{0}' as a PrecisionNumber", text));
        }

        public static PrecisionNumber Parse(string text, CultureInfo cultureInfo)
        {
            return Parse(text, cultureInfo, false);
        }

        public static bool TryParse(string text, CultureInfo cultureInfo, bool scientificPrecisionOnly, out PrecisionNumber result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, cultureInfo, out double value))
                return false;

            int decimalPlaces = CountDecimalPlaces(text, cultureInfo, scientificPrecisionOnly);
            result = new PrecisionNumber(value, decimalPlaces);
            return true;
        }

        public static bool TryParse(string text, CultureInfo cultureInfo, out PrecisionNumber result)
        {
            return TryParse(text, cultureInfo, false, out result);
        }

        private static int CountDecimalPlaces(string text, CultureInfo culture, bool scientificPrecisionOnly)
        {
            string decimalSep = culture.NumberFormat.NumberDecimalSeparator;

            // Find the exponent part (e or E) if present
            int exponentIndex = text.IndexOfAny(new[] { 'e', 'E' });
            int exponent = 0;
            string mantissa = text;
            if (exponentIndex >= 0)
            {
                mantissa = text.Substring(0, exponentIndex);
                if (int.TryParse(text.Substring(exponentIndex + 1), NumberStyles.AllowLeadingSign,
                        CultureInfo.InvariantCulture, out int exp))
                {
                    exponent = exp;
                }
            }

            if (scientificPrecisionOnly)
            {
                return MAX_DECIMAL_PLACES;
            }

            // Find decimal separator in the mantissa
            int decimalIndex = mantissa.IndexOf(decimalSep, StringComparison.Ordinal);
            if (decimalIndex < 0)
                return -exponent; // No decimal point: e.g. "300" → 0, "15e2" → -2

            // Count digits after the decimal separator
            int digitsAfterDecimal = mantissa.Length - decimalIndex - decimalSep.Length;
            return digitsAfterDecimal - exponent;
        }

        /// <summary>
        /// Returns true if this number and the other are equal within the lower
        /// precision of the two. The effective tolerance is the larger of the two
        /// tolerances, so the less-precise number dominates the comparison.
        /// </summary>
        public bool EqualsWithinPrecision(double other)
        {
            return Math.Abs(Value - other) <= Tolerance;
        }

        public bool Equals(PrecisionNumber other)
        {
            return Value.Equals(other.Value) && DecimalPlaces == other.DecimalPlaces;
        }

        public override bool Equals(object obj)
        {
            return obj is PrecisionNumber other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ DecimalPlaces;
            }
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return ToString(formatProvider, true);
        }

        public string ToString(IFormatProvider formatProvider, bool scientificPrecisionOnly)
        {
            if (DecimalPlaces >= 0 && !scientificPrecisionOnly)
            {
                return Value.ToString(@"F" + DecimalPlaces, formatProvider);
            }

            // Negative decimal places: use scientific notation.
            // E.g. DecimalPlaces=-2, Value=1500 → "1.5e3"
            if (Value == 0)
            {
                return @"0e" + (-DecimalPlaces);
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs(Value)));
            int mantissaDecimals = DecimalPlaces + exponent;
            if (mantissaDecimals < 0)
                mantissaDecimals = 0;

            double mantissa = Value / Math.Pow(10, exponent);
            return mantissa.ToString(@"F" + mantissaDecimals, formatProvider) + @"e" + exponent;
        }

        public string ToString(CultureInfo cultureInfo)
        {
            return ToString(null, cultureInfo);
        }

        public override string ToString()
        {
            return ToString(CultureInfo.CurrentCulture);
        }

        public int CompareTo(double value)
        {
            if (EqualsWithinPrecision(value))
            {
                return 0;
            }
            return Value.CompareTo(value);
        }
    }
}
