/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Text;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Contracts;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="PairingIds.Assign"/> and <see cref="EntrapmentPairing"/> - the
    /// reconciliation of the searched library against the entrapment pairing manifest.
    /// Verifies that the ONLY entrapment peptides dropped are the understood
    /// N-terminal-Met-clip artifacts (a clipped entrapment whose manifest-paired target
    /// cannot be clipped, so no matching clipped target can exist), that legitimate matched
    /// clip pairs are kept and paired, covered peptides keep their manifest pairing, and
    /// anything unexplained is surfaced rather than silently dropped. Every entry gets an id;
    /// an orphan is one whose group holds no original target.
    /// </summary>
    [TestClass]
    public class EntrapmentPairingTest
    {
        [TestMethod]
        public void TestEntrapmentPairing()
        {
            // Manifest (clean 1:1 quartets, as the entrapment generator writes it):
            //   idx 0: covered target/entrapment (neither starts with M)
            //   idx 1: target has NO N-term M, entrapment DOES (-> its Met clip is an orphan)
            //   idx 2: BOTH start with M (-> their Met clips are a matched pair)
            var manifest = new (string seq, string type, int idx)[]
            {
                ("COVTGTK",  "target",   0),
                ("COVENTK",  "p_target", 0),
                ("TGTBK",    "target",   1),   // no M
                ("MENTBK",   "p_target", 1),   // starts with M
                ("MTGTCK",   "target",   2),   // starts with M
                ("MENTCK",   "p_target", 2),   // starts with M
                ("MTGTDK",   "target",   3),   // starts with M; its clip "TGTDK" collides with a covered peptide
                ("MENTDK",   "p_target", 3),   // starts with M
                ("TGTDK",    "target",   4),   // COVERED peptide equal to MTGTDK's clip
                ("ENTD4K",   "p_target", 4),
            };
            string manifestPath = WriteTempManifest(manifest);
            try
            {
                // Library: what Carafe actually predicted (a subset, plus Met-clip forms).
                uint id = 10;
                var lib = new Dictionary<uint, LibraryEntry>();
                Add(lib, ref id, "COVTGTK", target: true);   // covered target
                Add(lib, ref id, "COVENTK", target: false);  // covered entrapment
                Add(lib, ref id, "ENTBK",   target: false);  // ORPHAN: M+ENTBK=MENTBK (idx1), target TGTBK has no M
                Add(lib, ref id, "ENTCK",   target: false);  // MATCHED: M+ENTCK=MENTCK (idx2), target MTGTCK starts M
                Add(lib, ref id, "TGTCK",   target: true);   //   ...and its clipped target (MTGTCK clipped) is present
                Add(lib, ref id, "ENTDK",   target: false);  // ORPHAN: its clipped target "TGTDK" is a COVERED peptide, not an extra
                Add(lib, ref id, "TGTDK",   target: true);   //   the covered peptide the clip collides with
                Add(lib, ref id, "ENTD4K",  target: false);  //   its manifest partner (idx 4 is a valid covered pair)
                Add(lib, ref id, "WEIRDK",  target: false);  // UNEXPLAINED: M+WEIRDK not in the manifest

                // The ids are assigned at load, from the manifest; the pairing then reads the entries.
                var summary = PairingIds.Assign(lib.Values.ToList(), DecoyPairingManifest.FromTsv(manifestPath));
                var pairing = EntrapmentPairing.Build(lib);

                // Only orphans and the unexplained entrapment are excluded.
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("ENTBK"), "orphan must be dropped");
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("WEIRDK"), "unexplained must be excluded");
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("ENTDK"), "clipped target colliding with a covered peptide -> orphan");
                Assert.IsFalse(pairing.ExcludedEntrapment.Contains("ENTCK"), "matched clip pair must be kept");
                Assert.IsFalse(pairing.ExcludedEntrapment.Contains("COVENTK"), "covered entrapment must be kept");
                Assert.AreEqual(3, pairing.ExcludedEntrapment.Count);

                // The two orphans are Met-clip artifacts; WEIRDK is surfaced, not silently dropped.
                Assert.AreEqual(2, summary.MetClipDropped);
                Assert.AreEqual(1, summary.Unexplained.Count);
                Assert.AreEqual("WEIRDK", summary.Unexplained[0]);
                // The covered collision pair keeps its own manifest index.
                Assert.AreEqual(4u, pairing.PairIndexBySeq["TGTDK"]);
                Assert.AreEqual(4u, pairing.PairIndexBySeq["ENTD4K"]);

                // Covered peptides keep the manifest pairing (shared index 0).
                Assert.AreEqual(0u, pairing.PairIndexBySeq["COVTGTK"]);
                Assert.AreEqual(0u, pairing.PairIndexBySeq["COVENTK"]);

                // The matched clip pair is kept and paired: the clipped entrapment and
                // the clipped target share one index, distinct from the manifest range.
                Assert.IsTrue(pairing.PairIndexBySeq.ContainsKey("ENTCK"));
                Assert.IsTrue(pairing.PairIndexBySeq.ContainsKey("TGTCK"));
                Assert.AreEqual(pairing.PairIndexBySeq["ENTCK"], pairing.PairIndexBySeq["TGTCK"]);
                Assert.IsTrue(pairing.PairIndexBySeq["ENTCK"] > 2u, "clip index must sit above the manifest range");

                // Excluded peptides are not in the pairing, though each has an id of its own.
                Assert.IsFalse(pairing.PairIndexBySeq.ContainsKey("ENTBK"));
                Assert.IsFalse(pairing.PairIndexBySeq.ContainsKey("WEIRDK"));
                Assert.AreEqual(3, lib.Values.Where(e => pairing.ExcludedEntrapment.Contains(e.Sequence)).Select(e => e.PairingId).Distinct().Count());

                // No manifest and no entrapment (the loader refuses an entrapment library without
                // its pairing): every peptide its own group, nothing excluded.
                var plain = lib.Where(kv => !EntrapmentLibraryClassifier.IsEntrapment(kv.Value.ProteinIds))
                    .ToDictionary(kv => kv.Key, kv => kv.Value);
                var noManifestSummary = PairingIds.Assign(plain.Values.ToList(), null);
                var noManifest = EntrapmentPairing.Build(plain);
                Assert.AreEqual(0, noManifest.ExcludedEntrapment.Count);
                Assert.AreEqual(0, noManifestSummary.MetClipDropped);
                Assert.AreEqual(plain.Count, noManifest.PairIndexBySeq.Count);
                Assert.AreEqual(plain.Count, noManifest.PairIndexBySeq.Values.Distinct().Count());
            }
            finally
            {
                File.Delete(manifestPath);
            }

            TestEntrapmentOnTargetProtein();
        }

        /// <summary>
        /// Entrapment placed on a real target protein, the protein-aware audit's design: its
        /// manifest row is p_target but its accessions are the target's own, clean ones, so
        /// nothing but the manifest says it is entrapment. The manifest's kind must set
        /// <see cref="LibraryEntry.IsEntrapment"/>, and the classifier must read the entry, not
        /// the accessions: entrapment, though not on an entrapment protein. Its p_decoy likewise.
        /// A marked entrapment is both; a target and its decoy are neither.
        /// </summary>
        private static void TestEntrapmentOnTargetProtein()
        {
            const string cleanProtein = @"sp|P1|G1";
            const string markedProtein = @"sp|P2_p_target|G2_p_target";
            const string decoyPrefix = @"decoy_";
            string manifestPath = WriteTempManifest(new (string seq, string type, string proteins, int idx)[]
            {
                ("TGTAK", "target",   cleanProtein,               0),
                ("ENTAK", "p_target", cleanProtein,               0),   // on its target's protein: no marker
                ("KATGT", "decoy",    decoyPrefix + cleanProtein, 0),
                ("KATNE", "p_decoy",  decoyPrefix + cleanProtein, 0),
                ("TGTBK", "target",   @"sp|P2|G2",                1),
                ("ENTBK", "p_target", markedProtein,              1),   // the standard design: marked
                ("KBTNE", "p_decoy",  decoyPrefix + markedProtein, 1),
            });
            try
            {
                var lib = new List<LibraryEntry>
                {
                    Entry(1, "TGTAK", false, cleanProtein),
                    Entry(2, "ENTAK", false, cleanProtein),
                    Entry(3, "KATGT", true, decoyPrefix + cleanProtein),
                    Entry(4, "KATNE", true, decoyPrefix + cleanProtein),
                    Entry(5, "TGTBK", false, @"sp|P2|G2"),
                    Entry(6, "ENTBK", false, markedProtein),
                    Entry(7, "KBTNE", true, decoyPrefix + markedProtein),
                };
                // Before the manifest, the accessions are all there is: the on-target rows read
                // as a plain target and decoy, the marked ones as entrapment.
                Assert.AreEqual(PeptideKind.Target, EntrapmentLibraryClassifier.Classify(lib[1]));
                Assert.AreEqual(PeptideKind.Decoy, EntrapmentLibraryClassifier.Classify(lib[3]));
                Assert.AreEqual(PeptideKind.PTarget, EntrapmentLibraryClassifier.Classify(lib[5]));
                Assert.IsTrue(lib.All(e => !e.IsEntrapment));

                var stats = DecoyPairingManifest.FromTsv(manifestPath).ApplyToLibrary(lib, new PairingState());
                Assert.AreEqual(3, stats.NPaired, @"every decoy pairs with its target or entrapment");

                AssertClass(lib[0], PeptideKind.Target, flagged: false, onEntrapmentProtein: false);
                AssertClass(lib[1], PeptideKind.PTarget, flagged: true, onEntrapmentProtein: false);
                AssertClass(lib[2], PeptideKind.Decoy, flagged: false, onEntrapmentProtein: false);
                AssertClass(lib[3], PeptideKind.PDecoy, flagged: true, onEntrapmentProtein: false);
                AssertClass(lib[4], PeptideKind.Target, flagged: false, onEntrapmentProtein: false);
                AssertClass(lib[5], PeptideKind.PTarget, flagged: true, onEntrapmentProtein: true);
                AssertClass(lib[6], PeptideKind.PDecoy, flagged: true, onEntrapmentProtein: true);
                // The on-target entrapment keeps the clean accession the manifest gave it.
                CollectionAssert.AreEqual(new[] { cleanProtein }, lib[1].ProteinIds.ToArray());
                Assert.IsFalse(EntrapmentLibraryClassifier.IsEntrapment(lib[1].ProteinIds));

                // The flag alone, with no manifest: the accession overloads still read the marker only.
                var flaggedOnly = Entry(8, "ENTCK", false, cleanProtein);
                flaggedOnly.IsEntrapment = true;
                AssertClass(flaggedOnly, PeptideKind.PTarget, flagged: true, onEntrapmentProtein: false);
                Assert.AreEqual(PeptideKind.Target, EntrapmentLibraryClassifier.Classify(flaggedOnly.ProteinIds));
            }
            finally
            {
                File.Delete(manifestPath);
            }
        }

        private static void AssertClass(LibraryEntry entry, PeptideKind kind, bool flagged, bool onEntrapmentProtein)
        {
            Assert.AreEqual(flagged, entry.IsEntrapment, entry.Sequence);
            Assert.AreEqual(PeptideKinds.IsEntrapment(kind), EntrapmentLibraryClassifier.IsEntrapment(entry), entry.Sequence);
            Assert.AreEqual(onEntrapmentProtein, EntrapmentLibraryClassifier.IsOnEntrapmentProtein(entry), entry.Sequence);
            Assert.AreEqual(kind, EntrapmentLibraryClassifier.Classify(entry), entry.Sequence);
        }

        private static LibraryEntry Entry(uint id, string seq, bool decoy, string protein)
        {
            var e = new LibraryEntry(id, seq, seq, 2, 500.0, 10.0);
            e.ProteinIds = new List<string> { protein };
            e.IsDecoy = decoy;
            if (decoy)
                e.Id |= LibraryEntry.DECOY_ID_BIT;
            return e;
        }

        private static void Add(Dictionary<uint, LibraryEntry> lib, ref uint id, string seq, bool target)
        {
            string protein = target ? @"sp|P" + id + @"|G" + id
                                     : @"sp|P" + id + @"_p_target|G" + id + @"_p_target";
            var e = new LibraryEntry(id, seq, seq, 2, 500.0, 10.0);
            e.ProteinIds = new List<string> { protein };
            lib[id] = e;
            id++;
        }

        private static string WriteTempManifest((string seq, string type, int idx)[] rows)
        {
            return WriteTempManifest(rows.Select(r => (r.seq, r.type, @"sp|X|X", r.idx)).ToArray());
        }

        private static string WriteTempManifest((string seq, string type, string proteins, int idx)[] rows)
        {
            string path = Path.GetTempFileName();
            var sb = new StringBuilder();
            sb.Append("sequence\tdecoy\tproteins\tpeptide_type\tpeptide_pair_index\n");
            foreach (var r in rows)
            {
                string decoy = r.type.EndsWith("decoy") ? "Yes" : "No";
                sb.Append(r.seq).Append('\t').Append(decoy).Append('\t').Append(r.proteins).Append('\t')
                    .Append(r.type).Append('\t').Append(r.idx).Append('\n');
            }
            File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
