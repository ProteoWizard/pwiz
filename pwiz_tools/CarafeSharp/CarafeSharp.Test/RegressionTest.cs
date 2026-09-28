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
using pwiz.CarafeSharp.Training;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// The comparator of the golden regression, which <c>regression.ps1</c> runs on a run folder
    /// it made: the RT and MS2 models fine-tuned on a packaged Osprey training export, and the
    /// library predicted from a subset of the library FASTA.
    /// <para>
    /// It reads the run folder from <c>CARAFESHARP_REGRESSION_RUN</c>, and is inconclusive
    /// without it, so the normal test passes skip it. The golden is
    /// <c>regression.data/&lt;dataset&gt;/golden.json</c>, found from
    /// <c>CARAFESHARP_REGRESSION_DATA</c> or above the test assembly.
    /// </para>
    /// <para>
    /// Exact mode, for a CPU run on the processor, operating system and thread count the golden
    /// was made with, where a fine-tune is byte-reproducible: the model weights, every held-out
    /// metric and the library content must be identical. Statistical mode, for anything else
    /// (a GPU run, another machine): the metrics and a sample of the library's spectra must
    /// agree within the tolerances calibrated across runs. In both, the inputs and the four
    /// training tables must be identical, since the training set does not depend on the device.
    /// </para>
    /// <para>
    /// With <c>CARAFESHARP_REGRESSION_CREATE</c> set to a folder, it writes a golden of the run
    /// there instead, after checking that the fine-tuned MS2 model beats the pretrained one.
    /// </para>
    /// </summary>
    [TestClass]
    public class RegressionTest
    {
        public const string REGRESSION_CATEGORY = @"Regression";

        public const string RUN_VARIABLE = @"CARAFESHARP_REGRESSION_RUN";
        public const string CREATE_VARIABLE = @"CARAFESHARP_REGRESSION_CREATE";
        public const string DATA_VARIABLE = @"CARAFESHARP_REGRESSION_DATA";
        public const string MODE_VARIABLE = @"CARAFESHARP_REGRESSION_MODE";

        public const string GOLDEN_FILE = @"golden.json";
        public const string SAMPLE_FILE = @"library_sample.tsv.gz";
        public const string RUN_INFO_FILE = @"regression-run.json";
        public const string REPORT_FILE = @"regression-report.txt";
        public const string DATA_FOLDER = @"regression.data";

        private const string GOLDEN_FORMAT = @"carafesharp-regression-golden-1";
        private const string OUTPUT_FOLDER = @"out";
        private const string MODE_AUTO = @"Auto";
        private const string MODE_EXACT = @"Exact";
        private const string MODE_STATISTICAL = @"Statistical";
        private const string DEVICE_CPU = @"cpu";

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

        /// <summary>The MS2 metrics a golden's fine-tuned model must improve on.</summary>
        private static readonly string[] MS2_METRICS = { @"cos", @"pcc", @"sa", @"spc" };

        /// <summary>regression-run.json values a golden keeps, as provenance.</summary>
        private static readonly string[] PROVENANCE_KEYS =
        {
            @"dataset", @"leg", @"torch", @"device_used", @"commit", @"branch", @"os", @"os_platform", @"processor",
            @"logical_processors", @"omp_num_threads", @"minutes", @"subset_stride", @"subset_records", @"subset_pairing_rows",
            @"arguments", @"export_note",
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

            var run = RunMeasurement.Measure(runFolder, TorchSharp.torch.get_num_threads());
            string createFolder = Environment.GetEnvironmentVariable(CREATE_VARIABLE);
            if (!string.IsNullOrEmpty(createFolder))
            {
                CreateGolden(run, createFolder);
                return;
            }

            string goldenFolder = GetGoldenFolder(run.Dataset);
            var golden = Golden.Read(goldenFolder);
            Compare(golden, run);
            string report = Path.Combine(runFolder, REPORT_FILE);
            File.WriteAllLines(report, _report);
            foreach (string line in _report)
                TestContext.WriteLine(@"{0}", line);
            Assert.AreEqual(0, _failures.Count, @"The run differs from the golden {0}:{1}{2}", goldenFolder, Environment.NewLine,
                string.Join(Environment.NewLine, _failures));
        }

        private void CreateGolden(RunMeasurement run, string folder)
        {
            Assert.IsFalse(run.Dirty, @"A golden must come from a clean working tree; {0} records changes.", RUN_INFO_FILE);
            Assert.IsTrue(run.UseFineTuned, @"The fine-tune chose the pretrained MS2 model ({0} is false).", ModelFiles.METRICS_USE_FINETUNED);
            foreach (string metric in MS2_METRICS)
            {
                double pretrained = run.Metrics[MetricKey(ModelFiles.METRICS_MS2, ModelFiles.METRICS_PRETRAINED, metric)];
                double finetuned = run.Metrics[MetricKey(ModelFiles.METRICS_MS2, ModelFiles.METRICS_FINETUNED, metric)];
                Assert.IsTrue(finetuned > pretrained, @"The fine-tuned MS2 model does not beat the pretrained one on {0}: {1} vs {2}.",
                    metric, finetuned.ToString(@"R", CultureInfo.InvariantCulture), pretrained.ToString(@"R", CultureInfo.InvariantCulture));
            }
            Directory.CreateDirectory(folder);
            string samplePath = Path.Combine(folder, SAMPLE_FILE);
            LibrarySample.Write(samplePath, run.Library.Sample);
            long sampleBytes = new FileInfo(samplePath).Length;
            Assert.IsTrue(sampleBytes <= MAX_SAMPLE_BYTES, @"The library sample is {0} bytes, more than {1}.", sampleBytes, MAX_SAMPLE_BYTES);
            Golden.Write(Path.Combine(folder, GOLDEN_FILE), run);
            TestContext.WriteLine(@"Golden written to {0}: {1} precursors, {2} peaks, {3} sampled ({4} bytes).", folder,
                run.Library.Precursors, run.Library.Peaks, run.Library.Sample.Count, sampleBytes);
        }

        private void Compare(Golden golden, RunMeasurement run)
        {
            string mode = ChooseMode(golden, run, out string why);
            Report(@"Run {0} against the golden {1}", run.Folder, golden.Folder);
            Report(@"Mode: {0} ({1})", mode, why);
            Report(@"Golden: commit {0}, {1} on {2}, {3}; run: commit {4}, {5} on {6}, {7}", golden.Commit, golden.Device, golden.Processor,
                golden.OsPlatform, run.Commit, run.Device, run.Processor, run.OsPlatform);
            if (!golden.Arguments.SequenceEqual(run.Arguments))
                Report(@"INFO arguments differ from the golden's: {0}", DescribeArgumentChange(golden.Arguments, run.Arguments));

            // Whatever the device: the inputs, the training tables and the model choice.
            CheckEqualMaps(@"input", golden.Inputs, run.Inputs, @"the golden is of other inputs; recreate it for these");
            CheckEqualMaps(@"training table", golden.TrainingTables, run.TrainingTables, null);
            Check(golden.UseFineTuned == run.UseFineTuned, true, @"{0}: golden {1}, run {2}", ModelFiles.METRICS_USE_FINETUNED,
                golden.UseFineTuned, run.UseFineTuned);

            bool exact = mode == MODE_EXACT;
            if (exact)
            {
                CheckEqualMaps(@"model", golden.Models, run.Models, null);
                foreach (var pair in golden.Metrics)
                {
                    bool found = run.Metrics.TryGetValue(pair.Key, out double value);
                    Check(found && value.Equals(pair.Value), true, @"metric {0}: golden {1}, run {2}", pair.Key, Format(pair.Value),
                        found ? Format(value) : @"missing");
                }
                Check(golden.Library.Precursors == run.Library.Precursors, true, @"library precursors: golden {0}, run {1}",
                    golden.Library.Precursors, run.Library.Precursors);
                Check(golden.Library.Peaks == run.Library.Peaks, true, @"library peaks: golden {0}, run {1}", golden.Library.Peaks,
                    run.Library.Peaks);
                Check(golden.Library.ContentSha256 == run.Library.ContentSha256, true, @"library content SHA-256: golden {0}, run {1}",
                    golden.Library.ContentSha256, run.Library.ContentSha256);
                Report(@"Statistical checks, for information (DIFF: outside the tolerance):");
            }
            CompareStatistically(golden, run, !exact);
        }

        /// <summary>The metric and library checks for a run on another device; failures count only when <paramref name="gate"/>.</summary>
        private void CompareStatistically(Golden golden, RunMeasurement run, bool gate)
        {
            foreach (var pair in golden.Metrics)
            {
                if (!run.Metrics.TryGetValue(pair.Key, out double value))
                {
                    Check(false, gate, @"metric {0}: missing from the run", pair.Key);
                    continue;
                }
                double tolerance = GetMetricTolerance(pair.Key);
                double difference = value - pair.Value;
                Check(Math.Abs(difference) <= tolerance, gate, @"metric {0}: golden {1}, run {2}, difference {3} (tolerance {4})", pair.Key,
                    Format(pair.Value), Format(value), difference.ToString(@"E2", CultureInfo.InvariantCulture),
                    tolerance.ToString(@"G3", CultureInfo.InvariantCulture));
            }

            long precursorDifference = Math.Abs(run.Library.Precursors - golden.Library.Precursors);
            long allowed = Math.Max(PRECURSOR_COUNT_MIN_ALLOWED, (long)(PRECURSOR_COUNT_TOLERANCE * golden.Library.Precursors));
            Check(precursorDifference <= allowed, gate, @"library precursors: golden {0}, run {1} (at most {2} may differ)",
                golden.Library.Precursors, run.Library.Precursors, allowed);
            double peakChange = golden.Library.Peaks > 0 ? (double)(run.Library.Peaks - golden.Library.Peaks) / golden.Library.Peaks : 0;
            Check(Math.Abs(peakChange) <= PEAK_COUNT_TOLERANCE, gate, @"library peaks: golden {0}, run {1}, change {2:P2} (tolerance {3:P0})",
                golden.Library.Peaks, run.Library.Peaks, peakChange, PEAK_COUNT_TOLERANCE);

            var comparison = SampleComparison.Compare(golden.Sample, run.Library.Sample);
            Check(comparison.MissingWithManyFragments == 0 && comparison.ExtraWithManyFragments == 0, gate,
                @"sampled precursors: {0} of the golden's {1} missing from the run ({2} with more than {3} fragments), {4} only in the run ({5} with more)",
                comparison.Missing, golden.Sample.Count, comparison.MissingWithManyFragments, FEW_FRAGMENTS, comparison.Extra,
                comparison.ExtraWithManyFragments);
            Check(comparison.PrecursorMzDiffers == 0, gate, @"sampled precursor m/z: {0} of {1} differ", comparison.PrecursorMzDiffers,
                comparison.Shared);
            if (comparison.Shared == 0)
            {
                Check(false, gate, @"sampled precursors: none shared with the golden");
                return;
            }
            double cosineMedian = comparison.CosinePercentile(0.5);
            double cosineP5 = comparison.CosinePercentile(0.05);
            double cosineP1 = comparison.CosinePercentile(0.01);
            Check(cosineMedian >= COSINE_MEDIAN_MIN && cosineP5 >= COSINE_P5_MIN && cosineP1 >= COSINE_P1_MIN, gate,
                @"sampled spectral cosine over {0} precursors: median {1:F6} (min {2}), p5 {3:F6} (min {4}), p1 {5:F6} (min {6}), lowest {7:F6}",
                comparison.Shared, cosineMedian, COSINE_MEDIAN_MIN, cosineP5, COSINE_P5_MIN, cosineP1, COSINE_P1_MIN,
                comparison.CosinePercentile(0));
            double rtMedian = comparison.RetentionTimePercentile(0.5);
            double rtP95 = comparison.RetentionTimePercentile(0.95);
            double rtP99 = comparison.RetentionTimePercentile(0.99);
            Check(rtMedian <= RT_MEDIAN_MAX && rtP95 <= RT_P95_MAX && rtP99 <= RT_P99_MAX, gate,
                @"sampled |RT difference| (min): median {0:F4} (max {1}), p95 {2:F4} (max {3}), p99 {4:F4} (max {5}), largest {6:F4}",
                rtMedian, RT_MEDIAN_MAX, rtP95, RT_P95_MAX, rtP99, RT_P99_MAX, comparison.RetentionTimePercentile(1));
            Report(@"INFO sampled precursors with the golden's fragment m/z set: {0} of {1} ({2:P1})", comparison.SameFragmentSet,
                comparison.Shared, (double)comparison.SameFragmentSet / comparison.Shared);
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

        /// <summary>Exact for CPU runs on the golden's processor, OS and thread count, unless <c>CARAFESHARP_REGRESSION_MODE</c> says otherwise.</summary>
        private static string ChooseMode(Golden golden, RunMeasurement run, out string why)
        {
            string requested = Environment.GetEnvironmentVariable(MODE_VARIABLE);
            if (string.Equals(requested, MODE_EXACT, StringComparison.OrdinalIgnoreCase))
            {
                why = MODE_VARIABLE + @" asks for it";
                return MODE_EXACT;
            }
            if (string.Equals(requested, MODE_STATISTICAL, StringComparison.OrdinalIgnoreCase))
            {
                why = MODE_VARIABLE + @" asks for it";
                return MODE_STATISTICAL;
            }
            if (!string.IsNullOrEmpty(requested) && !string.Equals(requested, MODE_AUTO, StringComparison.OrdinalIgnoreCase))
                Assert.Fail(@"{0} must be {1}, {2} or {3}, not {4}", MODE_VARIABLE, MODE_AUTO, MODE_EXACT, MODE_STATISTICAL, requested);
            var differences = new List<string>();
            if (golden.Device != DEVICE_CPU || run.Device != DEVICE_CPU)
                differences.Add(@"not both CPU runs");
            if (golden.OsPlatform != run.OsPlatform)
                differences.Add(@"another OS");
            if (golden.Processor != run.Processor)
                differences.Add(@"another processor");
            if (golden.TorchThreads != run.TorchThreads)
                differences.Add(string.Format(CultureInfo.InvariantCulture, @"{0} libtorch threads, not {1}", run.TorchThreads, golden.TorchThreads));
            why = differences.Count == 0
                ? @"a CPU run on the golden's processor, OS and thread count, where a fine-tune is byte-reproducible"
                : string.Join(@", ", differences);
            return differences.Count == 0 ? MODE_EXACT : MODE_STATISTICAL;
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

        private void CheckEqualMaps(string what, IReadOnlyDictionary<string, string> golden, IReadOnlyDictionary<string, string> run, string hint)
        {
            foreach (var pair in golden)
            {
                run.TryGetValue(pair.Key, out string value);
                Check(value == pair.Value, true, @"{0} {1}: golden {2}, run {3}{4}", what, pair.Key, pair.Value, value ?? @"missing",
                    value != pair.Value && hint != null ? @" (" + hint + @")" : string.Empty);
            }
        }

        /// <summary>Reports one check; a failed one fails the test only when <paramref name="gate"/>.</summary>
        private void Check(bool passed, bool gate, string format, params object[] args)
        {
            string message = string.Format(CultureInfo.InvariantCulture, format, args);
            string status = passed ? @"PASS" : gate ? @"FAIL" : @"DIFF";
            Report(@"{0} {1}", status, message);
            if (!passed && gate)
                _failures.Add(message);
        }

        private void Report(string format, params object[] args)
        {
            _report.Add(string.Format(CultureInfo.InvariantCulture, format, args));
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

        /// <summary>What a run folder holds: its provenance, training tables, models, metrics and library.</summary>
        private sealed class RunMeasurement
        {
            public static RunMeasurement Measure(string folder, int torchThreads)
            {
                string infoPath = TestData.RequireFile(Path.Combine(folder, RUN_INFO_FILE));
                string output = Path.Combine(folder, OUTPUT_FOLDER);
                var run = new RunMeasurement { Folder = folder, TorchThreads = torchThreads };
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
                    Assert.AreEqual(0, root.GetProperty(@"exit_code").GetInt32(), @"CarafeSharp failed in {0}", folder);
                    foreach (var input in root.GetProperty(@"inputs").EnumerateObject())
                        run.Inputs[input.Name] = input.Value.GetProperty(@"sha256").GetString();
                }
                foreach (string table in TRAINING_TABLES)
                    run.TrainingTables[table] = Sha256(Path.Combine(output, table));
                foreach (string model in MODEL_FILES)
                    run.Models[model] = Sha256(Path.Combine(output, model));
                run.ReadMetrics(TestData.RequireFile(Path.Combine(output, ModelFiles.METRICS)));
                run.Library = LibraryContent.Read(TestData.RequireFile(Path.Combine(output, BlibLibraryWriter.FILE_NAME)));
                return run;
            }

            public string Folder { get; private set; }
            public JsonElement InfoJson { get; private set; }
            public string Dataset { get; private set; }
            public string Device { get; private set; }
            public string Commit { get; private set; }
            public bool Dirty { get; private set; }
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
                using (var connection = new SQLiteConnection(@"Data Source=" + path + @";Read Only=True;"))
                {
                    connection.Open();
                    const string sql = @"SELECT r.peptideModSeq, r.precursorCharge, r.precursorMZ, r.retentionTime, r.numPeaks, p.peakMZ, p.peakIntensity " +
                                       @"FROM RefSpectra r JOIN RefSpectraPeaks p ON p.RefSpectraID = r.id";
                    using (var command = new SQLiteCommand(sql, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string sequence = reader.GetString(0);
                            long charge = reader.GetInt64(1);
                            double precursorMz = reader.GetDouble(2);
                            double retentionTime = reader.GetDouble(3);
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
                            peaks += count;
                            lines.Add(CanonicalLine(sequence, charge, precursorMz, retentionTime, mz, intensity));
                            string key = PrecursorKey(sequence, charge);
                            if (IsSampled(key))
                                sample.Add(new SampledSpectrum(key, precursorMz, retentionTime, mz, intensity));
                        }
                    }
                }
                SQLiteConnection.ClearAllPools();
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
                    Assert.AreEqual(GOLDEN_FORMAT, root.GetProperty(@"format").GetString(), path);
                    var provenance = root.GetProperty(@"provenance");
                    golden.Commit = provenance.GetProperty(@"commit").GetString();
                    golden.Device = provenance.GetProperty(@"device_used").GetString();
                    golden.OsPlatform = provenance.GetProperty(@"os_platform").GetString();
                    golden.Processor = provenance.GetProperty(@"processor").GetString();
                    golden.TorchThreads = provenance.GetProperty(@"torch_cpu_threads").GetInt32();
                    golden.Arguments = provenance.GetProperty(@"arguments").EnumerateArray().Select(a => a.GetString()).ToList();
                    foreach (var input in root.GetProperty(@"inputs").EnumerateObject())
                        golden.Inputs[input.Name] = input.Value.GetProperty(@"sha256").GetString();
                    var exact = root.GetProperty(@"exact");
                    ReadMap(exact.GetProperty(@"training_tables"), golden.TrainingTables);
                    ReadMap(exact.GetProperty(@"models"), golden.Models);
                    foreach (var metric in exact.GetProperty(@"metrics").EnumerateObject())
                        golden.Metrics[metric.Name] = metric.Value.GetDouble();
                    golden.UseFineTuned = exact.GetProperty(ModelFiles.METRICS_USE_FINETUNED).GetBoolean();
                    var library = exact.GetProperty(@"library");
                    golden.Library = new LibrarySummary(library.GetProperty(@"precursors").GetInt64(), library.GetProperty(@"peaks").GetInt64(),
                        library.GetProperty(@"content_sha256").GetString());
                    var sample = root.GetProperty(@"statistical").GetProperty(@"sample");
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
                    json.WriteEndObject();
                    json.WritePropertyName(@"inputs");
                    run.InfoJson.GetProperty(@"inputs").WriteTo(json);

                    json.WriteStartObject(@"exact");
                    WriteMap(json, @"training_tables", run.TrainingTables);
                    WriteMap(json, @"models", run.Models);
                    json.WriteStartObject(@"metrics");
                    foreach (var pair in run.Metrics)
                        json.WriteNumber(pair.Key, pair.Value);
                    json.WriteEndObject();
                    json.WriteBoolean(ModelFiles.METRICS_USE_FINETUNED, run.UseFineTuned);
                    json.WriteStartObject(@"library");
                    json.WriteNumber(@"precursors", run.Library.Precursors);
                    json.WriteNumber(@"peaks", run.Library.Peaks);
                    json.WriteString(@"content_sha256", run.Library.ContentSha256);
                    json.WriteString(@"content", @"SHA-256 of one line per precursor, sorted: Skyline modified sequence, charge, precursor m/z, " +
                                                 @"RT rounded to 1e-6, then the fragments in m/z order as m/z:intensity, each rounded to 1e-6");
                    json.WriteEndObject();
                    json.WriteEndObject();

                    json.WriteStartObject(@"statistical");
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
                    json.WriteNumber(@"precursor_count_fraction", PRECURSOR_COUNT_TOLERANCE);
                    json.WriteNumber(@"peak_count_fraction", PEAK_COUNT_TOLERANCE);
                    json.WriteNumber(@"cosine_median_min", COSINE_MEDIAN_MIN);
                    json.WriteNumber(@"cosine_p5_min", COSINE_P5_MIN);
                    json.WriteNumber(@"cosine_p1_min", COSINE_P1_MIN);
                    json.WriteNumber(@"rt_minutes_median_max", RT_MEDIAN_MAX);
                    json.WriteNumber(@"rt_minutes_p95_max", RT_P95_MAX);
                    json.WriteNumber(@"rt_minutes_p99_max", RT_P99_MAX);
                    json.WriteEndObject();
                    json.WriteEndObject();
                    json.WriteEndObject();
                }
            }

            public string Folder { get; private set; }
            public string Commit { get; private set; }
            public string Device { get; private set; }
            public string OsPlatform { get; private set; }
            public string Processor { get; private set; }
            public int TorchThreads { get; private set; }
            public IReadOnlyList<string> Arguments { get; private set; }
            public SortedDictionary<string, string> Inputs { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> TrainingTables { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, string> Models { get; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public SortedDictionary<string, double> Metrics { get; } = new SortedDictionary<string, double>(StringComparer.Ordinal);
            public bool UseFineTuned { get; private set; }
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
