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
            // Total matches scored in this pass (before any q-value or S/N
            // filtering). Plumbed into CalibrationMetadata.NumSampledPrecursors
            // for parity with Rust's accumulated_matches.len().
            public int MatchCount;
            // (lib_rt, measured_rt) pairs that were actually fed to the LOESS
            // fit for this pass. Exposed so the caller can emit the
            // OSPREY_DUMP_LOESS_INPUT diagnostic only for the pass whose
            // calibration is actually used (pass 1 always; pass 2 only on
            // acceptance) -- mirroring Rust pipeline.rs's "dump reflects
            // the calibration actually used" semantics.
            public double[] LibRts;
            public double[] MeasuredRts;
        }

        private readonly PipelineContext _ctx;

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

            foreach (var entry in library)
            {
                if (!entry.IsDecoy)
                {
                    if (entry.RetentionTime < libMinRt)
                        libMinRt = entry.RetentionTime;
                    if (entry.RetentionTime > libMaxRt)
                        libMaxRt = entry.RetentionTime;
                }
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

            // Sample library entries (paired target+decoy). Use seed 43 to match
            // Rust's sample_library_for_calibration(..., 42 + attempt=1) on the
            // first calibration attempt.
            var swSample = Stopwatch.StartNew();
            var sampledEntries = SampleLibraryForCalibration(
                library, config.RtCalibration.CalibrationSampleSize, 43UL, _ctx.Diagnostics);
            swSample.Stop();

            // Diagnostic: dump sorted sampled entry IDs + (modseq, charge) for
            // direct comparison with Rust. Abort after dump if CAL_SAMPLE_ONLY
            // env var is set (bisection mode - stop once we agree here).
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
                "[TIMING] Calibration sampling: {0:F2}s ({1} targets + {2} decoys)",
                swSample.Elapsed.TotalSeconds, nSampledTargets, nSampledDecoys));

            if (nSampledTargets == 0)
            {
                _ctx.LogWarning("No target entries available for calibration sampling.");
                ms1Calibration = MzCalibrationResult.Uncalibrated();
                ms2Calibration = MzCalibrationResult.Uncalibrated();
                return null;
            }

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

            // Pass 1: score all sampled entries with the linear pre-fit RT mapping
            // and the wide initial tolerance. Fits a LOESS RTCalibration from the
            // LDA + S/N surviving targets.
            var pass1 = RunCalibrationScoringPass(
                1,
                sampledEntries, spectraByWindowKey, ms1Spectra, context,
                rtSlope, rtIntercept, initialTolerance,
                null /* calibrationModel: pass 1 uses linear mapping */,
                config.RtCalibration.MinCalibrationPoints);

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

            // Match Rust accumulated_matches.len() semantics: report pass 1's
            // total scored matches (before any q-value / S/N filtering).
            // Pass 2 is a refinement using narrowed RT tolerance and does not
            // change the sampled-precursor count.
            numSampledPrecursors = pass1.MatchCount;

            // === Iterative calibration refinement (2-pass) ===
            // Mirrors Rust pipeline.rs:714-839.
            // MAD * 1.4826 ~ SD for a normal distribution; 3* that covers ~99.7%.
            double madTolerance = pass1.Stats.MAD * 1.4826 * 3.0;
            double pass1Tolerance = Math.Max(
                config.RtCalibration.MinRtTolerance,
                Math.Min(config.RtCalibration.MaxRtTolerance, madTolerance));

            _ctx.LogVerbose(string.Format(
                "First-pass RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                pass1Tolerance,
                pass1.Stats.MAD,
                pass1.Stats.MAD * 1.4826,
                pass1.Stats.ResidualSD,
                pass1.Stats.NPoints,
                pass1.Stats.RSquared));

            // Only refine if the tolerance narrowed at least 2* tighter than the
            // initial wide window.
            if (pass1Tolerance < initialTolerance * 0.5)
            {
                _ctx.LogVerbose(string.Format(
                    "Calibration refinement: re-scoring with {0:F2} min tolerance (was {1:F1} min)",
                    pass1Tolerance, initialTolerance));

                var pass2 = RunCalibrationScoringPass(
                    2,
                    sampledEntries, spectraByWindowKey, ms1Spectra, context,
                    rtSlope, rtIntercept, pass1Tolerance,
                    pass1.Calibration /* pass 2 predicts RT via the LOESS fit */,
                    ABSOLUTE_MIN_CALIBRATION_POINTS);

                if (pass2 != null)
                {
                    double refinedMadTolerance = pass2.Stats.MAD * 1.4826 * 3.0;
                    double refinedTolerance = Math.Max(
                        config.RtCalibration.MinRtTolerance,
                        Math.Min(config.RtCalibration.MaxRtTolerance, refinedMadTolerance));

                    _ctx.LogVerbose(string.Format(
                        "Refined RT tolerance: {0:F2} min (MAD={1:F3}, robust_SD={2:F3}, residual_SD={3:F3}, {4} points, R^2={5:F4})",
                        refinedTolerance,
                        pass2.Stats.MAD,
                        pass2.Stats.MAD * 1.4826,
                        pass2.Stats.ResidualSD,
                        pass2.Stats.NPoints,
                        pass2.Stats.RSquared));

                    // Accept the refined calibration only if R^2 didn't degrade
                    // by more than 1% (matches Rust pipeline.rs:811).
                    if (pass2.Stats.RSquared >= pass1.Stats.RSquared * 0.99)
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

        /// <summary>
        /// Run one calibration scoring pass: score each sampled entry, train LDA,
        /// apply S/N filter, and fit LOESS on the surviving (libRt, measuredRt) pairs.
        /// Returns null if the pass has fewer than minLoessPoints survivors or the
        /// LOESS fit fails. This helper is called twice by RunCalibration to
        /// implement the two-pass refinement (pipeline.rs:714-839).
        /// </summary>
        private CalibrationPassResult RunCalibrationScoringPass(
            int passNumber,
            List<LibraryEntry> sampledEntries,
            Dictionary<int, List<Spectrum>> spectraByWindowKey,
            List<MS1Spectrum> ms1Spectra,
            ScoringContext context,
            double rtSlope, double rtIntercept, double tolerance,
            RTCalibration calibrationModel,
            int minLoessPoints)
        {
            var config = context.Config;
            var fileName = context.FileName;
            // Activate per-entry window dump if requested. Cleared after the
            // matching loop completes (file written below).
            _ctx.Diagnostics?.StartCalWindowCollection();

            // Pre-preprocess all window spectra for XCorr (f32 unit-bin scorer).
            var preprocessedByWindowKey = PreprocessWindowsForXcorr(spectraByWindowKey);

            // Parallel score each sampled entry (timing + match-count logs inside).
            var (matches, snrByEntryId, matchRts) = ScoreCalibrationMatches(
                passNumber, sampledEntries, spectraByWindowKey, preprocessedByWindowKey,
                ms1Spectra, context, rtSlope, rtIntercept, tolerance, calibrationModel);

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

            if (matches.Count == 0)
            {
                _ctx.LogWarning(string.Format(
                    "No calibration matches could be scored in pass {0}.", passNumber));
                return null;
            }

            // Train LDA + 1% FDR target-decoy competition.
            var swLda = Stopwatch.StartNew();
            var matchArray = matches.ToArray();
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
            int nPassing = CalibrationScorer.TrainAndScoreCalibration(matchArray, false);
            swLda.Stop();

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
            var (libRtsDetected, measuredRtsDetected) = CollectCalibrationPoints(
                matchArray, matchRts, snrByEntryId, passNumber, fileName, nTargetWins);
            _ctx.LogInfo(string.Format(
                "Calibration pass {0}: {1} RT calibration points (from {2} peptides at 1% FDR)",
                passNumber, libRtsDetected.Count, nPassing));

            if (libRtsDetected.Count < minLoessPoints)
            {
                _ctx.LogWarning(string.Format(
                    "Insufficient calibration points in pass {0} ({1} < {2}).",
                    passNumber, libRtsDetected.Count, minLoessPoints));
                return null;
            }

            // Aggregate MS1 + MS2 mass errors from passing targets only
            // (same LDA + competition + S/N survivors as the RT points;
            // emits the MS2 cal-errors dump + the MS1/MS2 calibration logs).
            AggregateMassCalibrations(
                matchArray, snrByEntryId, config, passNumber,
                out var ms1Cal, out var ms2Cal);

            // Fit LOESS calibration.
            var swLoess = Stopwatch.StartNew();
            try
            {
                double[] libRts = libRtsDetected.ToArray();
                double[] measuredRts = measuredRtsDetected.ToArray();

                var calibratorConfig = new RTCalibratorConfig
                {
                    Bandwidth = config.RtCalibration.LoessBandwidth,
                    Degree = 1,
                    MinPoints = Math.Min(20, libRts.Length),
                    RobustnessIterations = 2,
                    OutlierRetention = 1.0, // LDA + S/N already filtered
                    ClassicalRobustIterations = OspreyEnvironment.LoessClassicalRobust
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
                    MatchCount = matchArray.Length,
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
            ConcurrentDictionary<uint, KeyValuePair<double, double>> matchRts,
            ConcurrentDictionary<uint, double> snrByEntryId,
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

                KeyValuePair<double, double> rtPair;
                if (!matchRts.TryGetValue(m.EntryId, out rtPair))
                    continue;

                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr))
                    snr = 0.0;

                if (snr < MIN_SNR_FOR_RT_CAL)
                {
                    nSnrFiltered++;
                    continue;
                }

                libRtsDetected.Add(rtPair.Key);
                measuredRtsDetected.Add(rtPair.Value);
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
            ConcurrentDictionary<uint, double> snrByEntryId,
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
                double snr;
                if (!snrByEntryId.TryGetValue(m.EntryId, out snr) || snr < MIN_SNR_FOR_RT_CAL)
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
                "MS1 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
                passNumber, ms1Calibration.Mean, unitStr, ms1Calibration.SD, 3.0 * ms1Calibration.SD, allMs1Errors.Count));
            _ctx.LogVerbose(string.Format(
                "MS2 calibration (pass {0}): mean={1:F4} {2}, SD={3:F4} {2}, 3*SD={4:F4} {2} ({5} errors)",
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

            return new CalibrationMatch
            {
                EntryId = entry.Id,
                IsDecoy = entry.IsDecoy,
                Sequence = entry.Sequence,
                ScanNumber = apexSpectrum.ScanNumber,
                CorrelationScore = bestCorrSum,
                LibcosineApex = libCosineApex,
                Top6MatchedApex = top6Matched,
                XcorrScore = xcorrApex,
                IsotopeCosine = 0.0,
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
