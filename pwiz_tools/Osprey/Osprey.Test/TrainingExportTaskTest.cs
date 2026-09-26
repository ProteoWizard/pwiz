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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// How the training export joins one run's artifacts: each reconciled target row to the
    /// library entry its entry_id names, and to its own second-pass record. A pairing it cannot
    /// make exactly is refused rather than exported, because a wrong pairing is a
    /// well-formed row with another precursor's peak or q-value in it.
    /// </summary>
    [TestClass]
    public class TrainingExportTaskTest
    {
        private const string RECONCILED = @"run1.scores-reconciled.parquet";
        private const string PASS2 = @"run1.2nd-pass.fdr_scores.bin";

        [TestMethod]
        public void TestTrainingExportPairing()
        {
            AssertEachRowGetsItsOwnRecord();
            AssertLibraryMismatchIsRefused();
            AssertRecordCollisionIsRefused();
        }

        /// <summary>
        /// Stage 6 gap-fill can leave two reconciled rows with one entry_id in a run (one
        /// target scored in two overlapping windows). Each row takes the record of its own
        /// peak, a row with no record takes no q-value, and a decoy or a released spectrum is
        /// not exported.
        /// </summary>
        private static void AssertEachRowGetsItsOwnRecord()
        {
            var library = Library(Entry(5, @"PEPTIDEK", 2), Entry(6, @"ELVISK", 2), Released(7, @"LIVESK", 2));
            var rows = new List<FdrEntry>
            {
                Row(5, @"PEPTIDEK", 2, 10.0),
                Row(5, @"PEPTIDEK", 2, 12.0),
                Row(6, @"ELVISK", 2, 11.0),
                Row(7, @"LIVESK", 2, 11.5),
                Row(9, @"KEDITPEP", 2, 10.0, isDecoy: true),
            };
            var records = new List<FdrScoreRecord>
            {
                new FdrScoreRecord(5, 4.0, 0.001, 0.001, 10.0),
                new FdrScoreRecord(5, 0.5, 0.2, 0.2, 12.0),
            };
            var targets = TrainingExportTask.PairTargets(RECONCILED, rows, library, PASS2, records, out int nNoLibrary);
            Assert.AreEqual(3, targets.Count);
            Assert.AreEqual(1, nNoLibrary, @"the released spectrum");
            Assert.AreEqual(10.0, targets[0].Row.ApexRt);
            Assert.AreEqual(0.001, targets[0].RunQ, @"the first peak's own q-value");
            Assert.AreEqual(4.0, targets[0].Score);
            Assert.AreEqual(12.0, targets[1].Row.ApexRt);
            Assert.AreEqual(0.2, targets[1].RunQ, @"the second peak's own q-value, not the last one read");
            Assert.AreEqual(0.5, targets[1].Score);
            Assert.IsTrue(double.IsNaN(targets[2].RunQ), @"a row with no record has no q-value");
        }

        /// <summary>
        /// A row whose modified sequence or charge is not that of the library entry its
        /// entry_id names came from another library (or another build's ids); exporting it
        /// would pair one precursor's ladder with another's peak.
        /// </summary>
        private static void AssertLibraryMismatchIsRefused()
        {
            var library = Library(Entry(5, @"PEPTIDEK", 2));
            foreach (var row in new[] { Row(5, @"PEPTIDER", 2, 10.0), Row(5, @"PEPTIDEK", 3, 10.0) })
            {
                var ex = Assert.ThrowsException<InvalidDataException>(() => TrainingExportTask.PairTargets(RECONCILED,
                    new List<FdrEntry> { row }, library, PASS2, new List<FdrScoreRecord>(), out _));
                StringAssert.Contains(ex.Message, RECONCILED);
            }
        }

        /// <summary>Two records for one observation cannot both be its q-value.</summary>
        private static void AssertRecordCollisionIsRefused()
        {
            var library = Library(Entry(5, @"PEPTIDEK", 2));
            var records = new List<FdrScoreRecord>
            {
                new FdrScoreRecord(5, 4.0, 0.001, 0.001, 10.0),
                new FdrScoreRecord(5, 0.5, 0.2, 0.2, 10.0),
            };
            var ex = Assert.ThrowsException<InvalidDataException>(() => TrainingExportTask.PairTargets(RECONCILED,
                new List<FdrEntry> { Row(5, @"PEPTIDEK", 2, 10.0) }, library, PASS2, records, out _));
            StringAssert.Contains(ex.Message, PASS2);
        }

        private static IReadOnlyDictionary<uint, LibraryEntry> Library(params LibraryEntry[] entries)
        {
            var library = new Dictionary<uint, LibraryEntry>();
            foreach (var entry in entries)
                library[entry.Id] = entry;
            return library;
        }

        private static LibraryEntry Entry(uint id, string modifiedSequence, byte charge)
        {
            var entry = new LibraryEntry(id, modifiedSequence, modifiedSequence, charge, 500.0, 10.0);
            entry.Fragments = new List<LibraryFragment> { new LibraryFragment { Mz = 300.0, RelativeIntensity = 1f } };
            return entry;
        }

        private static LibraryEntry Released(uint id, string modifiedSequence, byte charge)
        {
            var entry = Entry(id, modifiedSequence, charge);
            entry.ReleaseSpectrum();
            return entry;
        }

        private static FdrEntry Row(uint entryId, string modifiedSequence, byte charge, double apexRt, bool isDecoy = false)
        {
            return new FdrEntry
            {
                EntryId = entryId,
                IsDecoy = isDecoy,
                ModifiedSequence = modifiedSequence,
                Charge = charge,
                ApexRt = apexRt,
            };
        }
    }
}
