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
                // Deliberately unsorted, and deliberately not the insertion order a HashSet
                // would enumerate: the stratum must survive as a SET, and the artifact must
                // be written in a stable order regardless of how it was built.
                var stratum = new HashSet<uint> { 900, 3, 47, 1, 12345 };
                Assert.IsTrue(FirstPassModelIO.Save(path, model, @"mean-best-3", stratum),
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

                // The protein-compact stratum rides in the same sidecar, and a SecondPassFDR node
                // cannot rebuild it, so a lossy round trip would silently constrain the
                // pass-2 competition to the wrong population.
                Assert.IsNotNull(sidecar.StratumBaseIds, @"stratum should survive the round trip");
                Assert.IsTrue(stratum.SetEquals(sidecar.StratumBaseIds), @"stratum base ids");

                // The written order is sorted, not the set's enumeration order, so the
                // artifact is diffable and safe to compare byte-wise between runs.
                CollectionAssert.AreEqual(new[] { 1u, 3u, 47u, 900u, 12345u },
                    ReadStratumInFileOrder(path), @"stratum written ascending");

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

        [TestMethod]
        public void TestFirstPassModelSaveDeclinesGbtAndDegenerate()
        {
            // No linear weights (the GBDT shape, or a degenerate/empty model) -> Save writes
            // nothing so SecondPassFDR keeps its existing fail-fast rather than loading a
            // sidecar it cannot score with.
            string path = Path.Combine(Path.GetTempPath(),
                @"osprey_model_decline_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                var noWeights = new PercolatorResults
                {
                    Standardizer = FeatureStandardizer.FromMeansStds(new[] { 0.0, 1.0 }, new[] { 1.0, 1.0 }),
                };
                Assert.IsFalse(FirstPassModelIO.Save(path, noWeights, @"max"),
                    @"model without linear weights should not persist");
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
