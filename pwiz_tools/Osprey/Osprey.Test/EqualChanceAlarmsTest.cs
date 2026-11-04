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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using pwiz.Osprey.FDR.ModelDiagnostics;
using pwiz.Osprey.Tasks.ModelDiagnostics;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Unit tests for the <c>--model-diagnostics</c> equal-chance alarms (issue #4464):
    /// the decoy-independent mixture null, the decoy-vs-null alignment, the q-band balance,
    /// the paired coin, the decoy support, and the additive <c>equalChance</c> report key.
    /// All on synthetic histograms whose answer is known by construction.
    /// </summary>
    [TestClass]
    public class EqualChanceAlarmsTest
    {
        private const double LO = -8, HI = 10;
        private const int BINS = 80;

        [TestMethod]
        public void TestEqualChanceAlarms()
        {
            TestMixtureFitRecoversComponents();
            TestNullAlignmentOkAndAlarm();
            TestNullAlignmentGuard();
            TestQBandBalance();
            TestPairedCoin();
            TestDecoySupport();
            TestReportJsonIsAdditive();
        }

        private static void TestMixtureFitRecoversComponents()
        {
            var h = Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2, 1, 200000));
            var edges = h.BinEdges;
            var centers = new double[BINS];
            var counts = new long[BINS];
            for (int i = 0; i < BINS; i++)
            {
                centers[i] = 0.5 * (edges[i] + edges[i + 1]);
                counts[i] = h.Target[i];
            }
            var fit = EqualChanceMixtureFit.Fit(centers, counts, edges[1] - edges[0]);
            Assert.IsNotNull(fit);
            Assert.IsTrue(fit.Converged);
            Assert.AreEqual(0.7, fit.PiFalse, 0.01);
            Assert.AreEqual(-2, fit.FalseMean, 0.05);
            Assert.AreEqual(1, fit.FalseStd, 0.05);
            Assert.AreEqual(3, fit.TrueMean, 0.05);
            Assert.AreEqual(1.5, fit.TrueStd, 0.05);
            Assert.IsTrue(fit.Separation > EqualChanceThresholds.MIXTURE_MIN_SEPARATION);
        }

        private static void TestNullAlignmentOkAndAlarm()
        {
            // Decoys drawn from the target's own false component: aligned.
            var fair = EqualChanceAlarms.BuildNullAlignment(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2, 1, 200000)));
            Assert.AreEqual(EqualChanceStatus.ok, fair.Status);
            Assert.AreEqual(EqualChanceGuard.none, fair.Guard);
            Assert.AreEqual(0, fair.LocationShift, 0.05);
            Assert.AreEqual(1, fair.ScaleRatio, 0.05);
            Assert.IsTrue(fair.SymmetricDivergence < 0.01);
            Assert.IsTrue(double.IsNaN(fair.EntrapmentLocationShift));

            // Decoys one null sigma weaker than the false targets: the anti-conservative case.
            var weak = EqualChanceAlarms.BuildNullAlignment(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-3, 1, 200000)));
            Assert.AreEqual(EqualChanceStatus.alarm, weak.Status);
            Assert.AreEqual(-1, weak.LocationShift, 0.05);

            // Right location, too narrow: the scale half of the alarm.
            var narrow = EqualChanceAlarms.BuildNullAlignment(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2, 0.5, 200000)));
            Assert.AreEqual(EqualChanceStatus.alarm, narrow.Status);
            Assert.AreEqual(0.5, narrow.ScaleRatio, 0.05);

            // Entrapment from the false component validates the fit.
            var withEntrapment = Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2.5, 1, 200000));
            withEntrapment.PTarget = Counts(Normal(-2, 1, 100000));
            var ent = EqualChanceAlarms.BuildNullAlignment(withEntrapment);
            Assert.IsFalse(double.IsNaN(ent.EntrapmentLocationShift));
            Assert.IsTrue(ent.DecoyVsEntrapmentShift < -0.3);
        }

        private static void TestNullAlignmentGuard()
        {
            // One Gaussian: no second component to separate, so "cannot assess", never an alarm.
            var unimodal = EqualChanceAlarms.BuildNullAlignment(Histogram(Normal(0, 1, 200000), Normal(-3, 1, 200000)));
            Assert.AreEqual(EqualChanceStatus.cannot_assess, unimodal.Status);
            Assert.AreNotEqual(EqualChanceGuard.none, unimodal.Guard);
            Assert.IsTrue(double.IsNaN(unimodal.LocationShift));

            var tooFew = EqualChanceAlarms.BuildNullAlignment(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 500), Normal(-2, 1, 500)));
            Assert.AreEqual(EqualChanceStatus.cannot_assess, tooFew.Status);
            Assert.AreEqual(EqualChanceGuard.too_few_targets, tooFew.Guard);

            Assert.AreEqual(EqualChanceStatus.not_applicable, EqualChanceAlarms.BuildNullAlignment(null).Status);
        }

        private static void TestQBandBalance()
        {
            // Competition-style population: every bin holds a decoy for each false target, so a
            // mismatch-only region reads 50%.
            var fair = EqualChanceAlarms.BuildQBandBalance(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2, 1, 140000)));
            Assert.AreEqual(EqualChanceStatus.ok, fair.Status);
            Assert.IsTrue(fair.PooledN >= EqualChanceThresholds.QBAND_MIN_N);
            Assert.IsTrue(Math.Abs(fair.DeltaBalance) <= EqualChanceThresholds.QBAND_DELTA_BALANCE_ALARM);
            Assert.AreEqual(EqualChanceThresholds.QBAND_BREAKS.Length - 1, fair.BandN.Length);
            // The most confident band is almost all targets.
            Assert.IsTrue(fair.BandDecoyFraction[0] < 0.05);

            // Decoys a null sigma stronger than the false targets: the low-confidence band is
            // decoy-heavy (0.77 by construction).
            var strong = EqualChanceAlarms.BuildQBandBalance(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-1, 1, 140000)));
            Assert.AreEqual(EqualChanceStatus.alarm, strong.Status);
            Assert.IsTrue(strong.DeltaBalance > EqualChanceThresholds.QBAND_DELTA_BALANCE_ALARM);

            // Weaker decoys do NOT trip it (0.52): q-band balance alone is blind to the
            // anti-conservative direction, which is why the null alignment is a separate check.
            var weak = EqualChanceAlarms.BuildQBandBalance(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-3, 1, 140000)));
            Assert.AreEqual(EqualChanceStatus.ok, weak.Status);

            // Too few decoys for q to reach the pooled band: not assessed.
            var thin = EqualChanceAlarms.BuildQBandBalance(
                Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-2, 1, 30000)));
            Assert.AreEqual(EqualChanceStatus.cannot_assess, thin.Status);

            Assert.AreEqual(EqualChanceStatus.not_applicable, EqualChanceAlarms.BuildQBandBalance(null).Status);
        }

        private static void TestPairedCoin()
        {
            Assert.AreEqual(EqualChanceStatus.ok, EqualChanceAlarms.BuildPairedCoin(WinFraction(0.49, 5000)).Status);
            var collapsed = EqualChanceAlarms.BuildPairedCoin(WinFraction(0.22, 5000));
            Assert.AreEqual(EqualChanceStatus.alarm, collapsed.Status);
            Assert.AreEqual(-0.28, collapsed.Deviation, 1e-12);
            Assert.AreEqual(5000, collapsed.EntrapmentPairs);
            Assert.AreEqual(EqualChanceStatus.cannot_assess, EqualChanceAlarms.BuildPairedCoin(WinFraction(0.22, 50)).Status);

            var noEntrapment = WinFraction(0.5, 5000);
            noEntrapment.HasEntrapment = false;
            var na = EqualChanceAlarms.BuildPairedCoin(noEntrapment);
            Assert.AreEqual(EqualChanceStatus.not_applicable, na.Status);
            Assert.AreEqual(0.45, na.RealCoin, 1e-12);
            Assert.AreEqual(EqualChanceStatus.not_applicable, EqualChanceAlarms.BuildPairedCoin(null).Status);
        }

        private static void TestDecoySupport()
        {
            var ok = EqualChanceAlarms.BuildDecoySupport(Files(230, 235, 237), 0.01);
            Assert.AreEqual(EqualChanceStatus.ok, ok.Status);
            Assert.AreEqual(230, ok.MinRunDecoys);
            Assert.AreEqual(@"run0", ok.MinRunFile);
            Assert.AreEqual(235, ok.MedianRunDecoys);
            Assert.AreEqual(Math.Sqrt(2.0 / 230), ok.FdpCvBenchmark, 1e-12);

            var granular = EqualChanceAlarms.BuildDecoySupport(Files(230, 12, 237, 240), 0.01);
            Assert.AreEqual(EqualChanceStatus.alarm, granular.Status);
            Assert.AreEqual(@"run1", granular.MinRunFile);
            Assert.AreEqual(233.5, granular.MedianRunDecoys);

            Assert.AreEqual(EqualChanceStatus.not_applicable, EqualChanceAlarms.BuildDecoySupport(null, 0.01).Status);
        }

        private static void TestReportJsonIsAdditive()
        {
            var data = new ModelDiagnosticsData
            {
                RunFdr = 0.01,
                Scores = Histogram(Mixture(0.7, -2, 1, 3, 1.5, 200000), Normal(-3, 1, 200000)),
                WinFraction = WinFraction(0.22, 5000),
                PerFile = Files(230, 12),
                Pass2 = new ModelDiagnosticsData.Pass2Data { PerFile = Files(300, 310) },
            };
            var page = JObject.Parse(ModelDiagnosticsHtml.SerializeData(data));
            var view1 = page[EqualChanceAlarms.JSON_KEY]?[@"pass1"];
            Assert.IsNotNull(view1);
            Assert.AreEqual(@"alarm", (string)view1[@"nullAlignment"]?[@"status"]);
            Assert.AreEqual(@"alarm", (string)view1[@"pairedCoin"]?[@"status"]);
            Assert.AreEqual(@"alarm", (string)view1[@"decoySupport"]?[@"status"]);
            Assert.AreEqual(3, (int)view1[@"alarmCount"]);
            var view2 = page[EqualChanceAlarms.JSON_KEY]?[@"pass2"];
            Assert.IsNotNull(view2);
            // Pass 2 here has no model or win fraction (pass-2 Percolator is gone), only the counts.
            Assert.AreEqual(@"not_applicable", (string)view2[@"nullAlignment"]?[@"status"]);
            Assert.AreEqual(@"ok", (string)view2[@"decoySupport"]?[@"status"]);

            // Every other key is exactly what the data serializes to on its own.
            page.Remove(EqualChanceAlarms.JSON_KEY);
            var plain = JObject.Parse(JsonConvert.SerializeObject(data, new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include,
                FloatFormatHandling = FloatFormatHandling.String,
            }));
            Assert.IsTrue(JToken.DeepEquals(plain, page));
        }

        private static ModelDiagnosticsData.ScoreHistogram Histogram(Func<double, double> targetCdf,
            Func<double, double> decoyCdf)
        {
            var edges = new double[BINS + 1];
            for (int i = 0; i <= BINS; i++)
                edges[i] = LO + (HI - LO) * i / BINS;
            return new ModelDiagnosticsData.ScoreHistogram
            {
                BinEdges = edges,
                Target = Counts(targetCdf),
                Decoy = Counts(decoyCdf),
                PTarget = new int[BINS],
                PDecoy = new int[BINS],
            };
        }

        // Expected counts per bin of a population given by its scaled CDF (no sampling noise).
        private static int[] Counts(Func<double, double> scaledCdf)
        {
            var counts = new int[BINS];
            for (int i = 0; i < BINS; i++)
            {
                double a = LO + (HI - LO) * i / BINS, b = LO + (HI - LO) * (i + 1) / BINS;
                counts[i] = (int)Math.Round(scaledCdf(b) - scaledCdf(a));
            }
            return counts;
        }

        private static Func<double, double> Normal(double mean, double std, double n)
        {
            return x => n * Phi((x - mean) / std);
        }

        private static Func<double, double> Mixture(double piFalse, double m1, double s1, double m2, double s2, double n)
        {
            return x => n * (piFalse * Phi((x - m1) / s1) + (1 - piFalse) * Phi((x - m2) / s2));
        }

        // Standard normal CDF by numerical integration of the density (test-only; precise
        // enough for the expected bin counts above).
        private static double Phi(double z)
        {
            if (z < -10)
                return 0;
            const int steps = 4000;
            double a = -10, h = (z - a) / steps, sum = 0;
            for (int i = 0; i <= steps; i++)
            {
                double x = a + i * h;
                double f = Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
                sum += (i == 0 || i == steps ? 1 : i % 2 == 1 ? 4 : 2) * f;
            }
            return sum * h / 3;
        }

        private static ModelDiagnosticsData.WinFractionData WinFraction(double entCoin, int entPairsInBand)
        {
            const int nb = 24;
            var centers = new double[nb];
            var entN = new int[nb];
            for (int i = 0; i < nb; i++)
                centers[i] = i;
            // The null band covers centers 0..7; spread the pairs over its 8 bins.
            for (int i = 0; i < 8; i++)
                entN[i] = entPairsInBand / 8;
            entN[0] += entPairsInBand - 8 * (entPairsInBand / 8);
            return new ModelDiagnosticsData.WinFractionData
            {
                BinCenters = centers,
                EntN = entN,
                RealN = new int[nb],
                NullBandLo = 0,
                NullBandHi = 7.5,
                NullBandReal = 0.45,
                NullBandEnt = entCoin,
                HasEntrapment = true,
            };
        }

        private static List<ModelDiagnosticsData.FileSummaryRow> Files(params int[] decoys)
        {
            var rows = new List<ModelDiagnosticsData.FileSummaryRow>();
            for (int i = 0; i < decoys.Length; i++)
            {
                rows.Add(new ModelDiagnosticsData.FileSummaryRow
                {
                    File = @"run" + i,
                    Targets = 100 * decoys[i],
                    Decoys = decoys[i],
                });
            }
            return rows;
        }
    }
}
