/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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

using System.Globalization;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Cross-implementation diagnostic text formatting. Mirrors Rust's
    /// <c>osprey_core::diagnostics</c> module; the two implementations must
    /// produce byte-identical text for any given f64 input so diagnostic
    /// dumps can be compared via SHA-256 across runtimes.
    /// </summary>
    public static class Diagnostics
    {
        /// <summary>
        /// Format a double as the shortest decimal that round-trips back to
        /// the same f64 bits, matching Rust's <c>format!("{}", v)</c> output
        /// (<c>osprey_core::diagnostics::format_f64_roundtrip</c>). This is
        /// what every Stage 5+ cross-impl dump uses for numeric columns.
        ///
        /// Special values: <c>NaN</c> -> <c>"NaN"</c>; <c>+inf</c> ->
        /// <c>"inf"</c>; <c>-inf</c> -> <c>"-inf"</c>; both <c>+0</c> and
        /// <c>-0</c> -> <c>"0"</c> (normalizing away the sign so .NET's
        /// behaviour for signed zero doesn't leak into diffs).
        ///
        /// Finite non-zero values: bounded <c>G1..G17</c> shortest-roundtrip
        /// search (see <see cref="ShortestRoundTrip"/>) — try each precision
        /// in increasing order, return the first <c>v.ToString("Gp")</c>
        /// candidate that <c>double.Parse</c> accepts back to the original
        /// f64 bits. <c>G17</c> always round-trips by the IEEE 754 guarantee,
        /// so the loop terminates. Then expand any scientific form to fixed
        /// decimal (Rust's f64 Display never uses 'e' notation). The
        /// per-precision <c>G&lt;p&gt;</c> + parse-check loop is what gives
        /// shortest-roundtrip output on both net472 and net8.0; the prior
        /// "R" + G17 fallback emitted one digit more than ryu on
        /// .NET Framework 4.7.2 for many values that the parse-check loop
        /// gets right.
        /// </summary>
        public static string FormatF64Roundtrip(double v)
        {
            if (double.IsNaN(v)) return @"NaN";
            if (double.IsPositiveInfinity(v)) return @"inf";
            if (double.IsNegativeInfinity(v)) return @"-inf";
            // Covers +0 and -0 (C# treats them as equal under ==).
            if (v == 0.0) return @"0";

            var inv = CultureInfo.InvariantCulture;

            // Always use the shortest-roundtrip search rather than "R", to
            // match Rust's ryu output exactly. On .NET Framework 4.7.2,
            // "R" round-trips for most values but often produces one
            // digit more than ryu (e.g. ryu emits "12.50611897910133",
            // "R" emits "12.506118979101331"). The G&lt;p&gt; loop below
            // is bounded at 17 iterations per value and only fires for
            // diagnostic dumps gated by env vars, so the cost is bounded
            // and not on any production hot path.
            string s = ShortestRoundTrip(v, inv);
            return ExpandScientificToFixed(s, inv);
        }

        /// <summary>
        /// Find the shortest digit count that still round-trips for this
        /// double, mirroring Rust's ryu output. .NET Framework 4.7.2's
        /// "R" formatter is round-trip-correct on most values but typically
        /// emits one digit more than ryu does; the G&lt;p&gt; loop below
        /// finds the actual minimum precision needed.
        /// </summary>
        private static string ShortestRoundTrip(double v, CultureInfo inv)
        {
            // Try G1 through G17. G17 is guaranteed to round-trip by IEEE
            // 754; we hit it last only if every shorter precision fails.
            for (int p = 1; p <= 17; p++)
            {
                string candidate = v.ToString(@"G" + p.ToString(inv), inv);
                if (double.Parse(candidate, NumberStyles.Float, inv) == v)
                    return candidate;
            }
            // Pathological: G17 always round-trips by IEEE 754 guarantee;
            // this branch should be unreachable.
            return v.ToString(@"G17", inv);
        }

        /// <summary>
        /// Convert a G17 output that may be in scientific form (e.g.,
        /// <c>"4.1292833941950792E-121"</c>) into its equivalent fixed
        /// decimal form (<c>"0.000...04129283394195079"</c>). Rust's f64
        /// Display never emits 'e' notation, so the C# diagnostic dumps
        /// must not either. No-op if the input has no <c>E</c>/<c>e</c>.
        /// </summary>
        private static string ExpandScientificToFixed(string s, CultureInfo inv)
        {
            int eIdx = -1;
            for (int i = 0; i < s.Length; i++)
                if (s[i] == 'E' || s[i] == 'e') { eIdx = i; break; }
            if (eIdx < 0) return s;

            string mantissa = s.Substring(0, eIdx);
            int expo = int.Parse(s.Substring(eIdx + 1), inv);

            bool neg = mantissa.Length > 0 && mantissa[0] == '-';
            int mStart = 0;
            if (mantissa.Length > 0 && (mantissa[0] == '-' || mantissa[0] == '+')) mStart = 1;
            string mBody = mantissa.Substring(mStart);

            int dotPos = mBody.IndexOf('.');
            string digits;
            int intDigits; // digits before the decimal point in the mantissa
            if (dotPos < 0)
            {
                digits = mBody;
                intDigits = digits.Length;
            }
            else
            {
                digits = mBody.Substring(0, dotPos) + mBody.Substring(dotPos + 1);
                intDigits = dotPos;
            }

            int newPoint = intDigits + expo;
            string sign = neg ? @"-" : string.Empty;

            if (newPoint <= 0)
                return sign + @"0." + new string('0', -newPoint) + digits;
            if (newPoint >= digits.Length)
                return sign + digits + new string('0', newPoint - digits.Length);
            return sign + digits.Substring(0, newPoint) + @"." + digits.Substring(newPoint);
        }

    }
}
