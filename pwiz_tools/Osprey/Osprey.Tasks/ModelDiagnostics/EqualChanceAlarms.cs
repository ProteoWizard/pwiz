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
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using pwiz.Osprey.FDR.ModelDiagnostics;

namespace pwiz.Osprey.Tasks.ModelDiagnostics
{
    /// <summary>
    /// The thresholds of the equal-chance alarms, in one place (issue #4464). Every alarm
    /// reads its number from here and the report prints it beside the measured value, so a
    /// recalibration is a one-line change that shows up in the page as well.
    /// </summary>
    public static class EqualChanceThresholds
    {
        /// <summary>Fewer targets than this in the composite histogram: too few to fit a mixture.</summary>
        public const long MIXTURE_MIN_TARGETS = 1000;
        /// <summary>A mixture component lighter than this is not identified.</summary>
        public const double MIXTURE_MIN_COMPONENT_WEIGHT = 0.02;
        /// <summary>Minimum Ashman's D between the two components; below it the false and true
        /// components overlap too much to name either one the null.</summary>
        public const double MIXTURE_MIN_SEPARATION = 2.0;

        /// <summary>Alarm when the decoy mean sits more than this many <c>f_false</c> standard
        /// deviations from the fitted null's mean, in either direction.</summary>
        public const double LOCATION_SHIFT_ALARM = 0.5;
        /// <summary>Alarm when the decoy standard deviation differs from <c>f_false</c>'s by
        /// more than this factor, in either direction.</summary>
        public const double SCALE_RATIO_ALARM = 1.5;

        /// <summary>diagFDR's q-band edges (<c>dfdr_equal_chance_qbands</c> defaults).</summary>
        public static readonly double[] QBAND_BREAKS = { 0, 0.01, 0.02, 0.05, 0.1, 0.2, 0.5 };
        /// <summary>The low-confidence region pooled for the balance test (diagFDR <c>low_conf</c>).</summary>
        public const double QBAND_POOLED_LO = 0.2;
        public const double QBAND_POOLED_HI = 0.5;
        /// <summary>Below this many pooled entries the balance is not assessed (diagFDR <c>min_N</c>).</summary>
        public const long QBAND_MIN_N = 2000;
        /// <summary>Alarm when <c>|decoy fraction - 0.5|</c> exceeds this (issue #4464).</summary>
        public const double QBAND_DELTA_BALANCE_ALARM = 0.15;

        /// <summary>The fair coin: an entrapment pair's decoy wins half the time.</summary>
        public const double COIN_FAIR = 0.5;
        /// <summary>Alarm when the entrapment-pair decoy-win fraction in the null band is
        /// further than this from 50% (gendecoy measured 22% when it was miscalibrated).</summary>
        public const double COIN_DEVIATION_ALARM = 0.10;
        /// <summary>Fewer entrapment pairs than this in the null band: not assessed.</summary>
        public const long COIN_MIN_PAIRS = 100;

        /// <summary>Granular regime when a run accepts fewer decoys than this at the run FDR:
        /// diagFDR's FDP coefficient-of-variation benchmark <c>sqrt(2 / D)</c> passes 20% at 50.</summary>
        public const int GRANULAR_MIN_DECOYS = 50;
    }

    /// <summary>The verdict of one equal-chance check. Serialized by name.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EqualChanceStatus
    {
        /// <summary>Assessed; within threshold.</summary>
        ok,
        /// <summary>Assessed; past the threshold.</summary>
        alarm,
        /// <summary>The input exists but cannot support the check (see the check's reason).</summary>
        cannot_assess,
        /// <summary>The input this check reads is absent for this pass (no entrapment, no model).</summary>
        not_applicable,
    }

    /// <summary>Why a mixture fit was not accepted as a null reference.</summary>
    [JsonConverter(typeof(StringEnumConverter))]
    public enum EqualChanceGuard
    {
        none,
        too_few_targets,
        not_converged,
        component_weight,
        not_separated,
    }

    /// <summary>
    /// The automated equal-chance detectors of issue #4464 for one pass of the
    /// <c>--model-diagnostics</c> report. A pure derivation over the views
    /// <see cref="ModelDiagnosticsData"/> already carries (the composite-score histogram, the
    /// paired decoy-win fraction, the per-file passing counts), computed when the page is
    /// rendered and spliced into its JSON as <c>equalChance</c>. Nothing here reaches the FDR
    /// path: it reads the report's data and writes only the report.
    /// </summary>
    public sealed class EqualChanceView
    {
        public NullAlignmentCheck NullAlignment { get; set; }
        public QBandBalanceCheck QBandBalance { get; set; }
        public PairedCoinCheck PairedCoin { get; set; }
        public DecoySupportCheck DecoySupport { get; set; }

        /// <summary>Number of checks at <see cref="EqualChanceStatus.alarm"/>.</summary>
        public int AlarmCount { get; set; }

        /// <summary>
        /// (A) + (B): the decoy-independent <c>f_false</c> from a mixture fit of the target
        /// composite scores, and how far the decoys sit from it.
        /// </summary>
        public sealed class NullAlignmentCheck
        {
            public EqualChanceStatus Status { get; set; }
            public EqualChanceGuard Guard { get; set; }
            public EqualChanceMixtureFit Fit { get; set; }
            public long NDecoy { get; set; }
            public double DecoyMean { get; set; } = double.NaN;
            public double DecoyStd { get; set; } = double.NaN;
            /// <summary><c>(decoyMean - falseMean) / falseStd</c>.</summary>
            public double LocationShift { get; set; } = double.NaN;
            /// <summary><c>decoyStd / falseStd</c>.</summary>
            public double ScaleRatio { get; set; } = double.NaN;
            /// <summary>Binned symmetric KL (Jeffreys) divergence of the decoy histogram from
            /// <c>f_false</c>'s bin masses. Reported, not alarmed: a Gaussian null misfits skewed
            /// decoys, which inflates it on a well-calibrated run.</summary>
            public double SymmetricDivergence { get; set; } = double.NaN;
            /// <summary>Entrapment (p_target) mean relative to <c>f_false</c>, in its sigma; NaN
            /// without entrapment. Entrapment is a pure null, so this validates the fit.</summary>
            public double EntrapmentLocationShift { get; set; } = double.NaN;
            /// <summary><c>(decoyMean - entrapmentMean) / falseStd</c>; NaN without entrapment.</summary>
            public double DecoyVsEntrapmentShift { get; set; } = double.NaN;
            public double LocationThreshold { get; set; } = EqualChanceThresholds.LOCATION_SHIFT_ALARM;
            public double ScaleThreshold { get; set; } = EqualChanceThresholds.SCALE_RATIO_ALARM;
        }

        /// <summary>
        /// diagFDR's q-band decoy-fraction check, on q computed from the composite histogram
        /// (bin resolution): under equal chance the decoy fraction of a mismatch-dominated band
        /// sits near 0.5.
        /// </summary>
        public sealed class QBandBalanceCheck
        {
            public EqualChanceStatus Status { get; set; }
            public double[] Breaks { get; set; }
            public long[] BandN { get; set; }
            public long[] BandDecoys { get; set; }
            public double[] BandDecoyFraction { get; set; }
            public double PooledLo { get; set; } = EqualChanceThresholds.QBAND_POOLED_LO;
            public double PooledHi { get; set; } = EqualChanceThresholds.QBAND_POOLED_HI;
            public long PooledN { get; set; }
            public double PooledDecoyFraction { get; set; } = double.NaN;
            /// <summary><c>PooledDecoyFraction - 0.5</c>.</summary>
            public double DeltaBalance { get; set; } = double.NaN;
            public double Threshold { get; set; } = EqualChanceThresholds.QBAND_DELTA_BALANCE_ALARM;
        }

        /// <summary>(C) The entrapment-pair decoy-win fraction in the null band.</summary>
        public sealed class PairedCoinCheck
        {
            public EqualChanceStatus Status { get; set; }
            public double EntrapmentCoin { get; set; } = double.NaN;
            /// <summary>The real-pair coin, for context only: real pairs are confounded by true
            /// positives, so it is never alarmed.</summary>
            public double RealCoin { get; set; } = double.NaN;
            /// <summary>Entrapment pairs in the null band (bins whose centers fall in it).</summary>
            public long EntrapmentPairs { get; set; }
            public double Deviation { get; set; } = double.NaN;
            public double Threshold { get; set; } = EqualChanceThresholds.COIN_DEVIATION_ALARM;
        }

        /// <summary>
        /// (D) Decoy support at the operating cutoff: the decoys each run accepts at the run
        /// FDR (diagFDR's <c>D_alpha</c>), and the FDP coefficient-of-variation benchmark
        /// <c>sqrt(2 / D_alpha)</c> of the thinnest run.
        /// </summary>
        public sealed class DecoySupportCheck
        {
            public EqualChanceStatus Status { get; set; }
            public double Alpha { get; set; }
            public int Runs { get; set; }
            public int MinRunDecoys { get; set; }
            public string MinRunFile { get; set; }
            public double MedianRunDecoys { get; set; } = double.NaN;
            public double FdpCvBenchmark { get; set; } = double.NaN;
            public int Threshold { get; set; } = EqualChanceThresholds.GRANULAR_MIN_DECOYS;
        }
    }

    /// <summary>The <c>equalChance</c> key of the report JSON: one view per pass.</summary>
    public sealed class EqualChanceData
    {
        public EqualChanceView Pass1 { get; set; }
        /// <summary>Null when no second pass ran.</summary>
        public EqualChanceView Pass2 { get; set; }
    }

    /// <summary>
    /// Builds <see cref="EqualChanceData"/> from a <see cref="ModelDiagnosticsData"/>.
    /// </summary>
    public static class EqualChanceAlarms
    {
        /// <summary>The report-JSON key the views are spliced in under.</summary>
        public const string JSON_KEY = @"equalChance";

        public static EqualChanceData Build(ModelDiagnosticsData data)
        {
            if (data == null)
                return null;
            var result = new EqualChanceData
            {
                Pass1 = BuildView(data.Scores, data.WinFraction, data.PerFile, data.RunFdr),
            };
            if (data.Pass2 != null)
            {
                result.Pass2 = BuildView(data.Pass2.Model?.Scores, data.Pass2.WinFraction,
                    data.Pass2.PerFile, data.RunFdr);
            }
            return result;
        }

        /// <summary>One pass's checks, each from the view it reads (any may be null).</summary>
        public static EqualChanceView BuildView(ModelDiagnosticsData.ScoreHistogram scores,
            ModelDiagnosticsData.WinFractionData winFraction,
            IList<ModelDiagnosticsData.FileSummaryRow> perFile, double runFdr)
        {
            var view = new EqualChanceView
            {
                NullAlignment = BuildNullAlignment(scores),
                QBandBalance = BuildQBandBalance(scores),
                PairedCoin = BuildPairedCoin(winFraction),
                DecoySupport = BuildDecoySupport(perFile, runFdr),
            };
            view.AlarmCount = new[]
            {
                view.NullAlignment.Status, view.QBandBalance.Status,
                view.PairedCoin.Status, view.DecoySupport.Status,
            }.Count(s => s == EqualChanceStatus.alarm);
            return view;
        }

        /// <summary>(A) + (B) over the composite histogram.</summary>
        public static EqualChanceView.NullAlignmentCheck BuildNullAlignment(ModelDiagnosticsData.ScoreHistogram h)
        {
            var check = new EqualChanceView.NullAlignmentCheck { Status = EqualChanceStatus.not_applicable };
            if (!HasBins(h))
                return check;
            var edges = h.BinEdges;
            int nb = edges.Length - 1;
            double[] centers = Centers(edges);
            // Targets = every non-decoy (real + entrapment): the population a q counts as
            // targets, whose false part the decoys are meant to model.
            long[] targets = Sum(h.Target, h.PTarget, nb);
            long[] decoys = Sum(h.Decoy, h.PDecoy, nb);
            check.NDecoy = decoys.Sum();
            long nTarget = targets.Sum();
            check.Status = EqualChanceStatus.cannot_assess;
            if (nTarget < EqualChanceThresholds.MIXTURE_MIN_TARGETS)
            {
                check.Guard = EqualChanceGuard.too_few_targets;
                return check;
            }
            var fit = EqualChanceMixtureFit.Fit(centers, targets, edges[1] - edges[0]);
            check.Fit = fit;
            check.Guard = GuardFit(fit);
            if (check.Guard != EqualChanceGuard.none || check.NDecoy == 0)
            {
                if (check.NDecoy == 0)
                    check.Status = EqualChanceStatus.not_applicable;
                return check;
            }

            Moments(centers, decoys, out double dMean, out double dStd);
            check.DecoyMean = dMean;
            check.DecoyStd = dStd;
            check.LocationShift = (dMean - fit.FalseMean) / fit.FalseStd;
            check.ScaleRatio = dStd / fit.FalseStd;
            check.SymmetricDivergence = JeffreysDivergence(decoys,
                EqualChanceMixtureFit.BinMass(edges, fit.FalseMean, fit.FalseStd));
            long[] entrapment = Sum(h.PTarget, null, nb);
            if (entrapment.Sum() > 0)
            {
                Moments(centers, entrapment, out double eMean, out _);
                check.EntrapmentLocationShift = (eMean - fit.FalseMean) / fit.FalseStd;
                check.DecoyVsEntrapmentShift = (dMean - eMean) / fit.FalseStd;
            }
            bool alarm = Math.Abs(check.LocationShift) > EqualChanceThresholds.LOCATION_SHIFT_ALARM ||
                         Math.Abs(Math.Log(check.ScaleRatio)) > Math.Log(EqualChanceThresholds.SCALE_RATIO_ALARM);
            check.Status = alarm ? EqualChanceStatus.alarm : EqualChanceStatus.ok;
            return check;
        }

        /// <summary>diagFDR's q-band decoy fraction, on histogram-resolution q.</summary>
        public static EqualChanceView.QBandBalanceCheck BuildQBandBalance(ModelDiagnosticsData.ScoreHistogram h)
        {
            var breaks = EqualChanceThresholds.QBAND_BREAKS;
            int nBands = breaks.Length - 1;
            var check = new EqualChanceView.QBandBalanceCheck
            {
                Status = EqualChanceStatus.not_applicable,
                Breaks = (double[])breaks.Clone(),
                BandN = new long[nBands],
                BandDecoys = new long[nBands],
                BandDecoyFraction = Enumerable.Repeat(double.NaN, nBands).ToArray(),
            };
            if (!HasBins(h))
                return check;
            int nb = h.BinEdges.Length - 1;
            long[] targets = Sum(h.Target, h.PTarget, nb);
            long[] decoys = Sum(h.Decoy, h.PDecoy, nb);
            if (targets.Sum() == 0 || decoys.Sum() == 0)
                return check;

            // FDR of the threshold at each bin's lower edge, D(>=) / T(>=) accumulated from the
            // top; then q, the smallest FDR at any threshold at or below the score.
            var q = new double[nb];
            long cumT = 0, cumD = 0;
            for (int i = nb - 1; i >= 0; i--)
            {
                cumT += targets[i];
                cumD += decoys[i];
                q[i] = cumT > 0 ? Math.Min(1.0, (double)cumD / cumT) : 1.0;
            }
            MonotoneQ(q);

            long pooledN = 0, pooledD = 0;
            for (int i = 0; i < nb; i++)
            {
                long n = targets[i] + decoys[i];
                if (n == 0)
                    continue;
                int band = BandIndex(breaks, q[i]);
                if (band >= 0)
                {
                    check.BandN[band] += n;
                    check.BandDecoys[band] += decoys[i];
                }
                if (q[i] >= EqualChanceThresholds.QBAND_POOLED_LO && q[i] <= EqualChanceThresholds.QBAND_POOLED_HI)
                {
                    pooledN += n;
                    pooledD += decoys[i];
                }
            }
            for (int b = 0; b < nBands; b++)
            {
                if (check.BandN[b] > 0)
                    check.BandDecoyFraction[b] = (double)check.BandDecoys[b] / check.BandN[b];
            }
            check.PooledN = pooledN;
            check.Status = EqualChanceStatus.cannot_assess;
            if (pooledN < EqualChanceThresholds.QBAND_MIN_N)
                return check;
            check.PooledDecoyFraction = (double)pooledD / pooledN;
            check.DeltaBalance = check.PooledDecoyFraction - 0.5;
            check.Status = Math.Abs(check.DeltaBalance) > EqualChanceThresholds.QBAND_DELTA_BALANCE_ALARM
                ? EqualChanceStatus.alarm
                : EqualChanceStatus.ok;
            return check;
        }

        /// <summary>(C) The entrapment-pair coin in the win-fraction null band.</summary>
        public static EqualChanceView.PairedCoinCheck BuildPairedCoin(ModelDiagnosticsData.WinFractionData wf)
        {
            var check = new EqualChanceView.PairedCoinCheck { Status = EqualChanceStatus.not_applicable };
            if (wf == null)
                return check;
            check.RealCoin = wf.NullBandReal;
            if (!wf.HasEntrapment || wf.EntN == null || wf.BinCenters == null)
                return check;
            long pairs = 0;
            for (int i = 0; i < wf.BinCenters.Length && i < wf.EntN.Length; i++)
            {
                double c = wf.BinCenters[i];
                if (c >= wf.NullBandLo && c <= wf.NullBandHi)
                    pairs += wf.EntN[i];
            }
            check.EntrapmentPairs = pairs;
            check.EntrapmentCoin = wf.NullBandEnt;
            check.Status = EqualChanceStatus.cannot_assess;
            if (pairs < EqualChanceThresholds.COIN_MIN_PAIRS || double.IsNaN(wf.NullBandEnt))
                return check;
            check.Deviation = wf.NullBandEnt - EqualChanceThresholds.COIN_FAIR;
            check.Status = Math.Abs(check.Deviation) > EqualChanceThresholds.COIN_DEVIATION_ALARM
                ? EqualChanceStatus.alarm
                : EqualChanceStatus.ok;
            return check;
        }

        /// <summary>(D) Decoys accepted per run at the run FDR.</summary>
        public static EqualChanceView.DecoySupportCheck BuildDecoySupport(
            IList<ModelDiagnosticsData.FileSummaryRow> perFile, double runFdr)
        {
            var check = new EqualChanceView.DecoySupportCheck
            {
                Status = EqualChanceStatus.not_applicable,
                Alpha = runFdr,
            };
            if (perFile == null || perFile.Count == 0)
                return check;
            check.Runs = perFile.Count;
            var thinnest = perFile[0];
            foreach (var row in perFile)
            {
                if (row.Decoys < thinnest.Decoys)
                    thinnest = row;
            }
            check.MinRunDecoys = thinnest.Decoys;
            check.MinRunFile = thinnest.File;
            var sorted = perFile.Select(r => r.Decoys).OrderBy(d => d).ToArray();
            int mid = sorted.Length / 2;
            check.MedianRunDecoys = sorted.Length % 2 == 1 ? sorted[mid] : 0.5 * (sorted[mid - 1] + sorted[mid]);
            check.FdpCvBenchmark = thinnest.Decoys > 0 ? Math.Sqrt(2.0 / thinnest.Decoys) : double.PositiveInfinity;
            check.Status = thinnest.Decoys < EqualChanceThresholds.GRANULAR_MIN_DECOYS
                ? EqualChanceStatus.alarm
                : EqualChanceStatus.ok;
            return check;
        }

        private static EqualChanceGuard GuardFit(EqualChanceMixtureFit fit)
        {
            if (fit == null || !fit.Converged)
                return EqualChanceGuard.not_converged;
            if (Math.Min(fit.PiFalse, 1 - fit.PiFalse) < EqualChanceThresholds.MIXTURE_MIN_COMPONENT_WEIGHT)
                return EqualChanceGuard.component_weight;
            if (!(fit.Separation >= EqualChanceThresholds.MIXTURE_MIN_SEPARATION))
                return EqualChanceGuard.not_separated;
            return EqualChanceGuard.none;
        }

        private static bool HasBins(ModelDiagnosticsData.ScoreHistogram h)
        {
            return h?.BinEdges != null && h.BinEdges.Length >= 3 && h.Target != null && h.Decoy != null;
        }

        private static double[] Centers(double[] edges)
        {
            var centers = new double[edges.Length - 1];
            for (int i = 0; i < centers.Length; i++)
                centers[i] = 0.5 * (edges[i] + edges[i + 1]);
            return centers;
        }

        // Per-bin a + b over nb bins; either array may be null (an absent class).
        private static long[] Sum(int[] a, int[] b, int nb)
        {
            var s = new long[nb];
            for (int i = 0; i < nb; i++)
            {
                if (a != null && i < a.Length)
                    s[i] += a[i];
                if (b != null && i < b.Length)
                    s[i] += b[i];
            }
            return s;
        }

        private static void Moments(double[] x, long[] w, out double mean, out double std)
        {
            long n = w.Sum();
            mean = 0;
            for (int i = 0; i < x.Length; i++)
                mean += x[i] * w[i];
            mean /= n;
            double var = 0;
            for (int i = 0; i < x.Length; i++)
                var += w[i] * (x[i] - mean) * (x[i] - mean);
            std = Math.Sqrt(var / n);
        }

        // Jeffreys (symmetric KL) divergence sum (p - q) ln(p / q) between the area-normalized
        // counts and a bin-mass vector, both smoothed by a small floor so an empty bin on one
        // side stays finite.
        private static double JeffreysDivergence(long[] counts, double[] mass)
        {
            const double floor = 1e-6;
            double n = counts.Sum();
            var p = new double[counts.Length];
            var q = new double[counts.Length];
            double sp = 0, sq = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                p[i] = counts[i] / n + floor;
                q[i] = mass[i] + floor;
                sp += p[i];
                sq += q[i];
            }
            double j = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                double pi = p[i] / sp, qi = q[i] / sq;
                j += (pi - qi) * Math.Log(pi / qi);
            }
            return j;
        }

        // Turn per-threshold FDR (bins in ascending score order) into q: the minimum FDR over
        // the thresholds at or below each score, a running minimum walking up from the bottom.
        private static void MonotoneQ(double[] q)
        {
            for (int i = 1; i < q.Length; i++)
                q[i] = Math.Min(q[i], q[i - 1]);
        }

        // The band (lo, hi] holding q, the first band closed at 0; -1 past the last break.
        private static int BandIndex(double[] breaks, double q)
        {
            for (int b = 0; b < breaks.Length - 1; b++)
            {
                if (q <= breaks[b + 1] && (b == 0 || q > breaks[b]))
                    return b;
            }
            return -1;
        }
    }
}
