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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
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
        private const string PROTEIN_BEST = @"OSPREY_PROTEIN_BEST";
        private const string PROTEIN_BEST_DECOY = @"OSPREY_PROTEIN_BEST_DECOY";
        private const string PROTEIN_BEST_SHUFFLE = @"OSPREY_PROTEIN_BEST_SHUFFLE";
        private const string PREFILTER_SIZE = @"OSPREY_GBT_PREFILTER_SIZE";

        /// <summary>
        /// The protein-best column through the whole pipeline on the subset: a run with it completes
        /// both passes under both second-pass modes and writes FDRBench for both passes and a
        /// library, every arm moves the scores, and a rerun, another lane count, one thread, a
        /// resume with the second pass deleted, a resume missing one run's column and the four-task
        /// chain give the same bits. With it off a two-round run is untouched and writes no column.
        /// The refusals that remain cost no work. <see cref="OspreyEnvironment.FdrModel"/> is read
        /// once at class load, so the classifier is set directly, as the gbdt leg above does.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestProteinBestPipeline()
        {
            var savedFdrModel = OspreyEnvironment.FdrModel;
            try
            {
                AssertProteinBestRefusals(FdrClassifier.LinearSvm);
                AssertLinearProteinBestAnalysis();
                OspreyEnvironment.FdrModel = FdrClassifier.Gbdt;
                AssertProteinBestRefusals(FdrClassifier.Gbdt);
                AssertProteinBestArms();
                // The four-task chain under both second-pass modes: the PerFileRescoring nodes
                // score with the columns FirstPassFDR wrote, shipped to them with the rest.
                foreach (string pass2Mode in new[] { null, OspreyEnvironment.PASS2_QVALUE_TRANSFER })
                {
                    AssertHpcChainMatchesStraight(new Subset(_dataDir, LIBRARY_FILE, @"unit", RUN_NAMES, pass2Mode,
                        ProteinBestArm(@"mean-2")), @"protein-best-" + (pass2Mode ?? @"default") + @"-");
                }
            }
            finally
            {
                OspreyEnvironment.FdrModel = savedFdrModel;
            }
        }

        private void AssertProteinBestArms()
        {
            // Off, in two directories, one spelling it out: the same bits, and no column written.
            string offDir = CreateDir(@"protein-best-off");
            RunAnalysis(offDir, DataInputs(), ProteinBestVariables(null), BenchArgs(offDir));
            string explicitOffDir = CreateDir(@"protein-best-explicit-off");
            RunAnalysis(explicitOffDir, DataInputs(), ProteinBestVariables(@"off"), BenchArgs(explicitOffDir));
            var offScores = FirstPassScores(offDir);
            AssertScoresIdentical(offScores, FirstPassScores(explicitOffDir), @"off is the baseline");
            AssertBlibsEqual(Path.Combine(offDir, BLIB_FILE), Path.Combine(explicitOffDir, BLIB_FILE));
            Assert.AreEqual(0, ProteinBestColumns(offDir).Length, @"off writes no column");

            // On, under the default second pass: both passes, both FDRBench tables, the library.
            string onDir = CreateDir(@"protein-best-on");
            string log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), BenchArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText }).ToArray());
            StringAssert.Contains(log, @"Protein-best training column (EXPERIMENTAL, OSPREY_PROTEIN_BEST): mean-2, decoys paired");
            string onBlib = Path.Combine(onDir, BLIB_FILE);
            AssertProteinBestAnalysis(onDir);
            StringAssert.Contains(File.ReadAllText(Directory.GetFiles(onDir, @"*" + ModelDiagnosticsReport.EXT_HTML).Single()),
                ProteinBestFeature.FEATURE_NAME);
            var mean2 = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, mean2.Count, @"every row is scored");
            AssertScoresDiffer(offScores, mean2, @"the column must move the scores");
            string expectedBlib = Path.Combine(_testDir, @"protein-best-on.blib");
            File.Copy(onBlib, expectedBlib);

            // The same arm again: every output stands.
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), BenchArgs(onDir));
            AssertNoRecompute(log);
            AssertScoresIdentical(mean2, FirstPassScores(onDir), @"rerun");
            AssertBlibsEqual(expectedBlib, onBlib);

            // A resume with the first pass on disk and the second pass deleted: PerFileRescoring
            // scores with the columns it reads back, in a process that never trained the model.
            File.Delete(onBlib);
            DeleteFiles(onDir, @"*." + FdrScoresSidecar.LABEL_SECOND_PASS + @".*");
            DeleteFiles(onDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED);
            DeleteFiles(onDir, @"*" + ReconciliationFile.EXT);
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), BenchArgs(onDir));
            AssertTasks(log, new[] { FirstPassFdrTask.TASK_NAME }, new[] { PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertBlibsEqual(expectedBlib, onBlib);

            // A run killed after one run's column was lost: that run is scored again, from the
            // retrained model (the saved one cannot supply round 1), and the library is the same.
            File.Delete(ProteinBestColumns(onDir).First());
            File.Delete(onBlib);
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), BenchArgs(onDir));
            AssertTasks(log, Array.Empty<string>(), new[] { FirstPassFdrTask.TASK_NAME });
            AssertScoresIdentical(mean2, FirstPassScores(onDir), @"resume");
            AssertBlibsEqual(expectedBlib, onBlib);

            // Another lane count and one thread.
            string laneDir = CreateDir(@"protein-best-one-lane");
            var oneLane = new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [FDR_FILE_LANES] = @"1" };
            RunAnalysis(laneDir, DataInputs(), oneLane, BenchArgs(laneDir));
            AssertScoresIdentical(mean2, FirstPassScores(laneDir), @"one lane");
            AssertBlibsEqual(expectedBlib, Path.Combine(laneDir, BLIB_FILE));
            string threadDir = CreateDir(@"protein-best-one-thread");
            RunProteinBestSerial(threadDir);
            AssertScoresIdentical(mean2, FirstPassScores(threadDir), @"one thread");
            AssertBlibsEqual(expectedBlib, Path.Combine(threadDir, BLIB_FILE));

            // The transfer second pass: the same first pass, its own complete analysis.
            string transferDir = CreateDir(@"protein-best-transfer");
            var transfer = new Dictionary<string, string>(ProteinBestVariables(@"mean-2"))
                { [PASS2_QVALUE] = OspreyEnvironment.PASS2_QVALUE_TRANSFER };
            RunAnalysis(transferDir, DataInputs(), transfer, BenchArgs(transferDir));
            AssertProteinBestAnalysis(transferDir);
            AssertScoresIdentical(mean2, FirstPassScores(transferDir), @"transfer has the same first pass");

            // Each other arm is a different column.
            foreach (var other in new[]
                     {
                         ProteinBestVariables(@"max"),
                         new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [PROTEIN_BEST_DECOY] = @"own" },
                         new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [PROTEIN_BEST_SHUFFLE] = @"1" }
                     })
            {
                RunAnalysis(onDir, DataInputs(), other, BenchArgs(onDir));
                var arm = FirstPassScores(onDir);
                Assert.AreEqual(mean2.Count, arm.Count);
                AssertScoresDiffer(mean2, arm, string.Join(@" ", other.Values));
            }
        }

        /// <summary>
        /// The linear SVM (the default classifier) with the two-round training and the column: a
        /// complete analysis whose saved model is linear and scores with the column, and whose
        /// first-pass scores the column moves from the two-round run without it.
        /// </summary>
        private void AssertLinearProteinBestAnalysis()
        {
            string offDir = CreateDir(@"protein-best-svm-off");
            RunAnalysis(offDir, DataInputs(), ProteinBestVariables(null));
            string onDir = CreateDir(@"protein-best-svm-on");
            RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), BenchArgs(onDir));
            AssertProteinBestAnalysis(onDir);
            var saved = FirstPassModelIO.Load(Directory.GetFiles(onDir, @"*" + FirstPassModelIO.EXT_MODEL).Single());
            Assert.IsNotNull(saved);
            Assert.IsFalse(saved.Model.IsGradientBoostedTrees, @"the default classifier trains the linear SVM");
            var offScores = FirstPassScores(offDir);
            var onScores = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, onScores.Count, @"every row is scored");
            AssertScoresDiffer(offScores, onScores, @"the column must move the linear scores");
        }

        /// <summary>
        /// A complete protein-best analysis in <paramref name="workDir"/>: a library, FDRBench rows
        /// for both passes, a column per run, and a saved model that loads and scores with it.
        /// </summary>
        private static void AssertProteinBestAnalysis(string workDir)
        {
            Assert.IsTrue(File.Exists(Path.Combine(workDir, BLIB_FILE)), @"the second pass writes the library");
            AssertHasRows(Path.Combine(workDir, @"bench.pass1.tsv"));
            AssertHasRows(Path.Combine(workDir, @"bench.pass2.tsv"));
            Assert.AreEqual(RUN_NAMES.Length, ProteinBestColumns(workDir).Length, workDir);
            foreach (string column in ProteinBestColumns(workDir))
                Assert.IsNotNull(ProteinBestSidecar.Read(column), column);
            var modelFile = Directory.GetFiles(workDir, @"*" + FirstPassModelIO.EXT_MODEL).Single();
            StringAssert.Contains(File.ReadAllText(modelFile), ProteinBestFeature.FEATURE_NAME);
            var saved = FirstPassModelIO.Load(modelFile);
            Assert.IsNotNull(saved, @"a model with the column loads");
            var scorer = FrozenModelScorer.TryCreate(saved.Model);
            Assert.IsNotNull(scorer);
            Assert.IsTrue(scorer.ScoresProteinBest, @"and scores with it");
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

        private static string[] ProteinBestColumns(string workDir)
        {
            return Directory.GetFiles(workDir, @"*." + FdrScoresSidecar.LABEL_FIRST_PASS + ProteinBestSidecar.EXT);
        }

        /// <summary>The mean-2 arm with one thread and one lane, which RunAnalysis's common arguments do not allow.</summary>
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
            });
            RunOsprey(args.ToArray(), new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [FDR_FILE_LANES] = @"1" });
        }

        private void AssertProteinBestRefusals(FdrClassifier classifier)
        {
            string workDir = CreateDir(@"protein-best-refused-" + classifier);
            string[] refused(IReadOnlyDictionary<string, string> variables, params string[] extraArgs) =>
                SplitLines(RunAnalysisExpecting(workDir, DataInputs(), LIBRARY_FILE, variables,
                    Program.EXIT_CODE_FAILURE_TO_START, extraArgs)).Where(CommandStatusWriter.IsErrorLine).ToArray();
            void assertNames(string[] errors, string name) => StringAssert.Contains(string.Join(@" ", errors), name);

            // Off, with a switch that shapes it: the baseline would run under the baseline's key.
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables(null)) { [PROTEIN_BEST_DECOY] = @"own" }), PROTEIN_BEST_DECOY);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables(null)) { [PROTEIN_BEST_SHUFFLE] = @"1" }), PROTEIN_BEST_SHUFFLE);
            // A value of no arm.
            assertNames(refused(ProteinBestVariables(@"mean-99")), PROTEIN_BEST);
            // Without the two-round training that delivers it.
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables(@"max")) { [PREFILTER_SIZE] = string.Empty }), PREFILTER_SIZE);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables(@"max")) { [PROTEIN_BEST_DECOY] = @"both" }), PROTEIN_BEST_DECOY);
            // The stand-alone report, whose model views read the features without the column.
            assertNames(refused(ProteinBestVariables(@"max"), OspreyCommandArgs.ARG_TASK.ArgumentText, OspreyTaskNames.MODEL_DIAGNOSTICS),
                OspreyTaskNames.MODEL_DIAGNOSTICS);
        }

        /// <summary>The subset's variables with two-round training and the column at <paramref name="proteinBest"/>.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestVariables(string proteinBest)
        {
            var variables = new Dictionary<string, string>(Verifier(false));
            foreach (var kv in ProteinBestArm(proteinBest))
                variables[kv.Key] = kv.Value;
            return variables;
        }

        /// <summary>Only the arm's own variables: two-round training and the column at <paramref name="proteinBest"/>.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestArm(string proteinBest)
        {
            return new Dictionary<string, string>
            {
                { PREFILTER_SIZE, @"2000" },
                { PROTEIN_BEST, proteinBest ?? string.Empty },
                { PROTEIN_BEST_DECOY, string.Empty },
                { PROTEIN_BEST_SHUFFLE, string.Empty }
            };
        }

        /// <summary>Every first-pass row's score, by run file name and row, off the per-file sidecars.</summary>
        private static Dictionary<string, long> FirstPassScores(string workDir)
        {
            var scores = new Dictionary<string, long>();
            var sidecars = Directory.GetFiles(workDir, @"*.1st-pass.fdr_scores.bin");
            Assert.AreEqual(RUN_NAMES.Length, sidecars.Length, workDir);
            foreach (string sidecar in sidecars)
            {
                string run = Path.GetFileName(sidecar);
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
