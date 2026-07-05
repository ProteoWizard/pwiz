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
            public byte Charge;
            public bool HasPair;
        }

        /// <summary>
        /// Build the full model-diagnostics data model from first-pass results.
        /// </summary>
        /// <param name="perFileEntries">Per-file first-pass FdrEntry lists, scored and q-valued, pre-compaction.</param>
        /// <param name="contributions">The trained model's feature contributions (may be null for non-Percolator FDR).</param>
        /// <param name="classByBaseId">
        /// library base-id (<c>EntryId &amp; 0x7FFFFFFF</c>) -> target-side
        /// entrapment class (Target / PTarget), resolved from the library
        /// sequence exactly as FDRBench's input writer does. This is the
        /// authoritative FDRBench classification key: a precursor whose base-id
        /// is absent is "invalid" (its library sequence is not in the pairing
        /// manifest) and is excluded from the entrapment FDP, reproducing
        /// FDRBench's <c>remove_invalid_peptides</c> step. Null/empty degrades to
        /// the is_decoy-only Target/Decoy split.
        /// </param>
        /// <param name="pairByBaseId">
        /// library base-id -> peptide_pair_index from the manifest (a target and
        /// its entrapment share a value), for the paired-FDP estimator. May be null.
        /// </param>
        /// <param name="entrapmentRatio">
        /// Entrapment-to-target database ratio r (p_target count / target count
        /// from the manifest). 1.0 for a balanced library or when no manifest is
        /// present -- FDRBench's 1-fold estimators use r = 1.
        /// </param>
        /// <param name="runFdr">The configured run-level FDR (for the summary counts).</param>
        /// <param name="fdrLevel">Display name of the FDR level used.</param>
        public static ModelDiagnosticsData Build(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, uint> pairByBaseId,
            double entrapmentRatio,
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

            bool haveManifest = classByBaseId != null && classByBaseId.Count > 0;

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

                    uint baseId = e.EntryId & BASE_ID_MASK;
                    EntrapmentClass cls = Classify(e, baseId, classByBaseId, haveManifest,
                        ref nWithClass, ref nWithoutClass);
                    uint pairIdx = 0;
                    bool hasPair = pairByBaseId != null &&
                        pairByBaseId.TryGetValue(baseId, out pairIdx);
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
                            Charge = e.Charge,
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
            data.WinFraction = BuildWinFraction(perFileEntries, classByBaseId, haveManifest);

            // ---- entrapment FDP calibration (all q-scope views) ----
            if (data.HasEntrapment)
            {
                double r = entrapmentRatio > 0 ? entrapmentRatio : 1.0;
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

        // Mask clearing the decoy high bit from an EntryId to get the shared
        // target/decoy library base-id (same convention as FdrBenchInputWriter).
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        private static EntrapmentClass Classify(FdrEntry e, uint baseId,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId, bool haveManifest,
            ref int nWithClass, ref int nWithoutClass)
        {
            // FDRBench classifies each row by the library sequence its base-id
            // resolves to (a decoy shares its target's base-id), then looks that
            // sequence up in the pairing manifest. classByBaseId holds
            // Target / PTarget for every base-id whose library sequence the
            // manifest classifies as target / p_target.
            if (haveManifest && classByBaseId.TryGetValue(baseId, out var cls))
            {
                nWithClass++;
                if (!e.IsDecoy)
                    return cls;                    // Target or PTarget
                // Decoy side inherits its target's partition.
                return cls == EntrapmentClass.PTarget
                    ? EntrapmentClass.PDecoy : EntrapmentClass.Decoy;
            }
            if (!haveManifest)
                return e.IsDecoy ? EntrapmentClass.Decoy : EntrapmentClass.Target;
            // Base-id not in the classification map. A decoy is still definitely a
            // decoy (e.g. an unpaired decoy-side entry whose target-side base-id
            // isn't present) -- classify and don't count it as unclassified.
            if (e.IsDecoy)
                return EntrapmentClass.Decoy;
            // A non-decoy we genuinely cannot class as target vs entrapment: exclude
            // it from the entrapment FDP (Unknown) and count it, so a real coverage
            // gap surfaces. With library-derived classification this should be 0.
            nWithoutClass++;
            return EntrapmentClass.Unknown;
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
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId, bool haveManifest)
        {
            // base_id -> best target-side score, target-side class, best decoy-side score
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
                            tClass[bid] = haveManifest &&
                                          classByBaseId.TryGetValue(bid, out var c)
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
            // not.)
            //
            // Paired estimator (FDRBench FDPCalcKFold, k = 1):
            //   paired = (n_p + vt) / (n_t + n_p),  vt = n_p_s_t + 2 * n_p_t_s
            //   n_p_s_t = entrapment accepted, its paired target NOT accepted (or absent)
            //   n_p_t_s = entrapment accepted, paired target accepted, entrapment
            //             ranked at or above that target
            // where "ranked at or above" means the entrapment sorts first under the
            // (q asc, then score desc) order FDRBench ranks on:
            //   rankAbove = q_e < q_t  ||  (q_e == q_t && score_e >= score_t)
            // The target of a pair is matched by (pair_index, charge) -- the same
            // (paired-target-sequence, charge) key FDRBench pairs on. Each paired
            // entrapment that ranks above its target contributes +1 to vt once
            // t >= q_e (target not yet accepted -> n_p_s_t) and a further +1 once
            // t >= q_t (target accepted -> n_p_t_s, total 2); an entrapment ranked
            // below its accepted target contributes 0. This lets a single ascending
            // sweep accumulate vt from two event streams (eventsA at q_e, eventsB
            // at q_t).
            var pairTarget = new Dictionary<long, double[]>();   // (pairIndex,charge) -> [qTgt, scoreTgt]
            foreach (var p in precs)
            {
                if (p.IsDecoy || p.Class != EntrapmentClass.Target || !p.HasPair)
                    continue;
                long key = PairKey(p.PairIndex, p.Charge);
                double q = qSel(p);
                if (!pairTarget.TryGetValue(key, out var slot)
                    || q < slot[0] || (q == slot[0] && p.Score > slot[1]))
                {
                    pairTarget[key] = new[] { q, p.Score };
                }
            }

            var targetQ = new List<double>();
            var entrapQ = new List<double>();     // all entrapment q (for n_p)
            var eventsA = new List<double>();     // +1 at q_e  (entrapment accepted, ranks above target)
            var eventsB = new List<double>();     // +1 at q_t  (paired target then also accepted)
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
                    double qe = qSel(p);
                    entrapQ.Add(qe);
                    if (!p.HasPair)
                        continue;
                    double[] tgt;
                    bool haveTgt = pairTarget.TryGetValue(PairKey(p.PairIndex, p.Charge), out tgt);
                    // rankAbove: entrapment sorts at or before its target. An absent
                    // target (never observed) is always "below" -> rankAbove holds.
                    bool rankAbove = !haveTgt || qe < tgt[0] || (qe == tgt[0] && p.Score >= tgt[1]);
                    if (rankAbove)
                    {
                        eventsA.Add(qe);
                        if (haveTgt)
                            eventsB.Add(tgt[0]);
                    }
                }
            }
            if (targetQ.Count == 0)
                return null;
            bool anyPair = eventsA.Count > 0;
            targetQ.Sort();
            entrapQ.Sort();
            eventsA.Sort();
            eventsB.Sort();

            var qs = new List<double>();
            var lb = new List<double>();
            var comb = new List<double>();
            var paired = new List<double>();
            var ntAcc = new List<int>();

            int ei = 0, ai = 0, bi = 0, nE = 0;
            for (int i = 0; i < targetQ.Count; i++)
            {
                double t = targetQ[i];
                while (ei < entrapQ.Count && entrapQ[ei] <= t) { nE++; ei++; }
                while (ai < eventsA.Count && eventsA[ai] <= t) ai++;
                while (bi < eventsB.Count && eventsB[bi] <= t) bi++;
                int nT = i + 1;
                int vt = ai + bi;   // n_p_s_t + 2 * n_p_t_s
                // FDRBench 1-fold estimators, verified against fdrbench fdp.csv:
                //   combined    = (1 + 1/r) * n_p / (n_t + n_p)
                //   lower_bound = n_p / (r * (n_t + n_p))
                //   paired      = (n_p + vt) / (n_t + n_p)
                double denom = nT + nE;
                qs.Add(t);
                lb.Add(nE / (r * denom));
                comb.Add((1.0 + 1.0 / r) * nE / denom);
                paired.Add((nE + vt) / denom);
                ntAcc.Add(nT);
            }

            // q-aware thinning: keep the [0, 2%] calibration zoom window dense
            // (so the plotted curve and the 1%-q metrics stay faithful to the
            // per-precursor FDRBench values) and subsample only the long tail.
            var idx = ThinFdpIndices(qs);
            return new FdpView
            {
                Label = label,
                Pass = pass,
                Scope = scope,
                MatchesFdrBench = matchesFdrBench,
                EntrapmentRatio = r,
                Q = Pick(qs, idx),
                LowerBound = Pick(lb, idx),
                Combined = Pick(comb, idx),
                Paired = anyPair ? Pick(paired, idx) : null,
                NTargetAccepted = Pick(ntAcc, idx),
            };
        }

        // Pack an entrapment pair index (target and its p_target share it) with a
        // charge into a single key, so an entrapment precursor pairs only with a
        // target of the SAME charge -- FDRBench's (paired-target-sequence, charge)
        // pairing key. Charge fits in a byte, so shift the pair index up 8 bits.
        private static long PairKey(uint pairIndex, byte charge)
        {
            return ((long)pairIndex << 8) | charge;
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

        // Select a subset of a series by explicit index list (companion to
        // ThinFdpIndices, which chooses the indices once so every FDP series is
        // thinned to the same points).
        private static double[] Pick(List<double> xs, int[] idx)
        {
            var r = new double[idx.Length];
            for (int i = 0; i < idx.Length; i++) r[i] = xs[idx[i]];
            return r;
        }

        private static int[] Pick(List<int> xs, int[] idx)
        {
            var r = new int[idx.Length];
            for (int i = 0; i < idx.Length; i++) r[i] = xs[idx[i]];
            return r;
        }

        // Choose which points of an ascending-q FDP curve to keep for a compact,
        // faithful JSON. The [0, 2%] calibration zoom window is kept dense (up to
        // ~350 points) so the plotted curve and the 1%-q metrics match the
        // per-precursor FDRBench values; the long tail out to the max q is
        // subsampled (~120 points) for the full-extent panel. Indices are chosen
        // from the q values so every series (q / lower / combined / paired / n_t)
        // is thinned identically.
        private static int[] ThinFdpIndices(List<double> qs)
        {
            int n = qs.Count;
            if (n == 0) return new int[0];
            const double zoomMax = 0.02;
            const int lowCap = 350, highCap = 120;
            int split = 0;
            while (split < n && qs[split] <= zoomMax) split++;
            var keep = new SortedSet<int>();
            AddUniform(keep, 0, split, lowCap);   // dense across the zoom window
            AddUniform(keep, split, n, highCap);  // sparse across the tail
            keep.Add(n - 1);                       // always the final point
            var arr = new int[keep.Count];
            keep.CopyTo(arr);
            return arr;
        }

        // Add up to cap indices spread uniformly across [lo, hi).
        private static void AddUniform(SortedSet<int> keep, int lo, int hi, int cap)
        {
            int count = hi - lo;
            if (count <= 0) return;
            if (count <= cap)
            {
                for (int i = lo; i < hi; i++) keep.Add(i);
                return;
            }
            for (int k = 0; k < cap; k++)
                keep.Add(lo + (int)((long)k * (count - 1) / (cap - 1)));
        }
    }
}
