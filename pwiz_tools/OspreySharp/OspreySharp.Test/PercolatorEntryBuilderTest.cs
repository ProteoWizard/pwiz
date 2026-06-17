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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Unit tests for <see cref="PercolatorEntryBuilder"/>, the PercolatorEntry
    /// construction + feature-fallback seam extracted from RunPercolatorFdr. The
    /// SVM run itself stays parity-locked and is characterized by regression.ps1.
    /// </summary>
    [TestClass]
    public class PercolatorEntryBuilderTest
    {
        /// <summary>
        /// Build must: keep a well-formed 21-feature vector by reference, fall back
        /// to the basic vector when Features is null OR the wrong length, count
        /// targets / decoys and with- / without-feature entries correctly, preserve
        /// input order, and compose the 4-component PSM id.
        /// </summary>
        [TestMethod]
        public void TestBuildFeatureFallbackAndCounts()
        {
            int nFeat = AbstractScoringTask.NUM_PIN_FEATURES;

            // Entry 0: well-formed 21-feature target -> kept by reference.
            var goodFeatures = new double[nFeat];
            goodFeatures[0] = 42.0;
            var withFeatures = new FdrEntry
            {
                EntryId = 10, ModifiedSequence = "PEPTIDEK", Charge = 2, ScanNumber = 100,
                IsDecoy = false, CoelutionSum = 7.0, Features = goodFeatures
            };
            // Entry 1: null features decoy -> basic-vector fallback.
            var nullFeatures = new FdrEntry
            {
                EntryId = 11, ModifiedSequence = "DECOYR", Charge = 3, ScanNumber = 200,
                IsDecoy = true, CoelutionSum = 4.0, Features = null
            };
            // Entry 2: wrong-length features target -> basic-vector fallback.
            var wrongLen = new FdrEntry
            {
                EntryId = 12, ModifiedSequence = "SHORTK", Charge = 2, ScanNumber = 300,
                IsDecoy = false, CoelutionSum = 2.0, Features = new[] { 1.0, 2.0, 3.0 }
            };

            var perFileEntries = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>(
                    "fileA", new List<FdrEntry> { withFeatures, nullFeatures, wrongLen })
            };

            var result = PercolatorEntryBuilder.Build(
                perFileEntries,
                out int nWithFeatures, out int nWithoutFeatures,
                out int nInputTargets, out int nInputDecoys);

            // Counts: 1 stored-feature, 2 fallback; 2 targets, 1 decoy.
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(1, nWithFeatures);
            Assert.AreEqual(2, nWithoutFeatures);
            Assert.AreEqual(2, nInputTargets);
            Assert.AreEqual(1, nInputDecoys);

            // Order preserved; stored 21-feature vector kept by reference.
            Assert.AreSame(goodFeatures, result[0].Features);

            // Fallback vector is a fresh 21-length array carrying the basic
            // coelution-derived features (and is NOT the stub's wrong-length array).
            Assert.AreEqual(nFeat, result[1].Features.Length);
            Assert.AreEqual(4.0, result[1].Features[0]); // coelution_sum
            Assert.AreEqual(2.0, result[1].Features[1]); // coelution_sum * 0.5
            Assert.AreEqual(3.0, result[1].Features[2]); // n_coeluting_fragments
            Assert.AreEqual(nFeat, result[2].Features.Length);
            Assert.AreEqual(2.0, result[2].Features[0]);

            // Field mapping + 4-component PSM id (file_modseq_charge_scan).
            Assert.AreEqual("fileA_PEPTIDEK_2_100", result[0].Id);
            Assert.AreEqual("fileA", result[0].FileName);
            Assert.AreEqual("PEPTIDEK", result[0].Peptide);
            Assert.AreEqual(2, result[0].Charge);
            Assert.AreEqual(10u, result[0].EntryId);
            Assert.IsFalse(result[0].IsDecoy);
            Assert.IsTrue(result[1].IsDecoy);
        }
    }
}
