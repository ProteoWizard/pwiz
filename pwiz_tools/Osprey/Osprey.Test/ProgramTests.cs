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
using System.Linq;
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

        /// <summary>
        /// Two inputs sharing a file-name STEM are refused, whatever directories they sit in.
        ///
        /// <para>Every per-run artifact is <c>&lt;stem&gt;.&lt;suffix&gt;</c> and every per-run
        /// map is keyed the same way, so a shared stem is two runs the pipeline cannot tell
        /// apart. Left to be discovered downstream it takes two shapes and neither names the
        /// cause: an <c>ArgumentException</c> about a duplicate key mid-Stage-6/7, or - with
        /// <c>--output-dir</c>, where both stems resolve into one directory - two runs quietly
        /// sharing one parquet, no error at all.</para>
        ///
        /// <para>Asserted with DIFFERENT directories, which is the case that matters and the
        /// one <c>--input-list</c> makes routine at cohort scale; identical paths would be
        /// caught by cruder means.</para>
        /// </summary>
        [TestMethod]
        public void TestValidateRejectsDuplicateInputStems()
        {
            var config = TaskConfigs.ForTask(PerFileScoringTask.TASK_NAME);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            // Forward slashes deliberately. DuplicateInputStemError derives the stem with
            // Path.GetFileNameWithoutExtension, and on Linux \ is an ordinary filename
            // character - so "plateA\run1.mzML" is one flat name whose stem is
            // "plateA\run1", the two paths do not collide, and the check correctly finds
            // nothing. / is an alternate separator on Windows too, so one spelling yields the
            // stem "run1" on both platforms.
            config.InputFiles = new List<string> { @"plateA/run1.mzML", @"plateB/run1.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err, "two inputs sharing a stem must be refused");
            // The stem and BOTH colliding paths, so the operator can act without re-deriving
            // which of several hundred inputs collided.
            StringAssert.Contains(err, "run1");
            StringAssert.Contains(err, @"plateA/run1.mzML");
            StringAssert.Contains(err, @"plateB/run1.mzML");

            // Distinct stems in one directory remain fine - the check is on the stem, not the
            // directory, and a cohort in one folder is the ordinary case.
            config.InputFiles = new List<string> { @"plateA/run1.mzML", @"plateA/run2.mzML" };
            Assert.IsNull(Program.ValidateArgs(config));
        }

        // --- ValidateArgs: what each task requires -------------------------
        // Each config comes from TaskConfigs.ForTask, i.e. through the same SelectTask the
        // CLI goes through, so the flags are the task's own and not a copy of Main's wiring.

        // - SpectraCache (Stage 1 alone: inputs in, .spectra.bin out) --

        [TestMethod]
        public void TestValidateSpectraCache()
        {
            // Consolidated: the whole SpectraCache contract in one place.
            // The defining difference from every other task is that it needs
            // NO library - caching depends only on the input file - so the
            // happy path below deliberately leaves LibrarySource null.
            var config = TaskConfigs.ForTask(SpectraCacheTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.raw" };
            Assert.IsNull(Program.ValidateArgs(config), "no library should be required");

            // A library is merely unnecessary, not rejected: staging a dataset
            // with the eventual run's full command line must still work.
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            Assert.IsNull(Program.ValidateArgs(config), "a library should be tolerated");

            AssertSpectraCacheError(c => { }, OspreyCommandArgs.ARG_INPUT.ArgumentText);
        }

        private static void AssertSpectraCacheError(Action<OspreyConfig> mutate, string expected)
        {
            var config = TaskConfigs.ForTask(SpectraCacheTask.TASK_NAME);
            mutate(config);
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + SpectraCacheTask.TASK_NAME);
            StringAssert.Contains(err, expected);
        }

        // - PerFileScoring (mzML in) --

        [TestMethod]
        public void TestValidatePerFileScoringHappyPath()
        {
            var config = TaskConfigs.ForTask(PerFileScoringTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresInput()
        {
            var config = TaskConfigs.ForTask(PerFileScoringTask.TASK_NAME);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + PerFileScoringTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_INPUT.ArgumentText);
        }

        [TestMethod]
        public void TestValidatePerFileScoringRequiresLibrary()
        {
            var config = TaskConfigs.ForTask(PerFileScoringTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + PerFileScoringTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_LIBRARY.ArgumentText);
        }

        // - PerFileRescore (one run in, its reconciled parquet out) --

        [TestMethod]
        public void TestValidatePerFileRescoreHappyPath()
        {
            var config = TaskConfigs.ForTask(PerFileRescoreTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresInput()
        {
            var config = TaskConfigs.ForTask(PerFileRescoreTask.TASK_NAME);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + PerFileRescoreTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_INPUT.ArgumentText);
        }

        [TestMethod]
        public void TestValidatePerFileRescoreRequiresLibraryAndOutput()
        {
            var config = TaskConfigs.ForTask(PerFileRescoreTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + PerFileRescoreTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_LIBRARY.ArgumentText + @" and " + OspreyCommandArgs.ARG_OUTPUT.ArgumentText);
        }

        // - FirstPassFDR (2+ runs in, reconciliation on) --

        [TestMethod]
        public void TestValidateFirstPassFdrHappyPath()
        {
            var config = TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresInput()
        {
            var config = TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + FirstPassFdrTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_INPUT.ArgumentText);
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresLibraryAndOutput()
        {
            var config = TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + FirstPassFdrTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_LIBRARY.ArgumentText + @" and " + OspreyCommandArgs.ARG_OUTPUT.ArgumentText);
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRejectsSingleFile()
        {
            // FirstPassFDR writes the Stage 5 -> Stage 6 boundary pair, only
            // meaningful with siblings; a single-file run errors fast.
            var config = TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME);
            config.InputFiles = new List<string> { "only.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + FirstPassFdrTask.TASK_NAME);
            StringAssert.Contains(err, "2+ files");
        }

        [TestMethod]
        public void TestValidateFirstPassFdrRequiresReconciliationEnabled()
        {
            var config = TaskConfigs.ForTask(FirstPassFdrTask.TASK_NAME);
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
            var config = TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME);
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
            var config = TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME);
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + SecondPassFdrTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_INPUT.ArgumentText);
        }

        [TestMethod]
        public void TestValidateSecondPassFdrRequiresLibraryAndOutput()
        {
            var config = TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML" };
            string err = Program.ValidateArgs(config);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + SecondPassFdrTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_LIBRARY.ArgumentText + @" and " + OspreyCommandArgs.ARG_OUTPUT.ArgumentText);
        }

        // - ModelDiagnostics (the completed run's own command line, replayed) --

        [TestMethod]
        public void TestValidateModelDiagnosticsTakesTheFullPipelineArgs()
        {
            // Deliberately the one task that leaves ValidateSelection at the base default. It
            // runs the canonical pipeline so Stages 1-5 rehydrate from their stamps, which
            // means the caller re-issues the completed run's command line verbatim plus
            // --task ModelDiagnostics - so it must require exactly what that command line
            // does, and a task-specific rule here would reject the invocation it exists to
            // serve.
            var config = TaskConfigs.ForTask(ModelDiagnosticsTask.TASK_NAME);
            config.InputFiles = new List<string> { "a.mzML", "b.mzML" };
            config.LibrarySource = LibrarySource.FromPath("ref.blib");
            config.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(config));

            // -i mzML is the other accepted form, same as a full run.
            var fromMzml = TaskConfigs.ForTask(ModelDiagnosticsTask.TASK_NAME);
            fromMzml.InputFiles = new List<string> { "a.mzML" };
            fromMzml.LibrarySource = LibrarySource.FromPath("ref.blib");
            fromMzml.OutputBlib = "out.blib";
            Assert.IsNull(Program.ValidateArgs(fromMzml));

            // And the full-pipeline requirements still bite: no input at all is an error,
            // naming the task the user typed like every other task's message.
            var bare = TaskConfigs.ForTask(ModelDiagnosticsTask.TASK_NAME);
            bare.LibrarySource = LibrarySource.FromPath("ref.blib");
            bare.OutputBlib = "out.blib";
            string err = Program.ValidateArgs(bare);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK + ModelDiagnosticsTask.TASK_NAME);
            StringAssert.Contains(err, OspreyCommandArgs.ARG_INPUT.ArgumentText);
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

        /// <summary>
        /// Every task in the one list resolves to ITSELF - the same instance, not a
        /// namesake - and appears in the --task value list exactly once; matching is
        /// case-insensitive; and an unknown name is an error that names the flag, the value
        /// and every valid task. The list is the authority for all three, so this cannot
        /// drift from a second spelling of it: there is none.
        /// </summary>
        [TestMethod]
        public void TestResolveTask()
        {
            var tasks = OspreyTasks.Create();
            Assert.AreEqual(tasks.All.Count, OspreyCommandArgs.ARG_TASK.Values.Length, @"every task is listed once");
            foreach (var expected in tasks.All)
            {
                Assert.IsNull(Program.ResolveTask(expected.Name, tasks, out OspreyTask task));
                Assert.AreSame(expected, task, expected.Name);
                Assert.AreEqual(1, Array.FindAll(OspreyCommandArgs.ARG_TASK.Values, v => v == expected.Name).Length,
                    string.Format(@"{0} must appear in the --task values exactly once", expected.Name));
            }
            // The four canonical stages and the two selector-only tasks, by the constants the
            // classes declare, so a task dropped from the list fails here by name.
            CollectionAssert.AreEquivalent(
                new[]
                {
                    SpectraCacheTask.TASK_NAME, PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME,
                    PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME, ModelDiagnosticsTask.TASK_NAME
                },
                OspreyCommandArgs.ARG_TASK.Values);

            // Case-insensitive, resolving to the canonical spelling.
            Assert.IsNull(Program.ResolveTask(PerFileRescoreTask.TASK_NAME.ToLowerInvariant(), tasks, out OspreyTask lower));
            Assert.AreEqual(PerFileRescoreTask.TASK_NAME, lower.Name);

            string err = Program.ResolveTask("Bogus", tasks, out OspreyTask none);
            Assert.IsNull(none);
            Assert.IsNotNull(err);
            StringAssert.Contains(err, "unknown task");
            StringAssert.Contains(err, "Bogus");
            StringAssert.Contains(err, OspreyCommandArgs.ARG_TASK.ArgumentText);
            foreach (var task in tasks.All)
                StringAssert.Contains(err, task.Name);
        }

        [TestMethod]
        public void TestSelectTaskSetsExpectedBehaviorFlags()
        {
            // Each task must set (through its ApplySelection, reached by SelectTask) the
            // behavior flags its selection implies - and nothing else. These are not
            // membership flags any more (that is OspreyConfig.Includes, pinned in
            // PipelineMembershipTest); each is read by the arms of one task's body or by a
            // gate below the task library.
            //   task             | StopAfterStage5 | ExpectReconciled | DiagnosticsOnly
            //   SpectraCache     | false           | false            | false
            //   PerFileScoring   | false           | false            | false
            //   FirstPassFDR     | true            | false            | false
            //   PerFileRescoring | false           | false            | false
            //   SecondPassFDR    | false           | true             | false
            //   ModelDiagnostics | false           | false            | true
            var cases = new (string Task, bool StopAfterStage5, bool ExpectReconciled, bool DiagnosticsOnly)[]
            {
                (SpectraCacheTask.TASK_NAME,     false, false, false),
                (PerFileScoringTask.TASK_NAME,   false, false, false),
                (FirstPassFdrTask.TASK_NAME,     true,  false, false),
                (PerFileRescoreTask.TASK_NAME,   false, false, false),
                (SecondPassFdrTask.TASK_NAME,    false, true,  false),
                (ModelDiagnosticsTask.TASK_NAME, false, false, true),
            };
            Assert.AreEqual(OspreyTasks.Create().All.Count, cases.Length, @"every task has a flags row");
            foreach (var c in cases)
            {
                var config = TaskConfigs.ForTask(c.Task);
                Assert.AreEqual(c.Task, config.SelectedTask.Name);
                Assert.AreEqual(c.StopAfterStage5, config.StopAfterStage5,
                    string.Format("{0}: StopAfterStage5", c.Task));
                Assert.AreEqual(c.ExpectReconciled, config.ExpectReconciledInput,
                    string.Format("{0}: ExpectReconciledInput", c.Task));
                // DiagnosticsOnly is the flag every write suppression reads, so it must
                // single out exactly one row.
                Assert.AreEqual(c.DiagnosticsOnly, config.DiagnosticsOnly,
                    string.Format("{0}: DiagnosticsOnly", c.Task));
            }
            // No selection: the full pipeline, every flag off, the canonical pipeline carried.
            var full = TaskConfigs.StraightThrough();
            Assert.IsNull(full.SelectedTask);
            Assert.AreEqual(4, full.Pipeline.Count);
            Assert.IsFalse(full.StopAfterStage5 || full.ExpectReconciledInput || full.DiagnosticsOnly);
            // A re-selection holds exactly the new task's flags and pipeline: nothing a
            // previous selection set survives, so a config reused across selections cannot
            // carry a stale flag into a task body.
            var tasks = OspreyTasks.Create();
            var reselected = TaskConfigs.ForTask(tasks, FirstPassFdrTask.TASK_NAME);
            Assert.IsTrue(reselected.StopAfterStage5);
            reselected.SelectTask(null, tasks.Pipeline);
            Assert.IsFalse(reselected.StopAfterStage5, @"clearing the selection clears its flags");
            var modelDiagnostics = tasks.FindByName(ModelDiagnosticsTask.TASK_NAME);
            reselected.SelectTask(modelDiagnostics, tasks.PipelineFor(modelDiagnostics));
            var secondPass = tasks.FindByName(SecondPassFdrTask.TASK_NAME);
            reselected.SelectTask(secondPass, tasks.PipelineFor(secondPass));
            Assert.IsFalse(reselected.DiagnosticsOnly, @"a later selection drops the earlier one's flags");
            Assert.IsTrue(reselected.ExpectReconciledInput);
            // ... except --model-diagnostics, which is the operator's own flag, not a
            // selection's: ModelDiagnostics implies it but a later selection does not revoke it.
            Assert.IsTrue(reselected.ModelDiagnostics);
            // A selection without the pipeline it runs is refused: the two travel together.
            Assert.ThrowsException<ArgumentNullException>(() => new OspreyConfig().SelectTask(secondPass, null));
        }

        // --- --task ModelDiagnostics: regenerate the report, touch nothing else ---

        [TestMethod]
        public void TestModelDiagnosticsImpliesTheReportFlag()
        {
            // The selection turns the selector into --model-diagnostics
            // (ModelDiagnosticsTask.ApplySelection). Without it the run recomputes the
            // pass-2 view and writes nothing at all - a silent no-op that looks like a
            // successful regeneration.
            Assert.IsTrue(TaskConfigs.ForTask(ModelDiagnosticsTask.TASK_NAME).ModelDiagnostics);
            Assert.IsFalse(TaskConfigs.ForTask(SecondPassFdrTask.TASK_NAME).ModelDiagnostics);
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
            Assert.AreEqual(0, SecondPassFdrOutputs(ModelDiagnosticsTask.TASK_NAME).Count,
                "--task ModelDiagnostics must declare no outputs");
            Assert.AreNotEqual(0, SecondPassFdrOutputs(SecondPassFdrTask.TASK_NAME).Count,
                "--task SecondPassFDR must still declare its outputs");
        }

        private static List<string> SecondPassFdrOutputs(string taskName)
        {
            var config = TaskConfigs.ForTask(taskName);
            config.InputFiles = new List<string> { @"a.mzML", @"b.mzML" };
            config.LibrarySource = LibrarySource.FromPath(@"ref.blib");
            config.OutputBlib = @"out.blib";
            var ctx = TaskConfigs.ContextFor(config);
            var secondPass = config.Pipeline.OfType<SecondPassFdrTask>().Single();
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
                () => Parse("--no-join", OspreyCommandArgs.ARG_INPUT + @"a.mzML"));
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
            Parse(OspreyCommandArgs.ARG_TASK + FirstPassFdrTask.TASK_NAME, OspreyCommandArgs.ARG_LIBRARY + @"ref.blib", OspreyCommandArgs.ARG_OUTPUT + @"out.blib");
            // --task=Name is the one joined form Program.Main pre-scans, so it is spelled here.
            Parse(OspreyCommandArgs.ARG_TASK.ArgumentText + @"=" + SecondPassFdrTask.TASK_NAME, OspreyCommandArgs.ARG_LIBRARY + @"ref.blib", OspreyCommandArgs.ARG_OUTPUT + @"out.blib");
        }

        [TestMethod]
        public void TestParseArgsRejectsTaskWithoutValue()
        {
            // A bare --task (or --task followed by another flag) must throw,
            // like the other required-value flags, so it can't be silently
            // ignored when ParseArgs runs outside Main's pre-scan.
            var ex = Assert.ThrowsException<ArgumentException>(
                () => Parse(OspreyCommandArgs.ARG_TASK));
            StringAssert.Contains(ex.Message, OspreyCommandArgs.ARG_TASK.ArgumentText);
            Assert.ThrowsException<ArgumentException>(
                () => Parse(OspreyCommandArgs.ARG_TASK, OspreyCommandArgs.ARG_LIBRARY + @"ref.blib"));
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
                ArgTokens.Split(OspreyCommandArgs.ARG_LIBRARY.ShortArgumentText),
                ArgTokens.Split(OspreyCommandArgs.ARG_OUTPUT.ShortArgumentText, OspreyCommandArgs.ARG_LIBRARY + @"x.blib"),
                ArgTokens.Split(OspreyCommandArgs.ARG_OUTPUT),
                ArgTokens.Split(OspreyCommandArgs.ARG_RESOLUTION, OspreyCommandArgs.ARG_PROTEIN_FDR + 0.01),
                ArgTokens.Split(OspreyCommandArgs.ARG_PROTEIN_FDR),
                ArgTokens.Split(OspreyCommandArgs.ARG_THREADS, OspreyCommandArgs.ARG_INPUT + @"f.mzML"),
                ArgTokens.Split(OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST),
                ArgTokens.Split(OspreyCommandArgs.ARG_FDR_METHOD, OspreyCommandArgs.ARG_OUTPUT + @"out.blib"),
                ArgTokens.Split(OspreyCommandArgs.ARG_FDR_LEVEL),
                ArgTokens.Split(OspreyCommandArgs.ARG_SHARED_PEPTIDES, OspreyCommandArgs.ARG_THREADS + 4),
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
            Assert.IsNull(cfg.SelectedTask, "no task should be selected by default");
            Assert.IsFalse(cfg.StopAfterStage5 || cfg.ExpectReconciledInput || cfg.DiagnosticsOnly,
                "no selection-derived flag should default to true");
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
            var config = Parse(RequiredIoThen(OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY));
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
            var config = Parse(RequiredIoThen(
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST + @"T:\test\manifest.tsv"));
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.AreEqual(@"T:\test\manifest.tsv", config.DecoyPairingManifestPath);
        }

        [TestMethod]
        public void TestParseArgsDecoyPairingManifestRequiresValue()
        {
            // A bare --decoy-pairing-manifest with no value, or a value
            // that's itself an option (--decoys-in-library), must throw
            // rather than silently consume the next token as the path.
            var argsNoValue = RequiredIoThen(OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST);
            try
            {
                Parse(argsNoValue);
                Assert.Fail(@"Expected ArgumentException for bare --decoy-pairing-manifest.");
            }
            catch (ArgumentException)
            {
                // expected
            }

            var argsFlagAsValue = RequiredIoThen(
                OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST, OspreyCommandArgs.ARG_DECOYS_IN_LIBRARY);
            try
            {
                Parse(argsFlagAsValue);
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
            var config = Parse(RequiredIoThen());
            Assert.IsFalse(config.DecoysInLibrary);
            Assert.IsTrue(string.IsNullOrEmpty(config.DecoyPairingManifestPath));
        }

        /// <summary>
        /// <see cref="Program.ParseArgs"/> over tokens built from the Argument instances, split
        /// into argv the way a shell would by <see cref="ArgTokens.Split"/>.
        /// </summary>
        private static OspreyConfig Parse(params string[] tokens)
        {
            return Program.ParseArgs(ArgTokens.Split(tokens));
        }

        /// <summary>
        /// The input, library and output every parse needs, followed by the tokens under
        /// test. Each token is one ARG instance, alone for a flag or joined to its value
        /// with <c>+</c>, exactly as the command line the usage text documents.
        /// </summary>
        private static string[] RequiredIoThen(params string[] tokens)
        {
            var args = new List<string>
            {
                OspreyCommandArgs.ARG_INPUT + @"x.mzML",
                OspreyCommandArgs.ARG_LIBRARY + @"lib.tsv",
                OspreyCommandArgs.ARG_OUTPUT + @"out.blib"
            };
            args.AddRange(tokens);
            return args.ToArray();
        }
    }
}
