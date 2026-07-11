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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
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
            TestPairedSuppressedForPartialEntrapment();
            TestClassCountsAndDegrade();
            TestWinFractionCoinVsSignal();
            TestFeatureTableContributions();
            TestBaseIdClassificationAndInvalidDrop();
            TestPass2FdpViews();
            TestSidecarRoundTrip();
            TestFeatureHistograms();
            TestModelPass2();
            TestDensityRatioFlatness();
            TestIdYieldPerScope();
            TestCrossRunDetection();
            TestPassingSetHonorsFdrLevel();
        }

        // The "passing at run FDR" set (per-file Summary counts AND cross-run detection)
        // must gate on the run q for the CONFIGURED FDR level via EffectiveRunQvalue,
        // not a hardcoded peptide q -- otherwise a precursor- or both-controlled run is
        // miscounted. Three precursors whose precursor and peptide q straddle the
        // threshold differently make the passing set depend on the level.
        private static void TestPassingSetHonorsFdrLevel()
        {
            var f = new List<FdrEntry>
            {
                EntryPP(1, 0.005, 0.05, "A"),   // passes at precursor q, fails at peptide q
                EntryPP(2, 0.005, 0.05, "B"),   // passes at precursor q, fails at peptide q
                EntryPP(3, 0.05, 0.005, "C"),   // fails at precursor q, passes at peptide q
            };
            // Precursor level -> A, B pass (gated on RunPrecursorQvalue).
            var prec = ModelDiagnosticsData.Build(WrapFiles(f), null, null, null, 1.0, 0.01, FdrLevel.Precursor);
            Assert.AreEqual(2, prec.PerFile[0].Targets);
            CollectionAssert.AreEqual(new[] { 2 }, prec.CrossRun.PerRun.PerRunCount);
            // Peptide level -> only C passes (gated on RunPeptideQvalue).
            var pep = ModelDiagnosticsData.Build(WrapFiles(f), null, null, null, 1.0, 0.01, FdrLevel.Peptide);
            Assert.AreEqual(1, pep.PerFile[0].Targets);
            CollectionAssert.AreEqual(new[] { 1 }, pep.CrossRun.PerRun.PerRunCount);
            // Both level -> max(prec, pep) q; every precursor's max is 0.05 > 0.01 -> none pass.
            var both = ModelDiagnosticsData.Build(WrapFiles(f), null, null, null, 1.0, 0.01, FdrLevel.Both);
            Assert.AreEqual(0, both.PerFile[0].Targets);
            CollectionAssert.AreEqual(new[] { 0 }, both.CrossRun.PerRun.PerRunCount);
        }

        // The identification-yield curve carries BOTH precursor-q scopes over one
        // shared grid so the report's scope selector can switch between them (the
        // report bug: the panel didn't change experiment-wide vs per-run). The two
        // curves genuinely differ when a precursor's experiment-wide and per-run q
        // straddle the threshold.
        private static void TestIdYieldPerScope()
        {
            // Two real targets. T0 clears 1% at BOTH scopes; T1 clears 1% only at the
            // experiment scope (its per-run q is 9%). So at q = 1% the experiment
            // curve has accepted 2 and the per-run curve 1.
            var entries = new List<FdrEntry>
            {
                EntryQ(1, false, 5.0, 0.003, 0.003, "TA", 2),
                EntryQ(2, false, 4.0, 0.09, 0.008, "TB", 2),
            };
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, null, null, 1.0, 0.01, FdrLevel.Peptide);
            var y = data.IdYield;
            Assert.IsNotNull(y);
            Assert.IsNotNull(y.TargetsExperiment);
            Assert.IsNotNull(y.TargetsRun);
            Assert.AreEqual(y.Q.Length, y.TargetsExperiment.Length);
            Assert.AreEqual(y.Q.Length, y.TargetsRun.Length);
            // Index 9 is threshold 0.01 (grid = 0.10*(i+1)/100).
            Assert.AreEqual(0.01, y.Q[9], 1e-12);
            Assert.AreEqual(2, y.TargetsExperiment[9]);   // both clear 1% experiment-wide
            Assert.AreEqual(1, y.TargetsRun[9]);          // only TA clears 1% per-run
            // The two scopes converge once the threshold clears both q's (last point).
            int last = y.Q.Length - 1;
            Assert.AreEqual(2, y.TargetsExperiment[last]);
            Assert.AreEqual(2, y.TargetsRun[last]);
            // Both curves are monotone non-decreasing in q.
            for (int i = 1; i < y.Q.Length; i++)
            {
                Assert.IsTrue(y.TargetsExperiment[i] >= y.TargetsExperiment[i - 1]);
                Assert.IsTrue(y.TargetsRun[i] >= y.TargetsRun[i - 1]);
            }
        }

        // Cross-run detection reproducibility is built unconditionally (no entrapment
        // manifest needed) from the reported per-file passing precursors: cumulative
        // union monotone up, intersection monotone down, the run-count histogram sums
        // to the total unique precursors, at-least-half counts precursors in >= ceil(N/2)
        // runs, and decoys / q-failing entries are excluded.
        private static void TestCrossRunDetection()
        {
            // 3 runs. A passes in all 3, B in 2, C in 1. A decoy passes in run 1 (must
            // be excluded) and C fails the FDR in runs 2-3 (q above runFdr).
            var f1 = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "A", 2),
                Entry(2, false, 5, 0.001, "B", 2),
                Entry(3, false, 5, 0.001, "C", 2),
                Entry(9 | DECOY_BIT, true, 5, 0.001, "DEC", 2),   // decoy: excluded
            };
            var f2 = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "A", 2),
                Entry(2, false, 5, 0.001, "B", 2),
                Entry(3, false, 5, 0.5, "C", 2),                  // C fails FDR here
            };
            var f3 = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "A", 2),
                Entry(2, false, 5, 0.5, "B", 2),                  // B fails FDR here
                Entry(3, false, 5, 0.5, "C", 2),                  // C fails FDR here
            };
            var data = ModelDiagnosticsData.Build(WrapFiles(f1, f2, f3), null, null, null, 1.0, 0.01, FdrLevel.Peptide);
            var cr = data.CrossRun;
            Assert.IsNotNull(cr);
            Assert.IsFalse(data.HasEntrapment);                    // built without a manifest
            var pr = cr.PerRun;
            // No manifest -> no entrapment overlay and no union-FDP curve on either scope.
            Assert.IsNull(pr.EntrapmentRunCountHistogram);
            Assert.IsNull(pr.EntrapmentFdpByRunCount);
            Assert.IsNull(pr.CumUnionEntrapment);
            Assert.IsNull(pr.UnionFdp);
            Assert.IsNull(cr.Experiment.EntrapmentRunCountHistogram);
            Assert.IsNull(cr.Experiment.UnionFdp);
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, pr.PerRunCount);   // A,B,C | A,B | A
            CollectionAssert.AreEqual(new[] { 3, 3, 3 }, pr.CumUnion);      // all 3 unique from run 1
            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, pr.CumIntersection);
            // Histogram index k-1 => #precursors in exactly k runs: C(1), B(2), A(3).
            CollectionAssert.AreEqual(new[] { 1, 1, 1 }, pr.RunCountHistogram);
            Assert.AreEqual(3, pr.RunCountHistogram.Sum());        // == total unique precursors
            Assert.AreEqual(3, pr.CumUnion[pr.CumUnion.Length - 1]);
            Assert.AreEqual(2, pr.AtLeastHalf);                    // A,B in >= ceil(3/2)=2 runs
            Assert.AreEqual(3, cr.RunNames.Length);
            // Union monotone up, intersection monotone down.
            for (int i = 1; i < pr.CumUnion.Length; i++)
            {
                Assert.IsTrue(pr.CumUnion[i] >= pr.CumUnion[i - 1]);
                Assert.IsTrue(pr.CumIntersection[i] <= pr.CumIntersection[i - 1]);
            }
            Assert.AreEqual(2.0, pr.MeanPerRun, 1e-9);             // (3+2+1)/3
            // Here run q == experiment q for every entry (Entry sets both), so the
            // experiment-wide view (gate max(run q, exp q)) matches the per-run view.
            CollectionAssert.AreEqual(pr.PerRunCount, cr.Experiment.PerRunCount);
            CollectionAssert.AreEqual(pr.RunCountHistogram, cr.Experiment.RunCountHistogram);

            // Experiment-wide gate drops a precursor whose run q passes but experiment q
            // does not: max(run q, exp q) <= FDR. G has run q 0.001 in both runs (would be
            // k=2 under per-run) but experiment q 0.20 -> excluded entirely from the
            // experiment view; A (both q small) stays.
            var g1 = new List<FdrEntry> { EntryRunExp(1, 0.001, 0.001, "A"), EntryRunExp(2, 0.001, 0.20, "G") };
            var g2 = new List<FdrEntry> { EntryRunExp(1, 0.001, 0.001, "A"), EntryRunExp(2, 0.001, 0.20, "G") };
            var gcr = ModelDiagnosticsData.Build(WrapFiles(g1, g2), null, null, null, 1.0, 0.01, FdrLevel.Precursor).CrossRun;
            CollectionAssert.AreEqual(new[] { 2, 2 }, gcr.PerRun.PerRunCount);      // A, G both pass run q
            CollectionAssert.AreEqual(new[] { 1, 1 }, gcr.Experiment.PerRunCount);  // G dropped by exp q
            Assert.AreEqual(1, gcr.Experiment.CumUnion[gcr.Experiment.CumUnion.Length - 1]);

            // Entrapment (p_target) precursors are excluded from the reproducibility
            // counts (like the id-yield curve). Base-id 50 -> PTarget.
            var ef1 = new List<FdrEntry> { Entry(1, false, 5, 0.001, "A", 2), Entry(50, false, 5, 0.001, "ENT", 2) };
            var ef2 = new List<FdrEntry> { Entry(1, false, 5, 0.001, "A", 2) };
            var ecls = new Dictionary<uint, EntrapmentClass>
            {
                { 1u, EntrapmentClass.Target }, { 50u, EntrapmentClass.PTarget },
            };
            var ecrFull = ModelDiagnosticsData.Build(WrapFiles(ef1, ef2), null, ecls, null, 1.0, 0.01, FdrLevel.Peptide).CrossRun;
            var ecr = ecrFull.PerRun;
            CollectionAssert.AreEqual(new[] { 1, 1 }, ecr.PerRunCount);  // File 1 A + ENT -> only A; File 2 A
            Assert.AreEqual(1, ecr.CumUnion[ecr.CumUnion.Length - 1]);   // ENT never enters the union
            Assert.AreEqual(1, ecr.RunCountHistogram.Sum());             // just A, in both runs

            // Phase B: the entrapment (p_target) precursors get their own run-count
            // overlay and a per-k entrapment-measured FDP. ENT is in only file 1 (k=1);
            // the real target A is in both (k=2). Both scopes carry the overlay.
            CollectionAssert.AreEqual(new[] { 0, 1 }, ecr.RunCountHistogram);            // A in exactly 2 runs
            CollectionAssert.AreEqual(new[] { 1, 0 }, ecr.EntrapmentRunCountHistogram);  // ENT in exactly 1 run
            // Combined estimator (1 + 1/r) * n_p / (n_t + n_p), r = 1: k=1 slice has
            // n_t=0, n_p=1 -> 2.0; k=2 slice has n_p=0 -> 0.
            Assert.AreEqual(2.0, ecr.EntrapmentFdpByRunCount[0], 1e-9);
            Assert.AreEqual(0.0, ecr.EntrapmentFdpByRunCount[1], 1e-9);
            Assert.IsNotNull(ecrFull.Experiment.EntrapmentRunCountHistogram);

            // Phase C: union FDP vs number of runs. Real union = {A} at both prefixes;
            // entrapment union = {ENT} from run 1 on. UnionFdp[i] = (1 + 1/r) * npU /
            // (ntU + npU); r = 1: (2 * 1) / (1 + 1) = 1.0 at both i.
            CollectionAssert.AreEqual(new[] { 1, 1 }, ecr.CumUnionEntrapment);
            Assert.AreEqual(1.0, ecr.UnionFdp[0], 1e-9);
            Assert.AreEqual(1.0, ecr.UnionFdp[1], 1e-9);

            // The ratio r scales both the per-k and the union FDP: at r = 0.1 the k=1
            // slice reads (1 + 1/0.1) * 1 / 1 = 11.0, and the run-1 union reads
            // (11 * 1) / (1 + 1) = 5.5.
            var rcr = ModelDiagnosticsData.Build(WrapFiles(ef1, ef2), null, ecls, null, 0.1, 0.01, FdrLevel.Peptide)
                .CrossRun.PerRun;
            Assert.AreEqual(11.0, rcr.EntrapmentFdpByRunCount[0], 1e-9);
            Assert.AreEqual(5.5, rcr.UnionFdp[0], 1e-9);
        }

        // The non-parametric null-alignment ratio (Mike's Storey check): the ratio
        // of the per-class score DENSITIES. A matched decoy null gives a FLAT
        // target:decoy plateau on the null-dominated left (small flatness slope); a
        // decoy shifted off the false-target null makes that left side SLOPE (large
        // flatness slope). The p_target:p_decoy reference (both pure null) rides an
        // exactly-flat ratio of 1. Built directly from a synthetic ScoreHistogram so
        // the assertions are deterministic (no binning / percentile dependence).
        private static void TestDensityRatioFlatness()
        {
            const int nb = 60;
            var edges = Edges(nb);
            // A true-hit bump well to the right of the null, shared by both cases, so
            // the target's high-score mass sits above the decoy null either way.
            var trueHit = Bump(nb, 45, 5, 1200);

            // --- Matched: false-target null and decoys share a shape (centered 20).
            // target = 0.30 * decoy (its false component) + the true-hit bump.
            var decoyM = Bump(nb, 20, 5, 2000);
            var targetM = new int[nb];
            for (int i = 0; i < nb; i++)
                targetM[i] = (int)System.Math.Round(0.30 * decoyM[i]) + trueHit[i];
            var hM = Hist(edges, targetM, decoyM, new int[nb], new int[nb]);
            var drM = ModelDiagnosticsData.BuildDensityRatio(hM, false);

            Assert.IsNotNull(drM);
            Assert.IsNotNull(drM.TargetDecoy);
            Assert.IsNull(drM.PTargetPDecoy);                      // no entrapment supplied
            Assert.IsTrue(drM.NullRegionBins >= 4);
            Assert.IsFalse(double.IsNaN(drM.FlatnessSlope));
            // Empty far-left bin -> NaN in the series (never +/-Infinity).
            Assert.IsTrue(double.IsNaN(drM.TargetDecoy[0]));
            // Matched: the left plateau is essentially flat.
            Assert.IsTrue(System.Math.Abs(drM.FlatnessSlope) < 0.15,
                "matched flatness slope should be ~0, was " + drM.FlatnessSlope);
            // Plateau height is the null fraction pi0 (< 1: target carries true hits).
            Assert.IsTrue(drM.PlateauRatio > 0.05 && drM.PlateauRatio < 1.0,
                "matched plateau ratio (pi0) out of range: " + drM.PlateauRatio);

            // --- Shifted: decoys centered LEFT of the false-target null (too weak,
            // the gendecoy signature). Same true-hit bump.
            var decoyS = Bump(nb, 14, 5, 2000);
            var falseS = Bump(nb, 24, 5, 2000);                    // false-target null, shifted right of decoys
            var targetS = new int[nb];
            for (int i = 0; i < nb; i++)
                targetS[i] = (int)System.Math.Round(0.30 * falseS[i]) + trueHit[i];
            var hS = Hist(edges, targetS, decoyS, new int[nb], new int[nb]);
            var drS = ModelDiagnosticsData.BuildDensityRatio(hS, false);

            Assert.IsNotNull(drS);
            Assert.IsFalse(double.IsNaN(drS.FlatnessSlope));
            Assert.IsTrue(drS.NullRegionBins >= 4);
            // The miscalibrated decoy makes the left side clearly slope...
            Assert.IsTrue(System.Math.Abs(drS.FlatnessSlope) > 0.6,
                "shifted flatness slope should be large, was " + drS.FlatnessSlope);
            // ...and much larger than the matched case (the oracle the report leans on).
            Assert.IsTrue(System.Math.Abs(drS.FlatnessSlope) > 4.0 * System.Math.Abs(drM.FlatnessSlope),
                "shifted slope " + drS.FlatnessSlope + " should dwarf matched " + drM.FlatnessSlope);

            // --- Reference line: p_target == p_decoy (identical pure-null arrays) ->
            // the ratio is exactly 1 everywhere, so the reference flatness is exactly 0.
            var pnull = Bump(nb, 20, 5, 1500);
            var hEnt = Hist(edges, targetM, decoyM, (int[])pnull.Clone(), (int[])pnull.Clone());
            var drEnt = ModelDiagnosticsData.BuildDensityRatio(hEnt, true);
            Assert.IsTrue(drEnt.HasEntrapment);
            Assert.IsNotNull(drEnt.PTargetPDecoy);
            // Identical arrays -> unit ratio in every populated bin.
            for (int i = 0; i < nb; i++)
                if (!double.IsNaN(drEnt.PTargetPDecoy[i]))
                    Assert.AreEqual(1.0, drEnt.PTargetPDecoy[i], 1e-9);
            Assert.IsFalse(double.IsNaN(drEnt.RefFlatnessSlope));
            Assert.AreEqual(0.0, drEnt.RefFlatnessSlope, 1e-9);   // pure-null reference is dead flat

            // Too little data to assess -> a ratio object with NaN KPIs, not a throw.
            var hTiny = Hist(edges, Bump(nb, 20, 5, 3), Bump(nb, 20, 5, 3), new int[nb], new int[nb]);
            var drTiny = ModelDiagnosticsData.BuildDensityRatio(hTiny, false);
            Assert.IsNotNull(drTiny);
            Assert.IsTrue(double.IsNaN(drTiny.FlatnessSlope));

            // No histogram to divide -> null.
            Assert.IsNull(ModelDiagnosticsData.BuildDensityRatio(
                new ModelDiagnosticsData.ScoreHistogram { BinEdges = new double[0] }, false));
        }

        // A discretized Gaussian bump (rounded integer counts) over nb unit bins.
        private static int[] Bump(int nb, double center, double sigma, double peak)
        {
            var a = new int[nb];
            for (int i = 0; i < nb; i++)
                a[i] = (int)System.Math.Round(peak *
                    System.Math.Exp(-((i - center) * (i - center)) / (2 * sigma * sigma)));
            return a;
        }

        // Unit-width bin edges 0..nb (centers i+0.5).
        private static double[] Edges(int nb)
        {
            var e = new double[nb + 1];
            for (int i = 0; i <= nb; i++) e[i] = i;
            return e;
        }

        private static ModelDiagnosticsData.ScoreHistogram Hist(
            double[] edges, int[] target, int[] decoy, int[] pTarget, int[] pDecoy)
        {
            return new ModelDiagnosticsData.ScoreHistogram
            {
                BinEdges = edges, Target = target, Decoy = decoy, PTarget = pTarget, PDecoy = pDecoy,
            };
        }

        // The pass-2 model view (the --protein-fdr retrain) is built from the
        // second-pass contributions + the reported pool: a feature table (same
        // most-influential-first ordering + per-feature histograms as pass 1) and a
        // composite score histogram. Null contributions -> null pass (single-pass run).
        private static void TestModelPass2()
        {
            var infos = new[]
            {
                new OspreyFeatureInfo("f0", "Feature Zero", false),
                new OspreyFeatureInfo("f1", "Feature One", false),
            };
            var acc = new FeatureContributions.Accumulator(2, true);
            for (int i = 0; i < 10; i++) acc.Add(new[] { 2.0, 0.5 }, false);
            for (int i = 0; i < 10; i++) acc.Add(new[] { -1.0, 0.0 }, true);
            var contrib = acc.Build(new List<double[]> { new[] { 2.0, -1.0 } }, infos);

            var entries = new List<FdrEntry>
            {
                Entry(1, false, 5, 0.001, "TA", 2),
                Entry(1 | DECOY_BIT, true, 1, 0.5, "DA", 2),
            };
            var cls = new Dictionary<uint, EntrapmentClass> { { 1u, EntrapmentClass.Target } };

            // deltaMu = (2-(-1), 0.5-0) = (3, 0.5); weighted = w*deltaMu = (6, -0.5);
            // composite 5.5; percent f0 = 6/5.5 = 109.09%, f1 = -0.5/5.5 = -9.09% (unexpected: w<0).
            var mp = ModelDiagnosticsData.BuildModelPass2(Wrap(entries), contrib, cls, null);
            Assert.IsNotNull(mp);
            Assert.AreEqual(2, mp.Features.Count);
            Assert.AreEqual("Feature Zero", mp.Features[0].Label);   // sorted by |percent| desc
            Assert.AreEqual(100.0 * 6.0 / 5.5, mp.Features[0].Percent, 1e-6);
            Assert.IsTrue(mp.Features[1].Unexpected);                // f1 weight sign is unexpected
            Assert.IsNotNull(mp.Features[0].TargetHist);             // histograms carried through
            Assert.IsNotNull(mp.Scores);
            Assert.IsTrue(mp.Scores.BinEdges.Length > 1);

            Assert.IsNull(ModelDiagnosticsData.BuildModelPass2(Wrap(entries), null, cls, null));
        }

        // The per-feature standardized-value histograms (Skyline mProphet "Feature
        // Scores" view) are collected only when asked, keyed by feature index, and
        // separate the target mass above the decoy mass for a discriminating
        // feature. Off by default so the production scoring path is untouched.
        private static void TestFeatureHistograms()
        {
            var infos = new[]
            {
                new OspreyFeatureInfo("f0", "Feature Zero", false),
                new OspreyFeatureInfo("f1", "Feature One", false),
            };
            var acc = new FeatureContributions.Accumulator(2, true);
            for (int i = 0; i < 10; i++) acc.Add(new[] { 2.0, 0.5 }, false);   // targets high in f0
            for (int i = 0; i < 10; i++) acc.Add(new[] { -1.0, 0.0 }, true);   // decoys low in f0
            var c = acc.Build(new List<double[]> { new[] { 1.0, 1.0 } }, infos);

            Assert.IsNotNull(c.HistogramEdges);
            Assert.AreEqual(FeatureContributions.HistEdges().Length, c.HistogramEdges.Length);
            Assert.IsNotNull(c.TargetHistograms);
            Assert.AreEqual(2, c.TargetHistograms.Count);
            Assert.AreEqual(10, c.TargetHistograms[0].Sum());
            Assert.AreEqual(10, c.DecoyHistograms[0].Sum());
            // Target mass sits in a higher bin than decoy mass for feature 0.
            Assert.IsTrue(ArgMax(c.TargetHistograms[0]) > ArgMax(c.DecoyHistograms[0]));

            // The report carries them through onto the model rows.
            var entries = new List<FdrEntry> { Entry(1, false, 5, 0.001, "TA", 2), Entry(1 | DECOY_BIT, true, 1, 0.5, "DA", 2) };
            var data = ModelDiagnosticsData.Build(Wrap(entries), c, null, null, 1.0, 0.01, FdrLevel.Peptide);
            Assert.IsNotNull(data.FeatureHistEdges);
            Assert.IsTrue(data.Model.All(m => m.TargetHist != null && m.DecoyHist != null));

            // Default accumulator collects nothing (production path unaffected).
            var acc2 = new FeatureContributions.Accumulator(2);
            acc2.Add(new[] { 1.0, 1.0 }, false);
            var c2 = acc2.Build(new List<double[]> { new[] { 1.0, 1.0 } }, infos);
            Assert.IsNull(c2.HistogramEdges);
            Assert.IsNull(c2.TargetHistograms);
        }

        private static int ArgMax(int[] a)
        {
            int mi = 0;
            for (int i = 1; i < a.Length; i++) if (a[i] > a[mi]) mi = i;
            return mi;
        }

        // Pass-2 FDP views are computed from the final reported pool by the same
        // shared estimator code as pass 1 (BuildFdpViewsFromPrecs), tagged Pass = 2
        // so the HTML FDR view selector can offer both passes. Same class-counting
        // and 1-fold estimator math as pass 1; an empty result when the reported
        // pool carries no entrapment.
        private static void TestPass2FdpViews()
        {
            var entries = new List<FdrEntry>();
            var cls = new Dictionary<uint, EntrapmentClass>();
            for (int i = 0; i < 8; i++)
            {
                entries.Add(Entry((uint)(100 + i), false, 8 - i, 0.001 * (i + 1), "T" + i, 2));
                cls[(uint)(100 + i)] = EntrapmentClass.Target;
            }
            AddEntrap(entries, cls, 200, 5.5, 0.003, "P0");
            AddEntrap(entries, cls, 201, 1.5, 0.005, "P1");

            var views = ModelDiagnosticsData.BuildPass2FdpViews(Wrap(entries), cls, null, 1.0);
            Assert.AreEqual(2, views.Count);                     // experiment + per-run
            Assert.IsTrue(views.All(v => v.Pass == 2));
            var exp = views.Single(v => v.Scope == "experiment");
            Assert.IsTrue(exp.MatchesFdrBench);
            Assert.IsTrue(exp.Label.Contains("Pass 2"));
            // Same FDP math as pass 1: n_t = 8, n_p = 2 -> combined 0.40, lower 0.20.
            int last = exp.Combined.Length - 1;
            Assert.AreEqual(8, exp.NTargetAccepted[last]);
            Assert.AreEqual(0.40, exp.Combined[last], 1e-9);
            Assert.AreEqual(0.20, exp.LowerBound[last], 1e-9);

            // A reported pool with no entrapment -> no pass-2 views to add.
            var noEntrap = ModelDiagnosticsData.BuildPass2FdpViews(
                Wrap(new List<FdrEntry> { Entry(1, false, 5, 0.001, "T", 2) }),
                new Dictionary<uint, EntrapmentClass> { { 1u, EntrapmentClass.Target } }, null, 1.0);
            Assert.AreEqual(0, noEntrap.Count);
        }

        // The pass-1 data model must survive a Newtonsoft round-trip (camelCase +
        // NaN/Infinity as bare literals): FirstJoin stashes it to a sidecar and
        // MergeNode reloads it to append the pass-2 views. Mirrors the settings in
        // ModelDiagnosticsReport.SidecarSettings -- the empty-bin NaN in the
        // win-fraction curve is the round-trip's sharp edge.
        private static void TestSidecarRoundTrip()
        {
            var entries = new List<FdrEntry>();
            var cls = new Dictionary<uint, EntrapmentClass>();
            for (int i = 0; i < 8; i++)
            {
                entries.Add(Entry((uint)(100 + i), false, 8 - i, 0.001 * (i + 1), "T" + i, 2));
                cls[(uint)(100 + i)] = EntrapmentClass.Target;
            }
            AddEntrap(entries, cls, 200, 5.5, 0.003, "P0");
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, FdrLevel.Peptide);

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                FloatFormatHandling = FloatFormatHandling.Symbol,
                FloatParseHandling = FloatParseHandling.Double,
            };
            string json = JsonConvert.SerializeObject(data, settings);
            var back = JsonConvert.DeserializeObject<ModelDiagnosticsData>(json, settings);

            Assert.IsNotNull(back.FdpViews);
            Assert.AreEqual(data.FdpViews.Count, back.FdpViews.Count);
            var a = data.FdpViews.Single(v => v.Scope == "experiment");
            var b = back.FdpViews.Single(v => v.Scope == "experiment");
            Assert.AreEqual(a.Combined.Length, b.Combined.Length);
            Assert.AreEqual(a.Combined[a.Combined.Length - 1],
                b.Combined[b.Combined.Length - 1], 1e-12);
            Assert.AreEqual(data.NPTarget, back.NPTarget);
            Assert.IsNotNull(back.WinFraction);   // empty-bin NaN survived the round-trip
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

            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, pair, 1.0, 0.01, FdrLevel.Peptide);
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            Assert.IsNotNull(fdp.Paired);
            int last = fdp.Paired.Length - 1;
            // At the last target: n_t = 4, n_p = 2, vt = 1 (only P_a, n_p_s_t = 1).
            // paired = (2 + 1) / (4 + 2) = 0.5; lower = 2 / 6.
            Assert.AreEqual(0.5, fdp.Paired[last], 1e-9);
            Assert.AreEqual(2.0 / 6.0, fdp.LowerBound[last], 1e-9);
            // Paired sits at or above lower-bound once an entrapment ranks above its pair.
            Assert.IsTrue(fdp.Paired[last] >= fdp.LowerBound[last]);
            // r == 1 (balanced 1-fold library): paired is shown, not suppressed.
            Assert.IsFalse(fdp.PairedSuppressedPartial);
        }

        // A partial (non-1:1) entrapment library -- e.g. a routine 10% overlay,
        // r != 1 -- must SUPPRESS the paired estimator (it is 1-fold only) while
        // combined and lower-bound stay valid and r-aware. Same fixture as
        // TestPairedEstimator, built with r = 0.1.
        private static void TestPairedSuppressedForPartialEntrapment()
        {
            var entries = new List<FdrEntry>();
            var cls = new Dictionary<uint, EntrapmentClass>();
            var pair = new Dictionary<uint, uint>();
            double[] tscores = { 8, 6, 4, 2 };
            for (int i = 0; i < 4; i++)
            {
                entries.Add(Entry((uint)(10 + i), false, tscores[i], 0.001 * (i + 1), "T" + i, 2));
                cls[(uint)(10 + i)] = EntrapmentClass.Target;
                pair[(uint)(10 + i)] = (uint)i;
            }
            entries.Add(Entry(20, false, 5.0, 0.002, "Pa", 2));
            cls[20] = EntrapmentClass.PTarget; pair[20] = 100;
            entries.Add(Entry(21, false, 3.0, 0.003, "Pb", 2));
            cls[21] = EntrapmentClass.PTarget; pair[21] = 0;

            const double r = 0.1;
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, pair, r, 0.01, FdrLevel.Peptide);
            var fdp = data.FdpViews.Single(v => v.Scope == "experiment");
            // Paired is a 1-fold estimator: suppressed (null) and flagged for r != 1.
            Assert.IsNull(fdp.Paired);
            Assert.IsTrue(fdp.PairedSuppressedPartial);
            // Combined and lower-bound stay populated and r-aware. At the last target
            // n_t = 4, n_p = 2: combined = (1 + 1/r)*2/(4+2), lower = 2/(r*(4+2)).
            Assert.IsNotNull(fdp.Combined);
            Assert.IsNotNull(fdp.LowerBound);
            int last = fdp.Combined.Length - 1;
            Assert.AreEqual((1.0 + 1.0 / r) * 2.0 / 6.0, fdp.Combined[last], 1e-9);
            Assert.AreEqual(2.0 / (r * 6.0), fdp.LowerBound[last], 1e-9);
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, FdrLevel.Peptide);
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, FdrLevel.Peptide);
            Assert.AreEqual(1, data.NTarget);
            Assert.AreEqual(1, data.NDecoy);
            Assert.AreEqual(1, data.NPTarget);
            Assert.AreEqual(1, data.NPDecoy);
            Assert.IsTrue(data.HasEntrapment);

            // No manifest -> degrade to the is_decoy-only split; no FDP views.
            var degraded = ModelDiagnosticsData.Build(Wrap(entries), null, null, null, 1.0, 0.01, FdrLevel.Peptide);
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, FdrLevel.Peptide);
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

            // In THIS fixture the real pairs are native signal (winner ~5.0, above
            // the low-score null band), so only the entrapment coin populates the
            // band -- a fair ~0.5 (the Competition KPI shows the real coin as "-").
            Assert.IsTrue(System.Math.Abs(wf.NullBandEnt - 0.5) < 0.1);

            // Boost signature: real target-decoy pairs sitting IN the low-score null
            // band where the target nonetheless wins ~70% of competitions (real
            // decoy-win ~30%), against entrapment pairs at a fair ~50% in the same
            // band. Only the paired coin sees this -- the real coin collapses below
            // the entrapment ruler (the KPI's headline gap), invisible to every
            // marginal density and to the entrapment FDP.
            var be = new List<FdrEntry>();
            var bc = new Dictionary<uint, EntrapmentClass>();
            for (int i = 0; i < 200; i++)
            {
                bool rDecoy = (i % 10) < 3;                 // real decoy-win 30%
                be.Add(Entry((uint)(1000 + i), false, rDecoy ? 1.7 : 2.0, 0.2, "BR" + i, 2));
                be.Add(Entry((uint)(1000 + i) | DECOY_BIT, true, rDecoy ? 2.0 : 1.7, 0.5, "BRD" + i, 2));
                bc[(uint)(1000 + i)] = EntrapmentClass.Target;
                bool eDecoy = (i % 2 == 0);                 // entrapment coin 50%
                be.Add(Entry((uint)(5000 + i), false, eDecoy ? 1.7 : 2.0, 0.5, "BE" + i, 2));
                be.Add(Entry((uint)(5000 + i) | DECOY_BIT, true, eDecoy ? 2.0 : 1.7, 0.5, "BED" + i, 2));
                bc[(uint)(5000 + i)] = EntrapmentClass.PTarget;
            }
            var bwf = ModelDiagnosticsData.Build(Wrap(be), null, bc, null, 1.0, 0.01, FdrLevel.Peptide).WinFraction;
            Assert.IsTrue(bwf.NullBandReal < 0.4, "real coin should be collapsed: " + bwf.NullBandReal);
            Assert.IsTrue(System.Math.Abs(bwf.NullBandEnt - 0.5) < 0.1, "entrapment coin ~0.5: " + bwf.NullBandEnt);
            Assert.IsTrue(bwf.NullBandEnt - bwf.NullBandReal > 0.1,
                "coin collapse gap positive: " + (bwf.NullBandEnt - bwf.NullBandReal));
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), contrib, null, null, 1.0, 0.01, FdrLevel.Peptide);
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
            var data = ModelDiagnosticsData.Build(Wrap(entries), null, cls, null, 1.0, 0.01, FdrLevel.Peptide);
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
                ExperimentPeptideQvalue = q,
                ModifiedSequence = seq,
                Charge = charge,
            };
        }

        // An entry with distinct per-run and experiment-wide precursor q (for the
        // per-scope yield curve, where the default Entry sets both scopes equal).
        private static FdrEntry EntryQ(uint id, bool decoy, double score,
            double qRun, double qExp, string seq, byte charge)
        {
            return new FdrEntry
            {
                EntryId = id,
                IsDecoy = decoy,
                Score = score,
                RunPeptideQvalue = System.Math.Min(qRun, qExp),
                RunPrecursorQvalue = qRun,
                ExperimentPrecursorQvalue = qExp,
                ModifiedSequence = seq,
                Charge = charge,
            };
        }

        // A non-decoy entry with distinct run precursor / peptide q, for testing that
        // the passing set follows the configured FDR level (EffectiveRunQvalue).
        private static FdrEntry EntryPP(uint id, double runPrecursorQ, double runPeptideQ, string seq)
        {
            return new FdrEntry
            {
                EntryId = id,
                IsDecoy = false,
                Score = 5,
                RunPrecursorQvalue = runPrecursorQ,
                RunPeptideQvalue = runPeptideQ,
                ExperimentPrecursorQvalue = runPrecursorQ,
                ModifiedSequence = seq,
                Charge = 2,
            };
        }

        // A non-decoy entry with distinct run and experiment q (both scopes set to the
        // same run / experiment value), for testing the experiment-wide cross-run gate
        // max(run q, experiment q).
        private static FdrEntry EntryRunExp(uint id, double runQ, double expQ, string seq)
        {
            return new FdrEntry
            {
                EntryId = id,
                IsDecoy = false,
                Score = 5,
                RunPrecursorQvalue = runQ,
                RunPeptideQvalue = runQ,
                ExperimentPrecursorQvalue = expQ,
                ExperimentPeptideQvalue = expQ,
                ModifiedSequence = seq,
                Charge = 2,
            };
        }

        private static List<KeyValuePair<string, List<FdrEntry>>> Wrap(List<FdrEntry> entries)
        {
            return new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("file1", entries),
            };
        }

        // Wrap several per-file entry lists as ordered (fileN -> entries) pairs, for
        // the cross-run detection reproducibility model (which walks files in order).
        private static List<KeyValuePair<string, List<FdrEntry>>> WrapFiles(params List<FdrEntry>[] files)
        {
            var list = new List<KeyValuePair<string, List<FdrEntry>>>();
            for (int i = 0; i < files.Length; i++)
                list.Add(new KeyValuePair<string, List<FdrEntry>>("file" + (i + 1), files[i]));
            return list;
        }
    }
}
