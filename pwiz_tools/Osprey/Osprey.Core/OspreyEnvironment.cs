/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

namespace pwiz.Osprey.Core
{
    /// <summary>
    /// Central access point for OSPREY_* environment variables that control
    /// production behavior (throttling, fast-iteration early exits, algorithm
    /// variants). A separate OspreyDiagnostics class covers the diagnostic-dump
    /// env vars. Values are read once at process start and cached as readonly
    /// static fields so callers never reach for
    /// <see cref="Environment.GetEnvironmentVariable(string)"/> inline.
    ///
    /// Lives in Osprey.Core so every project below the main pipeline
    /// (FDR, Chromatography, Scoring, ML, IO) can read it without
    /// depending on the main project (which would create a cycle). See
    /// "Osprey project layering" in
    /// <c>ai/docs/osprey-development-guide.md</c>.
    /// </summary>
    public static class OspreyEnvironment
    {
        /// <summary>
        /// OSPREY_MAX_PARALLEL_FILES: legacy back-compat cap on concurrent file
        /// processing, superseded by the <c>--parallel-files</c> CLI argument
        /// (which wins when both are set). Consulted by
        /// <see cref="FileParallelismResolver"/> only when the argument is absent.
        /// Values:
        ///   0 / unset = no cap from here (the default is now strictly sequential)
        ///   1        = strictly sequential
        ///   N &gt; 1    = at most N files concurrently
        /// Note the default changed: an unset value used to mean "all files at
        /// once"; it now means "one file at a time" -- opt into concurrency with
        /// <c>--parallel-files</c>. Useful historically for memory-bound datasets
        /// (Astral HRAM) where three large working sets exceed a 64 GB budget.
        /// </summary>
        public static readonly int MaxParallelFiles = ParseIntOrZero(@"OSPREY_MAX_PARALLEL_FILES");

        /// <summary>
        /// OSPREY_MAX_SCORING_WINDOWS: limits main-search isolation windows
        /// scored in Stage 4. Used for fast iteration during dotTrace
        /// profiling and parity bisection. 0 or unset means "score them all".
        /// </summary>
        public static readonly int MaxScoringWindows = ParseIntOrZero(@"OSPREY_MAX_SCORING_WINDOWS");

        /// <summary>
        /// OSPREY_LOESS_CLASSICAL_ROBUST: use classical Cleveland (1979) robust
        /// LOESS iteration (residuals recomputed from the current fit each
        /// pass) instead of the legacy behavior that caches absolute residuals
        /// from the initial fit. Default on to match Rust calibration_ml.rs
        /// v26.3.1 and later; set to "0" to force the legacy single-refresh
        /// path for comparison.
        /// </summary>
        public static readonly bool LoessClassicalRobust = IsNotZero(@"OSPREY_LOESS_CLASSICAL_ROBUST");

        /// <summary>
        /// OSPREY_EXIT_AFTER_CALIBRATION: exit after Stage 3 (calibration
        /// complete), skipping Stage 4 main search and everything downstream.
        /// Used for calibration-only benchmarking and bisection.
        /// </summary>
        public static readonly bool ExitAfterCalibration = IsSet(@"OSPREY_EXIT_AFTER_CALIBRATION");

        /// <summary>
        /// OSPREY_CAL_MEDIANPOLISH=1: add median-polish cosine (the dominant full-search
        /// Percolator feature) as a 5th calibration-LDA feature, computed over the
        /// peak-cropped calibration XICs. Experimental lever for raising the calibration
        /// peak-selection yield; default OFF keeps the calibration output byte-identical
        /// and perf-neutral (the feature is neither computed nor scored when unset).
        /// </summary>
        public static readonly bool CalMedianPolishFeature = IsSetAndNotZero(@"OSPREY_CAL_MEDIANPOLISH");

        /// <summary>
        /// OSPREY_CAL_SAMPLE_SIZE: override the calibration library sample size (targets
        /// sampled per attempt). Default 0 = use the configured CalibrationSampleSize
        /// (100K). Experimental lever for testing whether a larger sample surfaces
        /// proportionally more near-zero-FDR calibration anchors on rich files.
        /// </summary>
        public static readonly int CalSampleSizeOverride = ParseIntOrZero(@"OSPREY_CAL_SAMPLE_SIZE");

        // Note: the OSPREY_EXIT_AFTER_SCORING env var that used to live here
        // was retired in favor of the --task PerFileScoring CLI flag. See the HPC
        // scoring split work in AnalysisPipeline.Run. ExitAfterCalibration
        // (Stage 3) stays because it has no production CLI analog.

        /// <summary>
        /// OSPREY_LOAD_CALIBRATION: path to a .calibration.json produced by
        /// the Rust implementation. When set and the file exists, Stage 3 is
        /// skipped and the Rust calibration is loaded directly. Used for
        /// feature-parity bisection (isolates downstream feature divergence
        /// from calibration drift).
        /// </summary>
        public static readonly string LoadCalibrationPath = Environment.GetEnvironmentVariable(@"OSPREY_LOAD_CALIBRATION");

        /// <summary>
        /// OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT: when a unit test is run under
        /// the cross-impl harness, the round-trip test for the v2
        /// .fdr_scores.bin format also copies its output to this path, so a
        /// sibling Rust unit test (with the same hardcoded inputs) can be
        /// byte-compared against ours. Test-only hook; never set in
        /// production. The harness verifies cross-impl byte parity once both
        /// sides have written their copy.
        /// </summary>
        public static readonly string CrossImplFdrSidecarOut = Environment.GetEnvironmentVariable(@"OSPREY_CROSS_IMPL_FDR_SIDECAR_OUT");

        /// <summary>
        /// OSPREY_CROSS_IMPL_RECONCILIATION_OUT: same idea as
        /// CrossImplFdrSidecarOut but for the per-file
        /// .reconciliation.json boundary file. Test-only hook; never set
        /// in production.
        /// </summary>
        public static readonly string CrossImplReconciliationOut = Environment.GetEnvironmentVariable(@"OSPREY_CROSS_IMPL_RECONCILIATION_OUT");

        /// <summary>
        /// OSPREY_FDR_PROJECTION (issue #4355 step (b) increment ii): route the
        /// first-pass FDR peak through the thin <c>FdrProjection</c> struct
        /// buffer instead of holding the full <see cref="FdrEntry"/> stub buffer
        /// resident across first-pass Percolator + protein FDR + the sidecar write
        /// + compaction. <c>FirstJoinTask</c> materializes the projection from the
        /// cold hand-off buffer, releases the <see cref="FdrEntry"/> stubs before the
        /// SVM peak, and reloads full <see cref="FdrEntry"/> survivors from parquet +
        /// the just-written 1st-pass sidecar after compaction.
        ///
        /// DEFAULT ON: Osprey cannot process real (large) file counts without this --
        /// the legacy resident path OOMs -- so streaming is the production default and
        /// byte-identical to the legacy path (Stellar regression mode1/2/3). Set
        /// OSPREY_FDR_PROJECTION=0 ONLY to force the legacy <see cref="FdrEntry"/>-buffer
        /// path as a transitional A/B / byte-identity oracle; that path (and this flag)
        /// are slated for removal once model-diagnostics + FDRBench stream from the
        /// persisted per-file scores. A settable property (not a readonly field) so
        /// unit tests can A/B both paths.
        /// </summary>
        public static bool UseFdrProjection { get; set; } = IsNotZero(@"OSPREY_FDR_PROJECTION");

        /// <summary>
        /// Semi-supervised training iterations for <c>--fdr-method fasttree</c>
        /// (OSPREY_GBT_MAX_ITERATIONS); 0/unset uses <see cref="GBT_MAX_ITERATIONS_DEFAULT"/>.
        /// Tree-only: the linear SVM keeps its own fixed 10 and is untouched by this.
        ///
        /// Exists because the two classifiers converge at very different rates. On Stellar
        /// the SVM plateaus by iteration 4 and early-stops, while the trees were still
        /// improving monotonically (0.7 -> 1.2% of training targets at 1% FDR) when they
        /// hit the shared cap of 10 -- i.e. the cap was binding on the trees, so their
        /// reported discrimination may understate the model rather than measure it. Raising
        /// it costs nothing when it is not binding: the existing
        /// stop-after-2-non-improving-iterations rule still ends training on convergence.
        /// </summary>
        public static readonly int GbtMaxIterations = ResolveGbtMaxIterations();

        /// <summary>Default for <see cref="GbtMaxIterations"/>. Well above the SVM's 10 so
        /// convergence (not the cap) ends tree training, while still bounding a pathological
        /// run: each iteration retrains the full ensemble on the &lt;= MaxTrainSize subsample.</summary>
        public const int GBT_MAX_ITERATIONS_DEFAULT = 30;

        private static int ResolveGbtMaxIterations()
        {
            int v = ParseIntOrZero(@"OSPREY_GBT_MAX_ITERATIONS");
            return v > 0 ? v : GBT_MAX_ITERATIONS_DEFAULT;
        }

        /// <summary>Optional overrides for the gradient-boosted-trees hyper-parameters
        /// (<c>--fdr-method fasttree</c>), so a regularization / capacity sweep runs from
        /// env vars without a recompile per setting. Each is null when its var is unset,
        /// leaving the validated <c>GbtParams</c> default in place; applied in
        /// <c>BuildProjectionPercolatorConfig</c>. Tree-only -- the linear SVM ignores them.
        ///   OSPREY_GBT_GAMMA            min split gain to keep a split   (default 0, off)
        ///   OSPREY_GBT_LAMBDA           L2 leaf-weight penalty           (default 1)
        ///   OSPREY_GBT_ALPHA            L1 leaf-weight penalty           (default 0, off)
        ///   OSPREY_GBT_MAX_DEPTH        tree depth                       (default 6)
        ///   OSPREY_GBT_N_TREES          boosting rounds per model        (default 200)
        ///   OSPREY_GBT_MIN_CHILD_WEIGHT min summed child hessian         (default 1)
        ///   OSPREY_GBT_LEARNING_RATE    shrinkage                        (default 0.1)
        ///   OSPREY_GBT_SUBSAMPLE        row subsample per tree           (default 0.8)
        ///   OSPREY_GBT_COLSAMPLE        feature subsample per tree       (default 0.8)
        /// The chosen values are echoed to the run log (the "Gradient-boosted trees: ..."
        /// line) so each sweep point records exactly what it ran with.</summary>
        public static readonly double? GbtGamma = ParseDoubleOrNull(@"OSPREY_GBT_GAMMA");
        public static readonly double? GbtRegLambda = ParseDoubleOrNull(@"OSPREY_GBT_LAMBDA");
        public static readonly double? GbtRegAlpha = ParseDoubleOrNull(@"OSPREY_GBT_ALPHA");
        public static readonly int? GbtMaxDepth = ParseIntOrNull(@"OSPREY_GBT_MAX_DEPTH");
        public static readonly int? GbtNTrees = ParseIntOrNull(@"OSPREY_GBT_N_TREES");
        public static readonly double? GbtMinChildWeight = ParseDoubleOrNull(@"OSPREY_GBT_MIN_CHILD_WEIGHT");
        public static readonly double? GbtLearningRate = ParseDoubleOrNull(@"OSPREY_GBT_LEARNING_RATE");
        public static readonly double? GbtSubsample = ParseDoubleOrNull(@"OSPREY_GBT_SUBSAMPLE");
        public static readonly double? GbtColSample = ParseDoubleOrNull(@"OSPREY_GBT_COLSAMPLE");

        /// <summary>Optional override for <c>PercolatorConfig.MaxTrainSize</c> -- the
        /// Percolator-3.0 peptide-grouped training-subsample cap (default 300000). Set via
        /// OSPREY_MAX_TRAIN_SIZE. Raising it feeds the classifier more real rows (the cap is
        /// binding when the deduped population exceeds it) at the cost of memory + training
        /// time. Null when unset -- keeps the 300k default.</summary>
        public static readonly int? MaxTrainSizeOverride = ParseIntOrNull(@"OSPREY_MAX_TRAIN_SIZE");

        /// <summary>Inner-fold count for the GBDT's held-out iteration selection
        /// (OSPREY_GBT_INNER_FOLDS, default 5 -> hold out 20% of each training fold to pick
        /// the boosting iteration honestly). A value &lt;= 1 turns held-out selection OFF and
        /// reverts to IN-SAMPLE selection (fit = validate = all training rows) -- the
        /// pre-held-out, validated behavior. Exposed so a regularization sweep or an
        /// in-sample-vs-held-out A/B runs without a code revert. Tree-only.</summary>
        public static readonly int GbtInnerFolds = ParseIntOrNull(@"OSPREY_GBT_INNER_FOLDS") ?? 5;

        /// <summary>The default <see cref="Pass2QValue"/> mode: retrain the 2nd-pass
        /// Percolator SVM and recompute a target/decoy null on the reconciled + compacted
        /// reported pool. Current (PR #4395) behavior; preserves Rust parity.</summary>
        public const string PASS2_QVALUE_PERCOLATOR = @"percolator";

        /// <summary>The <see cref="Pass2QValue"/> confidence-transfer mode: do NOT retrain
        /// or re-estimate a null; score each reconciled peak with the frozen 1st-pass model
        /// and map it to a q via the full pre-compaction 1st-pass score-&gt;q table.</summary>
        public const string PASS2_QVALUE_TRANSFER = @"transfer";

        /// <summary>The <see cref="Pass2QValue"/> transfer-with-competition mode: score the
        /// reconciled targets+decoys with the FROZEN 1st-pass model (no retrain), then
        /// recompute q + PEP by a fresh target-decoy competition over that full reconciled
        /// population (a non-depleted null) -- i.e. the frozen weights feed the standard
        /// competition q/PEP math instead of a co-monotone score->q table lookup.</summary>
        public const string PASS2_QVALUE_TRANSFER_COMPETE = @"transfer-compete";

        /// <summary>The <see cref="Pass2QValue"/> protein-anchored constrained mode: like
        /// <see cref="PASS2_QVALUE_TRANSFER_COMPETE"/> (frozen 1st-pass model, no retrain),
        /// but the target-decoy competition is CONSTRAINED to the peptides of proteins
        /// detected in the 1st pass -- included as target+decoy PAIRS so the stratum's null
        /// stays fair. Removing off-stratum decoys from the null lowers q for stratum
        /// members (reduced multiple testing / independent filtering; Bourgon 2010), which
        /// recovers marginal peptides of already-detected proteins. Honest because the
        /// protein-membership filter is ~independent of a peptide's own decoy score (a
        /// protein is detected via its OTHER peptides) and the stratum keeps its paired
        /// decoys. The frozen model avoids the two-pass retrain's over-separation.</summary>
        public const string PASS2_QVALUE_PROTEIN_COMPACT = @"protein-compact";

        /// <summary>
        /// OSPREY_PASS2_QVALUE: selects how the merge-node 2nd pass assigns the reported
        /// precursor/peptide q-values AFTER Stage 6 reconciliation. The 2nd-pass peak
        /// RE-SCORING (better peak choices against the consensus) is kept in ALL modes;
        /// only the q-value step changes.
        ///   <see cref="PASS2_QVALUE_PERCOLATOR"/> (default): retrain Percolator + recompute
        ///     a target/decoy null on the reconciled + compacted pool. Preserves the
        ///     always-on Rust 2nd pass. Compaction has already stripped most decoys from
        ///     that pool, so the null is decoy-depleted and the retrained q anti-conservative.
        ///   <see cref="PASS2_QVALUE_TRANSFER"/>: score each reconciled peak with the FROZEN
        ///     1st-pass model and read its q from the FULL pre-compaction 1st-pass
        ///     score-&gt;q table (co-monotonic confidence transfer; Rost 2016 TRIC). No
        ///     retrain, no reduced-pool null. Restores calibration while keeping the
        ///     re-scoring ID gain.
        /// Unset or unrecognized normalizes to the parity-preserving default. Read once at
        /// process start. See ai/todos/active/TODO-20260710_osprey_pass2_recalibration_fix.md.
        ///
        /// LIMITATION (experimental mode): use a FRESH <c>--output-dir</c> per mode. The
        /// per-file 2nd-pass sidecar (.2nd-pass.fdr_scores.bin) is not tagged with the mode,
        /// so resuming a run in an output dir written under a different mode would reuse the
        /// other mode's cached q-values. The Part-B work tags the sidecar validity with the
        /// mode; until then, do not switch modes within one output dir.
        /// </summary>
        public static readonly string Pass2QValue = NormalizePass2QValue(
            Environment.GetEnvironmentVariable(@"OSPREY_PASS2_QVALUE"));

        /// <summary>True when OSPREY_PASS2_QVALUE was set to a value that is neither
        /// <see cref="PASS2_QVALUE_PERCOLATOR"/> nor <see cref="PASS2_QVALUE_TRANSFER"/> and
        /// was therefore normalized to the default. The consuming site logs a one-line
        /// warning so a typo does not silently pick the default.</summary>
        public static readonly bool Pass2QValueUnrecognized = IsUnrecognizedPass2QValue(
            Environment.GetEnvironmentVariable(@"OSPREY_PASS2_QVALUE"));

        /// <summary>True when <see cref="Pass2QValue"/> selects the frozen-model
        /// confidence-transfer path (OSPREY_PASS2_QVALUE=transfer).</summary>
        public static readonly bool Pass2TransferQ =
            string.Equals(Pass2QValue, PASS2_QVALUE_TRANSFER, StringComparison.Ordinal);

        /// <summary>True when <see cref="Pass2QValue"/> selects the frozen-model +
        /// target-decoy competition path (OSPREY_PASS2_QVALUE=transfer-compete).</summary>
        public static readonly bool Pass2TransferCompete =
            string.Equals(Pass2QValue, PASS2_QVALUE_TRANSFER_COMPETE, StringComparison.Ordinal);

        /// <summary>True when <see cref="Pass2QValue"/> selects the protein-anchored
        /// constrained competition (OSPREY_PASS2_QVALUE=protein-compact).</summary>
        public static readonly bool Pass2ProteinCompact =
            string.Equals(Pass2QValue, PASS2_QVALUE_PROTEIN_COMPACT, StringComparison.Ordinal);

        private static string NormalizePass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return PASS2_QVALUE_PERCOLATOR;
            string v = raw.Trim().ToLowerInvariant();
            if (v == PASS2_QVALUE_TRANSFER)
                return PASS2_QVALUE_TRANSFER;
            if (v == PASS2_QVALUE_TRANSFER_COMPETE)
                return PASS2_QVALUE_TRANSFER_COMPETE;
            if (v == PASS2_QVALUE_PROTEIN_COMPACT)
                return PASS2_QVALUE_PROTEIN_COMPACT;
            // Fall back to the parity-preserving default on any unrecognized token; the
            // consuming site (Pass2FdrSidecar) warns so a typo is visible in the log.
            return PASS2_QVALUE_PERCOLATOR;
        }

        private static bool IsUnrecognizedPass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string v = raw.Trim().ToLowerInvariant();
            return v != PASS2_QVALUE_PERCOLATOR && v != PASS2_QVALUE_TRANSFER &&
                   v != PASS2_QVALUE_TRANSFER_COMPETE && v != PASS2_QVALUE_PROTEIN_COMPACT;
        }

        private static int ParseIntOrZero(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return 0;
            int.TryParse(v, out int result);
            return result;
        }

        /// <summary>Env int override, or null when unset/unparseable -- lets a consumer keep
        /// its own default rather than collapsing an unset var to 0 (as ParseIntOrZero does).</summary>
        private static int? ParseIntOrNull(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return null;
            return int.TryParse(v, out int result) ? result : (int?)null;
        }

        /// <summary>Env double override (invariant culture, so "0.5" parses regardless of
        /// locale), or null when unset/unparseable.</summary>
        private static double? ParseDoubleOrNull(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return null;
            return double.TryParse(v, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out double result)
                ? result : (double?)null;
        }

        private static bool IsSet(string name)
        {
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name));
        }

        private static bool IsNotZero(string name)
        {
            return Environment.GetEnvironmentVariable(name) != @"0";
        }

        private static bool IsSetAndNotZero(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return !string.IsNullOrEmpty(v) && v != @"0";
        }
    }
}
