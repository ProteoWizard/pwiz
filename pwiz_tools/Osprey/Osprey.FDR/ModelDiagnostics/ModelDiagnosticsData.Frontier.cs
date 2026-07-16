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

using System;
using System.Collections.Generic;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    /// <summary>
    /// The iso-FDR "reproducibility frontier": holding the entrapment-measured FDP at the
    /// nominal target and floating the q-cutoff upward as the run-count requirement rises, how
    /// many real precursors can you accept? For each minimum run count k = 1..N we find the
    /// LOOSEST q-cutoff whose retained set still holds the target FDP, and record the resulting
    /// yield and that cutoff (Q*). The yield rises to a peak (a modest reproducibility
    /// requirement buys a looser score gate) then falls (the run requirement starts discarding
    /// real precursors faster than the looser q recovers them).
    ///
    /// This is a strictly first-pass, PRE-COMPACTION construction: the loosen-q sweep needs the
    /// full un-gated candidate pool, which only the first pass carries. It is null without an
    /// entrapment library (the FDP that defines the frontier is entrapment-measured).
    ///
    /// Computed from the same FDRBench combined estimator the run-count histograms use
    /// ((1 + 1/r) * n_p / (n_t + n_p)); see <see cref="Accumulator"/> for the per-precursor
    /// un-gated run-q tally that feeds it.
    /// </summary>
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>First-pass reproducibility frontier (null without entrapment or on pass 2).</summary>
        public FrontierData Frontier { get; set; }

        /// <summary>One q scope's frontier curve: <see cref="Yield"/>[k-1] real precursors accepted
        /// requiring detection in &gt;= k runs, at the loosest cutoff <see cref="Q"/>[k-1] that holds
        /// the target FDP.</summary>
        public sealed class FrontierScope
        {
            public int[] Yield { get; set; }
            public double[] Q { get; set; }
        }

        /// <summary>
        /// The frontier for both q scopes plus the apex summary the KPI tiles read: the peak yield
        /// and its run-count / cutoff, the cross-scope content overlap at the peak, and the
        /// comparison against the experiment-wide-q "standard" (best-peak, no run requirement).
        /// </summary>
        public sealed class FrontierData
        {
            /// <summary>Nominal FDP target the frontier holds (== run FDR).</summary>
            public double Target { get; set; }
            /// <summary>Run count N.</summary>
            public int N { get; set; }
            /// <summary>Per-run gate: the cutoff is the run-level q.</summary>
            public FrontierScope PerRun { get; set; }
            /// <summary>Experiment-wide gate: the cutoff is the best-peak experiment-wide q; the
            /// run count uses run-level detection at the target.</summary>
            public FrontierScope Experiment { get; set; }
            /// <summary>Experiment-wide q&lt;=target "standard" yield: real precursors with best-peak
            /// exp-q &lt;= target and NO run-count requirement (the baseline the frontier beats).</summary>
            public int BestPeak { get; set; }
            /// <summary>Entrapment FDP of the <see cref="BestPeak"/> set.</summary>
            public double BestPeakFdp { get; set; }
            /// <summary>Run count at the per-run peak.</summary>
            public int PeakK { get; set; }
            /// <summary>Per-run peak yield.</summary>
            public int PerRunPeak { get; set; }
            /// <summary>Experiment-wide peak yield.</summary>
            public int ExpPeak { get; set; }
            /// <summary>Jaccard content overlap of the per-run-optimal and experiment-wide-optimal
            /// peak sets (how much the two routes select the SAME peptides).</summary>
            public double OverlapJaccard { get; set; }
            /// <summary>(PerRunPeak - BestPeak) / PerRunPeak: fraction of achievable detections the
            /// standard forgoes at matched FDP.</summary>
            public double SacrificePct { get; set; }
            /// <summary>(PerRunPeak - BestPeak) / BestPeak: the peak's gain over the standard.</summary>
            public double GainPct { get; set; }
        }

        /// <summary>
        /// q-cutoff sweep grid for the frontier: dense below 2%, coarse to 10%. Shared by the
        /// run-level and experiment-wide sweeps. A candidate detected with effective q above the
        /// last value never enters the frontier (it cannot be admitted even at the loosest cutoff).
        /// </summary>
        internal static readonly double[] FrontierQGrid =
        {
            0.001, 0.002, 0.003, 0.004, 0.005, 0.006, 0.007, 0.008, 0.009,
            0.010, 0.011, 0.012, 0.013, 0.014, 0.015, 0.016, 0.017, 0.018, 0.019,
            0.020, 0.025, 0.030, 0.035, 0.040, 0.045, 0.050,
            0.060, 0.070, 0.080, 0.100,
        };

        /// <summary>Smallest grid index whose threshold is &gt;= q (the bin q first qualifies at),
        /// or -1 when q exceeds the last grid value.</summary>
        internal static int FrontierBin(double q)
        {
            for (int i = 0; i < FrontierQGrid.Length; i++)
                if (q <= FrontierQGrid[i])
                    return i;
            return -1;
        }

        /// <summary>
        /// Per target-side precursor, the un-gated first-pass run-q distribution needed by the
        /// frontier: <see cref="RunQBins"/>[b] counts the files whose effective run-q first
        /// qualifies at grid bin b (so the running sum over b is the run count at each cutoff),
        /// plus the best-peak experiment-wide q and whether the precursor is entrapment.
        /// </summary>
        internal sealed class FrontierPrec
        {
            public readonly ushort[] RunQBins;
            public double MinExpQ = double.MaxValue;
            public bool IsEntrapment;

            public FrontierPrec()
            {
                RunQBins = new ushort[FrontierQGrid.Length];
            }
        }

        /// <summary>
        /// Fold one un-gated first-pass target-side row into the frontier tally: bump the file
        /// count at the row's effective-run-q bin (the running sum over bins is the run count at
        /// each cutoff) and lower the best-peak experiment-wide q. Shared by the streaming
        /// accumulator and the resident batch build so the two byte-match.
        /// </summary>
        internal static void FoldFrontier(Dictionary<string, FrontierPrec> frontier,
            string key, bool isEntrapment, double effRunQ, double effExpQ)
        {
            if (!frontier.TryGetValue(key, out var fp))
            {
                fp = new FrontierPrec { IsEntrapment = isEntrapment };
                frontier[key] = fp;
            }
            int fb = FrontierBin(effRunQ);
            if (fb >= 0 && fp.RunQBins[fb] < ushort.MaxValue)
                fp.RunQBins[fb]++;
            if (effExpQ < fp.MinExpQ)
                fp.MinExpQ = effExpQ;
        }

        /// <summary>
        /// Build the frontier from the per-precursor un-gated run-q tally. Ports the validated
        /// offline reference: per scope and run-count k, sweep the cutoff and keep the loosest
        /// whose FDRBench-combined FDP clears the target, then summarize the apex. Returns null
        /// when there is no separable signal (no real or no entrapment precursors).
        /// </summary>
        internal static FrontierData BuildFrontier(ICollection<FrontierPrec> precs, int n, double r, double target)
        {
            if (precs == null || precs.Count == 0 || n < 2)
                return null;
            int nb = FrontierQGrid.Length;
            double a = 1.0 + 1.0 / r;
            int j1 = FrontierBin(target);   // run-q<=target bin, for the experiment-wide run count
            if (j1 < 0) j1 = nb - 1;

            // Marginal histograms. Per-run: [bin][runCountAtCutoff]. Experiment-wide:
            // [expBin][runCountAtTarget]. counts split real vs entrapment.
            var hrReal = new int[nb, n + 1]; var hrEnt = new int[nb, n + 1];
            var heReal = new int[nb, n + 1]; var heEnt = new int[nb, n + 1];
            int bestReal = 0, bestEnt = 0;   // best-peak standard: exp-q<=target, no run requirement
            foreach (var p in precs)
            {
                int running = 0, rcAtTarget = 0;
                for (int j = 0; j < nb; j++)
                {
                    running += p.RunQBins[j];
                    if (p.IsEntrapment) hrEnt[j, running]++; else hrReal[j, running]++;
                    if (j == j1) rcAtTarget = running;
                }
                int eb = FrontierBin(p.MinExpQ);
                if (eb >= 0)
                {
                    if (p.IsEntrapment) heEnt[eb, rcAtTarget]++; else heReal[eb, rcAtTarget]++;
                    if (p.MinExpQ <= target) { if (p.IsEntrapment) bestEnt++; else bestReal++; }
                }
            }

            var perRun = new int[nb][]; var perRunJ = new int[n + 1];
            var perRunScope = SweepPerRun(hrReal, hrEnt, nb, n, a, target, perRunJ);
            int[] expJ = new int[n + 1];
            var expScope = SweepExperiment(heReal, heEnt, nb, n, a, target, expJ);

            int peakK = 1;
            for (int k = 2; k <= n; k++)
                if (perRunScope.Yield[k - 1] > perRunScope.Yield[peakK - 1]) peakK = k;
            int perRunPeak = perRunScope.Yield[peakK - 1];
            int expPeak = 0;
            for (int k = 1; k <= n; k++) expPeak = Math.Max(expPeak, expScope.Yield[k - 1]);

            double overlap = OverlapAtPeak(precs, peakK, perRunJ[peakK], expJ[peakK], j1);

            return new FrontierData
            {
                Target = target,
                N = n,
                PerRun = perRunScope,
                Experiment = expScope,
                BestPeak = bestReal,
                BestPeakFdp = bestReal + bestEnt > 0 ? a * bestEnt / (bestReal + bestEnt) : double.NaN,
                PeakK = peakK,
                PerRunPeak = perRunPeak,
                ExpPeak = expPeak,
                OverlapJaccard = overlap,
                SacrificePct = perRunPeak > 0 ? (double)(perRunPeak - bestReal) / perRunPeak : 0,
                GainPct = bestReal > 0 ? (double)(perRunPeak - bestReal) / bestReal : 0,
            };
        }

        // Per-run sweep: cutoff = run-level q. yield(k) = max targets over cutoffs whose combined
        // FDP <= target requiring run count >= k. jStar[k] = the winning cutoff bin.
        private static FrontierScope SweepPerRun(int[,] hR, int[,] hE, int nb, int n, double a, double target, int[] jStar)
        {
            // cumulative-from-top over run count for each bin: ge[bin, k] = sum_{rc>=k}.
            var tGe = CumFromTop(hR, nb, n);
            var eGe = CumFromTop(hE, nb, n);
            var yield = new int[n]; var q = new double[n];
            for (int k = 1; k <= n; k++)
            {
                int best = -1, bj = -1;
                for (int j = 0; j < nb; j++)
                {
                    int t = tGe[j, k], e = eGe[j, k];
                    if (t + e == 0) continue;
                    if (a * e / (t + e) <= target && t > best) { best = t; bj = j; }
                }
                yield[k - 1] = best < 0 ? 0 : best; q[k - 1] = bj < 0 ? 0 : FrontierQGrid[bj]; jStar[k] = bj;
            }
            return new FrontierScope { Yield = yield, Q = q };
        }

        // Experiment-wide sweep: cutoff = best-peak exp-q; run count is fixed at the target. The
        // accepted set is exp-q <= cutoff AND run count >= k, so cumulate the [expBin][rc] table
        // over expBin (<=je) then from the top over rc.
        private static FrontierScope SweepExperiment(int[,] hR, int[,] hE, int nb, int n, double a, double target, int[] jStar)
        {
            var cR = CumOverBin(hR, nb, n); var cE = CumOverBin(hE, nb, n);   // [je][rc] = sum over expBin<=je
            var tGe = CumFromTop(cR, nb, n); var eGe = CumFromTop(cE, nb, n); // then >= k over rc
            var yield = new int[n]; var q = new double[n];
            for (int k = 1; k <= n; k++)
            {
                int best = -1, bj = -1;
                for (int je = 0; je < nb; je++)
                {
                    int t = tGe[je, k], e = eGe[je, k];
                    if (t + e == 0) continue;
                    if (a * e / (t + e) <= target && t > best) { best = t; bj = je; }
                }
                yield[k - 1] = best < 0 ? 0 : best; q[k - 1] = bj < 0 ? 0 : FrontierQGrid[bj]; jStar[k] = bj;
            }
            return new FrontierScope { Yield = yield, Q = q };
        }

        // ge[bin, k] = sum_{rc >= k} h[bin, rc], for k = 0..n.
        private static int[,] CumFromTop(int[,] h, int nb, int n)
        {
            var ge = new int[nb, n + 2];
            for (int j = 0; j < nb; j++)
                for (int k = n; k >= 0; k--)
                    ge[j, k] = ge[j, k + 1] + h[j, k];
            return ge;
        }

        // c[je, rc] = sum_{bin <= je} h[bin, rc] (running over the cutoff bin axis).
        private static int[,] CumOverBin(int[,] h, int nb, int n)
        {
            var c = new int[nb, n + 1];
            for (int rc = 0; rc <= n; rc++)
            {
                int run = 0;
                for (int j = 0; j < nb; j++) { run += h[j, rc]; c[j, rc] = run; }
            }
            return c;
        }

        // Content overlap (Jaccard) of the two peak-optimal real-target sets: per-run-optimal
        // (run count at cutoff jr >= peakK) and experiment-wide-optimal (exp-q <= cutoff je AND
        // run count at target >= peakK). Needs the per-precursor joint, so a second pass.
        private static double OverlapAtPeak(ICollection<FrontierPrec> precs, int peakK, int jr, int je, int j1)
        {
            if (jr < 0 || je < 0) return double.NaN;
            double qe = FrontierQGrid[je];
            int both = 0, aOnly = 0, bOnly = 0;
            foreach (var p in precs)
            {
                if (p.IsEntrapment) continue;
                int rcJr = 0, rcTarget = 0;
                for (int j = 0; j <= jr; j++) rcJr += p.RunQBins[j];
                for (int j = 0; j <= j1; j++) rcTarget += p.RunQBins[j];
                bool inA = rcJr >= peakK;
                bool inB = p.MinExpQ <= qe && rcTarget >= peakK;
                if (inA && inB) both++;
                else if (inA) aOnly++;
                else if (inB) bOnly++;
            }
            int uni = both + aOnly + bOnly;
            return uni > 0 ? (double)both / uni : double.NaN;
        }
    }
}
