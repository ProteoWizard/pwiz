/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The pieces around an FDRBench pairing manifest that the fixture builds do not reach: the
    /// foreign-proteome entrapment pool (its filters, the co-location search, the similarity
    /// gate's budget and putting rejected candidates back), reading the manifest into DecoyPairs
    /// rows (entrapment pairs, reverse or cycle, charge and modification buckets), and the
    /// reconciler's refusals of malformed input.
    /// </summary>
    [TestClass]
    public class PairingReconciliationTest
    {
        /// <summary>A target whose 23 same-composition permutations all fail the similarity gate against it.</summary>
        private const string NEAR_COPY_TARGET = @"AAAAAAAADEFHAAAAAAAAK";

        public TestContext TestContext { get; set; }

        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Pairing_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            SQLiteConnection.ClearAllPools();
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void TestForeignEntrapmentSource()
        {
            // One foreign protein: every DEFH permutation of the near-copy target, two small
            // peptides, an I/L isobar of a real target and a peptide with an unknown residue,
            // split over lines with stray whitespace and in lower case; then an empty entry, which
            // ends the FASTA as it ends Carafe's reader.
            var permutations = Permutations(@"DEFH").Select(p => @"AAAAAAAA" + p + @"AAAAAAAAK").ToArray();
            Assert.AreEqual(24, permutations.Length);
            string protein = string.Concat(permutations) + @"SAMPLERSAMPLEKGGGGGGGKIVNEITEFAKGXGXGXGK";
            string fasta = Path.Combine(_folder, @"foreign.fasta");
            File.WriteAllText(fasta, ">sp|F1|FOREIGN\n" + protein.Substring(0, 30) + "\n " +
                                     protein.Substring(30, 40).ToLowerInvariant() + "\t\n" + protein.Substring(70) + "\n" +
                                     ">sp|EMPTY|E\n>sp|F2|NEVER_READ\nWWWWWWWWK\n");
            var digester = new Digester(new DigestSettings { MaxMissedCleavages = 0, ClipNTermMethionine = false });
            var excluded = new HashSet<string> { NEAR_COPY_TARGET };
            var excludedIl = new HashSet<string> { EntrapmentSequences.IlNormalize(@"LVNELTEFAK") };
            var log = new StringWriter();
            var pool = ForeignEntrapmentSource.Build(fasta, digester, excluded, excludedIl, false, new[] { 2, 3 }, 0, 0, log);
            // The target itself, its I/L isobar and the unknown residue are dropped.
            Assert.AreEqual(26, pool.Size);
            Assert.AreEqual(26, pool.Available);
            Assert.AreEqual(1, pool.IlCollisionsDropped);
            Assert.AreNotEqual(0, log.ToString().Length);

            // Every candidate at the target's mass shares its ladder: the gate's budget of 16 is
            // spent inside the co-location window, the search does not widen, and the rejected
            // candidates go back for other targets.
            double targetMass = Mass(NEAR_COPY_TARGET);
            Assert.IsNull(pool.Assign(NEAR_COPY_TARGET, targetMass));
            Assert.AreEqual(26, pool.Available);
            // With nothing within 6 Da, the search continues outward to the nearest candidate
            // (the lower-mass side here) that passes the gate.
            const string unrelated = @"LVNELTEFAK";
            string assigned = pool.Assign(unrelated, Mass(unrelated));
            Assert.AreEqual(@"SAMPLER", assigned);
            Assert.AreEqual(25, pool.Available);
            // A target lighter than the whole pool starts from the lightest bin.
            Assert.AreEqual(@"GGGGGGGK", pool.Assign(@"WWK", Mass(@"WWK")));
            // Each foreign peptide is used once, the near copies too once a target accepts them,
            // then the pool is exhausted.
            var used = new HashSet<string> { @"SAMPLER", @"GGGGGGGK" };
            string next;
            while ((next = pool.Assign(unrelated, Mass(unrelated))) != null)
            {
                Assert.IsTrue(used.Add(next), next);
                Assert.IsTrue(DecoySimilarityGate.IsCandidateAcceptable(unrelated, next), next);
            }
            Assert.AreEqual(26, used.Count);
            Assert.AreEqual(0, pool.Available);
            Assert.AreEqual(26, pool.Size);

            // The m/z filter keeps only peptides with a charge in range: the 21-residue permutations at 2+.
            var filtered = ForeignEntrapmentSource.Build(fasta, digester, excluded, excludedIl, true, new[] { 2, 3 }, 800, 1000);
            Assert.AreEqual(23, filtered.Size);
            // A FASTA with nothing usable is an error.
            string unusable = Path.Combine(_folder, @"unusable.fasta");
            File.WriteAllText(unusable, ">sp|U|U\nGXGXGXGKAAAAAAAADEFHAAAAAAAAK\n");
            Assert.ThrowsException<IOException>(() =>
                ForeignEntrapmentSource.Build(unusable, digester, excluded, excludedIl, false, new[] { 2 }, 0, 0));
        }

        [TestMethod]
        public void TestDecoyPairsFromManifest()
        {
            // Columns found by name in any order, trimmed values, a signed pair index, blank lines.
            string manifest = WriteLines(@"manifest.tsv",
                "peptide_pair_index\tpeptide_type\tsequence\textra\tdecoy",
                "+1\t target \tPEPTIDEK\tx\tNo",
                "1\tdecoy\tEDITPEPK\tx\tyes",
                "1\tp_target\tSAMPLERK\tx\tNo",
                string.Empty,
                "1\tp_decoy\tLPMSAERK\tx\tYES",
                "0\ttarget\tLVNELTEFAK\tx\tNo",
                "0\tdecoy\tAFETLENVLK\tx\tYes",
                "2\ttarget\tDECQYK\tx\tNo");
            var entries = DecoyPairPlanner.ReadManifest(manifest);
            Assert.AreEqual(7, entries.Count);
            Assert.AreEqual(1, entries[0].PairIndex);
            Assert.AreEqual(@"target", entries[0].PeptideType);
            Assert.IsFalse(entries[0].IsDecoy);
            Assert.IsTrue(entries[1].IsDecoy);
            Assert.IsTrue(entries[3].IsDecoy);

            var precursors = new List<DecoyPairPlanner.Precursor>
            {
                // Group 0: the decoy only at 3+, and the target only at 2+, so nothing pairs.
                new DecoyPairPlanner.Precursor(1, @"LVNELTEFAK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(2, @"AFETLENVLK", 3, string.Empty),
                // Group 1: target and reversed decoy at 2+, unmodified and oxidized; the 3+ target has no decoy.
                new DecoyPairPlanner.Precursor(3, @"PEPTIDEK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(4, @"PEPTIDEK", 2, @"Oxidation@M"),
                new DecoyPairPlanner.Precursor(5, @"PEPTIDEK", 3, string.Empty),
                new DecoyPairPlanner.Precursor(6, @"EDITPEPK", 2, @"Oxidation@M"),
                new DecoyPairPlanner.Precursor(7, @"EDITPEPK", 2, string.Empty),
                // The entrapment pair, whose decoy is a cycle, not a reverse.
                new DecoyPairPlanner.Precursor(8, @"SAMPLERK", 2, string.Empty),
                new DecoyPairPlanner.Precursor(9, @"LPMSAERK", 2, string.Empty),
                // Group 2 has no decoy.
                new DecoyPairPlanner.Precursor(10, @"DECQYK", 2, string.Empty),
            };
            var rows = DecoyPairPlanner.Plan(precursors, entries, out int skipped);
            Assert.AreEqual(0, skipped);
            CollectionAssert.AreEqual(new[] { 3, 7, 4, 6, 8, 9 }, rows.Select(r => r.RefSpectraId).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 1, 2, 2, 3, 3 }, rows.Select(r => r.PairId).ToArray());
            CollectionAssert.AreEqual(new[] { false, false, false, false, true, true }, rows.Select(r => r.IsEntrapment).ToArray());
            CollectionAssert.AreEqual(new[] { null, @"reverse", null, @"reverse", null, @"cycle" }, rows.Select(r => r.Method).ToArray());

            // A decoy the I/L normalization makes the target's own sequence would pair a
            // precursor with itself, which is skipped.
            var self = new List<DecoyPairPlanner.ManifestEntry>
            {
                new DecoyPairPlanner.ManifestEntry(@"PEPTIDEK", false, @"target", 0),
                new DecoyPairPlanner.ManifestEntry(@"PEPTLDEK", true, @"decoy", 0),
            };
            Assert.AreEqual(0, DecoyPairPlanner.Plan(precursors, self, out skipped).Count);
            Assert.AreEqual(3, skipped);

            // An empty file has no rows; a short row, a non-integer pair index or a missing column is an error.
            Assert.AreEqual(0, DecoyPairPlanner.ReadManifest(WriteLines(@"empty.tsv")).Count);
            AssertIoError(() => DecoyPairPlanner.ReadManifest(WriteLines(@"short.tsv", EntrapmentFastaBuilder.MANIFEST_HEADER, "PEPTIDEK\tNo\tP1")));
            AssertIoError(() => DecoyPairPlanner.ReadManifest(WriteLines(@"index.tsv", EntrapmentFastaBuilder.MANIFEST_HEADER,
                "PEPTIDEK\tNo\tP1\ttarget\tone")));
            AssertIoError(() => DecoyPairPlanner.ReadManifest(WriteLines(@"column.tsv", "sequence\tdecoy\tproteins\tpeptide_type")));
        }

        [TestMethod]
        public void TestReconcilerRefusals()
        {
            string manifest = WriteLines(@"manifest.tsv", EntrapmentFastaBuilder.MANIFEST_HEADER,
                "PEPTIDEK\tNo\tP1\ttarget\t0", "EDITPEPK\tYes\tdecoy_P1\tdecoy\t0",
                "SAMPLERK\tNo\tP2\ttarget\t1", "REMPLASK\tYes\tdecoy_P2\tdecoy\t1");
            string output = Path.Combine(_folder, @"out", @"reconciled.tsv");
            // A library holding a target without its decoy, and a peptide the manifest lacks.
            string library = WriteLines(@"library.tsv", "ModifiedPeptide\tStrippedPeptide\tPrecursorCharge",
                "_PEPTIDEK_\tPEPTIDEK\t2", "_EDITPEPK_\tEDITPEPK\t2", "_SAMPLERK_\tSAMPLERK\t2", "_ELVISK_\tELVISK\t2", string.Empty);
            var log = new StringWriter();
            var result = PairingManifestReconciler.Run(manifest, library, output, log);
            Assert.AreEqual(2, result.GroupsKept);
            Assert.AreEqual(1, result.KeptTargetsWithoutDecoy);
            Assert.AreEqual(1, result.LibraryPeptidesNotInManifest);
            Assert.AreEqual(3, result.RowsKept);
            Assert.AreEqual(4, result.LibraryPeptides);
            Assert.AreEqual(4, File.ReadAllLines(output).Length);
            // A null sequence normalizes to the empty string.
            Assert.AreEqual(string.Empty, PairingManifestReconciler.Normalize(null));

            // Each malformed input stops the run before anything is written.
            AssertRefused(WriteLines(@"empty_manifest.tsv"), library);
            AssertRefused(WriteLines(@"short_manifest.tsv", EntrapmentFastaBuilder.MANIFEST_HEADER, "PEPTIDEK\tNo\tP1\ttarget"), library);
            AssertRefused(WriteLines(@"column_manifest.tsv", "sequence\tdecoy\tproteins\tpeptide_type"), library);
            AssertRefused(manifest, null);
            AssertRefused(manifest, WriteLines(@"empty_library.tsv"));
            AssertRefused(manifest, WriteLines(@"column_library.tsv", "ModifiedPeptide\tPrecursorCharge"));
            AssertRefused(manifest, WriteLines(@"short_library.tsv", "ModifiedPeptide\tPrecursorCharge\tStrippedPeptide", "_PEPTIDEK_\t2"));
            // A .blib that is not a SQLite database reports the SQLite error as its cause.
            string notBlib = WriteLines(@"not_a_library.blib", "not a database");
            var e = AssertRefused(manifest, notBlib);
            Assert.IsInstanceOfType(e.InnerException, typeof(SQLiteException));
        }

        private IOException AssertRefused(string manifest, string library)
        {
            string output = Path.Combine(_folder, @"refused_" + Guid.NewGuid().ToString(@"N") + @".tsv");
            var e = Assert.ThrowsException<IOException>(() => PairingManifestReconciler.Run(manifest, library, output));
            Assert.IsFalse(File.Exists(output), library ?? manifest);
            return e;
        }

        private static void AssertIoError(Action action)
        {
            Assert.ThrowsException<IOException>(action);
        }

        private string WriteLines(string name, params string[] lines)
        {
            string path = Path.Combine(_folder, name);
            File.WriteAllText(path, lines.Length == 0 ? string.Empty : string.Join("\n", lines) + "\n");
            return path;
        }

        private static double Mass(string sequence)
        {
            double? mass = ResidueMasses.PeptideNeutralMass(sequence);
            Assert.IsTrue(mass.HasValue, sequence);
            return mass.Value;
        }

        private static IEnumerable<string> Permutations(string residues)
        {
            if (residues.Length <= 1)
                return new[] { residues };
            return residues.SelectMany((c, i) => Permutations(residues.Remove(i, 1)).Select(rest => c + rest));
        }
    }
}
