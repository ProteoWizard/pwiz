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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class PrecisionNumberTest
    {
        [TestMethod]
        public void TestParseRegularNumbers()
        {
            var pn = PrecisionNumber.Parse(3.14.ToString("F2"));
            Assert.AreEqual(3.14m, pn.Value);
            Assert.AreEqual(2, pn.DecimalPlaces);
            Assert.AreEqual(0.005m, pn.Tolerance);

            pn = PrecisionNumber.Parse(3.0.ToString("F1"));
            Assert.AreEqual(3.0m, pn.Value);
            Assert.AreEqual(1, pn.DecimalPlaces);
            Assert.AreEqual(0.05m, pn.Tolerance);

            pn = PrecisionNumber.Parse("300");
            Assert.AreEqual(300m, pn.Value);
            Assert.AreEqual(0, pn.DecimalPlaces);
            Assert.AreEqual(0.5m, pn.Tolerance);
        }

        [TestMethod]
        public void TestParseScientificNotation()
        {
            var pn = PrecisionNumber.Parse(1500.ToString("e1"));
            Assert.AreEqual(1500m, pn.Value);
            Assert.AreEqual(-2, pn.DecimalPlaces);
            Assert.AreEqual(50m, pn.Tolerance);

            pn = PrecisionNumber.Parse(.0002.ToString("e1"));
            Assert.AreEqual(0.0002m, pn.Value);
            Assert.AreEqual(5, pn.DecimalPlaces);
            Assert.AreEqual(0.000005m, pn.Tolerance);
        }

        [TestMethod]
        public void TestParseNegativeNumbers()
        {
            var pn = PrecisionNumber.Parse((-3.14).ToString("F2"));
            Assert.AreEqual(-3.14m, pn.Value);
            Assert.AreEqual(2, pn.DecimalPlaces);
        }

        [TestMethod]
        public void TestDefaultPrecisionNumber()
        {
            var pnDefault = default(PrecisionNumber);
            Assert.AreEqual(-1, pnDefault.DecimalPlaces);
            Assert.AreEqual(0, pnDefault.SignificantDigits);
            Assert.AreEqual(0m, pnDefault.Value);
            Assert.AreEqual(5m, pnDefault.Tolerance);
            var pnZero = new PrecisionNumber(0m).ChangeSignificantDigits(0);
            Assert.AreEqual(pnDefault, pnZero);
        }

        [TestMethod]
        public void TestEqualsWithinPrecision()
        {
            var pn = PrecisionNumber.Parse(3.14.ToString("F2"));
            // Tolerance is 0.005, range is [3.135, 3.145]
            Assert.IsTrue(pn.EqualsWithinPrecision(3.14));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.1401));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.1399));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.1441));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.1359));
            // Values clearly outside the precision range
            Assert.IsFalse(pn.EqualsWithinPrecision(3.146));
            Assert.IsFalse(pn.EqualsWithinPrecision(3.134));
        }

        [TestMethod]
        public void TestParsePrecisionNumber()
        {
            var str = 3.14.ToString("F2");
            var pn = PrecisionNumber.Parse(str);
            Assert.AreEqual(str, pn.ToString());
        }

        [TestMethod]
        public void TestFinitePrecisionNumbers()
        {
            VerifyFiniteNumber(Math.PI);
            VerifyFiniteNumber(Math.E);
            VerifyFiniteNumber(PrecisionNumber.MIN_DOUBLE);
            VerifyFiniteNumber(PrecisionNumber.MAX_DOUBLE);
        }

        private void VerifyFiniteNumber(double value)
        {
            for (int significantDigits = 1; significantDigits <= PrecisionNumber.MAX_SIGNIFICANT_DIGITS; significantDigits++)
            {
                var precisionNumber = PrecisionNumber.FromDouble(value).ChangeSignificantDigits(significantDigits);
                AssertEx.IsTrue(precisionNumber.EqualsWithinPrecision((decimal) value));
                VerifyNumber(precisionNumber, precisionNumber.ToDouble());
                for (var scaledValue = value / 10; scaledValue > 1e-15; scaledValue /= 10)
                {
                    precisionNumber = PrecisionNumber.FromDouble(scaledValue).ChangeSignificantDigits(significantDigits);
                    AssertEx.IsTrue(precisionNumber.EqualsWithinPrecision(scaledValue));
                    VerifyNumber(precisionNumber, precisionNumber.ToDouble());
                }
                for (var scaledValue = value * 10; Math.Abs(scaledValue) < 1e15; scaledValue *= 10)
                {
                    precisionNumber = PrecisionNumber.FromDouble(scaledValue).ChangeSignificantDigits(significantDigits);
                    Assert.IsTrue(precisionNumber.EqualsWithinPrecision(scaledValue));
                    VerifyNumber(precisionNumber, precisionNumber.ToDouble());
                }
            }
        }


        private void VerifyRoundTrip(PrecisionNumber precisionNumber)
        {
            var textImplicitPrecision = precisionNumber.ToString(CultureInfo.CurrentCulture, false);
            var roundTripImplicitPrecision =
                PrecisionNumber.Parse(textImplicitPrecision, CultureInfo.CurrentCulture, false);
            AssertEx.AreEqual(precisionNumber, roundTripImplicitPrecision);
            var textExplicitPrecision = precisionNumber.ToString(CultureInfo.CurrentCulture, true);
            var roundTripExplicitPrecision =
                PrecisionNumber.Parse(textExplicitPrecision, CultureInfo.CurrentCulture, true);
            AssertEx.AreEqual(precisionNumber, roundTripExplicitPrecision);
        }

        [TestMethod]
        public void TestPrecisionNumberSize()
        {
            Assert.AreEqual(24, Marshal.SizeOf<PrecisionNumber>());
        }

        [TestMethod]
        public void TestInfinitePrecisionNumbers()
        {
            VerifyNumber(PrecisionNumber.NAN, double.NaN);

            VerifyNumber(PrecisionNumber.NEGATIVE_INFINITY, double.NegativeInfinity);
            VerifyNumber(PrecisionNumber.FromDouble(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits((double)decimal.MinValue) + 1)), double.NegativeInfinity);

            VerifyNumber(PrecisionNumber.POSITIVE_INFINITY, double.PositiveInfinity);
            VerifyNumber(PrecisionNumber.FromDouble(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits((double)decimal.MaxValue) + 1)), double.PositiveInfinity);
        }

        private void VerifyNumber(PrecisionNumber precisionNumber, double expectedDoubleValue)
        {
            VerifyRoundTrip(precisionNumber);
            Assert.AreEqual(expectedDoubleValue, precisionNumber.ToDouble());
            Assert.AreEqual(precisionNumber, PrecisionNumber.FromDouble(expectedDoubleValue).ChangeSignificantDigits(precisionNumber.SignificantDigits));
            Assert.AreEqual(precisionNumber, PrecisionNumber.FromDouble(expectedDoubleValue).ChangeDecimalPlaces(precisionNumber.DecimalPlaces));
            Assert.IsTrue(precisionNumber.EqualsWithinPrecision(expectedDoubleValue));
            if (double.IsNaN(expectedDoubleValue) || double.IsInfinity(expectedDoubleValue))
            {
                Assert.IsFalse(precisionNumber.IsFinite);
            }
            else
            {
                Assert.IsTrue(precisionNumber.IsFinite);
                Assert.AreEqual(-1, precisionNumber.CompareTo(double.MaxValue));
                Assert.AreEqual(1, precisionNumber.CompareTo(double.MinValue));
                if (Equals(decimal.MinValue, precisionNumber.Value))
                {
                    Assert.AreEqual(0, precisionNumber.CompareTo(decimal.MinValue));
                }
                else
                {
                    Assert.AreEqual(1, precisionNumber.CompareTo(decimal.MinValue));
                }

                if (Equals(decimal.MaxValue, precisionNumber.Value))
                {
                    Assert.AreEqual(0, precisionNumber.CompareTo(decimal.MaxValue));
                }
                else
                {
                    Assert.AreEqual(-1, precisionNumber.CompareTo(decimal.MaxValue));
                }
                Assert.AreEqual(precisionNumber.ToDouble(), PrecisionNumber.Parse(precisionNumber.Value.ToString(CultureInfo.CurrentCulture))
                    .ChangeSignificantDigits(precisionNumber.SignificantDigits).ToDouble());

            }
        }

        [TestMethod]
        public void TestPrecisionNumberToString()
        {
            var strThreeSigFigs = 20.0.ToString("F1");
            var threeSigFigs = PrecisionNumber.Parse(strThreeSigFigs);
            Assert.AreEqual(3, threeSigFigs.SignificantDigits);
            Assert.AreEqual(strThreeSigFigs, threeSigFigs.ToString());
        }
    }
}
