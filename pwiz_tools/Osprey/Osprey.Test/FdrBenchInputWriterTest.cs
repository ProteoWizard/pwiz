/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/maccoss/osprey)
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for <see cref="FdrBenchInputWriter"/>: per-precursor and per-peptide dedup keeping the
    /// best (min q-value, ties by max score), per-run emission, decoy exclusion, the SVM discriminant
    /// landing in the score column, and protein-column joining and oversize truncation. Mirrors the
    /// Rust osprey-io output/fdrbench.rs unit tests.
    /// </summary>
    [TestClass]
    public class FdrBenchInputWriterTest
    {
        // Column indices in the peptide/precursor TSV.
        private const int COL_PEPTIDE = 0;
        private const int COL_MOD_PEPTIDE = 1;
        private const int COL_CHARGE = 2;
        private const int COL_SCORE = 4;
        private const int COL_PROTEIN = 5;

        [TestMethod]
        public void TestFdrBenchInputWriter()
        {
            PrecursorDedupKeepsBestScore();
            PeptideLevelDedupsAcrossCharges();
            DecoysAreExcluded();
            PerRunEmitsRowPerObservation();
            ProteinColumnJoinsWithSemicolons();
            OversizeProteinListIsTruncated();
            DedupOutputIsOrderedDeterministically();
        }

        private static void PrecursorDedupKeepsBestScore()
        {
            var lib = new List<LibraryEntry> { MakeLib(1, @"PEPTIDE", @"PEPTIDE", 2, @"P1") };
            // Same precursor scored in two runs; run_b is better (lower q, higher score).
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a", MakeEntry(1, false, @"PEPTIDE", 2, 0.05, 0.05, 1.2)),
                Run(@"run_b", MakeEntry(1, false, @"PEPTIDE", 2, 0.01, 0.01, 3.4)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, false);
            Assert.AreEqual(2, lines.Count, @"header + 1 deduped row");
            var cols = lines[1].Split('\t');
            Assert.AreEqual(@"PEPTIDE", cols[COL_PEPTIDE]);
            Assert.AreEqual(@"P1", cols[COL_PROTEIN]);
            Assert.AreEqual(Sci(3.4), cols[COL_SCORE], @"expected the best run's discriminant (3.4)");
        }

        private static void PeptideLevelDedupsAcrossCharges()
        {
            var lib = new List<LibraryEntry>
            {
                MakeLib(1, @"PEPTIDE", @"PEPTIDE", 2, @"P1"),
                MakeLib(2, @"PEPTIDE", @"PEPTIDE", 3, @"P1"),
            };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a",
                    MakeEntry(1, false, @"PEPTIDE", 2, 0.05, 0.05, 1.0),
                    MakeEntry(2, false, @"PEPTIDE", 3, 0.02, 0.02, 2.0)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Peptide, false);
            Assert.AreEqual(2, lines.Count, @"header + 1 peptide row (dedup across charges)");
        }

        private static void DecoysAreExcluded()
        {
            var lib = new List<LibraryEntry> { MakeLib(1, @"PEPTIDE", @"PEPTIDE", 2, @"P1") };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a",
                    MakeEntry(1, false, @"PEPTIDE", 2, 0.01, 0.01, 3.0),
                    // High bit set = decoy id; IsDecoy true.
                    MakeEntry(1u | 0x80000000u, true, @"DECOY_PEPTIDE", 2, 0.5, 0.5, -1.0)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, false);
            Assert.AreEqual(2, lines.Count, @"header + 1 target row (decoy excluded)");
            Assert.IsFalse(lines[1].Contains(@"DECOY_"), @"decoy must not appear");
        }

        private static void PerRunEmitsRowPerObservation()
        {
            var lib = new List<LibraryEntry> { MakeLib(1, @"PEPTIDE", @"PEPTIDE", 2, @"P1") };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a", MakeEntry(1, false, @"PEPTIDE", 2, 0.05, 0.05, 1.2)),
                Run(@"run_b", MakeEntry(1, false, @"PEPTIDE", 2, 0.01, 0.01, 3.4)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, true);
            Assert.AreEqual(3, lines.Count, @"header + one row per observation");
            Assert.IsTrue(lines[0].EndsWith(@"run"), @"per-run header has a run column");
            Assert.IsTrue(lines.Any(l => l.EndsWith("\trun_a")), @"row for run_a");
            Assert.IsTrue(lines.Any(l => l.EndsWith("\trun_b")), @"row for run_b");
        }

        private static void ProteinColumnJoinsWithSemicolons()
        {
            // Entrapment marker (_p_target) must survive in the protein column.
            var lib = new List<LibraryEntry> { MakeLib(42, @"PEPTIDE", @"PEPTIDE", 2, @"P1", @"P2_p_target") };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a", MakeEntry(42, false, @"PEPTIDE", 2, 0.01, 0.01, 3.0)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, false);
            Assert.AreEqual(@"P1;P2_p_target", lines[1].Split('\t')[COL_PROTEIN]);
        }

        private static void OversizeProteinListIsTruncated()
        {
            // 200 IDs of ~22 chars joined = ~4400 chars, over the 4000-char cap.
            var ids = Enumerable.Range(0, 200)
                .Select(i => string.Format(@"sp|A{0:D8}|PROT_{1:D4}", i, i))
                .ToArray();
            var lib = new List<LibraryEntry> { MakeLib(7, @"PEPTIDE", @"PEPTIDE", 2, ids) };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_a", MakeEntry(7, false, @"PEPTIDE", 2, 0.01, 0.01, 3.0)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, false);
            string protein = lines[1].Split('\t')[COL_PROTEIN];
            Assert.IsTrue(protein.Length <= 4000, @"protein field stays under the 4000-char cap");
            Assert.IsTrue(protein.Contains(@";...+") && protein.EndsWith(@"_more"), @"truncation marker present");
            Assert.IsTrue(protein.StartsWith(@"sp|A00000000|PROT_0000"), @"first ID survives intact");
        }

        private static void DedupOutputIsOrderedDeterministically()
        {
            // Distinct precursors supplied deliberately out of sorted order and split across
            // runs, so a Dictionary-iteration-order writer would emit them unsorted. The writer
            // must sort by (modified sequence ordinal, charge), giving stable, diff-friendly TSV.
            var lib = new List<LibraryEntry>
            {
                MakeLib(1, @"PEPTIDEK", @"PEPTIDEK", 2, @"P1"),
                MakeLib(2, @"ANCHORR", @"ANCHORR", 3, @"P2"),
                MakeLib(3, @"ANCHORR", @"ANCHORR", 2, @"P2"),
                MakeLib(4, @"ELVISLIVESK", @"ELVISLIVESK", 2, @"P3"),
            };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                Run(@"run_b",
                    MakeEntry(4, false, @"ELVISLIVESK", 2, 0.04, 0.04, 1.0),
                    MakeEntry(1, false, @"PEPTIDEK", 2, 0.03, 0.03, 1.0)),
                Run(@"run_a",
                    MakeEntry(2, false, @"ANCHORR", 3, 0.02, 0.02, 1.0),
                    MakeEntry(3, false, @"ANCHORR", 2, 0.01, 0.01, 1.0)),
            };
            var lines = RunWriter(perFile, lib, FdrLevel.Precursor, false);
            var observed = lines.Skip(1)
                .Select(l => l.Split('\t'))
                .Select(c => c[COL_MOD_PEPTIDE] + @"/" + c[COL_CHARGE])
                .ToList();
            var expected = new List<string> { @"ANCHORR/2", @"ANCHORR/3", @"ELVISLIVESK/2", @"PEPTIDEK/2" };
            CollectionAssert.AreEqual(expected, observed, @"rows must be sorted by (mod sequence, charge)");
        }

        private static FdrEntry MakeEntry(uint entryId, bool isDecoy, string modSeq, byte charge,
            double runQPrecursor, double expQPrecursor, double score)
        {
            return new FdrEntry
            {
                EntryId = entryId,
                IsDecoy = isDecoy,
                Charge = charge,
                ModifiedSequence = modSeq,
                Score = score,
                RunPrecursorQvalue = runQPrecursor,
                RunPeptideQvalue = runQPrecursor,
                ExperimentPrecursorQvalue = expQPrecursor,
                ExperimentPeptideQvalue = expQPrecursor,
            };
        }

        private static LibraryEntry MakeLib(uint id, string sequence, string modSeq, byte charge,
            params string[] proteins)
        {
            var entry = new LibraryEntry(id, sequence, modSeq, charge, 0.0, 0.0);
            entry.ProteinIds = proteins.ToList();
            return entry;
        }

        private static KeyValuePair<string, List<FdrEntry>> Run(string name, params FdrEntry[] entries)
        {
            return new KeyValuePair<string, List<FdrEntry>>(name, entries.ToList());
        }

        private static string Sci(double value)
        {
            return value.ToString(@"E10", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static List<string> RunWriter(List<KeyValuePair<string, List<FdrEntry>>> perFile,
            List<LibraryEntry> lib, FdrLevel level, bool perRun)
        {
            var byId = lib.ToDictionary(e => e.Id, e => e);
            string path = Path.Combine(Path.GetTempPath(), @"fdrbench_test_" + Guid.NewGuid().ToString(@"N") + @".tsv");
            try
            {
                FdrBenchInputWriter.WritePeptideInput(path, perFile, byId, level, perRun);
                return File.ReadAllLines(path).ToList();
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }
    }
}
