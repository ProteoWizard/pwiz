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
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Compares CarafeSharp's library prediction with Carafe's own output folders, each named
    /// by an environment variable (the tests are inconclusive when none is set):
    /// <list type="bullet">
    /// <item><c>CARAFESHARP_CARAFE_REFERENCE</c>: a library Carafe predicted with the generic
    /// pretrained models (the Stellar <c>osprey_initial_library</c>).</item>
    /// <item><c>CARAFESHARP_CARAFE_FINETUNED</c>: a library Carafe predicted after fine-tuning
    /// (<c>osprey_new_library</c>).</item>
    /// <item><c>CARAFESHARP_LIBRARY_REFERENCES</c>: a folder of small Carafe runs, one per
    /// subfolder, each with its parameter.txt, TSV and Skyline .blib.</item>
    /// </list>
    /// The Stellar folders were written by Carafe 2.2.0 of June 2026, before Carafe stopped
    /// clipping the initiator methionine of NoCut records (maccoss/carafe#11): their peptide
    /// lists hold M-clipped copies of M-initial records, which the port of origin/main does
    /// not make. Those are counted apart as explained.
    /// </summary>
    [TestClass]
    public class LibraryParityTest
    {
        public const string REFERENCES_VARIABLE = @"CARAFESHARP_LIBRARY_REFERENCES";

        /// <summary>Every n-th FASTA record is predicted in the subset comparison.</summary>
        private const int SUBSET_STRIDE = 50;

        /// <summary>Every n-th precursor of each batch is assembled from Carafe's own predictions.</summary>
        private const int ASSEMBLY_STRIDE = 10;

        public TestContext TestContext { get; set; }

        /// <summary>Requirement 1: the peptidoforms and precursors, with their m/z.</summary>
        [TestMethod]
        public void TestPeptideFormsMatchCarafe()
        {
            foreach (var run in ReferenceRuns())
            {
                var fasta = ReadFastaSequences(run.FastaPath);
                var ours = EnumeratePrecursors(run.Settings);
                int oracleRows = 0, explained = 0, oracleOnly = 0, mzDiffers = 0, sameId = 0;
                double maxMzDiff = 0;
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (string file in run.PeptideFormFiles)
                {
                    var forms = ParquetColumns.Read(file, @"pepID", @"sequence", @"mz", @"charge", @"mods", @"mod_sites");
                    var ids = forms.Get<int>(@"pepID");
                    var sequences = forms.Get<string>(@"sequence");
                    var mzs = forms.Get<double>(@"mz");
                    var charges = forms.Get<int>(@"charge");
                    var mods = forms.Get<string>(@"mods");
                    var sites = forms.Get<string>(@"mod_sites");
                    for (int i = 0; i < forms.RowCount; i++)
                    {
                        oracleRows++;
                        string key = PrecursorKey(sequences[i], charges[i], mods[i], sites[i]);
                        seen.Add(key);
                        if (!ours.TryGetValue(key, out var mine))
                        {
                            if (IsClippedCopy(sequences[i], fasta, run.Settings))
                                explained++;
                            else
                                oracleOnly++;
                            continue;
                        }
                        if (mine.Mz != mzs[i])
                        {
                            mzDiffers++;
                            maxMzDiff = Math.Max(maxMzDiff, Math.Abs(mine.Mz - mzs[i]));
                        }
                        if (mine.PepId == ids[i])
                            sameId++;
                    }
                }
                int oursOnly = ours.Keys.Count(k => !seen.Contains(k));
                Log(@"{0}: {1} Carafe precursors, {2} CarafeSharp; Carafe only {3} (+{4} M-clipped copies), CarafeSharp only {5}; " +
                    @"m/z differs {6} (max {7:E2}); same pepID {8}",
                    run, oracleRows, ours.Count, oracleOnly, explained, oursOnly, mzDiffers, maxMzDiff, sameId);
                Assert.AreEqual(0, oracleOnly, run.Name);
                Assert.AreEqual(0, oursOnly, run.Name);
                Assert.AreEqual(0, mzDiffers, run.Name);
            }
        }

        /// <summary>Requirement 2: alphabase fragment m/z against the _ms2_mz_df float32 values.</summary>
        [TestMethod]
        public void TestFragmentMzMatchesCarafe()
        {
            foreach (var run in ReferenceRuns())
            {
                long values = 0, exact = 0, oneUlp = 0;
                int maxUlp = 0;
                foreach (int batch in run.Batches)
                {
                    var frame = ParquetColumns.Read(run.BatchFile(batch, @"_ms2_df.parquet"),
                        @"sequence", @"mods", @"mod_sites", @"charge", @"frag_start_idx");
                    var mzFrame = ParquetColumns.Read(run.BatchFile(batch, @"_ms2_mz_df.parquet"), @"b_z1", @"b_z2", @"y_z1", @"y_z2");
                    var columns = new[] { @"b_z1", @"b_z2", @"y_z1", @"y_z2" }.Select(c => mzFrame.Get<float>(c)).ToArray();
                    var sequences = frame.Get<string>(@"sequence");
                    var mods = frame.Get<string>(@"mods");
                    var sites = frame.Get<string>(@"mod_sites");
                    var charges = frame.Get<int>(@"charge");
                    var starts = frame.Get<long>(@"frag_start_idx");
                    for (int i = 0; i < frame.RowCount; i++)
                    {
                        var precursor = new PrecursorForm(PeptideForm.FromAlphabase(sequences[i], mods[i], sites[i]), charges[i]);
                        var mz = AlphabaseFragmentMz.ToFloat32(AlphabaseFragmentMz.Calculate(precursor));
                        for (int row = 0; row < sequences[i].Length - 1; row++)
                        {
                            for (int k = 0; k < AlphabaseFragmentMz.COLUMN_COUNT; k++)
                            {
                                int ulp = Math.Abs(BitConverter.SingleToInt32Bits(mz[row * AlphabaseFragmentMz.COLUMN_COUNT + k]) -
                                                   BitConverter.SingleToInt32Bits(columns[k][starts[i] + row]));
                                values++;
                                if (ulp == 0)
                                    exact++;
                                else if (ulp == 1)
                                    oneUlp++;
                                maxUlp = Math.Max(maxUlp, ulp);
                            }
                        }
                    }
                }
                Log(@"{0}: {1} fragment m/z values, {2} bit-identical, {3} one float32 ulp apart, max {4} ulp",
                    run, values, exact, oneUlp, maxUlp);
                Assert.IsTrue(values > 0, run.Name);
                Assert.IsTrue(maxUlp <= 1, run.Name + @" max ulp " + maxUlp);
            }
        }

        /// <summary>
        /// Requirement 3, assembly alone: a sample of precursors built from Carafe's own
        /// predictions must reproduce Carafe's TSV rows character for character.
        /// </summary>
        [TestMethod]
        public void TestLibraryAssemblyMatchesCarafe()
        {
            foreach (var run in ReferenceRuns())
            {
                var fasta = ReadFastaSequences(run.FastaPath);
                var settings = run.Settings;
                var outputs = LibraryOutputs.FromFormat(settings.LibraryFormat, settings.Fast);
                var builder = new LibrarySpectrumBuilder(settings, outputs,
                    LibraryDatabase.MapPeptidesToProteins(settings.Database, settings.Digest));
                var expected = new Dictionary<string, string>(StringComparer.Ordinal);
                int skippedClipped = 0, dropped = 0;
                foreach (int batch in run.Batches)
                    BuildFromCarafePredictions(run, batch, builder, fasta, expected, ref skippedClipped, ref dropped);
                var reference = CarafeLibraryTsv.Read(run.LibraryTsv, null, new HashSet<string>(expected.Keys, StringComparer.Ordinal));
                int same = 0, missing = 0;
                var mismatches = new List<string>();
                foreach (var pair in expected)
                {
                    if (!reference.Precursors.TryGetValue(pair.Key, out var precursor))
                    {
                        missing++;
                        continue;
                    }
                    if (string.Join("\n", precursor.Rows) + "\n" == pair.Value)
                        same++;
                    else if (mismatches.Count < 3)
                        mismatches.Add(pair.Key);
                }
                Log(@"{0}: {1} precursors assembled from Carafe's predictions, {2} identical to Carafe's TSV rows, {3} missing from it, " +
                    @"{4} dropped for too few fragments, {5} M-clipped copies skipped{6}",
                    run, expected.Count, same, missing, dropped, skippedClipped,
                    mismatches.Count > 0 ? @"; first differences: " + string.Join(@", ", mismatches) : string.Empty);
                Assert.IsTrue(expected.Count > 0, run.Name);
                Assert.AreEqual(expected.Count, same, run.Name);
            }
        }

        /// <summary>
        /// Requirements 3 and 4, end to end: predicts the library (for the Stellar folders, of
        /// every 50th FASTA record) and compares it with Carafe's, and with Carafe's .blib where
        /// the reference has one.
        /// </summary>
        [TestMethod]
        public void TestPredictedLibraryMatchesCarafe()
        {
            string scratch = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(), @"LibraryParity_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(scratch);
            try
            {
                foreach (var run in ReferenceRuns())
                {
                    string output = Path.Combine(scratch, run.Name);
                    Directory.CreateDirectory(output);
                    bool subset = run.LibraryBlib == null;
                    string fasta = subset ? WriteSubsetFasta(run.FastaPath, Path.Combine(output, @"subset.fasta")) : run.FastaPath;
                    var settings = run.CreateSettings(fasta, output);
                    settings.LibraryFormat = @"Skyline,DIA-NN";
                    var clock = Stopwatch.StartNew();
                    var generator = new LibraryGenerator(settings, new TestContextWriter(TestContext));
                    generator.Run();
                    Log(@"{0}: predicted {1} precursors in {2:F1} s", run, generator.SpectrumCount, clock.Elapsed.TotalSeconds);

                    var ours = CarafeLibraryTsv.Read(generator.TsvPath);
                    var sequences = new HashSet<string>(ours.Precursors.Values.Select(p => p.Sequence), StringComparer.Ordinal);
                    var fastaSequences = ReadFastaSequences(run.FastaPath);
                    var reference = CarafeLibraryTsv.Read(run.LibraryTsv, subset ? sequences.Contains : null);
                    var comparison = reference.Compare(ours, (a, b) => IsClipExplained(a, b, fastaSequences, run.Settings) ||
                                                                        (subset && IsSubsetExplained(a, b)));
                    Log(@"{0}: {1}", run, comparison);
                    Assert.AreEqual(0, comparison.ReferenceOnly, run.Name);
                    Assert.AreEqual(0, comparison.OursOnly, run.Name);
                    Assert.AreEqual(0, comparison.PrecursorMzDiffers, run.Name);
                    Assert.AreEqual(0, comparison.ProteinIdDiffers, run.Name);
                    Assert.AreEqual(0, comparison.DecoyDiffers, run.Name);
                    Assert.AreEqual(0, comparison.FragmentMzDiffers, run.Name);
                    Assert.IsTrue(comparison.IdenticalFraction > 0.97, run.Name);
                    Assert.IsTrue(comparison.MaxIntensityDiff < 0.002, run.Name);
                    Assert.IsTrue(comparison.MaxRetentionTimeDiff <= 0.011, run.Name);
                    if (run.LibraryBlib != null)
                        CompareBlibs(run, run.LibraryBlib, generator.BlibPath);
                }
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                Directory.Delete(scratch, true);
            }
        }

        private void BuildFromCarafePredictions(CarafeReferenceRun run, int batch, LibrarySpectrumBuilder builder, HashSet<string> fasta,
            Dictionary<string, string> expected, ref int skippedClipped, ref int dropped)
        {
            var settings = run.Settings;
            var frame = ParquetColumns.Read(run.BatchFile(batch, @"_ms2_df.parquet"),
                @"pepID", @"sequence", @"mods", @"mod_sites", @"charge", @"frag_start_idx");
            var predictionFrame = ParquetColumns.Read(run.BatchFile(batch, @"_ms2_pred.parquet"), @"b_z1", @"b_z2", @"y_z1", @"y_z2");
            var predicted = new[] { @"b_z1", @"b_z2", @"y_z1", @"y_z2" }.Select(c => predictionFrame.Get<float>(c)).ToArray();
            var rtFrame = ParquetColumns.Read(run.BatchFile(batch, @"_rt_pred.parquet"), @"pepID", @"rt_pred", @"irt_pred");
            // Carafe's pepID2rt map: the last row of a pepID wins.
            var retentionTimes = new Dictionary<int, double>();
            var rtIds = rtFrame.Get<int>(@"pepID");
            var rtPred = rtFrame.Get<double>(@"rt_pred");
            var irtPred = rtFrame.Get<double>(@"irt_pred");
            for (int i = 0; i < rtFrame.RowCount; i++)
                retentionTimes[rtIds[i]] = settings.RtMax > 0 ? settings.RtMax * rtPred[i] : irtPred[i];

            var ids = frame.Get<int>(@"pepID");
            var sequences = frame.Get<string>(@"sequence");
            var mods = frame.Get<string>(@"mods");
            var sites = frame.Get<string>(@"mod_sites");
            var charges = frame.Get<int>(@"charge");
            var starts = frame.Get<long>(@"frag_start_idx");
            bool noCut = EnzymeTable.IsNoCut(settings.Digest.Enzyme);
            for (int i = 0; i < frame.RowCount; i += ASSEMBLY_STRIDE)
            {
                // An M-clipped copy, or a record Carafe also mapped to the M-initial record it
                // is the clipped copy of.
                if (noCut && (!fasta.Contains(sequences[i]) || fasta.Contains(@"M" + sequences[i])))
                {
                    skippedClipped++;
                    continue;
                }
                var isoform = ToIsoform(sequences[i], mods[i], sites[i]);
                var precursor = new PrecursorForm(isoform.ToAlphabase(), charges[i]);
                int rows = sequences[i].Length - 1;
                var intensities = new float[rows * 4];
                for (int row = 0; row < rows; row++)
                {
                    for (int k = 0; k < 4; k++)
                        intensities[row * 4 + k] = predicted[k][starts[i] + row];
                }
                var spectrum = builder.Build(isoform, precursor, intensities, 4, retentionTimes[ids[i]]);
                if (spectrum == null)
                {
                    dropped++;
                    continue;
                }
                expected[CarafeLibraryTsv.Key(spectrum.ModifiedPeptide, spectrum.Charge.ToString(CultureInfo.InvariantCulture), spectrum.PrecursorMz)] =
                    CarafeLibraryTsvWriter.FormatRows(spectrum);
            }
        }

        private void CompareBlibs(CarafeReferenceRun run, string carafeBlib, string ourBlib)
        {
            var carafe = ReadBlib(carafeBlib);
            var ours = ReadBlib(ourBlib);
            int mzDiffers = 0, modsDiffer = 0, missing = 0;
            double maxRtDiff = 0;
            foreach (var pair in carafe)
            {
                if (!ours.TryGetValue(pair.Key, out var mine))
                {
                    missing++;
                    continue;
                }
                if (pair.Value.Mz != mine.Mz)
                    mzDiffers++;
                if (pair.Value.Modifications != mine.Modifications)
                    modsDiffer++;
                maxRtDiff = Math.Max(maxRtDiff, Math.Abs(pair.Value.RetentionTime - mine.RetentionTime));
            }
            Log(@"{0}: Carafe .blib {1} spectra, CarafeSharp {2}; missing {3}; precursorMZ differs {4}; Modifications differ {5}; " +
                @"max |retentionTime diff| {6:E2}", run, carafe.Count, ours.Count, missing, mzDiffers, modsDiffer, maxRtDiff);
            Assert.AreEqual(carafe.Count, ours.Count, run.Name);
            Assert.AreEqual(0, missing + mzDiffers + modsDiffer, run.Name);
            Assert.IsTrue(maxRtDiff < 1e-3, run.Name);
        }

        /// <summary>RefSpectra by peptideModSeq and charge: m/z, RT and the Modifications rows as text.</summary>
        private static Dictionary<string, (double Mz, double RetentionTime, string Modifications)> ReadBlib(string path)
        {
            var modifications = new Dictionary<long, StringBuilder>();
            var spectra = new Dictionary<string, (double, double, string)>(StringComparer.Ordinal);
            using (var connection = new SQLiteConnection(@"Data Source=" + path + @";Read Only=True;"))
            {
                connection.Open();
                using (var command = new SQLiteCommand(@"SELECT RefSpectraID, position, mass FROM Modifications ORDER BY id", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        if (!modifications.TryGetValue(reader.GetInt64(0), out var text))
                            modifications.Add(reader.GetInt64(0), text = new StringBuilder());
                        text.Append(reader.GetInt64(1)).Append('@').Append(reader.GetDouble(2).ToString(@"R", CultureInfo.InvariantCulture)).Append(';');
                    }
                }
                using (var command = new SQLiteCommand(@"SELECT id, peptideModSeq, precursorCharge, precursorMZ, retentionTime FROM RefSpectra", connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long id = reader.GetInt64(0);
                        spectra[reader.GetString(1) + @"/" + reader.GetInt64(2)] = (reader.GetDouble(3), reader.GetDouble(4),
                            modifications.TryGetValue(id, out var text) ? text.ToString() : string.Empty);
                    }
                }
            }
            return spectra;
        }

        /// <summary>Our precursors keyed as the peptide_forms rows are, with their m/z and pepID.</summary>
        private static Dictionary<string, (double Mz, int PepId)> EnumeratePrecursors(LibrarySettings settings)
        {
            var digester = new Digester(settings.Digest);
            var peptides = LibraryDatabase.DigestPeptides(settings.Database, digester);
            var forms = LibraryPeptideForms.Enumerate(peptides, new PeptideIsoformGenerator(settings.Modifications, digester.ProteinNTermPeptides));
            var precursors = new Dictionary<string, (double, int)>(StringComparer.Ordinal);
            int pepId = 0;
            foreach (var form in forms)
            {
                var charges = LibraryPeptideForms.GetCharges(form, settings.Charges, settings.MinPrecursorMz, settings.MaxPrecursorMz);
                if (charges.Count == 0)
                    continue;
                var alphabase = form.ToAlphabase();
                foreach (int charge in charges)
                    precursors.Add(PrecursorKey(form.Sequence, charge, alphabase.ModsText, alphabase.ModSitesText), (form.GetMz(charge), pepId));
                pepId++;
            }
            return precursors;
        }

        private static string PrecursorKey(string sequence, int charge, string mods, string sites)
        {
            return sequence + @"|" + charge + @"|" + mods + @"|" + sites;
        }

        /// <summary>A peptidoform from the alphabase mods and sites Carafe wrote, masses in that order.</summary>
        private static PeptideIsoform ToIsoform(string sequence, string mods, string sites)
        {
            var form = PeptideForm.FromAlphabase(sequence, mods, sites);
            var modifications = new List<ModificationSite>();
            for (int i = 0; i < form.ModNames.Count; i++)
            {
                var modification = CarafeModification.TopModifications.First(m => m.AlphabaseName == form.ModNames[i]);
                modifications.Add(new ModificationSite(form.ModSites[i], modification));
            }
            return new PeptideIsoform(sequence, modifications);
        }

        /// <summary>
        /// A peptide only the June 2026 Carafe made: the M-clipped copy of an M-initial NoCut
        /// record long enough to be clipped.
        /// </summary>
        private static bool IsClippedCopy(string sequence, HashSet<string> fasta, LibrarySettings settings)
        {
            return !fasta.Contains(sequence) && fasta.Contains(@"M" + sequence) && sequence.Length + 1 >= settings.Digest.MinLength + 1;
        }

        /// <summary>
        /// A ProteinID Carafe joined with the accessions of an M-initial record whose clipped
        /// copy is this peptide: ours plus those, in either order.
        /// </summary>
        private static bool IsClipExplained(CarafeLibraryTsv.Precursor reference, CarafeLibraryTsv.Precursor mine,
            HashSet<string> fasta, LibrarySettings settings)
        {
            if (!EnzymeTable.IsNoCut(settings.Digest.Enzyme) || !fasta.Contains(@"M" + reference.Sequence))
                return false;
            var theirs = reference.ProteinId.Split(';');
            var ours = mine.ProteinId == LibrarySpectrum.NO_PROTEIN ? Array.Empty<string>() : mine.ProteinId.Split(';');
            return ours.All(theirs.Contains) && theirs.Length > ours.Length;
        }

        /// <summary>
        /// A ProteinID Carafe joined from records a subset left out: ours is some of its
        /// accessions.
        /// </summary>
        private static bool IsSubsetExplained(CarafeLibraryTsv.Precursor reference, CarafeLibraryTsv.Precursor mine)
        {
            var theirs = reference.ProteinId.Split(';');
            var ours = mine.ProteinId.Split(';');
            return ours.All(theirs.Contains) && theirs.Length > ours.Length;
        }

        private static HashSet<string> ReadFastaSequences(string path)
        {
            return new HashSet<string>(FastaReader.ReadFile(path).Select(r => r.Sequence.ToUpperInvariant()), StringComparer.Ordinal);
        }

        private static string WriteSubsetFasta(string source, string path)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                int index = 0;
                foreach (var record in FastaReader.ReadFile(source))
                {
                    if (index++ % SUBSET_STRIDE == 0)
                        writer.Write(@">" + record.Header + "\n" + record.Sequence + "\n");
                }
            }
            return path;
        }

        private IEnumerable<CarafeReferenceRun> ReferenceRuns()
        {
            var runs = new List<CarafeReferenceRun>();
            foreach (string variable in new[] { CarafeParityTest.PRETRAINED_REFERENCE_VARIABLE, CarafeParityTest.FINETUNED_REFERENCE_VARIABLE })
            {
                string folder = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
                    runs.Add(CarafeReferenceRun.Open(folder));
            }
            string references = Environment.GetEnvironmentVariable(REFERENCES_VARIABLE);
            if (!string.IsNullOrEmpty(references) && Directory.Exists(references))
                runs.AddRange(Directory.GetDirectories(references).OrderBy(d => d, StringComparer.Ordinal).Select(CarafeReferenceRun.Open));
            runs.RemoveAll(r => r == null);
            if (runs.Count == 0)
            {
                Assert.Inconclusive(@"None of {0}, {1} or {2} names a Carafe library output folder.",
                    CarafeParityTest.PRETRAINED_REFERENCE_VARIABLE, CarafeParityTest.FINETUNED_REFERENCE_VARIABLE, REFERENCES_VARIABLE);
            }
            return runs;
        }

        private void Log(string format, params object[] args)
        {
            TestContext.WriteLine(format, args);
        }

        /// <summary>The generator's progress log, into the test output.</summary>
        private sealed class TestContextWriter : TextWriter
        {
            private readonly TestContext _context;

            public TestContextWriter(TestContext context)
            {
                _context = context;
            }

            public override Encoding Encoding
            {
                get { return Encoding.UTF8; }
            }

            public override void WriteLine(string value)
            {
                _context.WriteLine(@"{0}", value);
            }
        }
    }
}
