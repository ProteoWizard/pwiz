/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
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

namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// .NET Framework and .NET 8 format the round-trip ("R") specifier for <see cref="double"/> and
    /// <see cref="float"/> differently. .NET Framework's "R" emitted 15 significant digits for a double
    /// (7 for a float), falling back to 17 (9) only when the shorter form failed to round-trip; .NET 8's
    /// "R" emits the shortest round-trippable form, which is 16 (8) digits for some values where the
    /// framework produced 17 (9) -- e.g. 742.90069580078125 formats as "742.9006958007812" on net8 but
    /// "742.90069580078125" on net472.
    ///
    /// Skyline writes numbers in invariant language with the "R" format in two places that a test (and a
    /// user copying from a grid) expects to agree: the report file (via <see cref="DsvWriter"/>) and the
    /// grid preview (via the databound grid's cell formatting). This helper reproduces the .NET Framework
    /// algorithm on net8 so both paths match the historical, net472-generated output. On net472 it is a
    /// no-op because the framework already formats "R" this way.
    /// </summary>
    public static class RoundTripFormat
    {
        /// <summary>
        /// If <paramref name="formatString"/> is the round-trip specifier ("R"/"r") and
        /// <paramref name="value"/> is a <see cref="double"/> or <see cref="float"/>, returns the value
        /// formatted the way .NET Framework's "R" would have. Otherwise (any other format, any other
        /// type, or on net472) returns null, signaling the caller to format the value normally.
        /// </summary>
        public static string FormatOrNull(object value, string formatString, IFormatProvider provider)
        {
#if NET472
            return null;
#else
            if (formatString != @"R" && formatString != @"r")
            {
                return null;
            }
            if (value is double d)
            {
                return FrameworkRoundTrip(d, provider);
            }
            if (value is float f)
            {
                return FrameworkRoundTrip(f, provider);
            }
            return null;
#endif
        }

#if !NET472
        private static string FrameworkRoundTrip(double value, IFormatProvider provider)
        {
            var g15 = value.ToString(@"G15", provider);
            // .NET Framework performed the "R" round-trip check with its own decimal->double conversion,
            // which was accurate only to within one ULP. .NET 8's parser is correctly rounded, so a strict
            // equality check rejects some 15-digit forms that .NET Framework accepted -- e.g. .NET Framework
            // kept "500.477375" for the double whose shortest net8 form is 500.47737500000005. Accept the
            // shorter G15 form when it lands within one ULP of the value so the emulated output matches the
            // historical net472 string. (Values that need 16-17 digits, such as exact binary fractions, are
            // two or more ULPs from their G15 form and still correctly escalate to G17.)
            if (double.TryParse(g15, NumberStyles.Float, provider, out var roundTripped) && WithinOneUlp(value, roundTripped))
            {
                return g15;
            }
            return value.ToString(@"G17", provider);
        }

        /// <summary>
        /// Returns true if <paramref name="candidate"/> is equal to or an adjacent representable double of
        /// <paramref name="value"/> (i.e. within one ULP). The Int64 bit patterns of same-signed doubles are
        /// ordered, so adjacent values differ by exactly one; opposite signs are never treated as adjacent.
        /// </summary>
        private static bool WithinOneUlp(double value, double candidate)
        {
            if (candidate == value)
            {
                return true;
            }
            if (double.IsNaN(value) || double.IsNaN(candidate) ||
                double.IsInfinity(value) || double.IsInfinity(candidate))
            {
                return false;
            }
            var valueBits = BitConverter.DoubleToInt64Bits(value);
            var candidateBits = BitConverter.DoubleToInt64Bits(candidate);
            if ((valueBits < 0) != (candidateBits < 0))
            {
                return false;
            }
            return Math.Abs(valueBits - candidateBits) <= 1;
        }

        private static string FrameworkRoundTrip(float value, IFormatProvider provider)
        {
            var g7 = value.ToString(@"G7", provider);
            if (float.TryParse(g7, NumberStyles.Float, provider, out var roundTripped) && roundTripped == value)
            {
                return g7;
            }
            return value.ToString(@"G9", provider);
        }
#endif
    }
}
