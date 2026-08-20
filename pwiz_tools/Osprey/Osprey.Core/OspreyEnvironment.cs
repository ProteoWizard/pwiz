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
using System.Collections.Generic;
using System.Linq;

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
        /// OSPREY_MZML_VIA_MZMLREADER=1: read mzML with the hand-written
        /// <c>MzmlReader</c> instead of ProteoWizard. Diagnostic only, and
        /// meaningful only in a build that HAS ProteoWizard (net472 with
        /// <c>/p:OspreyVendorReader=true</c>), where ProteoWizard is otherwise used
        /// for every input format including mzML. A no-op anywhere else, since
        /// <c>MzmlReader</c> is already the only reader there.
        ///
        /// This isolates the two READERS against a fixed input: run the same
        /// mzML both ways and the resulting <c>.spectra.bin</c> files must be
        /// byte-identical, because nothing about the source file differs. A
        /// raw-vs-mzML comparison cannot make that claim - it varies the reader
        /// and the file at the same time, so a difference could come from
        /// either. Any difference this switch exposes is a defect in
        /// <c>MzmlReader</c>, which is the only parser in the picture that is
        /// not ProteoWizard.
        ///
        /// The switch is deliberately the ESCAPE HATCH rather than the opt-in: it
        /// exists to keep that comparison possible, and it disappears along with
        /// <c>MzmlReader</c> once ProteoWizard has a .NET 8 build (#4178).
        /// </summary>
        public static readonly bool MzmlViaMzmlReader = IsSetAndNotZero(@"OSPREY_MZML_VIA_MZMLREADER");

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
        /// + compaction. <c>FirstPassFdrTask</c> materializes the projection from the
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
        /// Stage 6 rebuilds each file's post-compaction survivors from that file's
        /// <c>.scores.parquet</c> + 1st-pass sidecar just before rescoring it, and drops
        /// them again once its reconciled parquet is on disk - so the all-files survivor
        /// buffer is not resident across the rescore loop.
        ///
        /// DEFAULT ON. That buffer is 88.9 M entries / 28 GB live at 163 files, held for
        /// the 5.5 hours of Stage 6, and it grows super-linearly in file count because the
        /// passing base_id set grows too (issue #4526). Set
        /// OSPREY_STAGE6_STREAM_SURVIVORS=0 to keep the resident buffer as the A/B
        /// byte-identity oracle, the same role OSPREY_FDR_PROJECTION=0 plays for Stage 5.
        /// A settable property (not a readonly field) so unit tests can A/B both paths.
        /// </summary>
        public static bool Stage6StreamSurvivors { get; set; } =
            IsNotZero(@"OSPREY_STAGE6_STREAM_SURVIVORS");

        /// <summary>
        /// At the Stage 5 -> 6 boundary, drop <c>LibraryEntry.Fragments</c> for every library
        /// entry that can no longer be scored or written - i.e. everything outside the
        /// compaction survivors and the gap-fill candidates. The identity fields
        /// (<c>ModifiedSequence</c> / <c>ProteinIds</c> / m/z / RT) are KEPT on every entry.
        ///
        /// DEFAULT ON. The library is held to the end of Stage 7 in order to write 37,078
        /// spectra out of 6,275,151 entries - 0.6%. Set OSPREY_RELEASE_LIBRARY_FRAGMENTS=0 to
        /// keep the whole library resident as the A/B byte-identity oracle, the same role
        /// OSPREY_STAGE6_STREAM_SURVIVORS=0 plays for the Stage 6 handoff.
        ///
        /// <para>MEASURED, as an A/B on 4 SEA-AD files against the full 12.7 GB library: Stage 7
        /// peak working set 28.5 -&gt; 17.7 GB, a 10.8 GB (-38%) saving, releasing 87.0% of the
        /// entries. Few files is the MAXIMUM-saving case rather than a scaled-down one - the
        /// library is fixed while the retained set grows with file count - so expect a smaller
        /// (still large) saving at 82. Only the FRAGMENTS are freed, never a whole entry, and
        /// the in-repo figure for the fragment share alone is ~3.2 GB at SEA-AD scale; do not
        /// read a saving here as recovering the library's total footprint.</para>
        ///
        /// <para>Why fragments and not whole entries: <c>ProteinFdr.BuildProteinParsimony</c>
        /// and <c>FirstPassFdrTask.BuildProteinCompactStratum</c> both walk the ENTIRE library
        /// after Stage 5, including entries already judged false. They read only the identity
        /// fields, never the spectra - so dropping entries would silently move protein FDR,
        /// while dropping fragments cannot. The blib write is safe for a separate reason:
        /// <c>BlibOutputWriter.PrecompressSpectra</c> reads fragments only for
        /// <c>bestByPrecursor</c>, which is derived from the post-compaction survivors, so
        /// blib-written is a SUBSET of what is retained here.</para>
        ///
        /// <para>Turning this OFF costs a full Stage-5 recompute rather than a resume, because
        /// the changed validity key fails <c>CanRehydrate</c> and <c>Run</c> deletes its own
        /// validity sidecars. The suffix is still required - the release is a Run-only side
        /// effect, so without it an in-place A/B would adopt the other arm's reconciled parquets
        /// and report a memory profile it never computed - but the escape hatch is not cheap.
        /// The suffix itself lives with the release
        /// (<c>LibraryFragmentRelease.ValidityKeySuffix</c>), keyed on whether the release
        /// actually RAN rather than on this flag alone.</para>
        ///
        /// <para>A settable property (not a readonly field) so unit tests can A/B both arms.</para>
        /// </summary>
        public static bool ReleaseLibraryFragments { get; set; } =
            IsNotZero(@"OSPREY_RELEASE_LIBRARY_FRAGMENTS");

        /// <summary>
        /// Cache-validity suffix for the Stage 6 handoff arm. EMPTY on the streamed default,
        /// so shipping this does not invalidate a single existing output directory; only the
        /// resident opt-out adds a term.
        ///
        /// <para>Without it an in-place A/B of the two arms is not an A/B at all: the second
        /// run finds the first run's reconciled parquets valid, skips Stage 6 entirely, and
        /// reports a clean match it never computed. That is the exact failure mode the two arms
        /// exist to detect, so leaving it out makes the oracle self-confirming.</para>
        /// </summary>
        public static string Stage6StreamSurvivorsValidityKeySuffix()
        {
            return Stage6StreamSurvivors ? string.Empty : @";stage6stream=0";
        }

        /// <summary>
        /// OSPREY_PICK_DUMP_CANDIDATES: when set to a non-empty / non-zero value, dump one
        /// row per CWT candidate peak of every precursor (targets AND decoys) scored in the
        /// first-pass main search to a per-input-file TSV
        /// (<c>&lt;work-dir&gt;\&lt;inputStem&gt;.pick_candidates.tsv</c>). The row carries the
        /// exact raw rank terms the picker computes (coelution, ln_intensity, rt_penalty,
        /// median_polish) plus the candidate bounds and whether it was the chosen peak, so a
        /// downstream trainer can learn a linear pick model (see <see cref="PickLdaModelPath"/>)
        /// on precisely those values. Default OFF: no per-candidate median polish is computed and
        /// no file is written, so the hot loop is byte-identical and pays nothing when unset.
        /// </summary>
        public static readonly bool PickDumpCandidates = IsSetAndNotZero(@"OSPREY_PICK_DUMP_CANDIDATES");

        /// <summary>
        /// OSPREY_PICK_LDA_MODEL: path to a JSON file with a frozen linear pick model. When set
        /// and the file exists, the CWT candidate rank score is REPLACED by
        ///   rank = w0*z(coelution) + w1*z(ln_intensity) + w2*z(rt_penalty) + w3*z(median_polish)
        /// where z(x_i) = (x_i - mean[i]) / scale[i], using the same four raw terms the dump
        /// (<see cref="PickDumpCandidates"/>) captures. The argmax selection and IEEE-754
        /// total-order tie-break are unchanged. Overrides the resolution-keyed default model.
        /// Loaded and cached once by <c>PickLdaModel</c>. JSON schema:
        ///   { "features": ["coelution","ln_intensity","rt_penalty","median_polish"],
        ///     "weights": [w0,w1,w2,w3], "means": [m0,m1,m2,m3], "scales": [s0,s1,s2,s3] }
        /// </summary>
        public static readonly string PickLdaModelPath = Environment.GetEnvironmentVariable(@"OSPREY_PICK_LDA_MODEL");

        /// <summary>
        /// OSPREY_PICK_LDA: use the learned resolution-keyed linear peak-pick model (Stellar
        /// for unit, Astral for HRAM). DEFAULT ON; set OSPREY_PICK_LDA=0 for the legacy pure
        /// product-form pick (coelution * rt_penalty * ln_intensity, no median-polish factor).
        /// Precedence in the picker:
        ///   1. OSPREY_PICK_LDA_MODEL set -> that model (test override);
        ///   2. else OSPREY_PICK_LDA != 0 (default) -> the hardcoded resolution-keyed model;
        ///   3. else (OSPREY_PICK_LDA=0) -> the legacy product pick.
        ///
        /// The default moved to the learned model with the #4484 golden re-baseline. An
        /// additive rank over standardized terms is the same direction Skyline's
        /// LegacyScoringModel took, and it is unlikely that a three-way product of terms whose
        /// combination in log space was never established is the end state. Measured effect on
        /// the discovery set is SMALL and its sign is not stable per cohort (seven A/B cells
        /// spanning -3.5% to +1.8% at matched true FDP), so do NOT expect a sensitivity gain
        /// from this and do NOT read a single validation run as evidence either way - the pick
        /// relocates ~44% of contested peaks and moves discoveries by about 1%. The =0 opt-out
        /// is kept precisely so the two stay comparable.
        /// </summary>
        public static readonly bool PickLda = IsNotZero(@"OSPREY_PICK_LDA");

        /// <summary>
        /// Semi-supervised training iterations for <c>--fdr-method gbdt</c>
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
        /// (<c>--fdr-method gbdt</c>), so a regularization / capacity sweep runs from
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

        /// <summary>The <see cref="ExperimentAgg"/> default: each precursor/peptide keeps its
        /// single BEST (max) observation across runs before the experiment-wide target/decoy
        /// competition. The shipped behavior; the committed golden is byte-identical here.</summary>
        public const string EXPERIMENT_AGG_MAX = @"max";

        /// <summary>Prefix of the <see cref="ExperimentAgg"/> reproducibility mode value
        /// OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt; (e.g. mean-best-2, mean-best-3, mean-best-4): the
        /// experiment-wide PRECURSOR score becomes the mean of its best-N per-run scores (runs beyond
        /// the detected count are filled with the decoy-median floor; see the OSPREY_MEANBEST2_FLOOR_*
        /// A/B toggles), rolled up by MAX to PEPTIDE. Not to protein: protein FDR ranks groups by the
        /// max RAW per-peptide SVM score and never reads this aggregate, so mean(best-N) reaches
        /// protein results only through which peptides clear the experiment-q gate. Larger N rewards
        /// detection in more runs (drives toward the &gt;=N-run reproducibility frontier). Symmetric
        /// for decoys, so the null stays honest - the honest sensitivity lever for #4484 vs. the
        /// target-conditioned transfer-compete/protein-compact. N is read from the flag value and
        /// bounded by <see cref="MEAN_BEST_N_MAX"/>; a future command argument may pick N
        /// intelligently from the run count.</summary>
        public const string EXPERIMENT_AGG_MEAN_BEST_PREFIX = @"mean-best-";

        /// <summary>Largest accepted N in OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt;. Two unbounded
        /// failures motivate a cap rather than trust: <c>mean-best-1000000</c> allocates an
        /// N-wide accumulator per (base_id, side) - gigabytes, an out-of-memory abort seconds
        /// into Stage 5 - and any N above the run count leaves EVERY unit floor-filled, which
        /// saturates the statistic (for N &gt;= the largest observation count the aggregate is
        /// <c>(S - L*floor)/N + floor</c>, whose ranking no longer depends on N at all) while
        /// still being recorded by the operator as a distinct A/B arm. 64 is far above any
        /// plausible reproducibility frontier for a DIA experiment and well inside both
        /// failures. Values above it are rejected as unrecognized, so
        /// <see cref="ExperimentAggUnrecognized"/> warns instead of the run silently
        /// proceeding on the max default.</summary>
        public const int MEAN_BEST_N_MAX = 64;

        /// <summary>
        /// OSPREY_PASS2_QVALUE: selects how the SecondPassFDR 2nd pass assigns the reported
        /// precursor/peptide q-values AFTER Stage 6 reconciliation. The 2nd-pass peak
        /// RE-SCORING (better peak choices against the consensus) is kept in ALL modes;
        /// only the q-value step changes.
        ///   <see cref="PASS2_QVALUE_PROTEIN_COMPACT"/> (default): frozen 1st-pass model, with
        ///     the target-decoy competition constrained to the protein stratum.
        ///   <see cref="PASS2_QVALUE_TRANSFER"/>: carry the pass-1 q through and recompute ONLY
        ///     the per-run q of the peaks reconciliation MOVED, scoring each with the FROZEN
        ///     1st-pass model and mapping it through THAT FILE'S OWN on-disk
        ///     <c>.1st-pass.fdr_scores.bin</c> score-&gt;run-q table, one file at a time. No
        ///     retrain, no reduced-pool null. Restores calibration while keeping the
        ///     re-scoring ID gain.
        ///     NOTE: the per-run-only redesign (#4438) REPLACED the earlier full pre-compaction
        ///     score-&gt;q table, which is why transfer no longer needs the O(files) resident
        ///     pool. Re-adding it to any resident-pool gate is the #4446 regression; see
        ///     <c>ResidentPaths</c>.
        /// Unset normalizes to the default; an unrecognized value is a startup ERROR (see
        /// <see cref="Pass2QValueUnrecognized"/>). Read once at process start.
        ///
        /// The former default <c>percolator</c> - retrain the 2nd-pass Percolator SVM and
        /// recompute a target/decoy null on the reconciled + COMPACTED pool - was REMOVED, not
        /// merely demoted. Compaction strips most decoys from that pool, so the retrained null
        /// is thin and the reported q anti-conservative: 1.57% true FDP at a nominal 1% on
        /// Stellar libdecoy entrapment (vs 0.92% for the 1st-pass q), and ~9% on an 82-file
        /// SEA-AD set. The linear model trained by the 1st-pass SVM is now the model for pass 2
        /// in every mode; only the <see cref="Pass2ProteinCompactRetrain"/> diagnostic A/B still
        /// retrains. See ai/todos/active/TODO-20260710_osprey_pass2_recalibration_fix.md.
        ///
        /// Switching modes within one output directory is now SAFE: the mode participates in
        /// the resume validity key through <see cref="Pass2QValueValidityKeySuffix()"/>, so a
        /// re-run under a different mode invalidates the previous mode's outputs instead of
        /// adopting its cached q-values. That retires the standing "use a FRESH --output-dir
        /// per mode" limitation, which became far more than an experimental-mode caveat once
        /// this variable acquired a non-percolator default.
        /// </summary>
        public static readonly string Pass2QValue = NormalizePass2QValue(
            Environment.GetEnvironmentVariable(@"OSPREY_PASS2_QVALUE"));

        /// <summary>True when OSPREY_PASS2_QVALUE was set to a value that is not one of the
        /// recognized modes. Program startup ABORTS on this rather than falling back: silently
        /// substituting the default would report numbers the caller did not ask for, and the
        /// removed <c>percolator</c> token in particular is one that existing sweep scripts
        /// still pass. Checked at startup, not at SecondPassFDR, so the run fails in seconds
        /// instead of after Stage 1-5.</summary>
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

        /// <summary>Diagnostic A/B toggle (OSPREY_PROTEIN_COMPACT_RETRAIN): when set with
        /// OSPREY_PASS2_QVALUE=protein-compact, SKIP the frozen 1st-pass model + stratum
        /// competition and instead RETRAIN the 2nd-pass Percolator over the same
        /// stratum-expanded compacted pool. Isolates the frozen-vs-retrain FDR-calibration
        /// difference (same reported set, only the 2nd-pass scoring changes) for the
        /// FDRBench/entrapment oracle. Off (frozen) by default.</summary>
        public static readonly bool Pass2ProteinCompactRetrain =
            IsSetAndNotZero(@"OSPREY_PROTEIN_COMPACT_RETRAIN");

        /// <summary>
        /// OSPREY_ALLOW_UNFIXED_RESIDENT: name the known-unfixed resident path(s) this run may
        /// take, e.g. <c>OSPREY_ALLOW_UNFIXED_RESIDENT=fdrbench-pass1</c>. Legal values are
        /// exactly <see cref="ResidentPaths.KNOWN_UNFIXED"/>; anything else, and any resident path
        /// that is not on that list, is refused no matter what this is set to.
        ///
        /// This REPLACES the former blanket <c>OSPREY_ALLOW_UNBOUNDED_MEMORY=1</c>, which granted
        /// amnesty to every trigger at once. That is not a hypothetical failure: it let
        /// <c>OSPREY_PASS2_QVALUE=transfer</c> silently regress back onto the resident pool for
        /// ten days (#4438 removed the forcing, a #4446 merge artifact restored it), because
        /// developers simply set the boolean and a re-broken memory bound looked like normal
        /// operating procedure. A named token cannot do that: an unlisted path errors even with
        /// this set, so the ONLY way to re-admit one is to add it to the committed list, which is
        /// a reviewed diff that fails <c>ResidentPoolGuardTest</c>.
        ///
        /// <para>SEVERAL paths may be named, comma- or semicolon-separated, because a run can
        /// legitimately trip more than one at once and a single-value variable made that run
        /// impossible to perform at all. An operator running the Stage 6 handoff A/B on a
        /// configuration that is already resident for its own reason needs
        /// <see cref="ResidentPaths.COMPACTED_ENTRIES_BUFFER"/> alongside that run's own token.
        /// A LIST keeps the property that matters - every admitted path is still named
        /// individually, so nothing rides along unnamed the way the blanket boolean allowed -
        /// while a single value only ever prevented honest work.</para>
        ///
        /// Read once at process start. Intended for local testing. The standing
        /// <c>regression.ps1</c> gate names NO token on any leg - #4536 removed the last one -
        /// and an INHERITED value is cleared at startup unless a deliberate A/B switch needs it.
        /// A resident path appearing anywhere in the gate fails CI rather than riding along on
        /// an ambient allowance, and any token the gate is ever made to require has to carry an
        /// open issue to remove it again.
        /// </summary>
        public static readonly string AllowUnfixedResident =
            (Environment.GetEnvironmentVariable(@"OSPREY_ALLOW_UNFIXED_RESIDENT") ?? string.Empty).Trim();

        /// <summary>
        /// True when <see cref="AllowUnfixedResident"/> names anything that is not a legal token,
        /// so the guard can say "that value is not a known path" instead of printing the
        /// same message it prints when the variable is unset. Without this, a typo
        /// (<c>mdiag_full_resume</c>) or a shell-quoted value (cmd.exe stores the quotes) is
        /// byte-for-byte indistinguishable from not setting it, and the operator is told to do
        /// what they believe they just did. Mirrors <see cref="Pass2QValueUnrecognized"/>.
        /// </summary>
        public static readonly bool AllowUnfixedResidentUnrecognized =
            SplitResidentTokens(AllowUnfixedResident).Any(
                v => !ResidentPaths.KNOWN_UNFIXED.Any(
                    t => string.Equals(t, v, StringComparison.OrdinalIgnoreCase)));

        /// <summary>
        /// Just the unrecognized tokens of <see cref="AllowUnfixedResident"/>, comma separated.
        /// The warning names THESE rather than the whole value: the flag is a list and
        /// <c>NamesResidentPath</c> tests each token independently, so a value pairing a retired
        /// token with a live one still grants the live one. Empty when every token is known.
        /// </summary>
        public static readonly string UnrecognizedResidentTokens =
            string.Join(@", ", SplitResidentTokens(AllowUnfixedResident).Where(
                v => !ResidentPaths.KNOWN_UNFIXED.Any(
                    t => string.Equals(t, v, StringComparison.OrdinalIgnoreCase))));

        /// <summary>
        /// Whether <paramref name="allowValue"/> (an OSPREY_ALLOW_UNFIXED_RESIDENT setting) names
        /// <paramref name="token"/>. Case-insensitive, matching how the rest of the CLI parses
        /// tokens: the guard's message names the exact token to set, so rejecting the operator's
        /// own value for capitalization would read as the guard ignoring what they just did.
        /// </summary>
        public static bool NamesResidentPath(string allowValue, string token)
        {
            return SplitResidentTokens(allowValue).Any(
                v => string.Equals(v, token, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// The individual tokens in an OSPREY_ALLOW_UNFIXED_RESIDENT setting. Comma and semicolon
        /// both separate, and empty entries are dropped, so a trailing separator is not a typo
        /// the operator has to hunt for.
        /// </summary>
        private static IEnumerable<string> SplitResidentTokens(string allowValue)
        {
            if (string.IsNullOrEmpty(allowValue))
                return Array.Empty<string>();
            return allowValue
                .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0);
        }

        /// <summary>The N in OSPREY_EXPERIMENT_AGG=mean-best-&lt;N&gt;: how many top per-run scores are
        /// averaged (runs beyond the detected count filled with the decoy floor). 0 in the default
        /// max mode; otherwise in [2, <see cref="MEAN_BEST_N_MAX"/>]. A value outside that range,
        /// or unrecognized, falls back to max (and sets <see cref="ExperimentAggUnrecognized"/>).
        ///
        /// Initialized from the environment at process start, exactly as before; a settable
        /// property only so unit tests can exercise the mean(best-N) arm at all. It is the SINGLE
        /// source of truth - <see cref="ExperimentAgg"/> and <see cref="ExperimentAggMeanBest"/>
        /// are computed from it rather than snapshotted, so a test that pins N cannot leave the
        /// three disagreeing (the failure mode that made <c>MeanBestFloorOverspecified</c> a
        /// computed property too). Nothing in the pipeline writes it.</summary>
        public static int MeanBestN { get; set; } = ParseMeanBestN(
            Environment.GetEnvironmentVariable(@"OSPREY_EXPERIMENT_AGG"));

        /// <summary>OSPREY_EXPERIMENT_AGG: how the 1st-pass EXPERIMENT-wide precursor/peptide
        /// score aggregates a unit's per-run observations before the target/decoy competition.
        /// <see cref="EXPERIMENT_AGG_MAX"/> (default, byte-identical golden) or
        /// <see cref="EXPERIMENT_AGG_MEAN_BEST_PREFIX"/>&lt;N&gt;. The normalized spelling of
        /// <see cref="MeanBestN"/> - this is the form persisted as pass-1 provenance and appended
        /// to the cache validity key, so it must never disagree with the N actually used.</summary>
        public static string ExperimentAgg =>
            MeanBestN >= 2
                ? EXPERIMENT_AGG_MEAN_BEST_PREFIX +
                  MeanBestN.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : EXPERIMENT_AGG_MAX;

        /// <summary>True when <see cref="MeanBestN"/> selects a mean(best-N) reproducibility score
        /// (OSPREY_EXPERIMENT_AGG=mean-best-N, N&gt;=2).</summary>
        public static bool ExperimentAggMeanBest => MeanBestN >= 2;

        /// <summary>True when OSPREY_EXPERIMENT_AGG was set to something that is neither
        /// <see cref="EXPERIMENT_AGG_MAX"/> nor a well-formed
        /// <see cref="EXPERIMENT_AGG_MEAN_BEST_PREFIX"/>&lt;N&gt; with N&gt;=2, and was therefore
        /// normalized to the max default. The consuming site logs a one-line warning, mirroring
        /// <see cref="Pass2QValueUnrecognized"/>. This matters more here than for most flags: the
        /// whole point of this one is A/B measurement, so a typo (mean-best-1, meanbest2) that
        /// silently ran the DEFAULT would be recorded by the operator as a mean(best-N) result and
        /// would corrupt the comparison rather than fail it.</summary>
        public static readonly bool ExperimentAggUnrecognized = IsUnrecognizedExperimentAgg(
            Environment.GetEnvironmentVariable(@"OSPREY_EXPERIMENT_AGG"));

        /// <summary>OSPREY_MEANBEST2_FLOOR_MEAN: A/B toggle to use the decoy MEAN instead of the
        /// default decoy MEDIAN as the missing-run floor. Off by default. Applies at every N
        /// despite the MEANBEST2 name, which predates the best-2 -> best-N generalization.
        /// TAKES PRECEDENCE over <see cref="MeanBest2FloorPercentile"/>; setting both is a
        /// configuration error and is refused rather than silently resolved.
        /// A settable property (not a readonly field) so unit tests can pin the floor instead of
        /// inheriting whatever the operator exported for a floor sweep - the aggregation tests
        /// assert exact floor-dependent values, so an ambient variable would fail them.</summary>
        public static bool MeanBest2FloorMean { get; set; } =
            IsSetAndNotZero(@"OSPREY_MEANBEST2_FLOOR_MEAN");

        /// <summary>OSPREY_MEANBEST2_FLOOR_PCT: A/B override to use a low PERCENTILE (0-100) of the
        /// decoy score distribution as the missing-run floor instead of the median center - a
        /// harder reproducibility cut. Null (unset) selects the median default. Applies at every N
        /// (see the MEANBEST2 naming note above). Settable for the same test reason.</summary>
        public static double? MeanBest2FloorPercentile { get; set; } =
            ParseDoubleOrNull(@"OSPREY_MEANBEST2_FLOOR_PCT");

        /// <summary>True when BOTH floor overrides are set. They are not composable -
        /// <see cref="MeanBest2FloorMean"/> would silently win and the percentile would never be
        /// consulted, so an operator sweeping OSPREY_MEANBEST2_FLOOR_PCT with a stale
        /// OSPREY_MEANBEST2_FLOOR_MEAN=1 still exported would log a percentile arm while measuring
        /// the mean. The consuming site refuses the combination.</summary>
        /// Computed, not snapshotted: the two toggles above are settable properties (so tests can
        /// pin the floor), and a readonly field capturing the raw environment at type load would
        /// disagree with them in both directions.
        public static bool MeanBestFloorOverspecified =>
            MeanBest2FloorMean && MeanBest2FloorPercentile.HasValue;

        /// <summary>
        /// One line naming the experiment-wide aggregation this process will actually use,
        /// including the missing-run floor arm. Logged unconditionally in the startup settings
        /// block - a POSITIVE statement, not only a warning - because the dominant real failure
        /// of an environment-variable flag is that the variable never reached the process
        /// (Start-Process without the parent environment, an HPC job spec, a scheduled launch,
        /// a service account). Unset is indistinguishable from a typo'd-then-normalized value in
        /// the output itself, so without this line an operator records a DEFAULT run as a
        /// mean(best-N) result and the A/B is silently corrupted rather than failed. Mirrors the
        /// positive "mode active" line OSPREY_PASS2_QVALUE prints.
        /// </summary>
        public static string DescribeExperimentAgg()
        {
            if (!ExperimentAggMeanBest)
            {
                return string.Format(@"Experiment aggregation: {0} (default - best observation per unit)",
                    EXPERIMENT_AGG_MAX);
            }
            return string.Format(
                @"Experiment aggregation: {0} ACTIVE - experiment-wide precursor score is the mean " +
                @"of its best {1} per-run scores; missing-run floor = {2}",
                ExperimentAgg, MeanBestN, DescribeMeanBestFloor());
        }

        /// <summary>
        /// Validate the OSPREY_EXPERIMENT_AGG family against each other and against the run's
        /// file count. Returns null when the settings are usable, or an operator-actionable
        /// message when they are not. Shared by the startup check
        /// (<c>Program.ValidateArgs</c>, before any I/O) and the Stage-5 consuming site, so a
        /// resumed or single-task run that skips one still hits the other and both say the same
        /// thing. Every check is gated on the aggregation being ENGAGED: with
        /// OSPREY_EXPERIMENT_AGG unset none of these variables is read, so refusing an ordinary
        /// run over a floor sweep the operator merely left exported would break the default path.
        /// </summary>
        /// <param name="fileCount">Number of runs (input files) this analysis will aggregate
        /// across, or 0 when the caller does not know it yet.</param>
        public static string ValidateExperimentAggSettings(int fileCount)
        {
            if (!ExperimentAggMeanBest)
                return null;
            if (MeanBestFloorOverspecified)
            {
                return @"OSPREY_MEANBEST2_FLOOR_MEAN and OSPREY_MEANBEST2_FLOOR_PCT are both set. " +
                       @"They are not composable: FLOOR_MEAN wins and the percentile is never " +
                       @"consulted, so a sweep would record a percentile arm while measuring the " +
                       @"decoy mean. Set exactly one.";
            }
            // Validated ONCE here rather than where the floor is computed: the streaming
            // estimator reaches its percentile only at the END of Stage 5, hours into a large
            // run, and the resident twin silently clamps the same value - so a mid-run refusal
            // would both come too late to be actionable and disagree with the other path.
            double? pct = MeanBest2FloorPercentile;
            if (pct.HasValue && (double.IsNaN(pct.Value) || pct.Value < 0.0 || pct.Value > 100.0))
            {
                return string.Format(
                    @"OSPREY_MEANBEST2_FLOOR_PCT={0} is outside the valid range [0, 100]. It names a " +
                    @"percentile of the decoy score distribution to use as the missing-run floor.",
                    pct.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            if (fileCount > 0 && MeanBestN > fileCount)
            {
                return string.Format(
                    @"OSPREY_EXPERIMENT_AGG={0} averages the best {1} per-run scores, but this " +
                    @"analysis has only {2} run(s). Every unit would be floor-filled for the " +
                    @"missing {3}, which saturates the statistic - for any N at or above the " +
                    @"largest observation count the ranking, and therefore every q-value, is " +
                    @"identical - so the arm would be recorded as distinct while measuring the " +
                    @"same thing. Use N <= {2}.",
                    ExperimentAgg, MeanBestN, fileCount, MeanBestN - fileCount);
            }
            return null;
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the experiment-wide
        /// aggregation, or the EMPTY string when the aggregation is not engaged.
        ///
        /// Empty by default and not merely "the default arm's value": <see cref="ExperimentAgg"/>
        /// normalizes an unset variable to the constant "max", so appending unconditionally would
        /// change the key of every DEFAULT run and invalidate every output directory produced
        /// before this suffix existed - re-running Stage 5 FDR, protein FDR and compaction (hours
        /// at 82 files) to reproduce byte-identical output. That exact regression shipped once and
        /// the byte-identity gate could not see it, because the gate always uses fresh directories.
        ///
        /// One helper rather than three copies: the suffix must be IDENTICAL across
        /// <c>FirstPassFdrTask</c>, <c>PerFileRescoreTask</c> and <c>SecondPassFdrTask</c>. When only the
        /// first had it, a flipped arm re-ran Stage 5 while the downstream reconciled parquets,
        /// 2nd-pass sidecars and .blib were reused from the OTHER arm - a self-inconsistent output
        /// set that no single task's key could have caught.
        /// </summary>
        public static string ExperimentAggValidityKeySuffix()
        {
            if (!ExperimentAggMeanBest)
                return string.Empty;
            return @";expagg=" + ExperimentAgg
                   + @";floormean=" + MeanBest2FloorMean
                   + @";floorpct=" + (MeanBest2FloorPercentile?.ToString(
                       System.Globalization.CultureInfo.InvariantCulture) ?? @"none");
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the peak-pick
        /// model - which is every task from per-file scoring onward, because the pick decides
        /// WHICH peak each precursor's row describes.
        ///
        /// UNCONDITIONAL, unlike <see cref="ExperimentAggValidityKeySuffix"/>, and that
        /// difference is the point. The aggregation suffix is empty for its default arm because
        /// that arm's output never changed; this arm's DID. The default moved from the
        /// product-form pick to the learned model (<see cref="PickLda"/>), so "emits nothing"
        /// already describes every output directory written before the flip. Emitting nothing
        /// for the new default too would make a post-flip key EQUAL a pre-flip key, and the
        /// resume driver would adopt product-form parquets as though the learned model had
        /// produced them.
        ///
        /// The one-time cost is real: every output directory written before this shipped is
        /// invalidated, so a warm re-run or a <c>-LinkFrom</c> adoption re-runs Stage 1-4. That
        /// is the correct outcome, not a regression - those artifacts were picked by a
        /// different model, and reusing them reports one model's peaks under the other's name.
        ///
        /// The arm can also be passed explicitly, because the environment is read once into a
        /// static and cannot be flipped at run time - a test would otherwise be able to assert
        /// only whichever arm the test process happens to be running under.
        /// </summary>
        public static string PickValidityKeySuffix()
        {
            return PickValidityKeySuffix(PickLda, PickLdaModelPath);
        }

        /// <summary>
        /// <see cref="PickValidityKeySuffix()"/> for an explicitly supplied arm. The model PATH
        /// participates as well as the on/off flag: <see cref="PickLdaModelPath"/> overrides the
        /// resolution-keyed default outright, so two runs can differ in nothing else.
        /// </summary>
        public static string PickValidityKeySuffix(bool pickLda, string pickModelPath)
        {
            return @";pick=" + (pickLda ? @"lda" : @"product")
                   + @";pickmodel=" + (string.IsNullOrEmpty(pickModelPath) ? @"none" : pickModelPath);
        }

        /// <summary>
        /// The cache-invalidation suffix for any task whose output depends on the 2nd-pass
        /// q-value mode. UNCONDITIONAL for the same reason as
        /// <see cref="PickValidityKeySuffix()"/>: the default moved from the removed
        /// <c>percolator</c> retrain to <see cref="PASS2_QVALUE_PROTEIN_COMPACT"/>, so an empty
        /// suffix would let a run adopt the other mode's cached q-values.
        ///
        /// This is the tagging the <see cref="Pass2QValue"/> remarks used to defer, and it
        /// retires the "use a FRESH --output-dir per mode" limitation they carried.
        /// </summary>
        public static string Pass2QValueValidityKeySuffix()
        {
            return Pass2QValueValidityKeySuffix(Pass2QValue);
        }

        /// <summary>
        /// <see cref="Pass2QValueValidityKeySuffix()"/> for an explicitly supplied mode, in the
        /// normalized form <see cref="Pass2QValue"/> holds.
        /// </summary>
        public static string Pass2QValueValidityKeySuffix(string normalizedPass2QValue)
        {
            return @";pass2=" + normalizedPass2QValue;
        }

        /// <summary>
        /// True when <paramref name="normalizedAgg"/> names a mean(best-N) arm. Takes the arm as
        /// an ARGUMENT rather than reading <see cref="ExperimentAggMeanBest"/> so a consumer can
        /// ask about an arm RECORDED BY ANOTHER PROCESS - a distributed <c>--task SecondPassFDR</c>
        /// SecondPassFDR node reloads the 1st-pass model from disk and must gate on the arm that trained
        /// it, not on its own environment. Expects the normalized form
        /// (<see cref="ExperimentAgg"/>) as persisted alongside the model.
        /// </summary>
        public static bool IsMeanBestArm(string normalizedAgg)
        {
            return !string.IsNullOrEmpty(normalizedAgg) &&
                   normalizedAgg.StartsWith(EXPERIMENT_AGG_MEAN_BEST_PREFIX, StringComparison.Ordinal);
        }

        /// <summary>The missing-run floor arm in words, for
        /// <see cref="DescribeExperimentAgg"/>.</summary>
        private static string DescribeMeanBestFloor()
        {
            if (MeanBest2FloorMean)
                return @"decoy MEAN (OSPREY_MEANBEST2_FLOOR_MEAN)";
            double? pct = MeanBest2FloorPercentile;
            if (pct.HasValue)
            {
                return string.Format(@"decoy {0} percentile (OSPREY_MEANBEST2_FLOOR_PCT)",
                    pct.Value.ToString(@"0.###", System.Globalization.CultureInfo.InvariantCulture));
            }
            return @"decoy MEDIAN (default)";
        }

        // Parse N from OSPREY_EXPERIMENT_AGG=mean-best-<N>. Returns 0 (the max default) when unset,
        // not a mean-best-<N> value, or N outside [2, MEAN_BEST_N_MAX] - and 0 makes
        // IsUnrecognizedExperimentAgg true, so an out-of-range N warns rather than silently
        // running the default.
        private static int ParseMeanBestN(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return 0;
            string v = raw.Trim().ToLowerInvariant();
            if (!v.StartsWith(EXPERIMENT_AGG_MEAN_BEST_PREFIX, StringComparison.Ordinal))
                return 0;
            string tail = v.Substring(EXPERIMENT_AGG_MEAN_BEST_PREFIX.Length);
            // NumberStyles.None, not Integer: Integer also accepts a leading sign and surrounding
            // whitespace, so "mean-best-+3" and "mean-best- 3" parsed as 3 - two spellings of the
            // same arm that an A/B log could not tell apart from the canonical one.
            return int.TryParse(tail, System.Globalization.NumberStyles.None,
                       System.Globalization.CultureInfo.InvariantCulture, out int n) &&
                   n >= 2 && n <= MEAN_BEST_N_MAX
                ? n
                : 0;
        }

        // A set-but-unusable OSPREY_EXPERIMENT_AGG: anything that is neither the max default nor a
        // well-formed mean-best-<N> with N >= 2. Unset / whitespace is NOT unrecognized - that is
        // simply the default. Mirrors IsUnrecognizedPass2QValue.
        private static bool IsUnrecognizedExperimentAgg(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            return !string.Equals(raw.Trim(), EXPERIMENT_AGG_MAX, StringComparison.OrdinalIgnoreCase) &&
                   ParseMeanBestN(raw) == 0;
        }

        private static string NormalizePass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return PASS2_QVALUE_PROTEIN_COMPACT;
            string v = raw.Trim().ToLowerInvariant();
            if (v == PASS2_QVALUE_TRANSFER)
                return PASS2_QVALUE_TRANSFER;
            if (v == PASS2_QVALUE_TRANSFER_COMPETE)
                return PASS2_QVALUE_TRANSFER_COMPETE;
            if (v == PASS2_QVALUE_PROTEIN_COMPACT)
                return PASS2_QVALUE_PROTEIN_COMPACT;
            // An unrecognized token normalizes to the default only so the other statics are
            // well-formed; the run does not get here, because Program aborts at startup on
            // IsUnrecognizedPass2QValue.
            return PASS2_QVALUE_PROTEIN_COMPACT;
        }

        private static bool IsUnrecognizedPass2QValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return false;
            string v = raw.Trim().ToLowerInvariant();
            return v != PASS2_QVALUE_TRANSFER &&
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
            return int.TryParse(v, out int result) ? result : null;
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
                ? result : null;
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
