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
        public static readonly PrecisionNumber NAN = new PrecisionNumber(0, short.MaxValue, 0);
        public static readonly PrecisionNumber POSITIVE_INFINITY = new PrecisionNumber(0, short.MaxValue, 1);
        public static readonly PrecisionNumber NEGATIVE_INFINITY = new PrecisionNumber(0, short.MaxValue, -1);
        public static readonly PrecisionNumber MAX_VALUE = new PrecisionNumber(7e28m, 28, 1);
        public static readonly PrecisionNumber MIN_VALUE = new PrecisionNumber(-7e28m, 28, 1);

        public static readonly double MAX_DOUBLE =
            BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(MAX_VALUE.ToDouble()) - 1);
        public static readonly double MIN_DOUBLE =
            BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(MIN_VALUE.ToDouble()) - 1);

        public PrecisionNumber(decimal value) : this(WithSignificantDigits(value, MAX_SIGNIFICANT_DIGITS))
        {
        }

        private PrecisionNumber(PrecisionNumber other)
        {
            Value = other.Value;
            _magnitude = other._magnitude;
            _significantDigits = other._significantDigits;
        }

        private PrecisionNumber(decimal value, short magnitude, short significantDigits)
        {
            Value = value;
            _magnitude = magnitude;
            _significantDigits = significantDigits;
        }

        public PrecisionNumber ChangeSignificantDigits(int newSignificantDigits)
        {
            if (!IsFinite)
            {
                return this;
            }
            if (newSignificantDigits == SignificantDigits)
            {
                return this;
            }
            return WithSignificantDigits(Value, newSignificantDigits);
        }

        public PrecisionNumber ChangeDecimalPlaces(int newDecimalPlaces)
        {
            int newSignificantDigits = newDecimalPlaces + _magnitude + 1;
            if (newSignificantDigits == SignificantDigits)
            {
                return this;
            }
            return WithSignificantDigits(Value, newSignificantDigits);
        }

        public static PrecisionNumber WithDecimalPlaces(decimal value, int decimalPlaces)
        {
            return new PrecisionNumber(value).ChangeDecimalPlaces(decimalPlaces);
        }

        public static PrecisionNumber WithSignificantDigits(decimal value, int significantDigits)
        {
            if (value < MIN_VALUE.Value)
            {
                return NEGATIVE_INFINITY;
            }

            if (value > MAX_VALUE.Value)
            {
                return POSITIVE_INFINITY;
            }
            double absValue = Math.Abs((double)value);
            var log10 = absValue == 0 ? double.NaN : Math.Log10(absValue);
            if (double.IsInfinity(log10) || double.IsNaN(log10))
            {
                return new PrecisionNumber(0, 0, (short)Math.Min(Math.Max(0, significantDigits), MAX_SIGNIFICANT_DIGITS));
            }
            var floorLog10 = (short)Math.Floor(log10);
            var sigFigs = (short)Math.Min(Math.Max(significantDigits, 1), MAX_SIGNIFICANT_DIGITS);
            int decimalPlaces = sigFigs - floorLog10 - 1;
            decimal roundedValue;
            if (decimalPlaces > 28)
            {
                roundedValue = value;
            }
            else if (decimalPlaces >= 0)
            {
                roundedValue = Math.Round(value, decimalPlaces);
            }
            else
            {
                var pow10 = Pow10(-decimalPlaces);
                roundedValue = Math.Round(value / pow10) * pow10;
            }

            return new PrecisionNumber(roundedValue, floorLog10, sigFigs);
        }

        public static PrecisionNumber FromDouble(double value)
        {
            if (double.IsNaN(value))
            {
                return NAN;
            }
            if (value <= (double) decimal.MinValue)
            {
                return NEGATIVE_INFINITY;
            }
            if (value >= (double) decimal.MaxValue)
            {
                return POSITIVE_INFINITY;
            }

            return WithSignificantDigits((decimal)value, MAX_SIGNIFICANT_DIGITS);
        }

        public decimal Value
        {
            get;
        }

        private readonly short _significantDigits;
        /// <summary>
        /// Magnitude of the most significant digit, e.g. 0 for "3.14", 2 for "300", and -3 for ".004".
        /// </summary>
        private readonly short _magnitude;
        /// <summary>
        /// Number of significant digits to the right of the decimal point.
        /// Can be negative if the least significant digit is to the left of the decimal point.
        /// </summary>
        public int DecimalPlaces
        {
            get { return _significantDigits - _magnitude - 1; }
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
        public decimal Tolerance
        {
            get
            {
                if (IsFinite)
                {
                    if (DecimalPlaces >= 28)
                    {
                        return 1e-28m;
                    }
                    return .5m / Pow10(DecimalPlaces);
                }

                return 0;
            }
        }

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
            {
                if (double.TryParse(text, NumberStyles.Float, cultureInfo, out var doubleValue))
                {
                    if (double.IsNaN(doubleValue))
                    {
                        result = NAN;
                        return true;
                    }

                    if (doubleValue > (double) decimal.MaxValue)
                    {
                        result = POSITIVE_INFINITY;
                        return true;
                    }

                    if (doubleValue < (double) decimal.MinValue)
                    {
                        result = NEGATIVE_INFINITY;
                        return true;
                    }
                }
                return false;
            }

            int decimalPlaces = CountDecimalPlaces(text, cultureInfo, defaultToFullPrecision);
            result = WithDecimalPlaces(value, decimalPlaces);
            return true;
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
            if (IsFinite)
            {
                if (other <= (double)decimal.MaxValue && other >= (double)decimal.MinValue)
                {
                    return EqualsWithinPrecision((decimal)other);
                }
                return other >= (double)(Value - Tolerance) && other <= (double)(Value + Tolerance);
            }

            return Equals(ToDouble(), other);
        }

        public bool EqualsWithinPrecision(decimal other)
        {
            return other >= Value - Tolerance && other <= Value + Tolerance;
        }

        public bool Equals(PrecisionNumber other)
        {
            return Value.Equals(other.Value) && _significantDigits == other._significantDigits;
        }

        public override bool Equals(object obj)
        {
            return obj is PrecisionNumber other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Value.GetHashCode() * 397) ^ _significantDigits;
            }
        }

        string IFormattable.ToString(string format, IFormatProvider formatProvider)
        {
            return ToString(formatProvider, true);
        }

        public string ToString(IFormatProvider formatProvider, bool explicitPrecision)
        {
            if (!IsFinite)
            {
                return ToDouble().ToString(null, formatProvider);
            }
            if (explicitPrecision && DecimalPlaces == MAX_SIGNIFICANT_DIGITS)
            {
                return Value.ToString(formatProvider);
            }

            if (DecimalPlaces >= 0 && !explicitPrecision && _magnitude > -1)
            {
                return Value.ToString(@"F" + DecimalPlaces, formatProvider);
            }

            // Negative decimal places: use scientific notation.
            // E.g. DecimalPlaces=-2, Value=1500 → "1.5e3"
            if (Value == 0)
            {
                return @"0E" + (-DecimalPlaces);
            }

            int exponent = (int)Math.Floor(Math.Log10(Math.Abs((double) Value)));
            int mantissaDecimals = DecimalPlaces + exponent;
            if (mantissaDecimals < 0)
                mantissaDecimals = 0;

            var mantissa = Value / Pow10(exponent);
            var mantissaString = mantissa.ToString(@"F" + mantissaDecimals, formatProvider);
            if (exponent >= 0)
            {
                return mantissaString + @"E+" + exponent;
            }
            else
            {
                return mantissaString + @"E" + exponent;
            }
        }

        public override string ToString()
        {
            return ToString(CultureInfo.CurrentCulture, SignificantDigits == MAX_SIGNIFICANT_DIGITS);
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

        public bool IsFinite
        {
            get { return _magnitude != NAN._magnitude; }
        }

        public double ToDouble()
        {
            if (IsFinite)
            {
                return (double)Value;
            }

            if (Equals(NEGATIVE_INFINITY))
            {
                return double.NegativeInfinity;
            }

            if (Equals(POSITIVE_INFINITY))
            {
                return double.PositiveInfinity;
            }

            return double.NaN;
        }
    }
}
