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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Training;
using static pwiz.CarafeSharp.Test.OspreyTestRecords;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Osprey training exports as parquet files built in the test, column by column with the
    /// types of Osprey's writer: reading them back field for field (across row groups, with
    /// NULL cells, and with every footer key), the reader's errors, and the training set built
    /// from two runs, down to the Carafe training tables written from it.
    /// </summary>
    [TestClass]
    public class OspreyTrainingExportTest
    {
        private const string FORMAT_VERSION_KEY = @"osprey.training_export.format_version";

        public TestContext TestContext { get; set; }

        private string _folder;

        [TestInitialize]
        public void CreateFolder()
        {
            _folder = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"Export_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void TestTrainingExportRoundTrip()
        {
            // Three records over two row groups: one with modifications, one with NULL blobs
            // (empty arrays), one with no ladder at all.
            var modified = CleanRecord(@"AMCPEPTK", 2);
            modified.ModPositions = new[] { 1, 2 };
            modified.ModMasses = new[] { 15.994915, 57.021464 };
            modified.ModUnimodIds = new[] { 35, 4 };
            modified.ModifiedSequence = @"AM(UniMod:35)C(UniMod:4)PEPTK";
            modified.IsEntrapment = true;
            modified.MedianPolishFitted = true;
            modified.MedianPolishResidualMad = 0.25;
            modified.SharedApexCount[4] = 3;
            modified.FiniteScanCount[5] = 65535;
            var nullBlobs = CleanRecord(@"PEPTIDEK", 3);
            nullBlobs.LibraryRelIntensity = Array.Empty<float>();
            nullBlobs.MinClaimantQ = Array.Empty<float>();
            var noLadder = NewRecord(@"K", 1);
            var records = Number(modified, nullBlobs, noLadder);
            string path = WriteExport(@"run_a", records, Footer(@"a"), 2);

            var export = OspreyTrainingExport.Read(path);
            Assert.AreEqual(path, export.Path);
            Assert.AreEqual(3, export.Records.Count);
            for (int i = 0; i < records.Length; i++)
                AssertSameRecord(records[i], export.Records[i]);
            CollectionAssert.AreEquivalent(Footer(@"a").ToArray(), export.Metadata.ToArray());

            // The footer keys, as present, empty, malformed or missing.
            Assert.AreEqual(10.0, export.RtMax);
            Assert.AreEqual(@"Q Exactive HF", export.InstrumentModel);
            Assert.AreEqual((400.5, 900.5), export.IsolationRange);
            Assert.AreEqual((200.25, 1800.0), export.Ms2ScanWindow);
            Assert.AreEqual(30.0, export.DominantCollisionEnergy);
            AssertFooter(@"osprey.instrument_model", string.Empty, e => Assert.IsNull(e.InstrumentModel));
            AssertFooter(@"osprey.training_export.run_q_pass", @"1", e => Assert.AreEqual(@"1", e.RunQPass));
            AssertFooter(@"osprey.training_export.run_q_pass", null, e => Assert.IsNull(e.RunQPass));
            AssertFooter(@"osprey.ms2_scan_window", @"200", e => Assert.IsNull(e.Ms2ScanWindow));
            AssertFooter(@"osprey.ms2_scan_window", @"200,high", e => Assert.IsNull(e.Ms2ScanWindow));
            AssertFooter(@"osprey.ms2_scan_window", null, e => Assert.IsNull(e.Ms2ScanWindow));
            AssertFooter(@"osprey.collision_energies", null, e => Assert.IsNull(e.DominantCollisionEnergy));
            // The most frequent energy; a tie keeps the first, and a key that is not a number is skipped.
            AssertFooter(@"osprey.collision_energies", @"{""x"":999,""27"":40,""35"":40,""25"":1}",
                e => Assert.AreEqual(27.0, e.DominantCollisionEnergy));
            AssertFooter(@"osprey.collision_energies", @"{""x"":5}", e => Assert.IsNull(e.DominantCollisionEnergy));
            AssertFooter(@"osprey.rt_max", null, e => Assert.ThrowsException<InvalidDataException>(() => e.RtMax));
            AssertFooter(@"osprey.isolation_mz_min", null, e => Assert.ThrowsException<InvalidDataException>(() => e.IsolationRange));

            // The footer alone, without the rows.
            var footer = OspreyTrainingExport.ReadFooter(path);
            Assert.AreEqual(@"search_a", footer[OspreyTrainingExport.SEARCH_HASH_KEY]);

            // A header-only export has no records.
            Assert.AreEqual(0, OspreyTrainingExport.Read(WriteExport(@"empty", Array.Empty<OspreyTrainingRecord>(), Footer(@"a"), 1)).Records.Count);

            // Format 1, which also carried the experiment q-value and PEP, reads the same.
            var footerV1 = Footer(@"a");
            footerV1[FORMAT_VERSION_KEY] = @"1";
            string v1 = Path.Combine(_folder, @"v1" + OspreyTrainingExport.FILE_SUFFIX);
            ParquetColumns.Write(v1, Columns(records).Concat(new[]
            {
                Column(@"experiment_precursor_q", records.Select(x => x.RunPrecursorQ).ToArray()),
                Column(@"pep", records.Select(x => 0.01).ToArray()),
            }).ToArray(), footerV1, 2);
            CollectionAssert.AreEqual(records.Select(x => x.ModifiedSequence).ToArray(),
                OspreyTrainingExport.Read(v1).Records.Select(x => x.ModifiedSequence).ToArray());

            // What the reader refuses: another format version or none, a record whose ladder has
            // the wrong number of slots, and a missing column.
            var footerV3 = Footer(@"a");
            footerV3[FORMAT_VERSION_KEY] = @"3";
            AssertInvalid(WriteExport(@"v3", records, footerV3, 2), null);
            var noVersion = Footer(@"a");
            noVersion.Remove(FORMAT_VERSION_KEY);
            AssertInvalid(WriteExport(@"v0", records, noVersion, 2), @"(none)");
            var truncated = CleanRecord(@"PEPTIDEK", 2);
            truncated.IonFlags = truncated.IonFlags.Take(20).ToArray();
            AssertInvalid(WriteExport(@"short", Number(truncated), Footer(@"a"), 1), truncated.ModifiedSequence);
            string noColumn = Path.Combine(_folder, @"nocolumn" + OspreyTrainingExport.FILE_SUFFIX);
            ParquetColumns.Write(noColumn, Columns(records).Where(c => c.Key != @"polish_r2").ToArray(), Footer(@"a"), 2);
            AssertInvalid(noColumn, @"polish_r2");
        }

        [TestMethod]
        public void TestParquetColumns()
        {
            // Nullable columns without nulls read as their value type, a null is an error, a
            // column reads as any type it converts to, and row groups concatenate.
            string path = Path.Combine(_folder, @"columns.parquet");
            var columns = new List<KeyValuePair<string, Array>>
            {
                new KeyValuePair<string, Array>(@"a", new int?[] { 1, 2, 3 }),
                new KeyValuePair<string, Array>(@"b", new int?[] { 1, null, 3 }),
                new KeyValuePair<string, Array>(@"c", new long[] { 4, 5, 6 }),
                new KeyValuePair<string, Array>(@"d", new[] { @"x", null, @"z" }),
            };
            ParquetColumns.Write(path, columns, new Dictionary<string, string> { { @"k", @"v" } }, 1);
            var read = ParquetColumns.Read(path, @"a", @"b", @"c", @"d");
            Assert.AreEqual(3, read.RowCount);
            Assert.AreEqual(@"v", read.Metadata[@"k"]);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, read.Get<int>(@"a"));
            CollectionAssert.AreEqual(new int?[] { 1, null, 3 }, read.Get<int?>(@"b"));
            Assert.ThrowsException<InvalidDataException>(() => read.Get<int>(@"b"));
            CollectionAssert.AreEqual(new long[] { 4, 5, 6 }, read.Get<long>(@"c"));
            CollectionAssert.AreEqual(new[] { 4.0, 5.0, 6.0 }, read.Get<double>(@"c"));
            CollectionAssert.AreEqual(new[] { @"x", null, @"z" }, read.Get<string>(@"d"));
            Assert.ThrowsException<ArgumentException>(() => read.Get<int>(@"e"));
            Assert.ThrowsException<InvalidDataException>(() => ParquetColumns.Read(path, @"a", @"e"));
            Assert.AreEqual(0, ParquetColumns.Read(path).RowCount);
            Assert.AreEqual(@"v", ParquetColumns.ReadMetadata(path)[@"k"]);
        }

        [TestMethod]
        public void TestTrainingSetFromExports()
        {
            // Run a: an Exploris-like run with collision energies; run b: an instrument Carafe
            // does not name and no energies. The same precursors appear in both.
            var a1 = Clean(@"PEPTIDEK", 2, 0.001, 5.0);
            var a2 = Clean(@"PEPTIDEK", 3, 0.005, 5.1);
            var a3 = Clean(@"SAMPLERK", 2, 0.01, 6.0);
            var a4 = Clean(@"ELVISLIVESK", 2, 0.0100001, 7.0);
            var a5 = Clean(@"DECQYPEPK", 2, 0.001, 7.5);
            a5.IsEntrapment = true;
            var a6 = Clean(@"PEPCK", 2, 0.001, 8.0);
            a6.ModPositions = new[] { 3 };
            a6.ModMasses = new[] { 12.3456 };
            a6.ModUnimodIds = new[] { -1 };
            a6.ModifiedSequence = @"PEPC[+12.3456]K";
            var a7 = Clean(@"LVNELTEFAK", 2, double.NaN, 8.5);
            var a8 = Clean(@"K", 2, 0.001, 9.0);
            var a9 = Clean(@"AMCPEPTK", 2, 0.001, 9.5);
            a9.ModPositions = new[] { 1, 2 };
            a9.ModMasses = new[] { 15.994915, 57.021464 };
            a9.ModUnimodIds = new[] { 35, 4 };
            a9.ModifiedSequence = @"AM(UniMod:35)C(UniMod:4)PEPTK";
            var b1 = Clean(@"PEPTIDEK", 2, 0.0005, 15.0);
            var b2 = Clean(@"SAMPLERK", 2, 0.01, 16.0);
            b2.Score = 2;
            var b3 = Clean(@"SAMPLERK", 3, 0.001, 16.5);
            for (int slot = 0; slot < b3.SlotCount; slot++)
                b3.CorrPolish[slot] = 0.1f;
            var footerA = Footer(@"a");
            footerA[@"osprey.collision_energies"] = @"{""30"":100,""27"":100}";
            var footerB = Footer(@"a");
            footerB[@"osprey.rt_max"] = @"20";
            footerB[@"osprey.instrument_model"] = @"Orbitrap Ascend";
            footerB.Remove(@"osprey.collision_energies");
            var exportA = OspreyTrainingExport.Read(WriteExport(@"a", Number(a1, a2, a3, a4, a5, a6, a7, a8, a9), footerA, 4));
            var exportB = OspreyTrainingExport.Read(WriteExport(@"b", Number(b1, b2, b3), footerB, 4));
            var exports = new[] { exportA, exportB };

            var trainingSet = OspreyTrainingSet.Build(exports, new OspreyTrainingSetOptions());
            var stats = trainingSet.Stats;
            Assert.AreEqual(12, stats.Records);
            // q is inclusive (a3, b2 at exactly 0.01 stay); just above it or NaN is dropped.
            Assert.AreEqual(2, stats.AboveQ);
            Assert.AreEqual(1, stats.Entrapment);
            Assert.AreEqual(1, stats.Unmapped);
            // PEPTIDEK 2+ and SAMPLERK 2+ are in both runs: the better q, then the higher score, wins.
            Assert.AreEqual(2, stats.DuplicatePrecursors);
            Assert.AreEqual(6, stats.Ms2Candidates);
            // One spectrum per precursor, in file then entry order.
            CollectionAssert.AreEqual(new[] { @"PEPTIDEK/3", @"AMCPEPTK/2", @"PEPTIDEK/2", @"SAMPLERK/2" },
                trainingSet.Ms2.Select(e => e.Sequence + @"/" + e.Precursor.Charge).ToArray());
            Assert.AreEqual(4, stats.Ms2Rows);
            Assert.AreEqual(1, stats.Ms2Rejected[OspreyMaskingPolicy.REJECT_FEW_MATCHED]);
            Assert.AreEqual(1, stats.Ms2Rejected[OspreyMaskingPolicy.REJECT_FEW_VALID]);
            Assert.AreEqual(@"Oxidation@M;Carbamidomethyl@C", trainingSet.Ms2[1].Precursor.Peptide.ModsText);
            // Each run's NCE and instrument: a's own energy (a tie keeps the first) and Carafe's
            // name for its model; b has neither, so Carafe's defaults.
            Assert.AreEqual(30.0, trainingSet.Ms2[0].Nce);
            Assert.AreEqual(@"QEHF", trainingSet.Ms2[0].Instrument);
            Assert.AreEqual(OspreyTrainingSetOptions.DEFAULT_NCE, trainingSet.Ms2[2].Nce);
            Assert.AreEqual(OspreyTrainingSetOptions.DEFAULT_INSTRUMENT, trainingSet.Ms2[2].Instrument);
            // The kept rows carry the masking policy's intensities and mask.
            var policy = new OspreyMaskingPolicy(new OspreyMaskingSettings());
            var bestPeptidek = exportB.Records[0];
            CollectionAssert.AreEqual(policy.Apply(bestPeptidek).Intensities, trainingSet.Ms2[2].Intensities);
            CollectionAssert.AreEqual(policy.Apply(bestPeptidek).Invalid, trainingSet.Ms2[2].Invalid);
            // Slots and rule counts cover the rejected spectra too: five 28-slot ladders and K's
            // empty one, with b1 and y1 at both charges below the floor in each ladder.
            Assert.AreEqual(140L, stats.Slots);
            Assert.AreEqual(70L, stats.MatchedSlots);
            Assert.AreEqual(20L, stats.MaskedBy[OspreyMaskingPolicy.RULE_ORDINAL]);
            Assert.AreEqual(14L, stats.MaskedBy[OspreyMaskingPolicy.RULE_CORRELATION]);

            // One RT row per peptide form, from its best precursor over both runs and charges
            // (even a spectrum the MS2 gates reject), divided by the longest run's rt_max.
            Assert.AreEqual(4, stats.RtRows);
            Assert.AreEqual(20.1, stats.RtMax, 1e-12);
            CollectionAssert.AreEqual(new[] { @"K", @"AMCPEPTK", @"PEPTIDEK", @"SAMPLERK" },
                trainingSet.Rt.Select(e => e.Peptide.Sequence).ToArray());
            var expectedRt = new[] { 9.0, 9.5, 15.0, 16.5 };
            for (int i = 0; i < expectedRt.Length; i++)
                Assert.AreEqual(expectedRt[i] / 20.1, trainingSet.Rt[i].RtNorm, 1e-12);
            StringAssert.StartsWith(stats.ToString(), @"12 ");

            // Options: entrapment included, a looser q, no masking (every ion trains, the same
            // spectra are kept), a padding on rt_max and an instrument for every row.
            var options = new OspreyTrainingSetOptions
            {
                IncludeEntrapment = true,
                MaxRunQ = 0.02,
                UseMasking = false,
                RtMaxPadding = 0,
                Instrument = @"Lumos",
            };
            var loose = OspreyTrainingSet.Build(exports, options);
            Assert.AreEqual(0, loose.Stats.Entrapment);
            Assert.AreEqual(1, loose.Stats.AboveQ);
            Assert.AreEqual(20.0, loose.Stats.RtMax);
            Assert.IsTrue(loose.Ms2.All(e => e.Invalid.All(v => v == 0) && e.Instrument == @"Lumos"));
            Assert.AreEqual(6, loose.Ms2.Count);

            // Carafe's names for instrument models, trimmed and case-insensitive.
            Assert.AreEqual(@"QEHF", OspreyTrainingSet.GetCarafeInstrument(@"  q exactive hf "));
            Assert.AreEqual(@"QE+", OspreyTrainingSet.GetCarafeInstrument(@"Exactive Plus"));
            // CarafeSharp's name for a Stellar, which is peptdeep's Lumos family.
            Assert.AreEqual(@"Stellar", OspreyTrainingSet.GetCarafeInstrument(@"Stellar"));
            Assert.IsNull(OspreyTrainingSet.GetCarafeInstrument(null));

            TestTrainingTables(trainingSet);
            TestTrainingExportFooters(exports);
        }

        [TestMethod]
        public void TestModificationMapperEdges()
        {
            // A position outside the sequence, and a residue alphabase has no modification site for.
            Assert.IsFalse(OspreyModificationMapper.TryMap(@"PEPK", @"PEPK", new[] { 4 }, new[] { 15.994915 }, new[] { 35 },
                out var peptide, out string reason));
            Assert.IsNull(peptide);
            StringAssert.Contains(reason, @"4");
            Assert.IsFalse(OspreyModificationMapper.TryMap(@"PEPK", @"PEPK", new[] { -1 }, new[] { 15.994915 }, new[] { 35 },
                out peptide, out _));
            Assert.IsFalse(OspreyModificationMapper.TryMap(@"PEPBK", @"PEPB[+15.9949]K", new[] { 3 }, new[] { 15.994915 }, new[] { -1 },
                out peptide, out reason));
            StringAssert.Contains(reason, @"15.9949");
            // A leading modification after DIA-NN's underscore and an 'n' is N-terminal, and not
            // only for acetyl: Carbamyl maps to its Any N-term entry at site 0.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"PEPTIDEK", @"_n[+43.0058]PEPTIDEK", new[] { 0 }, new[] { 43.005814 }, new[] { 5 },
                out peptide, out reason), reason);
            Assert.AreEqual(@"Carbamyl@Any N-term", peptide.ModsText);
            Assert.AreEqual(@"0", peptide.ModSitesText);
            // Without the leading bracket the same mass is a modification of the first residue,
            // which P has none of.
            Assert.IsFalse(OspreyModificationMapper.TryMap(@"PEPTIDEK", @"P[+43.0058]EPTIDEK", new[] { 0 }, new[] { 43.005814 }, new[] { -1 },
                out peptide, out _));
            // Only the first modification at position 0 can be the N-terminal acetyl.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"MPEPTIDEK", @"(UniMod:1)M(UniMod:35)PEPTIDEK", new[] { 0, 0 },
                new[] { 42.010565, 15.994915 }, new[] { 1, 35 }, out peptide, out reason), reason);
            Assert.AreEqual(OspreyModificationMapper.PROTEIN_N_TERM_ACETYL + @";Oxidation@M", peptide.ModsText);
            Assert.AreEqual(@"0;1", peptide.ModSitesText);
            // A missing UniMod id list is read as unknown ids, and no modification at all is the bare peptide.
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"PEPCK", @"PEPC[+57.0215]K", new[] { 3 }, new[] { 57.021464 },
                Array.Empty<int>(), out peptide, out reason), reason);
            Assert.AreEqual(@"Carbamidomethyl@C", peptide.ModsText);
            Assert.IsTrue(OspreyModificationMapper.TryMap(@"PEPCK", null, Array.Empty<int>(), Array.Empty<double>(),
                Array.Empty<int>(), out peptide, out reason), reason);
            Assert.AreEqual(string.Empty, peptide.ModsText);
        }

        /// <summary>The training tables written from a training set read back as the same rows.</summary>
        private void TestTrainingTables(OspreyTrainingSet trainingSet)
        {
            string tables = Path.Combine(_folder, @"tables");
            CarafeTrainingDirectory.Write(tables, trainingSet.Rt, trainingSet.Ms2);
            var ms2 = CarafeTrainingDirectory.ReadMs2(tables, 30, @"Lumos");
            Assert.AreEqual(trainingSet.Ms2.Count, ms2.Count);
            for (int i = 0; i < ms2.Count; i++)
            {
                var expected = trainingSet.Ms2[i];
                Assert.AreEqual(expected.Sequence, ms2[i].Sequence);
                Assert.AreEqual(expected.Precursor.Charge, ms2[i].Precursor.Charge);
                Assert.AreEqual(expected.Precursor.Peptide.ModsText, ms2[i].Precursor.Peptide.ModsText);
                Assert.AreEqual(expected.Precursor.Peptide.ModSitesText, ms2[i].Precursor.Peptide.ModSitesText);
                CollectionAssert.AreEqual(expected.Intensities, ms2[i].Intensities);
                CollectionAssert.AreEqual(expected.Invalid, ms2[i].Invalid);
                Assert.AreEqual(30.0, ms2[i].Nce);
                Assert.AreEqual(@"Lumos", ms2[i].Instrument);
            }
            var rt = CarafeTrainingDirectory.ReadRt(tables);
            CollectionAssert.AreEqual(trainingSet.Rt.Select(e => e.Peptide.ModsText).ToArray(), rt.Select(e => e.Peptide.ModsText).ToArray());
            CollectionAssert.AreEqual(trainingSet.Rt.Select(e => e.RtNorm).ToArray(), rt.Select(e => e.RtNorm).ToArray());

            // Without the validity table every ion is valid; a fragment table missing columns or
            // cells reads them as 0, as Carafe fills them.
            File.Delete(Path.Combine(tables, CarafeTrainingDirectory.VALID_FILE));
            ms2 = CarafeTrainingDirectory.ReadMs2(tables, 30, @"Lumos");
            Assert.IsTrue(ms2.All(e => e.Invalid.All(v => v == 0)));
            string intensityFile = Path.Combine(tables, CarafeTrainingDirectory.INTENSITY_FILE);
            int fragmentRows = File.ReadAllLines(intensityFile).Length - 1;
            File.WriteAllLines(intensityFile, new[] { @"y_z1" + "\t" + @"b_z1" }.Concat(Enumerable.Range(0, fragmentRows).Select(r => "0.5\t")));
            ms2 = CarafeTrainingDirectory.ReadMs2(tables, 30, @"Lumos");
            CollectionAssert.AreEqual(new[] { 0.0, 0.0, 0.5, 0.0 }, ms2[0].Intensities.Take(4).ToArray());

            // A PSM whose fragment rows do not match its length, a missing column, an empty file.
            string psmFile = Path.Combine(tables, CarafeTrainingDirectory.PSM_FILE);
            var psmLines = File.ReadAllLines(psmFile);
            var cells = psmLines[1].Split('\t');
            cells[6] = (int.Parse(cells[6], CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture);
            File.WriteAllLines(psmFile, new[] { psmLines[0], string.Join("\t", cells) });
            Assert.ThrowsException<InvalidDataException>(() => CarafeTrainingDirectory.ReadMs2(tables, 30, @"Lumos"));
            File.WriteAllLines(psmFile, new[] { psmLines[0].Replace(@"mod_sites", @"sites"), psmLines[1] });
            Assert.ThrowsException<InvalidDataException>(() => CarafeTrainingDirectory.ReadMs2(tables, 30, @"Lumos"));
            File.WriteAllText(Path.Combine(tables, CarafeTrainingDirectory.RT_FILE), string.Empty);
            Assert.ThrowsException<InvalidDataException>(() => CarafeTrainingDirectory.ReadRt(tables));
        }

        /// <summary>The exports' own footers decide whether the runs come from one Osprey search.</summary>
        private void TestTrainingExportFooters(IReadOnlyList<OspreyTrainingExport> exports)
        {
            var selection = TrainingExportLocator.Find(_folder, null);
            CollectionAssert.AreEqual(exports.Select(e => e.Path).ToArray(), selection.Exports.ToArray());
            Assert.AreEqual(0, selection.Warnings.Count);
            // Every -ms run missing is named, in order.
            var missing = Assert.ThrowsException<FileNotFoundException>(() => TrainingExportLocator.Find(_folder, new[] { @"z2.mzML", @"z1.mzML" }));
            StringAssert.Contains(missing.Message, @"z1, z2");
            // An export of another search in the same folder is an error.
            WriteExport(@"other", Number(Clean(@"PEPTIDEK", 2, 0.001, 5)), Footer(@"other"), 1);
            Assert.ThrowsException<InvalidDataException>(() => TrainingExportLocator.Find(_folder, null));
        }

        private static OspreyTrainingRecord Clean(string sequence, int charge, double runQ, double apexRt)
        {
            var record = CleanRecord(sequence, charge);
            record.RunPrecursorQ = runQ;
            record.ApexRt = apexRt;
            record.Score = 1;
            return record;
        }

        /// <summary>The records numbered 1, 2, ... as entries, each with its own protein, precursor m/z, peak bounds and scan count.</summary>
        private static OspreyTrainingRecord[] Number(params OspreyTrainingRecord[] records)
        {
            for (int i = 0; i < records.Length; i++)
            {
                records[i].EntryId = (uint)(i + 1);
                records[i].ProteinIds = @"P" + (i + 1).ToString(CultureInfo.InvariantCulture);
                records[i].PrecursorMz = 400 + i;
                records[i].StartRt = records[i].ApexRt - 0.1;
                records[i].EndRt = records[i].ApexRt + 0.1;
                records[i].PeakScanCount = 7 + i;
            }
            return records;
        }

        private static Dictionary<string, string> Footer(string search)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { FORMAT_VERSION_KEY, OspreyTrainingExport.FORMAT_VERSION },
                { OspreyTrainingExport.SEARCH_HASH_KEY, @"search_" + search },
                { OspreyTrainingExport.LIBRARY_HASH_KEY, @"library" },
                { @"osprey.rt_max", @"10" },
                { @"osprey.instrument_model", @"Q Exactive HF" },
                { @"osprey.isolation_mz_min", @"400.5" },
                { @"osprey.isolation_mz_max", @"900.5" },
                { @"osprey.ms2_scan_window", @"200.25,1800" },
                { @"osprey.collision_energies", @"{""30"":1000,""27"":10}" },
            };
        }

        /// <summary>Writes the records as run <paramref name="stem"/>'s export, each record's file name the stem.</summary>
        private string WriteExport(string stem, IReadOnlyList<OspreyTrainingRecord> records, IReadOnlyDictionary<string, string> footer,
            int rowsPerGroup)
        {
            foreach (var record in records)
                record.FileName = stem;
            string path = Path.Combine(_folder, stem + OspreyTrainingExport.FILE_SUFFIX);
            OspreyTestRecords.WriteExport(path, records, footer, rowsPerGroup);
            return path;
        }

        /// <summary>Every property read back equal, arrays element by element (NaN equal to NaN).</summary>
        private static void AssertSameRecord(OspreyTrainingRecord expected, OspreyTrainingRecord actual)
        {
            foreach (var property in typeof(OspreyTrainingRecord).GetProperties())
            {
                object expectedValue = property.GetValue(expected);
                object actualValue = property.GetValue(actual);
                if (expectedValue is Array array)
                    CollectionAssert.AreEqual(array, (Array)actualValue, property.Name);
                else
                    Assert.AreEqual(expectedValue, actualValue, property.Name);
            }
        }

        /// <summary>An export of one clean record with footer <paramref name="key"/> set to <paramref name="value"/> (null removes it).</summary>
        private void AssertFooter(string key, string value, Action<OspreyTrainingExport> check)
        {
            var footer = Footer(@"a");
            if (value == null)
                footer.Remove(key);
            else
                footer[key] = value;
            check(OspreyTrainingExport.Read(WriteExport(@"footer", Number(CleanRecord(@"PEPTIDEK", 2)), footer, 1)));
        }

        private static void AssertInvalid(string path, string expectedText)
        {
            var e = Assert.ThrowsException<InvalidDataException>(() => OspreyTrainingExport.Read(path));
            if (expectedText != null)
                StringAssert.Contains(e.Message, expectedText);
        }
    }
}
