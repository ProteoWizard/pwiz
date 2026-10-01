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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The comparator of the golden regression, which <c>regression.ps1</c> runs on a run folder
    /// it made: the RT and MS2 models fine-tuned on a packaged Osprey training export, and the
    /// library predicted from a subset of the library FASTA's pair groups.
    /// <para>
    /// It reads the run folder from <c>CARAFESHARP_REGRESSION_RUN</c>, and is inconclusive
    /// without it, so the normal test passes skip it. The golden is
    /// <c>regression.data/&lt;dataset&gt;/golden.json</c>, found from
    /// <c>CARAFESHARP_REGRESSION_DATA</c> or above the test assembly.
    /// </para>
    /// <para>
    /// The tolerances calibrated across GPU repeats, CPU against GPU and Windows against Linux
    /// decide pass or fail, on every machine: the held-out metrics, the library's precursor, peak
    /// and DecoyPairs counts, the DecoyPairs table's pairing, and a sample of the library's spectra.
    /// The exact hashes (training tables, model weights, library content) are compared too, and
    /// reported, but never fail the test.
    /// </para>
    /// <para>
    /// With <c>CARAFESHARP_REGRESSION_CREATE</c> set to a folder, it writes a golden of the run
    /// there instead, after checking that the fine-tuned MS2 model beats the pretrained one and
    /// that the library's DecoyPairs table pairs every target whose decoy was written.
    /// </para>
    /// <para>
    /// A chained run (<c>regression.ps1 -Leg Chained</c>) has no golden: Osprey searched the
    /// committed Stellar subset with <c>--training-export</c>, and CarafeSharp trained on those
    /// exports and predicted a library. Its checks are that each tool did its part and that
    /// CarafeSharp read what Osprey wrote.
    /// </para>
    /// </summary>
    [TestClass]
    public class RegressionTest
    {
        public const string REGRESSION_CATEGORY = @"Regression";

        public const string RUN_VARIABLE = @"CARAFESHARP_REGRESSION_RUN";
        public const string CREATE_VARIABLE = @"CARAFESHARP_REGRESSION_CREATE";
        public const string DATA_VARIABLE = @"CARAFESHARP_REGRESSION_DATA";

        public const string GOLDEN_FILE = @"golden.json";
        public const string SAMPLE_FILE = @"library_sample.tsv.gz";
        public const string RUN_INFO_FILE = @"regression-run.json";
        public const string REPORT_FILE = @"regression-report.txt";
        public const string DATA_FOLDER = @"regression.data";

        // 3: the training-table hashes are over the tables' lines, whatever their line ending.
        private const string GOLDEN_FORMAT = @"carafesharp-regression-golden-3";
        /// <summary>The training export's key under a run's and a golden's <c>inputs</c>.</summary>
        private const string INPUT_EXPORT = @"export";
        private const string OUTPUT_FOLDER = @"out";

        /// <summary>The sample keeps the precursors whose key hash is 0 modulo this.</summary>
        private const int SAMPLE_MODULUS = 10;

        /// <summary>A golden sample larger than this is not committed.</summary>
        private const long MAX_SAMPLE_BYTES = 2L * 1024 * 1024;

        // 64-bit FNV-1a, as LibraryParityTest partitions by.
        private const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV_PRIME = 1099511628211UL;

        // Calibrated tolerances (2026-09-27 calibration, each at least 3x the largest spread seen
        // across GPU repeats, CPU vs GPU and Windows vs Linux; Stellar and Astral).
        private const double PRETRAINED_METRIC_TOLERANCE = 1e-5;
        private static readonly IReadOnlyDictionary<string, double> FINETUNED_METRIC_TOLERANCES = new Dictionary<string, double>
        {
            { @"cos", 1.5e-3 }, { @"pcc", 1.5e-3 }, { @"sa", 6e-3 }, { @"spc", 5e-3 }, { @"r2", 1e-4 }, { @"mae_normalized", 5e-4 },
        };
        private const double PEAK_COUNT_TOLERANCE = 0.01;
        // The written precursor set, and with it the DecoyPairs rows, may lose a few precursors
        // near the -lf_min_n_frag cutoff on another device (Astral GPU vs GPU: 1.7e-5).
        private const double PRECURSOR_COUNT_TOLERANCE = 1e-4;
        private const int PRECURSOR_COUNT_MIN_ALLOWED = 2;
        private const double COSINE_MEDIAN_MIN = 0.99925;
        private const double COSINE_P5_MIN = 0.991;
        private const double COSINE_P1_MIN = 0.975;
        private const double RT_MEDIAN_MAX = 0.03;
        private const double RT_P95_MAX = 0.10;
        private const double RT_P99_MAX = 0.16;

        /// <summary>A precursor may be in one library only when it has at most this many fragments (lf_min_n_frag + 1).</summary>
        private const int FEW_FRAGMENTS = 3;

        private const string LEG_CHAINED = @"chained";
        // A 1% run FDR keeps 118-159 precursors per run of the subset (2026-09-30). The floor is
        // well below that: how many Osprey identifies is its own regression's to gate, and this
        // leg's to see only that each export is a real one.
        private const int CHAINED_MIN_EXPORTED = 50;
        /// <summary>The target m/z of the one isolation window the Stellar subset keeps.</summary>
        private const double CHAINED_WINDOW_TARGET = 594.5201;
        /// <summary>The regression's -lf_min_n_frag.</summary>
        private const int CHAINED_MIN_PEAKS = 2;
        // The same model predicts the same spectrum to float32 rounding, which moves with the other
        // peptides in a prediction batch: 8.6e-7 at most over the subset's wider window, and no
        // difference at all over the same window (2026-09-30).
        private const double SAVED_MODEL_INTENSITY_TOLERANCE = 1e-5;
        /// <summary>The per-run second pass, which a run of a multi-run analysis selects on.</summary>
        private const string SECOND_PASS = @"2";

        /// <summary>The MS2 metrics a golden's fine-tuned model must improve on.</summary>
        private static readonly string[] MS2_METRICS = { @"cos", @"pcc", @"sa", @"spc" };

        /// <summary>regression-run.json values a golden keeps, as provenance.</summary>
        private static readonly string[] PROVENANCE_KEYS =
        {
            @"dataset", @"leg", @"torch", @"device_used", @"commit", @"branch", @"os", @"os_platform", @"processor",
            @"logical_processors", @"omp_num_threads", @"minutes", @"subset_rule", @"subset_groups", @"subset_records",
            @"subset_pairing_rows", @"export_note",
        };

        private static readonly string[] TRAINING_TABLES =
        {
            CarafeTrainingDirectory.INTENSITY_FILE, CarafeTrainingDirectory.VALID_FILE, CarafeTrainingDirectory.PSM_FILE,
            CarafeTrainingDirectory.RT_FILE,
        };

        private static readonly string[] MODEL_FILES = { ModelFiles.MS2_SAFETENSORS, ModelFiles.RT_SAFETENSORS };

        private readonly List<string> _report = new List<string>();
        private readonly List<string> _failures = new List<string>();

        public TestContext TestContext { get; set; }

        /// <summary>Compares the run with its golden, or makes the golden (see the class summary).</summary>
        [TestMethod, TestCategory(REGRESSION_CATEGORY)]
        public void TestRegressionGolden()
        {
            string runFolder = Environment.GetEnvironmentVariable(RUN_VARIABLE);
            if (string.IsNullOrEmpty(runFolder))
                Assert.Inconclusive(@"{0} is not set: regression.ps1 runs this test on a run folder.", RUN_VARIABLE);
            Assert.IsTrue(Directory.Exists(runFolder), @"{0} names a folder that does not exist: {1}", RUN_VARIABLE, runFolder);
            string createFolder = Environment.GetEnvironmentVariable(CREATE_VARIABLE);

            if (ReadLeg(runFolder) == LEG_CHAINED)
            {
                Assert.IsTrue(string.IsNullOrEmpty(createFolder), @"A chained run has no golden to make.");
                CheckChained(runFolder);
                WriteReport(runFolder);
                Assert.AreEqual(0, _failures.Count, @"The chained run failed its checks:{0}{1}",
                    Environment.NewLine, string.Join(Environment.NewLine, _failures));
                return;
            }

            var run = RunMeasurement.Measure(runFolder, TorchSharp.torch.get_num_threads());
            if (!string.IsNullOrEmpty(createFolder))
            {
                CreateGolden(run, createFolder);
                return;
            }

            string goldenFolder = GetGoldenFolder(run.Dataset);
            var golden = Golden.Read(goldenFolder);
            Compare(golden, run);
            WriteReport(runFolder);
            Assert.AreEqual(0, _failures.Count, @"The run is outside the calibrated tolerances of the golden {0}:{1}{2}", goldenFolder,
                Environment.NewLine, string.Join(Environment.NewLine, _failures));
        }

        private void CreateGolden(RunMeasurement run, string folder)
        {
            Assert.IsFalse(run.Dirty, @"A golden must come from a clean working tree; {0} records changes.", RUN_INFO_FILE);
            Assert.IsFalse(run.OtherExport, @"A golden must come from the packaged export; {0} records a run with -Export.", RUN_INFO_FILE);
            Assert.IsTrue(run.UseFineTuned, @"The fine-tune chose the pretrained MS2 model ({0} is false).", ModelFiles.METRICS_USE_FINETUNED);
            foreach (string metric in MS2_METRICS)
            {
                double pretrained = run.Metrics[MetricKey(ModelFiles.METRICS_MS2, ModelFiles.METRICS_PRETRAINED, metric)];
                double finetuned = run.Metrics[MetricKey(ModelFiles.METRICS_MS2, ModelFiles.METRICS_FINETUNED, metric)];
                Assert.IsTrue(finetuned > pretrained, @"The fine-tuned MS2 model does not beat the pretrained one on {0}: {1} vs {2}.",
                    metric, Format(finetuned), Format(pretrained));
            }
            var pairs = run.Pairs;
            Assert.IsTrue(pairs.Rows > 0, @"The library has no DecoyPairs rows.");
            Assert.AreEqual(0, pairs.MalformedPairs, @"DecoyPairs has pairs that are not one pair group's target and decoy of one charge.");
            Assert.AreEqual(0, pairs.UnpairedTargetsWithDecoy, @"DecoyPairs leaves out targets whose decoy the library has.");

            Directory.CreateDirectory(folder);
            string samplePath = Path.Combine(folder, SAMPLE_FILE);
            LibrarySample.Write(samplePath, run.Library.Sample);
            long sampleBytes = new FileInfo(samplePath).Length;
            Assert.IsTrue(sampleBytes <= MAX_SAMPLE_BYTES, @"The library sample is {0} bytes, more than {1}.", sampleBytes, MAX_SAMPLE_BYTES);
            Golden.Write(Path.Combine(folder, GOLDEN_FILE), run);
            TestContext.WriteLine(@"Golden written to {0}: {1} precursors, {2} peaks, {3} DecoyPairs rows, {4} sampled ({5} bytes).",
                folder, run.Library.Precursors, run.Library.Peaks, pairs.Rows, run.Library.Sample.Count, sampleBytes);
        }

        private void Compare(Golden golden, RunMeasurement run)
        {
            Report(@"Run {0} against the golden {1}", run.Folder, golden.Folder);
            Report(@"Golden: commit {0}, {1} on {2}, {3}; run: commit {4}, {5} on {6}, {7}", golden.Commit, golden.Device, golden.Processor,
                golden.OsPlatform, run.Commit, run.Device, run.Processor, run.OsPlatform);
            if (!golden.Arguments.SequenceEqual(run.Arguments))
                Report(@"INFO arguments differ from the golden's: {0}", DescribeArgumentChange(golden.Arguments, run.Arguments));

            Report(@"Gate: the calibrated tolerances decide pass or fail.");
            foreach (var pair in golden.Inputs)
            {
                run.Inputs.TryGetValue(pair.Key, out string value);
                // regression.ps1 -Export: the run trained on another export, typically one another
                // platform's Osprey wrote from the same .raw, whose scores differ in the last digit.
                if (run.OtherExport && pair.Key == INPUT_EXPORT)
                {
                    Report(@"INFO input {0}: golden {1}, run {2} (-Export: another export, so the tolerances decide)",
                        pair.Key, pair.Value, value ?? @"missing");
                    continue;
                }
                Check(value == pair.Value, @"input {0}: golden {1}, run {2}{3}", pair.Key, pair.Value, value ?? @"missing",
                    value == pair.Value ? string.Empty : @" (the golden is of other inputs; recreate it for these)");
            }
            Check(golden.UseFineTuned == run.UseFineTuned, @"{0}: golden {1}, run {2}", ModelFiles.METRICS_USE_FINETUNED,
                golden.UseFineTuned, run.UseFineTuned);
            CheckMetrics(golden, run);
            CheckLibrary(golden, run);
            CheckDecoyPairs(golden, run);
            CheckSample(golden, run);
            ReportExactComparisons(golden, run);
        }

        /// <summary>
        /// The chained leg's checks: Osprey wrote an export per run, each with the precursors a
        /// 1% run FDR keeps and the per-run second pass's q-values; CarafeSharp read them, trained
        /// on a subset of their rows, wrote both models and finite metrics, recorded every run's
        /// isolation window, and predicted a library whose spectra keep -lf_min_n_frag peaks. Then
        /// the model it saved predicted the same spectra again with -model, as a user reuses it,
        /// and was fine-tuned further on the same exports, starting from the models it holds.
        /// </summary>
        private void CheckChained(string folder)
        {
            int runs;
            string exportFolder;
            int savedExitCode;
            string savedLibraryFolder;
            int? furtherExitCode = null;
            string furtherFolder = null;
            using (var info = JsonDocument.Parse(File.ReadAllText(TestData.RequireFile(Path.Combine(folder, RUN_INFO_FILE)))))
            {
                var root = info.RootElement;
                Report(@"Chained run {0}: Osprey searched the Stellar subset with --training-export, CarafeSharp trained on its exports",
                    folder);
                Report(@"Run: commit {0}, {1} on {2}, {3}", root.GetProperty(@"commit").GetString(), root.GetProperty(@"device_used").GetString(),
                    root.GetProperty(@"processor").GetString(), root.GetProperty(@"os_platform").GetString());
                Check(root.GetProperty(@"osprey_exit_code").GetInt32() == 0, @"Osprey exit code {0}", root.GetProperty(@"osprey_exit_code").GetInt32());
                Check(root.GetProperty(@"exit_code").GetInt32() == 0, @"CarafeSharp exit code {0}", root.GetProperty(@"exit_code").GetInt32());
                runs = root.GetProperty(@"runs").GetInt32();
                string relative = root.GetProperty(@"export_folder").GetString() ?? string.Empty;
                exportFolder = Path.Combine(new[] { folder }.Concat(relative.Split('/')).ToArray());
                savedExitCode = root.GetProperty(@"saved_model_exit_code").GetInt32();
                savedLibraryFolder = Path.Combine(folder, root.GetProperty(@"saved_model_library").GetString() ?? string.Empty);
                // A run made before the leg fine-tuned the saved model further has neither.
                if (root.TryGetProperty(@"fine_tuned_further_exit_code", out var furtherExit))
                {
                    furtherExitCode = furtherExit.GetInt32();
                    furtherFolder = Path.Combine(folder, root.GetProperty(@"fine_tuned_further").GetString() ?? string.Empty);
                }
            }

            var exports = Directory.GetFiles(exportFolder, @"*" + OspreyTrainingExport.FILE_SUFFIX).OrderBy(p => p, StringComparer.Ordinal).ToList();
            Check(exports.Count == runs, @"training exports: {0} for {1} runs", exports.Count, runs);
            long exported = 0;
            foreach (string path in exports)
            {
                var export = OspreyTrainingExport.Read(path);
                exported += export.Records.Count;
                string name = Path.GetFileName(path);
                Check(export.Records.Count >= CHAINED_MIN_EXPORTED, @"{0}: {1} precursors (at least {2})", name, export.Records.Count, CHAINED_MIN_EXPORTED);
                Check(export.RunQPass == SECOND_PASS, @"{0}: run q from pass {1} (the per-run second pass is {2})", name,
                    export.RunQPass ?? @"unknown", SECOND_PASS);
            }

            string output = Path.Combine(folder, OUTPUT_FOLDER);
            foreach (string table in TRAINING_TABLES)
            {
                int rows = File.ReadLines(TestData.RequireFile(Path.Combine(output, table))).Count() - 1;
                Check(rows > 0, @"training table {0}: {1} rows", table, rows);
            }
            int psms = File.ReadLines(Path.Combine(output, CarafeTrainingDirectory.PSM_FILE)).Count() - 1;
            Check(psms <= exported, @"{0}: {1} PSMs from {2} exported precursors", CarafeTrainingDirectory.PSM_FILE, psms, exported);
            foreach (string model in MODEL_FILES)
                Check(File.Exists(Path.Combine(output, model)), @"model {0} written", model);
            foreach (var metric in ReadMetricValues(TestData.RequireFile(Path.Combine(output, ModelFiles.METRICS))))
                Check(double.IsFinite(metric.Value), @"metric {0}: {1}", metric.Key, Format(metric.Value));

            var meta = CarafeModelDirectory.Open(output).Runs;
            Check(meta.Count == runs, @"{0}: {1} runs", CarafeModelDirectory.META_FILE, meta.Count);
            foreach (var run in meta)
            {
                Check(run.PrecursorMzMin <= CHAINED_WINDOW_TARGET && CHAINED_WINDOW_TARGET <= run.PrecursorMzMax,
                    @"{0} {1}: precursor m/z {2}-{3} holds the subset's window at {4}", CarafeModelDirectory.META_FILE, run.MsFile,
                    Format(run.PrecursorMzMin), Format(run.PrecursorMzMax), Format(CHAINED_WINDOW_TARGET));
            }

            string blib = TestData.RequireFile(Path.Combine(output, BlibLibraryWriter.FILE_NAME));
            var library = LibraryContent.Read(blib);
            long fewestPeaks;
            using (var connection = OpenLibrary(blib))
            using (var command = new SQLiteCommand(@"SELECT MIN(numPeaks) FROM RefSpectra", connection))
            {
                object value = command.ExecuteScalar();
                fewestPeaks = value is long peaks ? peaks : 0;
            }
            SQLiteConnection.ClearAllPools();
            Check(library.Precursors > 0, @"library: {0} precursors, {1} peaks", library.Precursors, library.Peaks);
            Check(fewestPeaks >= CHAINED_MIN_PEAKS, @"library: fewest peaks in a spectrum {0} (at least {1})", fewestPeaks, CHAINED_MIN_PEAKS);

            // The saved model, and a library predicted from it with -model over a wider precursor
            // window: every precursor of the library training predicted, with the same spectrum.
            var saved = CarafeModelFile.Open(TestData.RequireFile(Path.Combine(output, CarafeModelFile.DEFAULT_FILE_NAME)));
            Check(saved.Runs.Count == runs && saved.RtUsed, @"{0}: {1} training runs; {2}", CarafeModelFile.DEFAULT_FILE_NAME,
                saved.Runs.Count, saved.Describe());
            Check(savedExitCode == 0, @"CarafeSharp -model exit code {0}", savedExitCode);
            var trainedSpectra = ReadSpectra(blib).ToDictionary(s => PrecursorKey(s.Sequence, s.Charge), StringComparer.Ordinal);
            var savedSpectra = ReadSpectra(TestData.RequireFile(Path.Combine(savedLibraryFolder, BlibLibraryWriter.FILE_NAME)))
                .ToDictionary(s => PrecursorKey(s.Sequence, s.Charge), StringComparer.Ordinal);
            SQLiteConnection.ClearAllPools();
            int missing = 0, differ = 0;
            double largest = 0;
            foreach (var pair in trainedSpectra)
            {
                if (!savedSpectra.TryGetValue(pair.Key, out var other))
                {
                    missing++;
                    continue;
                }
                var spectrum = pair.Value;
                if (spectrum.PrecursorMz != other.PrecursorMz || spectrum.RetentionTime != other.RetentionTime || !spectrum.Mz.SequenceEqual(other.Mz))
                {
                    differ++;
                    continue;
                }
                largest = Math.Max(largest, spectrum.Intensity.Zip(other.Intensity, (a, b) => (double)Math.Abs(a - b)).Max());
            }
            Check(savedSpectra.Count > trainedSpectra.Count && missing == 0 && differ == 0 && largest <= SAVED_MODEL_INTENSITY_TOLERANCE,
                @"library from the saved model: {0} precursors over the wider window; of the training run's {1}, {2} missing, {3} with " +
                @"another m/z, RT or fragments, largest intensity difference {4} (at most {5})",
                savedSpectra.Count, trainedSpectra.Count, missing, differ, Format(largest), Format(SAVED_MODEL_INTENSITY_TOLERANCE));

            if (furtherExitCode == null)
            {
                Report(@"INFO the run did not fine-tune the saved model further (made before the leg did)");
                return;
            }
            CheckFineTunedFurther(output, saved, furtherExitCode.Value, furtherFolder);
        }

        /// <summary>
        /// The saved model fine-tuned further on the exports it was trained on (-model with
        /// training). Its start models are the ones the first training chose, so their held-out
        /// scores, on the same rows, are the first training's for those models: RT's fine-tuned
        /// scores, and MS2's fine-tuned or pretrained ones as it chose. A new MS2 model that does
        /// not beat a saved fine-tuned one leaves that one in the new file; the new file names the
        /// saved one as its base.
        /// </summary>
        private void CheckFineTunedFurther(string output, CarafeModelFile saved, int exitCode, string furtherFolder)
        {
            Check(exitCode == 0, @"CarafeSharp -model with training exit code {0}", exitCode);
            var first = ReadMetricValues(TestData.RequireFile(Path.Combine(output, ModelFiles.METRICS)))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var further = ReadMetricValues(TestData.RequireFile(Path.Combine(furtherFolder, ModelFiles.METRICS)))
                .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            string ms2Start = saved.Ms2Used ? @"finetuned" : @"pretrained";
            foreach (var pair in further.Where(p => p.Key.Contains(@".pretrained.")))
            {
                string start = pair.Key.StartsWith(ModelFiles.METRICS_RT + @".", StringComparison.Ordinal)
                    ? pair.Key.Replace(@".pretrained.", @".finetuned.")
                    : pair.Key.Replace(@".pretrained.", @"." + ms2Start + @".");
                Check(first.TryGetValue(start, out double expected) && expected == pair.Value,
                    @"fine-tuned further: start {0} {1}, the first training's {2} {3}", pair.Key, Format(pair.Value), start,
                    first.TryGetValue(start, out double value) ? Format(value) : @"(missing)");
            }
            var model = CarafeModelFile.Open(TestData.RequireFile(Path.Combine(furtherFolder, CarafeModelFile.DEFAULT_FILE_NAME)));
            bool beat = CarafeModelDirectory.Open(furtherFolder, true).UseFineTunedMs2;
            Check(model.Ms2FromBase == (!beat && saved.Ms2Used) && model.BaseModels.Count == 1 && model.BaseModels[0].Sha256 == saved.FileSha256,
                @"fine-tuned further: {0}; base {1} (SHA-256 {2}, the saved model's {3})", model.Describe(),
                model.BaseModels.Count == 0 ? @"none" : model.BaseModels[0].File, model.BaseModels.Count == 0 ? @"-" : model.BaseModels[0].Sha256,
                saved.FileSha256);
        }

        private void CheckMetrics(Golden golden, RunMeasurement run)
        {
            foreach (var pair in golden.Metrics)
            {
                if (!run.Metrics.TryGetValue(pair.Key, out double value))
                {
                    Check(false, @"metric {0}: missing from the run", pair.Key);
                    continue;
                }
                double tolerance = GetMetricTolerance(pair.Key);
                double difference = value - pair.Value;
                Check(Math.Abs(difference) <= tolerance, @"metric {0}: golden {1}, run {2}, difference {3} (tolerance {4})", pair.Key,
                    Format(pair.Value), Format(value), difference.ToString(@"E2", CultureInfo.InvariantCulture),
                    tolerance.ToString(@"G3", CultureInfo.InvariantCulture));
            }
        }

        private void CheckLibrary(Golden golden, RunMeasurement run)
        {
            CheckCount(@"library precursors", golden.Library.Precursors, run.Library.Precursors);
            double peakChange = golden.Library.Peaks > 0 ? (double)(run.Library.Peaks - golden.Library.Peaks) / golden.Library.Peaks : 0;
            Check(Math.Abs(peakChange) <= PEAK_COUNT_TOLERANCE, @"library peaks: golden {0}, run {1}, change {2:P2} (tolerance {3:P0})",
                golden.Library.Peaks, run.Library.Peaks, peakChange, PEAK_COUNT_TOLERANCE);
        }

        /// <summary>
        /// The DecoyPairs rows, within the precursor-count tolerance of the golden's; and the table
        /// itself, as in a full library: each pair a target and the decoy of its pair group, of one
        /// charge, and every target paired whose decoy the library has.
        /// </summary>
        private void CheckDecoyPairs(Golden golden, RunMeasurement run)
        {
            var pairs = run.Pairs;
            CheckCount(@"DecoyPairs rows", golden.DecoyPairRows, pairs.Rows);
            Check(pairs.MalformedPairs == 0,
                @"DecoyPairs pairs: {0} of {1} are not a target and the decoy of its pair group with one charge ({2} entrapment pairs)",
                pairs.MalformedPairs, pairs.Pairs, pairs.EntrapmentPairs);
            Check(pairs.UnpairedTargetsWithDecoy == 0,
                @"DecoyPairs targets: {0} of {1} paired; {2} unpaired although their decoy was written, {3} unpaired because it was not",
                pairs.PairedTargets, pairs.Targets, pairs.UnpairedTargetsWithDecoy, pairs.UnpairedTargetsWithoutDecoy);
            if (pairs.AmbiguousPrecursors > 0 || pairs.UnlistedPrecursors > 0)
            {
                Report(@"INFO DecoyPairs: {0} precursors left out of the pairing check (an I/L twin: their I/L-normalized sequence has more than one place in the manifest), {1} not in the manifest",
                    pairs.AmbiguousPrecursors, pairs.UnlistedPrecursors);
            }
        }

        private void CheckSample(Golden golden, RunMeasurement run)
        {
            var comparison = SampleComparison.Compare(golden.Sample, run.Library.Sample);
            Check(comparison.MissingWithManyFragments == 0 && comparison.ExtraWithManyFragments == 0,
                @"sampled precursors: {0} of the golden's {1} missing from the run ({2} with more than {3} fragments), {4} only in the run ({5} with more)",
                comparison.Missing, golden.Sample.Count, comparison.MissingWithManyFragments, FEW_FRAGMENTS, comparison.Extra,
                comparison.ExtraWithManyFragments);
            Check(comparison.PrecursorMzDiffers == 0, @"sampled precursor m/z: {0} of {1} differ", comparison.PrecursorMzDiffers,
                comparison.Shared);
            if (comparison.Shared == 0)
            {
                Check(false, @"sampled precursors: none shared with the golden");
                return;
            }
            double cosineMedian = comparison.CosinePercentile(0.5);
            double cosineP5 = comparison.CosinePercentile(0.05);
            double cosineP1 = comparison.CosinePercentile(0.01);
            Check(cosineMedian >= COSINE_MEDIAN_MIN && cosineP5 >= COSINE_P5_MIN && cosineP1 >= COSINE_P1_MIN,
                @"sampled spectral cosine over {0} precursors: median {1:F6} (min {2}), p5 {3:F6} (min {4}), p1 {5:F6} (min {6}), lowest {7:F6}",
                comparison.Shared, cosineMedian, COSINE_MEDIAN_MIN, cosineP5, COSINE_P5_MIN, cosineP1, COSINE_P1_MIN,
                comparison.CosinePercentile(0));
            double rtMedian = comparison.RetentionTimePercentile(0.5);
            double rtP95 = comparison.RetentionTimePercentile(0.95);
            double rtP99 = comparison.RetentionTimePercentile(0.99);
            Check(rtMedian <= RT_MEDIAN_MAX && rtP95 <= RT_P95_MAX && rtP99 <= RT_P99_MAX,
                @"sampled |RT difference| (min): median {0:F4} (max {1}), p95 {2:F4} (max {3}), p99 {4:F4} (max {5}), largest {6:F4}",
                rtMedian, RT_MEDIAN_MAX, rtP95, RT_P95_MAX, rtP99, RT_P99_MAX, comparison.RetentionTimePercentile(1));
            Report(@"INFO sampled precursors with the golden's fragment m/z set: {0} of {1} ({2:P1})", comparison.SameFragmentSet,
                comparison.Shared, (double)comparison.SameFragmentSet / comparison.Shared);
        }

        /// <summary>
        /// The exact comparisons, reported and never gated: a CPU fine-tune repeats byte for byte on
        /// one machine, so on the golden's machine these are all SAME, and anywhere else they need not be.
        /// </summary>
        private void ReportExactComparisons(Golden golden, RunMeasurement run)
        {
            Report(@"Information: exact comparisons, not gated (SAME is expected only on the golden's machine and device).");
            ReportSameMaps(@"training table", golden.TrainingTables, run.TrainingTables);
            ReportSameMaps(@"model", golden.Models, run.Models);
            var differing = golden.Metrics.Where(p => !run.Metrics.TryGetValue(p.Key, out double v) || !v.Equals(p.Value)).Select(p => p.Key).ToList();
            Inform(differing.Count == 0, @"held-out metrics: {0} of {1} identical{2}", golden.Metrics.Count - differing.Count, golden.Metrics.Count,
                differing.Count == 0 ? string.Empty : @" (" + string.Join(@", ", differing) + @" differ)");
            Inform(golden.Library.ContentSha256 == run.Library.ContentSha256, @"library content SHA-256: golden {0}, run {1}",
                golden.Library.ContentSha256, run.Library.ContentSha256);
            Inform(golden.Library.Precursors == run.Library.Precursors && golden.Library.Peaks == run.Library.Peaks &&
                   golden.DecoyPairRows == run.Pairs.Rows,
                @"library precursors, peaks, DecoyPairs rows: golden {0}, {1}, {2}; run {3}, {4}, {5}", golden.Library.Precursors,
                golden.Library.Peaks, golden.DecoyPairRows, run.Library.Precursors, run.Library.Peaks, run.Pairs.Rows);
        }

        /// <summary>The tolerance of a metric key such as "ms2.finetuned.cos": tight for the pretrained model, calibrated for the fine-tuned one.</summary>
        private static double GetMetricTolerance(string key)
        {
            if (key.Contains(@"." + ModelFiles.METRICS_PRETRAINED + @"."))
                return PRETRAINED_METRIC_TOLERANCE;
            if (!FINETUNED_METRIC_TOLERANCES.TryGetValue(key.Substring(key.LastIndexOf('.') + 1), out double tolerance))
                Assert.Fail(@"No tolerance is calibrated for the metric {0}", key);
            return tolerance;
        }

        private static string GetGoldenFolder(string dataset)
        {
            string folder = Environment.GetEnvironmentVariable(DATA_VARIABLE);
            if (string.IsNullOrEmpty(folder))
            {
                for (string dir = AppContext.BaseDirectory; !string.IsNullOrEmpty(dir); dir = Path.GetDirectoryName(dir))
                {
                    string candidate = Path.Combine(dir, DATA_FOLDER);
                    if (Directory.Exists(candidate))
                    {
                        folder = Path.Combine(candidate, dataset.ToLowerInvariant());
                        break;
                    }
                }
            }
            Assert.IsFalse(string.IsNullOrEmpty(folder), @"No {0} folder above {1}; set {2}.", DATA_FOLDER, AppContext.BaseDirectory, DATA_VARIABLE);
            Assert.IsTrue(File.Exists(Path.Combine(folder, GOLDEN_FILE)), @"No golden in {0}: make one with regression.ps1 -CreateGolden.", folder);
            return folder;
        }

        /// <summary>A count within the precursor-count tolerance of the golden's.</summary>
        private void CheckCount(string what, long golden, long run)
        {
            long allowed = Math.Max(PRECURSOR_COUNT_MIN_ALLOWED, (long)(PRECURSOR_COUNT_TOLERANCE * golden));
            Check(Math.Abs(run - golden) <= allowed, @"{0}: golden {1}, run {2} (at most {3} may differ)", what, golden, run, allowed);
        }

        private void ReportSameMaps(string what, IReadOnlyDictionary<string, string> golden, IReadOnlyDictionary<string, string> run)
        {
            foreach (var pair in golden)
            {
                run.TryGetValue(pair.Key, out string value);
                Inform(value == pair.Value, @"{0} {1}: golden {2}, run {3}", what, pair.Key, pair.Value, value ?? @"missing");
            }
        }

        /// <summary>Reports one gated check; a failed one fails the test.</summary>
        private void Check(bool passed, string format, params object[] args)
        {
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            Report(@"{0} {1}", passed ? @"PASS" : @"FAIL", message);
            if (!passed)
                _failures.Add(message);
        }

        /// <summary>Reports one exact comparison, which never fails the test.</summary>
        private void Inform(bool same, string format, params object[] args)
        {
            Report(@"{0} {1}", same ? @"SAME" : @"DIFFERS", string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private void Report(string format, params object[] args)
        {
            _report.Add(string.Format(CultureInfo.InvariantCulture, format, args));
        }

        private void WriteReport(string runFolder)
        {
            File.WriteAllLines(Path.Combine(runFolder, REPORT_FILE), _report);
            foreach (string line in _report)
                TestContext.WriteLine(@"{0}", line);
        }

        /// <summary>The leg regression-run.json records; runs from before the chained leg are isolated.</summary>
        private static string ReadLeg(string runFolder)
        {
            using (var info = JsonDocument.Parse(File.ReadAllText(TestData.RequireFile(Path.Combine(runFolder, RUN_INFO_FILE)))))
                return info.RootElement.TryGetProperty(@"leg", out var leg) ? leg.GetString() : @"isolated";
        }

        /// <summary>Every number of model_evaluation_metrics.json, keyed by its path.</summary>
        private static IEnumerable<KeyValuePair<string, double>> ReadMetricValues(string path)
        {
            var values = new List<KeyValuePair<string, double>>();
            using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                AddNumbers(json.RootElement, string.Empty, values);
            return values;
        }

        private static void AddNumbers(JsonElement element, string prefix, List<KeyValuePair<string, double>> values)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                    AddNumbers(property.Value, prefix.Length == 0 ? property.Name : prefix + @"." + property.Name, values);
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                values.Add(new KeyValuePair<string, double>(prefix, element.GetDouble()));
            }
        }

        private static string DescribeArgumentChange(IReadOnlyList<string> golden, IReadOnlyList<string> run)
        {
            var removed = golden.Where((a, i) => i >= run.Count || run[i] != a).ToList();
            var added = run.Where((a, i) => i >= golden.Count || golden[i] != a).ToList();
            return @"golden has [" + string.Join(@" ", removed) + @"], run has [" + string.Join(@" ", added) + @"]";
        }

        private static string MetricKey(string model, string kind, string metric)
        {
            return model + @"." + kind + @"." + metric;
        }

        private static string Format(double value)
        {
            return value.ToString(@"R", CultureInfo.InvariantCulture);
        }

        private static string Sha256(string path)
        {
            TestData.RequireFile(path);
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
        }

        /// <summary>
        /// The SHA-256 of a text file's lines, whatever their line ending. CarafeSharp writes the
        /// training tables with the platform's line ending, so a Linux run's tables would otherwise
        /// never match a Windows golden's even when every value does.
        /// </summary>
        private static string TextSha256(string path)
        {
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var newline = new[] { (byte)'\n' };
                foreach (string line in File.ReadLines(TestData.RequireFile(path)))
                {
                    hash.AppendData(Encoding.UTF8.GetBytes(line));
                    hash.AppendData(newline);
                }
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
        }

        /// <summary>The key of a precursor in the sample: its Skyline modified sequence and charge.</summary>
        private static string PrecursorKey(string modifiedSequence, long charge)
        {
            return modifiedSequence + @"/" + charge.ToString(CultureInfo.InvariantCulture);
        }

        private static bool IsSampled(string key)
        {
            ulong hash = FNV_OFFSET_BASIS;
            foreach (byte b in Encoding.UTF8.GetBytes(key))
                hash = (hash ^ b) * FNV_PRIME;
            // FNV-1a mixes each byte into the high bits, so the sample reads those.
            return (hash >> 32) % SAMPLE_MODULUS == 0;
        }

        /// <summary>Every spectrum of a .blib, its peaks decoded.</summary>
        private static IEnumerable<LibrarySpectrumRow> ReadSpectra(string path)
        {
            using (var connection = OpenLibrary(path))
            {
                const string sql = @"SELECT r.peptideModSeq, r.precursorCharge, r.precursorMZ, r.retentionTime, r.numPeaks, p.peakMZ, p.peakIntensity " +
                                   @"FROM RefSpectra r JOIN RefSpectraPeaks p ON p.RefSpectraID = r.id";
                using (var command = new SQLiteCommand(sql, connection))
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int count = (int)reader.GetInt64(4);
                        byte[] mzBytes = BlibLibraryWriter.DecodeBlob((byte[])reader.GetValue(5), count * sizeof(double));
                        byte[] intensityBytes = BlibLibraryWriter.DecodeBlob((byte[])reader.GetValue(6), count * sizeof(float));
                        var mz = new double[count];
                        var intensity = new float[count];
                        for (int i = 0; i < count; i++)
                        {
                            mz[i] = BitConverter.ToDouble(mzBytes, i * sizeof(double));
                            intensity[i] = BitConverter.ToSingle(intensityBytes, i * sizeof(float));
                        }
                        yield return new LibrarySpectrumRow(reader.GetString(0), reader.GetInt64(1), reader.GetDouble(2), reader.GetDouble(3), mz, intensity);
                    }
                }
            }
        }

        private static SQLiteConnection OpenLibrary(string path)
        {
            var connection = new SQLiteConnection(@"Data Source=" + path + @";Read Only=True;");
            connection.Open();
            return connection;
        }

        /// <summary>What a run folder holds: its provenance, training tables, models, metrics and library.</summary>
        private sealed class RunMeasurement
        {
            public static RunMeasurement Measure(string folder, int torchThreads)
            {
                string infoPath = TestData.RequireFile(Path.Combine(folder, RUN_INFO_FILE));
                string output = Path.Combine(folder, OUTPUT_FOLDER);
                var run = new RunMeasurement { Folder = folder, TorchThreads = torchThreads };
                string pairingPath;
                using (var info = JsonDocument.Parse(File.ReadAllText(infoPath)))
                {
                    var root = info.RootElement;
                    run.InfoJson = root.Clone();
                    run.Dataset = root.GetProperty(@"dataset").GetString();
                    run.Device = root.GetProperty(@"device_used").GetString();
                    run.Commit = root.GetProperty(@"commit").GetString();
                    run.Dirty = root.GetProperty(@"dirty").GetBoolean();
                    run.OsPlatform = root.GetProperty(@"os_platform").GetString();
                    run.Processor = root.GetProperty(@"processor").GetString();
                    run.Arguments = root.GetProperty(@"arguments").EnumerateArray().Select(a => a.GetString()).ToList();
                    run.OtherExport = root.TryGetProperty(@"other_export", out var otherExport) && otherExport.GetBoolean();
                    Assert.AreEqual(0, root.GetProperty(@"exit_code").GetInt32(), @"CarafeSharp failed in {0}", folder);
                    var inputs = root.GetProperty(@"inputs");
                    foreach (var input in inputs.EnumerateObject())
                        run.Inputs[input.Name] = input.Value.GetProperty(@"sha256").GetString();
                    // The subset manifest is in the run folder, at a '/'-separated relative path.
                    string relative = inputs.GetProperty(@"subset_pairing").GetProperty(@"path").GetString() ?? string.Empty;
                    pairingPath = Path.Combine(new[] { folder }.Concat(relative.Split('/')).ToArray());
                }
                foreach (string table in TRAINING_TABLES)
                    run.TrainingTables[table] = TextSha256(Path.Combine(output, table));
                foreach (string model in MODEL_FILES)
                    run.Models[model] = Sha256(Path.Combine(output, model));
                run.ReadMetrics(TestData.RequireFile(Path.Combine(output, ModelFiles.METRICS)));
                string blib = TestData.RequireFile(Path.Combine(output, BlibLibraryWriter.FILE_NAME));
                run.Library = LibraryContent.Read(blib);
                run.Pairs = DecoyPairSummary.Read(blib, TestData.RequireFile(pairingPath));
                SQLiteConnection.ClearAllPools();
                return run;
            }

            public string Folder { get; private set; }
            public JsonElement InfoJson { get; private set; }
            public string Dataset { get; private set; }
            public string Device { get; private set; }
            public string Commit { get; private set; }
            public bool Dirty { get; private set; }
            /// <summary>The run trained on an export given with regression.ps1 -Export.</summary>
            public bool OtherExport { get; private set; }
            public string OsPlatform { get; private set; }
            public string Processor { get; private set; }
            public int TorchThreads { get; private set; }
            public IReadOnlyList<string> Arguments { get; private set; }
            public SortedDictionary<string, string> Inputs { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> TrainingTables { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> Models { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, double> Metrics { get; } = new SortedDictionary<string, double>(StringComparer.Ordinal);
            public bool UseFineTuned { get; private set; }
            public LibraryContent Library { get; private set; }
            public DecoyPairSummary Pairs { get; private set; }

            /// <summary>model_evaluation_metrics.json as "ms2.finetuned.cos" and the like, and the MS2 model choice.</summary>
            private void ReadMetrics(string path)
            {
                using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    foreach (string model in new[] { ModelFiles.METRICS_MS2, ModelFiles.METRICS_RT })
                    {
                        var section = json.RootElement.GetProperty(model);
                        foreach (string kind in new[] { ModelFiles.METRICS_PRETRAINED, ModelFiles.METRICS_FINETUNED })
                        {
                            foreach (var metric in section.GetProperty(kind).EnumerateObject())
                                Metrics[MetricKey(model, kind, metric.Name)] = metric.Value.GetDouble();
                        }
                    }
                    UseFineTuned = json.RootElement.GetProperty(ModelFiles.METRICS_MS2).GetProperty(ModelFiles.METRICS_USE_FINETUNED).GetBoolean();
                }
            }
        }

        private sealed class LibrarySpectrumRow
        {
            public LibrarySpectrumRow(string sequence, long charge, double precursorMz, double retentionTime, double[] mz, float[] intensity)
            {
                Sequence = sequence;
                Charge = charge;
                PrecursorMz = precursorMz;
                RetentionTime = retentionTime;
                Mz = mz;
                Intensity = intensity;
            }

            public string Sequence { get; }
            public long Charge { get; }
            public double PrecursorMz { get; }
            public double RetentionTime { get; }
            public double[] Mz { get; }
            public float[] Intensity { get; }
        }

        /// <summary>
        /// A .blib's content, read with SQLite: counts, a hash over every precursor, and the
        /// sampled precursors' spectra. The file's bytes are not compared, since createTime changes
        /// on every write.
        /// </summary>
        private sealed class LibraryContent
        {
            public static LibraryContent Read(string path)
            {
                var lines = new List<string>();
                var sample = new List<SampledSpectrum>();
                long peaks = 0;
                foreach (var spectrum in ReadSpectra(path))
                {
                    peaks += spectrum.Mz.Length;
                    lines.Add(CanonicalLine(spectrum.Sequence, spectrum.Charge, spectrum.PrecursorMz, spectrum.RetentionTime, spectrum.Mz, spectrum.Intensity));
                    string key = PrecursorKey(spectrum.Sequence, spectrum.Charge);
                    if (IsSampled(key))
                        sample.Add(new SampledSpectrum(key, spectrum.PrecursorMz, spectrum.RetentionTime, spectrum.Mz, spectrum.Intensity));
                }
                lines.Sort(StringComparer.Ordinal);
                using (var sha = SHA256.Create())
                {
                    var bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines) + "\n");
                    return new LibraryContent
                    {
                        Precursors = lines.Count,
                        Peaks = peaks,
                        ContentSha256 = Convert.ToHexString(sha.ComputeHash(bytes)).ToLowerInvariant(),
                        Sample = sample.OrderBy(s => s.Key, StringComparer.Ordinal).ToList(),
                    };
                }
            }

            public long Precursors { get; private set; }
            public long Peaks { get; private set; }
            public string ContentSha256 { get; private set; }
            public IReadOnlyList<SampledSpectrum> Sample { get; private set; }

            /// <summary>
            /// One precursor for the content hash: modified sequence, charge, exact precursor m/z,
            /// RT and the fragments in m/z order, each m/z and intensity rounded to 1e-6.
            /// </summary>
            private static string CanonicalLine(string sequence, long charge, double precursorMz, double retentionTime,
                double[] mz, float[] intensity)
            {
                var fragments = Enumerable.Range(0, mz.Length).OrderBy(i => mz[i]).ThenBy(i => intensity[i])
                    .Select(i => mz[i].ToString(@"F6", CultureInfo.InvariantCulture) + @":" +
                                 ((double)intensity[i]).ToString(@"F6", CultureInfo.InvariantCulture));
                return string.Join("\t", sequence, charge.ToString(CultureInfo.InvariantCulture),
                    precursorMz.ToString(@"R", CultureInfo.InvariantCulture), retentionTime.ToString(@"F6", CultureInfo.InvariantCulture),
                    string.Join(@";", fragments));
            }
        }

        /// <summary>
        /// A library's DecoyPairs table checked against the pairing manifest it was written from,
        /// independently of <see cref="DecoyPairPlanner"/>: each precursor is placed in its pair group
        /// by its I/L-normalized stripped sequence, and each target's partner is the precursor of
        /// the group's decoy (or, for an entrapment target, entrapment decoy) with the same charge
        /// and modifications.
        /// </summary>
        private sealed class DecoyPairSummary
        {
            private const string TARGET = @"target";
            private const string DECOY = @"decoy";
            private const string ENTRAPMENT_TARGET = @"p_target";
            private const string ENTRAPMENT_DECOY = @"p_decoy";

            public static DecoyPairSummary Read(string blibPath, string manifestPath)
            {
                var summary = new DecoyPairSummary();
                // The manifest: each group's members by type, and each sequence's group and type.
                var groups = new Dictionary<int, Dictionary<string, string>>();
                var membership = new Dictionary<string, (int PairIndex, string Type)>(StringComparer.Ordinal);
                var ambiguous = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in DecoyPairPlanner.ReadManifest(manifestPath))
                {
                    string sequence = PairingManifestReconciler.Normalize(entry.Sequence);
                    string type = (entry.PeptideType ?? string.Empty).Trim().ToLowerInvariant();
                    if (!groups.TryGetValue(entry.PairIndex, out var members))
                        groups.Add(entry.PairIndex, members = new Dictionary<string, string>(StringComparer.Ordinal));
                    members[type] = sequence;
                    if (membership.TryGetValue(sequence, out var existing) && (existing.PairIndex != entry.PairIndex || existing.Type != type))
                        ambiguous.Add(sequence);
                    else
                        membership[sequence] = (entry.PairIndex, type);
                }

                var precursors = ReadPrecursors(blibPath, out var pairRows);
                var written = new HashSet<string>(precursors.Values.Select(p => p.Key), StringComparer.Ordinal);
                summary.Rows = pairRows.Count;

                // Each pair: a target and the decoy of its group, of one charge.
                foreach (var pair in pairRows.GroupBy(r => r.PairId))
                {
                    summary.Pairs++;
                    var rows = pair.ToList();
                    bool entrapment = rows.Any(r => r.IsEntrapment);
                    if (entrapment)
                        summary.EntrapmentPairs++;
                    var target = rows.Where(r => !r.IsDecoy).Select(r => precursors.TryGetValue(r.RefSpectraId, out var p) ? p : null).ToList();
                    var decoy = rows.Where(r => r.IsDecoy).Select(r => precursors.TryGetValue(r.RefSpectraId, out var p) ? p : null).ToList();
                    if (rows.Count != 2 || target.Count != 1 || decoy.Count != 1 || target[0] == null || decoy[0] == null ||
                        rows.Any(r => r.IsEntrapment != entrapment) || target[0].Charge != decoy[0].Charge)
                    {
                        summary.MalformedPairs++;
                        continue;
                    }
                    if (ambiguous.Contains(target[0].Sequence) || ambiguous.Contains(decoy[0].Sequence))
                        continue;
                    bool knownTarget = membership.TryGetValue(target[0].Sequence, out var targetGroup);
                    bool knownDecoy = membership.TryGetValue(decoy[0].Sequence, out var decoyGroup);
                    if (!knownTarget || !knownDecoy || targetGroup.PairIndex != decoyGroup.PairIndex ||
                        targetGroup.Type != (entrapment ? ENTRAPMENT_TARGET : TARGET) || decoyGroup.Type != (entrapment ? ENTRAPMENT_DECOY : DECOY))
                    {
                        summary.MalformedPairs++;
                    }
                }

                // Every target is paired whose decoy was written.
                var pairedIds = new HashSet<long>(pairRows.Select(r => r.RefSpectraId));
                foreach (var precursor in precursors.Values)
                {
                    if (ambiguous.Contains(precursor.Sequence))
                    {
                        summary.AmbiguousPrecursors++;
                        continue;
                    }
                    if (!membership.TryGetValue(precursor.Sequence, out var group))
                    {
                        summary.UnlistedPrecursors++;
                        continue;
                    }
                    if (group.Type != TARGET && group.Type != ENTRAPMENT_TARGET)
                        continue;
                    summary.Targets++;
                    if (pairedIds.Contains(precursor.Id))
                    {
                        summary.PairedTargets++;
                        continue;
                    }
                    groups[group.PairIndex].TryGetValue(group.Type == TARGET ? DECOY : ENTRAPMENT_DECOY, out string partner);
                    if (partner != null && written.Contains(Precursor.MakeKey(partner, precursor.Charge, precursor.ModificationKey)))
                        summary.UnpairedTargetsWithDecoy++;
                    else
                        summary.UnpairedTargetsWithoutDecoy++;
                }
                return summary;
            }

            public int Rows { get; private set; }
            public int Pairs { get; private set; }
            public int EntrapmentPairs { get; private set; }
            public int MalformedPairs { get; private set; }
            public int Targets { get; private set; }
            public int PairedTargets { get; private set; }
            public int UnpairedTargetsWithDecoy { get; private set; }
            public int UnpairedTargetsWithoutDecoy { get; private set; }
            public int AmbiguousPrecursors { get; private set; }
            public int UnlistedPrecursors { get; private set; }

            /// <summary>The library's precursors by RefSpectra id, and its DecoyPairs rows (none when it has no such table).</summary>
            private static Dictionary<long, Precursor> ReadPrecursors(string path, out List<PairRow> pairRows)
            {
                var modifications = new Dictionary<long, List<double>>();
                var precursors = new Dictionary<long, Precursor>();
                pairRows = new List<PairRow>();
                using (var connection = OpenLibrary(path))
                {
                    using (var command = new SQLiteCommand(@"SELECT RefSpectraID, mass FROM Modifications", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            if (!modifications.TryGetValue(reader.GetInt64(0), out var masses))
                                modifications.Add(reader.GetInt64(0), masses = new List<double>());
                            masses.Add(reader.GetDouble(1));
                        }
                    }
                    using (var command = new SQLiteCommand(@"SELECT id, peptideSeq, precursorCharge FROM RefSpectra", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            long id = reader.GetInt64(0);
                            modifications.TryGetValue(id, out var masses);
                            precursors.Add(id, new Precursor(id, PairingManifestReconciler.Normalize(reader.GetString(1)), reader.GetInt64(2),
                                masses));
                        }
                    }
                    using (var command = new SQLiteCommand(@"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'DecoyPairs'", connection))
                    {
                        if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 0)
                            return precursors;
                    }
                    using (var command = new SQLiteCommand(@"SELECT RefSpectraID, IsDecoy, IsEntrapment, PairID FROM DecoyPairs", connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                            pairRows.Add(new PairRow(reader.GetInt64(0), reader.GetInt64(1) != 0, reader.GetInt64(2) != 0, reader.GetInt64(3)));
                    }
                }
                return precursors;
            }

            private sealed class Precursor
            {
                public Precursor(long id, string sequence, long charge, IEnumerable<double> modificationMasses)
                {
                    Id = id;
                    Sequence = sequence;
                    Charge = charge;
                    // Positions left out, as the planner's key leaves them out: a decoy's are elsewhere.
                    ModificationKey = string.Join(@";", (modificationMasses ?? Enumerable.Empty<double>()).OrderBy(m => m)
                        .Select(m => m.ToString(@"R", CultureInfo.InvariantCulture)));
                    Key = MakeKey(sequence, charge, ModificationKey);
                }

                public static string MakeKey(string sequence, long charge, string modificationKey)
                {
                    return sequence + @"|" + charge.ToString(CultureInfo.InvariantCulture) + @"|" + modificationKey;
                }

                public long Id { get; }
                public string Sequence { get; }
                public long Charge { get; }
                public string ModificationKey { get; }
                public string Key { get; }
            }

            private sealed class PairRow
            {
                public PairRow(long refSpectraId, bool isDecoy, bool isEntrapment, long pairId)
                {
                    RefSpectraId = refSpectraId;
                    IsDecoy = isDecoy;
                    IsEntrapment = isEntrapment;
                    PairId = pairId;
                }

                public long RefSpectraId { get; }
                public bool IsDecoy { get; }
                public bool IsEntrapment { get; }
                public long PairId { get; }
            }
        }

        /// <summary>A sampled precursor's spectrum, as the golden sample stores it.</summary>
        private sealed class SampledSpectrum
        {
            public SampledSpectrum(string key, double precursorMz, double retentionTime, double[] mz, float[] intensity)
            {
                Key = key;
                PrecursorMz = precursorMz;
                RetentionTime = retentionTime;
                Mz = mz;
                Intensity = intensity;
            }

            public string Key { get; }
            public double PrecursorMz { get; }
            public double RetentionTime { get; }
            public double[] Mz { get; }
            public float[] Intensity { get; }
        }

        /// <summary>The golden sample file: gzip'd TSV of key, precursor m/z, RT, and ';'-joined fragment m/z and intensities.</summary>
        private static class LibrarySample
        {
            private const string HEADER = "precursor\tprecursor_mz\tretention_time\tfragment_mz\tfragment_intensity";

            public static void Write(string path, IEnumerable<SampledSpectrum> spectra)
            {
                using (var file = File.Create(path))
                using (var gzip = new GZipStream(file, CompressionLevel.SmallestSize))
                using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
                {
                    writer.Write(HEADER + "\n");
                    foreach (var spectrum in spectra)
                    {
                        writer.Write(string.Join("\t", spectrum.Key, Format(spectrum.PrecursorMz), Format(spectrum.RetentionTime),
                            string.Join(@";", spectrum.Mz.Select(Format)),
                            string.Join(@";", spectrum.Intensity.Select(i => i.ToString(@"R", CultureInfo.InvariantCulture)))) + "\n");
                    }
                }
            }

            public static List<SampledSpectrum> Read(string path)
            {
                var spectra = new List<SampledSpectrum>();
                using (var file = File.OpenRead(TestData.RequireFile(path)))
                using (var gzip = new GZipStream(file, CompressionMode.Decompress))
                using (var reader = new StreamReader(gzip, new UTF8Encoding(false)))
                {
                    Assert.AreEqual(HEADER, reader.ReadLine(), path);
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var cells = line.Split('\t');
                        var mz = SplitList(cells[3]).Select(ParseDouble).ToArray();
                        var intensity = SplitList(cells[4]).Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                        spectra.Add(new SampledSpectrum(cells[0], ParseDouble(cells[1]), ParseDouble(cells[2]), mz, intensity));
                    }
                }
                return spectra;
            }

            private static IEnumerable<string> SplitList(string cell)
            {
                return cell.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            private static double ParseDouble(string text)
            {
                return double.Parse(text, CultureInfo.InvariantCulture);
            }
        }

        /// <summary>The golden sample against the run's: shared and one-sided precursors, cosines and RT differences.</summary>
        private sealed class SampleComparison
        {
            private readonly List<double> _cosines = new List<double>();
            private readonly List<double> _retentionTimeDifferences = new List<double>();

            public static SampleComparison Compare(IReadOnlyList<SampledSpectrum> golden, IReadOnlyList<SampledSpectrum> run)
            {
                var result = new SampleComparison();
                var runByKey = run.GroupBy(s => s.Key, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
                var goldenKeys = new HashSet<string>(golden.Select(s => s.Key), StringComparer.Ordinal);
                foreach (var expected in golden)
                {
                    if (!runByKey.TryGetValue(expected.Key, out var actual))
                    {
                        result.Missing++;
                        if (expected.Mz.Length > FEW_FRAGMENTS)
                            result.MissingWithManyFragments++;
                        continue;
                    }
                    result.Shared++;
                    if (!expected.PrecursorMz.Equals(actual.PrecursorMz))
                        result.PrecursorMzDiffers++;
                    result._cosines.Add(Cosine(expected, actual));
                    result._retentionTimeDifferences.Add(Math.Abs(expected.RetentionTime - actual.RetentionTime));
                    if (expected.Mz.OrderBy(m => m).SequenceEqual(actual.Mz.OrderBy(m => m)))
                        result.SameFragmentSet++;
                }
                foreach (var extra in run.Where(s => !goldenKeys.Contains(s.Key)))
                {
                    result.Extra++;
                    if (extra.Mz.Length > FEW_FRAGMENTS)
                        result.ExtraWithManyFragments++;
                }
                result._cosines.Sort();
                result._retentionTimeDifferences.Sort();
                return result;
            }

            public int Shared { get; private set; }
            public int Missing { get; private set; }
            public int MissingWithManyFragments { get; private set; }
            public int Extra { get; private set; }
            public int ExtraWithManyFragments { get; private set; }
            public int PrecursorMzDiffers { get; private set; }
            public int SameFragmentSet { get; private set; }

            public double CosinePercentile(double fraction)
            {
                return Percentile(_cosines, fraction);
            }

            public double RetentionTimePercentile(double fraction)
            {
                return Percentile(_retentionTimeDifferences, fraction);
            }

            /// <summary>The value at <paramref name="fraction"/> of the sorted list, by nearest rank.</summary>
            private static double Percentile(List<double> sorted, double fraction)
            {
                int index = (int)Math.Round(fraction * (sorted.Count - 1));
                return sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))];
            }

            /// <summary>
            /// The cosine of two spectra over the union of their fragments, matched by exact m/z.
            /// Fragments of one spectrum at the same m/z (a b and a y ion that coincide) are summed.
            /// </summary>
            private static double Cosine(SampledSpectrum a, SampledSpectrum b)
            {
                var x = ByMz(a);
                var y = ByMz(b);
                double dot = x.Sum(p => y.TryGetValue(p.Key, out double other) ? p.Value * other : 0);
                double normX = x.Values.Sum(v => v * v);
                double normY = y.Values.Sum(v => v * v);
                return normX > 0 && normY > 0 ? dot / Math.Sqrt(normX * normY) : 0;
            }

            private static Dictionary<double, double> ByMz(SampledSpectrum spectrum)
            {
                var intensities = new Dictionary<double, double>();
                for (int i = 0; i < spectrum.Mz.Length; i++)
                {
                    intensities.TryGetValue(spectrum.Mz[i], out double sum);
                    intensities[spectrum.Mz[i]] = sum + spectrum.Intensity[i];
                }
                return intensities;
            }
        }

        /// <summary>A golden: golden.json and its library sample.</summary>
        private sealed class Golden
        {
            public static Golden Read(string folder)
            {
                string path = TestData.RequireFile(Path.Combine(folder, GOLDEN_FILE));
                var golden = new Golden { Folder = folder };
                using (var json = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    var root = json.RootElement;
                    Assert.AreEqual(GOLDEN_FORMAT, root.GetProperty(@"format").GetString(), @"{0}: recreate it with regression.ps1 -CreateGolden", path);
                    var provenance = root.GetProperty(@"provenance");
                    golden.Commit = provenance.GetProperty(@"commit").GetString();
                    golden.Device = provenance.GetProperty(@"device_used").GetString();
                    golden.OsPlatform = provenance.GetProperty(@"os_platform").GetString();
                    golden.Processor = provenance.GetProperty(@"processor").GetString();
                    golden.Arguments = (provenance.GetProperty(@"arguments").GetString() ?? string.Empty).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var input in root.GetProperty(@"inputs").EnumerateObject())
                        golden.Inputs[input.Name] = input.Value.GetProperty(@"sha256").GetString();
                    foreach (var metric in root.GetProperty(@"metrics").EnumerateObject())
                        golden.Metrics[metric.Name] = metric.Value.GetDouble();
                    golden.UseFineTuned = root.GetProperty(ModelFiles.METRICS_USE_FINETUNED).GetBoolean();
                    var library = root.GetProperty(@"library");
                    golden.DecoyPairRows = library.GetProperty(@"decoy_pair_rows").GetInt32();
                    var exact = root.GetProperty(@"exact");
                    ReadMap(exact.GetProperty(@"training_tables"), golden.TrainingTables);
                    ReadMap(exact.GetProperty(@"models"), golden.Models);
                    golden.Library = new LibrarySummary(library.GetProperty(@"precursors").GetInt64(), library.GetProperty(@"peaks").GetInt64(),
                        exact.GetProperty(@"library_content_sha256").GetString());
                    var sample = root.GetProperty(@"sample");
                    Assert.AreEqual(SAMPLE_MODULUS, sample.GetProperty(@"modulus").GetInt32(), @"{0} was sampled with another modulus", path);
                    golden.Sample = LibrarySample.Read(Path.Combine(folder, sample.GetProperty(@"file").GetString() ?? SAMPLE_FILE));
                }
                return golden;
            }

            /// <summary>Writes golden.json for <paramref name="run"/>, whose sample is written beside it.</summary>
            public static void Write(string path, RunMeasurement run)
            {
                using (var stream = File.Create(path))
                using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    json.WriteStartObject();
                    json.WriteString(@"format", GOLDEN_FORMAT);
                    json.WriteString(@"created", DateTime.Now.ToString(@"s", CultureInfo.InvariantCulture));
                    json.WriteStartObject(@"provenance");
                    foreach (string key in PROVENANCE_KEYS)
                    {
                        if (run.InfoJson.TryGetProperty(key, out var value))
                        {
                            json.WritePropertyName(key);
                            value.WriteTo(json);
                        }
                    }
                    json.WriteNumber(@"torch_cpu_threads", run.TorchThreads);
                    // One line, not one argument per line.
                    json.WriteString(@"arguments", string.Join(@" ", run.Arguments));
                    json.WriteEndObject();
                    json.WritePropertyName(@"inputs");
                    run.InfoJson.GetProperty(@"inputs").WriteTo(json);

                    // Gated, within the tolerances below.
                    json.WriteStartObject(@"metrics");
                    foreach (var pair in run.Metrics)
                        json.WriteNumber(pair.Key, pair.Value);
                    json.WriteEndObject();
                    json.WriteBoolean(ModelFiles.METRICS_USE_FINETUNED, run.UseFineTuned);
                    json.WriteStartObject(@"library");
                    json.WriteNumber(@"precursors", run.Library.Precursors);
                    json.WriteNumber(@"peaks", run.Library.Peaks);
                    json.WriteNumber(@"decoy_pair_rows", run.Pairs.Rows);
                    json.WriteNumber(@"decoy_pairs", run.Pairs.Pairs);
                    json.WriteNumber(@"entrapment_pairs", run.Pairs.EntrapmentPairs);
                    json.WriteNumber(@"targets", run.Pairs.Targets);
                    json.WriteNumber(@"paired_targets", run.Pairs.PairedTargets);
                    json.WriteNumber(@"unpaired_targets_decoy_not_written", run.Pairs.UnpairedTargetsWithoutDecoy);
                    json.WriteEndObject();
                    json.WriteStartObject(@"sample");
                    json.WriteString(@"file", SAMPLE_FILE);
                    json.WriteNumber(@"modulus", SAMPLE_MODULUS);
                    json.WriteNumber(@"precursors", run.Library.Sample.Count);
                    json.WriteString(@"rule", @"the precursors whose 64-bit FNV-1a hash of modseq/charge, shifted right 32 bits, is 0 modulo the modulus");
                    json.WriteEndObject();
                    json.WriteStartObject(@"tolerances");
                    json.WriteNumber(@"pretrained_metric", PRETRAINED_METRIC_TOLERANCE);
                    json.WriteStartObject(@"finetuned_metric");
                    foreach (var pair in FINETUNED_METRIC_TOLERANCES)
                        json.WriteNumber(pair.Key, pair.Value);
                    json.WriteEndObject();
                    json.WriteNumber(@"precursor_and_decoy_pair_count_fraction", PRECURSOR_COUNT_TOLERANCE);
                    json.WriteNumber(@"peak_count_fraction", PEAK_COUNT_TOLERANCE);
                    json.WriteNumber(@"cosine_median_min", COSINE_MEDIAN_MIN);
                    json.WriteNumber(@"cosine_p5_min", COSINE_P5_MIN);
                    json.WriteNumber(@"cosine_p1_min", COSINE_P1_MIN);
                    json.WriteNumber(@"rt_minutes_median_max", RT_MEDIAN_MAX);
                    json.WriteNumber(@"rt_minutes_p95_max", RT_P95_MAX);
                    json.WriteNumber(@"rt_minutes_p99_max", RT_P99_MAX);
                    json.WriteEndObject();

                    // Reported, never gated.
                    json.WriteStartObject(@"exact");
                    json.WriteString(@"use", @"information only: reported as SAME or DIFFERS, never failing a run");
                    WriteMap(json, @"training_tables", run.TrainingTables);
                    WriteMap(json, @"models", run.Models);
                    json.WriteString(@"library_content_sha256", run.Library.ContentSha256);
                    json.WriteString(@"library_content", @"SHA-256 of one line per precursor, sorted: Skyline modified sequence, charge, " +
                                                         @"precursor m/z, RT rounded to 1e-6, then the fragments in m/z order as m/z:intensity, each rounded to 1e-6");
                    json.WriteEndObject();
                    json.WriteEndObject();
                }
            }

            public string Folder { get; private set; }
            public string Commit { get; private set; }
            public string Device { get; private set; }
            public string OsPlatform { get; private set; }
            public string Processor { get; private set; }
            public IReadOnlyList<string> Arguments { get; private set; }
            public SortedDictionary<string, string> Inputs { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> TrainingTables { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> Models { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, double> Metrics { get; } = new SortedDictionary<string, double>(StringComparer.Ordinal);
            public bool UseFineTuned { get; private set; }
            public int DecoyPairRows { get; private set; }
            public LibrarySummary Library { get; private set; }
            public IReadOnlyList<SampledSpectrum> Sample { get; private set; }

            private static void ReadMap(JsonElement element, IDictionary<string, string> map)
            {
                foreach (var property in element.EnumerateObject())
                    map[property.Name] = property.Value.GetString();
            }

            private static void WriteMap(Utf8JsonWriter json, string name, IEnumerable<KeyValuePair<string, string>> map)
            {
                json.WriteStartObject(name);
                foreach (var pair in map)
                    json.WriteString(pair.Key, pair.Value);
                json.WriteEndObject();
            }
        }

        /// <summary>The golden's library counts and content hash.</summary>
        private sealed class LibrarySummary
        {
            public LibrarySummary(long precursors, long peaks, string contentSha256)
            {
                Precursors = precursors;
                Peaks = peaks;
                ContentSha256 = contentSha256;
            }

            public long Precursors { get; }
            public long Peaks { get; }
            public string ContentSha256 { get; }
        }
    }
}
