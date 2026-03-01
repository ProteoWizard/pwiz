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
            var pn = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture);
            Assert.AreEqual(3.14, pn.Value, 1e-15);
            Assert.AreEqual(2, pn.DecimalPlaces);
            Assert.AreEqual(0.005, pn.Tolerance, 1e-15);

            pn = PrecisionNumber.Parse("3.0", CultureInfo.InvariantCulture);
            Assert.AreEqual(3.0, pn.Value, 1e-15);
            Assert.AreEqual(1, pn.DecimalPlaces);
            Assert.AreEqual(0.05, pn.Tolerance, 1e-15);

            pn = PrecisionNumber.Parse("300", CultureInfo.InvariantCulture);
            Assert.AreEqual(300.0, pn.Value, 1e-15);
            Assert.AreEqual(0, pn.DecimalPlaces);
            Assert.AreEqual(0.5, pn.Tolerance, 1e-15);
        }

        [TestMethod]
        public void TestParseScientificNotation()
        {
            var pn = PrecisionNumber.Parse("1.5e3", CultureInfo.InvariantCulture);
            Assert.AreEqual(1500.0, pn.Value, 1e-10);
            Assert.AreEqual(-2, pn.DecimalPlaces);
            Assert.AreEqual(50.0, pn.Tolerance, 1e-10);

            pn = PrecisionNumber.Parse("2.0E-4", CultureInfo.InvariantCulture);
            Assert.AreEqual(0.0002, pn.Value, 1e-15);
            Assert.AreEqual(5, pn.DecimalPlaces);
            Assert.AreEqual(0.000005, pn.Tolerance, 1e-15);
        }

        [TestMethod]
        public void TestParseNegativeNumbers()
        {
            var pn = PrecisionNumber.Parse("-3.14", CultureInfo.InvariantCulture);
            Assert.AreEqual(-3.14, pn.Value, 1e-15);
            Assert.AreEqual(2, pn.DecimalPlaces);
        }

        [TestMethod]
        public void TestParseLocale()
        {
            // German culture uses comma as decimal separator
            var german = new CultureInfo("de-DE");
            Assert.IsTrue(PrecisionNumber.TryParse("3,14", german, out var pn));
            Assert.AreEqual(3.14, pn.Value, 1e-15);
            Assert.AreEqual(2, pn.DecimalPlaces);
        }

        [TestMethod]
        public void TestEqualsWithinPrecision()
        {
            var pn = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture);
            // Tolerance is 0.005, range is [3.135, 3.145]
            Assert.IsTrue(pn.EqualsWithinPrecision(3.14));
            Assert.IsTrue(pn.EqualsWithinPrecision(3.1401));
            Assert.IsTrue(pn.EqualsWithinPrecision(new PrecisionNumber(3.1399)));
            Assert.IsTrue(pn.EqualsWithinPrecision(new PrecisionNumber(3.1441)));
            Assert.IsTrue(pn.EqualsWithinPrecision(new PrecisionNumber(3.1359)));
            // Values clearly outside the precision range
            Assert.IsFalse(pn.EqualsWithinPrecision(new PrecisionNumber(3.146)));
            Assert.IsFalse(pn.EqualsWithinPrecision(new PrecisionNumber(3.134)));
        }

        [TestMethod]
        public void TestEqualsWithinPrecisionBothLowPrecision()
        {
            // When both numbers have low precision, the larger tolerance dominates
            var a = PrecisionNumber.Parse("3.1", CultureInfo.InvariantCulture); // tolerance = 0.05
            var b = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture); // tolerance = 0.005
            // Max tolerance is 0.05, so |3.1 - 3.14| = 0.04 < 0.05 → match
            Assert.IsTrue(a.EqualsWithinPrecision(b));
            Assert.IsTrue(b.EqualsWithinPrecision(a));
        }

        [TestMethod]
        public void TestTryParseInvalid()
        {
            Assert.IsFalse(PrecisionNumber.TryParse(null, CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(PrecisionNumber.TryParse("", CultureInfo.InvariantCulture, out _));
            Assert.IsFalse(PrecisionNumber.TryParse("abc", CultureInfo.InvariantCulture, out _));
        }

        [TestMethod]
        public void TestEquality()
        {
            var a = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture);
            var b = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture);
            Assert.AreEqual(a, b);

            var c = PrecisionNumber.Parse("3.1", CultureInfo.InvariantCulture);
            Assert.AreNotEqual(a, c);
        }

        [TestMethod]
        public void TestToStringRoundTrip()
        {
            // Regular numbers preserve decimal places
            var pn = PrecisionNumber.Parse("3.14", CultureInfo.InvariantCulture);
            Assert.AreEqual("3.14", pn.ToString());
            var rt = PrecisionNumber.Parse(pn.ToString(), CultureInfo.InvariantCulture);
            Assert.AreEqual(pn.DecimalPlaces, rt.DecimalPlaces);
            Assert.AreEqual(pn.Value, rt.Value, 1e-15);

            // Trailing zero preserved
            pn = PrecisionNumber.Parse("3.0", CultureInfo.InvariantCulture);
            Assert.AreEqual("3.0", pn.ToString());

            // Integer
            pn = PrecisionNumber.Parse("300", CultureInfo.InvariantCulture);
            Assert.AreEqual("300", pn.ToString());

            // Scientific notation with negative decimal places
            pn = PrecisionNumber.Parse("1.5e3", CultureInfo.InvariantCulture);
            var str = pn.ToString();
            rt = PrecisionNumber.Parse(str, CultureInfo.InvariantCulture);
            Assert.AreEqual(pn.Value, rt.Value, 1e-10);
            Assert.AreEqual(pn.DecimalPlaces, rt.DecimalPlaces);

            // Small scientific notation
            pn = PrecisionNumber.Parse("2.0E-4", CultureInfo.InvariantCulture);
            Assert.AreEqual("0.00020", pn.ToString()); // DecimalPlaces=5, so F5
            rt = PrecisionNumber.Parse(pn.ToString(), CultureInfo.InvariantCulture);
            Assert.AreEqual(pn.Value, rt.Value, 1e-15);
            Assert.AreEqual(pn.DecimalPlaces, rt.DecimalPlaces);
        }

        [TestMethod]
        public void TestFromDouble()
        {
            // A raw double wraps with max precision (effectively zero tolerance)
            var pn = new PrecisionNumber(3.14);
            Assert.AreEqual(3.14, pn.Value, 1e-15);
            Assert.AreEqual(15, pn.DecimalPlaces);
            // Tolerance is 5e-16, essentially zero
            Assert.IsTrue(pn.Tolerance < 1e-14);
        }
    }
}
