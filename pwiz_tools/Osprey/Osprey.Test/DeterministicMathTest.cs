/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="DeterministicMath"/>: the IEEE special values, agreement with
    /// <see cref="Math"/> to within one ulp, a hash of the result bits over a large input
    /// grid, and the OSPREY_DETERMINISTIC_MATH switch between the two arms. The fdlibm arm is
    /// passed explicitly throughout, so the test means the same thing whatever the switch says
    /// in the test process. The hash is what enforces the class's contract: it was captured on Windows and
    /// verified on Linux, so any platform, framework or JIT that rounds one result differently
    /// (a fused multiply-add, a different reduction) fails here rather than silently moving a
    /// trained model.
    /// </summary>
    [TestClass]
    public class DeterministicMathTest
    {
        private const int GRID_POINTS = 200000;

        // Hashes of the result bits over the grids below, identical on Windows and Linux.
        private const long EXP_BITS_HASH = unchecked((long)0x8DF6D1F30CCD7CA8);
        private const long LOG_BITS_HASH = unchecked((long)0xB2C602A2BDF9906E);

        [TestMethod]
        public void TestDeterministicMath()
        {
            AssertExpSpecialValues();
            AssertLogSpecialValues();
            AssertWithinOneUlpOfSystemMath();
            AssertResultBitsPinned();
            AssertSwitchSelectsArm();
        }

        private static void AssertExpSpecialValues()
        {
            Assert.IsTrue(double.IsNaN(ExpFdlibm(double.NaN)));
            Assert.AreEqual(double.PositiveInfinity, ExpFdlibm(double.PositiveInfinity));
            AssertBitsEqual(0.0, ExpFdlibm(double.NegativeInfinity));
            Assert.AreEqual(1.0, ExpFdlibm(0.0));
            Assert.AreEqual(1.0, ExpFdlibm(-0.0));
            Assert.AreEqual(1.0, ExpFdlibm(double.Epsilon));
            Assert.AreEqual(1.0, ExpFdlibm(1e-300));

            // Overflow edge: ln(double.MaxValue) = 709.782712893384 is finite, just past it is not.
            double nearMax = ExpFdlibm(709.78);
            Assert.IsFalse(double.IsInfinity(nearMax));
            AssertWithinOneUlp(Math.Exp(709.78), nearMax, 709.78);
            Assert.AreEqual(double.PositiveInfinity, ExpFdlibm(709.79));
            Assert.AreEqual(double.PositiveInfinity, ExpFdlibm(double.MaxValue));

            // Underflow edge: results go subnormal below -708.4 and reach the smallest
            // subnormal, then zero, at -745.13.
            foreach (double x in new[] { -708.5, -720.0, -740.0, -744.0, -745.0 })
            {
                double sub = ExpFdlibm(x);
                Assert.IsTrue(sub > 0 && sub < 2.2250738585072014E-308, string.Format(
                    @"Exp({0}) = {1} should be subnormal", x, sub));
                AssertWithinOneUlp(Math.Exp(x), sub, x);
            }
            Assert.AreEqual(double.Epsilon, ExpFdlibm(-745.1));
            AssertBitsEqual(0.0, ExpFdlibm(-745.2));
            AssertBitsEqual(0.0, ExpFdlibm(-double.MaxValue));
        }

        private static void AssertLogSpecialValues()
        {
            Assert.IsTrue(double.IsNaN(LogFdlibm(double.NaN)));
            Assert.IsTrue(double.IsNaN(LogFdlibm(-1.0)));
            Assert.IsTrue(double.IsNaN(LogFdlibm(-double.Epsilon)));
            Assert.IsTrue(double.IsNaN(LogFdlibm(double.NegativeInfinity)));
            Assert.AreEqual(double.NegativeInfinity, LogFdlibm(0.0));
            Assert.AreEqual(double.NegativeInfinity, LogFdlibm(-0.0));
            Assert.AreEqual(double.PositiveInfinity, LogFdlibm(double.PositiveInfinity));
            AssertBitsEqual(0.0, LogFdlibm(1.0));
            Assert.AreEqual(1.0, LogFdlibm(Math.E));

            // Subnormal inputs take the rescaling branch; the extremes of the finite range.
            foreach (double x in new[] { double.Epsilon, 1e-310, 2.2250738585072009E-308,
                         2.2250738585072014E-308, double.MaxValue })
            {
                AssertWithinOneUlp(Math.Log(x), LogFdlibm(x), x);
            }
        }

        // Neither implementation is correctly rounded everywhere, but each is within one ulp
        // of the true value, and in practice they never disagree by more than one ulp.
        private static void AssertWithinOneUlpOfSystemMath()
        {
            for (int i = 0; i <= GRID_POINTS; i++)
            {
                double x = ExpGridPoint(i);
                AssertWithinOneUlp(Math.Exp(x), ExpFdlibm(x), x);
                double margin = -40 + i * (80.0 / GRID_POINTS);
                AssertWithinOneUlp(Math.Exp(margin), ExpFdlibm(margin), margin);
                double y = LogGridPoint(i);
                AssertWithinOneUlp(Math.Log(y), LogFdlibm(y), y);
            }
        }

        private static void AssertResultBitsPinned()
        {
            long expHash = 17, logHash = 17;
            for (int i = 0; i <= GRID_POINTS; i++)
            {
                expHash = unchecked(expHash * 31 + BitConverter.DoubleToInt64Bits(ExpFdlibm(ExpGridPoint(i))));
                logHash = unchecked(logHash * 31 + BitConverter.DoubleToInt64Bits(LogFdlibm(LogGridPoint(i))));
            }
            Assert.AreEqual(EXP_BITS_HASH, expHash, string.Format(@"Exp result bits changed: hash 0x{0:X16}", expHash));
            Assert.AreEqual(LOG_BITS_HASH, logHash, string.Format(@"Log result bits changed: hash 0x{0:X16}", logHash));
        }

        // The switch accepts exactly 0 and 1 (and unset), the single-argument entry points
        // follow it, and the System arm is Math itself, bit for bit.
        private static void AssertSwitchSelectsArm()
        {
            Assert.AreEqual(true, OspreyEnvironment.ParseDeterministicMath(@"1"));
            Assert.AreEqual(false, OspreyEnvironment.ParseDeterministicMath(@"0"));
            Assert.AreEqual(false, OspreyEnvironment.ParseDeterministicMath(@" 0 "));
            foreach (string bad in new[] { null, string.Empty, @"true", @"false", @"no", @"2", @"00", @"-0", @"""0""" })
                Assert.IsNull(OspreyEnvironment.ParseDeterministicMath(bad), bad);
            if (!OspreyEnvironment.DeterministicMathUnrecognized)
            {
                Assert.AreEqual(OspreyEnvironment.ParseDeterministicMath(OspreyEnvironment.DeterministicMathSetting) ?? true,
                    OspreyEnvironment.UseDeterministicMath, @"unset must select the fdlibm arm");
            }

            bool arm = OspreyEnvironment.UseDeterministicMath;
            for (int i = 0; i <= GRID_POINTS; i += 97)
            {
                double x = ExpGridPoint(i), y = LogGridPoint(i);
                AssertBitsEqual(Math.Exp(x), DeterministicMath.Exp(x, false));
                AssertBitsEqual(Math.Log(y), DeterministicMath.Log(y, false));
                AssertBitsEqual(DeterministicMath.Exp(x, arm), DeterministicMath.Exp(x));
                AssertBitsEqual(DeterministicMath.Log(y, arm), DeterministicMath.Log(y));
            }
        }

        private static double ExpFdlibm(double x)
        {
            return DeterministicMath.Exp(x, true);
        }

        private static double LogFdlibm(double x)
        {
            return DeterministicMath.Log(x, true);
        }

        // Evenly spaced over the whole domain with a finite non-zero result.
        private static double ExpGridPoint(int i)
        {
            return -745.0 + i * (1454.0 / GRID_POINTS);
        }

        // Evenly spaced in bit pattern, so in magnitude, from the smallest subnormal to
        // double.MaxValue.
        private static double LogGridPoint(int i)
        {
            const long maxBits = 0x7FEFFFFFFFFFFFFFL;
            return BitConverter.Int64BitsToDouble(1 + i * (maxBits / GRID_POINTS));
        }

        private static void AssertBitsEqual(double expected, double actual)
        {
            Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual),
                string.Format(@"expected {0:R}, got {1:R}", expected, actual));
        }

        private static void AssertWithinOneUlp(double expected, double actual, double x)
        {
            Assert.IsTrue(Math.Abs(Ordered(expected) - Ordered(actual)) <= 1, string.Format(
                @"f({0:R}): expected {1:R}, got {2:R}", x, expected, actual));
        }

        // Maps a double to an integer whose ordering and spacing match the doubles', so
        // adjacent doubles differ by exactly 1 even across zero.
        private static long Ordered(double d)
        {
            long bits = BitConverter.DoubleToInt64Bits(d);
            return bits < 0 ? long.MinValue - bits : bits;
        }
    }
}
