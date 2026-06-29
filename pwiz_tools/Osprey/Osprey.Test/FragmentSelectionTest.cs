/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for the stateless top-N fragment selection / matching helpers:
    /// <see cref="FragmentMath"/> (prefilter), <see cref="FragmentOverlap"/>
    /// (cross-entry overlap), and <see cref="TopFragmentExtractor"/> (top-N
    /// selection, closest-peak lookup, XIC extraction). Ported from the Rust
    /// has_topn_fragment_match / count_topn_fragment_overlap / extract_fragment_xics.
    /// </summary>
    [TestClass]
    public class FragmentSelectionTest
    {
        // FragmentMath memoizes top-6 m/z by entry Id in a process-wide cache,
        // so every entry handed to HasTopNFragmentMatch needs a unique Id. Use a
        // high base unlikely to collide with other test fixtures.
        private const uint FRAG_ID_BASE = 900000u;

        private static LibraryEntry EntryWithFragments(uint id, params double[] mzs)
        {
            var entry = new LibraryEntry(id, "PEPTIDE", "PEPTIDE", 2, 500.0, 10.0);
            foreach (double mz in mzs)
                entry.Fragments.Add(new LibraryFragment { Mz = mz, RelativeIntensity = 1.0f });
            return entry;
        }

        #region FragmentMath.HasTopNFragmentMatch

        [TestMethod]
        public void TestHasTopNMatchEmptyInputsReturnTrue()
        {
            var tol = FragmentToleranceConfig.UnitResolution(0.5);

            // No fragments -> conservatively true (nothing to prefilter on).
            var noFrags = new LibraryEntry(FRAG_ID_BASE + 1, "X", "X", 2, 500.0, 10.0);
            Assert.IsTrue(FragmentMath.HasTopNFragmentMatch(
                noFrags, new[] { 300.0, 400.0 }, tol));

            // No spectrum peaks -> conservatively true.
            var withFrags = EntryWithFragments(FRAG_ID_BASE + 2, 300.0, 400.0, 500.0);
            Assert.IsTrue(FragmentMath.HasTopNFragmentMatch(
                withFrags, new double[0], tol));
        }

        [TestMethod]
        public void TestHasTopNMatchRequiresTwoOfTopSix()
        {
            var tol = FragmentToleranceConfig.UnitResolution(0.5);

            // Two of three fragments fall on spectrum peaks -> meets the
            // 2-of-top-6 requirement.
            var entry = EntryWithFragments(FRAG_ID_BASE + 10, 300.0, 400.0, 500.0);
            double[] twoMatch = { 300.0, 400.0, 999.0 };  // sorted ascending
            Assert.IsTrue(FragmentMath.HasTopNFragmentMatch(entry, twoMatch, tol));

            // Only one fragment matches -> below the requirement.
            var entry2 = EntryWithFragments(FRAG_ID_BASE + 11, 300.0, 400.0, 500.0);
            double[] oneMatch = { 300.0, 998.0, 999.0 };
            Assert.IsFalse(FragmentMath.HasTopNFragmentMatch(entry2, oneMatch, tol));
        }

        [TestMethod]
        public void TestHasTopNMatchSingleFragmentNeedsOne()
        {
            var tol = FragmentToleranceConfig.UnitResolution(0.5);

            // With a single fragment the required-match count drops to 1.
            var entry = EntryWithFragments(FRAG_ID_BASE + 20, 300.0);
            Assert.IsTrue(FragmentMath.HasTopNFragmentMatch(entry, new[] { 300.0 }, tol));

            var entry2 = EntryWithFragments(FRAG_ID_BASE + 21, 300.0);
            Assert.IsFalse(FragmentMath.HasTopNFragmentMatch(entry2, new[] { 999.0 }, tol));
        }

        #endregion

        #region FragmentOverlap.CountTopNFragmentOverlap

        [TestMethod]
        public void TestFragmentOverlapIdenticalLists()
        {
            var a = MakeFragments(300.0, 400.0, 500.0);
            var b = MakeFragments(300.0, 400.0, 500.0);
            int overlap = FragmentOverlap.CountTopNFragmentOverlap(
                a, b, 6, 0.5, ToleranceUnit.Mz);
            Assert.AreEqual(3, overlap);
        }

        [TestMethod]
        public void TestFragmentOverlapDisjointLists()
        {
            var a = MakeFragments(300.0, 400.0, 500.0);
            var b = MakeFragments(600.0, 700.0, 800.0);
            int overlap = FragmentOverlap.CountTopNFragmentOverlap(
                a, b, 6, 0.5, ToleranceUnit.Mz);
            Assert.AreEqual(0, overlap);
        }

        [TestMethod]
        public void TestFragmentOverlapPartialAndPpm()
        {
            var a = MakeFragments(300.0, 400.0, 500.0);
            var b = MakeFragments(400.0, 500.0, 900.0);
            // 400 and 500 overlap within a 0.5 Th window; 300 does not.
            Assert.AreEqual(2, FragmentOverlap.CountTopNFragmentOverlap(
                a, b, 6, 0.5, ToleranceUnit.Mz));

            // 10 ppm of 500 ~ 0.005 Th: an exact-m/z list still overlaps.
            var c = MakeFragments(300.0, 400.0, 500.0);
            var d = MakeFragments(300.0, 400.0, 500.0);
            Assert.AreEqual(3, FragmentOverlap.CountTopNFragmentOverlap(
                c, d, 6, 10.0, ToleranceUnit.Ppm));
        }

        #endregion

        #region TopFragmentExtractor selection / lookup

        [TestMethod]
        public void TestSelectTopFragmentIndicesAllWhenFew()
        {
            var frags = MakeFragments(300.0, 400.0, 500.0);
            int[] idx = TopFragmentExtractor.SelectTopFragmentIndices(frags, 6);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, idx);
        }

        [TestMethod]
        public void TestSelectTopFragmentIndicesByIntensity()
        {
            // Eight fragments with distinct intensities; top-6 are the highest.
            var frags = new List<LibraryFragment>();
            float[] intens = { 1f, 8f, 3f, 7f, 2f, 6f, 5f, 4f };
            for (int i = 0; i < intens.Length; i++)
                frags.Add(new LibraryFragment { Mz = 300.0 + i, RelativeIntensity = intens[i] });

            int[] idx = TopFragmentExtractor.SelectTopFragmentIndices(frags, 6);
            Assert.AreEqual(6, idx.Length);
            // Highest six intensities are 8,7,6,5,4,3 at indices 1,3,5,6,7,2.
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 6, 7, 2 }, idx);
        }

        [TestMethod]
        public void TestFindClosestPeakInWindow()
        {
            double[] mzs = { 499.8, 500.4, 600.0 };

            // Closest to 500.0 within [499.5, 500.5] is 499.8 (index 0).
            Assert.AreEqual(0,
                TopFragmentExtractor.FindClosestPeakInWindow(mzs, 500.0, 499.5, 500.5));

            // No peak inside the window -> -1.
            Assert.AreEqual(-1,
                TopFragmentExtractor.FindClosestPeakInWindow(mzs, 550.0, 549.5, 550.5));

            // Empty input -> -1.
            Assert.AreEqual(-1,
                TopFragmentExtractor.FindClosestPeakInWindow(new double[0], 500.0, 0.0, 1000.0));

            // Exact distance tie keeps the first (lowest-index) peak.
            double[] tie = { 499.8, 500.2 };
            Assert.AreEqual(0,
                TopFragmentExtractor.FindClosestPeakInWindow(tie, 500.0, 499.0, 501.0));
        }

        [TestMethod]
        public void TestCountTop6Matches()
        {
            var config = new OspreyConfig
            {
                FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5)
            };
            var entry = EntryWithFragments(FRAG_ID_BASE + 30, 300.0, 400.0, 500.0);

            var allMatch = new Spectrum { Mzs = new[] { 300.0, 400.0, 500.0 },
                Intensities = new[] { 10f, 20f, 30f } };
            Assert.AreEqual((byte)3,
                TopFragmentExtractor.CountTop6Matches(entry, allMatch, config));

            var oneMatch = new Spectrum { Mzs = new[] { 300.0, 998.0, 999.0 },
                Intensities = new[] { 10f, 20f, 30f } };
            Assert.AreEqual((byte)1,
                TopFragmentExtractor.CountTop6Matches(entry, oneMatch, config));
        }

        [TestMethod]
        public void TestExtractTopNFragmentXics()
        {
            var config = new OspreyConfig
            {
                FragmentTolerance = FragmentToleranceConfig.UnitResolution(0.5)
            };
            var entry = EntryWithFragments(FRAG_ID_BASE + 40, 300.0, 400.0);

            // Three scans; the closest in-window peak intensity is captured per scan.
            var spectra = new List<Spectrum>
            {
                new Spectrum { Mzs = new[] { 300.0 },        Intensities = new[] { 10f } },
                new Spectrum { Mzs = new[] { 300.0, 400.0 }, Intensities = new[] { 20f, 50f } },
                new Spectrum { Mzs = new[] { 400.0 },        Intensities = new[] { 70f } },
            };
            double[] rts = { 1.0, 2.0, 3.0 };

            List<XicData> xics = TopFragmentExtractor.ExtractTopNFragmentXics(
                entry, spectra, rts, 6, config);

            Assert.AreEqual(2, xics.Count);
            // Fragment 0 (300 Th): present in scans 0 and 1, absent in scan 2.
            Assert.AreEqual(0, xics[0].FragmentIndex);
            CollectionAssert.AreEqual(new[] { 10.0, 20.0, 0.0 }, xics[0].Intensities);
            // Fragment 1 (400 Th): absent in scan 0, present in scans 1 and 2.
            Assert.AreEqual(1, xics[1].FragmentIndex);
            CollectionAssert.AreEqual(new[] { 0.0, 50.0, 70.0 }, xics[1].Intensities);
        }

        #endregion

        #region Helpers

        private static List<LibraryFragment> MakeFragments(params double[] mzs)
        {
            var frags = new List<LibraryFragment>();
            foreach (double mz in mzs)
                frags.Add(new LibraryFragment { Mz = mz, RelativeIntensity = 1.0f });
            return frags;
        }

        #endregion
    }
}
