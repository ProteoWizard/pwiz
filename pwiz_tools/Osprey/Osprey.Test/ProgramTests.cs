/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
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
using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Tests for Osprey Program-level helpers: the HPC scoring split
    /// flag validation (Program.ValidateArgs). The --input-scores directory
    /// expansion it also covered went with that flag: every task takes the data
    /// files and derives its parquets from their stems.
    ///
    /// These are unit tests of CLI argument plumbing only. End-to-end
    /// scoring round-trip (Stages 1-4 → parquet → Stage 5+) is exercised
    /// by perf-class tests against the Stellar dataset.
    /// </summary>
    [TestClass]
    public class ProgramTests
    {
        private int _savedMeanBestN;

        /// <summary>
        /// Pin the experiment-wide aggregation off for every test here. ValidateArgs now runs
        /// OspreyEnvironment.ValidateExperimentAggSettings FIRST, before the --task switch, so on a
        /// machine with OSPREY_EXPERIMENT_AGG=mean-best-N exported - the sweep this feature exists
        /// to run - the happy-path cases would fail their Assert.IsNull (their 1-2 input scores are
        /// fewer runs than N), and the negative cases would match the aggregation error instead of
        /// the --task message they assert on. Same ambient-environment hole that FdrTest had.
        /// </summary>
        [TestInitialize]
        public void PinExperimentAggToDefault()
        {
            _savedMeanBestN = OspreyEnvironment.MeanBestN;
            OspreyEnvironment.MeanBestN = 0;
        }

        [TestCleanup]
        public void RestoreExperimentAgg()
        {
            OspreyEnvironment.MeanBestN = _savedMeanBestN;
        }

        // --- ValidateArgs: what each task requires -------------------------

        private static OspreyConfig TaskConfig(HpcTask task)
        {
            // Mirror Main's wiring: ResolveTask -> SelectedTask + derived flags.
            return new OspreyConfig
            {
                SelectedTask = task,
                // The selector IS the request for the report; Main sets this so the run
                // cannot recompute the pass-2 view and then write nothing.
                ModelDiagnostics = task == HpcTask.ModelDiagnostics,
                NoJoin = task == HpcTask.PerFileScoring || task == HpcTask.PerFileRescore,
                StopAfterStage5 = task == HpcTask.FirstPassFdr,
                ExpectReconciledInput = task == HpcTask.SecondPassFdr,
            };
        }

        // - SpectraCache (Stage 1 alone: inputs in, .spectra.bin out) --

        [TestMethod]
        public void TestValidateSpectraCache()
        {
            // Consolidated: the whole SpectraCache contract in one place.
            // The defining difference from every other task is that it needs
            // NO library - caching depends only on the input file - so the
            // happy path below deliberately leaves LibrarySource null.
            var config = TaskConfig(HpcTask.SpectraCache);
            config.InputFiles = new List<string> { "a.raw" };
            Assert.IsNull(Program.ValidateArgs(config), "no library should be required");

            // A library is merely unnecessary, not rejected: staging a dataset
            // with the eventual run's full command line must still work.
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            Assert.IsNull(Program.ValidateArgs(config), "a library should be tolerated");

            AssertSpectraCacheError(c => { }, "--input <file");
        }

        private static void AssertSpectraCacheError(Action<OspreyConfig> mutate, string expected)
        {
            var config = TaskConfig(HpcTask.SpectraCache);
            mutate(config);
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task SpectraCache");
            StringAssert.Contains(err, expected);
        }

        // - PerFileScoring (mzML in) --

        [TestMethod]
        public void TestValidatePerFileScoringHappyPath()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresInput()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileScoring");
            StringAssert.Contains(err, "--input <mzML");
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresLibrary()
        {
            var config = TaskConfig(HpcTask.PerFileScoring);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileScoring");
            StringAssert.Contains(err, "--library");
        }

        // - PerFileRescore (one run in, its reconciled parquet out) --

        [TestMethod]
        public void TestValidatePerFileRescoreHappyPath()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresInput()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileRescoring");
            StringAssert.Contains(err, "--input");
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.PerFileRescore);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task PerFileRescoring");
            StringAssert.Contains(err, "--library and --output");
        }

        // - FirstPassFDR (2+ runs in, reconciliation on) --

        [TestMethod]
        public void TestValidateFirstPassFdrHappyPath()
        {
            var config = TaskConfig(HpcTask.FirstPassFdr);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresInput()
        {
            var config = TaskConfig(HpcTask.FirstPassFdr);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstPassFDR");
            StringAssert.Contains(err, "--input");
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.FirstPassFdr);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstPassFDR");
            StringAssert.Contains(err, "--library and --output");
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRejectsSingleFile()
        {
            // FirstPassFDR writes the Stage 5 -> Stage 6 boundary pair, only
            // meaningful with siblings; a single-file run errors fast.
            var config = TaskConfig(HpcTask.FirstPassFdr);
            config.InputFiles = new List<string> { "only.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task FirstPassFDR");
            StringAssert.Contains(err, "2+ files");
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresReconciliationEnabled()
        {
            var config = TaskConfig(HpcTask.FirstPassFdr);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            config.Reconciliation.Enabled = false;
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "Reconciliation.Enabled");
        }

        // - SecondPassFDR (every run in, reading their reconciled parquets) --

        [TestMethod]
        public void TestValidateSecondPassFdrHappyPath()
        {
            var config = TaskConfig(HpcTask.SecondPassFdr);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateSecondPassFdrRequiresInput()
        {
            // Uncontested gap from ultrareview: --task SecondPassFDR with no inputs at
            // all used to pass validation and silently run the full pipeline. It must
            // fail fast, and the message must name the task the user typed.
            var config = TaskConfig(HpcTask.SecondPassFdr);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task SecondPassFDR");
            StringAssert.Contains(err, "--input");
        }

        [TestMethod]
        public void TestValidateSecondPassFdrRequiresLibraryAndOutput()
        {
            var config = TaskConfig(HpcTask.SecondPassFdr);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "--task SecondPassFDR");
            StringAssert.Contains(err, "--library and --output");
        }

        // - ModelDiagnostics (the completed run's own command line, replayed) --

        [TestMethod]
        public void TestValidateModelDiagnosticsTakesTheFullPipelineArgs()
        {
            // Deliberately the ONLY task with no case in ValidateArgs' switch. It runs the
            // canonical pipeline so Stages 1-5 rehydrate from their stamps, which means the
            // caller re-issues the completed run's command line verbatim plus --task
            // ModelDiagnostics - so it must validate exactly as that command line does, and
            // adding a task-specific rule here would reject the invocation it exists to serve.
            var config = TaskConfig(HpcTask.ModelDiagnostics);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));

            // -i mzML is the other accepted form, same as a full run.
            var fromMzml = TaskConfig(HpcTask.ModelDiagnostics);
            fromMzml.InputFiles = new List<string> { "a.mzML" };
            fromMzml.LibrarySource = LibrarySource.FromPath("ref.blib");
            fromMzml.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(fromMzml));

            // And the full-pipeline requirements still bite: no input at all is an error.
            var bare = TaskConfig(HpcTask.ModelDiagnostics);
            bare.LibrarySource = LibrarySource.FromPath("ref.blib");
            bare.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(bare);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "No input files");
        }

        // - Default (no --task): the full pipeline --

        [TestMethod]
        public void TestValidateDefaultFullHappyPath()
        {
            var config = new OspreyConfig
            {
                InputFiles = new List<string> { "a.mzML" },
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateDefaultRejectsMissingInput()
        {
            var config = new OspreyConfig
            {
                LibrarySource = LibrarySource.FromPath("ref.blib"),
                OutputBlib = "out.blib"
            };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "No input files");
        }

        // --- ResolveTask (--task) -----------------------------------------

        [TestMethod]
        public void TestResolveTaskPerFileScoring()
        {
            Assert.IsNull(Program.ResolveTask("PerFileScoring", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileScoring, task);
        }

        [TestMethod]
        public void TestResolveTaskFirstPassFdr()
        {
            Assert.IsNull(Program.ResolveTask("FirstPassFDR", out HpcTask task));
            Assert.AreEqual(HpcTask.FirstPassFdr, task);
        }

        [TestMethod]
        public void TestResolveTaskPerFileRescore()
        {
            Assert.IsNull(Program.ResolveTask("PerFileRescoring", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileRescore, task);
        }

        [TestMethod]
        public void TestResolveTaskSecondPassFdr()
        {
            Assert.IsNull(Program.ResolveTask("SecondPassFDR", out HpcTask task));
            Assert.AreEqual(HpcTask.SecondPassFdr, task);
        }

        [TestMethod]
        public void TestResolveTaskSpectraCache()
        {
            Assert.IsNull(Program.ResolveTask("SpectraCache", out HpcTask task));
            Assert.AreEqual(HpcTask.SpectraCache, task);
        }

        [TestMethod]
        public void TestResolveTaskModelDiagnostics()
        {
            Assert.IsNull(Program.ResolveTask("ModelDiagnostics", out HpcTask task));
            Assert.AreEqual(HpcTask.ModelDiagnostics, task);
        }

        [TestMethod]
        public void TestResolveTaskIsCaseInsensitive()
        {
            Assert.IsNull(Program.ResolveTask("perfilerescoring", out HpcTask task));
            Assert.AreEqual(HpcTask.PerFileRescore, task);
        }

        [TestMethod]
        public void TestResolveTaskUnknownErrors()
        {
            string err = Program.ResolveTask("Bogus", out _);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "unknown task");
            StringAssert.Contains(err, "Bogus");
        }

        [TestMethod]
        public void TestResolveTaskMapsToExpectedMembershipFlags()
        {
            // Each task must derive (via Main's wiring, mirrored by TaskConfig)
            // the (NoJoin, StopAfterStage5, ExpectReconciledInput) tuple the four
            // tasks' IsIncluded methods read. Mirrors PipelineMembershipTest.
            //   task             | NoJoin | StopAfterStage5 | ExpectReconciled
            //   PerFileScoring   | true   | false           | false
            //   FirstPassFDR        | false  | true            | false
            //   PerFileRescore   | true   | false           | false
            //   SecondPassFDR        | false  | false           | true
            //   SpectraCache     | false  | false           | false
            //   ModelDiagnostics | false  | false           | false
            // SpectraCache is all-false because it drives no membership at all:
            // it runs its own one-task pipeline (AnalysisPipeline.SpectraCachePipeline)
            // rather than gating tasks inside the canonical one. ModelDiagnostics is
            // all-false for the opposite reason: it runs the CANONICAL pipeline
            // unchanged so Stages 1-5 rehydrate from their existing stamps, and
            // suppresses writes through DiagnosticsOnly instead of through membership.
            var cases = new (HpcTask Task, bool NoJoin, bool StopAfterStage5, bool ExpectReconciled)[]
            {
                (HpcTask.PerFileScoring, true,  false, false),
                (HpcTask.FirstPassFdr,      false, true,  false),
                (HpcTask.PerFileRescore, true,  false, false),
                (HpcTask.SecondPassFdr,      false, false, true),
                (HpcTask.SpectraCache,   false, false, false),
                (HpcTask.ModelDiagnostics, false, false, false),
            };
            foreach (var c in cases)
            {
                var config = TaskConfig(c.Task);
                Assert.AreEqual(c.NoJoin, config.NoJoin, string.Format("{0}: NoJoin", c.Task));
                Assert.AreEqual(c.StopAfterStage5, config.StopAfterStage5,
                    string.Format("{0}: StopAfterStage5", c.Task));
                Assert.AreEqual(c.ExpectReconciled, config.ExpectReconciledInput,
                    string.Format("{0}: ExpectReconciledInput", c.Task));
                // DiagnosticsOnly is derived from SelectedTask, so it must single out
                // exactly one row - it is the flag every write suppression reads.
                Assert.AreEqual(c.Task == HpcTask.ModelDiagnostics, config.DiagnosticsOnly,
                    string.Format("{0}: DiagnosticsOnly", c.Task));
            }
        }

        // --- --task ModelDiagnostics: regenerate the report, touch nothing else ---

        [TestMethod]
        public void TestModelDiagnosticsImpliesTheReportFlag()
        {
            // Main turns the selector into --model-diagnostics (mirrored by TaskConfig).
            // Without it the run recomputes the pass-2 view and writes nothing at all -
            // a silent no-op that looks like a successful regeneration.
            Assert.IsTrue(TaskConfig(HpcTask.ModelDiagnostics).ModelDiagnostics);
            Assert.IsFalse(TaskConfig(HpcTask.SecondPassFdr).ModelDiagnostics);
        }

        [TestMethod]
        public void TestModelDiagnosticsDeclaresNoOutputs()
        {
            // The regenerate-on-demand design rests on this: PipelineContext.CanRehydrate
            // returns false on an empty output list, so declaring nothing is what makes a
            // re-run actually re-run. Declaring the report instead made the task skip
            // itself the moment the report existed - which is precisely the case the
            // caller is asking to redo (observed: a first acceptance run changed 0 of 45
            // files). The second arm is the discriminating one: the same task with the
            // same output paths DOES declare outputs when the flag is off, so an empty
            // list here cannot be an artifact of the bare config.
            Assert.AreEqual(0, SecondPassFdrOutputs(HpcTask.ModelDiagnostics).Count,
                "--task ModelDiagnostics must declare no outputs");
            Assert.AreNotEqual(0, SecondPassFdrOutputs(HpcTask.SecondPassFdr).Count,
                "--task SecondPassFDR must still declare its outputs");
        }

        private static List<string> SecondPassFdrOutputs(HpcTask task)
        {
            var config = TaskConfig(task);
            config.InputFiles = new List<string> { @"a.mzML", @"b.mzML" };
            config.LibrarySource = LibrarySource.FromPath(@"ref.blib");
            config.OutputBlib = @"out.blib";
            var tasks = AnalysisPipeline.CanonicalPipeline();
            var ctx = new PipelineContext(config, tasks, null, null, null);
            var secondPass = tasks[tasks.Length - 1];
            Assert.IsInstanceOfType(secondPass, typeof(SecondPassFdrTask));
            return new List<string>(secondPass.Outputs(ctx));
        }

        // --- ParseArgs: unknown / retired flags fail fast -----------------

        [TestMethod]
        public void TestParseArgsRejectsRetiredNoJoin()
        {
            // The retired HPC mode flags are now unknown options. ParseArgs
            // must throw rather than silently dropping them (which would run
            // the full pipeline in the wrong mode). Replaced by --task <Name>.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--no-join", "-i", "a.mzML" }));
            StringAssert.Contains(ex.Message, "--no-join");
        }

        [TestMethod]
        public void TestParseArgsRejectsRetiredJoinOnly()
        {
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-only" }));
            StringAssert.Contains(ex.Message, "--join-only");
        }

        [TestMethod]
        public void TestParseArgsRejectsRetiredJoinAtPass()
        {
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-at-pass=2" }));
            StringAssert.Contains(ex.Message, "--join-at-pass=2");

            // Space-separated form too.
            Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--join-at-pass", "1" }));
        }

        [TestMethod]
        public void TestParseArgsRejectsUnknownFlag()
        {
            // Any unrecognized option fails fast (e.g. a typo), not just the
            // retired flags.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--bogus-flag" }));
            StringAssert.Contains(ex.Message, "--bogus-flag");
        }

        [TestMethod]
        public void TestParseArgsAcceptsTaskAndValidArgs()
        {
            // --task and ordinary flags must NOT throw.
            Program.ParseArgs(new[] { "--task", "FirstPassFDR", "-l", "ref.blib", "-o", "out.blib" });
            Program.ParseArgs(new[] { "--task=SecondPassFDR", "-l", "ref.blib", "-o", "out.blib" });
        }

        [TestMethod]
        public void TestParseArgsRejectsTaskWithoutValue()
        {
            // A bare --task (or --task followed by another flag) must throw,
            // like the other required-value flags, so it can't be silently
            // ignored when ParseArgs runs outside Main's pre-scan.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--task" }));
            StringAssert.Contains(ex.Message, "--task");
            Assert.ThrowsException<ArgumentException>(
                () => Program.ParseArgs(new[] { "--task", "-l", "ref.blib" }));
        }

        [TestMethod]
        public void TestParseArgsRejectsValueFlagsWithoutValue()
        {
            // Single-value option flags must reject both a missing value (flag
            // is the last token) and a following option token (the next arg
            // starts with '-'), so e.g. `-o -l x` can't silently swallow `-l`
            // as the output path. Representative coverage across the path,
            // numeric, and enum flags.
            var missingOrFlagFollowed = new[]
            {
                new[] { "-l" },
                new[] { "-o", "-l", "x.blib" },
                new[] { "--output" },
                new[] { "--resolution", "--protein-fdr", "0.01" },
                new[] { "--protein-fdr" },
                new[] { "--threads", "-i", "f.mzML" },
                new[] { "--decoy-pairing-manifest" },
                new[] { "--fdr-method", "-o", "out.blib" },
                new[] { "--fdr-level" },
                new[] { "--shared-peptides", "--threads", "4" },
            };
            foreach (var args in missingOrFlagFollowed)
            {
                Assert.ThrowsException<ArgumentException>(
                    () => Program.ParseArgs(args));
            }
        }

        // --- OspreyConfig defaults ----------------------------------------

        [TestMethod]
        public void TestConfigDefaultsDisableHpcMode()
        {
            var cfg = new OspreyConfig();
            Assert.IsFalse(cfg.NoJoin, "NoJoin should default to false");
        }

        // --- ParquetScoreCache.CheckParquetMetadata -----------------------

        private const string VALID_SEARCH = "search-hash-aaa";
        private const string VALID_LIB = "lib-hash-bbb";

        // Track OspreyVersion.Current so happy-path and drift tests stay
        // meaningful as the build version advances. The version follows the
        // Skyline scheme YEAR.ORDINAL.BRANCH.DOY; cache reuse requires an exact
        // version match. Any difference (release line or daily build) aborts.
        private static readonly string CURRENT_VERSION = OspreyVersion.Current;
        private static readonly string DAILY_DRIFT_VERSION = DriftVersion(0, 0, 0, 5);
        private static readonly string BRANCH_DRIFT_VERSION = DriftVersion(0, 0, 1, 0);
        private static readonly string ORDINAL_DRIFT_VERSION = DriftVersion(0, 1, 0, 0);
        private static readonly string YEAR_DRIFT_VERSION = DriftVersion(1, 0, 0, 0);

        private static string DriftVersion(int yearDelta, int ordinalDelta, int branchDelta, int doyDelta)
        {
            var parts = CURRENT_VERSION.Split('.');
            int year = int.Parse(parts[0], CultureInfo.InvariantCulture);
            int ordinal = int.Parse(parts[1], CultureInfo.InvariantCulture);
            int branch = int.Parse(parts[2], CultureInfo.InvariantCulture);
            int doy = int.Parse(parts[3], CultureInfo.InvariantCulture);
            return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}",
                year + yearDelta, ordinal + ordinalDelta, branch + branchDelta, doy + doyDelta);
        }

        private static string CheckMd(string cachedV, string cachedS, string cachedL)
        {
            return ParquetScoreCache.CheckParquetMetadata(
                "test.scores.parquet",
                cachedV, cachedS, cachedL,
                VALID_SEARCH, VALID_LIB, CURRENT_VERSION);
        }

        [TestMethod]
        public void TestParseVersionRoundTrip()
        {
            int y, o, b, d;
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("26.1.1.166", out y, out o, out b, out d));
            Assert.AreEqual(26, y); Assert.AreEqual(1, o); Assert.AreEqual(1, b); Assert.AreEqual(166, d);
            Assert.IsTrue(ParquetScoreCache.TryParseVersion("0.0.0.1", out y, out o, out b, out d));
            Assert.AreEqual(0, y); Assert.AreEqual(0, o); Assert.AreEqual(0, b); Assert.AreEqual(1, d);
        }

        [TestMethod]
        public void TestParseVersionRejectsBadInput()
        {
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.1.1", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("v26.1.1.0", out _, out _, out _, out _));
            Assert.IsFalse(ParquetScoreCache.TryParseVersion("26.1.1.x", out _, out _, out _, out _));
        }

        [TestMethod]
        public void TestMetadataExactVersionMatchOk()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNull(err);
        }

        [TestMethod]
        public void TestMetadataDailyDriftAborts()
        {
            // A different daily build may have changed scoring: hard-fail rather
            // than silently reuse a stale cache behind an easily-missed warning.
            string err = CheckMd(DAILY_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "different daily build");
        }

        [TestMethod]
        public void TestMetadataBranchVersionDriftAborts()
        {
            string err = CheckMd(BRANCH_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataOrdinalVersionDriftAborts()
        {
            string err = CheckMd(ORDINAL_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataYearVersionDriftAborts()
        {
            string err = CheckMd(YEAR_DRIFT_VERSION, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "incompatible release identity");
        }

        [TestMethod]
        public void TestMetadataMissingVersionAborts()
        {
            string err = CheckMd(null, VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.version");
        }

        [TestMethod]
        public void TestMetadataMissingSearchHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, null, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.search_hash");
        }

        [TestMethod]
        public void TestMetadataMissingLibraryHashAborts()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, null);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "osprey.library_hash");
        }

        [TestMethod]
        public void TestMetadataSearchHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, "wrong-hash", VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "search_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-hash");
        }

        [TestMethod]
        public void TestMetadataLibraryHashMismatchNamesFieldAndFile()
        {
            string err = CheckMd(CURRENT_VERSION, VALID_SEARCH, "wrong-lib");
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "library_hash mismatch");
            StringAssert.Contains(err, "test.scores.parquet");
            StringAssert.Contains(err, "wrong-lib");
        }

        [TestMethod]
        public void TestMetadataUnparseableVersionAborts()
        {
            // An unrecognized cached version can't be validated for
            // compatibility, so refuse to reuse the cache (hard fail).
            string err = CheckMd("garbage", VALID_SEARCH, VALID_LIB);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "unrecognized osprey version");
        }

        // --- Library-decoy CLI flags ---------------------------------------

        [TestMethod]
        public void TestParseArgsDecoysInLibraryFlag()
        {
            // --decoys-in-library is a flat boolean; flips DecoysInLibrary
            // to true without consuming a value. Mirrors Rust osprey's
            // --decoys-in-library semantics.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoys-in-library",
            };
            var config = Program.ParseArgs(args);
            Assert.IsTrue(config.DecoysInLibrary);
            Assert.IsTrue(string.IsNullOrEmpty(config.DecoyPairingManifestPath));
        }

        [TestMethod]
        public void TestParseArgsDecoyPairingManifestFlag()
        {
            // --decoy-pairing-manifest <PATH> sets the path on the config
            // WITHOUT flipping DecoysInLibrary. Pin the contract here in
            // isolation (no companion --decoys-in-library) so a regression
            // where the flag accidentally enables library-decoy mode on
            // its own would actually fail the test.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest", @"T:\test\manifest.tsv",
            };
            var config = Program.ParseArgs(args);
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.AreEqual(@"T:\test\manifest.tsv", config.DecoyPairingManifestPath);
        }

        [TestMethod]
        public void TestParseArgsDecoyPairingManifestRequiresValue()
        {
            // A bare --decoy-pairing-manifest with no value, or a value
            // that's itself an option (--decoys-in-library), must throw
            // rather than silently consume the next token as the path.
            var argsNoValue = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest",
            };
            try
            {
                Program.ParseArgs(argsNoValue);
                Assert.Fail(@"Expected ArgumentException for bare --decoy-pairing-manifest.");
            }
            catch (ArgumentException)
            {
                // expected
            }

            var argsFlagAsValue = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
                @"--decoy-pairing-manifest", @"--decoys-in-library",
            };
            try
            {
                Program.ParseArgs(argsFlagAsValue);
                Assert.Fail(@"Expected ArgumentException for next-flag-as-value.");
            }
            catch (ArgumentException)
            {
                // expected
            }
        }

        [TestMethod]
        public void TestParseArgsDecoysInLibraryDefaultsFalse()
        {
            // Without the flag, DecoysInLibrary stays at its config default
            // (false) and DecoyPairingManifestPath stays null. Pipeline runs
            // the existing reverse-decoy path. Pins the "library-decoy mode
            // is fully opt-in" contract.
            var args = new[]
            {
                @"-i", @"x.mzML",
                @"-l", @"lib.tsv",
                @"-o", @"out.blib",
            };
            var config = Program.ParseArgs(args);
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.IsTrue(string.IsNullOrEmpty(config.DecoyPairingManifestPath));
        }

        // --- helpers -------------------------------------------------------

        private static string NewTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "osprey_test_program_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
