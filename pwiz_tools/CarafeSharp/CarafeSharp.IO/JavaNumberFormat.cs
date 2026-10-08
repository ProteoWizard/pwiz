/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Text;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// The number text Carafe's Java writes into its library TSV, as Java 19 and later produce
    /// it (Carafe runs on Java 25):
    /// <list type="bullet">
    /// <item><c>Double.toString</c> and <c>Float.toString</c>: the shortest decimal that reads
    /// back as the same value (.NET's round-trip digits), laid out plainly with at least one
    /// fractional digit for magnitudes in [1e-3, 1e7) and as <c>d.dddE-n</c> otherwise.</item>
    /// <item><c>String.format("%.nf", x)</c>: those shortest digits rounded half up at the n-th
    /// decimal, which differs from rounding the binary value only when the digits end in a 5
    /// exactly there.</item>
    /// </list>
    /// </summary>
    public static class JavaNumberFormat
    {
        /// <summary><c>Double.toString(double)</c>.</summary>
        public static string ToString(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value == 0)
                return Special(value);
            ShortestDigits(Math.Abs(value).ToString(@"R", CultureInfo.InvariantCulture), out string digits, out int pointPosition);
            return Layout(value < 0, digits, pointPosition, Math.Abs(value));
        }

        /// <summary><c>Float.toString(float)</c>.</summary>
        public static string ToString(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value == 0)
                return Special(value);
            ShortestDigits(Math.Abs(value).ToString(@"R", CultureInfo.InvariantCulture), out string digits, out int pointPosition);
            return Layout(value < 0, digits, pointPosition, Math.Abs(value));
        }

        /// <summary><c>String.format("%.{decimals}f", value)</c>, with the invariant culture.</summary>
        public static string FormatFixed(double value, int decimals)
        {
            if (double.IsNaN(value))
                return @"NaN";
            if (double.IsInfinity(value))
                return value > 0 ? @"Infinity" : @"-Infinity";
            bool negative = value < 0 || (value == 0 && double.IsNegative(value));
            var result = new StringBuilder(24);
            if (negative)
                result.Append('-');
            if (value == 0)
            {
                result.Append('0');
                AppendFraction(result, string.Empty, decimals);
                return result.ToString();
            }
            ShortestDigits(Math.Abs(value).ToString(@"R", CultureInfo.InvariantCulture), out string digits, out int pointPosition);
            // Keep the digits up to the requested decimal, rounding half up on the next one.
            int keep = pointPosition + decimals;
            if (keep < 0)
            {
                digits = string.Empty;
                pointPosition = 0;
            }
            else if (keep < digits.Length)
            {
                bool roundUp = digits[keep] >= '5';
                digits = digits.Substring(0, keep);
                if (roundUp)
                    digits = Increment(digits, ref pointPosition);
            }
            string integerPart = pointPosition <= 0 ? @"0" : digits.Substring(0, Math.Min(pointPosition, digits.Length)).PadRight(pointPosition, '0');
            string fractionPart = pointPosition >= digits.Length ? string.Empty
                : pointPosition < 0 ? new string('0', -pointPosition) + digits : digits.Substring(pointPosition);
            result.Append(integerPart);
            AppendFraction(result, fractionPart, decimals);
            return result.ToString();
        }

        private static string Special(double value)
        {
            if (double.IsNaN(value))
                return @"NaN";
            if (double.IsInfinity(value))
                return value > 0 ? @"Infinity" : @"-Infinity";
            return double.IsNegative(value) ? @"-0.0" : @"0.0";
        }

        /// <summary>
        /// Splits .NET round-trip text of a positive number into its significant digits (no
        /// leading or trailing zeros) and the position of the decimal point relative to them.
        /// </summary>
        private static void ShortestDigits(string text, out string digits, out int pointPosition)
        {
            int exponent = 0;
            int e = text.IndexOfAny(new[] { 'E', 'e' });
            if (e >= 0)
            {
                exponent = int.Parse(text.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                text = text.Substring(0, e);
            }
            int point = text.IndexOf('.');
            string integerDigits = point < 0 ? text : text.Substring(0, point);
            string fractionDigits = point < 0 ? string.Empty : text.Substring(point + 1);
            string all = integerDigits + fractionDigits;
            int position = integerDigits.Length + exponent;
            int leading = 0;
            while (leading < all.Length - 1 && all[leading] == '0')
                leading++;
            all = all.Substring(leading);
            position -= leading;
            digits = all.TrimEnd('0');
            if (digits.Length == 0)
                digits = @"0";
            pointPosition = position;
        }

        private static string Layout(bool negative, string digits, int pointPosition, double magnitude)
        {
            var result = new StringBuilder(24);
            if (negative)
                result.Append('-');
            if (magnitude >= 1e-3 && magnitude < 1e7)
            {
                if (pointPosition <= 0)
                {
                    result.Append(@"0.").Append('0', -pointPosition).Append(digits);
                }
                else if (pointPosition >= digits.Length)
                {
                    result.Append(digits).Append('0', pointPosition - digits.Length).Append(@".0");
                }
                else
                {
                    result.Append(digits, 0, pointPosition).Append('.').Append(digits, pointPosition, digits.Length - pointPosition);
                }
            }
            else
            {
                result.Append(digits[0]).Append('.');
                result.Append(digits.Length > 1 ? digits.Substring(1) : @"0");
                result.Append('E').Append((pointPosition - 1).ToString(CultureInfo.InvariantCulture));
            }
            return result.ToString();
        }

        /// <summary>Adds one unit in the last place of <paramref name="digits"/>, carrying.</summary>
        private static string Increment(string digits, ref int pointPosition)
        {
            var chars = digits.ToCharArray();
            int i = chars.Length - 1;
            while (i >= 0)
            {
                if (chars[i] != '9')
                {
                    chars[i]++;
                    return new string(chars);
                }
                chars[i] = '0';
                i--;
            }
            pointPosition++;
            return @"1" + new string(chars);
        }

        private static void AppendFraction(StringBuilder result, string fraction, int decimals)
        {
            if (decimals <= 0)
                return;
            result.Append('.');
            if (fraction.Length >= decimals)
                result.Append(fraction, 0, decimals);
            else
                result.Append(fraction).Append('0', decimals - fraction.Length);
        }
    }
}
