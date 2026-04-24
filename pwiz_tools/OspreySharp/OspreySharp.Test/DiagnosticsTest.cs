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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Tests for pwiz.OspreySharp.Core.Diagnostics helpers. Mirrors
    /// osprey_core::diagnostics::tests on the Rust side -- the two
    /// implementations must produce byte-identical text for any given f64.
    /// </summary>
    [TestClass]
    public class DiagnosticsTest
    {
        [TestMethod]
        public void TestFormatF64RoundtripSpecialValues()
        {
            // Mirrors Rust's format_f64_roundtrip_handles_special_values.
            Assert.AreEqual("NaN", Diagnostics.FormatF64Roundtrip(double.NaN));
            Assert.AreEqual("inf", Diagnostics.FormatF64Roundtrip(double.PositiveInfinity));
            Assert.AreEqual("-inf", Diagnostics.FormatF64Roundtrip(double.NegativeInfinity));
            Assert.AreEqual("0", Diagnostics.FormatF64Roundtrip(0.0));
            // -0.0 must not render as "-0", since we normalize signed zero
            // so that Rust's and .NET's behaviour can't leak into diffs.
            Assert.AreEqual("0", Diagnostics.FormatF64Roundtrip(-0.0));
            Assert.AreEqual("1.5", Diagnostics.FormatF64Roundtrip(1.5));
            Assert.AreEqual("-1.5", Diagnostics.FormatF64Roundtrip(-1.5));
        }

        [TestMethod]
        public void TestFormatF64RoundtripIsShortest()
        {
            // The value 5.598374948209159 came out of a real Stellar Stage 5
            // standardizer dump: Rust ryu emits 16 significant digits (no
            // trailing "9" that G17 would pad on). On .NET Core 3+/.NET 5+,
            // "R" produces the same short form, so the helper returns
            // exactly "5.598374948209159". On .NET Framework 4.7.2, "R" is
            // historically 17 digits for this value; we only require the
            // helper's output to round-trip (length allowed to vary).
            double v = 5.598374948209159;
            string s = Diagnostics.FormatF64Roundtrip(v);
            double parsed = double.Parse(s, CultureInfo.InvariantCulture);
            Assert.AreEqual(v, parsed, "FormatF64Roundtrip must round-trip: got {0}", s);
#if NETCOREAPP || NET5_0_OR_GREATER
            Assert.AreEqual("5.598374948209159", s,
                ".NET Core 3+ should emit ryu-shortest byte-matching Rust");
#endif
        }

        [TestMethod]
        public void TestFormatF64RoundtripIntegerValues()
        {
            // Rust's ryu elides the decimal point for integer-valued f64,
            // e.g. 1.0 -> "1", 100.0 -> "100". The C# helper must match.
            Assert.AreEqual("0", Diagnostics.FormatF64Roundtrip(0.0));
            Assert.AreEqual("1", Diagnostics.FormatF64Roundtrip(1.0));
            Assert.AreEqual("-1", Diagnostics.FormatF64Roundtrip(-1.0));
            Assert.AreEqual("100", Diagnostics.FormatF64Roundtrip(100.0));
        }

        [TestMethod]
        public void TestFormatF64RoundtripMatchesRustOnRealStdsValue()
        {
            // Real Stellar Stage 5 peak_sharpness std value; Rust's ryu
            // emits this as "6354533.401694356" (16 sig digits). On .NET
            // Core 3+, ToString("R") matches byte-for-byte. On .NET
            // Framework 4.7.2, "R" falls back to G17 (17 digits) because
            // of the long-standing "R" bug. We only require round-trip
            // there; the Compare-Stage5 harness defaults to net8.0 to
            // get byte-identity with Rust.
            double v = System.BitConverter.Int64BitsToDouble(0x41583D9959B55C3F);
            string s = Diagnostics.FormatF64Roundtrip(v);
            Assert.AreEqual(v, double.Parse(s, CultureInfo.InvariantCulture),
                "FormatF64Roundtrip must round-trip: got {0}", s);
#if NETCOREAPP || NET5_0_OR_GREATER
            Assert.AreEqual("6354533.401694356", s,
                ".NET Core 3+ should emit ryu-shortest byte-matching Rust");
#endif
        }

        [TestMethod]
        public void TestFormatF64RoundtripPreservesEveryBit()
        {
            // Cover a handful of near-boundary values that tend to expose
            // classic "R" / G17 roundtrip bugs on .NET Framework.
            double[] samples = {
                0.1, 0.2, 0.3, // repeating-binary classics
                0.0005,        // 1e-4 boundary (below which C# G<p> flips to scientific)
                615245.6525365515,
                -0.01825951711444074,
                0.13212324912096937,
            };
            foreach (double v in samples)
            {
                string s = Diagnostics.FormatF64Roundtrip(v);
                double parsed = double.Parse(s, CultureInfo.InvariantCulture);
                Assert.AreEqual(v, parsed,
                    "FormatF64Roundtrip({0}) returned {1} which did not round-trip", v, s);
            }
        }
    }
}
