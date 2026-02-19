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
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class PrecisionNumberTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestParseRegularNumbers()
        {
            var pn = PrecisionNumber.Parse("3.14");
            Assert.AreEqual(3.14, pn.Value, 1e-15);
            Assert.AreEqual(2, pn.DecimalPlaces);
            Assert.AreEqual(0.005, pn.Tolerance, 1e-15);

            pn = PrecisionNumber.Parse("3.0");
            Assert.AreEqual(3.0, pn.Value, 1e-15);
            Assert.AreEqual(1, pn.DecimalPlaces);
            Assert.AreEqual(0.05, pn.Tolerance, 1e-15);

            pn = PrecisionNumber.Parse("300");
            Assert.AreEqual(300.0, pn.Value, 1e-15);
            Assert.AreEqual(0, pn.DecimalPlaces);
            Assert.AreEqual(0.5, pn.Tolerance, 1e-15);
        }

        [TestMethod]
        public void TestParseScientificNotation()
        {
            var pn = PrecisionNumber.Parse("1.5e3");
            Assert.AreEqual(1500.0, pn.Value, 1e-10);
            Assert.AreEqual(-2, pn.DecimalPlaces);
            Assert.AreEqual(50.0, pn.Tolerance, 1e-10);

            pn = PrecisionNumber.Parse("2.0E-4");
            Assert.AreEqual(0.0002, pn.Value, 1e-15);
            Assert.AreEqual(5, pn.DecimalPlaces);
            Assert.AreEqual(0.000005, pn.Tolerance, 1e-15);
        }

        [TestMethod]
        public void TestParseNegativeNumbers()
        {
            var pn = PrecisionNumber.Parse("-3.14");
            Assert.AreEqual(-3.14, pn.Value, 1e-15);
            Assert.AreEqual(2, pn.DecimalPlaces);
        }

        [TestMethod]
        public void TestParseLocale()
        {
            var savedCulture = CultureInfo.CurrentCulture;
            try
            {
                // German culture uses comma as decimal separator
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                // "3,14" should parse with CurrentCulture fallback
                Assert.IsTrue(PrecisionNumber.TryParse("3,14", out var pn));
                Assert.AreEqual(3.14, pn.Value, 1e-15);
                Assert.AreEqual(2, pn.DecimalPlaces);
            }
            finally
            {
                CultureInfo.CurrentCulture = savedCulture;
            }
        }

        [TestMethod]
        public void TestEqualsWithinPrecision()
        {
            var pn = PrecisionNumber.Parse("3.14");
            // Tolerance is 0.005, so range is [3.135, 3.145)
            Assert.IsTrue(pn.EqualsWithinPrecision(3.14));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.144));
            Assert.IsFalse(pn.EqualsWithinPrecision(3.145));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.135));
            Assert.IsFalse(pn.EqualsWithinPrecision(3.1349));
        }

        [TestMethod]
        public void TestTryParseInvalid()
        {
            Assert.IsFalse(PrecisionNumber.TryParse(null, out _));
            Assert.IsFalse(PrecisionNumber.TryParse("", out _));
            Assert.IsFalse(PrecisionNumber.TryParse("abc", out _));
        }

        [TestMethod]
        public void TestEquality()
        {
            var a = PrecisionNumber.Parse("3.14");
            var b = PrecisionNumber.Parse("3.14");
            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.IsFalse(a != b);

            var c = PrecisionNumber.Parse("3.1");
            Assert.AreNotEqual(a, c);
        }
    }
}
