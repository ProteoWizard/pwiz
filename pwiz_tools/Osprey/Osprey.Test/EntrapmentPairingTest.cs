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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for <see cref="EntrapmentPairing"/> -- the reconciliation of the
    /// searched library against the entrapment pairing manifest. Verifies that the
    /// ONLY entrapment peptides dropped are the understood N-terminal-Met-clip
    /// artifacts (a clipped entrapment whose manifest-paired target cannot be
    /// clipped, so no matching clipped target can exist), that legitimate matched
    /// clip pairs are kept and paired, covered peptides keep their manifest pairing,
    /// and anything unexplained is surfaced rather than silently dropped.
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

                var pairing = EntrapmentPairing.Build(lib, manifestPath);

                // Only orphans and the unexplained entrapment are excluded.
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("ENTBK"), "orphan must be dropped");
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("WEIRDK"), "unexplained must be excluded");
                Assert.IsTrue(pairing.ExcludedEntrapment.Contains("ENTDK"), "clipped target colliding with a covered peptide -> orphan");
                Assert.IsFalse(pairing.ExcludedEntrapment.Contains("ENTCK"), "matched clip pair must be kept");
                Assert.IsFalse(pairing.ExcludedEntrapment.Contains("COVENTK"), "covered entrapment must be kept");
                Assert.AreEqual(3, pairing.ExcludedEntrapment.Count);

                // The two orphans are Met-clip artifacts; WEIRDK is surfaced, not silently dropped.
                Assert.AreEqual(2, pairing.MetClipDroppedCount);
                Assert.AreEqual(1, pairing.UnexplainedEntrapment.Count);
                Assert.AreEqual("WEIRDK", pairing.UnexplainedEntrapment[0]);
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

                // Dropped peptides get no pair index.
                Assert.IsFalse(pairing.PairIndexBySeq.ContainsKey("ENTBK"));
                Assert.IsFalse(pairing.PairIndexBySeq.ContainsKey("WEIRDK"));

                // No manifest -> nothing is classified as a Met-clip artifact (no reference to reconcile against).
                var noManifest = EntrapmentPairing.Build(lib, null);
                Assert.AreEqual(0, noManifest.ExcludedEntrapment.Count);
                Assert.AreEqual(0, noManifest.MetClipDroppedCount);
            }
            finally
            {
                File.Delete(manifestPath);
            }
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
            string path = Path.GetTempFileName();
            var sb = new StringBuilder();
            sb.Append("sequence\tdecoy\tproteins\tpeptide_type\tpeptide_pair_index\n");
            foreach (var r in rows)
                sb.Append(r.seq).Append("\tNo\tsp|X|X\t").Append(r.type).Append('\t').Append(r.idx).Append('\n');
            File.WriteAllText(path, sb.ToString());
            return path;
        }
    }
}
