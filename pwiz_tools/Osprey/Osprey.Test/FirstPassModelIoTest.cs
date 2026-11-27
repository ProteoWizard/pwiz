/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using pwiz.Common.Pipeline;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.ML;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Round-trip tests for <see cref="FirstPassModelIO"/>: a reloaded 1st-pass model
    /// (the file PerFileRescoring reads to score a run's moved rows) must score
    /// BIT-IDENTICALLY to the in-process original. Without bit-parity a rescore in another
    /// process would report different q-values than a straight-through run.
    /// </summary>
    [TestClass]
    public class FirstPassModelIoTest
    {
        /// <summary>
        /// The pinned file below, opened by the validity stamp it was written with - the first
        /// property, ahead of everything the pin covers.
        /// </summary>
        private static string PinnedLinearModelFile(ArtifactStamp stamp)
        {
            return "{\n  \"" + ArtifactStamp.JSON_PROPERTY + "\": " + Newtonsoft.Json.JsonConvert.ToString(stamp.ToString()) +
                   ",\n" + PINNED_LINEAR_MODEL_FILE.Substring("{\n".Length);
        }

        /// <summary>The file <see cref="AssertLinearFileBytesArePinned"/> expects: LF, two-space
        /// indent, round-trip doubles, a trailing newline, no tree property, and the calibration
        /// map of <see cref="SaveModel"/> last.</summary>
        private const string PINNED_LINEAR_MODEL_FILE =
            "{\n" +
            "  \"SchemaVersion\": 1,\n" +
            "  \"NumFeatures\": 2,\n" +
            "  \"Means\": [\n" +
            "    0.5,\n" +
            "    -1.25\n" +
            "  ],\n" +
            "  \"Stds\": [\n" +
            "    2,\n" +
            "    0.1\n" +
            "  ],\n" +
            "  \"FoldWeights\": [\n" +
            "    [\n" +
            "      1.5,\n" +
            "      -0.25\n" +
            "    ],\n" +
            "    [\n" +
            "      0.3333333333333333,\n" +
            "      2\n" +
            "    ]\n" +
            "  ],\n" +
            "  \"FoldBiases\": [\n" +
            "    0.125,\n" +
            "    -3\n" +
            "  ],\n" +
            "  \"ExperimentAgg\": \"max\",\n" +
            "  \"CalibrationThreshold\": 0.75,\n" +
            "  \"CalibrationDenominator\": 2.5\n" +
            "}\n";

        // The calibration map every model these tests save carries, and its form in a file.
        private const double CALIBRATION_THRESHOLD = 0.75;
        private const double CALIBRATION_DENOMINATOR = 2.5;
        private const string CALIBRATION_JSON = @", ""CalibrationThreshold"": 0.75, ""CalibrationDenominator"": 2.5 }";

        /// <summary>
        /// Save <paramref name="model"/> as a run's own model file, the only form a model is
        /// written in, with a fixed calibration map. False when the model is declined.
        /// </summary>
        internal static bool SaveModel(string path, PercolatorResults model)
        {
            return FirstPassModelIO.SaveRunModel(path,
                new RunFirstPassModel(model, CALIBRATION_THRESHOLD, CALIBRATION_DENOMINATOR, null, -0.25), TestStamps.Any);
        }

        /// <summary>The model of the run model file at <paramref name="path"/>, or null when it does not load.</summary>
        internal static PercolatorResults LoadModel(string path)
        {
            return FirstPassModelIO.LoadRunModel(path, PinFeatureMask.Hram)?.Model;
        }

        private static PercolatorResults MakeSvmModel()
        {
            // Deliberately awkward doubles (long mantissas, negatives, tiny/huge) so a
            // lossy serializer would visibly break the round-trip. Negative zero is
            // intentionally NOT tested: JSON normalizes -0.0 to +0.0, which is harmless
            // here (means/weights only feed subtraction and multiplication, where the two
            // zeros are interchangeable), but would fail a raw bit-parity assertion.
            var means = new[] { 0.1234567890123456, -12.98765432109876, 1e-9, 42.0, 0.0009765625 };
            var stds = new[] { 1.4142135623730951, 2.718281828459045, 3.141592653589793, 0.5, 1e6 };
            return new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(means, stds),
                FoldWeights = new List<double[]>
                {
                    new[] { 0.9, -1.1, 2.2222222222222223, -3.3, 0.0001 },
                    new[] { -0.5, 1.25, -2.5, 3.75, -1e-8 },
                    new[] { 1e-12, -1e12, 0.333333333333333, -0.6666666666666666, 7.0 },
                },
                FoldBiases = new List<double> { 0.123456789, -9.87654321, 1e-7 },
            };
        }

        /// <summary>
        /// A three-fold tree model on a fixed synthetic population: what a
        /// <c>OSPREY_FDR_MODEL=gbdt</c> first pass publishes, without the Percolator loop around it.
        /// Internal so the pass-2 transfer test scores with the same model.
        /// </summary>
        internal static PercolatorResults MakeTreeModel()
        {
            return new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(
                    new[] { 0.5, 0.6180339887498949, 0.75, 0.8660254037844386, 1.0 },
                    new[] { 0.2886751345948129, 0.3, 0.3333333333333333, 0.35, 0.4 }),
                FoldGbtModels = TrainTreeFolds(5, GbtObjective.LogisticBinary),
            };
        }

        /// <summary>Three fold ensembles over <paramref name="nFeatures"/> features of a fixed
        /// synthetic population, fit under <paramref name="objective"/> - the log-odds objective
        /// the first pass uses, or another one a model file must not carry.</summary>
        private static List<GradientBoostedTrees> TrainTreeFolds(int nFeatures, GbtObjective objective)
        {
            var rng = new Random(11);   // Fixed seed: a fixed population, not a sampled one
            var rows = new double[200][];
            var y = new double[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                bool isDecoy = i % 2 == 1;
                y[i] = isDecoy ? 0.0 : 1.0;
                rows[i] = new double[nFeatures];
                for (int j = 0; j < nFeatures; j++)
                    rows[i][j] = rng.NextDouble() + (isDecoy ? 0.0 : 0.25 * j);
            }
            var folds = new List<GradientBoostedTrees>();
            for (ulong seed = 1; seed <= 3; seed++)
            {
                folds.Add(GradientBoostedTrees.Train(rows, y,
                    new GbtParams { NTrees = 20, MaxDepth = 3, Seed = seed, Objective = objective }));
            }
            return folds;
        }

        private static void AssertBitEqual(double expected, double actual, string what)
        {
            Assert.AreEqual(
                BitConverter.DoubleToInt64Bits(expected),
                BitConverter.DoubleToInt64Bits(actual),
                what + string.Format(@" (expected {0:R}, got {1:R})", expected, actual));
        }

        [TestMethod]
        public void TestFirstPassModelRoundTripScoresBitIdentical()
        {
            var model = MakeSvmModel();
            var scorerBefore = FrozenModelScorer.TryCreate(model);
            Assert.IsNotNull(scorerBefore, @"original model should produce a scorer");

            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_roundtrip_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsTrue(SaveModel(path, model), @"SVM model should persist");
                Assert.IsTrue(File.Exists(path), @"sidecar should exist after Save");

                var reloaded = LoadModel(path);
                Assert.IsNotNull(reloaded, @"reloaded model should not be null");

                // The file still records the experiment-wide aggregation, as it did when
                // OSPREY_EXPERIMENT_AGG could choose another, so its bytes are unchanged.
                Assert.AreEqual(@"max", (string) JObject.Parse(File.ReadAllText(path))[@"ExperimentAgg"],
                    @"recorded pass-1 aggregation");

                AssertLinearFileBytesArePinned();

                // Structural bit-parity.
                Assert.AreEqual(model.Standardizer.NumFeatures, reloaded.Standardizer.NumFeatures, @"NumFeatures");
                for (int i = 0; i < model.Standardizer.Means.Length; i++)
                {
                    AssertBitEqual(model.Standardizer.Means[i], reloaded.Standardizer.Means[i], @"Means[" + i + @"]");
                    AssertBitEqual(model.Standardizer.Stds[i], reloaded.Standardizer.Stds[i], @"Stds[" + i + @"]");
                }
                Assert.AreEqual(model.FoldWeights.Count, reloaded.FoldWeights.Count, @"fold count");
                for (int f = 0; f < model.FoldWeights.Count; f++)
                {
                    AssertBitEqual(model.FoldBiases[f], reloaded.FoldBiases[f], @"FoldBiases[" + f + @"]");
                    for (int j = 0; j < model.FoldWeights[f].Length; j++)
                        AssertBitEqual(model.FoldWeights[f][j], reloaded.FoldWeights[f][j], @"FoldWeights[" + f + @"][" + j + @"]");
                }

                // The contract that actually matters: identical scores through the scorer.
                var scorerAfter = FrozenModelScorer.TryCreate(reloaded);
                Assert.IsNotNull(scorerAfter, @"reloaded model should produce a scorer");
                var rows = new[]
                {
                    new[] { 1.0, 2.0, 3.0, 4.0, 5.0 },
                    new[] { -3.14, 0.0, 100.0, -1e-6, 2.5 },
                    new[] { 0.0, 0.0, 0.0, 0.0, 0.0 },
                    new[] { 1e9, -1e9, 1e-9, -1e-9, 12.5 },
                };
                foreach (var row in rows)
                    AssertBitEqual(scorerBefore.Score(row), scorerAfter.Score(row), @"Score");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// A <c>OSPREY_FDR_MODEL=gbdt</c> model must persist, reload as the tree ensemble and score
        /// BIT-IDENTICALLY, for the reason the SVM must: PerFileRescoring reads the model from
        /// this file rather than from the process that trained it.
        ///
        /// <para>Also pins the linear side: its file gains no tree property at all, so the
        /// default path's artifact is byte-for-byte what it was.</para>
        /// </summary>
        [TestMethod]
        public void TestFirstPassModelTreeRoundTripScoresBitIdentical()
        {
            var model = MakeTreeModel();
            var scorerBefore = FrozenModelScorer.TryCreate(model);
            Assert.IsNotNull(scorerBefore, @"original tree model should produce a scorer");
            Assert.IsTrue(scorerBefore.IsGradientBoostedTrees);

            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_trees_" + Guid.NewGuid().ToString(@"N") + @".json");
            string linearPath = Path.Combine(Path.GetTempPath(),
                @"osprey_model_linear_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsTrue(SaveModel(path, model), @"tree model should persist");
                var reloaded = LoadModel(path);
                Assert.IsNotNull(reloaded, @"reloaded tree model should not be null");
                Assert.IsNotNull(reloaded.FoldGbtModels, @"a tree model must reload as trees");
                Assert.AreEqual(model.FoldGbtModels.Count, reloaded.FoldGbtModels.Count, @"fold count");
                Assert.AreEqual(0, reloaded.FoldWeights.Count, @"a tree model carries no linear weights");

                // The contract that matters: identical scores through the scorer, over rows that
                // reach many different leaves.
                var scorerAfter = FrozenModelScorer.TryCreate(reloaded);
                Assert.IsNotNull(scorerAfter, @"reloaded tree model should produce a scorer");
                Assert.IsTrue(scorerAfter.IsGradientBoostedTrees);
                var rng = new Random(5);
                for (int i = 0; i < 50; i++)
                {
                    var row = new double[scorerBefore.NumFeatures];
                    for (int j = 0; j < row.Length; j++)
                        row[j] = 2.0 * rng.NextDouble() - 0.5;
                    AssertBitEqual(scorerBefore.Score(row), scorerAfter.Score(row), @"tree Score");
                }

                // A tree file that cannot be scored as written loads as null, like any other.
                var written = JObject.Parse(File.ReadAllText(path));
                AssertLoadsNull(Edited(written, json =>
                {
                    json[@"FoldWeights"] = new JArray(new JArray(0.5, 0.5, 0.5, 0.5, 0.5));
                    json[@"FoldBiases"] = new JArray(0.0);
                }), @"trees beside linear weights");
                AssertLoadsNull(Edited(written, json => FirstTree(json)[@"FeatureCount"] = 4),
                    @"tree width != feature count");
                AssertLoadsNull(Edited(written, json =>
                {
                    var left = FirstTree(json)[@"Left"] as JArray;
                    Assert.IsNotNull(left, @"a written tree should carry its Left child array");
                    left[0] = 0;
                }), @"tree node graph that would cycle");

                // Save refuses what Load refuses, rather than stamping a file that every reader
                // turns into null: the stamp inside it would attest a model nobody can load.
                AssertSaveDeclines(new PercolatorResults
                {
                    Standardizer = model.Standardizer,
                    FoldGbtModels = TrainTreeFolds(4, GbtObjective.LogisticBinary)
                }, @"trees narrower than the standardizer");
                AssertSaveDeclines(new PercolatorResults
                {
                    Standardizer = model.Standardizer,
                    FoldGbtModels = TrainTreeFolds(5, GbtObjective.SquaredError)
                }, @"trees fit under squared error");
                AssertSaveDeclines(new PercolatorResults
                {
                    Standardizer = model.Standardizer,
                    FoldGbtModels = new List<GradientBoostedTrees> { model.FoldGbtModels[0], null }
                }, @"a fold with no ensemble");

                Assert.IsTrue(SaveModel(linearPath, MakeSvmModel()), @"SVM model should persist");
                Assert.IsNull(JObject.Parse(File.ReadAllText(linearPath))[@"FoldGbtModels"],
                    @"a linear model's file must not gain a tree property");

                AssertRunModelRoundTrip(model, path);
                AssertRunModelRoundTrip(MakeSvmModel(), path);
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(linearPath))
                    File.Delete(linearPath);
            }
        }

        /// <summary>
        /// A run's own model file (per-run first-pass models) round-trips the model and its
        /// calibration map: the reloaded model scores bit-identically and calibrates through the
        /// same map. A file with no calibration map does not load as a run's model, nor does a
        /// map that could not be applied.
        /// </summary>
        private static void AssertRunModelRoundTrip(PercolatorResults model, string path)
        {
            Assert.IsTrue(FirstPassModelIO.SaveRunModel(path, new RunFirstPassModel(model, 0.75, 2.5, null, -0.25),
                TestStamps.Any));
            var reloaded = FirstPassModelIO.LoadRunModel(path, PinFeatureMask.Hram);
            Assert.IsNotNull(reloaded, @"a run's model should reload");
            AssertBitEqual(0.75, reloaded.CalibrationThreshold, @"calibration threshold");
            AssertBitEqual(2.5, reloaded.CalibrationDenominator, @"calibration denominator");
            Assert.IsFalse(reloaded.ScoresProteinBest);
            Assert.IsNull(JObject.Parse(File.ReadAllText(path))[@"ProteinBestFloor"],
                @"a model without the protein-best column records no floor");
            var before = FrozenModelScorer.TryCreate(model);
            var after = FrozenModelScorer.TryCreate(reloaded.Model);
            Assert.IsNotNull(before);
            Assert.IsNotNull(after);
            var rng = new Random(11);
            for (int i = 0; i < 20; i++)
            {
                var row = new double[before.NumFeatures];
                for (int j = 0; j < row.Length; j++)
                    row[j] = 2.0 * rng.NextDouble() - 0.5;
                double raw = before.Score(row);
                AssertBitEqual(raw, after.Score(row), @"run model Score");
                AssertBitEqual((raw - 0.75) / 2.5, reloaded.Calibrate(after.Score(row)), @"calibrated score");
            }
            var written = JObject.Parse(File.ReadAllText(path));
            AssertLoadsNull(Edited(written, json => json[@"CalibrationDenominator"] = 0.0), @"zero denominator");
            AssertLoadsNull(Edited(written, json => json.Remove(@"CalibrationThreshold")), @"no threshold");
            Assert.IsFalse(reloaded.UsesFallbackFeature);
            Assert.IsNull(written[@"FallbackFeature"], @"a run its model scores records no fallback feature");

            // A run the safeguard scored with one feature keeps that feature through the file.
            const int fallbackFeature = 2;
            Assert.IsTrue(FirstPassModelIO.SaveRunModel(path,
                new RunFirstPassModel(model, 0.75, 2.5, null, -0.25, fallbackFeature), TestStamps.Any));
            var fallback = FirstPassModelIO.LoadRunModel(path, PinFeatureMask.Hram);
            Assert.IsNotNull(fallback, @"a run scored by its fallback feature should reload");
            Assert.AreEqual(fallbackFeature, fallback.FallbackFeature);
            Assert.IsTrue(fallback.UsesFallbackFeature);
            var fallbackJson = JObject.Parse(File.ReadAllText(path));
            AssertLoadsNull(Edited(fallbackJson, json => json[@"FallbackFeature"] = PinFeatureMask.Hram.FdrFeatureIndices.Length),
                @"a fallback feature past the FDR features");
            AssertLoadsNull(Edited(fallbackJson, json => json[@"FallbackFeatureName"] = @"not_a_feature"),
                @"a fallback feature under another name");
        }

        [TestMethod]
        public void TestFirstPassModelSaveDeclinesDegenerate()
        {
            // Neither linear weights nor trees (a degenerate/empty model) -> the save writes
            // nothing, rather than a file no reader can score with.
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_decline_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                var noWeights = new PercolatorResults
                {
                    Standardizer = FeatureStandardizer.FromMeansStds(new[] { 0.0, 1.0 }, new[] { 1.0, 1.0 }),
                };
                Assert.IsFalse(SaveModel(path, noWeights), @"model without weights or trees should not persist");
                Assert.IsFalse(File.Exists(path), @"no file should be written when the save declines");
                Assert.IsFalse(SaveModel(path, null), @"null model should not persist");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// FirstPassFDR's fast resume enters at the compaction gate on the experiment sidecar of
        /// this run, and on no model: each run trained its own in PerFileScoring, which the gate's
        /// caller checks with the run's stub.
        /// </summary>
        [TestMethod]
        public void TestFirstPassFdrTaskGate()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_model_task_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                AssertGateKeysOnTheExperimentSidecar(dir);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// The analysis-wide products FirstPassFDR writes under one stamp - the Stage 6
        /// consensus, the retained base_id summary and the first-pass experiment sidecar - load
        /// only when each was written by FirstPassFDR of this build and the others present
        /// beside it still carry the same stamp. A node cannot rebuild FirstPassFDR's key, so this consistency check is what
        /// stands between a stale file shipped to it and a plan that looks current.
        /// </summary>
        [TestMethod]
        public void TestFirstPassProductsLoadOnlyFromThisBuildAndOneRun()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_firstpass_stamps_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                var config = AnalysisConfig(dir);
                string consensusPath = Stage6ConsensusFile.PathFor(config);
                string retainedPath = RetainedBaseIdSidecar.PathFor(config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config));
                var thisRun = OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"validity-key");

                // One FirstPassFDR run of this build: both load.
                SaveFirstPassProducts(config, thisRun, thisRun);
                Assert.IsNotNull(Stage6ConsensusFile.Load(config));
                Assert.IsNotNull(ScoringTaskShared.ReadRetainedBaseIds(config, out string error), error);

                // The summary left by another run beside this run's consensus: each refuses.
                SaveFirstPassProducts(config, thisRun, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"another-run"));
                string mismatch = string.Format(
                    OspreyTasksResources.ScoringTaskShared_FirstPassProductStampProblem_the_file_does_not_match__0___so_one_of_the_two_is_left_over_from_another__1__run,
                    retainedPath, FirstPassFdrTask.TASK_NAME);
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => Stage6ConsensusFile.Load(config)).Message, mismatch);
                Assert.IsNull(ScoringTaskShared.ReadRetainedBaseIds(config, out error));
                StringAssert.Contains(error, string.Format(
                    OspreyTasksResources.ScoringTaskShared_FirstPassProductStampProblem_the_file_does_not_match__0___so_one_of_the_two_is_left_over_from_another__1__run,
                    consensusPath, FirstPassFdrTask.TASK_NAME));

                // The first-pass experiment sidecar is written by the same run under the same
                // stamp, so one left by another run beside this run's pair is refused too.
                SaveFirstPassProducts(config, thisRun, thisRun);
                string experimentPath = FdrExperimentSidecar.PathFor(config.OutputBlib,
                    ScoringTaskShared.ArtifactSiblingPath(config), FdrScoresSidecar.Pass.FirstPass);
                FdrExperimentSidecar.Write(experimentPath, new Dictionary<uint, FdrExperimentRecord>(),
                    FdrScoresSidecar.Pass.FirstPass, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"another-run"));
                string experimentMismatch = string.Format(
                    OspreyTasksResources.ScoringTaskShared_FirstPassProductStampProblem_the_file_does_not_match__0___so_one_of_the_two_is_left_over_from_another__1__run,
                    experimentPath, FirstPassFdrTask.TASK_NAME);
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => Stage6ConsensusFile.Load(config)).Message, experimentMismatch);
                Assert.IsNull(ScoringTaskShared.ReadRetainedBaseIds(config, out error));
                StringAssert.Contains(error, experimentMismatch);
                FdrExperimentSidecar.Write(experimentPath, new Dictionary<uint, FdrExperimentRecord>(),
                    FdrScoresSidecar.Pass.FirstPass, thisRun);
                Assert.IsNotNull(Stage6ConsensusFile.Load(config));

                // A consensus with no stamp is refused for that, by its own reader. The summary
                // beside it does not blame it: with no stamp to compare there is no mismatch.
                File.WriteAllText(consensusPath,
                    @"{""SchemaVersion"":1,""Consensus"":[],""PassingBaseIds"":[],""PassingBaseIdCharges"":[],""PassingSequences"":[],""PassingSequenceCharges"":[]}");
                StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => Stage6ConsensusFile.Load(config)).Message,
                    OspreyTasksResources.ScoringTaskShared_FirstPassProductStampProblem_the_file_has_no_validity_stamp);
                Assert.IsNotNull(ScoringTaskShared.ReadRetainedBaseIds(config, out error), error);
                File.Delete(experimentPath);

                // A matching pair from another build, or written by another task, is refused by
                // its own stamp.
                AssertPairRefused(config, new ArtifactStamp(FirstPassFdrTask.TASK_NAME, OspreyVersion.Current + @".1", @"validity-key"));
                AssertPairRefused(config, OspreyVersion.Stamp(PerFileRescoreTask.TASK_NAME, @"validity-key"));

                // A SecondPassFDR node is shipped the summary and not the consensus, so there is no
                // pair to compare and this build's summary loads alone.
                SaveFirstPassProducts(config, thisRun, thisRun);
                File.Delete(consensusPath);
                Assert.IsNotNull(ScoringTaskShared.ReadRetainedBaseIds(config, out error), error);
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        private static void SaveFirstPassProducts(OspreyConfig config, ArtifactStamp consensusStamp, ArtifactStamp retainedStamp)
        {
            var consensus = new Stage6Consensus
            {
                Consensus = new List<PeptideConsensusRT>(),
                PassingBaseIds = new HashSet<(uint, byte)> { (5, 2) },
                PassingPrecursors = new HashSet<(string ModifiedSequence, byte Charge)> { (@"PEPTIDEK", 2) },
            };
            Stage6ConsensusFile.Save(Stage6ConsensusFile.PathFor(config), consensus, consensusStamp);
            RetainedBaseIdSidecar.Write(RetainedBaseIdSidecar.PathFor(config.OutputBlib, ScoringTaskShared.ArtifactSiblingPath(config)),
                new[] { 5u }, retainedStamp);
        }

        /// <summary>
        /// Both products written with <paramref name="stamp"/>, which is not FirstPassFDR of this
        /// build: the pair agrees, and each is still refused by the stamp itself.
        /// </summary>
        private static void AssertPairRefused(OspreyConfig config, ArtifactStamp stamp)
        {
            SaveFirstPassProducts(config, stamp, stamp);
            string notThisBuild = string.Format(
                OspreyTasksResources.ScoringTaskShared_FirstPassProductStampProblem_the_file_was_written_by__0__of_Osprey__1___not_by__2__of_this_build___3__,
                stamp.Task, stamp.Version, FirstPassFdrTask.TASK_NAME, OspreyVersion.Current);
            StringAssert.Contains(Assert.ThrowsException<InvalidDataException>(() => Stage6ConsensusFile.Load(config)).Message, notThisBuild);
            Assert.IsNull(ScoringTaskShared.ReadRetainedBaseIds(config, out string error));
            StringAssert.Contains(error, notThisBuild);
        }

        /// <summary>A two-run analysis in <paramref name="dir"/> writing <c>output.blib</c>.</summary>
        private static OspreyConfig AnalysisConfig(string dir)
        {
            return new OspreyConfig
            {
                OutputBlib = Path.Combine(dir, @"output.blib"),
                InputFiles = new List<string> { Path.Combine(dir, @"run1.mzML"), Path.Combine(dir, @"run2.mzML") },
            };
        }

        private static void AssertGateKeysOnTheExperimentSidecar(string dir)
        {
            const string key = @"validity-key";
            var task = FirstPassTask();
            string experimentPath = Path.Combine(dir, @"output.1st-pass.fdr_experiment.bin");
            // An experiment sidecar left by a run under ANOTHER key is not this run's first pass.
            FdrExperimentSidecar.Write(experimentPath, new Dictionary<uint, FdrExperimentRecord>(),
                FdrScoresSidecar.Pass.FirstPass, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"other-key"));
            var refusals = task.CompactionGateRefusals(experimentPath, key);
            Assert.AreEqual(1, refusals.Count, @"an experiment sidecar stamped with another key must not enter at the gate");

            FdrExperimentSidecar.Write(experimentPath, new Dictionary<uint, FdrExperimentRecord>(),
                FdrScoresSidecar.Pass.FirstPass, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, key));
            refusals = task.CompactionGateRefusals(experimentPath, key);
            Assert.AreEqual(0, refusals.Count, string.Join(@"; ", refusals));
        }

        private static FirstPassFdrTask FirstPassTask()
        {
            foreach (var t in OspreyTasks.Create().Pipeline)
            {
                if (t is FirstPassFdrTask firstPass)
                    return firstPass;
            }
            Assert.Fail(@"FirstPassFDR must be in the canonical pipeline");
            return null;
        }

        private static PipelineContext NewContext(OspreyConfig config)
        {
            return new PipelineContext(config, OspreyTasks.Create().Pipeline, null,
                message => Assert.Fail(message), message => Assert.Fail(message));
        }

        /// <summary>
        /// A masked PIN feature is ABSENT from the FDR classifier's matrix, not a column of zeros:
        /// the vectors the FDR stage loads hold only <see cref="PinFeatureSelection.FdrFeatureNames"/>,
        /// the trained model is exactly that wide, and its feature names - in memory and in the
        /// saved model - line up with the vectors' positions. Zero columns cost the classifier as
        /// much as real ones (97 columns trained where 21 carried information), and a model whose
        /// names did not travel with it could be applied to vectors laid out another way. The test
        /// process leaves OSPREY_FEATURE_MASK unset, so the default set is masked.
        /// </summary>
        [TestMethod]
        public void TestMaskedFeaturesAbsentFromTrainingMatrix()
        {
            int[] fdrIndices = PinFeatureMask.Hram.FdrFeatureIndices;
            string[] fdrNames = PinFeatureMask.Hram.FdrFeatureNames;
            Assert.IsTrue(fdrIndices.Length > 0 && fdrIndices.Length < ParquetScoreCache.NUM_PIN_FEATURES,
                @"the default mask must hide some features and leave some");
            Assert.AreEqual(ParquetScoreCache.NUM_PIN_FEATURES - Enumerable.Range(0, ParquetScoreCache.NUM_PIN_FEATURES).Count(PinFeatureMask.Hram.IsMasked),
                fdrIndices.Length, @"every masked feature must leave the vector");
            CollectionAssert.AreEqual(PinFeatureMask.FeatureSetIndices(PinFeatureMask.Hram.FeatureSet), fdrIndices,
                @"the vector holds the process's feature set");
            for (int i = 0; i < fdrIndices.Length; i++)
            {
                Assert.AreEqual(ParquetScoreCache.PIN_FEATURE_NAMES[fdrIndices[i]], fdrNames[i], @"name of FDR feature " + i);
                Assert.IsFalse(PinFeatureMask.Hram.IsMasked(fdrIndices[i]), fdrNames[i]);
                Assert.IsTrue(i == 0 || fdrIndices[i] > fdrIndices[i - 1], @"FDR features must stay in PIN order");
            }
            CollectionAssert.AreEqual(fdrNames, ScoringTaskShared.FdrFeatureInfos(new OspreyConfig()).Select(info => info.Name).ToArray(),
                @"the classifier's feature metadata must name the vector's positions");

            string parquetPath = Path.Combine(Path.GetTempPath(),
                @"osprey_fdrcols_" + Guid.NewGuid().ToString(@"N") + @".parquet");
            string modelPath = Path.Combine(Path.GetTempPath(),
                @"osprey_fdrcols_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                var entries = MakeAllFeatureEntries();
                ParquetScoreCache.WriteScoresParquet(parquetPath, entries, null, null, @"f.mzML", false, PinFeatureMask.Hram);
                var vectors = ParquetScoreCache.LoadPinFeaturesFromParquet(parquetPath, PinFeatureMask.Hram);
                Assert.AreEqual(entries.Count, vectors.Count);
                for (int row = 0; row < vectors.Count; row++)
                {
                    Assert.AreEqual(fdrIndices.Length, vectors[row].Length, @"FDR vector width, row " + row);
                    for (int i = 0; i < fdrIndices.Length; i++)
                        Assert.AreEqual(entries[row].Features[fdrIndices[i]], vectors[row][i], fdrNames[i] + @", row " + row);
                }

                var percEntries = new List<PercolatorEntry>();
                for (int row = 0; row < entries.Count; row++)
                {
                    percEntries.Add(new PercolatorEntry
                    {
                        FileName = @"f",
                        Peptide = entries[row].ModifiedSequence,
                        Charge = entries[row].Charge,
                        IsDecoy = entries[row].IsDecoy,
                        EntryId = entries[row].EntryId,
                        Features = vectors[row],
                    });
                }
                var model = PercolatorTrainer.RunPercolator(percEntries, new PercolatorConfig
                {
                    MaxIterations = 3,
                    TrainOnly = true,
                    FeatureInfos = ScoringTaskShared.FdrFeatureInfos(new OspreyConfig()),
                });
                Assert.AreEqual(fdrIndices.Length, model.Standardizer.NumFeatures, @"training matrix width");
                Assert.AreEqual(fdrIndices.Length, model.FoldWeights[0].Length, @"trained weight count");
                CollectionAssert.AreEqual(fdrNames, model.FeatureNames, @"the model must name the features it trained on");

                Assert.IsTrue(SaveModel(modelPath, model), @"model should persist");
                var reloaded = LoadModel(modelPath);
                Assert.IsNotNull(reloaded, @"a model trained on the FDR features should reload");
                CollectionAssert.AreEqual(fdrNames, reloaded.FeatureNames, @"the names must travel with the saved model");

                // A model whose names are not this run's FDR features (another mask's, say) would
                // weigh each column as some other feature, so it does not load.
                model.FeatureNames = fdrNames.Reverse().ToArray();
                Assert.IsTrue(SaveModel(modelPath, model));
                Assert.IsNull(LoadModel(modelPath), @"a model laid out another way must not load");
            }
            finally
            {
                foreach (string path in new[] { parquetPath, modelPath })
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        [TestMethod]
        public void TestFirstPassModelLoadMissingReturnsNull()
        {
            // Absent file -> null.
            string missing = Path.Combine(Path.GetTempPath(),
                @"osprey_model_missing_" + Guid.NewGuid().ToString(@"N") + @".json");
            Assert.IsNull(LoadModel(missing), @"missing file should load as null");
        }

        [TestMethod]
        public void TestFirstPassModelLoadRejectsCorruptOrInconsistent()
        {
            // The load's contract is null on anything unreadable: it must never throw, and never
            // accept a shape-inconsistent model that the frozen scorer would then crash on or
            // silently mis-score with. Each case below carries a calibration map, so it is
            // refused for the reason it names, and the same file without the defect loads.
            AssertLoads(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0]" + CALIBRATION_JSON, @"a consistent file");
            AssertLoadsNull(@"{ not valid json", @"malformed JSON");
            AssertLoadsNull(@"{}", @"empty object (all fields null)");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 2, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0]" + CALIBRATION_JSON, @"future schema version");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 3, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0]" + CALIBRATION_JSON, @"NumFeatures != Means.Length");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0]" + CALIBRATION_JSON, @"Means/Stds length mismatch");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5, 0.5]], ""FoldBiases"": [0.0]" + CALIBRATION_JSON, @"fold width != feature count");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0, 0.0]" + CALIBRATION_JSON, @"bias/fold count mismatch");

            // The recorded aggregation is not read back: a file naming another one still loads.
            AssertLoads(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""ExperimentAgg"": ""mean-best-2""" + CALIBRATION_JSON,
                @"file with another recorded aggregation");

            // A model file written by an old build carried the removed protein-compact stratum
            // inline; the unknown property is ignored and the model still loads.
            AssertLoads(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""StratumBaseIds"": [4, 9]" + CALIBRATION_JSON,
                @"model file with an inline stratum");
        }

        /// <summary>
        /// The linear model's file, byte for byte: a change to the serialized form must be
        /// deliberate, because PerFileRescoring in another process reads it back.
        /// </summary>
        private static void AssertLinearFileBytesArePinned()
        {
            var model = new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(new[] { 0.5, -1.25 }, new[] { 2.0, 0.1 }),
                FoldWeights = new List<double[]> { new[] { 1.5, -0.25 }, new[] { 0.3333333333333333, 2.0 } },
                FoldBiases = new List<double> { 0.125, -3.0 },
            };
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_pinned_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsTrue(SaveModel(path, model), @"pinned SVM model should persist");
                Assert.AreEqual(PinnedLinearModelFile(TestStamps.Any), File.ReadAllText(path));
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void AssertSaveDeclines(PercolatorResults model, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_declined_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsFalse(SaveModel(path, model), what + @" should not persist");
                Assert.IsFalse(File.Exists(path), what + @" should leave no file behind");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void AssertLoadsNull(string json, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_bad_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                File.WriteAllText(path, json);
                Assert.IsNull(LoadModel(path), what + @" should load as null");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>A copy of <paramref name="json"/> with <paramref name="edit"/> applied,
        /// serialized - so each refusal case starts from the file Save actually wrote.</summary>
        private static string Edited(JObject json, Action<JObject> edit)
        {
            var copy = (JObject) json.DeepClone();
            edit(copy);
            return copy.ToString();
        }

        /// <summary>The first fold's tree ensemble in a written model file.</summary>
        private static JObject FirstTree(JObject json)
        {
            var trees = json[@"FoldGbtModels"] as JArray;
            Assert.IsNotNull(trees, @"a tree model's file should carry FoldGbtModels");
            var first = trees[0] as JObject;
            Assert.IsNotNull(first, @"FoldGbtModels should hold one object per fold");
            return first;
        }

        private static void AssertLoads(string json, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_arm_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                File.WriteAllText(path, json);
                Assert.IsNotNull(LoadModel(path), what + @" should load");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// Paired targets and decoys carrying every PIN feature, targets higher on each so the
        /// classifier has something to separate. A masked feature gets a value no unmasked one
        /// has, so one leaking into an FDR vector shows.
        /// </summary>
        private static List<FdrEntry> MakeAllFeatureEntries()
        {
            var rng = new Random(13);   // Fixed seed: a fixed population, not a sampled one
            var entries = new List<FdrEntry>();
            for (uint i = 0; i < 100; i++)
            {
                foreach (bool isDecoy in new[] { false, true })
                {
                    var features = new double[ParquetScoreCache.NUM_PIN_FEATURES];
                    for (int f = 0; f < features.Length; f++)
                    {
                        features[f] = PinFeatureMask.Hram.IsMasked(f)
                            ? 1e6 + f
                            : rng.NextDouble() + (isDecoy ? 0.0 : 0.5);
                    }
                    entries.Add(new FdrEntry
                    {
                        EntryId = isDecoy ? (i + 1) | 0x80000000 : i + 1,
                        IsDecoy = isDecoy,
                        Charge = 2,
                        ScanNumber = 10 + i,
                        ModifiedSequence = (isDecoy ? @"DECOY" : @"PEPTIDE") + i,
                        Features = features,
                    });
                }
            }
            // In the order the parquet stores its rows, so row i of the load is entries[i].
            return entries.OrderBy(e => e.EntryId).ToList();
        }
    }
}
