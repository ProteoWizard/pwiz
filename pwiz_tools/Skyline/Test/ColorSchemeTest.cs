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
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Colors;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ColorSchemeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestNumericColorScheme()
        {
            // An infinite value must not make the scale infinite, which used to throw
            // ArgumentException from Color.FromArgb for every finite value
            var colorScheme = new NumericColorScheme();
            colorScheme.AddValues(new[] { 1.0, 10.0, double.PositiveInfinity, double.NegativeInfinity, double.NaN });
            AssertEx.AreEqual(10.0, colorScheme.MaxValue);
            AssertEx.AreEqual(0.0, colorScheme.MinValue);
            AssertColor(colorScheme, 0.0, 255, 255, 255);
            AssertColor(colorScheme, 10.0, 255, 0, 0);
            AssertColor(colorScheme, 5.0, 255, 127, 127);
            AssertColor(colorScheme, double.PositiveInfinity, 255, 0, 0);
            AssertColor(colorScheme, double.NegativeInfinity, 0, 0, 255);
            Assert.IsNull(colorScheme.GetColor(double.NaN));

            // Negative values with the largest magnitude set the scale
            colorScheme = new NumericColorScheme();
            colorScheme.AddValues(new[] { -10.0, 1.0 });
            AssertColor(colorScheme, -5.0, 127, 127, 255);
            AssertColor(colorScheme, -10.0, 0, 0, 255);
            AssertColor(colorScheme, 1.0, 255, 229, 229);
        }

        private static void AssertColor(NumericColorScheme colorScheme, double value, int red, int green, int blue)
        {
            var color = colorScheme.GetColor(value);
            Assert.IsNotNull(color);
            AssertEx.AreEqual(Color.FromArgb(red, green, blue), color.Value);
        }
    }
}
