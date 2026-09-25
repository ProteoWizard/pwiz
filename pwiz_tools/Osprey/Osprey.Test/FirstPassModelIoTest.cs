/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
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
            "  \"ExperimentAgg\": \"max\",\n" +
            "  \"StratumBaseIds\": null\n" +
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
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"mean-best-3"),
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
                Assert.IsTrue(FirstPassModelIO.SaveStratum(stratumPath, stratum),
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
                Assert.IsFalse(FirstPassModelIO.SaveStratum(stratumPath, new HashSet<uint>()),
                    @"empty stratum should not persist");
                Assert.IsFalse(FirstPassModelIO.SaveStratum(stratumPath, null),
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
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"max"), @"tree model should persist");
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
                // turns into null: the marker beside it would attest a model nobody can load.
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

                Assert.IsTrue(FirstPassModelIO.Save(linearPath, MakeSvmModel(), @"max"), @"SVM model should persist");
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
                Assert.IsFalse(FirstPassModelIO.Save(path, noWeights, @"max"),
                    @"model without weights or trees should not persist");
                Assert.IsFalse(File.Exists(path), @"no sidecar should be written when Save declines");
                Assert.IsFalse(FirstPassModelIO.Save(path, null, @"max"), @"null model should not persist");
            }
            finally
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        /// <summary>
        /// What FirstPassFDR does with the model file beside each input. It writes every model
        /// it TRAINS - a current model of the other classifier on disk does not excuse the
        /// write, because Stage 6 and a distributed SecondPassFDR read whatever file is there -
        /// and it enters the resume at the compaction gate only with a persisted model of the
        /// classifier this run uses.
        /// </summary>
        [TestMethod]
        public void TestFirstPassFdrTaskPersistsAndAdoptsOnlyItsOwnModel()
        {
            string dir = Path.Combine(Path.GetTempPath(), @"osprey_model_task_" + Guid.NewGuid().ToString(@"N"));
            Directory.CreateDirectory(dir);
            try
            {
                var parquetPaths = new Dictionary<string, string>
                {
                    { @"run1", Path.Combine(dir, @"run1.scores.parquet") },
                    { @"run2", Path.Combine(dir, @"run2.scores.parquet") },
                };
                AssertEveryTrainedModelIsPersisted(parquetPaths);
                AssertGateAdoptsOnlyThisRunsClassifier(dir, parquetPaths);
                AssertLoadFromAnyPairsOneStem(Path.Combine(dir, @"pairing"));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        /// <summary>
        /// <see cref="FirstPassModelIO.LoadFromAny"/> returns the model and stratum of ONE stem:
        /// the first stem that has both, else the first readable model with no stratum. The two
        /// stems here hold different models, so a pairing across stems would show.
        /// </summary>
        private static void AssertLoadFromAnyPairsOneStem(string dir)
        {
            Directory.CreateDirectory(dir);
            var parquetPaths = new Dictionary<string, string>
            {
                { @"run1", Path.Combine(dir, @"run1.scores.parquet") },
                { @"run2", Path.Combine(dir, @"run2.scores.parquet") },
            };
            Assert.IsNull(FirstPassModelIO.LoadFromAny(parquetPaths), @"no model file: nothing to load");
            Assert.IsTrue(FirstPassModelIO.Save(FirstPassModelIO.PathFor(parquetPaths[@"run1"], @"run1"), MakeTreeModel(), @"max"));
            Assert.IsTrue(FirstPassModelIO.Save(FirstPassModelIO.PathFor(parquetPaths[@"run2"], @"run2"), MakeSvmModel(), @"max"));

            var modelOnly = FirstPassModelIO.LoadFromAny(parquetPaths);
            Assert.IsNotNull(modelOnly);
            Assert.IsNotNull(modelOnly.Model);
            Assert.IsTrue(modelOnly.Model.IsGradientBoostedTrees, @"no stratum anywhere: the first stem's model");
            Assert.IsNull(modelOnly.StratumBaseIds);

            var stratum = new HashSet<uint> { 7, 11 };
            Assert.IsTrue(FirstPassModelIO.SaveStratum(FirstPassModelIO.StratumPathFor(parquetPaths[@"run2"], @"run2"), stratum));
            var paired = FirstPassModelIO.LoadFromAny(parquetPaths);
            Assert.IsNotNull(paired);
            Assert.IsNotNull(paired.Model);
            Assert.IsFalse(paired.Model.IsGradientBoostedTrees, @"the stratum's own stem supplies the model");
            Assert.IsTrue(stratum.SetEquals(paired.StratumBaseIds));
        }

        private static void AssertEveryTrainedModelIsPersisted(Dictionary<string, string> parquetPaths)
        {
            const string key = @"validity-key";
            var task = FirstPassTask();
            string run1Path = FirstPassModelIO.PathFor(parquetPaths[@"run1"], @"run1");
            string run2Path = FirstPassModelIO.PathFor(parquetPaths[@"run2"], @"run2");

            // A first run persists the linear model it trained.
            task.PublishFirstPassModel(MakeSvmModel(), null, null, parquetPaths, key, NewContext());
            var onDisk = FirstPassModelIO.Load(run1Path);
            Assert.IsNotNull(onDisk, @"the trained model should be persisted");
            Assert.IsNotNull(onDisk.Model, @"the persisted file should carry the model");
            Assert.IsFalse(onDisk.Model.IsGradientBoostedTrees, @"the persisted model should be the linear one");

            // A re-run finds that model current but trains its own, as it does whenever no file
            // is resumable. Its model must replace the file beside every input.
            task.PublishFirstPassModel(MakeTreeModel(), onDisk, null, parquetPaths, key, NewContext());
            foreach (var kvp in parquetPaths)
            {
                string path = FirstPassModelIO.PathFor(kvp.Value, kvp.Key);
                Assert.IsTrue(FirstPassModelIO.Load(path)?.Model?.IsGradientBoostedTrees ?? false,
                    @"a retrained model must replace the persisted one: " + kvp.Key);
                Assert.IsTrue(PerFileResumeDriver.IsCurrent(path, FirstPassFdrTask.TASK_NAME, key),
                    @"the replacement must be stamped current: " + kvp.Key);
            }

            // A model the run ADOPTED is the file already on disk, attested by its marker, so it
            // is not written again.
            var adopted = FirstPassModelIO.Load(run1Path);
            Assert.IsNotNull(adopted);
            File.Delete(run2Path);
            task.PublishFirstPassModel(adopted.Model, adopted, adopted.Model, parquetPaths, key, NewContext());
            Assert.IsFalse(File.Exists(run2Path), @"an adopted model is not written again");
        }

        private static void AssertGateAdoptsOnlyThisRunsClassifier(string dir, Dictionary<string, string> parquetPaths)
        {
            const string key = @"validity-key";
            var task = FirstPassTask();
            string experimentPath = Path.Combine(dir, @"output.1st-pass.fdr_experiment.bin");
            File.WriteAllText(experimentPath, @"experiment");
            PerFileResumeDriver.Stamp(experimentPath, FirstPassFdrTask.TASK_NAME, OspreyVersion.Current, key,
                Array.Empty<string>(), message => Assert.Fail(message));
            foreach (var kvp in parquetPaths)
            {
                Assert.IsTrue(FirstPassModelIO.Save(FirstPassModelIO.PathFor(kvp.Value, kvp.Key), MakeSvmModel(), @"max"));
                Assert.IsTrue(FirstPassModelIO.SaveStratum(FirstPassModelIO.StratumPathFor(kvp.Value, kvp.Key),
                    new HashSet<uint> { 1, 2, 3 }));
            }

            var refusals = task.CompactionGateRefusals(experimentPath, key, parquetPaths,
                new OspreyConfig(), out var sidecar);
            Assert.AreEqual(0, refusals.Count, string.Join(@"; ", refusals));
            Assert.IsNotNull(sidecar, @"the gate should hand back the sidecar it checked");
            Assert.IsNotNull(sidecar.Model, @"the gate should hand back the model it checked");

            refusals = task.CompactionGateRefusals(experimentPath, key, parquetPaths,
                new OspreyConfig { FdrMethod = FdrMethod.Gbdt }, out _);
            Assert.AreEqual(1, refusals.Count,
                @"a gbdt run must not enter at the gate with a linear model: it would publish that model");
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

        private static PipelineContext NewContext()
        {
            return new PipelineContext(new OspreyConfig(), OspreyTasks.Create().Pipeline, null,
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

            // Same argument for the stratum: it was added to the same schema version, so a
            // sidecar written before it exists must load with a null stratum rather than fail.
            // A SecondPassFDR node then keeps the protein-compact fail-fast, which is correct - an
            // EMPTY stratum would instead constrain the pass-2 competition to nothing.
            AssertLoadsWithStratum(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0] }", null, @"pre-stratum sidecar");
            AssertLoadsWithStratum(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""StratumBaseIds"": [] }",
                null, @"sidecar with an empty stratum");
            AssertLoadsWithStratum(
                @"{ ""SchemaVersion"": 1, ""NumFeatures"": 2, ""Means"": [0.0, 1.0], ""Stds"": [1.0, 1.0], " +
                @"""FoldWeights"": [[0.5, 0.5]], ""FoldBiases"": [0.0], ""StratumBaseIds"": [4, 9] }",
                new[] { 4u, 9u }, @"sidecar with a stratum");
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
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"max"), @"pinned SVM model should persist");
                Assert.AreEqual(PINNED_LINEAR_MODEL_FILE, File.ReadAllText(path));
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
                Assert.IsFalse(FirstPassModelIO.Save(path, model, @"max"), what + @" should not persist");
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

        private static void AssertLoadsWithStratum(string json, uint[] expectedBaseIds, string what)
        {
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_stratum_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                File.WriteAllText(path, json);
                var sidecar = FirstPassModelIO.Load(path);
                Assert.IsNotNull(sidecar, what + @" should load");
                if (expectedBaseIds == null)
                {
                    Assert.IsNull(sidecar.StratumBaseIds, what + @" should carry no stratum");
                    return;
                }
                Assert.IsNotNull(sidecar.StratumBaseIds, what + @" should carry a stratum");
                Assert.IsTrue(new HashSet<uint>(expectedBaseIds).SetEquals(sidecar.StratumBaseIds),
                    what + @" base ids");
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
