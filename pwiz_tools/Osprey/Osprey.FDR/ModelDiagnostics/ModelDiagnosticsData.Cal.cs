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
using System.Linq;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    /// <summary>
    /// The calibration-phase ("CAL") half of the --model-diagnostics data model.
    /// Unlike the Percolator passes, calibration is a PER-FILE, pre-search artifact:
    /// each input file trains its own lightweight LDA over sampled library entries,
    /// then fits its own RT + MS1/MS2 mass corrections. So the CAL view is a list of
    /// per-file rows plus a file selector, and it does NOT move with the Pass 1 / Pass 2
    /// switch. Its four cards mirror the Percolator tabs where they apply
    /// (Model = the calibration-LDA feature contributions + composite score separation;
    /// FDR calibration = entrapment-FDP vs the calibration q + the anchor yield curve;
    /// Reproducibility = the per-file mass-error / RT-correction overview that spots a
    /// file whose calibration went off the rails; Summary = the per-file corrections
    /// table) and drops Density / Competition, which have no per-file calibration analog.
    ///
    /// The heavy per-match arrays are binned to small histograms / swept curves in
    /// <see cref="BuildCalFile"/> at capture time (per file, while the calibration
    /// matches are still live), so only the small <see cref="CalFileRow"/> is retained
    /// and serialized -- the same "already reduced to what a chart needs" contract as
    /// the rest of the model.
    /// </summary>
    public sealed partial class ModelDiagnosticsData
    {
        /// <summary>
        /// The CAL view bundle, or null when the run did not collect calibration
        /// diagnostics (no <c>--model-diagnostics</c>, or a resumed run whose Stage-3
        /// calibration was rehydrated from cache without the matches). Null hides the
        /// CAL tab. Populated at Stage 5 from the per-file calibration capture.
        /// </summary>
        public CalibrationData Cal { get; set; }

        /// <summary>The whole CAL view: one <see cref="CalFileRow"/> per input file (input order).</summary>
        public sealed class CalibrationData
        {
            /// <summary>Per-input-file calibration diagnostics, in input-file order.</summary>
            public List<CalFileRow> Files { get; set; }
            /// <summary>True when the searched library carried FDRBench entrapment (enables the FDP card).</summary>
            public bool HasEntrapment { get; set; }
            /// <summary>Mass-error unit shared by every file: "ppm" (HRAM) or "Th" (unit resolution).</summary>
            public string MassUnit { get; set; }
            /// <summary>Number of files (== Files.Count; convenience for the header).</summary>
            public int FileCount { get; set; }
        }

        /// <summary>
        /// One input file's calibration diagnostics: the trained calibration-LDA
        /// contribution table + composite score histogram (Model card), the
        /// entrapment-FDP-vs-q curve + anchor yield (FDR-calibration card), and the
        /// scalar RT / MS1 / MS2 corrections + anchor counts (Reproducibility overview
        /// and Summary table). All arrays are pre-binned; nothing per-match survives here.
        /// </summary>
        public sealed class CalFileRow
        {
            public string File { get; set; }

            // ----- Model card: calibration-LDA feature contributions + composite dist -----
            /// <summary>Calibration-LDA feature-contribution rows (reuses the Percolator
            /// <see cref="FeatureRow"/> shape; percent = w_j*(meanT_j-meanD_j) / composite).</summary>
            public List<FeatureRow> Features { get; set; }
            /// <summary>Composite calibration-discriminant score histogram by class
            /// (target / decoy / p_target / p_decoy).</summary>
            public ScoreHistogram Scores { get; set; }
            /// <summary>True when the LDA collapsed to a single feature / degenerate model.</summary>
            public bool Degenerate { get; set; }

            // ----- FDR-calibration card: entrapment-FDP vs cal q + yield -----
            /// <summary>Entrapment-FDP (lower / combined estimators) vs the calibration
            /// q-value for this file, or null when the library carries no entrapment.</summary>
            public FdpView Fdp { get; set; }
            /// <summary>Anchor yield vs the calibration q-value (accepted target anchors).
            /// Only the per-run scope is populated (calibration q is per-file).</summary>
            public IdYieldData Yield { get; set; }

            // ----- Reproducibility overview + Summary table: per-file scalars -----
            /// <summary>Confident calibration anchors (target-side, q &lt;= 1%).</summary>
            public int CalPeptides { get; set; }
            /// <summary>Entrapment anchors among the confident set (q &lt;= 1%), when a manifest is present.</summary>
            public int Entrapment { get; set; }
            /// <summary>Combined entrapment-FDP of the confident (q &lt;= 1%) anchor set (NaN when no entrapment).</summary>
            public double AnchorFdp { get; set; }

            /// <summary>MS1 systematic mass correction (mean error) in <see cref="CalibrationData.MassUnit"/>.</summary>
            public double Ms1Mean { get; set; }
            /// <summary>MS1 random mass-error spread (SD).</summary>
            public double Ms1Sd { get; set; }
            /// <summary>MS1 mass-error measurements (M+0 peaks matched).</summary>
            public int Ms1Count { get; set; }
            /// <summary>MS1 post-calibration search tolerance (|mean| + 3*SD).</summary>
            public double Ms1Tol { get; set; }

            /// <summary>MS2 systematic mass correction (mean error).</summary>
            public double Ms2Mean { get; set; }
            /// <summary>MS2 random mass-error spread (SD).</summary>
            public double Ms2Sd { get; set; }
            /// <summary>MS2 mass-error measurements (matched fragments).</summary>
            public int Ms2Count { get; set; }
            /// <summary>MS2 post-calibration search tolerance (|mean| + 3*SD).</summary>
            public double Ms2Tol { get; set; }

            /// <summary>RT calibration points fed to the LOESS/linear fit.</summary>
            public int RtNPoints { get; set; }
            /// <summary>RT fit residual standard deviation (min).</summary>
            public double RtResidualSd { get; set; }
            /// <summary>RT fit coefficient of determination.</summary>
            public double RtRSquared { get; set; }
            /// <summary>RT fit median absolute residual (min).</summary>
            public double RtMad { get; set; }
            /// <summary>Post-calibration RT search half-window (min).</summary>
            public double RtToleranceMin { get; set; }
            /// <summary>Pre-calibration RT search half-window (min) -- the "before" number.</summary>
            public double RtWindowBefore { get; set; }

            /// <summary>True when this file's calibration succeeded (fit accepted, not fallback).</summary>
            public bool Calibrated { get; set; }
        }

        /// <summary>
        /// Transient raw ingredients for one file's calibration diagnostics, produced by
        /// the calibrator (Tasks layer) while the calibration matches are still live and
        /// consumed IMMEDIATELY by <see cref="BuildCalFile"/> (never retained across files,
        /// so the per-match arrays do not accumulate). Not serialized.
        /// </summary>
        public sealed class CalFileInput
        {
            public string File { get; set; }
            public bool Calibrated { get; set; }

            // calibration-LDA model (normalized feature space the weights live in)
            public string[] FeatureNames { get; set; }
            public double[] Weights { get; set; }
            public double[] MeanTarget { get; set; }
            public double[] MeanDecoy { get; set; }
            public bool Degenerate { get; set; }

            // per scored calibration match (all classes); class 0=target,1=decoy,2=p_target,3=p_decoy
            public double[] MatchScores { get; set; }
            public double[] MatchQ { get; set; }
            public int[] MatchClass { get; set; }

            public double EntrapmentRatio { get; set; }
            public bool HasEntrapment { get; set; }
            public string MassUnit { get; set; }

            public double Ms1Mean { get; set; }
            public double Ms1Sd { get; set; }
            public int Ms1Count { get; set; }
            public double Ms1Tol { get; set; }
            public double Ms2Mean { get; set; }
            public double Ms2Sd { get; set; }
            public int Ms2Count { get; set; }
            public double Ms2Tol { get; set; }

            public int RtNPoints { get; set; }
            public double RtResidualSd { get; set; }
            public double RtRSquared { get; set; }
            public double RtMad { get; set; }
            public double RtToleranceMin { get; set; }
            public double RtWindowBefore { get; set; }
        }

        /// <summary>The calibration q-value grid for the CAL FDR-calibration + yield curves.</summary>
        private static readonly double[] CalQGrid =
        {
            0.001, 0.0025, 0.005, 0.0075, 0.01, 0.015, 0.02, 0.03, 0.04, 0.05, 0.075, 0.10,
        };

        /// <summary>Number of bins for the per-file calibration composite score histogram.</summary>
        private const int CalScoreBins = 60;

        /// <summary>
        /// Shape one file's raw calibration ingredients into the small, serializable
        /// <see cref="CalFileRow"/>: the LDA contribution table (weight * mean-gap share),
        /// the class-binned composite score histogram, the entrapment-FDP-vs-q curve and
        /// anchor yield, and the scalar mass / RT corrections. Pure; unit-tested directly.
        /// </summary>
        public static CalFileRow BuildCalFile(CalFileInput inp)
        {
            if (inp == null)
                throw new ArgumentNullException(nameof(inp));

            var row = new CalFileRow
            {
                File = inp.File,
                Calibrated = inp.Calibrated,
                Degenerate = inp.Degenerate,
                Features = BuildCalFeatureRows(inp.FeatureNames, inp.Weights, inp.MeanTarget, inp.MeanDecoy),
                Scores = BinCalScores(inp.MatchScores, inp.MatchClass),
                Ms1Mean = inp.Ms1Mean,
                Ms1Sd = inp.Ms1Sd,
                Ms1Count = inp.Ms1Count,
                Ms1Tol = inp.Ms1Tol,
                Ms2Mean = inp.Ms2Mean,
                Ms2Sd = inp.Ms2Sd,
                Ms2Count = inp.Ms2Count,
                Ms2Tol = inp.Ms2Tol,
                RtNPoints = inp.RtNPoints,
                RtResidualSd = inp.RtResidualSd,
                RtRSquared = inp.RtRSquared,
                RtMad = inp.RtMad,
                RtToleranceMin = inp.RtToleranceMin,
                RtWindowBefore = inp.RtWindowBefore,
            };

            // FDP + yield swept over the shared q grid from the per-match (q, class) arrays.
            SweepCalFdpAndYield(inp, row);
            return row;
        }

        /// <summary>
        /// Calibration-LDA feature-contribution rows, sorted by |percent| descending.
        /// Mirrors the Percolator decomposition: weighted_j = w_j * (meanT_j - meanD_j),
        /// composite = sum_j weighted_j, percent_j = 100 * weighted_j / composite. A
        /// negative weighted_j means the feature separates decoys above targets (unexpected
        /// for the all-positive calibration features), which reds the row like Skyline.
        /// </summary>
        private static List<FeatureRow> BuildCalFeatureRows(
            string[] names, double[] weights, double[] meanT, double[] meanD)
        {
            var rows = new List<FeatureRow>();
            if (names == null || weights == null || meanT == null || meanD == null)
                return rows;
            int n = new[] { names.Length, weights.Length, meanT.Length, meanD.Length }.Min();

            var weighted = new double[n];
            double composite = 0.0;
            for (int j = 0; j < n; j++)
            {
                weighted[j] = weights[j] * (meanT[j] - meanD[j]);
                composite += weighted[j];
            }
            double denom = Math.Abs(composite) > double.Epsilon ? composite : 1.0;

            for (int j = 0; j < n; j++)
            {
                double percent = 100.0 * weighted[j] / denom;
                rows.Add(new FeatureRow
                {
                    Index = j,
                    Label = names[j],
                    Coefficient = weights[j],
                    DeltaMu = meanT[j] - meanD[j],
                    Weighted = weighted[j],
                    Percent = percent,
                    Reversed = weights[j] < 0.0,
                    Unexpected = weighted[j] < 0.0,
                });
            }
            // Stable order by |contribution| desc; Index is the unique secondary key so
            // ties never reorder (the cross-impl unstable-sort guard).
            return rows
                .OrderByDescending(r => Math.Abs(r.Percent))
                .ThenBy(r => r.Index)
                .ToList();
        }

        /// <summary>
        /// Bin the calibration discriminant scores into a per-class
        /// <see cref="ScoreHistogram"/> over the 0.5th-99.9th percentile range, and fit a
        /// normal to the decoy scores (the decoy-overlay). class 0=target,1=decoy,
        /// 2=p_target,3=p_decoy.
        /// </summary>
        private static ScoreHistogram BinCalScores(double[] scores, int[] classes)
        {
            var h = new ScoreHistogram
            {
                BinEdges = new double[CalScoreBins + 1],
                Target = new int[CalScoreBins],
                Decoy = new int[CalScoreBins],
                PTarget = new int[CalScoreBins],
                PDecoy = new int[CalScoreBins],
            };
            if (scores == null || classes == null || scores.Length == 0)
                return h;

            var sorted = scores.Where(s => !double.IsNaN(s)).OrderBy(s => s).ToArray();
            if (sorted.Length == 0)
                return h;
            double lo = CalPercentile(sorted, 0.005);
            double hi = CalPercentile(sorted, 0.999);
            if (!(hi > lo))
            {
                lo = sorted[0];
                hi = sorted[sorted.Length - 1];
                if (!(hi > lo)) { hi = lo + 1.0; }
            }
            double width = (hi - lo) / CalScoreBins;
            for (int b = 0; b <= CalScoreBins; b++)
                h.BinEdges[b] = lo + b * width;

            double decoySum = 0.0, decoySumSq = 0.0;
            long decoyN = 0;
            int n = Math.Min(scores.Length, classes.Length);
            for (int i = 0; i < n; i++)
            {
                double s = scores[i];
                if (double.IsNaN(s))
                    continue;
                int bin = (int)((s - lo) / width);
                if (bin < 0) bin = 0;
                if (bin >= CalScoreBins) bin = CalScoreBins - 1;
                switch (classes[i])
                {
                    case 0: h.Target[bin]++; break;
                    case 1: h.Decoy[bin]++; decoySum += s; decoySumSq += s * s; decoyN++; break;
                    case 2: h.PTarget[bin]++; break;
                    case 3: h.PDecoy[bin]++; break;
                }
            }
            if (decoyN > 1)
            {
                double mean = decoySum / decoyN;
                double var = (decoySumSq - decoySum * decoySum / decoyN) / (decoyN - 1);
                h.DecoyMean = mean;
                h.DecoyStd = var > 0 ? Math.Sqrt(var) : 0.0;
                h.DecoyN = decoyN;
            }
            return h;
        }

        /// <summary>
        /// Walk the shared calibration q grid, and at each threshold count the accepted
        /// target vs entrapment (p_target) anchors to build the entrapment-FDP curve
        /// (lower = N_E/(r*(N_T+N_E)); combined = (1+1/r)*N_E/(N_T+N_E)) and the anchor
        /// yield curve (accepted targets). Decoys are excluded (target-side anchors only).
        /// </summary>
        private static void SweepCalFdpAndYield(CalFileInput inp, CalFileRow row)
        {
            var q = inp.MatchQ;
            var cls = inp.MatchClass;
            double r = inp.EntrapmentRatio;
            var grid = CalQGrid;
            int g = grid.Length;

            var nTarget = new int[g];
            var nEntrap = new int[g];
            if (q != null && cls != null)
            {
                int n = Math.Min(q.Length, cls.Length);
                for (int i = 0; i < n; i++)
                {
                    int c = cls[i];
                    if (c != 0 && c != 2)   // target-side only (0=target, 2=p_target)
                        continue;
                    double qv = q[i];
                    for (int k = 0; k < g; k++)
                    {
                        if (qv <= grid[k])
                        {
                            if (c == 0) nTarget[k]++; else nEntrap[k]++;
                        }
                    }
                }
            }

            row.Yield = new IdYieldData { Q = (double[])grid.Clone(), TargetsRun = nTarget };

            // Confident-set (q<=1%) scalar summary for the Reproducibility / Summary cards.
            int oneIdx = Array.IndexOf(grid, 0.01);
            if (oneIdx >= 0)
            {
                row.CalPeptides = nTarget[oneIdx];
                row.Entrapment = nEntrap[oneIdx];
                int tot = nTarget[oneIdx] + nEntrap[oneIdx];
                row.AnchorFdp = inp.HasEntrapment && tot > 0 && r > 0
                    ? (1.0 + 1.0 / r) * nEntrap[oneIdx] / tot
                    : double.NaN;
            }
            else
            {
                row.AnchorFdp = double.NaN;
            }

            if (!inp.HasEntrapment)
            {
                row.Fdp = null;
                return;
            }

            var lower = new double[g];
            var combined = new double[g];
            for (int k = 0; k < g; k++)
            {
                int tot = nTarget[k] + nEntrap[k];
                if (tot > 0 && r > 0)
                {
                    lower[k] = nEntrap[k] / (r * tot);
                    combined[k] = (1.0 + 1.0 / r) * nEntrap[k] / tot;
                }
                else
                {
                    lower[k] = double.NaN;
                    combined[k] = double.NaN;
                }
            }
            row.Fdp = new FdpView
            {
                Label = @"Calibration q",
                Pass = 0,
                Scope = @"calibration",
                MatchesFdrBench = false,
                EntrapmentRatio = r,
                Q = (double[])grid.Clone(),
                LowerBound = lower,
                Combined = combined,
                Paired = null,
                PairedSuppressedPartial = Math.Abs(r - 1.0) > 0.2,
                NTargetAccepted = nTarget,
            };
        }

        /// <summary>Value at fraction <paramref name="frac"/> of a pre-sorted ascending array.</summary>
        private static double CalPercentile(double[] sortedAsc, double frac)
        {
            if (sortedAsc.Length == 0)
                return double.NaN;
            int idx = (int)Math.Round(frac * (sortedAsc.Length - 1));
            if (idx < 0) idx = 0;
            if (idx >= sortedAsc.Length) idx = sortedAsc.Length - 1;
            return sortedAsc[idx];
        }
    }
}
