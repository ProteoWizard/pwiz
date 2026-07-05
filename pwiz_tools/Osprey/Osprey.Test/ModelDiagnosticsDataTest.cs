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
            TestBaseIdClassificationAndInvalidDrop();
        }

        // paired = (n_p + vt) / (n_t + n_p), vt = n_p_s_t + 2*n_p_t_s (FDRBench
        // FDPCalcKFold, k = 1). n_p_s_t counts accepted entrapment whose paired
        // target (same pair_index + charge) is NOT accepted or absent; n_p_t_s
        // counts accepted entrapment whose paired target IS accepted and which
        // ranks at or above that target. Matches FDRBench's paired_fdp.
        private static void TestPairedEstimator()
        {
            var entries = new List<FdrEntry>();
            var cls = new Dictionary<uint, EntrapmentClass>();
            var pair = new Dictionary<uint, uint>();
            // 4 real targets, scores 8/6/4/2, pair_index 0..3, base-ids 10..13.
            double[] tscores = { 8, 6, 4, 2 };
            for (int i = 0; i < 4; i++)
            {
                entries.Add(Entry((uint)(10 + i), false, tscores[i], 0.001 * (i + 1), "T" + i, 2));
                cls[(uint)(10 + i)] = EntrapmentClass.Target;
                pair[(uint)(10 + i)] = (uint)i;
            }
            // P_a (score 5) is paired to an UNobserved target (index 100) -> ranks
            // above (absent) target, contributes n_p_s_t. P_b (score 3) is paired
            // to T0 (pair_index 0, q 0.001) but has a WORSE q (0.003) so ranks
            // below its accepted target -> contributes nothing. Both entrapment q
            // fall within the target q range (0.001..0.004) so both count at top.
            entries.Add(Entry(20, false, 5.0, 0.002, "Pa", 2));
            cls[20] = EntrapmentClass.PTarget; pair[20] = 100;
            entries.Add(Entry(21, false, 3.0, 0.003, "Pb", 2));
            cls[21] = EntrapmentClass.PTarget; pair[21] = 0;

            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, pair, 1.0, 0.01, "peptide");
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            Assert.IsNotNull(fdp.Paired);
            int last = fdp.Paired.Length - 1;
            // At the last target: n_t = 4, n_p = 2, vt = 1 (only P_a, n_p_s_t = 1).
            // paired = (2 + 1) / (4 + 2) = 0.5; lower = 2 / 6.
            Assert.AreEqual(0.5, fdp.Paired[last], 1e-9);
            Assert.AreEqual(2.0 / 6.0, fdp.LowerBound[last], 1e-9);
            // Paired sits at or above lower-bound once an entrapment ranks above its pair.
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
            var cls = new Dictionary<uint, EntrapmentClass>();
            for (int i = 0; i < 8; i++)
            {
                entries.Add(Entry((uint)(100 + i), false, 8 - i, 0.001 * (i + 1), "T" + i, 2));
                cls[(uint)(100 + i)] = EntrapmentClass.Target;
            }
            // Both entrapment have q within the target q range (0.001..0.008), so
            // at the highest target q both are counted (n_p = 2).
            AddEntrap(entries, cls, 200, 5.5, 0.003, "P0");
            AddEntrap(entries, cls, 201, 1.5, 0.005, "P1");
            // The entrapment DB ratio r is defined by the manifest composition,
            // not the observed hits: a balanced library gives r = 1, passed in
            // explicitly -- the case the estimator asserts below.
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, "peptide");
            Assert.IsTrue(data.HasEntrapment);
            Assert.IsNotNull(data.FdpViews);
            // Two pass-1 views: experiment-wide (FDRBench-matching) + per-run.
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            Assert.IsTrue(fdp.MatchesFdrBench);
            Assert.AreEqual(1.0, fdp.EntrapmentRatio, 1e-9);

            // At the highest target q (0.008), n_t = 8, n_p = 2 (both entrapment
            // q <= 0.008). combined = 2*2/(8+2) = 0.4, lower = 2/(8+2) = 0.2.
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
            // A target/decoy pair (base-id 1) and an entrapment/p_decoy pair
            // (base-id 2). Decoys share their target's base-id and inherit the
            // partition: base-id 1 decoy -> Decoy, base-id 2 decoy -> PDecoy.
            var entries = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "TA", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "DA", 2),
                Entry(2, false, 4, 0.002, "PA", 2),
                Entry(2 | DECOY_BIT, true, 0.5, 0.6, "PDA", 2),
            };
            var cls = new Dictionary<uint, EntrapmentClass>
            {
                { 1u, EntrapmentClass.Target }, { 2u, EntrapmentClass.PTarget },
            };
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, "peptide");
            Assert.AreEqual(1, data.NTarget);
            Assert.AreEqual(1, data.NDecoy);
            Assert.AreEqual(1, data.NPTarget);
            Assert.AreEqual(1, data.NPDecoy);
            Assert.IsTrue(data.HasEntrapment);

            // No manifest -> degrade to the is_decoy-only split; no FDP views.
            var degraded = ModelDiagnosticsData.Build(Wrap(entries), null, null, null, 1.0, 0.01, "peptide");
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
            var cls = new Dictionary<uint, EntrapmentClass>();
            for (int i = 0; i < 200; i++)
            {
                entries.Add(Entry((uint)(1000 + i), false, 5.0, 0.001, "RT" + i, 2));
                entries.Add(Entry((uint)(1000 + i) | DECOY_BIT, true, 1.0, 0.5, "RD" + i, 2));
                cls[(uint)(1000 + i)] = EntrapmentClass.Target;
            }
            for (int i = 0; i < 200; i++)
            {
                bool decoyWins = (i % 2 == 0);
                entries.Add(Entry((uint)(5000 + i), false, decoyWins ? 3.0 : 3.4, 0.01, "EP" + i, 2));
                entries.Add(Entry((uint)(5000 + i) | DECOY_BIT, true, decoyWins ? 3.4 : 3.0, 0.5, "EPD" + i, 2));
                cls[(uint)(5000 + i)] = EntrapmentClass.PTarget;
            }
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, "peptide");
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), contrib, null, null, 1.0, 0.01, "peptide");
            Assert.AreEqual(2, data.Model.Count);
            // Sorted most-influential-first: |200%| before |-100%|.
            Assert.AreEqual("Feature Zero", data.Model[0].Label);
            Assert.AreEqual(200.0, data.Model[0].Percent, 1e-6);
            Assert.IsFalse(data.Model[0].Unexpected);
            Assert.AreEqual("Feature One", data.Model[1].Label);
            Assert.AreEqual(-100.0, data.Model[1].Percent, 1e-6);
            Assert.IsTrue(data.Model[1].Unexpected);
        }

        // Classification is keyed by the library base-id (EntryId with the decoy
        // high bit cleared) -- exactly the key FDRBench's input writer resolves
        // and looks up in the manifest -- and is independent of the modified
        // sequence. A non-decoy whose base-id is absent from the manifest is
        // FDRBench-"invalid": it becomes Unknown and is excluded from the class
        // counts and the entrapment FDP, reproducing remove_invalid_peptides.
        private static void TestBaseIdClassificationAndInvalidDrop()
        {
            var entries = new List<FdrEntry>
            {
                // base-id 1 -> PTarget (regardless of the modified sequence text)
                Entry(1, false, 5, 0.001, "PEPTC[UniMod:4]IDEK", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "KEDITC[UniMod:4]PEP", 2),
                // base-id 7 is absent from the manifest -> invalid -> Unknown
                Entry(7, false, 4, 0.002, "SOMEUNPAIREDK", 2),
            };
            var cls = new Dictionary<uint, EntrapmentClass> { { 1u, EntrapmentClass.PTarget } };
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, "peptide");
            Assert.AreEqual(1, data.NPTarget);
            Assert.AreEqual(1, data.NPDecoy);
            // The unmatched non-decoy is dropped, NOT counted as a target.
            Assert.AreEqual(0, data.NTarget);
            Assert.AreEqual(2, data.NClassifiedFromManifest); // base-id 1 (target + decoy sides)
            Assert.AreEqual(1, data.NUnclassified);           // base-id 7
        }

        // ----- helpers -----

        private static void AddEntrap(List<FdrEntry> entries,
            Dictionary<uint, EntrapmentClass> cls, uint id, double score, double q, string seq)
        {
            entries.Add(Entry(id, false, score, q, seq, 2));
            cls[id] = EntrapmentClass.PTarget;
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
