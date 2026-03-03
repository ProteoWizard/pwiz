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
using pwiz.Common.CommonResources;

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
        public const int MAX_SIGNIFICANT_DIGITS = 17;

        public PrecisionNumber(int value) : this((decimal)value)
        {
        }

        public PrecisionNumber(double value) : this((decimal)value)
        {
        }

        public PrecisionNumber(decimal value) : this(value, MAX_SIGNIFICANT_DIGITS)
        {
        }

        private PrecisionNumber(decimal value, int significantDigits)
        {
            _log10 = (short)Math.Log10((double)value);
            _significantDigits = (short)Math.Min(Math.Max(significantDigits, 1), MAX_SIGNIFICANT_DIGITS);

            int decimalPlaces = _significantDigits - _log10;
            if (decimalPlaces > 28)
            {
                Value = value;
            }
            else if (decimalPlaces >= 0)
            {
                Value = Math.Round(value, decimalPlaces);
            }
            else
            {
                var pow10 = Pow10(-decimalPlaces);
                Value = Math.Round(value / pow10) * pow10;
            }
        }

        public PrecisionNumber ChangeSignificantDigits(int newSignificantDigits)
        {
            if (newSignificantDigits == SignificantDigits)
            {
                return this;
            }
            return new PrecisionNumber(Value, newSignificantDigits);
        }

        public PrecisionNumber ChangeDecimalPlaces(int newDecimalPlaces)
        {
            int newSignificantDigits = newDecimalPlaces + _log10;
            if (newSignificantDigits == SignificantDigits)
            {
                return this;
            }
            return new PrecisionNumber(Value, newSignificantDigits);
        }

        public static PrecisionNumber WithDecimalPlaces(decimal value, int decimalPlaces)
        {
            return new PrecisionNumber(value).ChangeDecimalPlaces(decimalPlaces);
        }

        public static PrecisionNumber WithSignificantDigits(decimal value, int significantDigits)
        {
            return new PrecisionNumber(value).ChangeSignificantDigits(significantDigits);
        }

        public decimal Value
        {
            get;
        }

        private readonly short _significantDigits;
        private readonly short _log10;
        public int DecimalPlaces
        {
            get { return _significantDigits - _log10; }
        }

        public int SignificantDigits
        {
            get { return _significantDigits; }
        }

        /// <summary>
        /// Half a unit in the last decimal place.
        /// For "3.14" (DecimalPlaces=2): 0.005
        /// For "300" (DecimalPlaces=0): 0.5
        /// </summary>
        public decimal Tolerance => 0.5m / Pow10(DecimalPlaces);

        private static decimal Pow10(int power)
        {
            if (power < 0)
            {
                return 1 / Pow10(-power);
            }
            return (decimal)Math.Pow(10, power);
        }

        public static PrecisionNumber Parse(string text, CultureInfo cultureInfo, bool explicitPrecision)
        {
            if (TryParse(text, cultureInfo, explicitPrecision, out var result))
                return result;
            throw new FormatException(string.Format(MessageResources.PrecisionNumber_Parse_Unable_to_parse___0___as_a_number, text));
        }

        public static PrecisionNumber Parse(string text)
        {
            return Parse(text, CultureInfo.CurrentCulture, false);
        }

        public static bool TryParse(string text, CultureInfo cultureInfo, bool defaultToFullPrecision, out PrecisionNumber result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(text))
                return false;

            text = text.Trim();

            if (!decimal.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, cultureInfo, out var value))
                return false;

            int decimalPlaces = CountDecimalPlaces(text, cultureInfo, defaultToFullPrecision);
            result = WithDecimalPlaces(value, decimalPlaces);
            return true;
        }

        public static bool TryParse(string text, CultureInfo cultureInfo, out PrecisionNumber result)
        {
            return TryParse(text, cultureInfo, false, out result);
        }

        private static int CountDecimalPlaces(string text, CultureInfo culture, bool defaultToFullPrecision)
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
            else if (defaultToFullPrecision)
            {
                return MAX_SIGNIFICANT_DIGITS;
            }

            // Find decimal separator in the mantissa
            int decimalIndex = mantissa.IndexOf(decimalSep, StringComparison.Ordinal);
            if (decimalIndex < 0)
                return -exponent; // No decimal point: e.g. "300" → 0, "15e2" → -2

            // Count digits after the decimal separator
            int digitsAfterDecimal = mantissa.Length - decimalIndex - decimalSep.Length;
            return digitsAfterDecimal - exponent;
        }

        public bool EqualsWithinPrecision(double other)
        {
            return other >= (double) (Value - Tolerance) && other <= (double) (Value + Tolerance);
        }

        public bool EqualsWithinPrecision(decimal other)
        {
            return other >= Value - Tolerance && other <= Value + Tolerance;
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

        public string ToString(IFormatProvider formatProvider, bool explicitPrecision)
        {
            if (explicitPrecision && DecimalPlaces == MAX_SIGNIFICANT_DIGITS)
            {
                return Value.ToString(formatProvider);
            }

            if (DecimalPlaces >= 0 && !explicitPrecision)
            {
                return Value.ToString(@"F" + DecimalPlaces, formatProvider);
            }

            // Negative decimal places: use scientific notation.
            // E.g. DecimalPlaces=-2, Value=1500 → "1.5e3"
            if (Value == 0)
            {
                return @"0e" + (-DecimalPlaces);
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs((double) Value)));
            int mantissaDecimals = DecimalPlaces + exponent;
            if (mantissaDecimals < 0)
                mantissaDecimals = 0;

            var mantissa = Value / Pow10(exponent);
            return mantissa.ToString(@"F" + mantissaDecimals, formatProvider) + @"e" + exponent;
        }

        public override string ToString()
        {
            return ToString(CultureInfo.CurrentCulture, false);
        }

        public int CompareTo(double value)
        {
            if (value > (double) decimal.MaxValue)
            {
                return -1;
            }
            if (value < (double) decimal.MinValue)
            {
                return 1;
            }

            return CompareTo((decimal)value);
        }

        public int CompareTo(decimal value)
        {
            if (EqualsWithinPrecision(value))
            {
                return 0;
            }
            return Value.CompareTo(value);
        }
    }
}
