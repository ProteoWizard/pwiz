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
using System.Text;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR.ModelDiagnostics
{
    /// <summary>
    /// Entrapment classification of a precursor, derived from an FDRBench
    /// pairing manifest (target / decoy / p_target / p_decoy). <see cref="Unknown"/>
    /// is used when no manifest is supplied; the report then degrades to the
    /// is_decoy-only Target/Decoy split.
    /// </summary>
    public enum EntrapmentClass
    {
        Unknown = 0,
        Target,
        Decoy,
        PTarget,
        PDecoy,
    }

    /// <summary>
    /// The full, serializable data model behind the <c>--model-diagnostics</c>
    /// HTML report. Pure computation over the first-pass FDR results
    /// (<see cref="FdrEntry"/> lists), the trained model's
    /// <see cref="FeatureContributions"/>, and an optional per-sequence
    /// entrapment classification from a pairing manifest. Performs no I/O and
    /// pulls in no presentation concern -- the HTML writer (in the Tasks layer)
    /// serializes this to a JSON blob and injects it into the page template.
    ///
    /// Every array here is already reduced to what a chart needs (binned
    /// histograms, thinned q/FDP curves), so the embedded JSON stays small even
    /// for an Astral-scale run.
    /// </summary>
    public sealed class ModelDiagnosticsData
    {
        // ----- run metadata -----
        public string GeneratedUtc { get; set; }
        public string OspreyVersion { get; set; }
        public string OutputName { get; set; }
        public int FileCount { get; set; }
        public int FeatureCount { get; set; }
        public double RunFdr { get; set; }
        public string FdrLevel { get; set; }
        public bool HasEntrapment { get; set; }

        // ----- population counts (best-per-precursor) -----
        public int NTarget { get; set; }
        public int NDecoy { get; set; }
        public int NPTarget { get; set; }
        public int NPDecoy { get; set; }

        // Classification health: how many entries matched the manifest by
        // modified sequence vs fell back to the is_decoy-only split. A large
        // NUnclassified on an entrapment run signals a sequence-key mismatch.
        public int NClassifiedFromManifest { get; set; }
        public int NUnclassified { get; set; }

        // ----- tabs -----
        public double ModelComposite { get; set; }
        public bool ModelDegenerate { get; set; }
        public List<FeatureRow> Model { get; set; }
        public ScoreHistogram Scores { get; set; }
        /// <summary>
        /// The FDR-calibration views the report offers. Each pairs an Osprey
        /// q-value axis (run- vs experiment-scope precursor q, at pass 1 or 2)
        /// with the entrapment FDP estimators. The experiment-scope views
        /// reproduce the FDRBench <c>--fdrbench</c> plots (FDRBench passes Osprey's
        /// own q through); the run-scope views are the per-run picture FDRBench
        /// does not show.
        /// </summary>
        public List<FdpView> FdpViews { get; set; }
        public WinFractionData WinFraction { get; set; }
        public List<FileSummaryRow> PerFile { get; set; }
        public IdYieldData IdYield { get; set; }

        /// <summary>One row of the trained-model feature-contribution table.</summary>
        public sealed class FeatureRow
        {
            public int Index { get; set; }
            public string Label { get; set; }
            public double Coefficient { get; set; }
            public double Percent { get; set; }
            public double DeltaMu { get; set; }
            public double Weighted { get; set; }
            public bool Reversed { get; set; }
            /// <summary>Negative percent contribution: the feature pushes decoys up (Skyline reds this row).</summary>
            public bool Unexpected { get; set; }
        }

        /// <summary>
        /// Best-per-precursor composite-score histogram, one count array per
        /// class over a shared set of bin edges, plus a normal fit to the decoy
        /// scores (the Skyline "decoy normal distribution" overlay).
        /// </summary>
        public sealed class ScoreHistogram
        {
            public double[] BinEdges { get; set; }
            public int[] Target { get; set; }
            public int[] Decoy { get; set; }
            public int[] PTarget { get; set; }
            public int[] PDecoy { get; set; }
            public double DecoyMean { get; set; }
            public double DecoyStd { get; set; }
            public long DecoyN { get; set; }
        }

        /// <summary>
        /// One FDR-calibration curve: an Osprey q-value axis vs the entrapment
        /// FDP estimators (lower-bound / paired / combined), best-per-precursor,
        /// walked down the score ranking. The q axis is either run- or
        /// experiment-scope precursor q, at pass 1 (pre-compaction) or pass 2
        /// (final reported pool).
        /// </summary>
        public sealed class FdpView
        {
            /// <summary>Display label, e.g. "Pass 1 - experiment-wide".</summary>
            public string Label { get; set; }
            /// <summary>1 = pre-compaction, 2 = final reported pool.</summary>
            public int Pass { get; set; }
            /// <summary>"run" or "experiment".</summary>
            public string Scope { get; set; }
            /// <summary>True for the experiment-scope views that reproduce FDRBench's fdp.csv.</summary>
            public bool MatchesFdrBench { get; set; }
            public double EntrapmentRatio { get; set; }
            /// <summary>The Osprey q-value axis (run- or experiment-scope precursor q).</summary>
            public double[] Q { get; set; }
            public double[] LowerBound { get; set; }
            public double[] Combined { get; set; }
            /// <summary>FDRBench paired estimator, or null when no pair_index was supplied.</summary>
            public double[] Paired { get; set; }
            public int[] NTargetAccepted { get; set; }
        }

        /// <summary>
        /// Paired decoy-win fraction vs winner score, for real (target/decoy)
        /// pairs and, when a manifest is present, entrapment (p_target/p_decoy)
        /// pairs. A fair null sits at 0.5 everywhere.
        /// </summary>
        public sealed class WinFractionData
        {
            public double[] BinCenters { get; set; }
            public double[] RealFraction { get; set; }
            public int[] RealN { get; set; }
            public double[] RealCi { get; set; }
            public double[] EntFraction { get; set; }
            public int[] EntN { get; set; }
            public double[] EntCi { get; set; }
            public double NullBandLo { get; set; }
            public double NullBandHi { get; set; }
            public double NullBandReal { get; set; }
            public double NullBandEnt { get; set; }
            public bool HasEntrapment { get; set; }
        }

        /// <summary>Per-input-file passing precursor counts at the run FDR.</summary>
        public sealed class FileSummaryRow
        {
            public string File { get; set; }
            public int Targets { get; set; }
            public int Decoys { get; set; }
        }

        /// <summary>Cumulative accepted-target count vs q-value threshold (the yield curve).</summary>
        public sealed class IdYieldData
        {
            public double[] Q { get; set; }
            public int[] Targets { get; set; }
        }

        // A precursor reduced to the fields the diagnostics need.
        private struct Prec
        {
            public double Score;
            public double QRunPrecursor;
            public double QExpPrecursor;
            public bool IsDecoy;
            public EntrapmentClass Class;
            public uint PairIndex;
            public bool HasPair;
        }

        /// <summary>
        /// Build the full model-diagnostics data model from first-pass results.
        /// </summary>
        /// <param name="perFileEntries">Per-file first-pass FdrEntry lists, scored and q-valued, pre-compaction.</param>
        /// <param name="contributions">The trained model's feature contributions (may be null for non-Percolator FDR).</param>
        /// <param name="classBySequence">
        /// bare sequence -> entrapment class from the pairing manifest, or
        /// null/empty to degrade to the is_decoy-only Target/Decoy split.
        /// </param>
        /// <param name="pairIndexBySequence">
        /// bare sequence -> peptide_pair_index from the manifest (target and its
        /// entrapment share a value), for the paired-FDP estimator. May be null.
        /// </param>
        /// <param name="runFdr">The configured run-level FDR (for the summary counts).</param>
        /// <param name="fdrLevel">Display name of the FDR level used.</param>
        public static ModelDiagnosticsData Build(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<string, EntrapmentClass> classBySequence,
            IReadOnlyDictionary<string, uint> pairIndexBySequence,
            double runFdr,
            string fdrLevel)
        {
            var data = new ModelDiagnosticsData
            {
                RunFdr = runFdr,
                FdrLevel = fdrLevel ?? string.Empty,
                FileCount = perFileEntries.Count,
                Model = new List<FeatureRow>(),
                PerFile = new List<FileSummaryRow>(),
            };

            bool haveManifest = classBySequence != null && classBySequence.Count > 0;

            // ---- best-per-precursor reduction (max score per modseq+charge) ----
            // Keyed within a file by (modseq,charge); a precursor seen in several
            // files keeps its single best score across files, matching the way
            // the sprint prototypes deduped the Stage-5 dump.
            var best = new Dictionary<string, Prec>(StringComparer.Ordinal);
            int nWithClass = 0, nWithoutClass = 0;
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0, fileDecoys = 0;
                foreach (var e in kvp.Value)
                {
                    if (!e.IsDecoy && e.RunPeptideQvalue <= runFdr)
                        fileTargets++;
                    else if (e.IsDecoy && e.RunPeptideQvalue <= runFdr)
                        fileDecoys++;

                    EntrapmentClass cls = Classify(e, classBySequence, haveManifest,
                        ref nWithClass, ref nWithoutClass);
                    uint pairIdx = 0;
                    bool hasPair = pairIndexBySequence != null && e.ModifiedSequence != null &&
                        pairIndexBySequence.TryGetValue(BareSequence(e.ModifiedSequence), out pairIdx);
                    string key = e.ModifiedSequence + "|" + e.Charge;
                    if (!best.TryGetValue(key, out var cur))
                    {
                        cur = new Prec
                        {
                            Score = e.Score,
                            // Precursor-level q at both scopes -- the axes the
                            // FDR-calibration views plot. Experiment scope is
                            // what --fdrbench emits (and thus reproduces its
                            // plots); run scope is the per-run picture. Each
                            // precursor takes its BEST (min) q across observations,
                            // matching FdrBenchInputWriter's dedup, while Score
                            // stays the max (for density / the score histogram).
                            QRunPrecursor = e.RunPrecursorQvalue,
                            QExpPrecursor = e.ExperimentPrecursorQvalue,
                            IsDecoy = e.IsDecoy,
                            Class = cls,
                            PairIndex = pairIdx,
                            HasPair = hasPair,
                        };
                    }
                    else
                    {
                        if (e.Score > cur.Score)
                        {
                            cur.Score = e.Score;
                            cur.IsDecoy = e.IsDecoy;
                            cur.Class = cls;
                            cur.PairIndex = pairIdx;
                            cur.HasPair = hasPair;
                        }
                        if (e.RunPrecursorQvalue < cur.QRunPrecursor)
                            cur.QRunPrecursor = e.RunPrecursorQvalue;
                        if (e.ExperimentPrecursorQvalue < cur.QExpPrecursor)
                            cur.QExpPrecursor = e.ExperimentPrecursorQvalue;
                    }
                    best[key] = cur;
                }
                data.PerFile.Add(new FileSummaryRow { File = kvp.Key, Targets = fileTargets, Decoys = fileDecoys });
            }

            var precs = best.Values.ToList();
            foreach (var p in precs)
            {
                switch (p.Class)
                {
                    case EntrapmentClass.Target: data.NTarget++; break;
                    case EntrapmentClass.Decoy: data.NDecoy++; break;
                    case EntrapmentClass.PTarget: data.NPTarget++; break;
                    case EntrapmentClass.PDecoy: data.NPDecoy++; break;
                }
            }
            data.HasEntrapment = data.NPTarget > 0;
            data.FeatureCount = contributions?.Features.Count ?? 0;
            data.NClassifiedFromManifest = nWithClass;
            data.NUnclassified = nWithoutClass;

            // ---- model table ----
            if (contributions != null)
            {
                data.ModelComposite = contributions.Composite;
                data.ModelDegenerate = contributions.IsDegenerate;
                foreach (var f in contributions.Features
                    .OrderByDescending(f => contributions.IsDegenerate ? 0.0 : Math.Abs(f.Percent))
                    .ThenBy(f => f.Index))
                {
                    data.Model.Add(new FeatureRow
                    {
                        Index = f.Index,
                        Label = f.Label,
                        Coefficient = f.Coefficient,
                        Percent = f.Percent,
                        DeltaMu = f.TargetDecoyMeanGap,
                        Weighted = f.Weighted,
                        Reversed = f.IsReversedScore,
                        // Skyline reds the row on negative percent contribution
                        // (the feature pulls decoys up in the composite), which
                        // is IsUnexpectedDirection when the composite is positive.
                        Unexpected = !contributions.IsDegenerate && f.Percent < 0,
                    });
                }
            }

            // ---- score histogram + decoy normal fit ----
            data.Scores = BuildScoreHistogram(precs);

            // ---- id-yield curve (accepted targets vs q) ----
            data.IdYield = BuildIdYield(precs);

            // ---- paired decoy-win fraction ----
            data.WinFraction = BuildWinFraction(perFileEntries, classBySequence, haveManifest);

            // ---- entrapment FDP calibration (all q-scope views) ----
            if (data.HasEntrapment)
            {
                double r = ComputeEntrapmentRatio(classBySequence);
                if (r <= 0) r = 1.0;
                data.FdpViews = new List<FdpView>();
                // Pass 1 (pre-compaction, this Stage-5 pool). Experiment-scope is
                // the axis --fdrbench emits, so it reproduces the FDRBench plot;
                // run-scope is the per-run view FDRBench does not show. Pass 2
                // (final reported pool) is added by the end-of-run writer.
                var expView = BuildFdpView(precs, r, p => p.QExpPrecursor,
                    @"Pass 1 - experiment-wide", 1, @"experiment", true);
                if (expView != null) data.FdpViews.Add(expView);
                var runView = BuildFdpView(precs, r, p => p.QRunPrecursor,
                    @"Pass 1 - per-run", 1, @"run", false);
                if (runView != null) data.FdpViews.Add(runView);
            }

            return data;
        }

        private static EntrapmentClass Classify(FdrEntry e,
            IReadOnlyDictionary<string, EntrapmentClass> classBySequence, bool haveManifest,
            ref int nWithClass, ref int nWithoutClass)
        {
            if (haveManifest && e.ModifiedSequence != null &&
                classBySequence.TryGetValue(BareSequence(e.ModifiedSequence), out var cls))
            {
                nWithClass++;
                return cls;
            }
            nWithoutClass++;
            return e.IsDecoy ? EntrapmentClass.Decoy : EntrapmentClass.Target;
        }

        private static ScoreHistogram BuildScoreHistogram(List<Prec> precs)
        {
            if (precs.Count == 0)
                return new ScoreHistogram { BinEdges = new double[0] };

            // Robust range: 0.5th..99.9th percentile of all scores, matching the
            // density prototype so extreme tails don't flatten the picture.
            var all = precs.Select(p => p.Score).OrderBy(s => s).ToArray();
            double lo = Percentile(all, 0.5);
            double hi = Percentile(all, 99.9);
            if (hi <= lo)
            {
                lo = all[0];
                hi = all[all.Length - 1] + 1e-9;
            }
            const int nbins = 80;
            var edges = new double[nbins + 1];
            for (int i = 0; i <= nbins; i++)
                edges[i] = lo + (hi - lo) * i / nbins;

            var h = new ScoreHistogram
            {
                BinEdges = edges,
                Target = new int[nbins],
                Decoy = new int[nbins],
                PTarget = new int[nbins],
                PDecoy = new int[nbins],
            };
            double dSum = 0, dSum2 = 0;
            long dN = 0;
            foreach (var p in precs)
            {
                int b = BinIndex(edges, p.Score);
                if (b >= 0)
                {
                    switch (p.Class)
                    {
                        case EntrapmentClass.Target: h.Target[b]++; break;
                        case EntrapmentClass.Decoy: h.Decoy[b]++; break;
                        case EntrapmentClass.PTarget: h.PTarget[b]++; break;
                        case EntrapmentClass.PDecoy: h.PDecoy[b]++; break;
                    }
                }
                if (p.Class == EntrapmentClass.Decoy || p.Class == EntrapmentClass.PDecoy)
                {
                    dSum += p.Score;
                    dSum2 += p.Score * p.Score;
                    dN++;
                }
            }
            if (dN > 1)
            {
                double mean = dSum / dN;
                double var = Math.Max(0, dSum2 / dN - mean * mean);
                h.DecoyMean = mean;
                h.DecoyStd = Math.Sqrt(var);
                h.DecoyN = dN;
            }
            return h;
        }

        private static IdYieldData BuildIdYield(List<Prec> precs)
        {
            // Cumulative accepted real-target precursors as the reported-q
            // threshold sweeps [0, 0.1]. Uses a fixed grid so the JSON is small.
            var qs = precs.Where(p => !p.IsDecoy && p.Class != EntrapmentClass.PTarget
                                       && p.Class != EntrapmentClass.PDecoy)
                          .Select(p => p.QExpPrecursor).OrderBy(q => q).ToArray();
            const int steps = 100;
            var grid = new double[steps];
            var counts = new int[steps];
            for (int i = 0; i < steps; i++)
            {
                double thr = 0.10 * (i + 1) / steps;
                grid[i] = thr;
                counts[i] = UpperBound(qs, thr);
            }
            return new IdYieldData { Q = grid, Targets = counts };
        }

        private static WinFractionData BuildWinFraction(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, EntrapmentClass> classBySequence, bool haveManifest)
        {
            // base_id -> best target-side score, target-side class, best decoy-side score
            const uint BASE_ID_MASK = 0x7FFFFFFF;
            var bt = new Dictionary<uint, double[]>();      // [tScore, dScore]
            var tClass = new Dictionary<uint, EntrapmentClass>();
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    uint bid = e.EntryId & BASE_ID_MASK;
                    if (!bt.TryGetValue(bid, out var slot))
                    {
                        slot = new[] { double.NegativeInfinity, double.NegativeInfinity };
                        bt[bid] = slot;
                    }
                    if (e.IsDecoy)
                    {
                        if (e.Score > slot[1]) slot[1] = e.Score;
                    }
                    else
                    {
                        if (e.Score > slot[0])
                        {
                            slot[0] = e.Score;
                            tClass[bid] = haveManifest && e.ModifiedSequence != null &&
                                          classBySequence.TryGetValue(BareSequence(e.ModifiedSequence), out var c)
                                ? c : EntrapmentClass.Target;
                        }
                    }
                }
            }

            var realWin = new List<KeyValuePair<double, bool>>();   // (winnerScore, decoyWon)
            var entWin = new List<KeyValuePair<double, bool>>();
            foreach (var pair in bt)
            {
                var slot = pair.Value;
                if (double.IsNegativeInfinity(slot[0]) || double.IsNegativeInfinity(slot[1]))
                    continue; // need both sides
                double winner = Math.Max(slot[0], slot[1]);
                bool decoyWon = slot[1] >= slot[0];
                EntrapmentClass c = tClass.TryGetValue(pair.Key, out var cc) ? cc : EntrapmentClass.Target;
                if (c == EntrapmentClass.PTarget)
                    entWin.Add(new KeyValuePair<double, bool>(winner, decoyWon));
                else if (c == EntrapmentClass.Target)
                    realWin.Add(new KeyValuePair<double, bool>(winner, decoyWon));
            }

            // Shared score bins over the pooled winner scores.
            var wf = new WinFractionData { HasEntrapment = entWin.Count > 0 };
            var pooled = realWin.Concat(entWin).Select(p => p.Key).OrderBy(s => s).ToArray();
            if (pooled.Length == 0)
                return wf;
            double lo = Percentile(pooled, 1.0);
            double hi = Percentile(pooled, 99.0);
            if (hi <= lo) { lo = pooled[0]; hi = pooled[pooled.Length - 1] + 1e-9; }
            const int nbins = 24;
            var edges = new double[nbins + 1];
            for (int i = 0; i <= nbins; i++)
                edges[i] = lo + (hi - lo) * i / nbins;

            Curve(realWin, edges, out var rCenters, out var rFrac, out var rN, out var rCi);
            Curve(entWin, edges, out _, out var eFrac, out var eN, out var eCi);
            wf.BinCenters = rCenters;
            wf.RealFraction = rFrac; wf.RealN = rN; wf.RealCi = rCi;
            wf.EntFraction = eFrac; wf.EntN = eN; wf.EntCi = eCi;

            // Null band summary over the low half of the score range.
            wf.NullBandLo = lo;
            wf.NullBandHi = lo + (hi - lo) * 0.35;
            wf.NullBandReal = BandFraction(realWin, wf.NullBandLo, wf.NullBandHi);
            wf.NullBandEnt = BandFraction(entWin, wf.NullBandLo, wf.NullBandHi);
            return wf;
        }

        private static void Curve(List<KeyValuePair<double, bool>> data, double[] edges,
            out double[] centers, out double[] frac, out int[] n, out double[] ci)
        {
            int nb = edges.Length - 1;
            var c = new double[nb];
            var fr = new double[nb];
            var nn = new int[nb];
            var cc = new double[nb];
            var win = new int[nb];
            var tot = new int[nb];
            foreach (var d in data)
            {
                int b = BinIndex(edges, d.Key);
                if (b < 0) continue;
                tot[b]++;
                if (d.Value) win[b]++;
            }
            for (int b = 0; b < nb; b++)
            {
                c[b] = 0.5 * (edges[b] + edges[b + 1]);
                nn[b] = tot[b];
                if (tot[b] > 0)
                {
                    double pf = (double)win[b] / tot[b];
                    fr[b] = pf;
                    cc[b] = 1.96 * Math.Sqrt(pf * (1 - pf) / tot[b]);
                }
                else
                {
                    fr[b] = double.NaN;
                    cc[b] = 0;
                }
            }
            centers = c; frac = fr; n = nn; ci = cc;
        }

        private static double BandFraction(List<KeyValuePair<double, bool>> data, double lo, double hi)
        {
            int win = 0, tot = 0;
            foreach (var d in data)
            {
                if (d.Key >= lo && d.Key <= hi)
                {
                    tot++;
                    if (d.Value) win++;
                }
            }
            return tot > 0 ? (double)win / tot : double.NaN;
        }

        // Build one calibration view: entrapment FDP estimators vs the selected
        // Osprey q-value axis, best-per-precursor, walked down the score ranking.
        // Reproduces FDRBench's fdp.csv when qSel is the experiment-precursor q
        // (which is what --fdrbench emits and FDRBench merely passes through), and
        // gives the per-run picture when qSel is the run-precursor q. r =
        // entrapment DB size / target DB size, from the manifest.
        private static FdpView BuildFdpView(List<Prec> precs, double r,
            Func<Prec, double> qSel, string label, int pass, string scope, bool matchesFdrBench)
        {
            // FDRBench thresholds each class by ITS OWN q-value: at q <= t,
            // n_t = targets with q <= t and n_p = entrapment with q <= t. So sort
            // each class by q and sweep t up through the target q-values. (Ranking
            // by score and tallying entrapment interspersed undercounts entrapment
            // whose own q clears the threshold at a lower score -- verified against
            // fdrbench: q-threshold counting reproduces fdp.csv, score-ranking does
            // not.) For the paired estimator, an entrapment "wins its pair"
            // (n_p_s_t) when it outscores its paired target, or that target went
            // unobserved.
            var pairTargetScore = new Dictionary<uint, double>();
            foreach (var p in precs)
            {
                if (p.Class == EntrapmentClass.Target && p.HasPair)
                {
                    if (!pairTargetScore.TryGetValue(p.PairIndex, out double s) || p.Score > s)
                        pairTargetScore[p.PairIndex] = p.Score;
                }
            }

            var targetQ = new List<double>();
            var entrap = new List<KeyValuePair<double, bool>>();  // (q, wonItsPair)
            foreach (var p in precs)
            {
                if (p.IsDecoy)
                    continue;
                if (p.Class == EntrapmentClass.Target)
                {
                    targetQ.Add(qSel(p));
                }
                else if (p.Class == EntrapmentClass.PTarget)
                {
                    bool won = p.HasPair &&
                        (!pairTargetScore.TryGetValue(p.PairIndex, out double ts) || p.Score > ts);
                    entrap.Add(new KeyValuePair<double, bool>(qSel(p), won));
                }
            }
            if (targetQ.Count == 0)
                return null;
            bool anyPair = entrap.Count > 0 && pairTargetScore.Count > 0;
            targetQ.Sort();
            entrap.Sort((a, b) => a.Key.CompareTo(b.Key));

            var qs = new List<double>();
            var lb = new List<double>();
            var comb = new List<double>();
            var paired = new List<double>();
            var ntAcc = new List<int>();

            int ei = 0, nE = 0, nPst = 0;
            for (int i = 0; i < targetQ.Count; i++)
            {
                double t = targetQ[i];
                while (ei < entrap.Count && entrap[ei].Key <= t)
                {
                    nE++;
                    if (entrap[ei].Value) nPst++;
                    ei++;
                }
                int nT = i + 1;
                // FDRBench estimators, verified against fdrbench fdp.csv:
                //   combined    = (1 + 1/r) * n_p / (n_t + n_p)
                //   lower_bound = n_p / (r * (n_t + n_p))
                //   paired      = (n_p + n_p_s_t) / (n_t + n_p)
                double denom = nT + nE;
                qs.Add(t);
                lb.Add(nE / (r * denom));
                comb.Add((1.0 + 1.0 / r) * nE / denom);
                paired.Add((nE + nPst) / denom);
                ntAcc.Add(nT);
            }

            return new FdpView
            {
                Label = label,
                Pass = pass,
                Scope = scope,
                MatchesFdrBench = matchesFdrBench,
                EntrapmentRatio = r,
                Q = Thin(qs),
                LowerBound = Thin(lb),
                Combined = Thin(comb),
                Paired = anyPair ? Thin(paired) : null,
                NTargetAccepted = Thin(ntAcc),
            };
        }

        private static double ComputeEntrapmentRatio(IReadOnlyDictionary<string, EntrapmentClass> classBySequence)
        {
            if (classBySequence == null) return 1.0;
            int nT = 0, nP = 0;
            foreach (var v in classBySequence.Values)
            {
                if (v == EntrapmentClass.Target) nT++;
                else if (v == EntrapmentClass.PTarget) nP++;
            }
            return nT > 0 ? (double)nP / nT : 1.0;
        }

        // Strip modification annotations (e.g. "C[UniMod:4]" -> "C") so a
        // modified precursor's sequence matches the pairing manifest, which is
        // keyed by the bare peptide sequence. Handles nested/successive
        // brackets and leaves an unmodified sequence untouched (no allocation).
        private static string BareSequence(string modSeq)
        {
            if (string.IsNullOrEmpty(modSeq) || modSeq.IndexOf('[') < 0)
                return modSeq;
            var sb = new StringBuilder(modSeq.Length);
            int depth = 0;
            foreach (char c in modSeq)
            {
                if (c == '[') depth++;
                else if (c == ']') { if (depth > 0) depth--; }
                else if (depth == 0) sb.Append(c);
            }
            return sb.ToString();
        }

        // ----- small numeric helpers -----

        private static int BinIndex(double[] edges, double x)
        {
            if (edges.Length < 2 || x < edges[0] || x > edges[edges.Length - 1])
                return -1;
            int lo = 0, hi = edges.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (x >= edges[mid]) lo = mid; else hi = mid;
            }
            return lo;
        }

        private static double Percentile(double[] sortedAsc, double pct)
        {
            if (sortedAsc.Length == 0) return 0;
            double rank = pct / 100.0 * (sortedAsc.Length - 1);
            int lo = (int)Math.Floor(rank);
            int hi = (int)Math.Ceiling(rank);
            if (lo == hi) return sortedAsc[lo];
            double frac = rank - lo;
            return sortedAsc[lo] * (1 - frac) + sortedAsc[hi] * frac;
        }

        private static int UpperBound(double[] sortedAsc, double value)
        {
            int lo = 0, hi = sortedAsc.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (sortedAsc[mid] <= value) lo = mid + 1; else hi = mid;
            }
            return lo;
        }

        // Thin a monotone-ish series to at most ~400 points for a compact JSON.
        private static double[] Thin(List<double> xs)
        {
            return ThinIndices(xs.Count).Select(i => xs[i]).ToArray();
        }

        private static int[] Thin(List<int> xs)
        {
            return ThinIndices(xs.Count).Select(i => xs[i]).ToArray();
        }

        private static IEnumerable<int> ThinIndices(int count)
        {
            const int cap = 400;
            if (count <= cap)
            {
                for (int i = 0; i < count; i++) yield return i;
                yield break;
            }
            var seen = new HashSet<int>();
            for (int k = 0; k < cap; k++)
            {
                int i = (int)((long)k * (count - 1) / (cap - 1));
                if (seen.Add(i)) yield return i;
            }
        }
    }
}
