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
        public FdpCurve Fdp { get; set; }
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
        /// Entrapment FDP vs q-value calibration, best-per-precursor, sorted by
        /// ascending threshold. Two q axes: <see cref="QReported"/> is Osprey's
        /// own Percolator peptide q (the "should I trust Osprey's q" view) and
        /// <see cref="QCompetition"/> is a target-decoy competition q recomputed
        /// from the score the FDRBench way (the axis that reproduces FDRBench's
        /// fdp.csv). The three FDP estimators mirror FDRBench.
        /// </summary>
        public sealed class FdpCurve
        {
            public double EntrapmentRatio { get; set; }
            public double[] QReported { get; set; }
            public double[] QCompetition { get; set; }
            public double[] LowerBound { get; set; }
            public double[] Paired { get; set; }
            public double[] Combined { get; set; }
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
            public double QReported;
            public bool IsDecoy;
            public EntrapmentClass Class;
        }

        /// <summary>
        /// Build the full model-diagnostics data model from first-pass results.
        /// </summary>
        /// <param name="perFileEntries">Per-file first-pass FdrEntry lists, scored and q-valued, pre-compaction.</param>
        /// <param name="contributions">The trained model's feature contributions (may be null for non-Percolator FDR).</param>
        /// <param name="classBySequence">
        /// modified_sequence -> entrapment class from the pairing manifest, or
        /// null/empty to degrade to the is_decoy-only Target/Decoy split.
        /// </param>
        /// <param name="runFdr">The configured run-level FDR (for the summary counts).</param>
        /// <param name="fdrLevel">Display name of the FDR level used.</param>
        public static ModelDiagnosticsData Build(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<string, EntrapmentClass> classBySequence,
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
                    string key = e.ModifiedSequence + "|" + e.Charge;
                    if (!best.TryGetValue(key, out var prev) || e.Score > prev.Score)
                    {
                        best[key] = new Prec
                        {
                            Score = e.Score,
                            QReported = e.RunPeptideQvalue,
                            IsDecoy = e.IsDecoy,
                            Class = cls,
                        };
                    }
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

            // ---- entrapment FDP calibration ----
            if (data.HasEntrapment)
                data.Fdp = BuildFdp(precs, classBySequence);

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
                          .Select(p => p.QReported).OrderBy(q => q).ToArray();
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

        private static FdpCurve BuildFdp(List<Prec> precs,
            IReadOnlyDictionary<string, EntrapmentClass> classBySequence)
        {
            // Accepted real targets + entrapment targets, ranked by descending
            // score (the FDRBench ranking). At each rank we know the running
            // target/entrapment counts and can emit the FDP estimators plus both
            // q axes. r = entrapment DB size / target DB size, from the manifest.
            double r = EntrapmentRatio(classBySequence);
            if (r <= 0) r = 1.0;

            var ranked = precs
                .Where(p => !p.IsDecoy &&
                            (p.Class == EntrapmentClass.Target || p.Class == EntrapmentClass.PTarget))
                .OrderByDescending(p => p.Score)
                .ToList();
            if (ranked.Count == 0)
                return null;

            // Competition q (FDRBench-style) needs decoys in the ranking too, so
            // recompute it on the full target+decoy+entrapment score order.
            var compQ = CompetitionQByScore(precs);

            int nT = 0, nE = 0;
            var qRep = new List<double>();
            var qComp = new List<double>();
            var lb = new List<double>();
            var paired = new List<double>();
            var comb = new List<double>();
            var ntAcc = new List<int>();

            // Running reported-q is the min over accepted (monotone); we emit one
            // point whenever the target count advances, thinning long tails.
            double runRepQ = 0;
            foreach (var p in ranked)
            {
                if (p.Class == EntrapmentClass.PTarget)
                {
                    nE++;
                    continue; // entrapment hits move the FDP, not the yield point
                }
                nT++;
                runRepQ = Math.Max(runRepQ, p.QReported);
                double denom = nT + nE;
                double lower = nE / (r * denom);
                double combined = (1.0 + 1.0 / r) * nE / denom;
                // Paired estimator placeholder == combined until calibrated
                // against FDRBench fdp.csv (tracked in the TODO). Kept as its own
                // series so the calibration only touches this line.
                double pairedFdp = combined;
                qRep.Add(runRepQ);
                qComp.Add(compQ.TryGetValue(p.Score, out var cq) ? cq : double.NaN);
                lb.Add(lower);
                paired.Add(pairedFdp);
                comb.Add(combined);
                ntAcc.Add(nT);
            }

            return new FdpCurve
            {
                EntrapmentRatio = r,
                QReported = Thin(qRep),
                QCompetition = Thin(qComp),
                LowerBound = Thin(lb),
                Paired = Thin(paired),
                Combined = Thin(comb),
                NTargetAccepted = Thin(ntAcc),
            };
        }

        private static double EntrapmentRatio(IReadOnlyDictionary<string, EntrapmentClass> classBySequence)
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

        // Standard target-decoy competition q from the score, monotonized from
        // the bottom (mirrors the FDRBench score:1 q-value). Returns score->q.
        private static Dictionary<double, double> CompetitionQByScore(List<Prec> precs)
        {
            var ranked = precs
                .Where(p => p.Class != EntrapmentClass.PDecoy && p.Class != EntrapmentClass.PTarget)
                .OrderByDescending(p => p.Score)
                .ToList();
            var fdr = new double[ranked.Count];
            int nt = 0, nd = 0;
            for (int i = 0; i < ranked.Count; i++)
            {
                if (ranked[i].IsDecoy) nd++; else nt++;
                fdr[i] = nt > 0 ? (double)nd / nt : 1.0;
            }
            // Monotone q: running min from the tail up.
            var q = new Dictionary<double, double>();
            double running = 1.0;
            for (int i = ranked.Count - 1; i >= 0; i--)
            {
                running = Math.Min(running, fdr[i]);
                if (!ranked[i].IsDecoy)
                    q[ranked[i].Score] = running;
            }
            return q;
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
