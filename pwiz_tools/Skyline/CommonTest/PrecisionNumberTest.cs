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
        public void TestTryParseInvalid()
        {
            Assert.IsFalse(PrecisionNumber.TryParse(null, CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(PrecisionNumber.TryParse("", CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(PrecisionNumber.TryParse("abc", CultureInfo.InvariantCulture, out _));
        }

        [TestMethod]
        public void TestParsePrecisionNumber()
        {
            var pn = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture, true);
            Assert.AreEqual("3.14", pn.ToString(CultureInfo.InvariantCulture, true));
        }

        [TestMethod]
        public void TestPrecisionNumbers()
        {
            foreach (var value in new[] { Math.PI, Math.E })
            {
                for (int significantDigits = 1; significantDigits <= PrecisionNumber.MAX_SIGNIFICANT_DIGITS; significantDigits++)
                {
                    var precisionNumber = PrecisionNumber.WithSignificantDigits((decimal) value, significantDigits);
                    VerifyRoundTrip(precisionNumber);
                    for (var scaledValue = value / 10; scaledValue > 1e-15; scaledValue /= 10)
                    {
                        precisionNumber = PrecisionNumber.WithSignificantDigits((decimal) scaledValue, significantDigits);
                        VerifyRoundTrip(precisionNumber);
                    }
                    for (var scaledValue = value * 10; scaledValue < 1e15; scaledValue *= 10)
                    {
                        precisionNumber = PrecisionNumber.WithSignificantDigits((decimal) scaledValue, significantDigits);
                        VerifyRoundTrip(precisionNumber);
                    }
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
    }
}
