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
using Newtonsoft.Json;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.ML;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The feature-analysis metrics of the model diagnostics (issues #4550 and #4467): the
    /// univariate separation arithmetic on hand-built sets, permutation importance on a toy model
    /// whose answer is known, fold weight stability, the trainer capturing its own training sets
    /// without changing what it scores, and the analysis surviving the first-pass model file.
    /// </summary>
    [TestClass]
    public class FeatureAnalysisTest
    {
        private const uint DECOY_BIT = 0x80000000;

        [TestMethod]
        public void TestFeatureAnalysis()
        {
            TestAuroc();
            TestCohenD();
            TestRocTpr();
            TestCorrelationGroups();
            TestPermutationOnToyModel();
            TestFoldWeightStability();
            TestTrainerCapturesTrainingSet();
        }

        private static void TestAuroc()
        {
            // Pairs (pos, neg): 1>0, 1<2, 2>0, 2=2 (half), 3>0, 3>2 -> 4.5 of 6.
            Assert.AreEqual(0.75, FeatureSeparation.Auroc(new[] { 3.0, 1.0, 2.0 }, new[] { 2.0, 0.0 }), 1e-12);
            Assert.AreEqual(1.0, FeatureSeparation.Auroc(new[] { 5.0, 6.0 }, new[] { 1.0, 2.0, 3.0 }), 1e-12);
            Assert.AreEqual(0.0, FeatureSeparation.Auroc(new[] { 1.0, 2.0 }, new[] { 5.0, 6.0 }), 1e-12);
            Assert.AreEqual(0.5, FeatureSeparation.Auroc(new[] { 1.0, 1.0 }, new[] { 1.0, 1.0, 1.0 }), 1e-12);
            Assert.IsTrue(double.IsNaN(FeatureSeparation.Auroc(new double[0], new[] { 1.0 })));
        }

        private static void TestCohenD()
        {
            // Means 4 and 2; sums of squares 8 and 2; pooled sd sqrt(10 / 4).
            Assert.AreEqual(2.0 / Math.Sqrt(2.5),
                FeatureSeparation.CohenD(new[] { 2.0, 4.0, 6.0 }, new[] { 1.0, 2.0, 3.0 }), 1e-12);
            Assert.IsTrue(double.IsNaN(FeatureSeparation.CohenD(new[] { 1.0, 1.0 }, new[] { 1.0, 1.0 })),
                @"zero pooled deviation");
            Assert.IsTrue(double.IsNaN(FeatureSeparation.CohenD(new[] { 1.0 }, new[] { 1.0, 2.0 })), @"one positive");
        }

        private static void TestRocTpr()
        {
            var negatives = Enumerable.Range(0, 10).Select(i => (double)i).ToArray();
            var positives = new[] { 5.5, 6.5, 7.5, 8.5, 9.5, 10.5 };
            // 10% FPR admits one decoy (9) above the threshold 8; three positives exceed 8.
            // Below one decoy's worth, the threshold is the top decoy (9); at FPR 1, everything.
            var tpr = FeatureSeparation.RocTprSorted(positives, negatives, new[] { 1e-4, 0.1, 1.0 });
            Assert.AreEqual(2.0 / 6, tpr[0], 1e-12);
            Assert.AreEqual(3.0 / 6, tpr[1], 1e-12);
            Assert.AreEqual(1.0, tpr[2], 1e-12);
            var grid = FeatureSeparation.LogFprGrid(4, 10);
            Assert.AreEqual(41, grid.Length);
            Assert.AreEqual(1e-4, grid[0], 1e-18);
            Assert.AreEqual(1.0, grid[40], 1e-12);
        }

        private static void TestCorrelationGroups()
        {
            var nan = double.NaN;
            var corr = new[]
            {
                new[] { 1.0, 0.85, 0.1, nan },
                new[] { 0.85, 1.0, -0.82, nan },
                new[] { 0.1, -0.82, 1.0, nan },
                new[] { nan, nan, nan, 1.0 },
            };
            // 0-1 and 1-2 link (single linkage, |r|), so 0 and 2 group through 1; NaN never links.
            var groups = FeatureSeparation.GroupByCorrelation(corr, 0.8);
            Assert.AreEqual(1, groups.Length);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, groups[0]);
            Assert.AreEqual(0, FeatureSeparation.GroupByCorrelation(corr, 0.9).Length);
        }

        /// <summary>
        /// A model that reads only feature 0, with feature 2 a near copy of it and feature 1 noise:
        /// permuting 1 or 2 cannot change a score, permuting 0 destroys the separation, and the
        /// correlated pair is one group whose joint drop is the large one.
        /// </summary>
        private static void TestPermutationOnToyModel()
        {
            var toy = new ToyData(300, 7);
            var analysis = toy.Analyze(1);
            Assert.IsNotNull(analysis);
            Assert.AreEqual(3, analysis.NFolds);
            Assert.AreEqual(300, analysis.SubsetTargets);
            Assert.AreEqual(300, analysis.SubsetDecoys);
            Assert.IsTrue(analysis.PermutationBaselinePassing > 0);
            Assert.AreEqual(0.0, analysis.Features[1].PermutationDrop, @"the model ignores feature 1");
            Assert.AreEqual(0.0, analysis.Features[2].PermutationDrop, @"the model ignores feature 2");
            Assert.IsTrue(analysis.Features[0].PermutationDrop > 0.5, @"feature 0 carries the model");
            Assert.AreEqual(1, analysis.Groups.Length);
            CollectionAssert.AreEqual(new[] { 0, 2 }, analysis.Groups[0].Members);
            Assert.IsTrue(analysis.Groups[0].PermutationDrop > 0.5);
            Assert.IsTrue(analysis.Features[0].Auroc > 0.9, @"feature 0 separates");
            Assert.IsTrue(Math.Abs(analysis.Features[1].Auroc - 0.5) < 0.15, @"feature 1 is noise");
            foreach (var stats in analysis.Features)
            {
                Assert.IsTrue(stats.Auroc >= 0 && stats.Auroc <= 1);
                Assert.IsTrue(stats.AurocFoldMin <= stats.Auroc + 0.2 && stats.AurocFoldMax >= stats.Auroc - 0.2);
                Assert.AreEqual(41, stats.RocTpr.Length);
            }
            Assert.IsTrue(analysis.HeldOutComposite.Auroc > 0.9);
            Assert.AreEqual(3, analysis.HeldOutComposite.AurocByFold.Length);

            // The permutations are seeded per task, so the thread count cannot change them.
            Assert.AreEqual(Serialize(analysis), Serialize(toy.Analyze(4)));

            // A row cap (trees) thins each fold's held-out rows by whole target-decoy pairs.
            Assert.AreEqual(600, analysis.PermutationRows);
            var capped = toy.Analyze(1, 60);
            Assert.IsTrue(capped.PermutationRows < 300, @"the cap thinned " + capped.PermutationRows);
            Assert.AreEqual(0, capped.PermutationRows % 2, @"thinning kept whole pairs");
            Assert.IsTrue(capped.Features[0].PermutationDrop > 0.5);
            Assert.AreEqual(analysis.HeldOutComposite.Auroc, capped.HeldOutComposite.Auroc, @"the cap is for permutations only");
        }

        private static void TestFoldWeightStability()
        {
            var acc = new FeatureContributions.Accumulator(2);
            acc.Add(new[] { 1.0, 1.0 }, false);
            acc.Add(new[] { 0.0, 0.0 }, true);
            var contributions = acc.Build(new[] { new[] { 1.0, -0.5 }, new[] { 2.0, 0.5 }, new[] { 3.0, 0.1 } }, null);
            var f0 = contributions.Features[0];
            Assert.AreEqual(2.0, f0.Coefficient, 1e-12, @"the coefficient is the fold mean");
            Assert.AreEqual(1.0, f0.FoldWeightSd, 1e-12);
            Assert.AreEqual(1.0, f0.FoldWeightMin);
            Assert.AreEqual(3.0, f0.FoldWeightMax);
            Assert.IsTrue(f0.IsFoldSignConsistent);
            Assert.IsFalse(contributions.Features[1].IsFoldSignConsistent, @"-0.5 and 0.5 flip sign");
            var tree = acc.BuildForTreeEnsemble(null);
            Assert.IsTrue(double.IsNaN(tree.Features[0].FoldWeightSd), @"trees have no weights");
        }

        /// <summary>
        /// The trainer records its own training sets only when asked, never changes the model or
        /// the scores for it, and the result survives the first-pass model file bit for bit -
        /// including the NaN correlation of a constant feature, which the round-trip converter
        /// cannot write as a number.
        /// </summary>
        private static void TestTrainerCapturesTrainingSet()
        {
            var entries = MakeEntries(240);
            var plain = PercolatorTrainer.RunPercolator(entries, new PercolatorConfig { MaxIterations = 3 });
            var withAnalysis = PercolatorTrainer.RunPercolator(entries,
                new PercolatorConfig { MaxIterations = 3, CollectTrainingAnalysis = true });
            Assert.IsNull(plain.TrainingAnalysis);
            var analysis = withAnalysis.TrainingAnalysis;
            Assert.IsNotNull(analysis);
            CollectionAssert.AreEqual(plain.Entries.Select(e => e.Score).ToArray(),
                withAnalysis.Entries.Select(e => e.Score).ToArray(), @"the analysis changed the scores");
            Assert.AreEqual(plain.FoldWeights.Count, analysis.NFolds);
            Assert.IsTrue(analysis.FoldPositives.All(n => n > 0) && analysis.FoldDecoys.All(n => n > 0));
            Assert.IsTrue(double.IsNaN(analysis.Correlation[0][2]), @"feature 2 is constant");
            Assert.AreSame(analysis, withAnalysis.FeatureContributions.TrainingAnalysis);

            // The diagnostics rows carry it.
            var view = ModelDiagnosticsData.ModelView.From(withAnalysis.FeatureContributions);
            Assert.IsNotNull(view.TrainingSet);
            Assert.AreEqual(analysis.NFolds, view.TrainingSet.NFolds);
            var row = view.Model.First(r => r.Index == 0);
            Assert.AreEqual(analysis.Features[0].Auroc, row.Auroc);
            Assert.AreEqual(analysis.Features[0].PermutationDrop, row.PermutationDrop);

            string path = Path.Combine(Path.GetTempPath(), @"osprey_analysis_roundtrip_" + Guid.NewGuid().ToString(@"N") + @".json");
            try
            {
                Assert.IsTrue(FirstPassModelIO.Save(path, withAnalysis, null, TestStamps.Any));
                var reloaded = FirstPassModelIO.Load(path)?.Model;
                Assert.IsNotNull(reloaded);
                Assert.AreEqual(Serialize(analysis), Serialize(reloaded.TrainingAnalysis));
                Assert.IsTrue(FirstPassModelIO.Save(path, plain, null, TestStamps.Any));
                Assert.IsNull(FirstPassModelIO.Load(path)?.Model.TrainingAnalysis);
            }
            finally
            {
                File.Delete(path);
            }
        }

        private static string Serialize(TrainingSetAnalysis analysis)
        {
            return JsonConvert.SerializeObject(analysis,
                new JsonSerializerSettings { FloatFormatHandling = FloatFormatHandling.String });
        }

        /// <summary>Paired targets and decoys over three features: one that separates, one that
        /// separates weakly, and a constant.</summary>
        private static List<PercolatorEntry> MakeEntries(int pairs)
        {
            var random = new Random(11);
            var entries = new List<PercolatorEntry>();
            for (int i = 0; i < pairs; i++)
            {
                uint id = (uint)(i + 1);
                string peptide = @"PEPTIDE" + i;
                entries.Add(MakeEntry(peptide, false, id,
                    new[] { 3.0 + random.NextDouble() * 2, 1.0 + random.NextDouble(), 1.0 }));
                entries.Add(MakeEntry(peptide, true, id | DECOY_BIT,
                    new[] { random.NextDouble() * 2, 0.5 + random.NextDouble(), 1.0 }));
            }
            return entries;
        }

        private static PercolatorEntry MakeEntry(string peptide, bool isDecoy, uint entryId, double[] features)
        {
            return new PercolatorEntry
            {
                FileName = @"file1",
                Peptide = peptide,
                Charge = 2,
                IsDecoy = isDecoy,
                EntryId = entryId,
                Features = features
            };
        }

        /// <summary>
        /// A hand-built training subset for <see cref="TrainingSetAnalysis.Compute"/>: feature 0
        /// separates, feature 1 is noise, feature 2 is feature 0 plus a little noise. Pairs share
        /// a fold, and each fold trains on the targets above 1 plus every decoy of the other folds.
        /// </summary>
        private sealed class ToyData
        {
            private readonly Matrix _features;
            private readonly bool[] _isDecoy;
            private readonly uint[] _entryIds;
            private readonly int[] _folds;

            public ToyData(int pairs, int seed)
            {
                var random = new Random(seed);
                int n = 2 * pairs;
                var data = new double[n * 3];
                _isDecoy = new bool[n];
                _entryIds = new uint[n];
                _folds = new int[n];
                for (int i = 0; i < n; i++)
                {
                    bool decoy = i % 2 == 1;
                    double signal = Gaussian(random) + (decoy ? 0.0 : 3.0);
                    data[i * 3] = signal;
                    data[i * 3 + 1] = Gaussian(random);
                    data[i * 3 + 2] = signal + 0.1 * Gaussian(random);
                    _isDecoy[i] = decoy;
                    _entryIds[i] = (uint)(i / 2 + 1) | (decoy ? DECOY_BIT : 0);
                    _folds[i] = i / 2 % 3;
                }
                _features = new Matrix(data, n, 3);
            }

            public TrainingSetAnalysis Analyze(int threads, int permutationRowCap = int.MaxValue)
            {
                var sets = new FoldTrainingSet[3];
                for (int f = 0; f < 3; f++)
                {
                    int fold = f;
                    var rows = Enumerable.Range(0, _isDecoy.Length).Where(i => _folds[i] != fold).ToArray();
                    sets[f] = new FoldTrainingSet(rows.Where(i => !_isDecoy[i] && _features[i, 0] > 1).ToArray(),
                        rows.Where(i => _isDecoy[i]).ToArray(), rows);
                }
                Func<double[], double> scorer = x => x[0];
                return TrainingSetAnalysis.Compute(_features, _isDecoy, _entryIds, _folds, sets,
                    new[] { scorer, scorer, scorer }, new[] { 1, 1, 1 }, 0.01, 42, threads, permutationRowCap);
            }

            private static double Gaussian(Random random)
            {
                double u1 = 1.0 - random.NextDouble(), u2 = random.NextDouble();
                return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
            }
        }
    }
}
