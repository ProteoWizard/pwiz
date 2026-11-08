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
using System.Security.Cryptography;
using System.Text;
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
        private const string PROTEIN_BEST_DECOY = @"OSPREY_PROTEIN_BEST_DECOY";
        private const string PROTEIN_BEST_SHUFFLE = @"OSPREY_PROTEIN_BEST_SHUFFLE";
        private const string PREFILTER_SIZE = @"OSPREY_GBT_PREFILTER_SIZE";
        // The environment variable --protein-best replaced, which is now refused as unknown.
        private const string RETIRED_PROTEIN_BEST = @"OSPREY_PROTEIN_BEST";

        /// <summary>
        /// Both passes' scores and run q-values of the subset analysis with <c>--protein-best off</c>,
        /// as digested by <see cref="AnalysisDigest"/>: the value the default analysis produced at
        /// c0c2bcd43c, before the column existed, captured there with the same digest. Off must stay
        /// that analysis bit for bit; a reviewed change to the one-round first pass recaptures it.
        /// </summary>
        private const string PRE_PROTEIN_BEST_DIGEST = @"484FA69B75AF6301E5B9570419C846170FB71A2288A270D678EC1C2BFEEAF8AE";

        /// <summary>
        /// The protein-best column (<c>--protein-best</c>, on by default) through the whole pipeline
        /// on the subset: off reproduces the analysis that preceded the column bit for bit; the
        /// default is on and trains two rounds at the default size; a run with it completes both
        /// passes under both second-pass modes and writes FDRBench for both passes and a library;
        /// every arm moves the scores; and a rerun, another lane count, one thread, a resume with
        /// the second pass deleted, a resume missing one run's column, the stand-alone model
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
                AssertProteinBestOffIsPreColumnAnalysis();
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
                        ProteinBestArm()), @"protein-best-" + (pass2Mode ?? @"default") + @"-");
                }
            }
            finally
            {
                OspreyEnvironment.FdrModel = savedFdrModel;
            }
        }

        /// <summary>
        /// <c>--protein-best off</c>, with no two-round training asked for, is the one-round first
        /// pass that was the default before the column: the same scores and q-values in both passes,
        /// to the bit, as <see cref="PRE_PROTEIN_BEST_DIGEST"/> pins, and no column written.
        /// </summary>
        private void AssertProteinBestOffIsPreColumnAnalysis()
        {
            string offDir = CreateDir(@"protein-best-pre-column");
            var variables = new Dictionary<string, string>(Verifier(false)) { [PREFILTER_SIZE] = string.Empty };
            string log = RunAnalysis(offDir, DataInputs(), variables, ProteinBestArgs(false));
            AssertProteinBestLogged(log, false, variables);
            Assert.AreEqual(0, ProteinBestColumns(offDir).Length, @"off writes no column");
            Assert.AreEqual(PRE_PROTEIN_BEST_DIGEST, AnalysisDigest(offDir), @"off is the analysis before the column");
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
            // Off, two-round: no column written.
            string offDir = CreateDir(@"protein-best-off");
            RunAnalysis(offDir, DataInputs(), ProteinBestVariables(), BenchArgs(offDir).Concat(ProteinBestArgs(false)).ToArray());
            var offScores = FirstPassScores(offDir);
            Assert.AreEqual(0, ProteinBestColumns(offDir).Length, @"off writes no column");

            // On by default, under the default second pass: both passes, both FDRBench tables, the library.
            string onDir = CreateDir(@"protein-best-on");
            string log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_MODEL_DIAGNOSTICS.ArgumentText }).ToArray());
            AssertProteinBestLogged(log, true, ProteinBestVariables());
            string onBlib = Path.Combine(onDir, BLIB_FILE);
            AssertProteinBestAnalysis(onDir);
            string reportPath = Directory.GetFiles(onDir, @"*" + ModelDiagnosticsReport.EXT_HTML).Single();
            StringAssert.Contains(File.ReadAllText(reportPath), ProteinBestFeature.FEATURE_NAME);
            var onScores = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, onScores.Count, @"every row is scored");
            AssertScoresDiffer(offScores, onScores, @"the column must move the scores");
            string expectedBlib = Path.Combine(_testDir, @"protein-best-on.blib");
            File.Copy(onBlib, expectedBlib);

            // Spelled out, on is the default: every output stands.
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir).Concat(ProteinBestArgs(true)).ToArray());
            AssertNoRecompute(log);
            AssertScoresIdentical(onScores, FirstPassScores(onDir), @"rerun");
            AssertBlibsEqual(expectedBlib, onBlib);

            // The stand-alone model diagnostics of the completed analysis, which writes only the report.
            File.Delete(reportPath);
            RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir)
                .Concat(new[] { OspreyCommandArgs.ARG_TASK.ArgumentText, OspreyTaskNames.MODEL_DIAGNOSTICS }).ToArray());
            StringAssert.Contains(File.ReadAllText(reportPath), ProteinBestFeature.FEATURE_NAME);
            AssertScoresIdentical(onScores, FirstPassScores(onDir), @"diagnostics only");

            // A resume with the first pass on disk and the second pass deleted: PerFileRescoring
            // scores with the columns it reads back, in a process that never trained the model.
            File.Delete(onBlib);
            DeleteFiles(onDir, @"*." + FdrScoresSidecar.LABEL_SECOND_PASS + @".*");
            DeleteFiles(onDir, @"*" + ParquetScoreCache.EXT_SCORES_RECONCILED);
            DeleteFiles(onDir, @"*" + ReconciliationFile.EXT);
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir));
            AssertTasks(log, new[] { FirstPassFdrTask.TASK_NAME }, new[] { PerFileRescoreTask.TASK_NAME, SecondPassFdrTask.TASK_NAME });
            AssertBlibsEqual(expectedBlib, onBlib);

            // A run killed after one run's column was lost: that run is scored again, from the
            // retrained model (the saved one cannot supply round 1), and the library is the same.
            File.Delete(ProteinBestColumns(onDir).First());
            File.Delete(onBlib);
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir));
            AssertTasks(log, Array.Empty<string>(), new[] { FirstPassFdrTask.TASK_NAME });
            AssertScoresIdentical(onScores, FirstPassScores(onDir), @"resume");
            AssertBlibsEqual(expectedBlib, onBlib);

            // A resume with the column turned off retrains rather than adopting the on arm's outputs.
            log = RunAnalysis(onDir, DataInputs(), ProteinBestVariables(), BenchArgs(onDir).Concat(ProteinBestArgs(false)).ToArray());
            AssertTasks(log, Array.Empty<string>(), new[] { FirstPassFdrTask.TASK_NAME });
            AssertScoresIdentical(offScores, FirstPassScores(onDir), @"off over on");

            // Another lane count and one thread.
            string laneDir = CreateDir(@"protein-best-one-lane");
            var oneLane = new Dictionary<string, string>(ProteinBestVariables()) { [FDR_FILE_LANES] = @"1" };
            RunAnalysis(laneDir, DataInputs(), oneLane, BenchArgs(laneDir));
            AssertScoresIdentical(onScores, FirstPassScores(laneDir), @"one lane");
            AssertBlibsEqual(expectedBlib, Path.Combine(laneDir, BLIB_FILE));
            string threadDir = CreateDir(@"protein-best-one-thread");
            RunProteinBestSerial(threadDir);
            AssertScoresIdentical(onScores, FirstPassScores(threadDir), @"one thread");
            AssertBlibsEqual(expectedBlib, Path.Combine(threadDir, BLIB_FILE));

            // The transfer second pass: the same first pass, its own complete analysis.
            string transferDir = CreateDir(@"protein-best-transfer");
            var transfer = new Dictionary<string, string>(ProteinBestVariables())
                { [PASS2_QVALUE] = OspreyEnvironment.PASS2_QVALUE_TRANSFER };
            RunAnalysis(transferDir, DataInputs(), transfer, BenchArgs(transferDir));
            AssertProteinBestAnalysis(transferDir);
            AssertScoresIdentical(onScores, FirstPassScores(transferDir), @"transfer has the same first pass");

            // Each developer arm is a different column.
            foreach (var other in new[]
                     {
                         new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_DECOY] = OspreyEnvironment.PROTEIN_BEST_DECOY_OWN },
                         new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_SHUFFLE] = @"1" }
                     })
            {
                RunAnalysis(onDir, DataInputs(), other, BenchArgs(onDir));
                var arm = FirstPassScores(onDir);
                Assert.AreEqual(onScores.Count, arm.Count);
                AssertScoresDiffer(onScores, arm, string.Join(@" ", other.Values));
            }
        }

        /// <summary>
        /// The linear SVM (the default classifier) with nothing asked for but the defaults: the column
        /// on, the two-round training at its default size, a complete analysis whose saved model is
        /// linear and scores with the column, and whose first-pass scores differ from the analysis
        /// with the column off.
        /// </summary>
        private void AssertLinearProteinBestAnalysis()
        {
            var defaults = new Dictionary<string, string>(Verifier(false))
            {
                [PREFILTER_SIZE] = string.Empty,
                [PROTEIN_BEST_DECOY] = string.Empty,
                [PROTEIN_BEST_SHUFFLE] = string.Empty
            };
            string offDir = CreateDir(@"protein-best-svm-off");
            RunAnalysis(offDir, DataInputs(), defaults, ProteinBestArgs(false));
            string onDir = CreateDir(@"protein-best-svm-on");
            string log = RunAnalysis(onDir, DataInputs(), defaults, BenchArgs(onDir));
            AssertProteinBestLogged(log, true, defaults);
            StringAssert.Contains(log, OspreyEnvironment.PROTEIN_BEST_PREFILTER_SIZE.ToString(CultureInfo.InvariantCulture));
            AssertProteinBestAnalysis(onDir);
            var saved = FirstPassModelIO.Load(Directory.GetFiles(onDir, @"*" + FirstPassModelIO.EXT_MODEL).Single());
            Assert.IsNotNull(saved);
            Assert.IsFalse(saved.Model.IsGradientBoostedTrees, @"the default classifier trains the linear SVM");
            var offScores = FirstPassScores(offDir);
            var onScores = FirstPassScores(onDir);
            Assert.AreEqual(offScores.Count, onScores.Count, @"every row is scored");
            AssertScoresDiffer(offScores, onScores, @"the column must move the linear scores");
            AssertBlibProteinBest(Path.Combine(offDir, BLIB_FILE), null);
            AssertBlibProteinBest(Path.Combine(onDir, BLIB_FILE), OspreyArgNames.VALUE_ON);
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
            AssertBlibProteinBest(Path.Combine(workDir, BLIB_FILE), OspreyArgNames.VALUE_ON);
        }

        /// <summary>
        /// The run log names the setting, on or off, as <see cref="OspreyEnvironment.DescribeProteinBest"/>
        /// spells it under the run's <paramref name="variables"/>.
        /// </summary>
        private static void AssertProteinBestLogged(string log, bool on, IReadOnlyDictionary<string, string> variables)
        {
            using (EnvVar.OverrideVariables(variables))
            {
                StringAssert.Contains(log, OspreyEnvironment.DescribeProteinBest(new OspreyConfig { ProteinBest = on }));
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

        private static string[] ProteinBestArgs(bool on)
        {
            return new[] { OspreyCommandArgs.ARG_PROTEIN_BEST.ArgumentText, on ? OspreyArgNames.VALUE_ON : OspreyArgNames.VALUE_OFF };
        }

        private static string[] ProteinBestColumns(string workDir)
        {
            return Directory.GetFiles(workDir, @"*." + FdrScoresSidecar.LABEL_FIRST_PASS + ProteinBestSidecar.EXT);
        }

        /// <summary>The default arm with one thread and one lane, which RunAnalysis's common arguments do not allow.</summary>
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
            RunOsprey(args.ToArray(), new Dictionary<string, string>(ProteinBestVariables()) { [FDR_FILE_LANES] = @"1" });
        }

        private void AssertProteinBestRefusals(FdrClassifier classifier)
        {
            string workDir = CreateDir(@"protein-best-refused-" + classifier);
            string[] refused(IReadOnlyDictionary<string, string> variables, params string[] extraArgs) =>
                SplitLines(RunAnalysisExpecting(workDir, DataInputs(), LIBRARY_FILE, variables,
                    Program.EXIT_CODE_FAILURE_TO_START, extraArgs)).Where(CommandStatusWriter.IsErrorLine).ToArray();
            void assertNames(string[] errors, string name) => StringAssert.Contains(string.Join(@" ", errors), name);

            // Off, with a switch that shapes it: the baseline would run under the baseline's key.
            var off = ProteinBestArgs(false);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_DECOY] = OspreyEnvironment.PROTEIN_BEST_DECOY_OWN }, off), PROTEIN_BEST_DECOY);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_SHUFFLE] = @"1" }, off), PROTEIN_BEST_SHUFFLE);
            // The environment variable the setting replaced is unknown now, so it cannot be mistaken for a lever.
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [RETIRED_PROTEIN_BEST] = @"max" }), RETIRED_PROTEIN_BEST);
            // Without the two-round training that delivers it.
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PREFILTER_SIZE] = @"0" }), PREFILTER_SIZE);
            assertNames(refused(new Dictionary<string, string>(ProteinBestVariables()) { [PROTEIN_BEST_DECOY] = @"both" }), PROTEIN_BEST_DECOY);
        }

        /// <summary>The subset's variables with two-round training at the subset's size and the developer switches unset.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestVariables()
        {
            var variables = new Dictionary<string, string>(Verifier(false));
            foreach (var kv in ProteinBestArm())
                variables[kv.Key] = kv.Value;
            return variables;
        }

        /// <summary>Only the arm's own variables: two-round training at 2000 rows and the developer switches unset.</summary>
        private static IReadOnlyDictionary<string, string> ProteinBestArm()
        {
            return new Dictionary<string, string>
            {
                { PREFILTER_SIZE, @"2000" },
                { PROTEIN_BEST_DECOY, string.Empty },
                { PROTEIN_BEST_SHUFFLE, string.Empty }
            };
        }

        /// <summary>
        /// Every first-pass and second-pass record's entry id, score and run q-values, run by run in
        /// file-name order, hashed: what the analysis concluded, without the provenance stamps.
        /// </summary>
        private static string AnalysisDigest(string workDir)
        {
            var text = new StringBuilder();
            foreach (var pass in new[] { FdrScoresSidecar.Pass.FirstPass, FdrScoresSidecar.Pass.SecondPass })
            {
                string label = pass == FdrScoresSidecar.Pass.FirstPass
                    ? FdrScoresSidecar.LABEL_FIRST_PASS
                    : FdrScoresSidecar.LABEL_SECOND_PASS;
                var sidecars = Directory.GetFiles(workDir, @"*." + label + @".fdr_scores.bin")
                    .OrderBy(Path.GetFileName, StringComparer.Ordinal).ToArray();
                Assert.AreEqual(RUN_NAMES.Length, sidecars.Length, workDir + @" " + label);
                foreach (string sidecar in sidecars)
                {
                    text.Append(Path.GetFileName(sidecar)).Append('\n');
                    Assert.IsTrue(FdrScoresSidecar.ReadRecords(sidecar, pass, rec => text.Append(rec.EntryId).Append(' ')
                        .Append(BitConverter.DoubleToInt64Bits(rec.Score)).Append(' ')
                        .Append(BitConverter.DoubleToInt64Bits(rec.RunPrecursorQvalue)).Append(' ')
                        .Append(BitConverter.DoubleToInt64Bits(rec.RunPeptideQvalue)).Append('\n')), sidecar);
                }
            }
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace(@"-", string.Empty);
            }
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
