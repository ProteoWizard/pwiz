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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec.IO;
using pwiz.Common.SystemUtil;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Test
{
    public partial class SubsetPipelineTest
    {
        private const string PROTEIN_BEST_SHUFFLE = @"OSPREY_PROTEIN_BEST_SHUFFLE";
        private const string PREFILTER_SIZE = @"OSPREY_GBT_PREFILTER_SIZE";
        // The environment variable --protein-boost best replaced, which is now refused as unknown.
        private const string RETIRED_PROTEIN_BEST = @"OSPREY_PROTEIN_BEST";

        /// <summary>
        /// The protein-best column (<c>--protein-boost best</c>, the default) through the whole pipeline
        /// on the subset, where each run computes its own column from its own round-1 model: on
        /// trains two rounds at the default size; a run with it completes both passes and writes
        /// FDRBench for both passes and a library; every arm moves the scores; and a rerun, another
        /// lane count, one thread, a resume with the second pass deleted, the stand-alone model
        /// diagnostics and the four-task chain give the same bits. The refusals cost no work.
        /// <see cref="OspreyEnvironment.FdrModel"/> is read once at class load, so the classifier
        /// is set directly, as the gbdt leg above does.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestProteinBestPipeline()
        {
            var savedFdrModel = OspreyEnvironment.FdrModel;
            try
            {
                AssertPrefilterSizeDefault();
                OspreyEnvironment.FdrModel = FdrClassifier.LinearSvm;
                AssertProteinBestRefusals(FdrClassifier.LinearSvm);
                AssertLinearProteinBestAnalysis();
                OspreyEnvironment.FdrModel = FdrClassifier.Gbdt;
                AssertProteinBestRefusals(FdrClassifier.Gbdt);
                AssertProteinBestArms();
                // The four-task chain: the PerFileRescoring nodes score with the run models
                // PerFileScoring wrote, each carrying its run's column, shipped to them with the rest.
                AssertHpcChainMatchesStraight(new Subset(_dataDir, LIBRARY_FILE, @"unit", RUN_NAMES, null,
                    ProteinBestArm(), ProteinBestArgs(true)), @"protein-best-default-");
            }
            finally
            {
                OspreyEnvironment.FdrModel = savedFdrModel;
            }
        }

        /// <summary>
        /// The round-2 size a run trains with: unset, the protein-best default with the column on and
        /// one round with it off; set, the variable under either, 0 included. A round-2 switch then
        /// needs no size of its own with the column on.
        /// </summary>
        private static void AssertPrefilterSizeDefault()
        {
            foreach (var size in new[] { string.Empty, @"5000", @"0" })
            {
                using (EnvVar.OverrideVariables(new Dictionary<string, string> { [PREFILTER_SIZE] = size }))
                {
                    int expectedOn = size.Length == 0 ? OspreyEnvironment.PROTEIN_BEST_PREFILTER_SIZE : int.Parse(size, CultureInfo.InvariantCulture);
                    int expectedOff = size.Length == 0 ? 0 : expectedOn;
                    Assert.AreEqual(expectedOn, OspreyEnvironment.PrefilterSizeFor(true), size);
                    Assert.AreEqual(expectedOff, OspreyEnvironment.PrefilterSizeFor(false), size);
                    Assert.AreEqual(expectedOn == 0, OspreyEnvironment.PrefilterSwitchesWithoutSize(@"0.1", null, false,
                        OspreyEnvironment.PrefilterSizeFor(true)), size);
                }
            }
        }

        private void AssertProteinBestArms()
        {
            // Off, two-round: no run model carries the column.
            string offDir = CreateDir(@"protein-best-off");
            string log = RunAnalysis(offDir, DataInputs(), ProteinBestVariables(), BenchArgs(offDir).Concat(ProteinBestArgs(false)).ToArray());
            AssertProteinBestLogged(log, false, ProteinBestVariables());
            var offScores = FirstPassScores(offDir);
            Assert.IsFalse(RunModels(offDir).Any(model => model.ScoresProteinBest), @"off trains no run model with the column");
            // A rerun: every output stands.
            log = RunAnalysis(offDir, DataInputs(), ProteinBestVariables(), BenchArgs(offDir).Concat(ProteinBestArgs(false)).ToArray());
            AssertNoRecompute(log);
            AssertScoresIdentical(offScores, FirstPassScores(offDir), @"off rerun");

            // On, under the default second pass: both passes, both FDRBench tables, the library.
            string onDir = CreateDir(@"protein-best-on");
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), OnArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText }).ToArray());
            AssertProteinBestLogged(log, true, ProteinBestVariables());
            string onBlib = Path.Combine(onDir, BLIB_FILE);
            AssertProteinBestAnalysis(onDir);
            string reportPath = Directory.GetFiles(onDir, @"*" + ModelDiagnosticsReport.EXT_HTML).Single();
            // No model view names the column: each run trained its own model, and the report's
            // model view is of an analysis-wide one.
            AssertHasRows(reportPath);
            var onScores = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, onScores.Count, @"every row is scored");
            AssertScoresDiffer(offScores, onScores, @"the column must move the scores");
            string expectedBlib = Path.Combine(_testDir, @"protein-best-on.blib");
            File.Copy(onBlib, expectedBlib);

            // A rerun without --protein-boost, whose default is best: every output stands.
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir));
            AssertNoRecompute(log);
            AssertScoresIdentical(onScores, FirstPassScores(onDir), @"rerun");
            AssertBlibsEqual(expectedBlib, onBlib);

            // The stand-alone model diagnostics of the completed analysis, which writes only the report.
            File.Delete(reportPath);
            RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), OnArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, OspreyTaskNames.MODEL_DIAGNOSTICS }).ToArray());
            AssertHasRows(reportPath);
            AssertScoresIdentical(onScores, FirstPassScores(onDir), @"diagnostics only");

            // A resume with the first pass on disk and the second pass deleted: PerFileRescoring
            // scores with the columns it reads back, in a process that never trained the model.
            File.Delete(onBlib);
            DeleteFiles(onDir, @"*." + FdrScoresSidecar.LABEL_SECOND_PASS + @".*");
            DeleteFiles(onDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED);
            DeleteFiles(onDir, @"*" + ReconciliationFile.EXT);
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), OnArgs(onDir));
            AssertTasks(log, new[] { FirstPassFdrTask.TASK_NAME }, new[] { PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertBlibsEqual(expectedBlib, onBlib);

            // A resume with the column off retrains each run's model rather than adopting the on
            // arm's scores.
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir).Concat(ProteinBestArgs(false)).ToArray());
            AssertTasks(log, Array.Empty<string>(), new[] { PerFileScoringTask.TASK_NAME, FirstPassFdrTask.TASK_NAME });
            AssertScoresIdentical(offScores, FirstPassScores(onDir), @"off over on");

            // Another lane count and one thread.
            string laneDir = CreateDir(@"protein-best-one-lane");
            var oneLane = new Dictionary<string, string>(ProteinBestVariables()) { [FDR_FILE_LANES] = @"1" };
            RunAnalysis(laneDir, DataInputs(), oneLane, OnArgs(laneDir));
            AssertScoresIdentical(onScores, FirstPassScores(laneDir), @"one lane");
            AssertBlibsEqual(expectedBlib, Path.Combine(laneDir, BLIB_FILE));
            string threadDir = CreateDir(@"protein-best-one-thread");
            RunProteinBestSerial(threadDir);
            AssertScoresIdentical(onScores, FirstPassScores(threadDir), @"one thread");
            AssertBlibsEqual(expectedBlib, Path.Combine(threadDir, BLIB_FILE));

            // The removed protein-compact second pass is refused at startup like any other
            // unrecognized mode, before a run is scored.
            const string removedMode = @"protein-compact";
            string compactDir = CreateDir(@"protein-best-protein-compact");
            var compact = new Dictionary<string, string>(ProteinBestVariables()) { [PASS2_QVALUE] = removedMode };
            string refused = RunOspreyExpecting(SearchArgs(compactDir, Path.Combine(_dataDir, LIBRARY_FILE), OnArgs(compactDir)),
                compact, Program.EXIT_CODE_FAILURE_TO_START);
            Assert.AreEqual(1, SplitLines(refused).Count(CommandStatusWriter.IsErrorLine), refused);
            StringAssert.Contains(refused, @"OSPREY_PASS2_QVALUE='" + removedMode + @"'");
            Assert.IsFalse(refused.Contains(TaskLine(PerFileScoringTask.TASK_NAME, @"starting")), refused);
            // A --task-files query is refused the same way, so a harness does not stage files
            // for nodes that will not start.
            string refusedQuery = RunOspreyExpecting(SearchArgs(compactDir, Path.Combine(_dataDir, LIBRARY_FILE), OnArgs(compactDir))
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, SecondPassFdrTask.TASK_NAME,
                    OspreyCommandArgs.ARG_TASK_FILES.ArgumentText }).ToArray(), compact, Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(refusedQuery, @"OSPREY_PASS2_QVALUE='" + removedMode + @"'");

            // The developer arm is a different column.
            var other = new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_SHUFFLE] = @"1" };
            RunAnalysis(onDir, DataInputs(), other, OnArgs(onDir));
            var arm = FirstPassScores(onDir);
            Assert.AreEqual(onScores.Count, arm.Count);
            AssertScoresDiffer(onScores, arm, string.Join(@" ", other.Values));
        }

        /// <summary>
        /// The linear SVM (OSPREY_FDR_MODEL=svm) with nothing else asked for but the column
        /// (<c>--protein-boost best</c>): the two-round training at its default size, a complete analysis
        /// whose run models are linear and score with the column, and whose first-pass scores differ
        /// from the analysis with the column off.
        /// </summary>
        private void AssertLinearProteinBestAnalysis()
        {
            var defaults = new Dictionary<string, string>(Verifier(false))
            {
                [PREFILTER_SIZE] = string.Empty,
                [PROTEIN_BEST_SHUFFLE] = string.Empty
            };
            string offDir = CreateDir(@"protein-best-svm-off");
            RunAnalysis(offDir, DataInputs(), defaults, ProteinBestArgs(false));
            string onDir = CreateDir(@"protein-best-svm-on");
            string log = RunAnalysis(onDir, DataInputs(), defaults, OnArgs(onDir));
            AssertProteinBestLogged(log, true, defaults);
            StringAssert.Contains(log, OspreyEnvironment.PROTEIN_BEST_PREFILTER_SIZE.ToString(CultureInfo.InvariantCulture));
            AssertProteinBestAnalysis(onDir);
            foreach (var saved in RunModels(onDir))
                Assert.IsFalse(saved.Model.IsGradientBoostedTrees, @"OSPREY_FDR_MODEL=svm trains the linear SVM");
            var offScores = FirstPassScores(offDir);
            var onScores = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, onScores.Count, @"every row is scored");
            AssertScoresDiffer(offScores, onScores, @"the column must move the linear scores");
            AssertBlibProteinBest(Path.Combine(offDir, BLIB_FILE), null);
            AssertBlibProteinBest(Path.Combine(onDir, BLIB_FILE), OspreyArgNames.VALUE_ON);
        }

        /// <summary>
        /// A complete protein-best analysis in <paramref name="workDir"/>: a library, FDRBench rows
        /// for both passes, and a model per run that loads and scores with the run's own column,
        /// whose values its stub scores parquet holds.
        /// </summary>
        private static void AssertProteinBestAnalysis(string workDir)
        {
            Assert.IsTrue(File.Exists(Path.Combine(workDir, BLIB_FILE)), @"the second pass writes the library");
            AssertHasRows(Path.Combine(workDir, @"bench.pass1.tsv"));
            AssertHasRows(Path.Combine(workDir, @"bench.pass2.tsv"));
            foreach (var saved in RunModels(workDir))
                Assert.IsTrue(saved.ScoresProteinBest, @"a run model scores with the column");
            // Each run's column values are a column of its stub scores parquet.
            foreach (string run in RUN_NAMES)
            {
                string stub = Path.Combine(workDir, run + ParquetScoreCache.EXT_SCORES);
                Assert.IsTrue(ParquetScoreCache.HasColumn(stub, @"protein_best"), stub);
            }
            AssertBlibProteinBest(Path.Combine(workDir, BLIB_FILE), OspreyArgNames.VALUE_ON);
        }

        /// <summary>Each run's own first-pass model, loaded as its second pass loads it.</summary>
        private static RunFirstPassModel[] RunModels(string workDir)
        {
            return RUN_NAMES.Select(run =>
            {
                string path = Path.Combine(workDir, run + FirstPassModelIO.EXT_MODEL);
                var model = FirstPassModelIO.LoadRunModel(path, UnitFeatures);
                Assert.IsNotNull(model, @"no run model loads from " + path);
                return model;
            }).ToArray();
        }

        /// <summary>
        /// The run log names the setting, on or off, as <see cref="OspreyEnvironment.DescribeProteinBest"/>
        /// spells it under the run's <paramref name="variables"/>.
        /// </summary>
        private static void AssertProteinBestLogged(string log, bool on, IReadOnlyDictionary<string, string> variables)
        {
            using (EnvVar.OverrideVariables(variables))
            {
                StringAssert.Contains(log, OspreyEnvironment.DescribeProteinBest(new OspreyConfig { ProteinBoost = on ? ProteinBoost.best : ProteinBoost.off }));
            }
        }

        /// <summary>The library records the column in its LibMetadata when on, and nothing when off.</summary>
        private static void AssertBlibProteinBest(string blibPath, string expected)
        {
            Assert.AreEqual(expected, BlibMetadata.Read(blibPath, OspreyBlibWriter.KEY_PROTEIN_BEST), blibPath);
        }

        /// <summary>The FDRBench arguments for both passes, the table written in <paramref name="dir"/>.</summary>
        private static string[] BenchArgs(string dir)
        {
            return new[]
            {
                OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, Path.Combine(dir, @"bench.tsv"),
                OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText, @"both"
            };
        }

        /// <summary><see cref="BenchArgs"/> with the column on, which every on arm asks for.</summary>
        private static string[] OnArgs(string dir)
        {
            return BenchArgs(dir).Concat(ProteinBestArgs(true)).ToArray();
        }

        private static string[] ProteinBestArgs(bool on)
        {
            return new[] { OspreyCommandArgs.ARG_PROTEIN_BOOST.ArgumentText, (on ? ProteinBoost.best : ProteinBoost.off).ToString() };
        }

        /// <summary>The on arm with one thread and one lane, which RunAnalysis's common arguments do not allow.</summary>
        private void RunProteinBestSerial(string workDir)
        {
            var args = InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir,
                OspreyCommandArgs.ARG_RESOLUTION.ArgumentText, @"unit",
                OspreyCommandArgs.ARG_PROTEIN_FDR.ArgumentText, @"0.01",
                OspreyCommandArgs.ARG_THREADS.ArgumentText, @"1"
            }).Concat(ProteinBestArgs(true));
            RunOsprey(args.ToArray(), new Dictionary<string, string>(ProteinBestVariables()) { [FDR_FILE_LANES] = @"1" });
        }

        private void AssertProteinBestRefusals(FdrClassifier classifier)
        {
            string workDir = CreateDir(@"protein-best-refused-" + classifier);
            string[] refused(IReadOnlyDictionary<string, string> variables, params string[] extraArgs) =>
                SplitLines(RunAnalysisExpecting(workDir, DataInputs(), LIBRARY_FILE, variables,
                    Program.EXIT_CODE_FAILURE_TO_START, extraArgs)).Where(CommandStatusWriter.IsErrorLine).ToArray();
            void assertNames(string[] errors, string name) => StringAssert.Contains(string.Join(@" ", errors), name);

            // Off, with the switch that shapes it: the baseline would run under the baseline's key.
            var off = ProteinBestArgs(false);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_SHUFFLE] = @"1" }, off), PROTEIN_BEST_SHUFFLE);
            // The environment variable the setting replaced is unknown now, so it cannot be mistaken for a lever.
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [RETIRED_PROTEIN_BEST] = @"max" }), RETIRED_PROTEIN_BEST);
            // On, without the two-round training that delivers it.
            var on = ProteinBestArgs(true);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PREFILTER_SIZE] = @"0" }, on), PREFILTER_SIZE);
        }

        /// <summary>The subset's variables with two-round training at the subset's size and the developer switch unset.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestVariables()
        {
            var variables = new Dictionary<string, string>(Verifier(false));
            foreach (var kv in ProteinBestArm())
                variables[kv.Key] = kv.Value;
            return variables;
        }

        /// <summary>Only the arm's own variables: two-round training at 2000 rows and the developer switch unset.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestArm()
        {
            return new Dictionary<string, string>
            {
                { PREFILTER_SIZE, @"2000" },
                { PROTEIN_BEST_SHUFFLE, string.Empty }
            };
        }

        /// <summary>Every first-pass row's score, by run and row, off each run's stub scores parquet.</summary>
        private static Dictionary<string, long> FirstPassScores(string workDir)
        {
            var scores = new Dictionary<string, long>();
            foreach (string run in RUN_NAMES)
            {
                // The first-pass path, which a reader serves from the stub that holds its records.
                string sidecar = Path.Combine(workDir, run + FdrScoresSidecar.EXT_FIRST_PASS);
                int row = 0;
                Assert.IsTrue(FdrScoresSidecar.ReadRecords(sidecar, FdrScoresSidecar.Pass.FirstPass, rec =>
                {
                    scores[run + @":" + row + @":" + rec.EntryId] = BitConverter.DoubleToInt64Bits(rec.Score);
                    row++;
                }), sidecar);
            }
            return scores;
        }

        private static void AssertScoresIdentical(Dictionary<string, long> expected, Dictionary<string, long> actual, string message)
        {
            Assert.AreEqual(expected.Count, actual.Count, message);
            foreach (var kvp in expected)
                Assert.AreEqual(kvp.Value, actual[kvp.Key], message + @" " + kvp.Key);
        }

        private static void AssertScoresDiffer(Dictionary<string, long> expected, Dictionary<string, long> actual, string message)
        {
            int differs = expected.Count(kvp => !actual.TryGetValue(kvp.Key, out long score) || score != kvp.Value);
            Assert.IsTrue(differs > 0, message);
        }
    }
}
