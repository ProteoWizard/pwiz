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
        /// <summary>
        /// The second-pass model (feature table + composite score histogram),
        /// present only when a run with <c>--protein-fdr</c> retrained Percolator on
        /// the post-reconciliation pool. Null on a single-pass run. The Model tab
        /// offers a Pass 1 / Pass 2 selector when this is present. Shares
        /// <see cref="FeatureHistEdges"/> with pass 1 (same standardized bins).
        /// </summary>
        public ModelPass ModelPass2 { get; set; }
        /// <summary>
        /// Shared bin edges for the per-feature standardized-value histograms
        /// (<see cref="FeatureRow.TargetHist"/> / <see cref="FeatureRow.DecoyHist"/>),
        /// or null when they were not collected. Drives the Skyline mProphet
        /// "Feature Scores" per-feature distribution view.
        /// </summary>
        public double[] FeatureHistEdges { get; set; }
        public ScoreHistogram Scores { get; set; }
        /// <summary>
        /// Non-parametric null-alignment ratios (Storey-style) derived from
        /// <see cref="Scores"/>: the per-class score-density ratios target:decoy
        /// and (when entrapment is present) p_target:p_decoy, plus a left-side
        /// flatness KPI. Null when there is no score histogram to divide.
        /// </summary>
        public DensityRatioData DensityRatio { get; set; }
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
        // ReSharper disable once CollectionNeverQueried.Global
        public List<FileSummaryRow> PerFile { get; set; }
        public IdYieldData IdYield { get; set; }
        /// <summary>
        /// Cross-run detection reproducibility (entrapment-free FDR QC): per-run
        /// detected-precursor counts and their cumulative union / intersection over
        /// runs, plus the histogram of precursors by the number of runs they are
        /// detected in. Real precursors reproduce across replicate runs while
        /// FDR-escaping false precursors do not, so a runaway union / collapsing
        /// intersection and a growing "detected in only one run" bump read FDR trouble
        /// with no decoy or entrapment model -- the picture that works on the ordinary
        /// target+decoy libraries most users run. Built unconditionally from the
        /// reported per-file passing REAL-target precursors (unique per modified
        /// sequence + charge; decoys and entrapment excluded as in <see cref="IdYield"/>).
        /// </summary>
        public CrossRunDetection CrossRun { get; set; }

        /// <summary>
        /// A trained model for one FDR pass: its feature-contribution table, the
        /// composite target/decoy separation, and the best-per-precursor composite
        /// score histogram over the pool it was trained on. Pass 1 lives in the
        /// top-level fields; pass 2 (the <c>--protein-fdr</c> retrain) is this type.
        /// </summary>
        public sealed class ModelPass
        {
            public double Composite { get; set; }
            public bool Degenerate { get; set; }
            public List<FeatureRow> Features { get; set; }
            public ScoreHistogram Scores { get; set; }
        }

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
            /// <summary>
            /// This feature's standardized-value histogram for target / decoy
            /// precursors over <see cref="FeatureHistEdges"/> (null when not
            /// collected). The per-feature analog of the composite-score view,
            /// shown on demand when the row is selected.
            /// </summary>
            public int[] TargetHist { get; set; }
            public int[] DecoyHist { get; set; }
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
        /// Non-parametric null-alignment ratios and the left-side flatness KPI,
        /// derived from a <see cref="ScoreHistogram"/>'s per-class count arrays
        /// (the Storey-style check). Each class density integrates to 1, so the
        /// ratio lines are pure shape comparisons on a unit scale. Companion to
        /// the decoy-normal overlay, but with no parametric fit: a decoy that
        /// tracks the false-target null rides a flat plateau on the null-dominated
        /// left; one that mistracks makes the target:decoy left side slope.
        /// </summary>
        public sealed class DensityRatioData
        {
            /// <summary>Score bin centers (the shared x for both ratio lines).</summary>
            public double[] BinCenters { get; set; }
            /// <summary>
            /// target:decoy density ratio per bin -- the diagnostic line. NaN in
            /// bins empty on either side. Flat on the null-dominated left at the
            /// null fraction pi0 (&lt; 1; the target carries the true-hit mass),
            /// then rises where real hits begin.
            /// </summary>
            public double[] TargetDecoy { get; set; }
            /// <summary>
            /// p_target:p_decoy density ratio per bin -- the matched-null reference
            /// line, or null when there is no entrapment. Both classes are pure
            /// null, so it rides ~1 flat throughout: "what a matched null pair
            /// looks like."
            /// </summary>
            public double[] PTargetPDecoy { get; set; }
            /// <summary>Lower score bound of the null region the flatness KPI was fit over (NaN if unmeasured).</summary>
            public double NullRegionLo { get; set; }
            /// <summary>Upper score bound of the null region the flatness KPI was fit over (NaN if unmeasured).</summary>
            public double NullRegionHi { get; set; }
            /// <summary>Number of bins that fed the target:decoy flatness fit.</summary>
            public int NullRegionBins { get; set; }
            /// <summary>
            /// Headline KPI: the tilt of the target:decoy plateau across the null
            /// region -- the weighted least-squares slope of ln(target:decoy)
            /// against a null-region-normalized score in [0, 1]. ~0 = flat (decoys
            /// track the false-target null); a large magnitude means the left side
            /// slopes (a decoy-quality / equal-chance violation), caught with no
            /// parametric fit. NaN when the null region has too little support to
            /// assess.
            /// </summary>
            public double FlatnessSlope { get; set; }
            /// <summary>
            /// Bonus Storey read: the left-plateau height of target:decoy (the
            /// weighted geometric-mean ratio over the null region) ~ pi0 = 1 -
            /// true-hit fraction. NaN when unmeasured.
            /// </summary>
            public double PlateauRatio { get; set; }
            /// <summary>
            /// The reference line's flatness (slope of ln(p_target:p_decoy) over
            /// the same null window) -- expected ~0 since both are pure null. NaN
            /// with no entrapment or insufficient support. A same-run "what flat
            /// looks like" baseline for <see cref="FlatnessSlope"/>.
            /// </summary>
            public double RefFlatnessSlope { get; set; }
            /// <summary>True when the p_target:p_decoy reference line is present (an entrapment run).</summary>
            public bool HasEntrapment { get; set; }
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
            /// <summary>FDRBench paired estimator, or null when no pair_index was supplied
            /// OR when the entrapment library is not ~1:1 (see <see cref="PairedSuppressedPartial"/>).</summary>
            public double[] Paired { get; set; }
            /// <summary>True when the paired estimator was suppressed because the entrapment
            /// library is not ~1:1 (|r - 1| exceeds <see cref="PairedRatioTolerance"/>). The
            /// paired estimator is a 1-fold method (FDRBench / Wen et al. 2025): it is only
            /// valid when every target has exactly one entrapment twin. Combined and
            /// lower-bound remain valid at any ratio.</summary>
            public bool PairedSuppressedPartial { get; set; }
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
            /// <summary>Entrapment (p_target) precursors passing the run FDR in this file, when a manifest is present.</summary>
            public int Entrapment { get; set; }
        }

        /// <summary>
        /// Cumulative accepted real-target precursor count vs the reported q-value
        /// threshold (the yield curve), at each precursor-q scope over one shared
        /// <see cref="Q"/> grid. The report's scope selector switches between the two
        /// curves: the experiment-wide q pooled across runs (what <c>--fdrbench</c>
        /// emits) and the per-run q. Both are entrapment-independent.
        /// </summary>
        public sealed class IdYieldData
        {
            public double[] Q { get; set; }
            /// <summary>Accepted targets vs the experiment-wide precursor q (pooled across runs).</summary>
            public int[] TargetsExperiment { get; set; }
            /// <summary>Accepted targets vs the per-run precursor q.</summary>
            public int[] TargetsRun { get; set; }
        }

        /// <summary>
        /// Cross-run detection reproducibility, read off run-to-run membership with no
        /// decoy or entrapment model. Built from the reported per-file passing
        /// precursors (identity = modified sequence + charge, in input-file order):
        /// real precursors reproduce across replicate runs, FDR-escaping false ones do
        /// not, so a runaway <see cref="CumUnion"/> / collapsing <see cref="CumIntersection"/>
        /// and a growing "detected in only one run" bump in <see cref="RunCountHistogram"/>
        /// surface FDR trouble that the entrapment FDP needs a special library to see.
        /// </summary>
        public sealed class CrossRunDetection
        {
            /// <summary>Input-file names, in input order (the x for the per-run curves).</summary>
            public string[] RunNames { get; set; }
            /// <summary>Detected precursors passing the run FDR in each run (the bars).</summary>
            public int[] PerRunCount { get; set; }
            /// <summary>|union of detected precursors over runs 1..i| -- total unique seen so far (monotone up).</summary>
            public int[] CumUnion { get; set; }
            /// <summary>|intersection over runs 1..i| -- detected in every run so far (monotone down).</summary>
            public int[] CumIntersection { get; set; }
            /// <summary>Precursors detected in at least ceil(N/2) of the N runs (the reproducibility floor line).</summary>
            public int AtLeastHalf { get; set; }
            /// <summary>Index k-1 =&gt; number of precursors detected in exactly k runs (k = 1..N).</summary>
            public int[] RunCountHistogram { get; set; }
            /// <summary>Mean of <see cref="PerRunCount"/> (annotation).</summary>
            public double MeanPerRun { get; set; }
            /// <summary>Population standard deviation of <see cref="PerRunCount"/> (annotation).</summary>
            public double StdPerRun { get; set; }
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
        /// <param name="fdrLevel">
        /// The FDR control level the run was reported at (Precursor / Peptide / Both).
        /// Decides which run-level q gates the "passing" set (via
        /// <see cref="FdrEntry.EffectiveRunQvalue"/>), so the per-file and cross-run
        /// counts match what the pipeline actually reported -- not a hardcoded scope.
        /// </param>
        public static ModelDiagnosticsData Build(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, uint> pairByBaseId,
            double entrapmentRatio,
            double runFdr,
            FdrLevel fdrLevel)
        {
            var data = new ModelDiagnosticsData
            {
                RunFdr = runFdr,
                FdrLevel = fdrLevel.ToString(),
                FileCount = perFileEntries.Count,
                Model = new List<FeatureRow>(),
                PerFile = new List<FileSummaryRow>(),
            };

            bool haveManifest = classByBaseId != null && classByBaseId.Count > 0;

            // ---- best-per-precursor reduction (max score per modseq+charge) ----
            var precs = ReduceToPrecs(perFileEntries, classByBaseId, pairByBaseId, haveManifest,
                out int nWithClass, out int nWithoutClass);

            // Per-file passing summary at the run-level peptide FDR (kept separate
            // from the reduction so the same reduction serves pass 1 and pass 2).
            // Non-decoy passing entries split into real targets vs entrapment
            // (p_target) by the library base-id class, so the table mirrors the
            // top-line target / decoy / entrapment breakdown.
            foreach (var kvp in perFileEntries)
            {
                int fileTargets = 0, fileDecoys = 0, fileEntrap = 0;
                foreach (var e in kvp.Value)
                {
                    // Gate on the run-level q for the CONFIGURED FDR level (precursor /
                    // peptide / both), the same value the pipeline reports on -- not a
                    // hardcoded peptide q, which would miscount a precursor-controlled run.
                    if (e.EffectiveRunQvalue(fdrLevel) > runFdr)
                        continue;
                    if (e.IsDecoy)
                    {
                        fileDecoys++;
                        continue;
                    }
                    uint baseId = e.EntryId & BASE_ID_MASK;
                    if (haveManifest && classByBaseId.TryGetValue(baseId, out var cls)
                        && cls == EntrapmentClass.PTarget)
                        fileEntrap++;
                    else
                        fileTargets++;
                }
                data.PerFile.Add(new FileSummaryRow
                {
                    File = kvp.Key, Targets = fileTargets, Decoys = fileDecoys, Entrapment = fileEntrap,
                });
            }
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

            // ---- model table (pass 1) ----
            if (contributions != null)
            {
                data.ModelComposite = contributions.Composite;
                data.ModelDegenerate = contributions.IsDegenerate;
                data.FeatureHistEdges = contributions.HistogramEdges;
                data.Model = BuildFeatureRows(contributions);
            }

            // ---- score histogram + decoy normal fit ----
            data.Scores = BuildScoreHistogram(precs);

            // ---- non-parametric null-alignment density ratios + flatness KPI ----
            // Divides the per-class density arrays already reduced above; no new
            // binning pass. Pass 1 only (the Density tab shows the pass-1 densities).
            data.DensityRatio = BuildDensityRatio(data.Scores, data.HasEntrapment);

            // ---- id-yield curve (accepted targets vs q, both precursor-q scopes) ----
            data.IdYield = BuildIdYield(precs);

            // ---- cross-run detection reproducibility (entrapment-free FDR QC) ----
            // From the reported per-file passing REAL-target precursors (entrapment
            // excluded, like the id-yield curve, so a run's reproducibility picture is
            // not polluted by the deliberately-non-reproducing entrapment padding).
            // Built unconditionally (no manifest needed for a plain target+decoy run).
            data.CrossRun = BuildCrossRunDetection(perFileEntries, classByBaseId, haveManifest, runFdr, fdrLevel);

            // ---- paired decoy-win fraction ----
            data.WinFraction = BuildWinFraction(perFileEntries, classByBaseId, haveManifest);

            // ---- entrapment FDP calibration (all q-scope views) ----
            // Pass 1 (pre-compaction, this Stage-5 pool). Pass 2 (the final
            // reported pool) is appended later by the end-of-run writer
            // (MergeNodeTask) via BuildPass2FdpViews over the same shared code.
            if (data.HasEntrapment)
            {
                double r = entrapmentRatio > 0 ? entrapmentRatio : 1.0;
                data.FdpViews = BuildFdpViewsFromPrecs(precs, r, 1);
            }

            return data;
        }

        // Mask clearing the decoy high bit from an EntryId to get the shared
        // target/decoy library base-id (same convention as FdrBenchInputWriter).
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        // Build the feature-contribution table rows (most-influential first) from a
        // trained model, carrying each feature's per-class histogram when collected.
        // Shared by pass 1 (Build) and pass 2 (BuildModelPass2).
        private static List<FeatureRow> BuildFeatureRows(FeatureContributions contributions)
        {
            var rows = new List<FeatureRow>();
            var targetHist = contributions.TargetHistograms;
            var decoyHist = contributions.DecoyHistograms;
            foreach (var f in contributions.Features
                .OrderByDescending(f => contributions.IsDegenerate ? 0.0 : Math.Abs(f.Percent))
                .ThenBy(f => f.Index))
            {
                rows.Add(new FeatureRow
                {
                    Index = f.Index,
                    Label = f.Label,
                    Coefficient = f.Coefficient,
                    Percent = f.Percent,
                    DeltaMu = f.TargetDecoyMeanGap,
                    Weighted = f.Weighted,
                    Reversed = f.IsReversedScore,
                    // Red row = the trained coefficient's sign is opposite the
                    // feature's expected direction (IsReversedScore XOR coefficient < 0):
                    // a feature behaving against expectation.
                    Unexpected = !contributions.IsDegenerate && f.IsUnexpectedDirection,
                    // Per-feature target/decoy histograms (index-keyed to the trained
                    // model; null when not collected).
                    TargetHist = targetHist != null && f.Index < targetHist.Count ? targetHist[f.Index] : null,
                    DecoyHist = decoyHist != null && f.Index < decoyHist.Count ? decoyHist[f.Index] : null,
                });
            }
            return rows;
        }

        /// <summary>
        /// Build the pass-2 model view (feature table + composite score histogram)
        /// from the second-pass Percolator model and the post-reconciliation pool.
        /// Called by the end-of-run writer (MergeNodeTask) only when a
        /// <c>--protein-fdr</c> run retrained Percolator on the reported pool, so both
        /// models can be shown side by side. Returns null when no second-pass
        /// contributions are available (single-pass run or a rehydrated resume).
        /// </summary>
        public static ModelPass BuildModelPass2(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            FeatureContributions contributions,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, uint> pairByBaseId)
        {
            if (contributions == null)
                return null;
            bool haveManifest = classByBaseId != null && classByBaseId.Count > 0;
            var precs = ReduceToPrecs(perFileEntries, classByBaseId, pairByBaseId, haveManifest,
                out _, out _);
            return new ModelPass
            {
                Composite = contributions.Composite,
                Degenerate = contributions.IsDegenerate,
                Features = BuildFeatureRows(contributions),
                Scores = BuildScoreHistogram(precs),
            };
        }

        /// <summary>
        /// Build the pass-2 FDP calibration views from the final reported
        /// (post-compaction, second-pass q-valued) pool. Called by the end-of-run
        /// writer (MergeNodeTask) with <c>RescoredEntries</c> -- the same pool the
        /// pass-2 FDRBench TSV is written from -- so the HTML pass-2 curve and
        /// stock FDRBench see the identical peptides and q-values. Returns an empty
        /// list when the pool carries no entrapment (nothing to calibrate against).
        /// The classification / pairing / ratio inputs come from the searched
        /// library exactly as pass 1 does (see ModelDiagnosticsReport).
        /// </summary>
        public static List<FdpView> BuildPass2FdpViews(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, uint> pairByBaseId,
            double entrapmentRatio)
        {
            bool haveManifest = classByBaseId != null && classByBaseId.Count > 0;
            var precs = ReduceToPrecs(perFileEntries, classByBaseId, pairByBaseId, haveManifest,
                out _, out _);
            bool hasEntrapment = precs.Any(p => p.Class == EntrapmentClass.PTarget);
            if (!hasEntrapment)
                return new List<FdpView>();
            double r = entrapmentRatio > 0 ? entrapmentRatio : 1.0;
            return BuildFdpViewsFromPrecs(precs, r, 2);
        }

        // Build the experiment- and run-scope FDP calibration views for one pass
        // from an already-reduced best-per-precursor list. The experiment-scope
        // view reproduces FDRBench (--fdrbench emits that q); the run-scope view is
        // the per-run picture FDRBench cannot show. pass = 1 (pre-compaction) or
        // 2 (final reported pool). Shared by pass 1 (Build) and pass 2
        // (BuildPass2FdpViews) so the two passes are computed identically.
        private static List<FdpView> BuildFdpViewsFromPrecs(List<Prec> precs, double r, int pass)
        {
            var views = new List<FdpView>();
            var expView = BuildFdpView(precs, r, p => p.QExpPrecursor,
                string.Format(@"Pass {0} - experiment-wide", pass), pass, @"experiment", true);
            if (expView != null) views.Add(expView);
            var runView = BuildFdpView(precs, r, p => p.QRunPrecursor,
                string.Format(@"Pass {0} - per-run", pass), pass, @"run", false);
            if (runView != null) views.Add(runView);
            return views;
        }

        // Reduce per-file FDR entries to one best-per-precursor record each,
        // keyed by (modified sequence, charge): a precursor seen in several files
        // keeps its single best (max) score and its best (min) q at each scope,
        // matching FdrBenchInputWriter's cross-file dedup. Shared by pass 1 (Build)
        // and pass 2 (BuildPass2FdpViews). nWithClass / nWithoutClass report the
        // manifest-classification health (a large nWithoutClass on an entrapment
        // run signals a base-id key mismatch).
        private static List<Prec> ReduceToPrecs(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId,
            IReadOnlyDictionary<uint, uint> pairByBaseId,
            bool haveManifest,
            out int nWithClass, out int nWithoutClass)
        {
            var best = new Dictionary<string, Prec>(StringComparer.Ordinal);
            int wc = 0, woc = 0;
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    uint baseId = e.EntryId & BASE_ID_MASK;
                    EntrapmentClass cls = Classify(e, baseId, classByBaseId, haveManifest,
                        ref wc, ref woc);
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
                            // FDR-calibration views plot. Experiment scope is what
                            // --fdrbench emits (and thus reproduces its plots); run
                            // scope is the per-run picture. Each precursor takes its
                            // BEST (min) q across observations, matching
                            // FdrBenchInputWriter's dedup, while Score stays the max
                            // (for density / the score histogram).
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
            }
            nWithClass = wc; nWithoutClass = woc;
            return best.Values.ToList();
        }

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

        // Minimum usable bins the flatness fit needs before it will report a KPI
        // (below this the null region is too sparse to assess -> NaN "cannot assess").
        private const int MinNullRegionBins = 4;

        // The null-region fit is bounded at this quantile of the decoy (null) score
        // distribution -- its null-dominated lower portion, below the true-hit
        // "shoulder" where the target's real hits start lifting the target:decoy
        // ratio off its plateau. Calibrated on the Stellar libdecoy oracle, where
        // that plateau is flat below ~the decoy 40th percentile and rises above it;
        // fitting only the plateau is what makes a calibrated pairing read as flat.
        private const double NullRegionDecoyQuantile = 0.40;

        /// <summary>
        /// Build the non-parametric null-alignment density ratios and the
        /// left-side flatness KPI from a best-per-precursor
        /// <see cref="ScoreHistogram"/>. Each per-class count array is
        /// area-normalized to a unit-mass density, then two ratio lines are
        /// formed -- target:decoy (the diagnostic, Mike's Storey check) and, when
        /// entrapment is present, p_target:p_decoy (the matched-null reference).
        /// The flatness KPI is the weighted slope of ln(target:decoy) across the
        /// null-dominated left region: ~0 when the decoys track the false-target
        /// null, large when they mistrack -- with no parametric fit. Pure function
        /// over the histogram (shared by <see cref="Build"/> and unit-tested
        /// directly); returns null when there is no histogram to divide.
        /// </summary>
        public static DensityRatioData BuildDensityRatio(ScoreHistogram h, bool hasEntrapment)
        {
            if (h?.BinEdges == null || h.BinEdges.Length < 3)
                return null;   // need at least two bins to form a ratio line
            int nb = h.BinEdges.Length - 1;
            var centers = new double[nb];
            for (int i = 0; i < nb; i++)
                centers[i] = 0.5 * (h.BinEdges[i] + h.BinEdges[i + 1]);

            double[] td = DensityRatioSeries(h.Target, h.Decoy, nb);
            double[] pp = hasEntrapment ? DensityRatioSeries(h.PTarget, h.PDecoy, nb) : null;

            var data = new DensityRatioData
            {
                BinCenters = centers,
                TargetDecoy = td,
                PTargetPDecoy = pp,
                HasEntrapment = pp != null,
                NullRegionLo = double.NaN,
                NullRegionHi = double.NaN,
                FlatnessSlope = double.NaN,
                PlateauRatio = double.NaN,
                RefFlatnessSlope = double.NaN,
            };
            if (td == null)
                return data;   // no target/decoy mass to divide: ratio lines only

            // Null region = the null-dominated lower portion of the decoy (null)
            // distribution -- bins up to and including the NullRegionDecoyQuantile
            // bin, where true target hits are negligible (they sit at high scores).
            // Fitting only this range isolates the FLAT plateau Storey's check reads
            // and excludes the true-hit "shoulder" (the rise a target/decoy crossover
            // would let in: the crossover lands only after the target has accumulated
            // its true-hit mass, well past where the ratio departs its plateau). The
            // true-hit onset still caps it as a safety for a degenerate target~decoy
            // separation (onset fires at bin 0 -> empty region -> "cannot assess").
            int onset = TrueHitOnset(h.Target, h.Decoy, nb);
            // hi is the exclusive end of the [0, hi) fit loop below: the +1 includes
            // the decoy-quantile bin itself (still null-dominated), while the onset
            // bin (the first true-hit bin) stays excluded.
            int hi = Math.Min(onset, DecoyQuantileBin(h.Decoy, nb, NullRegionDecoyQuantile) + 1);

            var xs = new List<double>();
            var ys = new List<double>();
            var ws = new List<double>();
            for (int i = 0; i < hi; i++)
                AddFitBin(centers, td, h.Target, h.Decoy, i, xs, ys, ws);
            if (xs.Count < MinNullRegionBins)
                return data;   // insufficient null support to assess flatness

            double loScore = xs[0], hiScore = xs[xs.Count - 1];
            data.NullRegionLo = loScore;
            data.NullRegionHi = hiScore;
            data.NullRegionBins = xs.Count;
            WeightedLogFit(xs, ys, ws, loScore, hiScore, out double slope, out double meanLog);
            data.FlatnessSlope = slope;
            data.PlateauRatio = Math.Exp(meanLog);   // weighted geometric-mean ratio ~ pi0

            // Reference line's flatness over the SAME score window, for an
            // apples-to-apples "what flat looks like" baseline (both p_target and
            // p_decoy are pure null -> ~0).
            if (pp != null)
            {
                var rxs = new List<double>();
                var rys = new List<double>();
                var rws = new List<double>();
                for (int i = 0; i < nb; i++)
                {
                    if (centers[i] < loScore || centers[i] > hiScore)
                        continue;
                    AddFitBin(centers, pp, h.PTarget, h.PDecoy, i, rxs, rys, rws);
                }
                if (rxs.Count >= MinNullRegionBins)
                {
                    WeightedLogFit(rxs, rys, rws, loScore, hiScore, out double rslope, out _);
                    data.RefFlatnessSlope = rslope;
                }
            }
            return data;
        }

        // Form one density-ratio line num:den over nb bins:
        // (num_i / sum(num)) / (den_i / sum(den)). Each class is area-normalized to
        // unit mass first, so the ratio is a pure shape comparison. NaN where a bin
        // is empty on either side (a gap the plot skips and the fit excludes) --
        // never +/-Infinity, which the HTML's JS isNaN() guard would let through.
        // Null when either class has no mass at all.
        private static double[] DensityRatioSeries(int[] num, int[] den, int nb)
        {
            if (num == null || den == null)
                return null;
            long sumN = 0, sumD = 0;
            for (int i = 0; i < nb; i++) { sumN += num[i]; sumD += den[i]; }
            if (sumN == 0 || sumD == 0)
                return null;
            var r = new double[nb];
            for (int i = 0; i < nb; i++)
                r[i] = num[i] <= 0 || den[i] <= 0
                    ? double.NaN
                    : ((double)num[i] / sumN) / ((double)den[i] / sumD);
            return r;
        }

        // Accumulate one bin into a weighted-log-fit sample set when both classes
        // are populated (ratio finite and loggable). x = score center, y = ln(ratio),
        // weight = num*den/(num+den): the inverse of the log-ratio's variance
        // (~ 1/num + 1/den by the delta method), so sparse bins count for less.
        private static void AddFitBin(double[] centers, double[] ratio, int[] num, int[] den,
            int i, List<double> xs, List<double> ys, List<double> ws)
        {
            if (num[i] < 1 || den[i] < 1)
                return;
            xs.Add(centers[i]);
            ys.Add(Math.Log(ratio[i]));
            ws.Add((double)num[i] * den[i] / ((double)num[i] + den[i]));
        }

        // The true-hit onset: the first bin at which the target density rises to
        // meet or exceed the decoy density and STAYS there (this bin AND the next
        // two usable bins are all at/above) -- i.e. where real hits begin to
        // dominate the shared null. Requiring a sustained run keeps a 1-2 bin
        // statistical bump in the null-dominated left from tripping a false-early
        // onset (which would only shrink the fit window -- conservative -- but is
        // still worth guarding). Below the onset, target:decoy sits on its null
        // plateau (at pi0 < 1). Returns nb when no sustained crossover exists (a
        // degenerate target~decoy separation), leaving the decoy-quantile cap to
        // bound the fit. Densities are compared by cross-multiplication
        // (target_i/sumT >= decoy_i/sumD) to avoid recomputing sums.
        private static int TrueHitOnset(int[] target, int[] decoy, int nb)
        {
            long sumT = 0, sumD = 0;
            for (int i = 0; i < nb; i++) { sumT += target[i]; sumD += decoy[i]; }
            if (sumT == 0 || sumD == 0)
                return nb;
            for (int i = 0; i < nb; i++)
            {
                if (target[i] < 1 || decoy[i] < 1 || !AtOrAbove(target[i], decoy[i], sumT, sumD))
                    continue;
                if (SustainedAtOrAbove(target, decoy, nb, i, sumT, sumD, 2))
                    return i;
            }
            return nb;
        }

        // target_i/sumT >= decoy_i/sumD, cross-multiplied (all non-negative).
        private static bool AtOrAbove(int target, int decoy, long sumT, long sumD)
        {
            return (double)target * sumD >= (double)decoy * sumT;
        }

        // True when the next `lookahead` USABLE bins after `start` (both classes
        // populated) are all at/above the decoy density -- or fewer usable bins
        // remain before nb (a crossover close to the right edge is still real).
        // Lets TrueHitOnset require a sustained, not single-fluctuation, crossover.
        private static bool SustainedAtOrAbove(int[] target, int[] decoy, int nb,
            int start, long sumT, long sumD, int lookahead)
        {
            int seen = 0;
            for (int j = start + 1; j < nb && seen < lookahead; j++)
            {
                if (target[j] < 1 || decoy[j] < 1)
                    continue;
                if (!AtOrAbove(target[j], decoy[j], sumT, sumD))
                    return false;
                seen++;
            }
            return true;
        }

        // The bin index at the given quantile of the decoy score distribution (by
        // decoy count) -- the primary null-region upper bound (its null-dominated
        // lower portion); the true-hit onset only caps it in the degenerate case.
        // Returns nb-1 as a safe upper bound if decoys are empty.
        private static int DecoyQuantileBin(int[] decoy, int nb, double q)
        {
            long total = 0;
            for (int i = 0; i < nb; i++) total += decoy[i];
            if (total == 0)
                return nb - 1;
            long threshold = (long)Math.Ceiling(q * total);
            long cum = 0;
            for (int i = 0; i < nb; i++)
            {
                cum += decoy[i];
                if (cum >= threshold)
                    return i;
            }
            return nb - 1;
        }

        // Weighted least-squares slope of y = ln(ratio) against an x normalized to
        // [0, 1] across [loScore, hiScore], plus the weighted mean of y. The [0, 1]
        // normalization makes the slope a scale-free "e-folds of drift across the
        // null plateau" (0 = perfectly flat), comparable across runs regardless of
        // the composite score's units. meanLog exponentiates to the weighted
        // geometric-mean ratio (~ pi0). slope is NaN for a degenerate (single-x) fit.
        private static void WeightedLogFit(List<double> xs, List<double> ys, List<double> ws,
            double loScore, double hiScore, out double slope, out double meanLog)
        {
            double span = hiScore - loScore;
            double sw = 0, swx = 0, swy = 0, swxx = 0, swxy = 0;
            for (int i = 0; i < xs.Count; i++)
            {
                double x = span > 0 ? (xs[i] - loScore) / span : 0.0;
                double y = ys[i], w = ws[i];
                sw += w; swx += w * x; swy += w * y; swxx += w * x * x; swxy += w * x * y;
            }
            meanLog = sw > 0 ? swy / sw : double.NaN;
            double denom = sw * swxx - swx * swx;
            slope = Math.Abs(denom) > 1e-12 ? (sw * swxy - swx * swy) / denom : double.NaN;
        }

        private static IdYieldData BuildIdYield(List<Prec> precs)
        {
            // Cumulative accepted real-target precursors as the reported-q threshold
            // sweeps [0, 0.1], at BOTH precursor-q scopes so the report's scope
            // selector can switch between them (experiment-wide = what --fdrbench
            // emits; per-run = the per-file picture). Real targets only (entrapment
            // excluded), and a fixed grid so the JSON stays small.
            var reals = precs.Where(p => !p.IsDecoy && p.Class != EntrapmentClass.PTarget
                                          && p.Class != EntrapmentClass.PDecoy).ToList();
            var qExp = reals.Select(p => p.QExpPrecursor).OrderBy(q => q).ToArray();
            var qRun = reals.Select(p => p.QRunPrecursor).OrderBy(q => q).ToArray();
            const int steps = 100;
            var grid = new double[steps];
            var cExp = new int[steps];
            var cRun = new int[steps];
            for (int i = 0; i < steps; i++)
            {
                double thr = 0.10 * (i + 1) / steps;
                grid[i] = thr;
                cExp[i] = UpperBound(qExp, thr);
                cRun[i] = UpperBound(qRun, thr);
            }
            return new IdYieldData { Q = grid, TargetsExperiment = cExp, TargetsRun = cRun };
        }

        // Build the cross-run detection reproducibility model from the reported
        // per-file passing REAL-target precursors: for each run, the set of UNIQUE
        // real-target precursors (modified sequence + charge, deduped per file)
        // passing the run-level peptide FDR. Decoys are excluded by is_decoy (which
        // also drops p_decoys) and entrapment (p_target) is excluded via the manifest
        // classification, mirroring BuildIdYield -- so the reproducibility picture
        // reflects real detections, not the deliberately-non-reproducing entrapment
        // padding. This is the real-target counterpart of the Summary per-file Targets
        // column (that column counts passing rows; here each precursor is counted once).
        // Cumulative union / intersection are walked in input-file order; the run-count
        // histogram tallies how many runs each detected precursor appears in.
        // Entrapment-independent (no manifest needed for a plain target+decoy run).
        private static CrossRunDetection BuildCrossRunDetection(
            IReadOnlyList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, EntrapmentClass> classByBaseId, bool haveManifest,
            double runFdr, FdrLevel fdrLevel)
        {
            int n = perFileEntries.Count;
            var runNames = new string[n];
            var perRunSets = new List<HashSet<string>>(n);
            for (int i = 0; i < n; i++)
            {
                var kvp = perFileEntries[i];
                runNames[i] = kvp.Key;
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var e in kvp.Value)
                {
                    // Gate on the CONFIGURED FDR level's run q (the value the pipeline
                    // reports on), matching the Summary per-file loop -- not a hardcoded
                    // peptide q, which miscounts a precursor- or both-controlled run.
                    if (e.IsDecoy || e.EffectiveRunQvalue(fdrLevel) > runFdr)
                        continue;
                    // Exclude entrapment (p_target): a known false set that by design
                    // does not reproduce, so counting it would inflate the k=1 bump.
                    if (haveManifest && classByBaseId != null
                        && classByBaseId.TryGetValue(e.EntryId & BASE_ID_MASK, out var cls)
                        && cls == EntrapmentClass.PTarget)
                        continue;
                    set.Add(e.ModifiedSequence + "|" + e.Charge);   // same key as ReduceToPrecs
                }
                perRunSets.Add(set);
            }

            var perRunCount = new int[n];
            var cumUnion = new int[n];
            var cumIntersection = new int[n];
            var union = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> inter = null;               // running intersection (seeded by run 0)
            var runCount = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                var set = perRunSets[i];
                perRunCount[i] = set.Count;
                union.UnionWith(set);
                cumUnion[i] = union.Count;
                if (inter == null)
                    inter = new HashSet<string>(set, StringComparer.Ordinal);
                else
                    inter.IntersectWith(set);
                cumIntersection[i] = inter.Count;
                foreach (var k in set)
                {
                    runCount.TryGetValue(k, out int c);
                    runCount[k] = c + 1;
                }
            }

            // Histogram: index k-1 => precursors detected in exactly k runs (k = 1..n),
            // and the reproducibility floor (precursors in at least ceil(n/2) runs).
            var hist = new int[n];
            int half = (n + 1) / 2;                     // ceil(n/2)
            int atLeastHalf = 0;
            foreach (var c in runCount.Values)
            {
                if (c >= 1 && c <= n)
                    hist[c - 1]++;
                if (c >= half)
                    atLeastHalf++;
            }

            double mean = 0;
            for (int i = 0; i < n; i++) mean += perRunCount[i];
            if (n > 0) mean /= n;
            double varSum = 0;
            for (int i = 0; i < n; i++) { double d = perRunCount[i] - mean; varSum += d * d; }
            double std = n > 0 ? Math.Sqrt(varSum / n) : 0;

            return new CrossRunDetection
            {
                RunNames = runNames,
                PerRunCount = perRunCount,
                CumUnion = cumUnion,
                CumIntersection = cumIntersection,
                AtLeastHalf = atLeastHalf,
                RunCountHistogram = hist,
                MeanPerRun = mean,
                StdPerRun = std,
            };
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

        // The paired estimator (FDRBench / Wen et al. 2025) is a strictly 1-fold
        // method: it is only valid when every target carries exactly one entrapment
        // twin (r = 1). Beyond this tolerance the paired curve is suppressed; combined
        // and lower-bound carry the r term and stay valid at any ratio (e.g. a routine
        // 10% entrapment overlay, r = 0.1).
        private const double PairedRatioTolerance = 0.05;

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
            // These are ascending sorts of primitive q-value doubles that are swept with
            // <= below: the value is the entire sort key, so ties are bit-identical and
            // their order cannot change any count. Stability is irrelevant here (and this
            // is C#-only diagnostics, off the cross-impl parity path).
            targetQ.Sort(); // Array.Sort OK: primitive q doubles, value is whole key, tie order immaterial to the <= sweep
            entrapQ.Sort(); // Array.Sort OK: primitive q doubles, value is whole key, tie order immaterial to the <= sweep
            eventsA.Sort(); // Array.Sort OK: primitive q doubles, value is whole key, tie order immaterial to the <= sweep
            eventsB.Sort(); // Array.Sort OK: primitive q doubles, value is whole key, tie order immaterial to the <= sweep

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
            // The paired estimator is 1-fold only; suppress it for partial (r != 1)
            // entrapment libraries so a nonsensical paired curve is never shown. The
            // template surfaces PairedSuppressedPartial as a note.
            bool pairedOk = anyPair && Math.Abs(r - 1.0) <= PairedRatioTolerance;
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
                Paired = pairedOk ? Pick(paired, idx) : null,
                PairedSuppressedPartial = anyPair && !pairedOk,
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
