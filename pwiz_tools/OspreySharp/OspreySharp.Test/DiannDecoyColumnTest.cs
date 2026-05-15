/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
{
    /// <summary>
    /// Cross-impl tests for the DIA-NN TSV loader's Decoy column support.
    /// Ports tests from <c>crates/osprey-io/src/library/diann.rs</c> at
    /// maccoss/osprey commit <c>fe7c7c1</c>.
    /// </summary>
    [TestClass]
    public class DiannDecoyColumnTest
    {
        [TestMethod]
        public void ParseDecoyFlagAcceptsTruthyValuesAndRejectsFalsy()
        {
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"1"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"true"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"TRUE"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"Yes"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"y"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@"t"));
            Assert.IsTrue(DiannTsvLoader.ParseDecoyFlag(@" 1 "));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag(@"0"));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag(string.Empty));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag(@"false"));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag(@"garbage"));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag(null));
            // ASCII-only lowercasing: non-ASCII input is never a match.
            // Mirrors Rust's `to_ascii_lowercase`; `ToLowerInvariant`
            // would case-fold Unicode differently for some locales.
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag("１"));
            Assert.IsFalse(DiannTsvLoader.ParseDecoyFlag("Yİ"));
        }

        [TestMethod]
        public void LoaderReadsDecoyColumn()
        {
            // Minimal DIA-NN TSV: 4 rows = 2 precursors x 2 fragments each.
            // First precursor has Decoy=0, second has Decoy=1.
            string tsv =
@"ModifiedPeptide	StrippedPeptide	PrecursorMz	PrecursorCharge	Tr_recalibrated	ProteinID	Decoy	FragmentMz	RelativeIntensity	FragmentType	FragmentNumber	FragmentCharge	FragmentLossType
_PEPTIDEK_	PEPTIDEK	400.0	2	10.5	sp|P00001|TEST_HUMAN	0	100.0	1.0	y	1	1	noloss
_PEPTIDEK_	PEPTIDEK	400.0	2	10.5	sp|P00001|TEST_HUMAN	0	200.0	0.8	y	2	1	noloss
_KEDITPEP_	KEDITPEP	400.0	2	10.5	decoy_sp|P00001|TEST_HUMAN	1	100.0	1.0	y	1	1	noloss
_KEDITPEP_	KEDITPEP	400.0	2	10.5	decoy_sp|P00001|TEST_HUMAN	1	200.0	0.8	y	2	1	noloss
";
            string path = WriteTempTsv(tsv);
            try
            {
                var loader = new DiannTsvLoader(2);
                var entries = loader.Load(path);
                Assert.AreEqual(2, entries.Count);
                var target = FindBySequence(entries, @"PEPTIDEK");
                var decoy = FindBySequence(entries, @"KEDITPEP");
                Assert.IsFalse(target.IsDecoy, @"target row should have IsDecoy=false");
                Assert.IsTrue(decoy.IsDecoy, @"decoy row should have IsDecoy=true");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void LoaderNoDecoyColumnDefaultsToTarget()
        {
            // No Decoy column at all -> loader sets IsDecoy=false on
            // every entry; the apply_library_decoy_marking pass is what
            // handles prefix-only libraries.
            string tsv =
@"ModifiedPeptide	StrippedPeptide	PrecursorMz	PrecursorCharge	Tr_recalibrated	ProteinID	FragmentMz	RelativeIntensity	FragmentType	FragmentNumber	FragmentCharge	FragmentLossType
_PEPTIDEK_	PEPTIDEK	400.0	2	10.5	decoy_sp|P00001|TEST_HUMAN	100.0	1.0	y	1	1	noloss
_PEPTIDEK_	PEPTIDEK	400.0	2	10.5	decoy_sp|P00001|TEST_HUMAN	200.0	0.8	y	2	1	noloss
";
            string path = WriteTempTsv(tsv);
            try
            {
                var loader = new DiannTsvLoader(2);
                var entries = loader.Load(path);
                Assert.AreEqual(1, entries.Count);
                Assert.IsFalse(entries[0].IsDecoy);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string WriteTempTsv(string contents)
        {
            string path = Path.GetTempFileName();
            File.WriteAllText(path, contents);
            return path;
        }

        private static Core.LibraryEntry FindBySequence(
            System.Collections.Generic.IList<Core.LibraryEntry> entries, string sequence)
        {
            foreach (var e in entries)
            {
                if (e.Sequence == sequence)
                    return e;
            }
            Assert.Fail(@"Entry not found: {0}", sequence);
            return null;
        }
    }
}
