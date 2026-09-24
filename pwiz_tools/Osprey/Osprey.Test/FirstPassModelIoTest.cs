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
        /// <c>--fdr-method gbdt</c> first pass publishes, without the Percolator loop around it.
        /// Internal so the pass-2 transfer test scores with the same model.
        /// </summary>
        internal static PercolatorResults MakeTreeModel()
        {
            const int nFeatures = 5;
            var rng = new Random(11);   // fixed seed: a fixed population, not a sampled one
            var rows = new double[200][];
            var isDecoy = new bool[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                isDecoy[i] = i % 2 == 1;
                rows[i] = new double[nFeatures];
                for (int j = 0; j < nFeatures; j++)
                    rows[i][j] = rng.NextDouble() + (isDecoy[i] ? 0.0 : 0.25 * j);
            }
            var folds = new List<GradientBoostedTrees>();
            for (ulong seed = 1; seed <= 3; seed++)
                folds.Add(GradientBoostedTrees.Train(rows, isDecoy, new GbtParams { NTrees = 20, MaxDepth = 3, Seed = seed }));
            return new PercolatorResults
            {
                Standardizer = FeatureStandardizer.FromMeansStds(
                    new[] { 0.5, 0.6180339887498949, 0.75, 0.8660254037844386, 1.0 },
                    new[] { 0.2886751345948129, 0.3, 0.3333333333333333, 0.35, 0.4 }),
                FoldGbtModels = folds,
            };
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
        /// A <c>--fdr-method gbdt</c> model must persist, reload as the tree ensemble and score
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
