/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// RT/mass calibration subsystem extracted from
    /// <see cref="PerFileScoringTask"/>. Runs the two-pass calibration
    /// discovery (sample library -> score windows -> LDA + FDR -> S/N filter
    /// -> LOESS fit) and returns the RT calibration plus MS1/MS2 mass
    /// calibrations for one file. Ports osprey/src/pipeline.rs
    /// run_calibration_discovery_windowed.
    ///
    /// Standalone collaborator (does not inherit AbstractScoringTask): it owns
    /// the calibration unit-resolution XCorr scorer (<see cref="s_calXcorrScorer"/>),
    /// reaches the shared top-N constant via <see cref="TopFragmentExtractor"/>,
    /// and takes the pipeline context for logging. Diagnostic dumps route
    /// through the injected <c>_ctx.Diagnostics</c> sink (the *_ONLY abort uses
    /// <c>OspreyDiagnosticsLog.ExitAfterDump</c>), preserving the Stage-cal dump
    /// call order bisection relies on.
    /// </summary>
    internal sealed class Calibrator
    {
        private const double MIN_SNR_FOR_RT_CAL = 5.0;
        private const double MIN_COELUTION_CORR_SCORE = 0.5;
        private const int MIN_COELUTION_SPECTRA = 3;
        private const double CAL_FDR_THRESHOLD = 0.01;
        // Hard floor for LOESS refit in the two-pass calibration
        // refinement. Matches Rust's ABSOLUTE_MIN_CALIBRATION_POINTS
        // in pipeline.rs:652.
        private const int ABSOLUTE_MIN_CALIBRATION_POINTS = 50;
        // Below this many calibration points a LOESS local window (Bandwidth * n)
        // holds fewer than ~30 points, too few to support a locally varying fit, so
        // a single global line is the better-conditioned estimator. See
        // SelectFitPlan and issue #4401.
        private const int LINEAR_FIT_MAX_POINTS = 100;
        // Fewest confident peptides that can still support a robust line. A line has
        // two degrees of freedom, and Theil-Sen needs enough pairs for its median to
        // mean anything; below this we would be extrapolating a gradient from noise.
        // Replaces ABSOLUTE_MIN_CALIBRATION_POINTS as the *fit* floor: a fit on a few
        // dozen confident peptides beats searching at the raw library RT (#4401).
        private const int MIN_LINEAR_FIT_POINTS = 15;
        // A line is only identifiable if its points have leverage. Twenty peptides
        // spread across the gradient determine a slope; twenty bunched into one
        // region do not, however confident each one is.
        private const double MIN_LINEAR_FIT_RT_SPAN_FRACTION = 0.5;
        // A fitted slope this far from the range-derived mapping means the fit has
        // gone somewhere the data cannot justify; fall back rather than trust it.
        private const double MAX_LINEAR_FIT_SLOPE_RATIO = 2.0;

        // Calibration XCorr always uses unit-resolution bins (~2K) regardless of
        // instrument resolution mode. Matches the spec in Rust osprey
        // docs/02-calibration.md ("Comet-style XCorr (unit resolution, BLAS
        // sdot)") and the calibration_xcorr_scorer helper in
        // osprey/crates/osprey/src/pipeline.rs, and avoids the LOH allocation
        // pressure that 100K-bin arrays cause on .NET Framework's large-object
        // heap. Main search XCorr still uses the resolution-mode bins via the
        // IResolutionStrategy abstraction. Owned by the calibration subsystem
        // (its sole consumer); exposed as internal so Osprey.Test can
        // assert the bin-config invariant.
        internal static readonly SpectralScorer s_calXcorrScorer =
            new SpectralScorer(BinConfig.UnitResolution());

        /// <summary>
        /// Result of one calibration scoring pass (scoring + LDA +
        /// S/N filter + LOESS fit).
        /// </summary>
        private class CalibrationPassResult
        {
            public RTCalibration Calibration;
            public RTCalibrationStats Stats;
            public MzCalibrationResult Ms1Calibration;
            public MzCalibrationResult Ms2Calibration;
            // (lib_rt, measured_rt) pairs that were actually fed to the LOESS
            // fit for this pass. Exposed so the caller can emit the
            // OSPREY_DUMP_LOESS_INPUT diagnostic only for the pass whose
            // calibration is actually used (pass 1 always; pass 2 only on
            // acceptance) -- mirroring Rust pipeline.rs's "dump reflects
            // the calibration actually used" semantics.
            public double[] LibRts;
            public double[] MeasuredRts;
        }

        /// <summary>
        /// One library entry's best calibration match across retry attempts,
        /// bundled with the S/N and (libRt, measuredRt) captured for THAT match.
        /// Rust carries these on <c>CalibrationMatch</c> itself
        /// (osprey-scoring/src/batch.rs:859-920); C# keeps them in side maps, so
        /// they must travel together whenever a later attempt supplies a better
        /// match for the same entry.
        /// Internal so Osprey.Test can exercise the accumulation rule.
        /// </summary>
        internal class AccumulatedMatch
        {
            public CalibrationMatch Match;
            public double Snr;
            public double LibRt;
            public double MeasuredRt;
        }

        /// <summary>What the retry ladder should do after scoring one attempt.</summary>
        internal enum CalibrationLadderAction
        {
            /// <summary>Too few confident peptides, attempts remain: grow the sample.</summary>
            Retry,
            /// <summary>Enough points, or final attempt at/above the absolute floor: fit LOESS.</summary>
            Fit,
            /// <summary>Final attempt below the absolute floor: degrade to fallback tolerances.</summary>
            Fallback,
        }

        /// <summary>Which curve to fit for a given number of calibration points.</summary>
        internal struct CalibrationFitPlan
        {
            /// <summary>Fit a global robust (Theil-Sen) line instead of a LOESS curve.</summary>
            public bool LinearFit;
            /// <summary>LOESS bandwidth to use when <see cref="LinearFit"/> is false.</summary>
            public double Bandwidth;
        }

        /// <summary>Result of <see cref="DecideLadderAction"/> for a single attempt.</summary>
        internal struct CalibrationLadderDecision
        {
            public CalibrationLadderAction Action;
            /// <summary>Sample size for the next attempt when Action == Retry (0 = all targets).</summary>
            public int NextSampleSize;
            /// <summary>The LOESS MinPoints guard when Action == Fit.</summary>
            public int EffectiveMinPoints;
            /// <summary>True when Fit accepted a count below MinCalibrationPoints but at/above the floor.</summary>
            public bool BelowTarget;
        }

        private readonly PipelineContext _ctx;

        // Full-library entrapment composition for the --verbose anchor-purity
        // (entrapment-FDP) diagnostic. Computed once per file in RunCalibration, only
        // when Verbose. _libTargetSideCount is all IsDecoy==false entries (real Target +
        // entrapment PTarget); _libEntrapmentCount is the PTarget subset. The FDRBench
        // ratio is r = entrapment / (target-side - entrapment). Zero on a plain library.
        private long _libTargetSideCount;
        private long _libEntrapmentCount;

        internal Calibrator(PipelineContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>
        /// Run RT calibration using calibration discovery scoring.
        /// Ports osprey/src/pipeline.rs run_calibration_discovery_windowed:
        ///   1. Sample target + paired decoy library entries (stratified by RT/m/z).
        ///   2. For each sample, extract fragment XICs, detect the best co-eluting
        ///      peak, then compute 4 features at the apex: mean pairwise correlation,
        ///      LibCosine, top-6 matched, and XCorr.
        ///   3. Train LDA with non-negative weights + 1% FDR target-decoy competition.
        ///   4. Apply S/N >= 5.0 quality filter on surviving targets.
        ///   5. Fit LOESS on the (libRt, measuredRt) pairs.
        /// </summary>
        public RTCalibration RunCalibration(
            List<LibraryEntry> library,
            List<Spectrum> spectra,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            out MzCalibrationResult ms1Calibration,
            out MzCalibrationResult ms2Calibration,
            out int numSampledPrecursors,
            out double initialRtTolerance)
        {
            var config = context.Config;
            // Default to 0 so early returns / exception paths leave the
            // metadata caller in a known state. Overwritten on success.
            numSampledPrecursors = 0;
            // The wide pre-calibration RT tolerance (the "before" number in the
            // console summary's before-vs-after RT window). Defaults to 0 to keep the
            // out-parameter assigned on any early exit before the initial tolerance is
            // computed below; every return path that runs past that point carries the
            // computed value (the no-target return happens after it is set, and the
            // caller ignores it then since RunCalibration returns null and emits no
            // summary).
            initialRtTolerance = 0.0;
            _ctx.LogInfo("Running RT calibration...");

            // Calculate library and mzML RT ranges
            double libMinRt = double.MaxValue, libMaxRt = double.MinValue;
            double mzmlMinRt = double.MaxValue, mzmlMaxRt = double.MinValue;

            // Target count drives the retry ladder's max_attempts derivation below
            // (Rust pipeline.rs:708-714).
            int nTotalTargets = 0;
            // --verbose anchor-purity diagnostic: tally the full-library entrapment
            // composition once, so RunLdaAndCollectPoints can report the entrapment-FDP of
            // the selected anchors against the FDRBench ratio r. Verbose-gated so a normal
            // run pays nothing for the per-entry accession scan.
            bool tallyEntrapment = OspreyOutput.Verbose;
            _libTargetSideCount = 0;
            _libEntrapmentCount = 0;
            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    nTotalTargets++;
                    if (tallyEntrapment)
                    {
                        _libTargetSideCount++;
                        if (EntrapmentLibraryClassifier.IsEntrapment(entry.ProteinIds))
                            _libEntrapmentCount++;
                    }
                    if (entry.RetentionTime < libMinRt)
                        libMinRt = entry.RetentionTime;
                    if (entry.RetentionTime > libMaxRt)
                        libMaxRt = entry.RetentionTime;
                }
            }
            if (tallyEntrapment && _libEntrapmentCount > 0)
            {
                long realTargets = _libTargetSideCount - _libEntrapmentCount;
                double rLib = realTargets > 0 ? (double)_libEntrapmentCount / realTargets : 0.0;
                _ctx.LogVerbose(string.Format(
                    "Calibration entrapment library: {0} target-side entries = {1} real targets + {2} entrapment (FDRBench r = {3:F3})",
                    _libTargetSideCount, realTargets, _libEntrapmentCount, rLib));
            }

            foreach (var spectrum in spectra)
            {
                if (spectrum.RetentionTime < mzmlMinRt)
                    mzmlMinRt = spectrum.RetentionTime;
                if (spectrum.RetentionTime > mzmlMaxRt)
                    mzmlMaxRt = spectrum.RetentionTime;
            }

            double libRtRange = libMaxRt - libMinRt;
            double mzmlRtRange = mzmlMaxRt - mzmlMinRt;

            _ctx.LogVerbose(string.Format(
                "Library RT range: {0:F1}-{1:F1}, mzML RT range: {2:F1}-{3:F1} min",
                libMinRt, libMaxRt, mzmlMinRt, mzmlMaxRt));

            // Linear RT mapping when library and mzML scales differ significantly.
            bool rangesSimilar = libRtRange > 0 && mzmlRtRange > 0 &&
                Math.Max(libRtRange / mzmlRtRange, mzmlRtRange / libRtRange) < 2.0 &&
                Math.Abs(libMinRt - mzmlMinRt) < libRtRange * 0.5;

            double rtSlope = 1.0;
            double rtIntercept = 0.0;
            if (!rangesSimilar && libRtRange > 0)
            {
                rtSlope = mzmlRtRange / libRtRange;
                rtIntercept = mzmlMinRt - rtSlope * libMinRt;
                _ctx.LogInfo(string.Format("RT mapping: slope={0:F4}, intercept={1:F4}",
                    rtSlope, rtIntercept));
            }

            double toleranceFraction = rangesSimilar ? 0.2 : 0.5;
            double initialTolerance = mzmlRtRange * toleranceFraction;
            initialRtTolerance = initialTolerance;

            _ctx.LogVerbose(string.Format("Initial RT tolerance: {0:F1} min", initialTolerance));

            // Available to the caller on every failure path below, so a file that ends
            // up uncalibrated still centres its search window on the library/mzML range
            // mapping rather than the raw library RT (issue #4401). Identity, hence a
            // no-op, when the two RT scales already agree.
            context.FallbackRtMap = RTCalibration.FromLinearMapping(
                libMinRt, libMaxRt, rtSlope, rtIntercept);

            // Group spectra by isolation window center for O(1) window lookup per candidate.
            var spectraByWindowKey = new Dictionary<int, List<Spectrum>>();
            foreach (var spectrum in spectra)
            {
                int key = (int)Math.Round(spectrum.IsolationWindow.Center * 10.0);
                List<Spectrum> list;
                if (!spectraByWindowKey.TryGetValue(key, out list))
                {
                    list = new List<Spectrum>();
                    spectraByWindowKey[key] = list;
                }
                list.Add(spectrum);
            }
            // Sort each window's spectra by RT for deterministic XIC extraction.
            // Array.Sort OK: RT tie hazard, conversion deferred (not a #4362 approved
            // U-site; the dict lists are re-sorted in place and reused across both
            // calibration passes, so an in-loop OrderBy reassignment is awkward). Two
            // spectra within one window sharing an identical RT would be rare; RT values
            // are per-cycle sampling times. Mirror of the CoelutionScorer RT sort.
            foreach (var list in spectraByWindowKey.Values)
                list.Sort((a, b) => a.RetentionTime.CompareTo(b.RetentionTime)); // Array.Sort OK: (see above) RT tie hazard, conversion deferred; not a #4362 approved U-site

            // Pre-preprocess every window's spectra for XCorr once. The result is a
            // pure function of the spectra, so it is hoisted out of the attempt and
            // pass loops (it used to be rebuilt inside every scoring pass).
            var preprocessedByWindowKey = PreprocessWindowsForXcorr(spectraByWindowKey);

            // === Calibration retry ladder (Rust pipeline.rs:700-1271) ===
            // Attempt 1 samples CalibrationSampleSize targets. On shortfall the sample
            // grows by CalibrationRetryFactor, and the FINAL attempt uses ALL library
            // targets. Matches accumulate across attempts (best per entry) and LDA is
            // retrained on the accumulated set each attempt. Before this loop existed,
            // C# ran attempt 1 only, so any file whose confident-peptide count landed
            // in [ABSOLUTE_MIN_CALIBRATION_POINTS, MinCalibrationPoints) silently fell
            // back to uncalibrated tolerances where Rust fitted a LOESS -- issue #4401.
            int sampleSize = config.RtCalibration.CalibrationSampleSize;
            // OSPREY_CAL_SAMPLE_SIZE experimental override: the default ladder samples 100K
            // and stops once >= MinCalibrationPoints (200) clear -- a MINIMAL sufficient
            // calibration. On a rich file the true peptides are a small fraction of a large
            // library (e.g. ~34K present in a 3.17M-entry library), so a 100K random sample
            // holds only ~1K present peptides and yields only a few hundred anchors. This
            // override lets us test whether a larger sample surfaces proportionally more
            // near-zero-FDR anchors (0 = use the config default, no change).
            if (OspreyEnvironment.CalSampleSizeOverride > 0)
                sampleSize = OspreyEnvironment.CalSampleSizeOverride;
            double retryFactor = config.RtCalibration.CalibrationRetryFactor;
            int maxAttempts = ComputeMaxAttempts(sampleSize, retryFactor, nTotalTargets);
            int currentSampleSize = sampleSize;

            _ctx.LogVerbose(string.Format(
                "Calibration: library has {0} targets, requesting {1} per attempt ({2} attempt(s) max)",
                nTotalTargets,
                currentSampleSize == 0 || nTotalTargets <= currentSampleSize
                    ? "all"
                    : string.Format("{0}", currentSampleSize),
                maxAttempts));

            // Best match per library entry, accumulated across attempts. Mirrors
            // Rust's accumulated_matches (pipeline.rs:730).
            var accumulated = new Dictionary<uint, AccumulatedMatch>();

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // Sample library entries (paired target+decoy). Seed 42 + attempt
                // matches Rust's sample_library_for_calibration (pipeline.rs:752).
                var swSample = Stopwatch.StartNew();
                var sampledEntries = SampleLibraryForCalibration(
                    library, currentSampleSize, (ulong)(42 + attempt), _ctx.Diagnostics);
                swSample.Stop();
                bool usedAll = sampledEntries.Count == library.Count;

                // Diagnostic: dump sorted sampled entry IDs + (modseq, charge) for
                // direct comparison with Rust. Abort after dump if CAL_SAMPLE_ONLY
                // env var is set (bisection mode - stop once we agree here). Rust
                // dumps per attempt too; attempt 1 is the comparable one, and the
                // *_ONLY abort fires there.
                if (config.WritePin || (_ctx.Diagnostics?.DumpCalSample ?? false))
                {
                    _ctx.Diagnostics?.WriteCalSampleDump(context.FileName, sampledEntries);
                    if (_ctx.Diagnostics?.CalSampleOnly ?? false)
                        OspreyDiagnosticsLog.ExitAfterDump("OSPREY_CAL_SAMPLE_ONLY");
                }
                int nSampledTargets = 0;
                int nSampledDecoys = 0;
                foreach (var e in sampledEntries)
                {
                    if (e.IsDecoy)
                        nSampledDecoys++;
                    else nSampledTargets++;
                }
                _ctx.LogInfo(string.Format(
                    "[TIMING] Calibration sampling (attempt {0}/{1}): {2:F2}s ({3} targets + {4} decoys)",
                    attempt, maxAttempts, swSample.Elapsed.TotalSeconds, nSampledTargets, nSampledDecoys));

                if (nSampledTargets == 0)
                {
                    _ctx.LogWarning("No target entries available for calibration sampling.");
                    ms1Calibration = MzCalibrationResult.Uncalibrated();
                    ms2Calibration = MzCalibrationResult.Uncalibrated();
                    return null;
                }

                // Score this attempt's sample with the linear pre-fit RT mapping and
                // the wide initial tolerance (pass-1 semantics).
                var (matches, snrByEntryId, matchRts) = ScoreCalibrationMatches(
                    1, sampledEntries, spectraByWindowKey, preprocessedByWindowKey,
                    ms1Spectra, context, rtSlope, rtIntercept, initialTolerance,
                    null /* calibrationModel: pass 1 uses linear mapping */);

                EmitScoringDumps(1, sampledEntries, matches, matchRts, snrByEntryId);

                // Merge into the cross-attempt accumulator, keeping the better match
                // per entry (Rust pipeline.rs:812-825).
                MergeCalibrationMatches(accumulated, matches, snrByEntryId, matchRts);

                int nConfident = 0;
                CalibrationMatch[] matchArray = null;
                List<double> libRtsDetected = null;
                List<double> measuredRtsDetected = null;

                if (accumulated.Count == 0)
                {
                    _ctx.LogWarning("No calibration matches could be scored in pass 1.");
                }
                else
                {
                    // LDA + FDR + S/N run on the ACCUMULATED set, retrained each
                    // attempt (Rust pipeline.rs:870-876).
                    matchArray = BuildSortedMatchArray(accumulated);
                    RunLdaAndCollectPoints(
                        1, matchArray, accumulated, context.FileName,
                        out libRtsDetected, out measuredRtsDetected);
                    nConfident = libRtsDetected.Count;
                }

                var decision = DecideLadderAction(
                    attempt, maxAttempts, usedAll, nConfident,
                    config.RtCalibration.MinCalibrationPoints,
                    currentSampleSize, retryFactor, nTotalTargets);

                if (decision.Action == CalibrationLadderAction.Retry)
                {
                    currentSampleSize = decision.NextSampleSize;
                    _ctx.LogWarning(string.Format(
                        "Calibration attempt {0} found only {1} confident peptides (need {2}). Retrying with {3} targets...",
                        attempt, nConfident, config.RtCalibration.MinCalibrationPoints,
                        currentSampleSize == 0 ? "ALL" : string.Format("{0}", currentSampleSize)));
                    continue;
                }

                // matchArray is null only when nothing matched at all. Guard the Fit
                // path against it: a degenerate MinCalibrationPoints of 0 would
                // otherwise satisfy the ladder's >= test and fit an empty point set.
                if (decision.Action == CalibrationLadderAction.Fallback || matchArray == null)
                {
                    // Below the absolute floor the file searches with fallback
                    // tolerances. Rust reaches the same outcome by a different route:
                    // it returns OspreyError::ConfigError (pipeline.rs:1039), which its
                    // caller catches (pipeline.rs:4278) to log "Calibration failed ...
                    // Using fallback tolerance" and carry on with an uncalibrated file.
                    // Neither implementation aborts the batch. The failure is loud here,
                    // and the caller records calibration_successful=false in the emitted
                    // .calibration.json (PerFileScoringTask derives it from a null
                    // return). See issue #4401 and osprey docs/02-calibration.md.
                    _ctx.LogWarning(string.Format(
                        "Insufficient calibration points after {0} attempt(s): {1} < {2} minimum for a fit.",
                        attempt, nConfident, MIN_LINEAR_FIT_POINTS));
                    _ctx.LogWarning("Calibration pass 1 failed. Using fallback tolerance.");
                    ms1Calibration = MzCalibrationResult.Uncalibrated();
                    ms2Calibration = MzCalibrationResult.Uncalibrated();
                    return null;
                }

                if (decision.BelowTarget)
                {
                    // Final attempt, at or above the fit floor: fit anyway
                    // (Rust pipeline.rs:1028-1036).
                    _ctx.LogWarning(string.Format(
                        "Calibration: Using {0} peptides (below target of {1} but above minimum of {2})",
                        nConfident, config.RtCalibration.MinCalibrationPoints,
                        MIN_LINEAR_FIT_POINTS));
                }

                // A line is only identifiable if its points have leverage. Check the
                // span BEFORE fitting: a tight cluster of confident peptides cannot
                // determine a gradient, however confident each one is (#4401).
                var fitPlan = SelectFitPlan(
                    nConfident, config.RtCalibration.LoessBandwidth,
                    config.RtCalibration.MinCalibrationPoints);
                if (fitPlan.LinearFit &&
                    !HasSufficientRtSpan(libRtsDetected, libMinRt, libMaxRt))
                {
                    _ctx.LogWarning(string.Format(
                        "Calibration: {0} points span too little of the library RT range " +
                        "to determine a slope. Using fallback tolerance.", nConfident));
                    ms1Calibration = MzCalibrationResult.Uncalibrated();
                    ms2Calibration = MzCalibrationResult.Uncalibrated();
                    return null;
                }

                var pass1 = FitCalibrationPass(
                    1, matchArray, accumulated, libRtsDetected, measuredRtsDetected,
                    config, decision.EffectiveMinPoints);

                // A thin fit that lands somewhere the range mapping cannot justify is
                // worse than no fit: it would re-centre every search window on a wrong
                // gradient. Reject it rather than trust it (#4401).
                if (pass1 != null &&
                    pass1.Calibration.Method == RTCalibrationMethod.Linear &&
                    !IsPlausibleLinearFit(pass1.Calibration, rtSlope, mzmlMinRt, mzmlMaxRt))
                {
                    _ctx.LogWarning(
                        "Calibration: the linear fit disagrees with the library/mzML RT " +
                        "range mapping. Using fallback tolerance.");
                    pass1 = null;
                }

                if (pass1 == null)
                {
                    _ctx.LogWarning("Calibration pass 1 failed. Using fallback tolerance.");
                    ms1Calibration = MzCalibrationResult.Uncalibrated();
                    ms2Calibration = MzCalibrationResult.Uncalibrated();
                    return null;
                }

                // Pass 1 succeeded -- emit the OSPREY_DUMP_LOESS_INPUT diagnostic
                // for the pass-1 fit unconditionally. If pass 2 is later accepted
                // it will overwrite this with the pass-2 pairs; if pass 2 is
                // rejected (or never runs) the pass-1 dump stays, matching the
                // calibration actually used. Mirrors Rust pipeline.rs.
                if (_ctx.Diagnostics?.DumpLoessInput ?? false)
                {
                    _ctx.Diagnostics?.WriteLoessInputDump(1, pass1.LibRts, pass1.MeasuredRts);
                    if (_ctx.Diagnostics?.LoessInputOnly ?? false)
                        OspreyDiagnosticsLog.ExitAfterDump("OSPREY_LOESS_INPUT_ONLY");
                }

                // Rust reports accumulated_matches.len() (pipeline.rs:1250): the
                // unique entries matched across every attempt, before any q-value or
                // S/N filtering. On a single-attempt run this equals the old
                // pass1.MatchCount.
                numSampledPrecursors = accumulated.Count;

                // === Iterative calibration refinement (2-pass) ===
                // Mirrors Rust pipeline.rs:1068-1238. Note this is the INNER loop:
                // it narrows the RT tolerance against the current attempt's sample,
                // and is orthogonal to the outer retry ladder above.
                // MAD * 1.4826 ~ SD for a normal distribution; 3* that covers ~99.7%.
                double madTolerance = pass1.Stats.MAD * 1.4826 * 3.0;
                // The floor widens for a thin fit, so a MAD that came out small by
                // luck cannot buy a window the fit does not support (#4401).
                double pass1MinTolerance = RTCalibration.EffectiveMinRtTolerance(
                    pass1.Stats.NPoints,
                    config.RtCalibration.MinRtTolerance,
                    config.RtCalibration.MaxRtTolerance,
                    config.RtCalibration.MinCalibrationPoints);
                double pass1Tolerance = Math.Max(
                    pass1MinTolerance,
                    Math.Min(config.RtCalibration.MaxRtTolerance, madTolerance));

                _ctx.LogVerbose(string.Format(
                    "First-pass RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4}, min tolerance {6:F2})",
                    pass1Tolerance,
                    pass1.Stats.MAD,
                    pass1.Stats.MAD * 1.4826,
                    pass1.Stats.ResidualSD,
                    pass1.Stats.NPoints,
                    pass1.Stats.RSquared,
                    pass1MinTolerance));

                // Only refine if the tolerance narrowed at least 2* tighter than the
                // initial wide window. Never refine off a linear fit: pass 2 re-scores
                // inside the narrowed window, so points outside a mis-centred window
                // can never be recovered. That is harmless at n=729 and is the whole
                // risk at the bottom of the graduated tier (#4401).
                if (pass1.Calibration.Method == RTCalibrationMethod.Linear)
                {
                    _ctx.LogInfo(string.Format(
                        "Refinement pass skipped: pass 1 used a linear fit ({0} points); " +
                        "narrowing the window from it would be self-confirming.",
                        pass1.Stats.NPoints));
                }
                else if (pass1Tolerance < initialTolerance * 0.5)
                {
                    _ctx.LogVerbose(string.Format(
                        "Calibration refinement: re-scoring with {0:F2} min tolerance (was {1:F1} min)",
                        pass1Tolerance, initialTolerance));

                    var pass2 = RunRefinementPass(
                        sampledEntries, spectraByWindowKey, preprocessedByWindowKey,
                        ms1Spectra, context, rtSlope, rtIntercept, pass1Tolerance,
                        pass1.Calibration /* pass 2 predicts RT via the LOESS fit */);

                    if (pass2 != null)
                    {
                        double refinedMadTolerance = pass2.Stats.MAD * 1.4826 * 3.0;
                        double refinedTolerance = Math.Max(
                            RTCalibration.EffectiveMinRtTolerance(
                                pass2.Stats.NPoints,
                                config.RtCalibration.MinRtTolerance,
                                config.RtCalibration.MaxRtTolerance,
                                config.RtCalibration.MinCalibrationPoints),
                            Math.Min(config.RtCalibration.MaxRtTolerance, refinedMadTolerance));

                        _ctx.LogVerbose(string.Format(
                            "Refined RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                            refinedTolerance,
                            pass2.Stats.MAD,
                            pass2.Stats.MAD * 1.4826,
                            pass2.Stats.ResidualSD,
                            pass2.Stats.NPoints,
                            pass2.Stats.RSquared));

                        // A refined LINEAR fit must clear the same guards pass 1's does.
                        // R^2 alone is not sufficient evidence for a line: a narrowed
                        // pass-2 window can leave the confident points clustered over a
                        // short RT span, where a line scores R^2 ~ 1 yet extrapolates
                        // badly across the rest of the gradient. Without this, such a fit
                        // could displace a perfectly good pass-1 LOESS. Reachable
                        // whenever pass 2's point count lands in
                        // [ABSOLUTE_MIN_CALIBRATION_POINTS, LINEAR_FIT_MAX_POINTS).
                        bool refinedLinearOk = IsRefinedFitAcceptable(
                            pass2.Calibration, pass2.LibRts, libMinRt, libMaxRt,
                            rtSlope, mzmlMinRt, mzmlMaxRt);

                        if (!refinedLinearOk)
                        {
                            _ctx.LogInfo(string.Format(
                                "Refined calibration is a linear fit over {0} points that fails the " +
                                "span/plausibility guards, keeping original calibration",
                                pass2.Stats.NPoints));
                        }

                        // Accept the refined calibration only if R^2 didn't degrade
                        // by more than 1% (matches Rust pipeline.rs).
                        if (refinedLinearOk && pass2.Stats.RSquared >= pass1.Stats.RSquared * 0.99)
                        {
                            // Overwrite the OSPREY_DUMP_LOESS_INPUT dump with
                            // pass 2's points so the diagnostic reflects the
                            // calibration actually being used. Mirrors Rust
                            // pipeline.rs; only fires on acceptance.
                            if (_ctx.Diagnostics?.DumpLoessInput ?? false)
                            {
                                _ctx.Diagnostics?.WriteLoessInputDump(
                                    2, pass2.LibRts, pass2.MeasuredRts);
                            }
                            ms1Calibration = pass2.Ms1Calibration;
                            ms2Calibration = pass2.Ms2Calibration;
                            return pass2.Calibration;
                        }
                        _ctx.LogInfo(string.Format(
                            "Refined calibration not better (R^2 {0:F4} vs {1:F4}), keeping original",
                            pass2.Stats.RSquared, pass1.Stats.RSquared));
                    }
                    else
                    {
                        _ctx.LogInfo(string.Format(
                            "Refinement pass: insufficient points (need {0}), keeping original calibration",
                            ABSOLUTE_MIN_CALIBRATION_POINTS));
                    }
                }

                ms1Calibration = pass1.Ms1Calibration;
                ms2Calibration = pass1.Ms2Calibration;
                return pass1.Calibration;
            }

            // Unreachable: maxAttempts >= 1, and every path inside the loop either
            // returns or continues to a further attempt. Mirrors Rust's
            // unreachable!("Calibration retry loop exited without result").
            _ctx.LogWarning("Calibration retry loop exited without a result. Using fallback tolerance.");
            ms1Calibration = MzCalibrationResult.Uncalibrated();
            ms2Calibration = MzCalibrationResult.Uncalibrated();
            return null;
        }

        /// <summary>
        /// Number of calibration sampling attempts. Three when the sample is a strict
        /// subset of the library's targets and the retry factor actually grows it,
        /// otherwise one (the first attempt already sees every target, so retrying
        /// would rescore an identical set). Ports Rust pipeline.rs:709-714.
        /// </summary>
        internal static int ComputeMaxAttempts(int sampleSize, double retryFactor, int nTotalTargets)
        {
            return sampleSize > 0 && retryFactor > 1.0 && nTotalTargets > sampleSize ? 3 : 1;
        }

        /// <summary>
        /// Decide what the retry ladder does after one attempt's confident-peptide
        /// count is known. Ports the branch at Rust pipeline.rs:1000-1048.
        ///
        /// Below the absolute floor Rust returns OspreyError::ConfigError, but its only
        /// caller (pipeline.rs:4278) catches that, logs "Calibration failed ... Using
        /// fallback tolerance", and searches the file with fallback tolerances. So the
        /// net per-file behaviour is a graceful degrade, which
        /// <see cref="CalibrationLadderAction.Fallback"/> reproduces exactly -- see also
        /// osprey docs/02-calibration.md, which documents the fallback. Neither
        /// implementation aborts the run over one bad file (issue #4401).
        ///
        /// Pure and side-effect free so the ladder that regressed is directly testable.
        /// </summary>
        internal static CalibrationLadderDecision DecideLadderAction(
            int attempt, int maxAttempts, bool usedAll, int nConfident,
            int minCalibrationPoints, int currentSampleSize, double retryFactor, int nTotalTargets)
        {
            // effective_min_points = min(num_confident, min_calibration_points),
            // Rust pipeline.rs:1048.
            int effectiveMinPoints = Math.Min(nConfident, minCalibrationPoints);

            if (nConfident >= minCalibrationPoints)
            {
                return new CalibrationLadderDecision
                {
                    Action = CalibrationLadderAction.Fit,
                    EffectiveMinPoints = effectiveMinPoints,
                };
            }

            if (attempt < maxAttempts && !usedAll)
            {
                // Grow the sample; the final attempt always uses ALL targets
                // (Rust pipeline.rs:1004-1014).
                int nextSampleSize;
                if (attempt + 1 == maxAttempts)
                {
                    nextSampleSize = 0;
                }
                else
                {
                    int newSize = (int)(currentSampleSize * retryFactor);
                    nextSampleSize = newSize >= nTotalTargets ? 0 : newSize;
                }
                return new CalibrationLadderDecision
                {
                    Action = CalibrationLadderAction.Retry,
                    NextSampleSize = nextSampleSize,
                };
            }

            if (nConfident < MIN_LINEAR_FIT_POINTS)
                return new CalibrationLadderDecision { Action = CalibrationLadderAction.Fallback };

            // Final attempt, at or above the fit floor: fit anyway with a relaxed
            // guard (Rust pipeline.rs:1028-1036). Between MIN_LINEAR_FIT_POINTS and
            // LINEAR_FIT_MAX_POINTS SelectFitPlan chooses a robust line; the caller
            // additionally requires the points to span the gradient and the fitted
            // line to stay plausible before accepting it.
            return new CalibrationLadderDecision
            {
                Action = CalibrationLadderAction.Fit,
                EffectiveMinPoints = effectiveMinPoints,
                BelowTarget = true,
            };
        }

        /// <summary>
        /// Choose the curve to fit for <paramref name="nPoints"/> calibration points.
        ///
        /// LOESS bandwidth is a *fraction* of the points, so the local window it fits
        /// is <c>bandwidth * n</c> and thins as n falls: at the default 0.3, n=729
        /// gives 219-point windows, n=193 gives 58, n=100 gives 30, n=50 gives 15.
        /// Rather than let the window collapse, hold it near the size the default
        /// configuration yields at exactly <paramref name="minCalibrationPoints"/>
        /// (0.3 * 200 = 60 points) by widening the bandwidth as n shrinks -- a wider
        /// bandwidth is a *stiffer*, less locally varying fit. Below
        /// <see cref="LINEAR_FIT_MAX_POINTS"/> even that is over-flexible, and a single
        /// global robust (Theil-Sen) line is the better-conditioned estimator.
        ///
        /// Pure, so the tier boundaries are directly testable. See issue #4401.
        /// </summary>
        internal static CalibrationFitPlan SelectFitPlan(
            int nPoints, double loessBandwidth, int minCalibrationPoints)
        {
            // Enough points for the configured bandwidth: unchanged behaviour.
            if (nPoints >= minCalibrationPoints)
                return new CalibrationFitPlan { Bandwidth = loessBandwidth };

            if (nPoints < LINEAR_FIT_MAX_POINTS)
                return new CalibrationFitPlan { LinearFit = true, Bandwidth = loessBandwidth };

            double targetWindow = loessBandwidth * minCalibrationPoints;
            double bandwidth = targetWindow / nPoints;
            if (bandwidth < loessBandwidth)
                bandwidth = loessBandwidth;
            if (bandwidth > 1.0)
                bandwidth = 1.0;
            return new CalibrationFitPlan { Bandwidth = bandwidth };
        }

        /// <summary>
        /// Do the confident calibration points cover enough of the library RT range to
        /// determine a slope? A line has two degrees of freedom, and its slope is only
        /// identifiable when the points have leverage: twenty peptides spread across
        /// the gradient determine one, twenty bunched into a single region do not,
        /// however confident each is. Guards the linear tier (#4401).
        /// </summary>
        internal static bool HasSufficientRtSpan(
            IReadOnlyList<double> libRts, double libMinRt, double libMaxRt)
        {
            double libRange = libMaxRt - libMinRt;
            if (libRange <= 0.0 || libRts.Count == 0)
                return false;

            double min = double.MaxValue, max = double.MinValue;
            foreach (double rt in libRts)
            {
                if (rt < min)
                    min = rt;
                if (rt > max)
                    max = rt;
            }
            return (max - min) >= libRange * MIN_LINEAR_FIT_RT_SPAN_FRACTION;
        }

        /// <summary>
        /// Sanity-check a linear calibration against the library/mzML RT range mapping.
        /// A thin fit that lands somewhere the ranges cannot justify -- a slope more
        /// than <see cref="MAX_LINEAR_FIT_SLOPE_RATIO"/> away from the range-derived
        /// one, or predictions outside the mzML acquisition window -- would re-centre
        /// every search window on a wrong gradient. That is strictly worse than no fit,
        /// so the caller falls back instead (#4401).
        /// </summary>
        internal static bool IsPlausibleLinearFit(
            RTCalibration calibration, double rangeSlope, double mzmlMinRt, double mzmlMaxRt)
        {
            calibration.LibraryRtRange(out double libMin, out double libMax);
            if (libMax <= libMin)
                return false;

            double predMin = calibration.Predict(libMin);
            double predMax = calibration.Predict(libMax);
            double fittedSlope = (predMax - predMin) / (libMax - libMin);

            // A negative or vanishing gradient means RT ordering is not preserved.
            if (!(fittedSlope > 0.0) || !(rangeSlope > 0.0))
                return false;

            double ratio = fittedSlope / rangeSlope;
            if (ratio > MAX_LINEAR_FIT_SLOPE_RATIO || ratio < 1.0 / MAX_LINEAR_FIT_SLOPE_RATIO)
                return false;

            // Predictions must land inside the acquisition window, allowing a margin
            // for peptides eluting near the very start or end of the gradient.
            double mzmlRange = mzmlMaxRt - mzmlMinRt;
            if (mzmlRange <= 0.0)
                return false;
            double margin = mzmlRange * 0.1;
            return predMin >= mzmlMinRt - margin && predMax <= mzmlMaxRt + margin;
        }

        /// <summary>
        /// May pass 2's refit replace pass 1's calibration?
        ///
        /// A LOESS refit is judged on R^2 alone. A LINEAR refit must additionally clear
        /// the same span and plausibility guards pass 1's linear fit does, because R^2
        /// is not evidence for a line: pass 2 re-scores inside a narrowed RT window, so
        /// its surviving points can be clustered over a short span where a line scores
        /// R^2 ~ 1 yet extrapolates badly across the rest of the gradient. Reachable
        /// whenever pass 2's point count lands in
        /// [<see cref="ABSOLUTE_MIN_CALIBRATION_POINTS"/>, <see cref="LINEAR_FIT_MAX_POINTS"/>),
        /// since pass 2 only runs at all when pass 1 was a LOESS fit. Reported by Copilot
        /// on maccoss/osprey#52; see issue #4401.
        /// </summary>
        internal static bool IsRefinedFitAcceptable(
            RTCalibration refined, IReadOnlyList<double> refinedLibRts,
            double libMinRt, double libMaxRt,
            double rangeSlope, double mzmlMinRt, double mzmlMaxRt)
        {
            if (refined.Method != RTCalibrationMethod.Linear)
                return true;

            return HasSufficientRtSpan(refinedLibRts, libMinRt, libMaxRt) &&
                   IsPlausibleLinearFit(refined, rangeSlope, mzmlMinRt, mzmlMaxRt);
        }

        /// <summary>
        /// Run the pass-2 refinement: re-score the current attempt's sample with the
        /// narrowed RT tolerance and the fitted LOESS model, then refit. Returns null
        /// when the refined pass falls below <see cref="ABSOLUTE_MIN_CALIBRATION_POINTS"/>
        /// or the fit throws, in which case the caller keeps pass 1's calibration.
        /// Mirrors Rust pipeline.rs:1094-1237: the refinement scores only the current
        /// attempt's sample and does NOT accumulate across attempts.
        /// </summary>
        private CalibrationPassResult RunRefinementPass(
            List<LibraryEntry> sampledEntries,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            Dictionary<int, double[][]> preprocessedByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double tolerance,
            RTCalibration calibrationModel)
        {
            var (matches, snrByEntryId, matchRts) = ScoreCalibrationMatches(
                2, sampledEntries, spectraByWindowKey, preprocessedByWindowKey,
                ms1Spectra, context, rtSlope, rtIntercept, tolerance, calibrationModel);

            EmitScoringDumps(2, sampledEntries, matches, matchRts, snrByEntryId);

            if (matches.Count == 0)
            {
                _ctx.LogWarning("No calibration matches could be scored in pass 2.");
                return null;
            }

            // Pass 2 stands alone: build a fresh per-entry map from its own matches
            // rather than reusing the attempt accumulator.
            var refined = new Dictionary<uint, AccumulatedMatch>();
            MergeCalibrationMatches(refined, matches, snrByEntryId, matchRts);

            var matchArray = BuildSortedMatchArray(refined);
            RunLdaAndCollectPoints(
                2, matchArray, refined, context.FileName,
                out var libRtsDetected, out var measuredRtsDetected);

            if (libRtsDetected.Count < ABSOLUTE_MIN_CALIBRATION_POINTS)
            {
                _ctx.LogWarning(string.Format(
                    "Insufficient calibration points in pass 2 ({0} < {1}).",
                    libRtsDetected.Count, ABSOLUTE_MIN_CALIBRATION_POINTS));
                return null;
            }

            // The refit's own adaptive floor, matching Rust's
            // `n_refined.min(min_calibration_points)` (pipeline.rs). MinPoints is only a
            // fit-time guard (OutlierRetention == 1.0 disables the other branch that
            // reads it) and this value is always <= the point count, so the refit is
            // never rejected for its size -- a weak pass 2 is discarded by the R^2 test
            // below, not by the fit. That is the behaviour osprey docs/02-calibration.md
            // specifies: "If refinement produces fewer calibration points than the
            // absolute minimum (50), or if R^2 drops, the original pass-1 calibration is
            // kept."
            //
            // Rust used to violate its own doc here by reusing pass 1's calibrator,
            // whose min_points is pass 1's effective_min_points. On a band file that is
            // n_pass1 (e.g. 193), not 50, so a refit yielding 50..192 points cleared the
            // >= 50 guard and then tripped RTCalibrator::fit's min_points check, whose
            // error discarded a perfectly good pass-1 calibration and ran the file
            // uncalibrated. Fixed upstream in maccoss/osprey. See issue #4401.
            return FitCalibrationPass(
                2, matchArray, refined, libRtsDetected, measuredRtsDetected,
                context.Config,
                Math.Min(libRtsDetected.Count, context.Config.RtCalibration.MinCalibrationPoints));
        }

        /// <summary>
        /// Emit the per-scoring-pass cross-implementation dumps (per-entry isolation
        /// window selection, then per-entry calibration match info). Each honours its
        /// *_ONLY bisection abort. Split out of the former RunCalibrationScoringPass so
        /// both the retry-ladder pass 1 and the pass-2 refinement fire them identically.
        /// </summary>
        private void EmitScoringDumps(
            int passNumber,
            List<LibraryEntry> sampledEntries,
            ConcurrentBag<CalibrationMatch> matches,
            ConcurrentDictionary<uint, KeyValuePair<double, double>> matchRts,
            ConcurrentDictionary<uint, double> snrByEntryId)
        {
            // Write per-entry window dump if requested. When two passes run, the
            // pass 2 dump overwrites pass 1 - same behaviour as Rust's
            // run_coelution_calibration_scoring dumping on every invocation.
            if (_ctx.Diagnostics?.CalWindowsCollecting ?? false)
            {
                _ctx.Diagnostics?.WriteCalWindowsDump(passNumber);
                if (_ctx.Diagnostics?.CalWindowsOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump("OSPREY_CAL_WINDOWS_ONLY");
            }

            // Cross-implementation diagnostic: dump per-entry calibration match info
            // for direct diff with Rust. Writes a row for EVERY sampled entry
            // (matched or not), sorted by entry_id for stable diff.
            if (_ctx.Diagnostics?.DumpCalMatch ?? false)
            {
                _ctx.Diagnostics?.WriteCalMatchDump(passNumber, matches, sampledEntries, matchRts, snrByEntryId);
                if (_ctx.Diagnostics?.CalMatchOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump("OSPREY_CAL_MATCH_ONLY");
            }
        }

        /// <summary>
        /// Merge one scoring pass's matches into a per-entry map, keeping the
        /// higher-scoring match for any entry already present. Ports Rust's
        /// accumulate step (pipeline.rs:812-825), which compares
        /// <c>CalibrationMatch.score</c>.
        ///
        /// That Rust field is set to the co-elution correlation sum
        /// (batch.rs:2773), NOT the XCorr its doc comment at batch.rs:869 claims.
        /// The C# field holding the same value is <see cref="CalibrationMatch.CorrelationScore"/>.
        /// It must NOT be <see cref="CalibrationMatch.DiscriminantScore"/>: LDA
        /// overwrites that in place, so on attempt 2+ a comparison against it would
        /// weigh a previous attempt's LDA discriminant against a raw correlation sum.
        ///
        /// A strict &gt; keeps the incumbent on ties, matching Rust. Within one pass an
        /// entry yields at most one match, so the merge is order-independent and the
        /// nondeterministic ConcurrentBag enumeration order cannot affect the result.
        /// Internal so Osprey.Test can pin the "which field decides" rule.
        /// </summary>
        internal static void MergeCalibrationMatches(
            Dictionary<uint, AccumulatedMatch> accumulated,
            ConcurrentBag<CalibrationMatch> matches,
            ConcurrentDictionary<uint, double> snrByEntryId,
            ConcurrentDictionary<uint, KeyValuePair<double, double>> matchRts)
        {
            foreach (var m in matches)
            {
                KeyValuePair<double, double> rtPair;
                if (!matchRts.TryGetValue(m.EntryId, out rtPair))
                    continue;

                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr))
                    snr = 0.0;

                AccumulatedMatch existing;
                if (accumulated.TryGetValue(m.EntryId, out existing) &&
                    m.CorrelationScore <= existing.Match.CorrelationScore)
                {
                    continue;
                }

                accumulated[m.EntryId] = new AccumulatedMatch
                {
                    Match = m,
                    Snr = snr,
                    LibRt = rtPair.Key,
                    MeasuredRt = rtPair.Value,
                };
            }
        }

        /// <summary>
        /// Flatten the per-entry map into the (base_id, entry_id)-sorted array LDA
        /// consumes. The explicit sort makes the result independent of Dictionary
        /// enumeration order, so an accumulated set is ordered identically to the
        /// single-pass array it replaces.
        /// </summary>
        private static CalibrationMatch[] BuildSortedMatchArray(
            Dictionary<uint, AccumulatedMatch> accumulated)
        {
            var matchArray = new CalibrationMatch[accumulated.Count];
            int i = 0;
            foreach (var kvp in accumulated)
                matchArray[i++] = kvp.Value.Match;

            // Sort deterministically by (base_id, entry_id) so LDA sees a stable order.
            Array.Sort(matchArray, (a, b) => // Array.Sort OK: comparator's secondary key is the unique EntryId, so no ties
            {
                uint baseA = a.EntryId & 0x7FFFFFFF;
                uint baseB = b.EntryId & 0x7FFFFFFF;
                int cmp = baseA.CompareTo(baseB);
                if (cmp != 0)
                    return cmp;
                return a.EntryId.CompareTo(b.EntryId);
            });
            return matchArray;
        }

        /// <summary>
        /// Train LDA with 1% FDR target-decoy competition over the supplied matches,
        /// then collect the surviving high-S/N targets' (libRt, measuredRt) pairs.
        /// Emits the LDA timing/count logs and the LDA-scores bisection dump.
        /// </summary>
        private void RunLdaAndCollectPoints(
            int passNumber,
            CalibrationMatch[] matchArray,
            Dictionary<uint, AccumulatedMatch> accumulated,
            string fileName,
            out List<double> libRtsDetected,
            out List<double> measuredRtsDetected)
        {
            // Train LDA + 1% FDR target-decoy competition.
            var swLda = Stopwatch.StartNew();
            int nPassing = CalibrationScorer.TrainAndScoreCalibration(
                matchArray, false, out CalibrationTrainingReport calReport);
            swLda.Stop();

            // --verbose: dump the calibration LDA's seed, per-iteration refinement trace,
            // per-feature contribution, and 1% / 0.1% q yield -- the calibration analog of
            // the Percolator feature-contribution report.
            if (OspreyOutput.Verbose && calReport != null)
            {
                foreach (string line in calReport.ToReportLines(
                    string.Format(@"{0} pass {1}", fileName, passNumber)))
                {
                    _ctx.LogVerbose(line);
                }
            }

            int nTargetWins = 0;
            int nDecoyWins = 0;
            foreach (var m in matchArray)
            {
                if (m.QValue <= CAL_FDR_THRESHOLD)
                {
                    if (m.IsDecoy)
                        nDecoyWins++;
                    else nTargetWins++;
                }
            }
            _ctx.LogInfo(string.Format(
                "[TIMING] Calibration pass {0} LDA: {1:F2}s ({2} target wins, {3} decoy wins at 1% FDR)",
                passNumber, swLda.Elapsed.TotalSeconds, nTargetWins, nDecoyWins));
            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} LDA winners [{1}]: {2} target wins, {3} decoy wins at 1% FDR",
                passNumber, fileName, nTargetWins, nDecoyWins));

            // --verbose anchor-purity (entrapment-FDP) diagnostic: of the target-side
            // anchors that clear the calibration q-gate, how many are FDRBench entrapment
            // (known-absent shuffles)? This is an INDEPENDENT check on whether the per-run
            // q<=1% gate actually controls the true error rate -- entrapment are targets in
            // the target-decoy competition, so they are invisible to the reported q. Only
            // emitted when the library carries entrapment markers (_libEntrapmentCount>0).
            if (OspreyOutput.Verbose && _libEntrapmentCount > 0)
            {
                long realTargets = _libTargetSideCount - _libEntrapmentCount;
                double rLib = realTargets > 0 ? (double)_libEntrapmentCount / realTargets : 0.0;
                foreach (string line in BuildAnchorPurityReport(matchArray, rLib, fileName, passNumber))
                    _ctx.LogVerbose(line);
            }

            // Cross-implementation diagnostic: dump per-entry LDA discriminant + q-value
            // sorted by entry_id for stable diff with rust_lda_scores.txt. Gated by
            // OSPREY_DUMP_LDA_SCORES; exits after write when OSPREY_LDA_SCORES_ONLY is set.
            if (_ctx.Diagnostics?.DumpLdaScores ?? false)
            {
                _ctx.Diagnostics?.WriteLdaScoresDump(passNumber, matchArray);
                if (_ctx.Diagnostics?.LdaScoresOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump("OSPREY_LDA_SCORES_ONLY");
            }

            // Collect high-confidence target matches that also meet the S/N
            // quality gate (logs the S/N filter + calibration-point counts).
            var (libRts, measuredRts) = CollectCalibrationPoints(
                matchArray, accumulated, passNumber, fileName, nTargetWins);
            _ctx.LogInfo(string.Format(
                "Calibration pass {0}: {1} RT calibration points (from {2} peptides at 1% FDR)",
                passNumber, libRts.Count, nPassing));

            libRtsDetected = libRts;
            measuredRtsDetected = measuredRts;
        }

        /// <summary>
        /// Build the --verbose anchor-purity (entrapment-FDP) report for one calibration
        /// pass: at each q threshold in a sweep (0.1/1/2/5/10%), among the target-side
        /// anchors that clear that gate, how many are FDRBench entrapment (known-absent
        /// shuffles)? Reports the raw entrapment fraction plus the ratio-corrected
        /// lower-bound and combined FDP estimators (see docs/fractional-entrapment.md), so
        /// the reader can read the yield-vs-true-FDP curve and compare it against the
        /// claimed q. Purely informational; anchor selection is unaffected.
        /// </summary>
        private static IEnumerable<string> BuildAnchorPurityReport(
            CalibrationMatch[] matchArray, double rLib, string fileName, int passNumber)
        {
            // The scored candidate pool (all target-side entries that produced a peak and
            // entered the LDA) is the denominator context: it shows how many entrapment were
            // in contention before the gate rejected them.
            int poolT = 0, poolE = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy)
                    continue;
                if (m.IsEntrapment)
                    poolE++;
                else poolT++;
            }
            var lines = new List<string>
            {
                string.Format(
                    "=== Calibration anchor purity [{0} pass {1}] (FDRBench r={2:F3}) ===",
                    fileName, passNumber, rLib),
                string.Format(
                    "  scored pool: {0} target-side = {1} target + {2} entrapment (peaks entered LDA)",
                    poolT + poolE, poolT, poolE),
            };
            // q-sweep: each threshold is a point on the yield-vs-entrapment-FDP curve, so a
            // single run shows how far the gate could loosen before the true FDP degrades
            // (informs any rank/floor or looser-q anchor-selection change).
            foreach (double q in new[] { 0.001, 0.01, 0.02, 0.05, 0.10 })
                lines.Add(AnchorPurityLine(matchArray, q, rLib,
                    string.Format("q<={0,5:0.0%}", q)));
            return lines;
        }

        /// <summary>One anchor-purity line at a q threshold: target vs entrapment anchor
        /// counts and the raw / lower-bound / combined entrapment-FDP.</summary>
        private static string AnchorPurityLine(
            CalibrationMatch[] matchArray, double qThreshold, double rLib, string label)
        {
            int nT = 0, nE = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy || m.QValue > qThreshold)
                    continue;
                if (m.IsEntrapment)
                    nE++;
                else nT++;
            }
            int total = nT + nE;
            double rawFrac = total > 0 ? (double)nE / total : 0.0;
            double fdpLower = (total > 0 && rLib > 0) ? nE / (rLib * total) : 0.0;
            double fdpCombined = (total > 0 && rLib > 0) ? (1.0 + 1.0 / rLib) * nE / total : 0.0;
            return string.Format(
                "  {0}: {1} anchors = {2} target + {3} entrapment | entrapment-frac {4:P2} | FDP lower {5:P2} combined {6:P2}",
                label, total, nT, nE, rawFrac, fdpLower, fdpCombined);
        }

        /// <summary>
        /// Aggregate the MS1/MS2 mass calibrations and fit the LOESS RT calibration
        /// for one pass. Returns null when the fit throws.
        /// </summary>
        private CalibrationPassResult FitCalibrationPass(
            int passNumber,
            CalibrationMatch[] matchArray,
            Dictionary<uint, AccumulatedMatch> accumulated,
            List<double> libRtsDetected,
            List<double> measuredRtsDetected,
            OspreyConfig config,
            int minLoessPoints)
        {
            // Aggregate MS1 + MS2 mass errors from passing targets only
            // (same LDA + competition + S/N survivors as the RT points;
            // emits the MS2 cal-errors dump + the MS1/MS2 calibration logs).
            AggregateMassCalibrations(
                matchArray, accumulated, config, passNumber,
                out var ms1Cal, out var ms2Cal);

            // Fit LOESS calibration.
            var swLoess = Stopwatch.StartNew();
            try
            {
                double[] libRts = libRtsDetected.ToArray();
                double[] measuredRts = measuredRtsDetected.ToArray();

                // Graduated fit: thin point sets get a stiffer LOESS, and very thin
                // ones a global line, rather than a collapsing local window (#4401).
                var plan = SelectFitPlan(
                    libRts.Length,
                    config.RtCalibration.LoessBandwidth,
                    config.RtCalibration.MinCalibrationPoints);

                if (plan.LinearFit)
                {
                    _ctx.LogWarning(string.Format(
                        "Calibration pass {0}: {1} points is below {2}; fitting a global linear calibration.",
                        passNumber, libRts.Length, LINEAR_FIT_MAX_POINTS));
                }
                else if (plan.Bandwidth > config.RtCalibration.LoessBandwidth)
                {
                    _ctx.LogWarning(string.Format(
                        "Calibration pass {0}: {1} points is below {2}; widening LOESS bandwidth {3:F2} -> {4:F2} to hold the local window near {5:F0} points.",
                        passNumber, libRts.Length, config.RtCalibration.MinCalibrationPoints,
                        config.RtCalibration.LoessBandwidth, plan.Bandwidth,
                        config.RtCalibration.LoessBandwidth * config.RtCalibration.MinCalibrationPoints));
                }

                var calibratorConfig = new RTCalibratorConfig
                {
                    Bandwidth = plan.Bandwidth,
                    Degree = 1,
                    MinPoints = minLoessPoints,
                    RobustnessIterations = 2,
                    OutlierRetention = 1.0, // LDA + S/N already filtered
                    ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust,
                    LinearFit = plan.LinearFit
                };

                // NOTE: the OSPREY_DUMP_LOESS_INPUT diagnostic is emitted by
                // the caller (RunCalibration) so that pass 2 only dumps when
                // it is actually accepted -- mirrors Rust pipeline.rs (and
                // matches the PR #42 semantics of "dump reflects the
                // calibration actually used"). The pairs are returned via
                // CalibrationPassResult.LibRts/MeasuredRts.

                var calibrator = new RTCalibrator(calibratorConfig);
                var rtCal = calibrator.Fit(libRts, measuredRts);
                swLoess.Stop();

                var stats = rtCal.Stats();
                _ctx.LogInfo(string.Format("[TIMING] Calibration pass {0} LOESS fit: {1:F2}s",
                    passNumber, swLoess.Elapsed.TotalSeconds));
                _ctx.LogVerbose(string.Format(
                    "RT calibration pass {0}: {1} points, R2={2:F4}, residual SD={3:F3} min, MAD={4:F3}",
                    passNumber, stats.NPoints, stats.RSquared, stats.ResidualSD, stats.MAD));

                return new CalibrationPassResult
                {
                    Calibration = rtCal,
                    Stats = stats,
                    Ms1Calibration = ms1Cal,
                    Ms2Calibration = ms2Cal,
                    LibRts = libRts,
                    MeasuredRts = measuredRts,
                };
            }
            catch (Exception ex)
            {
                swLoess.Stop();
                _ctx.LogWarning(string.Format("RT calibration pass {0} failed: {1}",
                    passNumber, ex.Message));
                return null;
            }
        }

        /// <summary>
        /// Pre-preprocess all window spectra for XCorr using the calibration
        /// unit-bin scorer (~2K bins per spectrum, ~16 KB per array).
        /// Independent of resolution mode -- HRAM 100K-bin arrays would
        /// consume ~160 GB of LOH for 204K Astral spectra, so we use the
        /// small unit-bin form for calibration regardless. Main search
        /// still uses the resolution-mode bins.
        /// Calibration preprocess runs in pure f32 to match Rust upstream
        /// maccoss/osprey's native f32 XCorr path (cross-impl parity at
        /// F10 rounding noise, vs ~4e-6 drift under f64). f32 values are
        /// widened to double[] here so the downstream XcorrFromPreprocessed
        /// path is unchanged; the widening is lossless (f32 is a subset of
        /// f64) and preserves the f32 bit pattern for the final sum.
        /// </summary>
        private Dictionary<int, double[][]> PreprocessWindowsForXcorr(
            Dictionary<int, List<Spectrum>> spectraByWindowKey)
        {
            var preprocessedByWindowKey = new Dictionary<int, double[][]>();
            foreach (var kvp in spectraByWindowKey)
            {
                var pp = new double[kvp.Value.Count][];
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    float[] f32pp = s_calXcorrScorer.PreprocessSpectrumForXcorrF32(kvp.Value[i]);
                    var widened = new double[f32pp.Length];
                    for (int k = 0; k < f32pp.Length; k++)
                        widened[k] = f32pp[k];
                    pp[i] = widened;
                }
                preprocessedByWindowKey[kvp.Key] = pp;
            }
            return preprocessedByWindowKey;
        }

        /// <summary>
        /// Parallel-score each sampled calibration entry against its isolation
        /// window, returning the successful matches plus the per-entry S/N and
        /// (libRt, measuredRt) maps. Emits the pass timing + match-count logs.
        /// </summary>
        private (ConcurrentBag<CalibrationMatch> Matches,
            ConcurrentDictionary<uint, double> SnrByEntryId,
            ConcurrentDictionary<uint, KeyValuePair<double, double>> MatchRts) ScoreCalibrationMatches(
                int passNumber,
                List<LibraryEntry> sampledEntries,
                Dictionary<int, List<Spectrum>> spectraByWindowKey,
                Dictionary<int, double[][]> preprocessedByWindowKey,
                List<MS1Spectrum> ms1Spectra,
                ScoringContext context,
                double rtSlope, double rtIntercept, double tolerance,
                RTCalibration calibrationModel)
        {
            var config = context.Config;
            var resolution = context.Resolution;
            var fileName = context.FileName;

            // Activate per-entry window dump if requested. The rows are added by
            // ScoreCalibrationEntry below and written by EmitScoringDumps.
            _ctx.Diagnostics?.StartCalWindowCollection();

            var swScoring = Stopwatch.StartNew();
            var matches = new ConcurrentBag<CalibrationMatch>();
            var snrByEntryId = new ConcurrentDictionary<uint, double>();
            var matchRts = new ConcurrentDictionary<uint, KeyValuePair<double, double>>();

            Parallel.ForEach(sampledEntries, new ParallelOptions
            {
                MaxDegreeOfParallelism = config.NThreads
            },
            () => resolution.CreateScorer(),
            (entry, loopState, localScorer) =>
            {
                double entrySnr;
                double entryLibRt;
                double entryMeasuredRt;
                var match = ScoreCalibrationEntry(
                    entry, spectraByWindowKey, preprocessedByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, tolerance,
                    calibrationModel,
                    localScorer,
                    out entrySnr, out entryLibRt, out entryMeasuredRt);
                if (match != null)
                {
                    matches.Add(match);
                    snrByEntryId[entry.Id] = entrySnr;
                    matchRts[entry.Id] = new KeyValuePair<double, double>(
                        entryLibRt, entryMeasuredRt);
                }
                return localScorer;
            },
            localScorer => { });
            swScoring.Stop();
            _ctx.LogInfo(string.Format(
                "[TIMING] Calibration pass {0} scoring: {1:F2}s ({2} matches)",
                passNumber, swScoring.Elapsed.TotalSeconds, matches.Count));
            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} matches scored [{1}]: {2}",
                passNumber, fileName, matches.Count));
            return (matches, snrByEntryId, matchRts);
        }

        /// <summary>
        /// Collect the high-confidence target matches that pass the 1% FDR gate
        /// and the S/N >= <see cref="MIN_SNR_FOR_RT_CAL"/> quality filter,
        /// returning their (libRt, measuredRt) pairs as the LOESS fit input.
        /// Emits the S/N-filter and calibration-point-count logs.
        /// </summary>
        private (List<double> LibRts, List<double> MeasuredRts) CollectCalibrationPoints(
            CalibrationMatch[] matchArray,
            Dictionary<uint, AccumulatedMatch> accumulated,
            int passNumber, string fileName, int nTargetWins)
        {
            var libRtsDetected = new List<double>();
            var measuredRtsDetected = new List<double>();
            int nSnrFiltered = 0;
            foreach (var m in matchArray)
            {
                if (m.IsDecoy)
                    continue;
                if (m.QValue > CAL_FDR_THRESHOLD)
                    continue;

                AccumulatedMatch acc;
                if (!accumulated.TryGetValue(m.EntryId, out acc))
                    continue;

                if (acc.Snr < MIN_SNR_FOR_RT_CAL)
                {
                    nSnrFiltered++;
                    continue;
                }

                libRtsDetected.Add(acc.LibRt);
                measuredRtsDetected.Add(acc.MeasuredRt);
            }

            if (nSnrFiltered > 0)
            {
                _ctx.LogVerbose(string.Format(
                    "  RT quality filter (pass {0}): {1} -> {2} peptides (removed {3} with S/N < {4:F1})",
                    passNumber, nTargetWins, libRtsDetected.Count, nSnrFiltered, MIN_SNR_FOR_RT_CAL));
            }

            _ctx.LogInfo(string.Format(
                "[COUNT] Calibration pass {0} high-quality (S/N>=5) [{1}]: {2}",
                passNumber, fileName, libRtsDetected.Count));

            return (libRtsDetected, measuredRtsDetected);
        }

        /// <summary>
        /// Aggregate MS1 + MS2 mass errors from passing targets only (those
        /// surviving LDA + competition + the S/N >= 5.0 filter -- the same set
        /// used for RT calibration points), compute the single-level MS1/MS2
        /// mass calibrations, and emit the MS2 cal-errors bisection dump plus
        /// the MS1/MS2 calibration logs. Mirrors Rust pipeline.rs:610-619.
        /// </summary>
        private void AggregateMassCalibrations(
            CalibrationMatch[] matchArray,
            Dictionary<uint, AccumulatedMatch> accumulated,
            OspreyConfig config, int passNumber,
            out MzCalibrationResult ms1Calibration,
            out MzCalibrationResult ms2Calibration)
        {
            var allMs1Errors = new List<double>();
            var allMs2Errors = new List<double>();
            // Track contributing matches in a stable list so a cross-impl
            // bisection dump can replay them in the same order the
            // calibration accumulator sees their errors.
            var contributingMatches = new List<CalibrationMatch>();
            foreach (var m in matchArray)
            {
                if (m.Ms2MassErrors == null || m.IsDecoy || m.QValue > CAL_FDR_THRESHOLD)
                    continue;
                AccumulatedMatch acc;
                if (!accumulated.TryGetValue(m.EntryId, out acc) || acc.Snr < MIN_SNR_FOR_RT_CAL)
                    continue;
                if (m.Ms1Error.HasValue)
                    allMs1Errors.Add(m.Ms1Error.Value);
                allMs2Errors.AddRange(m.Ms2MassErrors);
                contributingMatches.Add(m);
            }
            if (_ctx.Diagnostics?.DumpMs2CalErrors ?? false)
            {
                _ctx.Diagnostics?.WriteMs2CalErrorsDump(contributingMatches);
                if (_ctx.Diagnostics?.Ms2CalErrorsOnly ?? false)
                    OspreyDiagnosticsLog.ExitAfterDump("OSPREY_MS2_CAL_ERRORS_ONLY");
            }
            string unitStr = config.FragmentTolerance.Unit == ToleranceUnit.Ppm ? "ppm" : "Th";
            ms1Calibration = MzCalibration.CalculateSingleLevel(allMs1Errors.ToArray(), unitStr);
            ms2Calibration = MzCalibration.CalculateSingleLevel(allMs2Errors.ToArray(), unitStr);
            _ctx.LogVerbose(string.Format(
                "MS1 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} (n={5} precursor matches)",
                passNumber, ms1Calibration.Mean, unitStr, ms1Calibration.SD, 3.0 * ms1Calibration.SD, allMs1Errors.Count));
            _ctx.LogVerbose(string.Format(
                "MS2 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} (n={5} fragment matches)",
                passNumber, ms2Calibration.Mean, unitStr, ms2Calibration.SD, 3.0 * ms2Calibration.SD, allMs2Errors.Count));
        }

        /// <summary>
        /// Sample library entries for calibration discovery, keeping paired target-decoy
        /// pairs together (matched by base_id = entry_id &amp; 0x7FFFFFFF).
        /// Direct port of Rust sample_library_for_calibration (osprey-scoring/src/batch.rs:1450).
        /// Uses a 2D (RT x m/z) stratified grid with deterministic stride sampling.
        /// This is the first randomized/selected step in the pipeline, so it must match
        /// Rust exactly for the two tools to process the same calibration peptides.
        /// </summary>
        private static List<LibraryEntry> SampleLibraryForCalibration(
            List<LibraryEntry> library, int sampleSize, ulong seed, IOspreyDiagnostics diag)
        {
            if (sampleSize == 0)
                return new List<LibraryEntry>(library);

            var targets = new List<LibraryEntry>();
            var decoys = new List<LibraryEntry>();
            foreach (var entry in library)
            {
                if (entry.IsDecoy)
                    decoys.Add(entry);
                else targets.Add(entry);
            }

            if (targets.Count <= sampleSize)
                return new List<LibraryEntry>(library);

            // Build target_id -> decoy map (decoy_id = target_id | 0x80000000)
            var decoyMap = new Dictionary<uint, LibraryEntry>(decoys.Count);
            foreach (var d in decoys)
                decoyMap[d.Id & 0x7FFFFFFF] = d;

            // 2D stratified sampling: divide RT x m/z space into a grid.
            // ~sqrt(sample_size)/2 bins per axis for good 2D coverage.
            int binsPerAxis = (int)Math.Max(5, Math.Ceiling(Math.Sqrt(sampleSize) / 2.0));

            double rtMin = double.MaxValue, rtMax = double.MinValue;
            double mzMin = double.MaxValue, mzMax = double.MinValue;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t.RetentionTime < rtMin)
                    rtMin = t.RetentionTime;
                if (t.RetentionTime > rtMax)
                    rtMax = t.RetentionTime;
                if (t.PrecursorMz < mzMin)
                    mzMin = t.PrecursorMz;
                if (t.PrecursorMz > mzMax)
                    mzMax = t.PrecursorMz;
            }
            double rtRange = Math.Max(1e-6, rtMax - rtMin);
            double mzRange = Math.Max(1e-6, mzMax - mzMin);
            double rtBinWidth = rtRange / binsPerAxis;
            double mzBinWidth = mzRange / binsPerAxis;

            // Assign each target to a 2D grid cell
            var grid = new List<int>[binsPerAxis, binsPerAxis];
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    grid[i, j] = new List<int>();

            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                int rtBin = (int)Math.Floor((t.RetentionTime - rtMin) / rtBinWidth);
                int mzBin = (int)Math.Floor((t.PrecursorMz - mzMin) / mzBinWidth);
                if (rtBin >= binsPerAxis)
                    rtBin = binsPerAxis - 1;
                if (mzBin >= binsPerAxis)
                    mzBin = binsPerAxis - 1;
                grid[rtBin, mzBin].Add(i);
            }

            // Count non-empty cells and compute per-cell quota
            int nOccupied = 0;
            for (int i = 0; i < binsPerAxis; i++)
                for (int j = 0; j < binsPerAxis; j++)
                    if (grid[i, j].Count > 0)
                        nOccupied++;

            int perCell = nOccupied > 0 ? sampleSize / nOccupied : 1;
            if (perCell < 1)
                perCell = 1;

            // Diagnostic dump: scalar parameters + full grid contents,
            // matching Rust's dump format for direct diff.
            if (diag?.DumpCalSample ?? false)
            {
                diag.WriteCalScalarsAndGridDump(
                    targets, decoys, binsPerAxis,
                    rtMin, rtMax, mzMin, mzMax,
                    rtRange, mzRange, rtBinWidth, mzBinWidth,
                    nOccupied, perCell, seed, grid);
            }

            // Deterministic stride sampling from each cell.
            int offset = (int)(seed & 0x7FFFFFFF);
            var sampledIds = new HashSet<uint>();
            var sampled = new List<LibraryEntry>(sampleSize * 2);

            for (int ri = 0; ri < binsPerAxis; ri++)
            {
                for (int ci = 0; ci < binsPerAxis; ci++)
                {
                    var cell = grid[ri, ci];
                    if (cell.Count == 0)
                        continue;

                    int nTake = Math.Min(cell.Count, perCell);
                    int stride = Math.Max(1, cell.Count / nTake);
                    int cellOffset = offset % Math.Max(1, cell.Count);

                    for (int j = 0; j < nTake; j++)
                    {
                        int idx = (cellOffset + j * stride) % cell.Count;
                        var target = targets[cell[idx]];

                        if (sampledIds.Contains(target.Id))
                            continue;
                        sampledIds.Add(target.Id);
                        sampled.Add(target);

                        LibraryEntry decoy;
                        if (decoyMap.TryGetValue(target.Id, out decoy))
                            sampled.Add(decoy);
                    }
                }
            }

            // Second pass: if under-sampled, add more from occupied cells.
            if (sampledIds.Count < sampleSize)
            {
                int remaining = sampleSize - sampledIds.Count;
                int extraPerCell = Math.Max(1, remaining / Math.Max(1, nOccupied));

                bool done = false;
                for (int ri = 0; ri < binsPerAxis && !done; ri++)
                {
                    for (int ci = 0; ci < binsPerAxis; ci++)
                    {
                        var cell = grid[ri, ci];
                        if (cell.Count == 0)
                            continue;

                        int added = 0;
                        foreach (int targetIdx in cell)
                        {
                            if (added >= extraPerCell)
                                break;
                            var target = targets[targetIdx];
                            if (sampledIds.Contains(target.Id))
                                continue;
                            sampledIds.Add(target.Id);
                            sampled.Add(target);
                            LibraryEntry decoy;
                            if (decoyMap.TryGetValue(target.Id, out decoy))
                                sampled.Add(decoy);
                            added++;
                        }

                        if (sampledIds.Count >= sampleSize) { done = true; break; }
                    }
                }
            }

            return sampled;
        }

        /// <summary>
        /// Score a single library entry for calibration: extract fragment XICs across
        /// spectra in the entry's isolation window that fall within the initial RT
        /// tolerance, detect the best co-eluting peak, and compute the four LDA
        /// features at the apex (correlation, LibCosine, top-6 matched, XCorr).
        /// Returns null if the entry has no viable peak.
        /// On pass 1 (calibrationModel == null), expectedRt is computed from the
        /// linear (rtSlope * library_rt + rtIntercept) mapping. On pass 2, the
        /// LOESS-fitted RTCalibration is used to predict expected_rt and the
        /// (refined) tolerance is much tighter.
        /// </summary>
        private CalibrationMatch ScoreCalibrationEntry(
            LibraryEntry entry,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            Dictionary<int, double[][]> preprocessedByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double initialTolerance,
            RTCalibration calibrationModel,
            SpectralScorer scorer,
            out double signalToNoise,
            out double libraryRt,
            out double measuredRt)
        {
            var config = context.Config;
            signalToNoise = 0.0;
            libraryRt = entry.RetentionTime;
            measuredRt = 0.0;

            if (entry.Fragments == null || entry.Fragments.Count < 2)
                return null;

            // Find spectra in the entry's isolation window.
            int windowKey = (int)Math.Round(entry.PrecursorMz * 10.0);
            List<Spectrum> windowSpectra;
            if (!spectraByWindowKey.TryGetValue(windowKey, out windowSpectra))
            {
                // Try neighbouring window keys (handles off-by-one due to rounding).
                if (!spectraByWindowKey.TryGetValue(windowKey - 1, out windowSpectra) &&
                    !spectraByWindowKey.TryGetValue(windowKey + 1, out windowSpectra))
                {
                    // Fall back to linear scan across windows that contain this precursor.
                    windowSpectra = null;
                    foreach (var kvp in spectraByWindowKey)
                    {
                        var first = kvp.Value[0];
                        if (first.IsolationWindow.Contains(entry.PrecursorMz))
                        {
                            windowSpectra = kvp.Value;
                            break;
                        }
                    }
                    if (windowSpectra == null)
                        return null;
                }
            }
            else if (!windowSpectra[0].IsolationWindow.Contains(entry.PrecursorMz))
            {
                // Key collision where the actual isolation window doesn't contain this precursor.
                return null;
            }

            // Compute expected RT for this library entry.
            // Pass 1 (calibrationModel == null): use the linear pre-fit mapping
            //     rtSlope * library_rt + rtIntercept
            // Pass 2 (calibrationModel != null): use the LOESS-fitted prediction
            //     rtCalibration.Predict(library_rt)
            // Matches Rust's predict_fn pattern in pipeline.rs:740.
            double expectedRt = calibrationModel != null
                ? calibrationModel.Predict(entry.RetentionTime)
                : entry.RetentionTime * rtSlope + rtIntercept;

            // Diagnostic: record per-entry m/z + RT window selection.
            // C# selects ONE window per entry (the first match in dictionary order),
            // unlike Rust which scores in ALL matching windows. Capturing this here
            // before the RT/2-of-6 filter so it matches Rust's pre-filter dump.
            if (_ctx.Diagnostics?.CalWindowsCollecting ?? false)
            {
                _ctx.Diagnostics?.AddCalWindowRow(
                    entry, windowSpectra[0].IsolationWindow,
                    expectedRt,
                    expectedRt - initialTolerance,
                    expectedRt + initialTolerance);
            }

            // Resolve the actual window key that was used (may differ from primary
            // due to neighbour-key or linear-scan fallback).
            int resolvedWindowKey = windowKey;
            if (!spectraByWindowKey.ContainsKey(windowKey))
            {
                if (spectraByWindowKey.ContainsKey(windowKey - 1))
                    resolvedWindowKey = windowKey - 1;
                else if (spectraByWindowKey.ContainsKey(windowKey + 1))
                    resolvedWindowKey = windowKey + 1;
                else
                {
                    // Linear scan fallback - find the key that matched
                    foreach (var kvp in spectraByWindowKey)
                    {
                        if (kvp.Value == windowSpectra)
                        {
                            resolvedWindowKey = kvp.Key;
                            break;
                        }
                    }
                }
            }

            // Filter by RT tolerance and top-6 fragment prefilter.
            // Track window indices for preprocessed XCorr lookup.
            var candidateSpectra = new List<Spectrum>();
            var candidateWindowIndices = new List<int>();
            double[][] windowPreprocessed = null;
            preprocessedByWindowKey.TryGetValue(resolvedWindowKey, out windowPreprocessed);
            for (int si = 0; si < windowSpectra.Count; si++)
            {
                var spec = windowSpectra[si];
                if (Math.Abs(spec.RetentionTime - expectedRt) > initialTolerance)
                    continue;
                if (!FragmentMath.HasTopNFragmentMatch(entry, spec.Mzs, config.FragmentTolerance))
                    continue;
                candidateSpectra.Add(spec);
                candidateWindowIndices.Add(si);
            }

            if (candidateSpectra.Count < MIN_COELUTION_SPECTRA)
                return null;

            // Build shared RT axis for XIC extraction.
            int nScans = candidateSpectra.Count;
            double[] rts = new double[nScans];
            for (int i = 0; i < nScans; i++)
                rts[i] = candidateSpectra[i].RetentionTime;

            // Per-entry chromatogram diagnostic. Dump candidates + extracted XICs.
            // We default to pass 1 in OspreyDiagnostics because the cross-tool
            // bisection walks downstream: until pass 1 chromatograms match,
            // there's no point comparing pass 2 (which depends on pass 1's
            // LOESS fit).
            int currentPass = calibrationModel != null ? 2 : 1;

            // Extract XICs for the top-N most intense library fragments.
            var xics = TopFragmentExtractor.ExtractTopNFragmentXics(
                entry, candidateSpectra, rts, TopFragmentExtractor.CAL_TOP_N_FRAGMENTS, config);

            if (_ctx.Diagnostics?.ShouldDumpCalXicFor(entry.Id, currentPass) ?? false)
            {
                _ctx.Diagnostics?.WriteCalXicEntryDumpAndExit(
                    entry, currentPass, calibrationModel,
                    expectedRt, initialTolerance, rtSlope, rtIntercept,
                    candidateSpectra, xics);
            }

            if (xics.Count < 2)
                return null;

            // Detect consensus CWT peaks and score by pairwise correlation sum.
            var peaks = CwtPeakDetector.DetectConsensusPeaks(xics, 0.0);

            // Fallback: when CWT returns no consensus peaks, run DetectAllXicPeaks
            // on the reference fragment XIC alone. This rescues entries where
            // cross-fragment consensus is weak (one dominant fragment, noisy
            // others) but the reference has a clean peak shape. Matches Rust
            // batch.rs:2744-2751: the `cwt_candidates.is_empty()` branch.
            if (peaks.Count == 0)
            {
                // Pick the reference fragment (highest total intensity). Uses `>=`
                // so ties resolve to the LAST fragment, matching Rust's max_by.
                int refFallbackIdx = 0;
                double refTotal = -1.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double total = 0.0;
                    double[] inten = xics[i].Intensities;
                    for (int k = 0; k < inten.Length; k++) total += inten[k];
                    if (total >= refTotal)
                    {
                        refTotal = total;
                        refFallbackIdx = i;
                    }
                }
                var fallbackXic = xics[refFallbackIdx];
                peaks = PeakDetector.DetectAllXicPeaks(
                    fallbackXic.RetentionTimes,
                    fallbackXic.Intensities,
                    0.01, // min_height
                    5.0); // peak_boundary
            }

            if (peaks.Count == 0)
                return null;

            // Score each candidate peak by its summed pairwise fragment-XIC
            // correlation; keep the highest (ties -> last, matching Rust max_by).
            var (bestPeak, bestCorrSum) = ScorePeaksByCorrelation(peaks, xics);

            if (bestPeak == null || bestCorrSum < MIN_COELUTION_CORR_SCORE)
                return null;

            // Identify the reference XIC - the single fragment with the highest
            // total intensity across the extracted XICs. This is the signal that
            // feeds SNR computation and the apex selection. Direct port of
            // Rust's `ref_idx = xics.max_by(total intensity)` in batch.rs:~2718.
            //
            // Note: Rust's `Iterator::max_by` returns the LAST element on ties,
            // so we use `>=` (not `>`) here to match. Without this, fragments
            // with identical total intensities would select different reference
            // XICs between the two tools, causing downstream apex divergence.
            int refIdx = 0;
            double bestTotalIntensity = -1.0;
            for (int i = 0; i < xics.Count; i++)
            {
                double total = 0.0;
                double[] inten = xics[i].Intensities;
                for (int k = 0; k < inten.Length; k++)
                    total += inten[k];
                if (total >= bestTotalIntensity)
                {
                    bestTotalIntensity = total;
                    refIdx = i;
                }
            }
            var refXic = xics[refIdx];
            double[] refIntensities = refXic.Intensities;

            // Apex is the highest-intensity point within the peak boundaries of
            // the reference XIC. No top-6 constraint: Rust's batch.rs:2797-2802
            // is a straight argmax over `ref_xic[ref_start..=ref_end]`. Uses `>=`
            // so ties resolve to the LAST index, matching Rust `max_by`.
            int apexLocalIdx = bestPeak.StartIndex;
            double apexVal = refIntensities[Math.Min(apexLocalIdx, refIntensities.Length - 1)];
            for (int scan = bestPeak.StartIndex; scan <= bestPeak.EndIndex; scan++)
            {
                if (scan >= refIntensities.Length)
                    break;
                if (refIntensities[scan] >= apexVal)
                {
                    apexVal = refIntensities[scan];
                    apexLocalIdx = scan;
                }
            }

            // Apex RT from the reference XIC (shared time axis across fragments).
            double apexRt = refXic.RetentionTimes[apexLocalIdx];
            measuredRt = apexRt;

            // SNR is computed on the reference fragment's raw intensities
            // (NOT the composite sum). Direct port of Rust batch.rs:2803-2806.
            signalToNoise = PeakDetector.ComputeSnr(
                refIntensities, apexLocalIdx, bestPeak.StartIndex, bestPeak.EndIndex);

            // Map the apex RT to the candidate spectrum with the closest RT
            // for feature computation. In practice this returns apexLocalIdx
            // since the XIC time axis is built directly from candidate spectrum
            // RTs, but we port Rust's lookup verbatim for parity.
            int apexSpecLocalIdx = 0;
            double bestDt = Math.Abs(candidateSpectra[0].RetentionTime - apexRt);
            for (int i = 1; i < candidateSpectra.Count; i++)
            {
                double dt = Math.Abs(candidateSpectra[i].RetentionTime - apexRt);
                if (dt < bestDt)
                {
                    bestDt = dt;
                    apexSpecLocalIdx = i;
                }
            }
            var apexSpectrum = candidateSpectra[apexSpecLocalIdx];

            // Compute the four LDA features at the apex.
            double libCosineApex = scorer.LibCosine(
                apexSpectrum, entry, config.FragmentTolerance);
            // XCorr at apex always uses the calibration unit-bin scorer
            // (matches the pre-preprocessed arrays built with s_calXcorrScorer).
            int apexWindowIdx = candidateWindowIndices[apexSpecLocalIdx];
            double xcorrApex = (windowPreprocessed != null && apexWindowIdx < windowPreprocessed.Length)
                ? s_calXcorrScorer.XcorrFromPreprocessed(windowPreprocessed[apexWindowIdx], entry)
                : s_calXcorrScorer.XcorrAtScan(apexSpectrum, entry);
            byte top6Matched = TopFragmentExtractor.CountTop6Matches(entry, apexSpectrum, config);

            // Collect MS2 fragment mass errors at apex for m/z calibration.
            var ms2Errors = CollectMs2FragmentErrors(entry, apexSpectrum, config);

            // MS1 precursor mass error at apex for MS1 mass calibration.
            double? ms1Error = ComputeMs1MassError(entry, ms1Spectra, apexRt, config);

            // Median-polish library cosine (OSPREY_CAL_MEDIANPOLISH lever): the dominant
            // full-search Percolator feature, computed over the same peak-cropped top-N
            // XICs the calibrator already extracted -- the crop mirrors the full search's
            // CoelutionScorer.ScoreCandidate. Off by default (no compute, no output change).
            double medianPolishCosine = 0.0;
            if (OspreyEnvironment.CalMedianPolishFeature)
                medianPolishCosine = ComputeCalibrationMedianPolishCosine(entry, xics, bestPeak);

            return new CalibrationMatch
            {
                EntryId = entry.Id,
                IsDecoy = entry.IsDecoy,
                // Entrapment class recovered from the library accessions (FDRBench
                // _p_target marker). Pure diagnostic tag for the --verbose anchor-purity
                // report; unused by scoring/selection, so it leaves output unchanged. On a
                // library with no entrapment markers this is simply always false.
                IsEntrapment = EntrapmentLibraryClassifier.IsEntrapment(entry.ProteinIds),
                Sequence = entry.Sequence,
                ScanNumber = apexSpectrum.ScanNumber,
                CorrelationScore = bestCorrSum,
                LibcosineApex = libCosineApex,
                Top6MatchedApex = top6Matched,
                XcorrScore = xcorrApex,
                IsotopeCosine = 0.0,
                MedianPolishCosine = medianPolishCosine,
                DiscriminantScore = bestCorrSum,
                QValue = 1.0,
                Ms2MassErrors = ms2Errors.ToArray(),
                Ms1Error = ms1Error
            };
        }

        /// <summary>
        /// Score each candidate CWT peak by the sum of pairwise Pearson
        /// correlations between fragment XICs over the peak's index range,
        /// and return the highest-scoring peak (ties resolve to the last,
        /// matching Rust's max_by) with its correlation sum. bestPeak is null
        /// when no peak spans at least three points.
        /// </summary>
        private static (XICPeakBounds BestPeak, double BestCorrSum) ScorePeaksByCorrelation(
            List<XICPeakBounds> peaks, List<XicData> xics)
        {
            XICPeakBounds bestPeak = null;
            double bestCorrSum = double.NegativeInfinity;

            foreach (var peak in peaks)
            {
                int si = peak.StartIndex;
                int ei = peak.EndIndex;
                if (ei - si + 1 < 3)
                    continue;

                double corrSum = 0.0;
                for (int i = 0; i < xics.Count; i++)
                {
                    double[] inti = xics[i].Intensities;
                    for (int j = i + 1; j < xics.Count; j++)
                    {
                        double[] intj = xics[j].Intensities;
                        double corr = ScoringMath.PearsonOverRange(inti, intj, si, ei);
                        if (!double.IsNaN(corr))
                            corrSum += corr;
                    }
                }

                // Use >= to match Rust's max_by tie-break (last wins on ties).
                if (corrSum >= bestCorrSum)
                {
                    bestCorrSum = corrSum;
                    bestPeak = peak;
                }
            }
            return (bestPeak, bestCorrSum);
        }

        /// <summary>
        /// Median-polish library cosine for a calibration candidate: crops the top-N
        /// fragment XICs to the chosen peak and runs the same Tukey median-polish + library
        /// cosine the full search uses (CoelutionScorer.ScoreCandidate, maxIter=10, tol=0.01).
        /// XicData.FragmentIndex carries the raw entry.Fragments index, which LibCosine maps
        /// back to library intensities. Returns 0.0 when the peak is too short (&lt; 3 scans)
        /// or the fit is degenerate. Only called under the OSPREY_CAL_MEDIANPOLISH lever.
        /// </summary>
        private static double ComputeCalibrationMedianPolishCosine(
            LibraryEntry entry, List<XicData> xics, XICPeakBounds bestPeak)
        {
            if (xics == null || xics.Count < 2 || bestPeak == null)
                return 0.0;
            int peakLen = bestPeak.EndIndex - bestPeak.StartIndex + 1;
            if (peakLen < 3)
                return 0.0;

            double[] fullRts = xics[0].RetentionTimes;
            if (bestPeak.StartIndex < 0 || bestPeak.EndIndex >= fullRts.Length)
                return 0.0;

            var peakRts = new double[peakLen];
            for (int s = 0; s < peakLen; s++)
                peakRts[s] = fullRts[bestPeak.StartIndex + s];

            var peakXics = new List<KeyValuePair<int, double[]>>(xics.Count);
            foreach (var xic in xics)
            {
                double[] src = xic.Intensities;
                if (src == null || bestPeak.EndIndex >= src.Length)
                    continue;
                var slice = new double[peakLen];
                for (int s = 0; s < peakLen; s++)
                    slice[s] = src[bestPeak.StartIndex + s];
                peakXics.Add(new KeyValuePair<int, double[]>(xic.FragmentIndex, slice));
            }
            if (peakXics.Count < 2)
                return 0.0;

            var polish = TukeyMedianPolish.Compute(peakXics, peakRts, 10, 0.01);
            return TukeyMedianPolish.LibCosine(polish, entry.Fragments);
        }

        /// <summary>
        /// Collect MS2 fragment mass errors at the apex spectrum for m/z
        /// calibration. Matches Rust topn_fragment_match_with_errors: uses the
        /// TOP-6 fragments by intensity (stable sort to match Rust's tie-break),
        /// matches each within the fragment tolerance, and reports the mass
        /// error of the closest peak in fragment-tolerance units.
        /// </summary>
        private static List<double> CollectMs2FragmentErrors(
            LibraryEntry entry, Spectrum apexSpectrum, OspreyConfig config)
        {
            var ms2Errors = new List<double>();
            int[] topErrorIndices = TopFragmentExtractor.SelectTopFragmentIndices(
                entry.Fragments, TopFragmentExtractor.CAL_TOP_N_FRAGMENTS);

            foreach (int fragIdx in topErrorIndices)
            {
                var frag = entry.Fragments[fragIdx];
                double tolDa = config.FragmentTolerance.ToleranceDa(frag.Mz);
                double lower = frag.Mz - tolDa;
                double upper = frag.Mz + tolDa;

                int best = TopFragmentExtractor.FindClosestPeakInWindow(
                    apexSpectrum.Mzs, frag.Mz, lower, upper);
                if (best >= 0)
                    ms2Errors.Add(config.FragmentTolerance.MassError(frag.Mz, apexSpectrum.Mzs[best]));
            }
            return ms2Errors;
        }

        /// <summary>
        /// MS1 precursor mass error at the apex RT for MS1 mass calibration.
        /// Port of osprey-scoring/src/batch.rs:2912-2940 -- extract M+0 from the
        /// MS1 scan closest to the apex RT using the config precursor tolerance;
        /// report the error in fragment-tolerance units so MS1 + MS2 errors
        /// share the same unit for the MzQCData aggregator. Returns null when
        /// there are no MS1 spectra or no M+0 isotope peak is found.
        /// </summary>
        private double? ComputeMs1MassError(
            LibraryEntry entry, List<MS1Spectrum> ms1Spectra, double apexRt, OspreyConfig config)
        {
            double? ms1Error = null;
            if (ms1Spectra != null && ms1Spectra.Count > 0)
            {
                int charge = entry.Charge > 0 ? entry.Charge : 1;
                double precursorTolPpm = config.PrecursorTolerance != null
                    && config.PrecursorTolerance.Unit == ToleranceUnit.Ppm
                    ? config.PrecursorTolerance.Tolerance
                    : 10.0;
                var apexMs1 = ScoringTaskShared.FindNearestMs1(ms1Spectra, apexRt);
                if (apexMs1 != null)
                {
                    var envelope = IsotopeEnvelope.Extract(
                        apexMs1, entry.PrecursorMz, charge, precursorTolPpm);
                    if (envelope.HasM0 && envelope.M0ObservedMz.HasValue)
                    {
                        ms1Error = config.FragmentTolerance.MassError(
                            entry.PrecursorMz, envelope.M0ObservedMz.Value);
                    }
                }
            }
            return ms1Error;
        }
    }
}
