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
        /// The protein-best column through the real first pass on the subset: a run with it completes
        /// its first pass and FDRBench pass 1 and writes no library, every arm moves the scores, a
        /// rerun and another lane count give the same bits, and with it off a two-round run is
        /// untouched. The refusals cost no work. <see cref="OspreyEnvironment.FdrModel"/> is read
        /// once at class load, so the classifier is set directly, as the gbdt leg above does.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestProteinBestFirstPass()
        {
            var savedFdrModel = OspreyEnvironment.FdrModel;
            try
            {
                AssertProteinBestRefusals(FdrClassifier.LinearSvm);
                OspreyEnvironment.FdrModel = FdrClassifier.Gbdt;
                AssertProteinBestRefusals(FdrClassifier.Gbdt);
                AssertProteinBestArms();
            }
            finally
            {
                OspreyEnvironment.FdrModel = savedFdrModel;
            }
        }

        private void AssertProteinBestArms()
        {
            string[] benchArgs(string dir) => new[]
            {
                OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, Path.Combine(dir, @"bench.tsv"),
                OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText, @"1"
            };

            // Off, in two directories, one spelling it out: the same bits, a complete analysis.
            string offDir = CreateDir(@"protein-best-off");
            RunAnalysis(offDir, DataInputs(), ProteinBestVariables(null), benchArgs(offDir));
            Assert.IsTrue(File.Exists(Path.Combine(offDir, BLIB_FILE)), @"off runs the whole pipeline");
            string explicitOffDir = CreateDir(@"protein-best-explicit-off");
            RunAnalysis(explicitOffDir, DataInputs(), ProteinBestVariables(@"off"), benchArgs(explicitOffDir));
            var offScores = FirstPassScores(offDir);
            AssertScoresIdentical(offScores, FirstPassScores(explicitOffDir), @"off is the baseline");

            // On, all arms in one directory: FirstPassFDR is rekeyed each time and Stages 1-4 resume.
            string onDir = CreateDir(@"protein-best-on");
            string log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), benchArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText }).ToArray());
            StringAssert.Contains(log, @"Protein-best training column (EXPERIMENTAL, OSPREY_PROTEIN_BEST): mean-2, decoys paired");
            Assert.IsFalse(File.Exists(Path.Combine(onDir, BLIB_FILE)), @"no second pass, so no library");
            AssertHasRows(Path.Combine(onDir, @"bench.tsv"));
            var modelFile = Directory.GetFiles(onDir, @"*.1st-pass.model.json").Single();
            StringAssert.Contains(File.ReadAllText(modelFile), ProteinBestFeature.FEATURE_NAME);
            StringAssert.Contains(File.ReadAllText(Directory.GetFiles(onDir, @"*" + ModelDiagnosticsReport.EXT_HTML).Single()),
                ProteinBestFeature.FEATURE_NAME);
            var mean2 = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, mean2.Count, @"every row is scored");
            AssertScoresDiffer(offScores, mean2, @"the column must move the scores");

            // The saved model cannot be loaded: it has a column raw vectors lack. A fresh process,
            // which has no first-pass results in memory, cannot be handed it for a second pass.
            Assert.IsNull(FirstPassModelIO.Load(modelFile), @"a model with the column is refused by the reader");

            // The same arm again: the same outputs stand, and a file of another lane count agrees.
            RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), benchArgs(onDir));
            AssertScoresIdentical(mean2, FirstPassScores(onDir), @"rerun");
            // A run killed after one file: its other files' scores stand, and the retrained model
            // (the saved one cannot supply the column) scores the missing file as it was.
            File.Delete(Directory.GetFiles(onDir, @"*.1st-pass.fdr_scores.bin").First());
            RunAnalysis(onDir, DataInputs(), ProteinBestVariables(@"mean-2"), benchArgs(onDir));
            AssertScoresIdentical(mean2, FirstPassScores(onDir), @"resume");
            string laneDir = CreateDir(@"protein-best-one-lane");
            var oneLane = new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [FDR_FILE_LANES] = @"1" };
            RunAnalysis(laneDir, DataInputs(), oneLane, benchArgs(laneDir));
            AssertScoresIdentical(mean2, FirstPassScores(laneDir), @"one lane");
            string threadDir = CreateDir(@"protein-best-one-thread");
            RunProteinBestSerial(threadDir);
            AssertScoresIdentical(mean2, FirstPassScores(threadDir), @"one thread");

            // The same arm as the two HPC tasks the column allows: the lean FirstPassFDR library
            // still carries the accessions, and the scores are the straight run's.
            string chainDir = CreateDir(@"protein-best-chain");
            RunAnalysis(chainDir, DataInputs(), ProteinBestVariables(@"mean-2"),
                OspreyCommandArgs.ARG_TASK.ArgumentText, OspreyTaskNames.PER_FILE_SCORING);
            RunAnalysis(chainDir, DataInputs(), ProteinBestVariables(@"mean-2"), benchArgs(chainDir)
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, OspreyTaskNames.FIRST_PASS_FDR }).ToArray());
            AssertHasRows(Path.Combine(chainDir, @"bench.tsv"));
            AssertScoresIdentical(mean2, FirstPassScores(chainDir), @"HPC chain");

            // Each other arm is a different column.
            foreach (var other in new[]
                     {
                         ProteinBestVariables(@"max"),
                         new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [PROTEIN_BEST_DECOY] = @"own" },
                         new Dictionary<string, string>(ProteinBestVariables(@"mean-2")) { [PROTEIN_BEST_SHUFFLE] = @"1" }
                     })
            {
                RunAnalysis(onDir, DataInputs(), other, benchArgs(onDir));
                var arm = FirstPassScores(onDir);
                Assert.AreEqual(mean2.Count, arm.Count);
                AssertScoresDiffer(mean2, arm, string.Join(@" ", other.Values));
            }
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
            if (classifier != FdrClassifier.Gbdt)
            {
                assertNames(refused(ProteinBestVariables(@"max")), @"OSPREY_FDR_MODEL");
                return;
            }
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables(@"max")) { [PROTEIN_BEST_DECOY] = @"both" }), PROTEIN_BEST_DECOY);
            // Past the only stage that can supply the column, and the table that needs it.
            foreach (string task in new[] { OspreyTaskNames.PER_FILE_RESCORING, OspreyTaskNames.SECOND_PASS_FDR, OspreyTaskNames.MODEL_DIAGNOSTICS })
                assertNames(refused(ProteinBestVariables(@"max"), OspreyCommandArgs.ARG_TASK.ArgumentText, task), task);
            assertNames(refused(ProteinBestVariables(@"max"), OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, Path.Combine(workDir, @"bench.tsv")),
                OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText);
            assertNames(refused(ProteinBestVariables(@"max"), OspreyCommandArgs.ARG_FDRBENCH.ArgumentText, Path.Combine(workDir, @"bench.tsv"),
                OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText, @"both"), OspreyCommandArgs.ARG_FDRBENCH_PASS.ArgumentText);
        }

        /// <summary>The subset's variables with two-round training and the column at <paramref name="proteinBest"/>.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestVariables(string proteinBest)
        {
            return new Dictionary<string, string>(Verifier(false))
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
