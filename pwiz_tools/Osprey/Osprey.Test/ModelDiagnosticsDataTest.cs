/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;
using pwiz.Osprey.FDR.ModelDiagnostics;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for the pure <see cref="ModelDiagnosticsData"/> computation
    /// behind the <c>--model-diagnostics</c> report: entrapment classification,
    /// the FDRBench-matching FDP estimators, paired decoy-win fraction, the
    /// feature-contribution table, and graceful degrade without a manifest.
    /// </summary>
    [TestClass]
    public class ModelDiagnosticsDataTest
    {
        private const uint DECOY_BIT = 0x80000000;

        [TestMethod]
        public void TestModelDiagnosticsData()
        {
            TestFdpMatchesFdrBenchFormula();
            TestPairedEstimator();
            TestClassCountsAndDegrade();
            TestWinFractionCoinVsSignal();
            TestFeatureTableContributions();
            TestModifiedSequenceMatchesManifest();
        }

        // paired = (n_p + n_p_s_t) / (n_t + n_p), where n_p_s_t counts accepted
        // entrapment hits that outscore their paired target (shared pair_index)
        // or whose paired target went unobserved. Matches FDRBench's paired_fdp.
        private static void TestPairedEstimator()
        {
            var entries = new List<FdrEntry>();
            var map = new Dictionary<string, EntrapmentClass>();
            var pair = new Dictionary<string, uint>();
            // 4 real targets, scores 8/6/4/2, pair_index 0..3.
            double[] tscores = { 8, 6, 4, 2 };
            for (int i = 0; i < 4; i++)
            {
                string seq = "T" + i;
                entries.Add(Entry((uint)(10 + i), false, tscores[i], 0.001 * (i + 1), seq, 2));
                map[seq] = EntrapmentClass.Target;
                pair[seq] = (uint)i;
            }
            // P_a (score 5) is paired to an UNobserved target (index 100) -> wins.
            // P_b (score 3) is paired to T0 (score 8) -> does not win.
            entries.Add(Entry(20, false, 5.0, 0.01, "Pa", 2));
            map["Pa"] = EntrapmentClass.PTarget; pair["Pa"] = 100;
            entries.Add(Entry(21, false, 3.0, 0.01, "Pb", 2));
            map["Pb"] = EntrapmentClass.PTarget; pair["Pb"] = 0;
            // Balance the manifest to r = 1 (4 targets, 4 p_targets).
            map["Pu1"] = EntrapmentClass.PTarget;
            map["Pu2"] = EntrapmentClass.PTarget;

            var data = ModelDiagnosticsData.Build(Wrap(entries), null, map, pair, 0.01, "peptide");
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            Assert.IsNotNull(fdp.Paired);
            int last = fdp.Paired.Length - 1;
            // At the last target: n_t = 4, n_p = 2, n_p_s_t = 1 (only P_a won).
            // paired = (2 + 1) / (4 + 2) = 0.5; lower = 2 / 6.
            Assert.AreEqual(0.5, fdp.Paired[last], 1e-9);
            Assert.AreEqual(2.0 / 6.0, fdp.LowerBound[last], 1e-9);
            // Paired sits at or above lower-bound once an entrapment wins its pair.
            Assert.IsTrue(fdp.Paired[last] >= fdp.LowerBound[last]);
        }

        // combined = (1 + 1/r) * n_p / (n_t + n_p); lower = n_p / (r*(n_t+n_p)).
        // With r = 1 (equal target/entrapment manifest): combined = 2*lower and
        // both equal the running entrapment fraction. Verified against real
        // FDRBench fdp.csv output.
        private static void TestFdpMatchesFdrBenchFormula()
        {
            // 8 real targets (scores 8..1), 2 entrapment (scores 5.5, 0.5).
            var entries = new List<FdrEntry>();
            var map = new Dictionary<string, EntrapmentClass>();
            for (int i = 0; i < 8; i++)
            {
                string seq = "T" + i;
                entries.Add(Entry((uint)(100 + i), false, 8 - i, 0.001 * (i + 1), seq, 2));
                map[seq] = EntrapmentClass.Target;
            }
            // Both entrapment hits score above the lowest-scoring target, so at
            // the final accepted target n_p = 2.
            AddEntrap(entries, map, 200, 5.5, "P0");
            AddEntrap(entries, map, 201, 1.5, "P1");
            // The entrapment DB ratio r is defined by the manifest composition,
            // not the observed hits. A real entrapment library is balanced
            // (equal target and p_target peptides), so add unobserved p_target
            // keys to make r = 1 -- the case the estimator asserts below.
            for (int i = 2; i < 8; i++)
                map["Punseen" + i] = EntrapmentClass.PTarget;

            var data = ModelDiagnosticsData.Build(Wrap(entries), null, map, null, 0.01, "peptide");
            Assert.IsTrue(data.HasEntrapment);
            Assert.IsNotNull(data.FdpViews);
            // Two pass-1 views: experiment-wide (FDRBench-matching) + per-run.
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            Assert.IsTrue(fdp.MatchesFdrBench);
            Assert.AreEqual(1.0, fdp.EntrapmentRatio, 1e-9);

            // At the final accepted target, n_t = 8, n_p = 2 (both entrapment
            // ranked above the last target). combined = 2*2/(8+2) = 0.4,
            // lower = 2/(8+2) = 0.2.
            int last = fdp.Combined.Length - 1;
            Assert.AreEqual(8, fdp.NTargetAccepted[last]);
            Assert.AreEqual(0.40, fdp.Combined[last], 1e-9);
            Assert.AreEqual(0.20, fdp.LowerBound[last], 1e-9);
            // combined is exactly twice lower-bound everywhere at r = 1.
            for (int i = 0; i < fdp.Combined.Length; i++)
                Assert.AreEqual(2.0 * fdp.LowerBound[i], fdp.Combined[i], 1e-9);
        }

        private static void TestClassCountsAndDegrade()
        {
            var entries = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "TA", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "DA", 2),
                Entry(2, false, 4, 0.002, "PA", 2),
                Entry(2 | DECOY_BIT, true, 0.5, 0.6, "PDA", 2),
            };
            var map = new Dictionary<string, EntrapmentClass>
            {
                { "TA", EntrapmentClass.Target }, { "DA", EntrapmentClass.Decoy },
                { "PA", EntrapmentClass.PTarget }, { "PDA", EntrapmentClass.PDecoy },
            };
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, map, null, 0.01, "peptide");
            Assert.AreEqual(1, data.NTarget);
            Assert.AreEqual(1, data.NDecoy);
            Assert.AreEqual(1, data.NPTarget);
            Assert.AreEqual(1, data.NPDecoy);
            Assert.IsTrue(data.HasEntrapment);

            // No manifest -> degrade to the is_decoy-only split; no FDP views.
            var degraded = ModelDiagnosticsData.Build(Wrap(entries), null, null, null, 0.01, "peptide");
            Assert.IsFalse(degraded.HasEntrapment);
            Assert.IsNull(degraded.FdpViews);
            Assert.AreEqual(2, degraded.NTarget);  // TA + PA both is_decoy=false
            Assert.AreEqual(2, degraded.NDecoy);
        }

        private static void TestWinFractionCoinVsSignal()
        {
            // Real pairs: target always outscores its decoy (decoy never wins).
            // Entrapment pairs: alternating winner -> ~50% decoy-win coin.
            var entries = new List<FdrEntry>();
            var map = new Dictionary<string, EntrapmentClass>();
            for (int i = 0; i < 200; i++)
            {
                string t = "RT" + i, d = "RD" + i;
                entries.Add(Entry((uint)(1000 + i), false, 5.0, 0.001, t, 2));
                entries.Add(Entry((uint)(1000 + i) | DECOY_BIT, true, 1.0, 0.5, d, 2));
                map[t] = EntrapmentClass.Target;
                map[d] = EntrapmentClass.Decoy;
            }
            for (int i = 0; i < 200; i++)
            {
                string p = "EP" + i, pd = "EPD" + i;
                bool decoyWins = (i % 2 == 0);
                entries.Add(Entry((uint)(5000 + i), false, decoyWins ? 3.0 : 3.4, 0.01, p, 2));
                entries.Add(Entry((uint)(5000 + i) | DECOY_BIT, true, decoyWins ? 3.4 : 3.0, 0.5, pd, 2));
                map[p] = EntrapmentClass.PTarget;
                map[pd] = EntrapmentClass.PDecoy;
            }
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, map, null, 0.01, "peptide");
            var wf = data.WinFraction;
            Assert.IsNotNull(wf);
            Assert.IsTrue(wf.HasEntrapment);
            // Real pairs: the decoy never outscores its target, so every
            // populated real bin has a decoy-win fraction of exactly 0.
            bool sawReal = false;
            for (int b = 0; b < wf.RealFraction.Length; b++)
            {
                if (wf.RealN[b] > 0)
                {
                    sawReal = true;
                    Assert.AreEqual(0.0, wf.RealFraction[b], 1e-9);
                }
            }
            Assert.IsTrue(sawReal);
            // Entrapment pairs: a fair coin -> a populated bin near 0.5.
            bool sawCoin = false;
            for (int b = 0; b < wf.EntFraction.Length; b++)
            {
                if (wf.EntN[b] > 0 && System.Math.Abs(wf.EntFraction[b] - 0.5) < 0.06)
                    sawCoin = true;
            }
            Assert.IsTrue(sawCoin);
        }

        private static void TestFeatureTableContributions()
        {
            // Two features. Standardized target means (1,1), decoy means (0,0)
            // -> deltaMu = (1,1). Weights (2,-1) -> weighted (2,-1),
            // composite = 1. percent f0 = +200%, f1 = -100% (unexpected).
            var acc = new FeatureContributions.Accumulator(2);
            acc.Add(new[] { 1.0, 1.0 }, false);
            acc.Add(new[] { 0.0, 0.0 }, true);
            var infos = new[]
            {
                new OspreyFeatureInfo("f0", "Feature Zero", false),
                new OspreyFeatureInfo("f1", "Feature One", false),
            };
            var contrib = acc.Build(new List<double[]> { new[] { 2.0, -1.0 } }, infos);

            var entries = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "TA", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "DA", 2),
            };
            var data = ModelDiagnosticsData.Build(Wrap(entries), contrib, null, null, 0.01, "peptide");
            Assert.AreEqual(2, data.Model.Count);
            // Sorted most-influential-first: |200%| before |-100%|.
            Assert.AreEqual("Feature Zero", data.Model[0].Label);
            Assert.AreEqual(200.0, data.Model[0].Percent, 1e-6);
            Assert.IsFalse(data.Model[0].Unexpected);
            Assert.AreEqual("Feature One", data.Model[1].Label);
            Assert.AreEqual(-100.0, data.Model[1].Percent, 1e-6);
            Assert.IsTrue(data.Model[1].Unexpected);
        }

        private static void TestModifiedSequenceMatchesManifest()
        {
            // Manifest is keyed by the bare sequence; the entry carries a
            // modified sequence. Classification must strip the modification.
            var entries = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "PEPTC[UniMod:4]IDEK", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "KEDITC[UniMod:4]PEP", 2),
            };
            var map = new Dictionary<string, EntrapmentClass>
            {
                { "PEPTCIDEK", EntrapmentClass.PTarget },
                { "KEDITCPEP", EntrapmentClass.PDecoy },
            };
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, map, null, 0.01, "peptide");
            Assert.AreEqual(0, data.NUnclassified);
            Assert.AreEqual(1, data.NPTarget);
            Assert.AreEqual(1, data.NPDecoy);
        }

        // ----- helpers -----

        private static void AddEntrap(List<FdrEntry> entries,
            Dictionary<string, EntrapmentClass> map, uint id, double score, string seq)
        {
            entries.Add(Entry(id, false, score, 0.01, seq, 2));
            map[seq] = EntrapmentClass.PTarget;
        }

        private static FdrEntry Entry(uint id, bool decoy, double score, double q, string seq, byte charge)
        {
            return new FdrEntry
            {
                EntryId = id,
                IsDecoy = decoy,
                Score = score,
                RunPeptideQvalue = q,
                // The FDR-calibration views use precursor-level q; set both scopes.
                RunPrecursorQvalue = q,
                ExperimentPrecursorQvalue = q,
                ModifiedSequence = seq,
                Charge = charge,
            };
        }

        private static List<KeyValuePair<string, List<FdrEntry>>> Wrap(List<FdrEntry> entries)
        {
            return new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("file1", entries),
            };
        }
    }
}
