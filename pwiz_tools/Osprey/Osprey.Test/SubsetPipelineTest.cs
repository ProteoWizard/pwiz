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
using Newtonsoft.Json.Linq;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;
using pwiz.Osprey.Tasks.ModelDiagnostics;

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
    /// the straight-through library at 1e-9 and the chain's FDR sidecars too. The data is not a scientific
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
        private const string MZML_EXTENSION = SpectrumFileReader.EXT_MZML;
        private const string SPECTRA_CACHE_EXTENSION = SpectraCache.EXT;
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
        // A .blib of the subset library reports within a few precursors of the .tsv it came from
        // (the .blib stores intensities as float and its peaks are typed from m/z).
        private const double MIN_BLIB_LIBRARY_FRACTION = 0.9;
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
            string straightLog = RunAnalysis(straightDir, DataInputs(), Verifier(true));
            string log = straightLog;
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
            log = RunAnalysis(straightDir, RunNames(straightDir, MZML_EXTENSION), Verifier(true));
            AssertTasks(log, new[] { PerFileScoringTask.TASK_NAME, PerFileRescoreTask.TASK_NAME },
                new[] { FirstPassFdrTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertNoRecompute(log);
            AssertHasLine(log, PathLine(LogKey.ROUTE_INPUT_SOURCE, @"spectra-cache"));
            AssertBlibsEqual(coldBlib, blib);

            // Mode 5: invalidate only the second pass, so its bundle is rebuilt from the first
            // pass's own sidecars.
            DeleteSecondPassOutputs(blib);
            log = RunAnalysis(straightDir, DataInputs(), Verifier(true));
            AssertTasks(log,
                new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME, PerFileRescoreTask.TASK_NAME },
                new[] { SecondPassFdrTask.TASK_NAME });
            AssertNoRecompute(log);
            Assert.IsTrue(HasLine(log, PathLine(LogKey.ROUTE_FIRST_PASS_FDR, @"own-bundle-stream")) ||
                          HasLine(log, PathLine(LogKey.ROUTE_FIRST_PASS_FDR, @"survivor-loader-only")),
                @"rehydrate did not enter the first pass's own-bundle loader" + Environment.NewLine + log);
            AssertBlibsEqual(coldBlib, blib);
            // The rehydrate is the run an operator reaches for after running out of memory, so it
            // must keep both savings: Stage 7 folds one run at a time, and the library fragments
            // are released, against the summary the straight-through run wrote (#4650).
            AssertStreamedJoin(log);
            AssertFragmentRelease(log, straightLog);
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
            AssertHasRows(Path.Combine(workDir, OutputArtifact(OspreyReportWriter.EXT_PROTEIN_GROUPS)));
            AssertHasRows(Path.Combine(workDir, OutputArtifact(OspreyReportWriter.EXT_STATS)));
            string diagnosticsReport = Path.Combine(workDir, OutputArtifact(ModelDiagnosticsReport.EXT_HTML));
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
            var args = LibDecoyDiagnosticsArgs();
            RunAnalysis(workDir, DataInputs(), LIBDECOY_FILE, Verifier(true), args);
            string blib = Path.Combine(workDir, BLIB_FILE);
            string uninterruptedBlib = Path.Combine(workDir, @"output_uninterrupted.blib");
            File.Copy(blib, uninterruptedBlib);
            string cutRun = RUN_NAMES[RUN_NAMES.Length - 1];

            // Mode 8: a rescore that stopped before the last run - neither of its products exists.
            // Once, any current second-pass file read as "the rescore is finished", and the
            // remaining runs carried first-pass q-values into the library.
            CutRescore(workDir, cutRun, true);
            DeleteSecondPassOutputs(blib);
            string log = RunAnalysis(workDir, RunNames(workDir, MZML_EXTENSION), LIBDECOY_FILE, Verifier(true), args);
            Assert.AreEqual(RUN_NAMES.Length, Directory.GetFiles(workDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED).Length,
                @"the rescore did not finish the cohort" + Environment.NewLine + log);
            AssertHasLine(log, PathLine(LogKey.ROUTE_RESCORE_RESUME, string.Empty));
            AssertRescoredOnly(log, 1);
            AssertBlibsEqual(uninterruptedBlib, blib);

            // Mode 9: a crash between the two products - the reconciled parquet is written and
            // stamped, the second-pass sidecar is not. The cohort count calls the run outstanding
            // while a per-file check would call it done; it must be re-scored.
            CutRescore(workDir, cutRun, false);
            DeleteSecondPassOutputs(blib);
            log = RunAnalysis(workDir, RunNames(workDir, MZML_EXTENSION), LIBDECOY_FILE, Verifier(true), args);
            AssertRescoredOnly(log, 1);
            AssertBlibsEqual(uninterruptedBlib, blib);
        }

        /// <summary>
        /// The model diagnostics report from a completed analysis without re-running it (the
        /// regression's modes 5, 7 and 11 on its library-decoy dataset). A rehydrated second pass
        /// re-emits the same report; <c>--task ModelDiagnostics</c> rewrites only the report; and
        /// with its products deleted the report is folded from the sidecars - the same report,
        /// with no analysis run - from every entry point that can produce it.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetDiagnosticsWithoutReanalysis()
        {
            string workDir = CreateDir(@"diagnostics");
            var args = LibDecoyDiagnosticsArgs();
            string RunDiagnostics(string taskName, int exitCode = Program.EXIT_CODE_SUCCESS)
            {
                var taskArgs = taskName == null ? args : args.Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, taskName });
                return RunOspreyExpecting(InputArgs(DataInputs()).Concat(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBDECOY_FILE),
                    OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                    OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir
                }).Concat(CommonArgs()).Concat(taskArgs).ToArray(), Verifier(true), exitCode);
            }

            RunDiagnostics(null);
            string blib = Path.Combine(workDir, BLIB_FILE);
            string pass1 = Path.Combine(workDir, OutputArtifact(ModelDiagnosticsReport.EXT_PASS1));
            string pass2 = Path.Combine(workDir, OutputArtifact(ModelDiagnosticsReport.EXT_PASS2));
            string report = Path.Combine(workDir, OutputArtifact(ModelDiagnosticsReport.EXT_HTML));
            string referenceDir = CreateDir(@"diagnostics-reference");
            foreach (string product in new[] { pass1, pass2, report })
            {
                Assert.IsTrue(File.Exists(product), @"the analysis wrote no " + product);
                File.Copy(product, Path.Combine(referenceDir, Path.GetFileName(product)));
            }
            string ReferenceOf(string product) => Path.Combine(referenceDir, Path.GetFileName(product));

            // Mode 5: the rehydrated second pass re-emits the pass-2 product and the report,
            // unchanged. Deleted first: a product left in place would be restamped as current and
            // compared against itself.
            DeleteSecondPassOutputs(blib);
            DeleteDiagnosticsProducts(workDir, pass2, report);
            string log = RunDiagnostics(null);
            AssertNoRecompute(log);
            CollectionAssert.AreEqual(File.ReadAllBytes(ReferenceOf(pass2)), File.ReadAllBytes(pass2),
                @"the rehydrated second pass wrote a different pass-2 diagnostics product");

            // Mode 7: regenerating the report rewrites the report and nothing else.
            var before = FingerprintDir(workDir);
            log = RunDiagnostics(ModelDiagnosticsTask.TASK_NAME);
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_ALL_RUNS_BUNDLE, string.Empty)), log);
            CollectionAssert.AreEqual(new[] { Path.GetFileName(report) }, ChangedFiles(before, workDir),
                @"regeneration changed something other than the report" + Environment.NewLine + log);
            CollectionAssert.AreEqual(File.ReadAllBytes(ReferenceOf(report)), File.ReadAllBytes(report),
                @"the regenerated report differs from the one the analysis wrote");

            // Mode 11: with both products deleted, asking for the report folds it from the
            // sidecars. Re-running the analysis would produce the right report too, which is
            // why the fold markers and the absence of analysis are what is asserted.
            string[] noAnalysis =
            {
                PathLine(LogKey.ROUTE_PRE_COMPACTION_POOL, @"resident"),
                PathLine(LogKey.ROUTE_RESCORE_FILE, string.Empty),
                PathLine(LogKey.ROUTE_SCORED_ENTRIES, @"resident"),
                @"[" + LogTag.STAGE_WALL + @"] second-pass-fdr",
                PathLine(LogKey.ROUTE_PROTEIN_FDR, string.Empty),
                PathLine(LogKey.ROUTE_ALL_RUNS_BUNDLE, string.Empty)
            };
            string foldPass1 = PathLine(LogKey.ROUTE_MODEL_DIAGNOSTICS, @"fold-pass1");
            string foldPass2 = PathLine(LogKey.ROUTE_MODEL_DIAGNOSTICS, @"fold-pass2");
            DeleteDiagnosticsProducts(workDir, pass1, pass2);
            before = FingerprintDir(workDir);
            log = RunDiagnostics(ModelDiagnosticsTask.TASK_NAME);
            AssertLogShape(log, new[] { foldPass1, foldPass2, PathLine(LogKey.ROUTE_FIRST_PASS_FDR, @"survivor-loader-only") },
                noAnalysis);
            CollectionAssert.AreEqual(File.ReadAllBytes(ReferenceOf(pass2)), File.ReadAllBytes(pass2),
                @"the folded pass-2 product is not the same report");
            AssertSameFirstPassProduct(ReferenceOf(pass1), pass1);
            foreach (string changed in ChangedFiles(before, workDir))
            {
                Assert.IsTrue(changed == Path.GetFileName(pass1) || changed == Path.GetFileName(pass2) ||
                              changed == Path.GetFileName(report) || changed.EndsWith(TaskValiditySidecar.EXT, StringComparison.Ordinal),
                    @"the pay-later fold touched an artifact other than the report: " + changed);
            }

            // Every other entry point. The whole pipeline with the products present runs nothing.
            log = RunDiagnostics(null);
            AssertLogShape(log, Array.Empty<string>(), noAnalysis);
            AssertTasks(log, ALL_TASKS, Array.Empty<string>());
            // The whole pipeline with them absent folds both.
            DeleteDiagnosticsProducts(workDir, pass1, pass2);
            log = RunDiagnostics(null);
            AssertLogShape(log, new[] { foldPass1, foldPass2 }, noAnalysis);
            AssertTasks(log, new[] { PerFileScoringTask.TASK_NAME, PerFileRescoreTask.TASK_NAME }, Array.Empty<string>());
            Assert.IsTrue(File.Exists(pass1) && File.Exists(pass2), @"the whole pipeline folded nothing");
            // --task FirstPassFDR folds pass 1.
            DeleteDiagnosticsProducts(workDir, pass1, pass2);
            log = RunDiagnostics(FirstPassFdrTask.TASK_NAME);
            AssertLogShape(log, new[] { foldPass1 }, noAnalysis);
            Assert.IsTrue(File.Exists(pass1), @"--task FirstPassFDR folded nothing");
            // --task SecondPassFDR with no pass-1 product refuses rather than half-producing.
            DeleteDiagnosticsProducts(workDir, pass1, pass2);
            log = RunDiagnostics(SecondPassFdrTask.TASK_NAME, Program.EXIT_CODE_FAILURE_TO_START);
            AssertLogShape(log, new[] { PathLine(LogKey.ROUTE_MODEL_DIAGNOSTICS, @"refused-no-pass1") }, noAnalysis);
            Assert.IsFalse(File.Exists(pass2), @"a pass-2 product exists after a refusal");
            // --task SecondPassFDR with only the pass-2 product missing folds it.
            File.Copy(ReferenceOf(pass1), pass1);
            log = RunDiagnostics(SecondPassFdrTask.TASK_NAME);
            AssertLogShape(log, new[] { foldPass2 }, noAnalysis);
            Assert.IsTrue(File.Exists(pass2), @"--task SecondPassFDR folded nothing");
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
            // And it leaves the files the default leaves: transfer once completed without writing
            // the experiment-scope sidecar every other mode writes, unseen because no test ran it.
            Assert.IsTrue(File.Exists(Path.Combine(transferDir, ExperimentSidecarName(FdrScoresSidecar.Pass.SecondPass))),
                @"the transfer arm wrote no second-pass experiment sidecar");
            foreach (string run in RUN_NAMES)
            {
                string sidecar = Path.Combine(transferDir, PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, FdrScoresSidecar.EXT));
                Assert.IsTrue(File.Exists(sidecar), @"the transfer arm wrote no second-pass sidecar: " + sidecar);
            }

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
        /// recomputing the target's b/y fragments on the reversed sequence, so a fragment of
        /// another type is copied verbatim and one with no fragment number is dropped. A library
        /// with no fragment number column must stop with one plain error; a library with a few
        /// precursors of copied-only fragments must warn and still finish. A fragment number the
        /// library HAS but states as 0 is not "missing": the loader refuses the library, naming
        /// the lines. (A .blib states no fragment types at all, but Osprey types its peaks from
        /// m/z, so its decoys are usable - see the blib library tests.)
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetUnusableDecoys()
        {
            // No fragment number column, so every decoy fragment is dropped.
            string noNumbers = WriteLibraryWithoutColumn(@"no-fragment-numbers.tsv", @"FragmentNumber");
            int libraryPrecursors = CountLibraryPrecursors(Path.Combine(_dataDir, LIBRARY_FILE));
            string output = RunExpectingRefusal(@"no-fragment-numbers", noNumbers);
            StringAssert.Contains(output, RefusalText(noNumbers, libraryPrecursors));

            // Two precursors whose fragment numbers read 0: the library is invalid, and the load
            // refuses it with every one of their lines counted.
            var strayPeptides = new HashSet<string>(File.ReadLines(Path.Combine(_dataDir, LIBRARY_FILE))
                .Skip(1).Select(line => line.Split('\t')[0]).Distinct().Take(2));
            string zeroNumbers = WriteLibraryWithCell(@"zero-fragment-numbers.tsv", strayPeptides.Contains,
                @"FragmentNumber", @"0");
            int strayLines = File.ReadLines(Path.Combine(_dataDir, LIBRARY_FILE))
                .Skip(1).Count(line => strayPeptides.Contains(line.Split('\t')[0]));
            output = RunExpectingRefusal(@"zero-fragment-numbers", zeroNumbers);
            StringAssert.Contains(output, string.Format(
                OspreyIOResources.DiannTsvLoader_ToException__0__library_lines_have_errors__Fix_the_library_and_load_it_again_, strayLines));

            // Two precursors of a-ion fragments, which a decoy copies verbatim: a warning naming
            // the count, and a search.
            string strays = WriteLibraryWithCell(@"two-stray.tsv", strayPeptides.Contains, @"FragmentType", @"a");
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
            // The search finishes and reports. Not at the usual depth: those two decoys are
            // identical to their targets, so they score as well as real detections, and on a
            // subset reporting ~177 precursors two top-scoring decoys alone are over 1% FDR.
            int strayPrecursors = BlibComparer.CountRows(Path.Combine(strayDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(strayPrecursors > 0, string.Format(@"{0} precursors reported", strayPrecursors));
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
        /// A copy of the subset library with column <paramref name="columnName"/> set to
        /// <paramref name="value"/> on every row of the precursors <paramref name="select"/>
        /// selects by modified peptide.
        /// </summary>
        private string WriteLibraryWithCell(string fileName, Func<string, bool> select, string columnName, string value)
        {
            return WriteLibraryCopy(fileName, columnName, (fields, column) =>
            {
                if (!select(fields[0]))
                    return fields;
                fields[column] = value;
                return fields;
            });
        }

        /// <summary>A copy of the subset library without column <paramref name="columnName"/>.</summary>
        private string WriteLibraryWithoutColumn(string fileName, string columnName)
        {
            return WriteLibraryCopy(fileName, columnName,
                (fields, column) => fields.Where((_, i) => i != column).ToArray(), true);
        }

        /// <summary>
        /// A copy of the subset library with each data row passed through
        /// <paramref name="rewrite"/> (given the index of <paramref name="columnName"/>), and the
        /// header too when <paramref name="rewriteHeader"/>.
        /// </summary>
        private string WriteLibraryCopy(string fileName, string columnName,
            Func<string[], int, string[]> rewrite, bool rewriteHeader = false)
        {
            var lines = File.ReadAllLines(Path.Combine(_dataDir, LIBRARY_FILE));
            int column = Array.IndexOf(lines[0].Split('\t'), columnName);
            Assert.AreNotEqual(-1, column);
            for (int i = rewriteHeader ? 0 : 1; i < lines.Length; i++)
                lines[i] = string.Join('\t', rewrite(lines[i].Split('\t'), column));
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

        /// <summary>
        /// The BLIB libraries Osprey writes, searched as libraries. <c>--export-library</c> writes the
        /// subset library as a .blib with no <c>RefSpectraPeakAnnotations</c> rows, as BiblioSpec
        /// writes one; Osprey types every peak from m/z, so searching it builds real decoys and
        /// reports what the .tsv search does. The output .blib of that search searches back with
        /// decoys of its own.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetBlibLibrary()
        {
            string tsvDir = CreateDir(@"tsv-library");
            RunAnalysis(tsvDir, DataInputs(), Verifier(false));
            string tsvOutput = Path.Combine(tsvDir, BLIB_FILE);
            // Peaks stored in m/z order, as BiblioSpec stores them, whatever the library order.
            foreach (var spectrum in new BlibLoader(FragmentToleranceConfig.UnitResolution(0.5)).Load(tsvOutput))
            {
                for (int i = 1; i < spectrum.Fragments.Count; i++)
                    Assert.IsTrue(spectrum.Fragments[i - 1].Mz <= spectrum.Fragments[i].Mz, spectrum.ModifiedSequence);
            }

            string library = Path.Combine(_dataDir, LIBRARY_FILE);
            string exported = Path.Combine(_testDir, @"subset-library.blib");
            // A reused search command line: the export reads only the library, so an input that
            // has moved does not refuse it.
            string log = RunOsprey(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                OspreyCommandArgs.ARG_EXPORT_LIBRARY.ArgumentText, exported,
                OspreyCommandArgs.ARG_INPUT.ShortArgumentText, Path.Combine(_testDir, @"moved.mzML")
            }, Verifier(false));
            int libraryPrecursors = CountLibraryPrecursors(library);
            StringAssert.Contains(log, CountText.Format(libraryPrecursors,
                OspreyResources.Program_RunExportLibrary_Saved_1_library_precursor_to__1_,
                OspreyResources.Program_RunExportLibrary_Saved__0_N0__library_precursors_to__1_,
                exported));
            Assert.AreEqual(libraryPrecursors, BlibComparer.CountRows(exported, @"RefSpectra"));
            Assert.AreEqual(0, BlibComparer.CountWhere(exported, @"RefSpectraPeakAnnotations", @"1"));
            AssertExportOverLibraryRefused(library);

            // Osprey's own typing reproduces every type the subset library states - the isobars
            // too: b2 and b4^2 of IQQLTEEIGR share one m/z, and the library lists both as two
            // peaks, which the lower-charge preference and one peak per ion type as it does.
            var tsvLoader = new DiannTsvLoader(FragmentToleranceConfig.UnitResolution(0.5));
            tsvLoader.Load(library);
            Assert.AreEqual(tsvLoader.TypeCheck.StatedPrimary, tsvLoader.TypeCheck.Agree,
                string.Join(Environment.NewLine, tsvLoader.TypeCheck.Examples));

            // So searching the .blib, which states no types, reports what the .tsv search does.
            string blibDir = CreateDir(@"blib-library");
            RunAnalysis(blibDir, DataInputs(), exported, Verifier(false));
            AssertSameSearch(tsvOutput, Path.Combine(blibDir, BLIB_FILE));

            string backDir = CreateDir(@"output-blib-library");
            RunAnalysis(backDir, DataInputs(), tsvOutput, Verifier(false));
            int tsvPrecursors = BlibComparer.CountRows(tsvOutput, @"RefSpectra");
            int backPrecursors = BlibComparer.CountRows(Path.Combine(backDir, BLIB_FILE), @"RefSpectra");
            Assert.IsTrue(backPrecursors >= MIN_BLIB_LIBRARY_FRACTION * tsvPrecursors,
                string.Format(@"{0} precursors from the output .blib, {1} in it", backPrecursors, tsvPrecursors));
        }

        /// <summary>
        /// An export onto the library it reads is refused before anything is written, and the
        /// library is left as it was.
        /// </summary>
        private static void AssertExportOverLibraryRefused(string library)
        {
            byte[] before = HashFile(library);
            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(Verifier(false)))
            {
                exitCode = InProcessOsprey.Run(new[]
                {
                    OspreyCommandArgs.ARG_LIBRARY.ArgumentText, library,
                    OspreyCommandArgs.ARG_EXPORT_LIBRARY.ArgumentText, library
                }, out output);
            }
            Assert.AreEqual(Program.EXIT_CODE_FAILURE_TO_START, exitCode, output);
            StringAssert.Contains(output, string.Format(OspreyResources.Program_ValidateArgs_The__0__path_is_the_library_it_reads___1_,
                OspreyCommandArgs.ARG_EXPORT_LIBRARY.ArgumentText, library));
            CollectionAssert.AreEqual(before, HashFile(library));
        }

        /// <summary>
        /// Two searches of one library in different formats report the same thing: every table,
        /// peaks included, agrees at <see cref="TOLERANCE"/> but the library each names as its
        /// source.
        /// </summary>
        private static void AssertSameSearch(string expectedBlib, string actualBlib)
        {
            var differences = BlibComparer.Compare(expectedBlib, actualBlib, TOLERANCE, @"SpectrumSourceFiles");
            Assert.AreEqual(0, differences.Count, string.Join(Environment.NewLine, differences.Take(20)));
        }

        private void ValidateStraightThrough(string workDir, string log)
        {
            AssertTasks(log, Array.Empty<string>(), ALL_TASKS);
            AssertHasLine(log, PathLine(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run"));
            AssertHasLine(log, PathLine(LogKey.ROUTE_SECOND_PASS_FOLD, @"verify=on"));
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_SURVIVOR_POOL, string.Empty)),
                @"the second pass materialized an all-runs survivor pool");
            // The library-fragment release ran at both of its points (issue #4532).
            foreach (string scope in new[] { LogKey.SCOPE_RESCORE_GAP_FILL, LogKey.SCOPE_RETAINED_SUMMARY })
            {
                Assert.IsTrue(log.Contains(LogKey.COUNT_LIBRARY_FRAGMENTS_RELEASED + @": ") &&
                              log.Contains(@"scope=" + scope), @"no library-fragment release for " + scope);
            }
            // Every run wrote its second-pass FDR sidecar.
            Assert.AreEqual(RUN_NAMES.Length, Directory.GetFiles(workDir, @"*." + FdrScoresSidecar.LABEL_SECOND_PASS + FdrScoresSidecar.EXT).Length);

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
                ShipFiles(@"phase1_" + run, phase2Dir, run + ParquetScoreCache.EXT_SCORES, run + CalibrationIO.EXT);
            }
            RunTask(data, phase2Dir, FirstPassFdrTask.TASK_NAME, data.Runs);

            // Phase 3, one worker per run: rescore with the first pass's answer.
            foreach (string run in data.Runs)
            {
                string phaseDir = CreateDir(@"phase3_" + run);
                ShipFiles(@"phase1_" + run, phaseDir, run + SPECTRA_CACHE_EXTENSION, run + ParquetScoreCache.EXT_SCORES,
                    run + CalibrationIO.EXT);
                ShipFiles(@"phase2", phaseDir, PassArtifact(run, FdrScoresSidecar.Pass.FirstPass, FdrScoresSidecar.EXT), run + ReconciliationFile.EXT,
                    OutputArtifact(@"." + FdrScoresSidecar.LABEL_FIRST_PASS + RetainedBaseIdSidecar.EXT));
                ShipFilesIfPresent(@"phase2", phaseDir, run + FirstPassModelIO.EXT_MODEL, run + FirstPassModelIO.EXT_STRATUM,
                    ExperimentSidecarName(FdrScoresSidecar.Pass.FirstPass), OutputArtifact(ModelDiagnosticsReport.EXT_PASS1));
                string log = RunTask(data, phaseDir, PerFileRescoreTask.TASK_NAME, run);
                AssertHasLine(log, PathLine(LogKey.ROUTE_RESCORE_HYDRATE, @"per-run"));
            }

            // Phase 4, one node over every run: second-pass FDR and the library.
            string phase4Dir = CreateDir(@"phase4");
            foreach (string run in data.Runs)
            {
                string phase3 = @"phase3_" + run;
                ShipFiles(phase3, phase4Dir, run + ParquetScoreCache.EXT_SCORES_RECONCILED, run + CalibrationIO.EXT,
                    run + ReconciliationFile.EXT);
                ShipFilesIfPresent(phase3, phase4Dir, run + FirstPassModelIO.EXT_MODEL, run + FirstPassModelIO.EXT_STRATUM);
                string pass2Scores = PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, FdrScoresSidecar.EXT);
                string pass2Decoys = PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, Pass2CompetitionDecoys.EXT);
                if (File.Exists(Path.Combine(_testDir, phase3, pass2Scores)))
                {
                    ShipFiles(phase3, phase4Dir, pass2Scores, pass2Decoys,
                        Path.GetFileName(TaskValiditySidecar.PathFor(pass2Scores, PerFileRescoreTask.TASK_NAME)),
                        Path.GetFileName(TaskValiditySidecar.PathFor(pass2Decoys, PerFileRescoreTask.TASK_NAME)));
                }
                else
                {
                    ShipFiles(phase3, phase4Dir, PassArtifact(run, FdrScoresSidecar.Pass.FirstPass, FdrScoresSidecar.EXT));
                }
            }
            ShipFiles(@"phase2", phase4Dir, OutputArtifact(@"." + FdrScoresSidecar.LABEL_FIRST_PASS + RetainedBaseIdSidecar.EXT));
            ShipFilesIfPresent(@"phase2", phase4Dir, ExperimentSidecarName(FdrScoresSidecar.Pass.FirstPass),
                OutputArtifact(ModelDiagnosticsReport.EXT_PASS1));
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
            return RunOspreyExpecting(args, variables, Program.EXIT_CODE_SUCCESS);
        }

        /// <summary>
        /// Run a command line that must exit with <paramref name="expectedExitCode"/>: on success
        /// with no "Error:" line, and either way without reconciling the exit code with the log.
        /// </summary>
        private static string RunOspreyExpecting(string[] args, IReadOnlyDictionary<string, string> variables,
            int expectedExitCode)
        {
            string output;
            int exitCode;
            using (OspreyEnvironment.OverrideVariables(variables))
            {
                exitCode = InProcessOsprey.Run(args, out output);
            }
            string message = string.Format(@"Command line: {0}{1}Output:{1}{2}",
                string.Join(@" ", args), Environment.NewLine, output);
            Assert.AreEqual(expectedExitCode, exitCode, message);
            // Success has no Error: line; a designed refusal has exactly one and no exception,
            // which is what distinguishes it from a crash with the same exit code.
            int expectedErrors = expectedExitCode == Program.EXIT_CODE_SUCCESS ? 0 : 1;
            Assert.AreEqual(expectedErrors, SplitLines(output).Count(CommandStatusWriter.IsErrorLine), message);
            Assert.IsFalse(output.Contains(typeof(Exception).Namespace + @"."), message);
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
        /// The second-pass worker verifier, set or blanked, with the switches that change which
        /// route a run takes blanked too, so a value exported in the developer's shell cannot
        /// change what a leg runs. A resident-pool allowance in particular would admit, silently,
        /// the O(files) paths these legs exist to keep out.
        /// </summary>
        private static IReadOnlyDictionary<string, string> Verifier(bool on)
        {
            return new Dictionary<string, string>
            {
                { @"OSPREY_PASS2_VERIFY_WORKER", on ? @"1" : string.Empty },
                { @"OSPREY_ALLOW_UNFIXED_RESIDENT", string.Empty },
                { @"OSPREY_PASS2_QVALUE", string.Empty }
            };
        }

        private string[] LibDecoyDiagnosticsArgs()
        {
            return new[]
            {
                OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY.ArgumentText,
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, Path.Combine(_dataDir, LIBDECOY_PAIRING_FILE),
                OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText
            };
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
                PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, FdrScoresSidecar.EXT), PassArtifact(run, FdrScoresSidecar.Pass.SecondPass, Pass2CompetitionDecoys.EXT)
            };
            if (withReconciledParquet)
                products.Add(run + ParquetScoreCache.EXT_SCORES_RECONCILED);
            foreach (string product in products)
            {
                string path = Path.Combine(workDir, product);
                Assert.IsTrue(File.Exists(path), @"no rescore product to cut: " + path);
                File.Delete(path);
                File.Delete(TaskValiditySidecar.PathFor(path, PerFileRescoreTask.TASK_NAME));
            }
        }

        /// <summary>
        /// Stage 7 folded the survivors run by run: the per-run source was published and nothing
        /// then pulled the whole pool through it. The output is the same either way.
        /// </summary>
        private static void AssertStreamedJoin(string log)
        {
            AssertHasLine(log, PathLine(LogKey.ROUTE_SECOND_PASS_JOIN, @"per-run"));
            Assert.IsFalse(HasLine(log, PathLine(LogKey.ROUTE_SURVIVOR_POOL, string.Empty)),
                @"a consumer pulled the whole survivor pool through the per-run source" + Environment.NewLine + log);
        }

        /// <summary>
        /// The library-fragment release ran and freed spectra when Stage 6 re-scored, and Stage 7's
        /// release read the retained summary written in <paramref name="summaryLog"/> rather than
        /// rebuilding it (#4650): the same count, Stage 6 retaining no more than it, and nothing
        /// left for Stage 7 to release in the process where Stage 6 already did. The release does
        /// not change the output, so only these counts can show it happened.
        /// </summary>
        private static void AssertFragmentRelease(string log, string summaryLog)
        {
            var rescore = ReadRelease(log, LogKey.SCOPE_RESCORE_GAP_FILL);
            var summary = ReadRelease(log, LogKey.SCOPE_RETAINED_SUMMARY);
            var written = Regex.Match(summaryLog, Regex.Escape(@"[" + LogTag.COUNT + @"] " + LogKey.COUNT_RETAINED_SUMMARY_WRITTEN + @": base-ids=") + @"(\d+)");
            Assert.IsTrue(written.Success, @"no retained summary written" + Environment.NewLine + summaryLog);
            int summaryCount = int.Parse(written.Groups[1].Value, CultureInfo.InvariantCulture);
            Assert.IsTrue(rescore.Released > 0, @"the Stage 6 release freed nothing" + Environment.NewLine + log);
            Assert.AreEqual(summaryCount, summary.Retained, @"Stage 7 did not retain the summary's base_ids");
            Assert.IsTrue(rescore.Retained <= summaryCount, @"Stage 6 retained more than the summary");
            Assert.AreEqual(0, summary.Released, @"Stage 7 released what Stage 6 already had");
        }

        private static (int Released, int Retained) ReadRelease(string log, string scope)
        {
            var match = Regex.Match(log, Regex.Escape(@"[" + LogTag.COUNT + @"] " + LogKey.COUNT_LIBRARY_FRAGMENTS_RELEASED + @": released=") +
                                         @"(\d+) entries=\d+ retained=(\d+) scope=" + Regex.Escape(scope) + @"\b");
            Assert.IsTrue(match.Success, @"no library-fragment release for scope " + scope + Environment.NewLine + log);
            return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// Delete the diagnostics products and any sidecar written beside them.
        /// </summary>
        private static void DeleteDiagnosticsProducts(string dir, params string[] products)
        {
            foreach (string product in products)
            {
                foreach (string file in Directory.GetFiles(dir, Path.GetFileName(product) + @"*"))
                    File.Delete(file);
            }
        }

        /// <summary>
        /// The pass-1 product folded later equals the one written with the analysis, except for
        /// its generation time and the views no fold can rebuild today (each held privately by a
        /// phase that has already exited).
        /// </summary>
        private static void AssertSameFirstPassProduct(string expectedPath, string actualPath)
        {
            string[] notComparable = { @"generatedUtc", @"cal", @"model", @"featureHistEdges", @"featureCount", @"modelComposite" };
            JObject Comparable(string path)
            {
                var json = JObject.Parse(File.ReadAllText(path));
                foreach (string key in notComparable)
                    json.Remove(key);
                return json;
            }
            Assert.IsTrue(JToken.DeepEquals(Comparable(expectedPath), Comparable(actualPath)),
                @"the folded pass-1 product differs outside the views a fold cannot rebuild");
        }

        /// <summary>
        /// Every line in <paramref name="required"/> appears in the log and none in
        /// <paramref name="forbidden"/> does.
        /// </summary>
        private static void AssertLogShape(string log, IEnumerable<string> required, IEnumerable<string> forbidden)
        {
            foreach (string line in required)
                AssertHasLine(log, line);
            foreach (string line in forbidden)
                Assert.IsFalse(HasLine(log, line), @"the analysis ran: " + line + Environment.NewLine + log);
        }

        /// <summary>
        /// Size and last-write time of every file in <paramref name="dir"/>, by name.
        /// </summary>
        private static Dictionary<string, string> FingerprintDir(string dir)
        {
            return Directory.GetFiles(dir).Select(f => new FileInfo(f)).ToDictionary(f => f.Name,
                f => f.Length.ToString(CultureInfo.InvariantCulture) + @":" + f.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>
        /// The names of files added, changed or removed since <paramref name="before"/>, sorted.
        /// </summary>
        private static string[] ChangedFiles(Dictionary<string, string> before, string dir)
        {
            var after = FingerprintDir(dir);
            return after.Where(p => !before.TryGetValue(p.Key, out string value) || value != p.Value).Select(p => p.Key)
                .Concat(before.Keys.Where(name => !after.ContainsKey(name)))
                .OrderBy(name => name, StringComparer.Ordinal).ToArray();
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
                string fileName = PassArtifact(run, pass, FdrScoresSidecar.EXT);
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
            return OutputArtifact(@"." + FdrScoresSidecar.PassLabel(pass) + FdrExperimentSidecar.EXT);
        }

        /// <summary>
        /// An analysis-wide artifact, named from the output library as the product names it.
        /// </summary>
        private static string OutputArtifact(string extension)
        {
            return Path.GetFileNameWithoutExtension(BLIB_FILE) + extension;
        }

        /// <summary>
        /// A per-run artifact of one pass, e.g. the run's second-pass FDR scores.
        /// </summary>
        private static string PassArtifact(string run, FdrScoresSidecar.Pass pass, string extension)
        {
            return run + @"." + FdrScoresSidecar.PassLabel(pass) + extension;
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
