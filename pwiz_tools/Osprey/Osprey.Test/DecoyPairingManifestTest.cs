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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Cross-impl tests for the FDRBench-style manifest reader and the
    /// manifest+composition hybrid pairing. Ports tests from
    /// <c>crates/osprey-io/src/pairing.rs</c> at maccoss/osprey commits
    /// <c>a8ae4a1</c> and <c>4bb7068</c>.
    /// </summary>
    [TestClass]
    public class DecoyPairingManifestTest
    {
        private static string WriteManifest(IEnumerable<string> rows)
        {
            string path = Path.GetTempFileName();
            using (var w = new StreamWriter(path))
            {
                w.WriteLine(@"sequence	decoy	proteins	peptide_type	peptide_pair_index");
                foreach (var r in rows)
                    w.WriteLine(r);
            }
            return path;
        }

        private static LibraryEntry MakeEntry(uint id, string sequence,
            byte charge, bool isDecoy)
        {
            var e = new LibraryEntry(id, sequence, sequence, charge, 500.0, 10.0);
            e.IsDecoy = isDecoy;
            return e;
        }

        [TestMethod]
        public void ManifestPairsTargetAndDecoyWithinPairIndex()
        {
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"PEPTIDEB	No	protA_p	p_target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
                @"BPETPIDE	Yes	rev_protA_p	p_decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    MakeEntry(1, @"PEPTIDEA", 2, false),
                    MakeEntry(2, @"PEPTIDEB", 2, false),
                    MakeEntry(3, @"AEPTPIDE", 2, true),
                    MakeEntry(4, @"BPETPIDE", 2, true),
                };
                var state = new PairingState();
                int nPaired = m.ApplyToLibrary(lib, state).NPaired;
                Assert.AreEqual(2, nPaired);
                // The decoy of PEPTIDEA should base_id-match target id=1.
                var aeptpide = FindBySequence(lib, @"AEPTPIDE");
                var bpetpide = FindBySequence(lib, @"BPETPIDE");
                Assert.AreEqual(1u, aeptpide.Id & 0x7FFFFFFFu);
                Assert.AreEqual(2u, bpetpide.Id & 0x7FFFFFFFu);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestPairingHonorsCharge()
        {
            // Manifest assigns charge-2 and charge-3 decoys; verify each
            // pairs to the same-charge target.
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    MakeEntry(1, @"PEPTIDEA", 2, false),
                    MakeEntry(2, @"PEPTIDEA", 3, false),
                    MakeEntry(3, @"AEPTPIDE", 2, true),
                    MakeEntry(4, @"AEPTPIDE", 3, true),
                };
                // Explicitly pin the "last-write-wins on duplicate
                // sequence" contract: the manifest is indexed by sequence
                // alone, so this 4-row file with 2 sequences yields 2
                // entries -- not 4. Charge-aware bucketing happens later
                // at apply-to-library time.
                Assert.AreEqual(2, m.Count);

                var state = new PairingState();
                int nPaired = m.ApplyToLibrary(lib, state).NPaired;
                Assert.AreEqual(2, nPaired);
                var dCharge2 = FindByDecoyAndCharge(lib, @"AEPTPIDE", 2);
                var dCharge3 = FindByDecoyAndCharge(lib, @"AEPTPIDE", 3);
                Assert.AreEqual(1u, dCharge2.Id & 0x7FFFFFFFu);
                Assert.AreEqual(2u, dCharge3.Id & 0x7FFFFFFFu);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestReaderHandlesUtf8BomInHeader()
        {
            // FDRBench can emit TSV files with a UTF-8 BOM (some Excel
            // workflows insert one when round-tripping). StreamReader's
            // default ctor enables BOM detection so the BOM is consumed
            // before the header parse sees the column names. This test
            // pins that behavior so a future "let me tighten the
            // StreamReader options" edit doesn't regress.
            string path = Path.GetTempFileName();
            try
            {
                using (var fs = new FileStream(path, FileMode.Create))
                {
                    // UTF-8 BOM: EF BB BF.
                    fs.WriteByte(0xEF);
                    fs.WriteByte(0xBB);
                    fs.WriteByte(0xBF);
                    using (var sw = new StreamWriter(fs, new System.Text.UTF8Encoding(false)))
                    {
                        sw.WriteLine(@"sequence	decoy	proteins	peptide_type	peptide_pair_index");
                        sw.WriteLine(@"PEPTIDEA	No	protA	target	0");
                        sw.WriteLine(@"AEPTPIDE	Yes	rev_protA	decoy	0");
                    }
                }
                var m = DecoyPairingManifest.FromTsv(path);
                Assert.AreEqual(2, m.Count, @"BOM should be stripped; header columns should resolve");
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestSkipsSequencesNotInManifest()
        {
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    MakeEntry(1, @"PEPTIDEA", 2, false),
                    MakeEntry(2, @"UNKNOWNK", 2, true), // not in manifest
                };
                var state = new PairingState();
                int nPaired = m.ApplyToLibrary(lib, state).NPaired;
                Assert.AreEqual(0, nPaired);
                // The unpaired decoy must not be in state.PairedDecoys.
                Assert.IsFalse(state.PairedDecoys.Contains(1));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestThenCompositionPairsUnmatchedDecoys()
        {
            // Hybrid: manifest covers PEPTIDEA's pair; composition covers
            // PEPTIDEB <-> EPBPTIDE (a permutation pair not in the manifest).
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    AddProteins(MakeEntry(1, @"PEPTIDEA", 2, false), @"protA"),
                    AddProteins(MakeEntry(2, @"AEPTPIDE", 2, true), @"rev_protA"),
                    AddProteins(MakeEntry(3, @"PEPTIDEB", 2, false), @"protB"),
                    AddProteins(MakeEntry(4, @"EPBPTIDE", 2, true), @"rev_protB"),
                };
                var state = new PairingState();
                int nManifest = m.ApplyToLibrary(lib, state).NPaired;
                Assert.AreEqual(1, nManifest);
                int nComposition = LibraryDecoyPairing.PairLibraryDecoysByComposition(
                    lib, new List<string> { @"rev_" }, state);
                Assert.AreEqual(1, nComposition);
                var decA = FindBySequence(lib, @"AEPTPIDE");
                var decB = FindBySequence(lib, @"EPBPTIDE");
                Assert.AreEqual(1u, decA.Id & 0x7FFFFFFFu);
                Assert.AreEqual(3u, decB.Id & 0x7FFFFFFFu);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestFlipsUnflaggedDecoysToIsDecoy()
        {
            // Real-world failure mode: a library predictor (Carafe)
            // strips the rev_ / decoy_ prefix from protein accessions, so
            // the library loads with every decoy looking like a target
            // (IsDecoy = false). The manifest's sequence-based
            // classification is authoritative and must flip those entries
            // to IsDecoy = true.
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    MakeEntry(1, @"PEPTIDEA", 2, false),
                    MakeEntry(2, @"AEPTPIDE", 2, false), // <-- loaded as target
                };
                Assert.IsFalse(lib[1].IsDecoy);
                Assert.AreEqual(0u, lib[1].Id & LibraryEntry.DECOY_ID_BIT);

                var state = new PairingState();
                var stats = m.ApplyToLibrary(lib, state);

                Assert.AreEqual(1, stats.NPaired);
                Assert.AreEqual(1, stats.NNewlyMarkedDecoy);
                var decoy = FindBySequence(lib, @"AEPTPIDE");
                Assert.IsTrue(decoy.IsDecoy, @"manifest should flip IsDecoy=true");
                Assert.IsTrue((decoy.Id & LibraryEntry.DECOY_ID_BIT) != 0u,
                    @"decoy id should have high bit set");
                Assert.AreEqual(1u, decoy.Id & 0x7FFFFFFFu);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestMarksUnpairedDecoyWithoutPairingIt()
        {
            // An unpaired manifest-flagged decoy (no target-side
            // counterpart in the library) still gets IsDecoy=true and the
            // high bit on its Id, even though pairing skips it.
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var lib = new List<LibraryEntry>
                {
                    // Only the decoy peptide is in the library; the
                    // target sibling PEPTIDEA isn't.
                    MakeEntry(7, @"AEPTPIDE", 2, false),
                };
                var state = new PairingState();
                var stats = m.ApplyToLibrary(lib, state);
                Assert.AreEqual(0, stats.NPaired); // no target -> no pair
                Assert.AreEqual(1, stats.NNewlyMarkedDecoy);
                Assert.IsTrue(lib[0].IsDecoy);
                Assert.IsTrue((lib[0].Id & LibraryEntry.DECOY_ID_BIT) != 0u);
                // Unpaired: low 31 bits keep the original id.
                Assert.AreEqual(7u, lib[0].Id & 0x7FFFFFFFu);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestReplacesProteinIdsWithCleanAccessions()
        {
            // Cross-impl port of Rust `manifest_replaces_protein_ids_with_clean_accessions`
            // (commit 0c3a73e). The manifest's `proteins` column is the
            // authoritative source of protein info: a Carafe-built library
            // can stamp a per-peptide suffix into ProteinID (e.g.
            // `sp|P12345_pep00001|GENE_A`) that breaks protein parsimony
            // unless we substitute the clean source accessions.
            string path = WriteManifest(new[]
            {
                @"PEPTIDE	No	sp|P12345|GENE_A;sp|Q67890|GENE_B	target	0",
                @"AEPTPID	Yes	decoy_sp|P12345|GENE_A;decoy_sp|Q67890|GENE_B	decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var libEntry1 = MakeEntry(1, @"PEPTIDE", 2, false);
                libEntry1.ProteinIds.Add(@"sp|P12345_pep00001|GENE_A");
                var libEntry2 = MakeEntry(2, @"AEPTPID", 2, false);
                libEntry2.ProteinIds.Add(@"decoy_sp|P12345_pep00001|GENE_A");
                var lib = new List<LibraryEntry> { libEntry1, libEntry2 };

                var state = new PairingState();
                var stats = m.ApplyToLibrary(lib, state);

                Assert.AreEqual(1, stats.NPaired);
                Assert.AreEqual(2, stats.NProteinsReplaced);
                CollectionAssert.AreEqual(
                    new[] { @"sp|P12345|GENE_A", @"sp|Q67890|GENE_B" },
                    libEntry1.ProteinIds);
                CollectionAssert.AreEqual(
                    new[] { @"decoy_sp|P12345|GENE_A", @"decoy_sp|Q67890|GENE_B" },
                    libEntry2.ProteinIds);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestSkipsReplacementWhenProteinsColumnEmpty()
        {
            // Cross-impl port of Rust `manifest_skips_replacement_when_proteins_column_empty`.
            // Empty or "-" `proteins` field is a no-op; library wins.
            string path = WriteManifest(new[]
            {
                @"PEPTIDE	No	-	target	0",
                @"AEPTPID	Yes		decoy	0",
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                var libEntry1 = MakeEntry(1, @"PEPTIDE", 2, false);
                libEntry1.ProteinIds.Add(@"sp|original|FROM_LIB");
                var libEntry2 = MakeEntry(2, @"AEPTPID", 2, false);
                libEntry2.ProteinIds.Add(@"sp|orig_decoy|FROM_LIB");
                var lib = new List<LibraryEntry> { libEntry1, libEntry2 };
                var state = new PairingState();
                var stats = m.ApplyToLibrary(lib, state);
                Assert.AreEqual(0, stats.NProteinsReplaced);
                CollectionAssert.AreEqual(
                    new[] { @"sp|original|FROM_LIB" }, libEntry1.ProteinIds);
                CollectionAssert.AreEqual(
                    new[] { @"sp|orig_decoy|FROM_LIB" }, libEntry2.ProteinIds);
                // The second manifest row classifies AEPTPID as `decoy`
                // and the library entry is loaded as a target, so the
                // manifest's sequence-based classification flips IsDecoy
                // on it. Pin that side-effect so a future change to the
                // marking-before-pairing ordering doesn't silently slip.
                Assert.IsTrue(libEntry2.IsDecoy);
                Assert.AreEqual(1, stats.NNewlyMarkedDecoy);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestSkipsUnknownPeptideTypeRows()
        {
            string path = WriteManifest(new[]
            {
                @"PEPTIDEA	No	protA	target	0",
                @"AEPTPIDE	Yes	rev_protA	decoy	0",
                @"GIBBERISH	No	protX	bogus	7", // unknown type
            });
            try
            {
                var m = DecoyPairingManifest.FromTsv(path);
                Assert.AreEqual(2, m.Count);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [TestMethod]
        public void ManifestRejectsMissingRequiredColumns()
        {
            string path = Path.GetTempFileName();
            try
            {
                using (var w = new StreamWriter(path))
                {
                    w.WriteLine(@"foo	bar	baz");
                    w.WriteLine(@"a	b	c");
                }
                try
                {
                    DecoyPairingManifest.FromTsv(path);
                    Assert.Fail(@"expected InvalidDataException");
                }
                catch (InvalidDataException)
                {
                    // expected
                }
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static LibraryEntry AddProteins(LibraryEntry e, params string[] proteins)
        {
            foreach (var p in proteins)
                e.ProteinIds.Add(p);
            return e;
        }

        private static LibraryEntry FindBySequence(IList<LibraryEntry> lib, string sequence)
        {
            foreach (var e in lib)
            {
                if (e.Sequence == sequence)
                    return e;
            }
            Assert.Fail(@"Entry not found: {0}", sequence);
            return null;
        }

        private static LibraryEntry FindByDecoyAndCharge(
            IList<LibraryEntry> lib, string sequence, byte charge)
        {
            foreach (var e in lib)
            {
                if (e.Sequence == sequence && e.Charge == charge && e.IsDecoy)
                    return e;
            }
            Assert.Fail(@"Decoy not found: {0} z={1}", sequence, charge);
            return null;
        }
    }
}
