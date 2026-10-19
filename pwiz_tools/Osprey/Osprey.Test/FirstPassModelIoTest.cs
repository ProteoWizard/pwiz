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
    /// (the sidecar a distributed SecondPassFDR node reads to run the frozen
    /// 2nd-pass modes) must score BIT-IDENTICALLY to the in-process original. Without
    /// bit-parity the pass-2-only path would report different q-values than a
    /// straight-through run under the same flag.
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

        /// <summary>The file <see cref="AssertLinearFileBytesArePinned"/> expects, as the build
        /// before tree models were persisted wrote it: LF, two-space indent, round-trip doubles,
        /// a trailing newline, and no tree property.</summary>
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
            "  \"ExperimentAgg\": \"max\"\n" +
            "}\n";

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
            string stratumPath = Path.Combine(Path.GetTempPath(),
                @"osprey_stratum_roundtrip_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"mean-best-3", TestStamps.Any),
                    @"SVM model should persist");
                Assert.IsTrue(File.Exists(path), @"sidecar should exist after Save");

                var sidecar = FirstPassModelIO.Load(path);
                Assert.IsNotNull(sidecar, @"reloaded sidecar should not be null");
                var reloaded = sidecar.Model;
                Assert.IsNotNull(reloaded, @"reloaded model should not be null");

                // Pass-1 provenance survives the round trip. This is what lets a --task
                // SecondPassFDR node - which never trained pass 1 - gate on the arm that
                // actually produced the q-values instead of on its own environment.
                Assert.AreEqual(@"mean-best-3", sidecar.ExperimentAgg, @"recorded pass-1 aggregation arm");

                // The stratum is a SEPARATE artifact, written when protein FDR ends rather than
                // when training does, so the model file carries none of it. Deliberately
                // unsorted here, and deliberately not the insertion order a HashSet would
                // enumerate: the stratum must survive as a SET, and the artifact must be written
                // in a stable order regardless of how it was built.
                Assert.IsNull(sidecar.StratumBaseIds, @"model sidecar should carry no stratum");
                var stratum = new HashSet<uint> { 900, 3, 47, 1, 12345 };
                Assert.IsTrue(FirstPassModelIO.SaveStratum(stratumPath, stratum, TestStamps.Any),
                    @"stratum should persist");

                // A SecondPassFDR node cannot rebuild the stratum, so a lossy round trip would
                // silently constrain the pass-2 competition to the wrong population.
                var reloadedStratum = FirstPassModelIO.LoadStratum(stratumPath);
                Assert.IsNotNull(reloadedStratum, @"stratum should survive the round trip");
                Assert.IsTrue(stratum.SetEquals(reloadedStratum), @"stratum base ids");

                // The written order is sorted, not the set's enumeration order, so the
                // artifact is diffable and safe to compare byte-wise between runs.
                CollectionAssert.AreEqual(new[] { 1u, 3u, 47u, 900u, 12345u },
                    ReadStratumInFileOrder(stratumPath), @"stratum written ascending");

                // Nothing to persist is not a failure to persist - it is how every mode but
                // protein-compact reaches this code, and an empty file would make a resume
                // adopt an empty stratum as if it were a computed one.
                Assert.IsFalse(FirstPassModelIO.SaveStratum(stratumPath, new HashSet<uint>(), TestStamps.Any),
                    @"empty stratum should not persist");
                Assert.IsFalse(FirstPassModelIO.SaveStratum(stratumPath, null, TestStamps.Any),
                    @"null stratum should not persist");

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
                if (File.Exists(stratumPath))
                    File.Delete(stratumPath);
            }
        }

        /// <summary>
        /// A <c>OSPREY_FDR_MODEL=gbdt</c> model must persist, reload as the tree ensemble and score
        /// BIT-IDENTICALLY, for the reason the SVM must: a distributed SecondPassFDR node, the
        /// Stage 6 per-file competition and a resume all read the model from this file rather
        /// than from the process that trained it. Save used to decline a tree model, which went
        /// unnoticed because the default first pass never trained one.
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
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"max", TestStamps.Any), @"tree model should persist");
                var sidecar = FirstPassModelIO.Load(path);
                Assert.IsNotNull(sidecar, @"reloaded tree sidecar should not be null");
                Assert.AreEqual(@"max", sidecar.ExperimentAgg, @"recorded pass-1 aggregation arm");
                var reloaded = sidecar.Model;
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

                Assert.IsTrue(FirstPassModelIO.Save(linearPath, MakeSvmModel(), @"max", TestStamps.Any), @"SVM model should persist");
                Assert.IsNull(JObject.Parse(File.ReadAllText(linearPath))[@"FoldGbtModels"],
                    @"a linear model's file must not gain a tree property");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
                if (File.Exists(linearPath))
                    File.Delete(linearPath);
            }
        }

        [TestMethod]
        public void TestFirstPassModelSaveDeclinesDegenerate()
        {
            // Neither linear weights nor trees (a degenerate/empty model) -> Save writes nothing
            // so SecondPassFDR keeps its existing fail-fast rather than loading a sidecar it
            // cannot score with.
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_decline_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                var noWeights = new PercolatorResults
                {
                    Standardizer = FeatureStandardizer.FromMeansStds(new[] { 0.0, 1.0 }, new[] { 1.0, 1.0 }),
                };
                Assert.IsFalse(FirstPassModelIO.Save(path, noWeights, @"max", TestStamps.Any),
                    @"model without weights or trees should not persist");
                Assert.IsFalse(File.Exists(path), @"no sidecar should be written when Save declines");
                Assert.IsFalse(FirstPassModelIO.Save(path, null, @"max", TestStamps.Any), @"null model should not persist");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// What FirstPassFDR does with the analysis's model file. It writes every model it TRAINS
        /// - a current model of the other classifier on disk does not excuse the write, because
        /// Stage 6 and a distributed SecondPassFDR read whatever file is there - and it enters the
        /// resume at the compaction gate only with a persisted model of the classifier this run
        /// uses. The model and stratum are experiment-wide, so they are written ONCE under the
        /// analysis name and never beside a run.
        /// </summary>
        [TestMethod]
        public void TestFirstPassFdrTaskPersistsAndAdoptsOnlyItsOwnModel()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_model_task_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                AssertEveryTrainedModelIsPersisted(dir, AnalysisConfig(dir));
                AssertGateAdoptsOnlyThisRunsClassifier(dir, AnalysisConfig(dir));
                string pairingDir = Path.Combine(dir, @"pairing");
                Directory.CreateDirectory(pairingDir);
                AssertLoadForAnalysisPairsByStamp(AnalysisConfig(pairingDir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// The two analysis-wide products FirstPassFDR writes under one stamp - the Stage 6
        /// consensus and the retained base_id summary - load only when each was written by
        /// FirstPassFDR of this build and, where both are present, they still carry the same
        /// stamp. A node cannot rebuild FirstPassFDR's key, so this consistency check is what
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

        /// <summary>
        /// <see cref="FirstPassModelIO.LoadForAnalysis"/> pairs the stratum with the model only
        /// when both carry the same stamp, and otherwise returns the model alone.
        /// </summary>
        private static void AssertLoadForAnalysisPairsByStamp(OspreyConfig config)
        {
            Assert.IsNull(FirstPassModelIO.LoadForAnalysis(config), @"no model file: nothing to load");
            Assert.IsTrue(FirstPassModelIO.Save(FirstPassModelIO.PathFor(config), MakeSvmModel(), @"max", TestStamps.Any));

            var modelOnly = FirstPassModelIO.LoadForAnalysis(config);
            Assert.IsNotNull(modelOnly);
            Assert.IsNotNull(modelOnly.Model);
            Assert.IsNull(modelOnly.StratumBaseIds, @"no stratum file: the model alone");

            var stratum = new HashSet<uint> { 7, 11 };
            Assert.IsTrue(FirstPassModelIO.SaveStratum(FirstPassModelIO.StratumPathFor(config), stratum, TestStamps.Any));
            var paired = FirstPassModelIO.LoadForAnalysis(config);
            Assert.IsNotNull(paired);
            Assert.IsTrue(stratum.SetEquals(paired.StratumBaseIds));

            // A stratum written by another run (another key) is not this model's, so it is not
            // paired with it.
            Assert.IsTrue(FirstPassModelIO.SaveStratum(FirstPassModelIO.StratumPathFor(config), stratum,
                OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"another-run")));
            var unpaired = FirstPassModelIO.LoadForAnalysis(config);
            Assert.IsNotNull(unpaired);
            Assert.IsNull(unpaired.StratumBaseIds, @"a stratum stamped by another run must not be paired");
        }

        private static void AssertEveryTrainedModelIsPersisted(string dir, OspreyConfig config)
        {
            const string key = @"validity-key";
            var task = FirstPassTask();
            string modelPath = FirstPassModelIO.PathFor(config);
            Assert.AreEqual(Path.Combine(dir, @"output" + FirstPassModelIO.EXT_MODEL), modelPath,
                @"the model is named for the analysis, beside the per-run artifacts");

            // A first run persists the linear model it trained - once, and not beside any run.
            task.PublishFirstPassModel(MakeSvmModel(), null, null, key, NewContext(config));
            var onDisk = FirstPassModelIO.Load(modelPath);
            Assert.IsNotNull(onDisk, @"the trained model should be persisted");
            Assert.IsNotNull(onDisk.Model, @"the persisted file should carry the model");
            Assert.IsFalse(onDisk.Model.IsGradientBoostedTrees, @"the persisted model should be the linear one");
            Assert.AreEqual(0, Directory.GetFiles(dir, @"run*").Length, @"no per-run copy of the model");

            // A re-run finds that model current but trains its own, as it does whenever no file
            // is resumable. Its model must replace the file.
            task.PublishFirstPassModel(MakeTreeModel(), onDisk, null, key, NewContext(config));
            Assert.IsTrue(FirstPassModelIO.Load(modelPath)?.Model?.IsGradientBoostedTrees ?? false,
                @"a retrained model must replace the persisted one");
            Assert.IsTrue(PerFileResumeDriver.IsCurrent(modelPath, FirstPassFdrTask.TASK_NAME, key),
                @"the replacement must be stamped current");

            // A model the run ADOPTED is the file already on disk, attested by its marker, so it
            // is not written again.
            var adopted = FirstPassModelIO.Load(modelPath);
            Assert.IsNotNull(adopted);
            File.Delete(modelPath);
            task.PublishFirstPassModel(adopted.Model, adopted, adopted.Model, key, NewContext(config));
            Assert.IsFalse(File.Exists(modelPath), @"an adopted model is not written again");
        }

        private static void AssertGateAdoptsOnlyThisRunsClassifier(string dir, OspreyConfig config)
        {
            const string key = @"validity-key";
            var task = FirstPassTask();
            string experimentPath = Path.Combine(dir, @"output.1st-pass.fdr_experiment.bin");
            FdrExperimentSidecar.Write(experimentPath, new Dictionary<uint, FdrExperimentRecord>(),
                FdrScoresSidecar.Pass.FirstPass, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, key));
            // A model and stratum left by a run under ANOTHER key are not this run's first pass.
            var otherRun = OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, @"other-key");
            SaveModelAndStratum(config, otherRun);
            var refusals = task.CompactionGateRefusals(experimentPath, key, config, out _);
            Assert.AreEqual(1, refusals.Count, @"a model stamped with another key must not enter at the gate");

            SaveModelAndStratum(config, OspreyVersion.Stamp(FirstPassFdrTask.TASK_NAME, key));
            refusals = task.CompactionGateRefusals(experimentPath, key, config, out var sidecar);
            Assert.AreEqual(0, refusals.Count, string.Join(@"; ", refusals));
            Assert.IsNotNull(sidecar, @"the gate should hand back the sidecar it checked");
            Assert.IsNotNull(sidecar.Model, @"the gate should hand back the model it checked");

            config.FdrClassifier = FdrClassifier.Gbdt;
            refusals = task.CompactionGateRefusals(experimentPath, key, config, out _);
            Assert.AreEqual(1, refusals.Count,
                @"a gbdt run must not enter at the gate with a linear model: it would publish that model");
        }

        private static void SaveModelAndStratum(OspreyConfig config, ArtifactStamp stamp)
        {
            Assert.IsTrue(FirstPassModelIO.Save(FirstPassModelIO.PathFor(config), MakeSvmModel(), @"max", stamp));
            Assert.IsTrue(FirstPassModelIO.SaveStratum(FirstPassModelIO.StratumPathFor(config),
                new HashSet<uint> { 1, 2, 3 }, stamp));
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

        [TestMethod]
        public void TestFirstPassModelLoadMissingReturnsNull()
        {
            // Absent sidecar -> null (the caller then fails fast exactly as before persistence).
            string missing = Path.Combine(Path.GetTempPath(),
                @"osprey_model_missing_" + Guid.NewGuid().ToString(@"N") + @".json");
            Assert.IsNull(FirstPassModelIO.Load(missing), @"missing sidecar should load as null");
        }

        [TestMethod]
        public void TestFirstPassModelLoadRejectsCorruptOrInconsistent()
        {
            // Load's contract is null (fail-fast) on anything unreadable: it must never
            // throw and crash SecondPassFDR, and never accept a shape-inconsistent model
            // that the frozen scorer would then crash on or silently mis-score with.
            AssertLoadsNull(@"{ not valid json", @"malformed JSON");
            AssertLoadsNull(@"{}", @"empty object (all fields null)");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 2, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0] }", @"future schema version");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 3, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0] }", @"NumFeatures != Means.Length");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0] }", @"Means/Stds length mismatch");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5, 0.5]], ""FoldBiases"": [0.0] }", @"fold width != feature count");
            AssertLoadsNull(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0, 0.0] }", @"bias/fold count mismatch");

            // A sidecar written BEFORE the arm was recorded must still load - the field was added
            // without bumping SchemaVersion precisely so pre-existing sidecars stay readable, and
            // a SecondPassFDR node that could not read one would hard fail-fast instead of degrading.
            // The arm then reports null, which the caller must treat as UNKNOWN, not as "max".
            AssertLoadsWithArm(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0] }", null, @"pre-provenance sidecar");
            AssertLoadsWithArm(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""ExperimentAgg"": ""mean-best-2"" }",
                @"mean-best-2", @"sidecar with a recorded arm");

            // The stratum comes only from its own file. A model file written before the split
            // carried one inline; that copy is ignored, so it loads as a model with no stratum
            // and a protein-compact consumer keeps its fail-fast.
            AssertLoadsWithoutStratum(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""StratumBaseIds"": [4, 9] }",
                @"model file with an inline stratum");
        }

        /// <summary>
        /// The linear model's file, byte for byte. Every directory written before tree models
        /// were persisted holds this form, and the resume, the Stage 6 competition and a
        /// distributed SecondPassFDR node all read it back, so adding the tree form and
        /// splitting serialization from the per-file write must leave it exactly as it was.
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
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"max", TestStamps.Any), @"pinned SVM model should persist");
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
                Assert.IsFalse(FirstPassModelIO.Save(path, model, @"max", TestStamps.Any), what + @" should not persist");
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
                Assert.IsNull(FirstPassModelIO.Load(path), what + @" should load as null");
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

        private static void AssertLoadsWithArm(string json, string expectedArm, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_arm_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                File.WriteAllText(path, json);
                var sidecar = FirstPassModelIO.Load(path);
                Assert.IsNotNull(sidecar, what + @" should load");
                Assert.AreEqual(expectedArm, sidecar.ExperimentAgg, what + @" arm");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void AssertLoadsWithoutStratum(string json, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_stratum_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                File.WriteAllText(path, json);
                var sidecar = FirstPassModelIO.Load(path);
                Assert.IsNotNull(sidecar, what + @" should load");
                Assert.IsNull(sidecar.StratumBaseIds, what + @" should carry no stratum");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>The StratumBaseIds array as it appears in the written JSON, so the test can
        /// assert the on-disk ORDER rather than the set it reloads into.</summary>
        private static List<uint> ReadStratumInFileOrder(string path)
        {
            var ids = new List<uint>();
            var array = (JArray) JObject.Parse(File.ReadAllText(path))[@"StratumBaseIds"];
            Assert.IsNotNull(array, @"sidecar should carry StratumBaseIds");
            foreach (var token in array)
                ids.Add((uint) token);
            return ids;
        }
    }
}
