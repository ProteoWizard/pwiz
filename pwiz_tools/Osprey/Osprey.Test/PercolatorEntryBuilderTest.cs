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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
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
        /// #6 parity: Build keeps only entries with a well-formed 21-feature vector
        /// (by reference) and SKIPS entries whose Features is null OR the wrong length,
        /// matching Rust run_percolator_fdr_direct (pipeline.rs:6153-6162 `continue`).
        /// It previously fabricated a placeholder vector for those, which perturbed the
        /// standardizer statistics, the training pool, and the output row set. The counts
        /// reflect only kept entries (a skipped decoy is not tallied as an input decoy);
        /// nWithoutFeatures still tallies the skipped entries. Field mapping + the
        /// 4-component PSM id are preserved.
        ///
        /// FAILS on revert: if Build fabricates instead of skipping, result.Count would
        /// be 3 (and nInputDecoys 1) rather than 1 (and 0).
        /// </summary>
        [TestMethod]
        public void TestBuildSkipsFeaturelessEntries()
        {
            int nFeat = ScoringTaskShared.NUM_PIN_FEATURES;

            // Entry 0: well-formed 21-feature target -> kept by reference.
            var goodFeatures = new double[nFeat];
            goodFeatures[0] = 42.0;
            var withFeatures = new FdrEntry
            {
                EntryId = 10, ModifiedSequence = "PEPTIDEK", Charge = 2, ScanNumber = 100,
                IsDecoy = false, CoelutionSum = 7.0, Features = goodFeatures
            };
            // Entry 1: null-features decoy -> skipped (was fabricated pre-#6).
            var nullFeatures = new FdrEntry
            {
                EntryId = 11, ModifiedSequence = "DECOYR", Charge = 3, ScanNumber = 200,
                IsDecoy = true, CoelutionSum = 4.0, Features = null
            };
            // Entry 2: wrong-length-features target -> skipped.
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
                perFileEntries, nFeat,
                out int nWithFeatures, out int nWithoutFeatures,
                out int nInputTargets, out int nInputDecoys);

            // Only the featured entry is kept; the two featureless entries are skipped.
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(1, nWithFeatures);
            Assert.AreEqual(2, nWithoutFeatures);
            // Input counts reflect only kept entries: the skipped decoy is NOT counted.
            Assert.AreEqual(1, nInputTargets);
            Assert.AreEqual(0, nInputDecoys);

            // Kept entry: stored 21-feature vector by reference + field mapping + PSM id.
            Assert.AreSame(goodFeatures, result[0].Features);
            Assert.AreEqual("fileA_PEPTIDEK_2_100", result[0].Id);
            Assert.AreEqual("fileA", result[0].FileName);
            Assert.AreEqual("PEPTIDEK", result[0].Peptide);
            Assert.AreEqual(2, result[0].Charge);
            Assert.AreEqual(10u, result[0].EntryId);
            Assert.IsFalse(result[0].IsDecoy);
        }
    }
}
