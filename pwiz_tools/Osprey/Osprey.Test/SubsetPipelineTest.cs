/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Runs the whole Osprey pipeline in-process on a small rectangle of real Stellar DIA data
    /// (<c>TestData\StellarSubset.zip</c>: one isolation window, 7 min of three runs, and a
    /// 358-precursor library; see its README.txt), in the manner of the regression's legs, which
    /// otherwise need the 4.5 GB Stellar download and minutes per leg. Each run takes seconds.
    ///
    /// <para>This is where checks of pipeline BEHAVIOR belong: caching, resume, task boundaries,
    /// sidecar contracts, route markers, which files a run writes. They are valid on any data,
    /// so they run here on every commit rather than as legs of <c>regression.ps1</c>, which keeps
    /// the results at real-data scale (issue #4728). The legs here follow the regression's modes:
    /// straight-through (mode 1), warm re-run with every task cached (mode 4), resume after
    /// invalidating the join (mode 2), second-pass rehydrate (mode 5), and the four-task HPC
    /// chain across process-like boundaries (mode 3, on both subsets), each re-run compared to
    /// the straight-through library and FDR sidecars at 1e-9. The data is not a scientific
    /// gate - the regression stays that - so the counts asserted are loose floors that catch a
    /// pipeline stopping early or detecting nothing.</para>
    ///
    /// <para>Assertions read only untranslated text: exit codes, library contents, file names and
    /// the [TASK] / [PATH] / [COUNT] lines --perf-stats writes.</para>
    /// </summary>
    [TestClass]
    public class SubsetPipelineTest
    {
        private const string DATA_ZIP = @"StellarSubset.zip";
        private const string ASTRAL_ZIP = @"AstralSubset.zip";
        private const string ASTRAL_LIBRARY_FILE = @"astral-subset-library.tsv";
        private const string LIBRARY_FILE = @"stellar-subset-library.tsv";
        private const string MANIFEST_FILE = @"manifest.tsv";
        private const string LIBDECOY_FILE = @"stellar-subset-libdecoy.tsv";
        private const string LIBDECOY_PAIRING_FILE = @"stellar-subset-libdecoy-pairing.tsv";
        private const string BLIB_FILE = @"output.blib";
        private const string MZML_EXTENSION = @".mzML";
        private const string SPECTRA_CACHE_EXTENSION = @".spectra.bin";
        private const double TOLERANCE = 1e-9;

        // Floors well under what the data gives (about 177 precursors reported, 169 of the 178
        // the full 3-file run detected in this rectangle, 8 of the 180 it did not).
        private const int MIN_PRECURSORS = 140;
        private const double MIN_RECOVERED_FRACTION = 0.8;
        private const double MAX_NEWLY_DETECTED_FRACTION = 0.15;
        // The library-decoy variant reports about 150.
        private const int MIN_LIBDECOY_PRECURSORS = 100;
        // The Astral subset reports about 164.
        private const int MIN_ASTRAL_PRECURSORS = 120;
        // The Astral subset recovers about 164 of the 203 its full run detected (81%), so the
        // Stellar floor would leave one precursor of headroom.
        private const double MIN_ASTRAL_RECOVERED_FRACTION = 0.7;

        private static readonly string[] RUN_NAMES =
        {
            @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20",
            @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21",
            @"Ste-2024-12-02_HeLa_4mz_sDIA_400-900_22"
        };

        private static readonly string[] ASTRAL_RUN_NAMES =
        {
            @"Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_49",
            @"Ast-2024-12-05_HeLa_3mzDIA_6mIIT_400-900_55"
        };

        private static readonly string[] ALL_TASKS =
        {
            PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME, PerFileRescoreTask.TASK_NAME,
            SecondPassFdrTask.TASK_NAME
        };

        private string _testDir;
        private string _dataDir;

        [TestInitialize]
        public void ExtractData()
        {
            _testDir = Path.Combine(Path.GetTempPath(), @"osprey-subset-" + Guid.NewGuid().ToString(@"N"));
            _dataDir = Path.Combine(_testDir, @"data");
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, @"TestData", DATA_ZIP), _dataDir);
        }

        [TestCleanup]
        public void DeleteTestDir()
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }

        /// <summary>
        /// Straight-through, then the three in-place re-runs the regression makes of the same
        /// directory: warm (nothing invalidated), resume (join invalidated) and rehydrate (only
        /// the second pass invalidated). Each must produce the straight-through library.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetStraightThroughAndReruns()
        {
            string straightDir = CreateDir(@"straight");
            string blib = Path.Combine(straightDir, BLIB_FILE);

            // Mode 1: straight-through, with the second-pass worker verifier on as the regression runs it.
            string log = RunAnalysis(straightDir, DataInputs(), Verifier(true));
            ValidateStraightThrough(straightDir, log);
            string coldBlib = Path.Combine(straightDir, @"output_cold.blib");
            File.Copy(blib, coldBlib);
            byte[] coldHash = HashFile(blib);

            // Mode 4: the identical command again. Every task must report a cache hit, nothing
            // may be re-scored from spectra, and the library must not be rewritten.
            log = RunAnalysis(straightDir, DataInputs(), Verifier(false));
            AssertTasks(log, ALL_TASKS, Array.Empty<string>());
            AssertNoRecompute(log);
            CollectionAssert.AreEqual(coldHash, HashFile(blib), @"warm re-run rewrote the library");

            // Mode 2: invalidate the join and the library, and resume from the spectra caches
            // alone - the inputs are named in the work directory, where no mzML exists.
            DeleteFiles(straightDir, Path.GetFileName(TaskValiditySidecar.PathFor(@"*", FirstPassFdrTask.TASK_NAME)));
            DeleteSecondPassOutputs(blib);
            log = RunAnalysis(straightDir, RunNames(straightDir, MZML_EXTENSION), Verifier(false));
            AssertTasks(log, new[] { PerFileScoringTask.TASK_NAME, PerFileRescoreTask.TASK_NAME },
                new[] { FirstPassFdrTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertNoRecompute(log);
            AssertHasLine(log, PathLine(LogKey.ROUTE_INPUT_SOURCE, @"spectra-cache"));
            AssertBlibsEqual(coldBlib, blib);

            // Mode 5: invalidate only the second pass, so its bundle is rebuilt from the first
            // pass's own sidecars.
            DeleteSecondPassOutputs(blib);
            log = RunAnalysis(straightDir, DataInputs(), Verifier(false));
            AssertTasks(log,
                new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME, PerFileRescoreTask.TASK_NAME },
                new[] { SecondPassFdrTask.TASK_NAME });
            AssertNoRecompute(log);
            Assert.IsTrue(HasLine(log, PathLine(LogKey.ROUTE_FIRST_PASS_FDR, @"own-bundle-stream")) ||
                          HasLine(log, PathLine(LogKey.ROUTE_FIRST_PASS_FDR, @"survivor-loader-only")),
                @"rehydrate did not enter the first pass's own-bundle loader" + Environment.NewLine + log);
            AssertBlibsEqual(coldBlib, blib);
        }

        /// <summary>
        /// The four-task HPC chain, each phase in its own directory holding only the files the
        /// previous phase shipped, as separate computers would run it. The chain's library and
        /// every FDR sidecar it wrote must equal the straight-through run's.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetHpcTaskChain()
        {
            AssertHpcChainMatchesStraight(new Subset(_dataDir, LIBRARY_FILE, @"unit", RUN_NAMES));
        }

        /// <summary>
        /// The same chain on the Astral subset: high-resolution scoring on both sides of every
        /// task boundary.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestAstralSubsetHpcTaskChain()
        {
            AssertHpcChainMatchesStraight(new Subset(ExtractAstral(), ASTRAL_LIBRARY_FILE, @"hram", ASTRAL_RUN_NAMES));
        }

        /// <summary>
        /// The StellarLibDecoy / StellarGenDecoyEntrap legs: a library that supplies its own
        /// decoys and carries shuffled entrapment peptides, searched with its pairing manifest,
        /// the model diagnostics report and FDRBench input for both passes. Then the report again
        /// from the finished analysis alone, with <c>--task ModelDiagnostics</c>.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryDecoysAndReports()
        {
            string workDir = CreateDir(@"libdecoy");
            string bench = Path.Combine(workDir, @"bench.tsv");
            var args = new[]
            {
                OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY.ArgumentText,
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE),
                OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText,
                OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, bench,
                OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText, @"both",
                OspreyCommandArgs.ARG_WRITE_PIN.ArgumentText
            };
            RunAnalysis(workDir, DataInputs(), LIBDECOY_FILE, Verifier(false), args);
            string blib = Path.Combine(workDir, BLIB_FILE);
            Assert.IsTrue(BlibComparer.CountRows(blib, @"RefSpectra") >= MIN_LIBDECOY_PRECURSORS);
            // No decoy reaches the library, but entrapment peptides compete as targets.
            Assert.AreEqual(0, BlibComparer.CountWhere(blib, @"Proteins", @"accession LIKE 'decoy_%'"));
            foreach (string pass in new[] { @"pass1", @"pass2" })
                AssertHasRows(Path.ChangeExtension(bench, pass + @".tsv"));
            // The protein-group and summary reports are named from the output library.
            AssertHasRows(Path.Combine(workDir, @"output.protein_groups.tsv"));
            AssertHasRows(Path.Combine(workDir, @"output.stats.tsv"));
            string diagnosticsReport = Path.Combine(workDir, @"output.model-diagnostics.html");
            AssertHasRows(diagnosticsReport);

            // The report alone, from what the analysis left: every analysis task is cached.
            File.Delete(diagnosticsReport);
            string log = RunAnalysis(workDir, DataInputs(), LIBDECOY_FILE, Verifier(false),
                args.Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, ModelDiagnosticsTask.TASK_NAME }).ToArray());
            AssertNoRecompute(log);
            AssertHasRows(diagnosticsReport);
        }

        /// <summary>
        /// Interrupted second-pass rescoring, the two shapes an interruption leaves (regression
        /// modes 8 and 9), on the library-decoy search with the diagnostics report. The last run's
        /// rescore is cut and the same command run again: it must re-score that run and only
        /// that run, say so, and finish to the uninterrupted library.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetRescoreResume()
        {
            string workDir = CreateDir(@"rescore-resume");
            var args = new[]
            {
                OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY.ArgumentText,
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE),
                OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText
            };
            RunAnalysis(workDir, DataInputs(), LIBDECOY_FILE, Verifier(false), args);
            string blib = Path.Combine(workDir, BLIB_FILE);
            string uninterruptedBlib = Path.Combine(workDir, @"output_uninterrupted.blib");
            File.Copy(blib, uninterruptedBlib);
            string cutRun = RUN_NAMES[RUN_NAMES.Length - 1];

            // Mode 8: a rescore that stopped before the last run - neither of its products exists.
            // Once, any current second-pass file read as "the rescore is finished", and the
            // remaining runs carried first-pass q-values into the library.
            CutRescore(workDir, cutRun, true);
            DeleteSecondPassOutputs(blib);
            string log = RunAnalysis(workDir, RunNames(workDir, MZML_EXTENSION), LIBDECOY_FILE, Verifier(false), args);
            Assert.AreEqual(RUN_NAMES.Length, Directory.GetFiles(workDir, @"*.scores-reconciled.parquet").Length,
                @"the rescore did not finish the cohort" + Environment.NewLine + log);
            AssertHasLine(log, PathLine(LogKey.ROUTE_RESCORE_RESUME, string.Empty));
            AssertRescoredOnly(log, 1);
            AssertBlibsEqual(uninterruptedBlib, blib);

            // Mode 9: a crash between the two products - the reconciled parquet is written and
            // stamped, the second-pass sidecar is not. The cohort count calls the run outstanding
            // while a per-file check would call it done; it must be re-scored.
            CutRescore(workDir, cutRun, false);
            DeleteSecondPassOutputs(blib);
            log = RunAnalysis(workDir, RunNames(workDir, MZML_EXTENSION), LIBDECOY_FILE, Verifier(false), args);
            AssertRescoredOnly(log, 1);
            AssertBlibsEqual(uninterruptedBlib, blib);
        }

        /// <summary>
        /// Options that must not change the answer - the cross-implementation dumps, an input list
        /// instead of -i - against a plain straight-through run, and the alternative FDR methods
        /// and levels, which must at least complete and report precursors.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetOptionVariants()
        {
            string baseDir = CreateDir(@"sequential");
            // Explicitly one file at a time: OSPREY_MAX_PARALLEL_FILES is read at class load, so an
            // exported value would otherwise make this baseline parallel too.
            string baseLog = RunAnalysis(baseDir, DataInputs(), Verifier(false),
                OspreyCommandArgs.ARG_PARALLEL_FILES.ArgumentText, @"1");
            string baseBlib = Path.Combine(baseDir, BLIB_FILE);

            // Files scored and re-scored concurrently must give the sequential answer. The subset's
            // three runs reach the second-pass worker together, so a race there shows every time
            // (a shared FrozenModelScorer scratch buffer mixed the files' features).
            string parallelDir = CreateDir(@"parallel-files");
            RunAnalysis(parallelDir, DataInputs(), Verifier(false),
                OspreyCommandArgs.ARG_PARALLEL_FILES.ArgumentText, RUN_NAMES.Length.ToString(CultureInfo.InvariantCulture));
            AssertBlibsEqual(baseBlib, Path.Combine(parallelDir, BLIB_FILE));

            // --diagnostics writes its dumps to the current directory.
            string diagnosticsDir = CreateDir(@"diagnostics");
            // It also sets every OSPREY_DUMP_* variable in the process environment, which would turn
            // the dumps on for every later command line in this test host, so restore them.
            string savedDirectory = Directory.GetCurrentDirectory();
            var savedVariables = SnapshotOspreyVariables();
            try
            {
                Directory.SetCurrentDirectory(diagnosticsDir);
                RunAnalysis(diagnosticsDir, DataInputs(), Verifier(false), OspreyCommandArgs.ARG_DIAGNOSTICS.ArgumentText);
            }
            finally
            {
                Directory.SetCurrentDirectory(savedDirectory);
                RestoreOspreyVariables(savedVariables);
            }
            Assert.AreNotEqual(0, Directory.GetFiles(diagnosticsDir, @"cs_*").Length, @"no diagnostic dumps written");
            AssertBlibsEqual(baseBlib, Path.Combine(diagnosticsDir, BLIB_FILE));

            // --input-list, with a log file and every line decoration.
            string listDir = CreateDir(@"input-list");
            string inputList = Path.Combine(listDir, @"inputs.txt");
            File.WriteAllLines(inputList, new[] { @"# the three runs" }.Concat(DataInputs()));
            string logFile = Path.Combine(listDir, @"osprey.log");
            RunOsprey(new[]
            {
                OspreyCommandArgs.ARG_INPUT_LIST.ArgumentText, inputList,
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(listDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, listDir,
                OspreyCommandArgs.ARG_LOG_FILE.ArgumentText, logFile,
                OspreyCommandArgs.ARG_TIMESTAMP.ArgumentText, OspreyCommandArgs.ARG_MEMSTAMP.ArgumentText,
                OspreyCommandArgs.ARG_VERBOSE.ArgumentText
            }.Concat(CommonArgs()).ToArray(), Verifier(false));
            AssertHasRows(logFile);
            AssertBlibsEqual(baseBlib, Path.Combine(listDir, BLIB_FILE));

            foreach (var variant in new[]
                     {
                         new[] { OspreyCommandArgs.ARG_FDR_METHOD.ArgumentText, @"gbdt" },
                         new[] { OspreyCommandArgs.ARG_FDR_LEVEL.ArgumentText, @"peptide" },
                         new[] { OspreyCommandArgs.ARG_FDR_LEVEL.ArgumentText, @"protein" },
                         new[] { OspreyCommandArgs.ARG_SHARED_PEPTIDES.ArgumentText, @"razor" },
                         new[] { OspreyCommandArgs.ARG_SHARED_PEPTIDES.ArgumentText, @"unique" },
                         new[] { OspreyCommandArgs.ARG_NO_PREFILTER.ArgumentText }
                     })
            {
                string variantDir = CreateDir(string.Join(@"-", variant).TrimStart('-'));
                RunAnalysis(variantDir, DataInputs(), Verifier(false), variant);
                Assert.IsTrue(BlibComparer.CountRows(Path.Combine(variantDir, BLIB_FILE), @"RefSpectra") > 0,
                    string.Join(@" ", variant) + @" reported no precursors");
            }

            // FDRBench input with one row per precursor and run.
            string benchDir = CreateDir(@"fdrbench-per-run");
            string bench = Path.Combine(benchDir, @"bench.tsv");
            RunAnalysis(benchDir, DataInputs(), Verifier(false), OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, bench,
                OspreyCommandArgs.ARG_FDRBENCH_PER_RUN.ArgumentText);
            AssertHasRows(bench);

            // The non-default second-pass arms the regression's mode 10 runs: first-pass q-values
            // transferred, and the experiment score as the mean of each precursor's best two runs.
            string transferDir = CreateDir(@"pass2-transfer");
            int savedMeanBestN = OspreyEnvironment.MeanBestN;
            try
            {
                // OSPREY_EXPERIMENT_AGG is parsed once at class load, so an override cannot reach
                // it; set the value it parses to, as MeanBestNAggregationTest does.
                OspreyEnvironment.MeanBestN = 2;
                string transferLog = RunAnalysis(transferDir, DataInputs(), new Dictionary<string, string>
                {
                    { @"OSPREY_PASS2_VERIFY_WORKER", string.Empty },
                    { @"OSPREY_PASS2_QVALUE", @"transfer" }
                });
                AssertHasLine(transferLog, PathLine(LogKey.ROUTE_PASS2_QVALUE, @"transfer"));
                AssertHasLine(transferLog, PathLine(LogKey.ROUTE_EXPERIMENT_AGG, OspreyEnvironment.ExperimentAgg));
            }
            finally
            {
                OspreyEnvironment.MeanBestN = savedMeanBestN;
            }
            Assert.IsTrue(BlibComparer.CountRows(Path.Combine(transferDir, BLIB_FILE), @"RefSpectra") > 0,
                @"the transfer arm reported no precursors");

            // Calibration from a sample of the library, as on a full-size library: sampled
            // below the 178 detected precursors, so the ladder must widen the sample to fit.
            string sampledDir = CreateDir(@"calibration-sample");
            string sampledLog = RunAnalysis(sampledDir, DataInputs(), new Dictionary<string, string>
            {
                { @"OSPREY_PASS2_VERIFY_WORKER", string.Empty },
                { @"OSPREY_CAL_SAMPLE_SIZE", @"100" }
            });
            Assert.IsTrue(CalibrationMatchesScored(sampledLog) < CalibrationMatchesScored(baseLog),
                @"the calibration sample was not smaller than the whole library");
            Assert.IsTrue(BlibComparer.CountRows(Path.Combine(sampledDir, BLIB_FILE), @"RefSpectra") > 0,
                @"sampled calibration reported no precursors");

            // The spectra-cache task alone writes one cache per run and nothing downstream.
            string cacheDir = CreateDir(@"spectra-cache");
            RunAnalysis(cacheDir, DataInputs(), Verifier(false), OspreyCommandArgs.ARG_TASK.ArgumentText,
                SpectraCacheTask.TASK_NAME);
            Assert.AreEqual(RUN_NAMES.Length, Directory.GetFiles(cacheDir, @"*" + SPECTRA_CACHE_EXTENSION).Length);
            Assert.IsFalse(File.Exists(Path.Combine(cacheDir, BLIB_FILE)));
        }

        /// <summary>
        /// The Astral leg: high-resolution data (<c>TestData\AstralSubset.zip</c>, one 3 m/z
        /// window x 3 min of two runs), which runs the HRAM paths - MS1 isotope scoring, ppm
        /// tolerances - that unit-resolution data never reaches. Straight-through, then the same
        /// search with the two files scored concurrently, which must give the same library.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestAstralSubsetHram()
        {
            string dataDir = ExtractAstral();
            var inputs = ASTRAL_RUN_NAMES.Select(run => Path.Combine(dataDir, run + MZML_EXTENSION)).ToArray();

            string[] AstralArgs(string workDir, params string[] extraArgs)
            {
                return InputArgs(inputs).Concat(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(dataDir, ASTRAL_LIBRARY_FILE),
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                    OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir,
                    OspreyCommandArgs.ARG_RESOLUTION.ArgumentText, @"hram",
                    OspreyCommandArgs.ARG_PROTEIN_FDR.ArgumentText, @"0.01",
                    OspreyCommandArgs.ARG_THREADS.ArgumentText, @"4",
                    OspreyCommandArgs.ARG_PERF_STATS.ArgumentText
                }).Concat(extraArgs).ToArray();
            }

            string straightDir = CreateDir(@"astral");
            string log = RunOsprey(AstralArgs(straightDir), Verifier(false));
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            string blib = Path.Combine(straightDir, BLIB_FILE);
            int precursors = BlibComparer.CountRows(blib, @"RefSpectra");
            Assert.IsTrue(precursors >= MIN_ASTRAL_PRECURSORS, string.Format(@"{0} precursors reported", precursors));
            Assert.AreEqual(precursors * ASTRAL_RUN_NAMES.Length, BlibComparer.CountRows(blib, @"RetentionTimes"));
            AssertRecoversFullRun(Path.Combine(dataDir, MANIFEST_FILE), blib, MIN_ASTRAL_RECOVERED_FRACTION);

            string parallelDir = CreateDir(@"astral-parallel");
            RunOsprey(AstralArgs(parallelDir, OspreyCommandArgs.ARG_PARALLEL_FILES.ArgumentText,
                ASTRAL_RUN_NAMES.Length.ToString(CultureInfo.InvariantCulture)), Verifier(false));
            AssertBlibsEqual(blib, Path.Combine(parallelDir, BLIB_FILE));
        }

        /// <summary>
        /// Libraries whose generated decoys have no fragments of their own. A decoy is built by
        /// recomputing the target's b/y fragments on the reversed sequence, so a fragment of unknown
        /// type is copied verbatim and one with no fragment number is dropped. An Osprey output
        /// .blib (no fragment annotations) and a library whose fragment numbers are all missing
        /// must each stop with one plain error; a library with a few such entries must warn and
        /// still finish.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetUnusableDecoys()
        {
            string straightDir = CreateDir(@"straight");
            RunAnalysis(straightDir, DataInputs(), Verifier(false));

            // Every decoy a copy of its target.
            string blib = Path.Combine(straightDir, BLIB_FILE);
            int blibPrecursors = BlibComparer.CountRows(blib, @"RefSpectra");
            string output = RunExpectingRefusal(@"blib-library", blib);
            StringAssert.Contains(output, RefusalText(blib, blibPrecursors));

            // Every fragment number missing, so every decoy fragment is dropped.
            string noNumbers = WriteLibraryWithoutFragmentNumbers(@"no-fragment-numbers.tsv", _ => true);
            int libraryPrecursors = CountLibraryPrecursors(Path.Combine(_dataDir, LIBRARY_FILE));
            output = RunExpectingRefusal(@"no-fragment-numbers", noNumbers);
            StringAssert.Contains(output, RefusalText(noNumbers, libraryPrecursors));

            // Two precursors without fragment numbers: a warning naming the count, and a search.
            var strayPeptides = new HashSet<string>(File.ReadLines(Path.Combine(_dataDir, LIBRARY_FILE))
                .Skip(1).Select(line => line.Split('\t')[0]).Distinct().Take(2));
            string strays = WriteLibraryWithoutFragmentNumbers(@"two-stray.tsv", strayPeptides.Contains);
            string strayDir = CreateDir(@"two-stray");
            string log = RunOsprey(InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, strays,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(strayDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, strayDir
            }).Concat(CommonArgs()).ToArray(), Verifier(false));
            StringAssert.Contains(log, string.Format(
                OspreyTasksResources.PerFileScoringTask_CheckDecoysUsable_Decoys_with_no_fragment_distinct_from_their_target___0__of__1____2____generated_,
                strayPeptides.Count, libraryPrecursors, strayPeptides.Count / (double)libraryPrecursors, strays));
            Assert.IsTrue(BlibComparer.CountRows(Path.Combine(strayDir, BLIB_FILE), @"RefSpectra") >= MIN_PRECURSORS);
        }

        /// <summary>
        /// Search the subset with <paramref name="library"/>, which must be refused before any work:
        /// the exit code for a failure to start, exactly one error line, no exception type, and no
        /// line saying the exit code had to be reconciled with the log.
        /// </summary>
        private string RunExpectingRefusal(string dirName, string library)
        {
            string workDir = CreateDir(dirName);
            var args = InputArgs(DataInputs().Take(1)).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs()).ToArray();
            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(Verifier(false)))
            {
                exitCode = InProcessOsprey.Run(args, out output);
            }
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, exitCode, output);
            Assert.AreEqual(1, SplitLines(output).Count(CommandStatusWriter.IsErrorLine), output);
            Assert.IsFalse(output.Contains(typeof(Exception).Namespace + @"."), output);
            Assert.IsFalse(HasLine(output, PathLine(LogKey.ROUTE_EXIT_RECONCILED, string.Empty)), output);
            return output;
        }

        /// <summary>
        /// The refusal for a library none of whose <paramref name="decoyCount"/> generated decoys
        /// has a fragment of its own.
        /// </summary>
        private static string RefusalText(string library, int decoyCount)
        {
            return string.Format(
                OspreyTasksResources.PerFileScoringTask_CheckDecoysUsable_The_library__3__is_missing_b_or_y_fragment_ion_annotations_or_fragment_numbers___0__of__,
                decoyCount, decoyCount, 1.0, library, PerFileScoringTask.MAX_UNUSABLE_DECOY_FRACTION,
                OspreyArgNames.Text(OspreyArgNames.DECOYS_IN_LIBRARY));
        }

        /// <summary>
        /// The distinct precursors (modified peptide, charge) in a DIA-NN style .tsv library.
        /// </summary>
        private static int CountLibraryPrecursors(string library)
        {
            var lines = File.ReadLines(library).ToList();
            int charge = Array.IndexOf(lines[0].Split('\t'), @"PrecursorCharge");
            return lines.Skip(1).Select(line => line.Split('\t'))
                .Select(fields => fields[0] + @"|" + fields[charge]).Distinct().Count();
        }

        /// <summary>
        /// A copy of the subset library with the fragment number set to 0 on every row of the
        /// precursors <paramref name="blank"/> selects by modified peptide.
        /// </summary>
        private string WriteLibraryWithoutFragmentNumbers(string fileName, Func<string, bool> blank)
        {
            var lines = File.ReadAllLines(Path.Combine(_dataDir, LIBRARY_FILE));
            int column = Array.IndexOf(lines[0].Split('\t'), @"FragmentNumber");
            Assert.AreNotEqual(-1, column);
            for (int i = 1; i < lines.Length; i++)
            {
                var fields = lines[i].Split('\t');
                if (blank(fields[0]))
                {
                    fields[column] = @"0";
                    lines[i] = string.Join('\t', fields);
                }
            }
            string path = Path.Combine(_testDir, fileName);
            File.WriteAllLines(path, lines);
            return path;
        }

        /// <summary>
        /// The calibration matches scored in the first run's first calibration pass, from the
        /// [COUNT] line --perf-stats writes.
        /// </summary>
        private static int CalibrationMatchesScored(string log)
        {
            var match = Regex.Match(log, @"Calibration pass 1 matches scored \[[^\]]*\]: (\d+)");
            Assert.IsTrue(match.Success, @"no calibration match count in the log");
            return int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Every OSPREY_* variable in the process environment, to restore after a command line that
        /// sets some.
        /// </summary>
        private static Dictionary<string, string> SnapshotOspreyVariables()
        {
            return Environment.GetEnvironmentVariables().Keys.Cast<string>()
                .Where(name => name.StartsWith(@"OSPREY_", StringComparison.OrdinalIgnoreCase))
                .ToDictionary(name => name, Environment.GetEnvironmentVariable);
        }

        private static void RestoreOspreyVariables(Dictionary<string, string> saved)
        {
            foreach (string name in SnapshotOspreyVariables().Keys.Where(name => !saved.ContainsKey(name)))
                Environment.SetEnvironmentVariable(name, null);
            foreach (var pair in saved)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }

        private void ValidateStraightThrough(string workDir, string log)
        {
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertHasLine(log, PathLine(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run"));
            AssertHasLine(log, PathLine(LogKey.ROUTE_SECOND_PASS_FOLD, @"verify=on"));
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_SURVIVOR_POOL, @"materialized")),
                @"the second pass materialized an all-runs survivor pool");
            // The library-fragment release ran at both of its points (issue #4532).
            foreach (string scope in new[] { LogKey.SCOPE_RESCORE_GAP_FILL, LogKey.SCOPE_RETAINED_SUMMARY })
            {
                Assert.IsTrue(log.Contains(LogKey.COUNT_LIBRARY_FRAGMENTS_RELEASED + @": ") &&
                              log.Contains(@"scope=" + scope), @"no library-fragment release for " + scope);
            }
            // Every run wrote its second-pass FDR sidecar.
            Assert.AreEqual(RUN_NAMES.Length, Directory.GetFiles(workDir, @"*.2nd-pass.fdr_scores.bin").Length);

            string blib = Path.Combine(workDir, BLIB_FILE);
            int precursors = BlibComparer.CountRows(blib, @"RefSpectra");
            Assert.IsTrue(precursors >= MIN_PRECURSORS, string.Format(@"{0} precursors reported", precursors));
            Assert.AreEqual(RUN_NAMES.Length, BlibComparer.CountRows(blib, @"SpectrumSourceFiles"));
            Assert.AreEqual(precursors * RUN_NAMES.Length, BlibComparer.CountRows(blib, @"RetentionTimes"));
            Assert.AreEqual(precursors, BlibComparer.CountRows(blib, @"OspreyExperimentScores"));
            Assert.IsTrue(BlibComparer.CountRows(blib, @"OspreyPeakBoundaries") >= precursors);
            Assert.IsTrue(BlibComparer.CountRows(blib, @"Proteins") > 0);

            AssertRecoversFullRun(Path.Combine(_dataDir, MANIFEST_FILE), blib, MIN_RECOVERED_FRACTION);
        }

        /// <summary>
        /// The reported precursors are mostly the ones the full 3-file regression run detected in
        /// this rectangle, and few of the ones it did not.
        /// </summary>
        private static void AssertRecoversFullRun(string manifestPath, string blib, double minRecoveredFraction)
        {
            var manifest = ReadManifest(manifestPath);
            var reported = new HashSet<string>(ReadPrecursorKeys(blib));
            int detected = manifest.Count(p => p.Value);
            int recovered = manifest.Count(p => p.Value && reported.Contains(p.Key));
            int newlyDetected = manifest.Count(p => !p.Value && reported.Contains(p.Key));
            Assert.IsTrue(recovered >= minRecoveredFraction * detected,
                string.Format(@"recovered {0} of {1} full-run detections", recovered, detected));
            Assert.IsTrue(newlyDetected <= MAX_NEWLY_DETECTED_FRACTION * (manifest.Count - detected),
                string.Format(@"{0} precursors the full run did not detect were reported", newlyDetected));
            Assert.AreEqual(0, reported.Count(key => !manifest.ContainsKey(key)),
                @"a reported precursor is not in the library");
        }

        /// <summary>
        /// Run <paramref name="data"/> straight through, then as the four-task chain, and assert
        /// the chain reproduced the straight run's library and FDR sidecars.
        /// </summary>
        private void AssertHpcChainMatchesStraight(Subset data)
        {
            // The straight run verifies the second pass's worker answers and the chain folds them
            // unverified, so the two legs cover different second-pass paths.
            string straightDir = CreateDir(@"straight");
            string straightLog = RunOsprey(data.AnalysisArgs(straightDir), Verifier(true));
            string straightBlib = Path.Combine(straightDir, BLIB_FILE);

            // Phase 1, one worker per run: score it from its mzML.
            foreach (string run in data.Runs)
            {
                string phaseDir = CreateDir(@"phase1_" + run);
                File.Copy(Path.Combine(data.DataDir, run + MZML_EXTENSION), Path.Combine(phaseDir, run + MZML_EXTENSION));
                RunTask(data, phaseDir, PerFileScoringTask.TASK_NAME, run);
            }

            // Phase 2, one node over every run: first-pass FDR from the scores and calibrations.
            string phase2Dir = CreateDir(@"phase2");
            foreach (string run in data.Runs)
            {
                ShipFiles(@"phase1_" + run, phase2Dir, run + @".scores.parquet", run + @".calibration.json");
            }
            RunTask(data, phase2Dir, FirstPassFdrTask.TASK_NAME, data.Runs);

            // Phase 3, one worker per run: rescore with the first pass's answer.
            foreach (string run in data.Runs)
            {
                string phaseDir = CreateDir(@"phase3_" + run);
                ShipFiles(@"phase1_" + run, phaseDir, run + SPECTRA_CACHE_EXTENSION, run + @".scores.parquet",
                    run + @".calibration.json");
                ShipFiles(@"phase2", phaseDir, run + @".1st-pass.fdr_scores.bin", run + @".reconciliation.json",
                    @"output.1st-pass.retained_base_ids.bin");
                ShipFilesIfPresent(@"phase2", phaseDir, run + @".1st-pass.model.json", run + @".1st-pass.stratum.json",
                    @"output.1st-pass.fdr_experiment.bin", @"output.1st-pass.model-diagnostics.json");
                string log = RunTask(data, phaseDir, PerFileRescoreTask.TASK_NAME, run);
                AssertHasLine(log, PathLine(LogKey.ROUTE_RESCORE_HYDRATE, @"per-run"));
            }

            // Phase 4, one node over every run: second-pass FDR and the library.
            string phase4Dir = CreateDir(@"phase4");
            foreach (string run in data.Runs)
            {
                string phase3 = @"phase3_" + run;
                ShipFiles(phase3, phase4Dir, run + @".scores-reconciled.parquet", run + @".calibration.json",
                    run + @".reconciliation.json");
                ShipFilesIfPresent(phase3, phase4Dir, run + @".1st-pass.model.json", run + @".1st-pass.stratum.json");
                string pass2Scores = run + @".2nd-pass.fdr_scores.bin";
                string pass2Decoys = run + @".2nd-pass.fdr_decoys.bin";
                if (File.Exists(Path.Combine(_testDir, phase3, pass2Scores)))
                {
                    ShipFiles(phase3, phase4Dir, pass2Scores, pass2Decoys,
                        Path.GetFileName(TaskValiditySidecar.PathFor(pass2Scores, PerFileRescoreTask.TASK_NAME)),
                        Path.GetFileName(TaskValiditySidecar.PathFor(pass2Decoys, PerFileRescoreTask.TASK_NAME)));
                }
                else
                {
                    ShipFiles(phase3, phase4Dir, run + @".1st-pass.fdr_scores.bin");
                }
            }
            ShipFiles(@"phase2", phase4Dir, @"output.1st-pass.retained_base_ids.bin");
            ShipFilesIfPresent(@"phase2", phase4Dir, @"output.1st-pass.fdr_experiment.bin",
                @"output.1st-pass.model-diagnostics.json");
            string phase4Log = RunTask(data, phase4Dir, SecondPassFdrTask.TASK_NAME, data.Runs);
            AssertHasLine(phase4Log, PathLine(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run"));
            // Every run's per-file worker answer was folded, none recomputed here.
            var fold = new Regex(Regex.Escape(PathLine(LogKey.ROUTE_SECOND_PASS_FOLD, string.Empty)) +
                                 @"verify=\w+ answered=(\d+)/\1\b");
            Assert.IsTrue(fold.IsMatch(phase4Log), @"second pass did not fold every worker answer" +
                                                   Environment.NewLine + phase4Log);
            // The verifier split: if the variable stopped reaching a run, both legs would take
            // the same path and every comparison below would still pass.
            string verified = PathLine(LogKey.ROUTE_SECOND_PASS_FOLD, @"verify=on ");
            AssertHasLine(straightLog, verified);
            Assert.IsFalse(HasLine(phase4Log, verified), @"the chain verified its worker answers" +
                                                         Environment.NewLine + phase4Log);

            AssertBlibsEqual(straightBlib, Path.Combine(phase4Dir, BLIB_FILE));
            // The library carries no per-entry score or protein q-value, so a route that writes
            // different values into every sidecar still produces the same library (#4553). The
            // first-pass sidecars are compared where the phase-3 workers read them.
            foreach (string run in data.Runs)
            {
                AssertFdrSidecarsEqual(straightDir, Path.Combine(_testDir, @"phase3_" + run),
                    FdrScoresSidecar.Pass.FirstPass, run);
            }
            AssertFdrSidecarsEqual(straightDir, phase4Dir, FdrScoresSidecar.Pass.SecondPass, data.Runs);
            // One experiment-scope sidecar per pass, written in entry_id order by both routes, so
            // the files must be byte-identical.
            AssertFilesEqual(straightDir, phase2Dir, ExperimentSidecarName(FdrScoresSidecar.Pass.FirstPass));
            AssertFilesEqual(straightDir, phase4Dir, ExperimentSidecarName(FdrScoresSidecar.Pass.SecondPass));
        }

        /// <summary>
        /// Run the straight-through analysis the regression runs on Stellar, with every derived
        /// artifact and cache in <paramref name="workDir"/>, and return what it wrote.
        /// </summary>
        private string RunAnalysis(string workDir, IEnumerable<string> inputs,
            IReadOnlyDictionary<string, string> variables, params string[] extraArgs)
        {
            return RunAnalysis(workDir, inputs, LIBRARY_FILE, variables, extraArgs);
        }

        /// <summary>
        /// The same, searching the library <paramref name="libraryFile"/> from the test data.
        /// </summary>
        private string RunAnalysis(string workDir, IEnumerable<string> inputs, string libraryFile,
            IReadOnlyDictionary<string, string> variables, params string[] extraArgs)
        {
            var args = InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, libraryFile),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
            }).Concat(CommonArgs()).Concat(extraArgs);
            return RunOsprey(args.ToArray(), variables);
        }

        /// <summary>
        /// Run one HPC task in <paramref name="phaseDir"/>, which holds everything the task reads
        /// except the library, the way a worker node receives its inputs: no --work-dir, inputs
        /// and output named in the phase directory.
        /// </summary>
        private static string RunTask(Subset data, string phaseDir, string taskName, params string[] runs)
        {
            string library = Path.Combine(phaseDir, data.LibraryFile);
            File.Copy(Path.Combine(data.DataDir, data.LibraryFile), library);
            var args = new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, taskName }
                .Concat(InputArgs(RunNames(phaseDir, MZML_EXTENSION, runs)))
                .Concat(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(phaseDir, BLIB_FILE)
                }).Concat(CommonArgs(data.Resolution));
            return RunOsprey(args.ToArray(), Verifier(false));
        }

        /// <summary>
        /// Run a command line that must succeed, as the regression's exit check has it: exit 0, no
        /// "Error:" line, and no line saying the exit code had to be reconciled with the log.
        /// </summary>
        private static string RunOsprey(string[] args, IReadOnlyDictionary<string, string> variables)
        {
            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(variables))
            {
                exitCode = InProcessOsprey.Run(args, out output);
            }
            string message = string.Format(@"Command line: {0}{1}Output:{1}{2}",
                string.Join(@" ", args), Environment.NewLine, output);
            Assert.AreEqual(Program.EXIT_CODE_SUCCESS, exitCode, message);
            Assert.IsFalse(SplitLines(output).Any(CommandStatusWriter.IsErrorLine), message);
            Assert.IsFalse(HasLine(output, PathLine(LogKey.ROUTE_EXIT_RECONCILED, string.Empty)), message);
            return output;
        }

        private static IEnumerable<string> CommonArgs()
        {
            return CommonArgs(@"unit");
        }

        private static IEnumerable<string> CommonArgs(string resolution)
        {
            return new[]
            {
                OspreyCommandArgs.ARG_RESOLUTION.ArgumentText, resolution,
                OspreyCommandArgs.ARG_PROTEIN_FDR.ArgumentText, @"0.01",
                OspreyCommandArgs.ARG_THREADS.ArgumentText, @"4",
                OspreyCommandArgs.ARG_PERF_STATS.ArgumentText
            };
        }

        private static IEnumerable<string> InputArgs(IEnumerable<string> inputs)
        {
            return inputs.SelectMany(input => new[] { OspreyCommandArgs.ARG_INPUT.ArgumentText, input });
        }

        /// <summary>
        /// The second-pass worker verifier, set or blanked so a value exported in the developer's
        /// shell cannot change what a leg runs.
        /// </summary>
        private static IReadOnlyDictionary<string, string> Verifier(bool on)
        {
            return new Dictionary<string, string> { { @"OSPREY_PASS2_VERIFY_WORKER", on ? @"1" : string.Empty } };
        }

        private IEnumerable<string> DataInputs()
        {
            return RunNames(_dataDir, MZML_EXTENSION);
        }

        private static IEnumerable<string> RunNames(string dir, string extension, params string[] runs)
        {
            return (runs.Length > 0 ? runs : RUN_NAMES).Select(run => Path.Combine(dir, run + extension));
        }

        private string ExtractAstral()
        {
            string dataDir = Path.Combine(_testDir, @"astral-data");
            ZipFile.ExtractToDirectory(Path.Combine(AppContext.BaseDirectory, @"TestData", ASTRAL_ZIP), dataDir);
            return dataDir;
        }

        private string CreateDir(string name)
        {
            return Directory.CreateDirectory(Path.Combine(_testDir, name)).FullName;
        }

        private void ShipFiles(string fromDir, string toDir, params string[] fileNames)
        {
            foreach (string fileName in fileNames)
                File.Copy(Path.Combine(_testDir, fromDir, fileName), Path.Combine(toDir, fileName));
        }

        private void ShipFilesIfPresent(string fromDir, string toDir, params string[] fileNames)
        {
            ShipFiles(fromDir, toDir, fileNames.Where(f => File.Exists(Path.Combine(_testDir, fromDir, f))).ToArray());
        }

        private static void DeleteSecondPassOutputs(string blib)
        {
            string stamp = TaskValiditySidecar.PathFor(blib, SecondPassFdrTask.TASK_NAME);
            Assert.IsTrue(File.Exists(blib) && File.Exists(stamp), @"no second-pass outputs to invalidate");
            File.Delete(blib);
            File.Delete(stamp);
        }

        /// <summary>
        /// Delete <paramref name="run"/>'s second-pass sidecars and their stamps, and with
        /// <paramref name="withReconciledParquet"/> its reconciled parquet too: a rescore that
        /// never reached the run, or one that died between its two products.
        /// </summary>
        private static void CutRescore(string workDir, string run, bool withReconciledParquet)
        {
            var products = new List<string>
            {
                run + @".2nd-pass.fdr_scores.bin", run + @".2nd-pass.fdr_decoys.bin"
            };
            if (withReconciledParquet)
                products.Add(run + @".scores-reconciled.parquet");
            foreach (string product in products)
            {
                string path = Path.Combine(workDir, product);
                Assert.IsTrue(File.Exists(path), @"no rescore product to cut: " + path);
                File.Delete(path);
                File.Delete(TaskValiditySidecar.PathFor(path, PerFileRescoreTask.TASK_NAME));
            }
        }

        /// <summary>
        /// Exactly <paramref name="count"/> runs were re-scored from spectra.
        /// </summary>
        private static void AssertRescoredOnly(string log, int count)
        {
            Assert.AreEqual(count, SplitLines(log).Count(line =>
                    line.StartsWith(PathLine(LogKey.ROUTE_RESCORE_FILE, string.Empty), StringComparison.Ordinal)),
                @"re-scored runs" + Environment.NewLine + log);
        }

        private static void DeleteFiles(string dir, string pattern)
        {
            var files = Directory.GetFiles(dir, pattern);
            Assert.AreNotEqual(0, files.Length, @"nothing to invalidate matched " + pattern);
            foreach (string file in files)
                File.Delete(file);
        }

        /// <summary>
        /// Each task in <paramref name="skipped"/> reported its outputs valid and each in
        /// <paramref name="ran"/> started; none of either set went unmentioned.
        /// </summary>
        private static void AssertTasks(string log, IEnumerable<string> skipped, IEnumerable<string> ran)
        {
            foreach (string task in skipped)
            {
                Assert.IsTrue(HasLine(log, TaskLine(task, @"skipping")), task + @" did not skip" + Environment.NewLine + log);
                Assert.IsFalse(HasLine(log, TaskLine(task, @"starting")), task + @" ran" + Environment.NewLine + log);
            }
            foreach (string task in ran)
                Assert.IsTrue(HasLine(log, TaskLine(task, @"starting")), task + @" did not run" + Environment.NewLine + log);
        }

        /// <summary>
        /// Nothing was scored or re-scored from spectra.
        /// </summary>
        private static void AssertNoRecompute(string log)
        {
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_SCORE_FILE, string.Empty)), log);
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_RESCORE_FILE, string.Empty)), log);
        }

        private static void AssertBlibsEqual(string expectedBlib, string actualBlib)
        {
            var differences = BlibComparer.Compare(expectedBlib, actualBlib, TOLERANCE);
            Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences.Take(20)));
        }

        /// <summary>
        /// Each run's FDR score sidecar for <paramref name="pass"/> in <paramref name="actualDir"/>
        /// holds the same records as the straight-through run's, matched by entry_id, every value
        /// within <see cref="TOLERANCE"/>.
        /// </summary>
        private static void AssertFdrSidecarsEqual(string expectedDir, string actualDir,
            FdrScoresSidecar.Pass pass, params string[] runs)
        {
            foreach (string run in runs)
            {
                string fileName = run + @"." + FdrScoresSidecar.PassLabel(pass) + FdrScoresSidecar.EXT;
                var expected = ReadFdrSidecar(Path.Combine(expectedDir, fileName), pass);
                var actual = ReadFdrSidecar(Path.Combine(actualDir, fileName), pass);
                // Agreeing on no records is not agreement.
                Assert.AreNotEqual(0, expected.Count, fileName + @" has no records");
                Assert.AreEqual(expected.Count, actual.Count, fileName + @" record count");
                foreach (var e in expected.Values)
                {
                    Assert.IsTrue(actual.TryGetValue(e.EntryId, out var a),
                        string.Format(@"{0}: entry {1} missing", fileName, e.EntryId));
                    string message = string.Format(@"{0}: entry {1}", fileName, e.EntryId);
                    Assert.AreEqual(e.Score, a.Score, TOLERANCE, message + @" score");
                    Assert.AreEqual(e.RunPrecursorQvalue, a.RunPrecursorQvalue, TOLERANCE, message + @" precursor q");
                    Assert.AreEqual(e.RunPeptideQvalue, a.RunPeptideQvalue, TOLERANCE, message + @" peptide q");
                    Assert.AreEqual(e.ApexRt, a.ApexRt, TOLERANCE, message + @" apex RT");
                }
            }
        }

        /// <summary>
        /// A sidecar's records by entry_id, which must be unique: a repeat would let the last
        /// record hide a difference in an earlier one.
        /// </summary>
        private static Dictionary<uint, FdrScoreRecord> ReadFdrSidecar(string path, FdrScoresSidecar.Pass pass)
        {
            // Collected before asserting, because the reader reports any exception its callback
            // throws as an unreadable file.
            var records = new List<FdrScoreRecord>();
            Assert.IsTrue(FdrScoresSidecar.ReadRecords(path, pass, records.Add), @"unreadable " + path);
            var byEntryId = new Dictionary<uint, FdrScoreRecord>();
            foreach (var record in records)
            {
                Assert.IsFalse(byEntryId.ContainsKey(record.EntryId),
                    string.Format(@"{0}: entry {1} repeated", path, record.EntryId));
                byEntryId.Add(record.EntryId, record);
            }
            return byEntryId;
        }

        private static string ExperimentSidecarName(FdrScoresSidecar.Pass pass)
        {
            return Path.GetFileNameWithoutExtension(BLIB_FILE) + @"." + FdrScoresSidecar.PassLabel(pass) +
                   FdrExperimentSidecar.EXT;
        }

        private static void AssertFilesEqual(string expectedDir, string actualDir, string fileName)
        {
            string expected = Path.Combine(expectedDir, fileName);
            string actual = Path.Combine(actualDir, fileName);
            Assert.IsTrue(File.Exists(expected), @"missing " + expected);
            Assert.IsTrue(File.Exists(actual), @"missing " + actual);
            CollectionAssert.AreEqual(File.ReadAllBytes(expected), File.ReadAllBytes(actual),
                fileName + @" differs between routes");
        }

        /// <summary>
        /// The file exists and has content past its first line.
        /// </summary>
        private static void AssertHasRows(string path)
        {
            Assert.IsTrue(File.Exists(path), @"missing " + path);
            Assert.IsTrue(File.ReadLines(path).Skip(1).Any(), @"no rows in " + path);
        }

        private static void AssertHasLine(string log, string text)
        {
            Assert.IsTrue(HasLine(log, text), @"missing " + text + Environment.NewLine + log);
        }

        private static bool HasLine(string log, string text)
        {
            return log.IndexOf(text, StringComparison.Ordinal) >= 0;
        }

        private static string PathLine(string key, string value)
        {
            return @"[" + LogTag.PATH + @"] " + key + @": " + value;
        }

        private static string TaskLine(string task, string state)
        {
            return @"[" + LogTag.TASK + @"] " + task + @":" + state;
        }

        private static IEnumerable<string> SplitLines(string text)
        {
            return text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        }

        private static byte[] HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
            {
                return sha.ComputeHash(stream);
            }
        }

        /// <summary>
        /// The library's precursors as "SEQUENCE|charge" keys, with whether the full 3-file run
        /// detected each one.
        /// </summary>
        private static Dictionary<string, bool> ReadManifest(string manifestPath)
        {
            var manifest = new Dictionary<string, bool>();
            foreach (string line in File.ReadLines(manifestPath).Skip(1))
            {
                var fields = line.Split('\t');
                manifest[PrecursorKey(DiannTsvLoader.StripModifications(fields[0]), fields[1])] = fields[2] == @"1";
            }
            return manifest;
        }

        private static IEnumerable<string> ReadPrecursorKeys(string blib)
        {
            using (var conn = new SQLiteConnection(
                       @"Data Source=" + blib + @";Version=3;Read Only=True;Pooling=False;"))
            {
                conn.Open();
                using (var cmd = new SQLiteCommand(
                           @"SELECT peptideSeq, precursorCharge FROM RefSpectra", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    var keys = new List<string>();
                    while (reader.Read())
                    {
                        keys.Add(PrecursorKey(reader.GetString(0),
                            reader.GetInt32(1).ToString(CultureInfo.InvariantCulture)));
                    }
                    return keys;
                }
            }
        }

        private static string PrecursorKey(string sequence, string charge)
        {
            return sequence + @"|" + charge;
        }

        /// <summary>
        /// One extracted subset: where its files are, the library to search, the resolution its
        /// data needs and its runs.
        /// </summary>
        private sealed class Subset
        {
            public Subset(string dataDir, string libraryFile, string resolution, string[] runs)
            {
                DataDir = dataDir;
                LibraryFile = libraryFile;
                Resolution = resolution;
                Runs = runs;
            }

            public string DataDir { get; }
            public string LibraryFile { get; }
            public string Resolution { get; }
            public string[] Runs { get; }

            /// <summary>
            /// The straight-through analysis of every run, with every derived artifact and cache
            /// in <paramref name="workDir"/>.
            /// </summary>
            public string[] AnalysisArgs(string workDir)
            {
                return InputArgs(RunNames(DataDir, MZML_EXTENSION, Runs)).Concat(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(DataDir, LibraryFile),
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                    OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
                }).Concat(CommonArgs(Resolution)).ToArray();
            }
        }
    }
}
