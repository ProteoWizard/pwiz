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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.IO;

namespace pwiz.OspreySharp.Test
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
                int nPaired = m.ApplyToLibrary(lib, state);
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
                var state = new PairingState();
                int nPaired = m.ApplyToLibrary(lib, state);
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
                int nPaired = m.ApplyToLibrary(lib, state);
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
                int nManifest = m.ApplyToLibrary(lib, state);
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
