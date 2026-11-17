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
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that waters_connect CE optimization chromatograms, which share their product m/z and differ
    /// only in collision energy, get the product m/z spacing the loader reads optimization steps from, and
    /// that data acquired with the product m/z already stepped is left alone.
    /// </summary>
    [TestClass]
    public class WatersConnectCeStepsTest : AbstractUnitTest
    {
        private const double PRECURSOR_MZ = 311.08;
        private const double PRODUCT_MZ = 91.99;
        private const int STEP_COUNT = 5;

        [TestMethod]
        public void TestWatersConnectCeSteps()
        {
            ValidateFullSeries();
            ValidateTruncatedSeries();
            ValidateSeriesKeptSeparate();
            ValidateNoSeries();
        }

        /// <summary>
        /// A full set of 11 steps, listed out of collision energy order, is spaced -5..+5 in collision energy
        /// order around the real product m/z, and the result has the spacing the loader recognizes.
        /// </summary>
        private static void ValidateFullSeries()
        {
            var collisionEnergies = new[] { 25.0, 15, 35, 17, 33, 19, 31, 21, 29, 23, 27 };
            var chromIds = MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, collisionEnergies);
            WatersConnectCeSteps.SpaceProductMzBySteps(chromIds, STEP_COUNT);
            AssertSpacedInCeOrder(chromIds, PRODUCT_MZ, -STEP_COUNT);

            var productMzs = chromIds.OrderBy(chromId => chromId.Key.CollisionEnergy)
                .Select(chromId => chromId.Key.Product.RawValue).ToList();
            for (int i = 1; i < productMzs.Count; i++)
                AssertEx.IsTrue(ChromatogramInfo.IsOptimizationSpacing(productMzs[i - 1], productMzs[i]));
        }

        /// <summary>
        /// The export drops steps whose collision energy would not have been positive, so a short series
        /// counts back from the highest collision energy rather than centering on the middle.
        /// </summary>
        private static void ValidateTruncatedSeries()
        {
            // The three lowest of 11 steps are missing, so the remaining 8 are steps -2..+5
            var chromIds = MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, new[] { 21.0, 23, 25, 27, 29, 31, 33, 35 });
            WatersConnectCeSteps.SpaceProductMzBySteps(chromIds, STEP_COUNT);
            AssertSpacedInCeOrder(chromIds, PRODUCT_MZ, -2);
        }

        /// <summary>
        /// Chromatograms of a different precursor or product m/z form separate series, each centered on its
        /// own product m/z.
        /// </summary>
        private static void ValidateSeriesKeptSeparate()
        {
            var collisionEnergies = new[] { 19.0, 21, 23 };
            var chromIds = MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, collisionEnergies);
            foreach (var chromId in MakeSeries(PRECURSOR_MZ, PRODUCT_MZ + 64, collisionEnergies))
                chromIds.Add(chromId);
            foreach (var chromId in MakeSeries(PRECURSOR_MZ + 1, PRODUCT_MZ, collisionEnergies))
                chromIds.Add(chromId);
            WatersConnectCeSteps.SpaceProductMzBySteps(chromIds, STEP_COUNT);
            AssertSpacedInCeOrder(chromIds.Take(3).ToList(), PRODUCT_MZ, -1);
            AssertSpacedInCeOrder(chromIds.Skip(3).Take(3).ToList(), PRODUCT_MZ + 64, -1);
            AssertSpacedInCeOrder(chromIds.Skip(6).ToList(), PRODUCT_MZ, -1);
        }

        /// <summary>
        /// Nothing moves without a series: data acquired with the product m/z already stepped, repeated
        /// collision energies (as in cone voltage optimization), missing collision energies, or too few
        /// chromatograms to be an optimization series.
        /// </summary>
        private static void ValidateNoSeries()
        {
            var alreadyStepped = new List<ChromKeyProviderIdPair>();
            for (int i = 0; i < 5; i++)
                alreadyStepped.Add(MakeChromId(i, PRECURSOR_MZ, PRODUCT_MZ + i * ChromatogramInfo.OPTIMIZE_SHIFT_SIZE, 15 + 2 * i));
            AssertUnchanged(alreadyStepped);

            AssertUnchanged(MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, new[] { 21.0, 21, 21, 21, 21 }));
            AssertUnchanged(MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, new[] { 0.0, 0, 0, 0, 0 }));
            AssertUnchanged(MakeSeries(PRECURSOR_MZ, PRODUCT_MZ, new[] { 21.0, 23 }));
        }

        private static List<ChromKeyProviderIdPair> MakeSeries(double precursorMz, double productMz,
            IList<double> collisionEnergies)
        {
            return Enumerable.Range(0, collisionEnergies.Count)
                .Select(i => MakeChromId(i, precursorMz, productMz, collisionEnergies[i])).ToList();
        }

        private static ChromKeyProviderIdPair MakeChromId(int providerId, double precursorMz, double productMz,
            double collisionEnergy)
        {
            var key = new ChromKey(null, new SignedMz(precursorMz), IonMobilityFilter.EMPTY, new SignedMz(productMz),
                0, collisionEnergy, 0, ChromSource.fragment, ChromExtractor.summed);
            return new ChromKeyProviderIdPair(key, providerId);
        }

        /// <summary>
        /// Asserts that the chromatograms are spaced one step apart in collision energy order, starting at
        /// <paramref name="firstStep"/>, around the given real product m/z.
        /// </summary>
        private static void AssertSpacedInCeOrder(IList<ChromKeyProviderIdPair> chromIds, double productMz, int firstStep)
        {
            int step = firstStep;
            foreach (var chromId in chromIds.OrderBy(chromId => chromId.Key.CollisionEnergy))
            {
                AssertEx.AreEqual(productMz + step * ChromatogramInfo.OPTIMIZE_SHIFT_SIZE,
                    chromId.Key.Product.RawValue, 1e-9, $@"CE {chromId.Key.CollisionEnergy}");
                step++;
            }
        }

        private static void AssertUnchanged(IList<ChromKeyProviderIdPair> chromIds)
        {
            var productMzs = chromIds.Select(chromId => chromId.Key.Product.RawValue).ToList();
            WatersConnectCeSteps.SpaceProductMzBySteps(chromIds, STEP_COUNT);
            AssertEx.AreEqualDeep(productMzs, chromIds.Select(chromId => chromId.Key.Product.RawValue).ToList());
        }
    }
}
