/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that waters_connect CE optimization channels, which share their product m/z and differ only
    /// in collision energy, get the product m/z shifts Skyline uses to tell optimization steps apart, and that
    /// data acquired the old way, with shifted product m/z, is left alone.
    /// </summary>
    [TestClass]
    public class WatersConnectCeStepsTest : AbstractUnitTest
    {
        private const double PRECURSOR_MZ = 311.08;
        private const double PRODUCT_MZ = 91.99;

        [TestMethod]
        public void TestWatersConnectCeSteps()
        {
            AssertEx.AreEqual(ChromatogramInfo.OPTIMIZE_SHIFT_SIZE, WatersConnectCeSteps.OPTIMIZE_SHIFT_SIZE);

            ValidateOddSeries();
            ValidateEvenSeries();
            ValidateSeriesKeptSeparate();
            ValidateNoSeries();
        }

        /// <summary>
        /// A full series of 7 steps, listed out of CE order, is shifted -3..+3 in CE order, and the shifted
        /// product m/z values have the spacing the Skyline loader recognizes as optimization steps.
        /// </summary>
        private static void ValidateOddSeries()
        {
            var collisionEnergies = new[] { 15.0, 9, 21, 11, 19, 13, 17 };
            var channels = collisionEnergies.Select((ce, i) => MakeChannel(i, false, PRECURSOR_MZ, PRODUCT_MZ, ce)).ToList();
            var shifts = WatersConnectCeSteps.GetProductMzShifts(channels);
            AssertShiftsInCeOrder(channels, shifts, -3);

            var shiftedMzs = channels.OrderBy(c => c.CollisionEnergy)
                .Select(c => c.ProductMz + shifts[c.Index]).ToList();
            for (int i = 1; i < shiftedMzs.Count; i++)
                AssertEx.IsTrue(ChromatogramInfo.IsOptimizationSpacing(shiftedMzs[i - 1], shiftedMzs[i]));
        }

        /// <summary>
        /// With an even number of steps the center is the lower of the two middle channels, the same rule
        /// the Skyline loader uses.
        /// </summary>
        private static void ValidateEvenSeries()
        {
            var channels = new[] { 10.0, 12, 14, 16 }.Select((ce, i) => MakeChannel(i, false, PRECURSOR_MZ, PRODUCT_MZ, ce)).ToList();
            AssertShiftsInCeOrder(channels, WatersConnectCeSteps.GetProductMzShifts(channels), -1);
        }

        /// <summary>
        /// Channels of different polarity or precursor m/z form separate series even when the other values match.
        /// </summary>
        private static void ValidateSeriesKeptSeparate()
        {
            var ces = new[] { 10.0, 12, 14 };
            var positive = ces.Select((ce, i) => MakeChannel(i, false, PRECURSOR_MZ, PRODUCT_MZ, ce)).ToList();
            var negative = ces.Select((ce, i) => MakeChannel(10 + i, true, PRECURSOR_MZ, PRODUCT_MZ, ce)).ToList();
            var otherPrecursor = ces.Select((ce, i) => MakeChannel(20 + i, false, PRECURSOR_MZ + 1, PRODUCT_MZ, ce)).ToList();
            var shifts = WatersConnectCeSteps.GetProductMzShifts(positive.Concat(negative).Concat(otherPrecursor));
            AssertEx.AreEqual(9, shifts.Count);
            AssertShiftsInCeOrder(positive, shifts, -1);
            AssertShiftsInCeOrder(negative, shifts, -1);
            AssertShiftsInCeOrder(otherPrecursor, shifts, -1);
        }

        /// <summary>
        /// Nothing is shifted without a series: data acquired with shifted product m/z (the old waters_connect
        /// export), repeated CE values (e.g. cone voltage optimization), missing CE, or a single channel.
        /// </summary>
        private static void ValidateNoSeries()
        {
            var legacyShifted = new[] { 91.97, 91.98, 91.99, 92.00, 92.01 }
                .Select((mz, i) => MakeChannel(i, false, PRECURSOR_MZ, mz, 11 + 2 * i));
            AssertNoShifts(legacyShifted);

            var repeatedCe = Enumerable.Range(0, 5).Select(i => MakeChannel(i, false, PRECURSOR_MZ, PRODUCT_MZ, 15));
            AssertNoShifts(repeatedCe);

            var missingCe = Enumerable.Range(0, 5).Select(i => MakeChannel(i, false, PRECURSOR_MZ, PRODUCT_MZ, 0));
            AssertNoShifts(missingCe);

            AssertNoShifts(new[] { MakeChannel(0, false, PRECURSOR_MZ, PRODUCT_MZ, 15) });
        }

        private static WatersConnectCeSteps.Channel MakeChannel(int index, bool? isNegativePolarity,
            double precursorMz, double productMz, double collisionEnergy)
        {
            return new WatersConnectCeSteps.Channel(index, isNegativePolarity, precursorMz, productMz, collisionEnergy);
        }

        private static void AssertShiftsInCeOrder(IEnumerable<WatersConnectCeSteps.Channel> channels,
            IDictionary<int, double> shifts, int firstStep)
        {
            int step = firstStep;
            foreach (var channel in channels.OrderBy(c => c.CollisionEnergy))
            {
                AssertEx.IsTrue(shifts.ContainsKey(channel.Index));
                AssertEx.AreEqual(step * ChromatogramInfo.OPTIMIZE_SHIFT_SIZE, shifts[channel.Index], 1e-9);
                step++;
            }
        }

        private static void AssertNoShifts(IEnumerable<WatersConnectCeSteps.Channel> channels)
        {
            AssertEx.AreEqual(0, WatersConnectCeSteps.GetProductMzShifts(channels).Count);
        }
    }
}
