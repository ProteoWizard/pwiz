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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Per-window + per-candidate coelution scoring engine. Relocated verbatim
    /// out of AbstractScoringTask, which carried these as instance members
    /// coupled to the exe-layer PipelineContext for logging and to
    /// OspreyDiagnostics for the per-entry dump sink. Those two ambient
    /// dependencies are now injected: logging via an <see cref="Action{T}"/>
    /// sink, and the dump sink via <see cref="IScoringDiagnostics"/> (nullable;
    /// invoked null-conditionally). The arithmetic, Parallel usage, CWT / peak /
    /// feature logic, and tie-breaks are unchanged, so cross-impl parity is
    /// unaffected.
    ///
    /// Data production (RT window, XICs, peak detection, apex/reference-XIC
    /// resolution) is delegated to <see cref="PeakDataExtractor"/> -- the producer
    /// seam mirroring Skyline's results layer. This class composes the extractor,
    /// then runs the 21-feature calculator pass over the resulting peak-data view
    /// and assembles the FdrEntry; it no longer interleaves data preparation with
    /// calculator dispatch.
    /// </summary>
    public class CoelutionScorer
    {
        // Most candidates whose XICs ScoreWindow extracts scan-major at once, and most bytes of
        // XICs: a block ends at whichever comes first. At ~6 fragments x ~100-300 scans each,
        // 512 candidates' XICs are a few MB, but a wide RT tolerance (a failed calibration's
        // 2 min fallback, or a 3 min maximum) makes each several times longer, so the byte
        // budget is what bounds the XICs alive per concurrently scored window.
        private const int XIC_BLOCK_SIZE = 512;
        private const long XIC_BLOCK_BYTES = 4L * 1024 * 1024;

        // The correlation-shape features that read a byproduct the scorer publishes for them over the
        // wider-tolerance XICs (OSPREY_CORR_FRAGMENT_TOLERANCE): the median-polish fit over those XICs,
        // and the runner-up candidates by their coelution over them. Each is computed only when a
        // feature that reads it is.
        private static readonly string[] CORRELATION_POLISH_READERS =
        {
            @"mp_fragment_r2_rank2", @"mp_fragment_r2_rank3", @"mp_fragment_r2_rank4",
            @"mp_fragment_r2_top3_mean", @"mp_fragment_r2_median", @"mp_n_fragments_good_fit",
            @"top3_ref_n_corr_good", @"top3_mp_min_r2", @"top3_mp_r2_median", @"top3_coelution_min",
        };
        private static readonly string[] CORRELATION_CANDIDATE_READERS =
        {
            @"peak_coelution_margin",
            @"wide_coelution_sum", @"wide_n_coeluting", @"wide_coelution_margin", @"wide_top3_coelution_min",
        };

        private readonly IScoringDiagnostics _diagnostics;  // nullable by contract; invoked null-conditionally
        private readonly PeakDataExtractor _extractor;
        private readonly bool[] _recordedFeatures;   // null computes every feature
        private readonly bool _correlationPolish;    // A computed feature reads the wider-XIC median polish

        /// <summary>
        /// A scorer that computes every PIN feature, or, when <paramref name="recordedFeatures"/> is
        /// given, only the features it flags beyond the first <see cref="OspreyFeatureCalculators.CORE_FEATURE_COUNT"/>,
        /// which are always computed. An unflagged feature is left 0 in the vector and is not
        /// written to the scores parquet (<c>PinFeatureMask.IsRecorded</c> decides both).
        /// </summary>
        public CoelutionScorer(IScoringDiagnostics diagnostics, bool[] recordedFeatures = null)
        {
            if (recordedFeatures != null && recordedFeatures.Length != OspreyFeatureCalculators.FeatureCount)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new ArgumentException(string.Format(@"Expected {0} recorded-feature flags, not {1}",
                    OspreyFeatureCalculators.FeatureCount, recordedFeatures.Length), nameof(recordedFeatures));
            }
            _diagnostics = diagnostics;
            _recordedFeatures = recordedFeatures;
            _correlationPolish = AnyComputed(CORRELATION_POLISH_READERS);
            _extractor = new PeakDataExtractor(diagnostics, AnyComputed(CORRELATION_CANDIDATE_READERS));
        }


        /// <summary>
        /// Score all candidate library entries within a single isolation window.
        /// For each candidate:
        /// 1. Extract fragment XICs from spectra in this window
        /// 2. Detect consensus CWT peaks
        /// 3. Score XCorr and LibCosine at the best peak apex
        /// 4. Build feature set and create FdrEntry
        /// </summary>
        public List<FdrEntry> ScoreWindow(
            IsolationWindow window,
            List<LibraryEntry> fullLibrary,
            List<Spectrum> windowSpectra,
            List<MS1Spectrum> ms1Spectra,
            RTCalibration rtCalibration,
            MzCalibrationResult ms1Calibration,
            double globalRtTolerance,
            double rtSigma,
            SpectralScorer scorer,
            ScoringContext context)
        {
            var work = OpenWindow(window, fullLibrary, windowSpectra, ms1Spectra, rtCalibration,
                ms1Calibration, globalRtTolerance, rtSigma, scorer, context);
            try
            {
                while (work.TryScoreNextBlock(out _))
                {
                }
                return work.Entries;
            }
            finally
            {
                work.Release();
            }
        }

        /// <summary>
        /// Prepare the scoring of one window's candidates, as <see cref="ScoreWindow"/> scores them,
        /// in blocks of consecutive candidates that any number of threads may score at once through
        /// <see cref="WindowWork.TryScoreNextBlock"/>. The window's entries are those blocks' entries
        /// in candidate order, the same list in the same order whichever threads scored them.
        /// </summary>
        internal WindowWork OpenWindow(
            IsolationWindow window,
            List<LibraryEntry> fullLibrary,
            List<Spectrum> windowSpectra,
            List<MS1Spectrum> ms1Spectra,
            RTCalibration rtCalibration,
            MzCalibrationResult ms1Calibration,
            double globalRtTolerance,
            double rtSigma,
            SpectralScorer scorer,
            ScoringContext context)
        {
            // The caller (RunCoelutionScoring) resolves this window's calibrated
            // spectra from the IWindowSpectraProvider by key; an absent/empty
            // window yields no scored entries, as before.
            if (windowSpectra == null || windowSpectra.Count == 0)
                return new WindowWork();

            // Sort spectra by RT for XIC extraction, with ScanNumber as a unique
            // secondary key so the comparator never returns 0 -- an allocation-free
            // total order that matches Rust's stable slice::sort_by on the scan-ordered
            // window (tie hazard #4362).
            windowSpectra.Sort((a, b) => // Array.Sort OK: (RetentionTime, ScanNumber) is a unique total order (ScanNumber unique per scan), so the comparator never ties
            {
                int byRt = a.RetentionTime.CompareTo(b.RetentionTime);
                return byRt != 0 ? byRt : a.ScanNumber.CompareTo(b.ScanNumber);
            });

            // Find candidate library entries whose precursor m/z falls in this window.
            // No minimum fragment count filter - matches Rust which scores all entries.
            var candidates = new List<LibraryEntry>();
            foreach (var entry in fullLibrary)
            {
                if (entry.Fragments == null || entry.Fragments.Count == 0)
                    continue;
                if (window.Contains(entry.PrecursorMz))
                    candidates.Add(entry);
            }

            if (candidates.Count == 0)
                return new WindowWork();

            return new WindowWork(this, candidates, windowSpectra, ms1Spectra, rtCalibration,
                ms1Calibration, globalRtTolerance, rtSigma, scorer, context);
        }

        /// <summary>
        /// One window's candidates, prepared for scoring in blocks of consecutive candidates: the
        /// window's sorted spectra, its XCorr cache, and the signal prefilter run for all of them at
        /// once. Each block is scored with its own scoring context, peak-data view and
        /// <see cref="WindowXcorrCache.ForLane"/> cache view, so blocks share only state that is
        /// read-only once the window is open, and a candidate scores the same values on any thread.
        /// </summary>
        internal sealed class WindowWork
        {
            private readonly CoelutionScorer _scorer;
            private readonly List<LibraryEntry> _candidates;
            private readonly List<Spectrum> _windowSpectra;
            private readonly double[] _windowRts;
            private readonly List<MS1Spectrum> _ms1Spectra;
            private readonly RTCalibration _rtCalibration;
            private readonly MzCalibrationResult _ms1Calibration;
            private readonly double _globalRtTolerance;
            private readonly double _rtSigma;
            private readonly SpectralScorer _spectralScorer;
            private readonly ScoringContext _context;
            private readonly WindowPrefilter _prefilter;
            private readonly bool _scanMajorXics;
            private readonly int[] _blockEnds;          // Exclusive end of each block; block b starts at _blockEnds[b - 1]
            private readonly List<FdrEntry>[] _blockEntries;
            private WindowXcorrCache _preprocessedXcorr;
            private int _nextBlock;
            private int _completedBlocks;
            private int _released;

            /// <summary>A window with no candidates to score: complete, with no entries.</summary>
            public WindowWork()
            {
                _blockEnds = Array.Empty<int>();
                Entries = new List<FdrEntry>();
            }

            public WindowWork(CoelutionScorer scorer, List<LibraryEntry> candidates, List<Spectrum> windowSpectra,
                List<MS1Spectrum> ms1Spectra, RTCalibration rtCalibration, MzCalibrationResult ms1Calibration,
                double globalRtTolerance, double rtSigma, SpectralScorer spectralScorer, ScoringContext context)
            {
                _scorer = scorer;
                _candidates = candidates;
                _windowSpectra = windowSpectra;
                _ms1Spectra = ms1Spectra;
                _rtCalibration = rtCalibration;
                _ms1Calibration = ms1Calibration;
                _globalRtTolerance = globalRtTolerance;
                _rtSigma = rtSigma;
                _spectralScorer = spectralScorer;
                _context = context;

                // Build RT array for this window
                _windowRts = new double[windowSpectra.Count];
                for (int i = 0; i < windowSpectra.Count; i++)
                    _windowRts[i] = windowSpectra[i].RetentionTime;

                // The window's XCorr cache (see IResolutionStrategy.PreprocessWindowSpectra);
                // released once all candidates for this window are scored.
                _preprocessedXcorr = context.Resolution.PreprocessWindowSpectra(
                    windowSpectra, spectralScorer, context.XcorrScratchPool);
                WindowPrefilter prefilter;
                try
                {
                    // Run the signal prefilter for all candidates at once, scan-major, so each
                    // spectrum is matched against every candidate while it is in cache. Null when
                    // the prefilter is off (or OSPREY_SCAN_MAJOR_PREFILTER=0), leaving each
                    // candidate's own TryExtract to run it as before.
                    prefilter = OspreyEnvironment.ScanMajorPrefilter
                        ? scorer._extractor.ComputePrefilterScanMajor(candidates, windowSpectra, _windowRts,
                            rtCalibration, globalRtTolerance, context)
                        : null;
                }
                catch
                {
                    Release();
                    throw;
                }
                // The passing candidates' fragment XICs, also extracted scan-major, one block of
                // consecutive candidates at a time to bound the XICs alive at once. Without them
                // the window is one block.
                _prefilter = prefilter;
                _scanMajorXics = prefilter != null && OspreyEnvironment.ScanMajorXic;
                var blockEnds = new List<int>();
                int blockEnd;
                for (int blockStart = 0; blockStart < candidates.Count; blockStart = blockEnd)
                {
                    blockEnd = prefilter != null && _scanMajorXics
                        ? prefilter.NextXicBlockEnd(blockStart, XIC_BLOCK_SIZE, XIC_BLOCK_BYTES)
                        : candidates.Count;
                    blockEnds.Add(blockEnd);
                }
                _blockEnds = blockEnds.ToArray();
                _blockEntries = new List<FdrEntry>[_blockEnds.Length];
            }

            /// <summary>The window's entries in candidate order, once every block is scored; else null.</summary>
            public List<FdrEntry> Entries { get; private set; }

            /// <summary>True once every block is scored, and <see cref="Entries"/> set.</summary>
            public bool IsComplete
            {
                get { return Entries != null; }
            }

            /// <summary>The number of blocks the window's candidates score in.</summary>
            public int BlockCount
            {
                get { return _blockEnds.Length; }
            }

            /// <summary>True while a block remains that no thread has started.</summary>
            public bool HasUnstartedBlocks
            {
                get { return Volatile.Read(ref _nextBlock) < _blockEnds.Length; }
            }

            /// <summary>
            /// Score the next block no thread has started, if any. False when none remains.
            /// <paramref name="completedWindow"/> is true for the one call, on whichever thread,
            /// that scores the window's last block to finish: <see cref="Entries"/> is then set
            /// and the window's XCorr cache released.
            /// </summary>
            public bool TryScoreNextBlock(out bool completedWindow)
            {
                completedWindow = false;
                int block = Interlocked.Increment(ref _nextBlock) - 1;
                if (block >= _blockEnds.Length)
                    return false;

                _blockEntries[block] = ScoreBlock(block == 0 ? 0 : _blockEnds[block - 1], _blockEnds[block]);
                if (Interlocked.Increment(ref _completedBlocks) == _blockEnds.Length)
                {
                    var entries = new List<FdrEntry>();
                    for (int b = 0; b < _blockEntries.Length; b++)
                    {
                        entries.AddRange(_blockEntries[b]);
                        _blockEntries[b] = null;
                    }
                    Release();
                    Entries = entries;
                    completedWindow = true;
                }
                return true;
            }

            /// <summary>Return the window's XCorr cache scratch to the pool; safe to call more than once.</summary>
            public void Release()
            {
                if (_preprocessedXcorr != null && Interlocked.Exchange(ref _released, 1) == 0)
                    _context.Resolution.ReleaseWindowCache(_preprocessedXcorr, _context.XcorrScratchPool);
            }

            private List<FdrEntry> ScoreBlock(int blockStart, int blockEnd)
            {
                var entries = new List<FdrEntry>();
                var config = _context.Config;
                var laneXcorr = _preprocessedXcorr?.ForLane(_context.XcorrScratchPool);
                try
                {
                    // Reused scoring context + peak-data adapter for the modular feature
                    // calculators. The per-candidate loop mutates them in place (ClearByproducts +
                    // Set) so scoring allocates no context / peak-data per candidate. A block is
                    // scored on one thread, so these are block-local, never shared.
                    var ospreyContext = new OspreyScoringContext(config);
                    ospreyContext.SetWindow(_context.Resolution, laneXcorr, _spectralScorer,
                        _context.XcorrScratchPool);
                    var ospreyPeakData = new OspreyPeakData();
                    // The correlation-shape features' context and view over the wider-tolerance XICs, when
                    // OSPREY_CORR_FRAGMENT_TOLERANCE sets one (ScoringContext.CorrelationConfig).
                    var corrScoring = _context.CorrelationConfig != null
                        ? new CorrelationScoring(_context.CorrelationConfig, _context, laneXcorr, _spectralScorer,
                            _scorer._correlationPolish)
                        : null;

                    List<XicData>[] blockCorrXics = null;
                    var blockXics = _scanMajorXics
                        ? PeakDataExtractor.ExtractXicsScanMajor(_candidates, blockStart, blockEnd,
                            _prefilter, _windowSpectra, _windowRts, config, _context.CorrelationConfig, out blockCorrXics)
                        : null;
                    for (int c = blockStart; c < blockEnd; c++)
                    {
                        List<XicData> precomputedXics = null;
                        List<XicData> precomputedCorrXics = null;
                        if (blockXics != null)
                        {
                            precomputedXics = blockXics[c - blockStart];
                            blockXics[c - blockStart] = null;    // Released with the candidate's scoring
                        }
                        if (blockCorrXics != null)
                        {
                            precomputedCorrXics = blockCorrXics[c - blockStart];
                            blockCorrXics[c - blockStart] = null;
                        }

                        var fdrEntry = _scorer.ScoreCandidate(
                            _candidates[c], _windowSpectra, _windowRts,
                            _rtCalibration,
                            _globalRtTolerance, _rtSigma,
                            _ms1Spectra, _ms1Calibration,
                            _context,
                            ospreyContext, ospreyPeakData, corrScoring,
                            _prefilter, c, precomputedXics, precomputedCorrXics);

                        if (fdrEntry != null)
                            entries.Add(fdrEntry);
                    }
                    return entries;
                }
                finally
                {
                    if (laneXcorr != null)
                        _context.Resolution.ReleaseWindowCache(laneXcorr, _context.XcorrScratchPool);
                }
            }
        }


        /// <summary>
        /// Score one candidate: ask the <see cref="PeakDataExtractor"/> for the
        /// per-candidate peak-data view (RT window, XICs, winning peak, apex), then
        /// publish the median-polish fit, run the 21 PIN feature calculators over
        /// that view, and assemble the <see cref="FdrEntry"/>. Returns null when
        /// the extractor found no scorable peak. The detection arithmetic and its
        /// diagnostics live in the extractor; only the median-polish publish (which
        /// is harness-owned because its bisection dump is exe-layer) and the
        /// calculator pass remain here.
        /// </summary>
        private FdrEntry ScoreCandidate(
            LibraryEntry candidate,
            List<Spectrum> windowSpectra,
            double[] windowRts,
            RTCalibration rtCalibration,
            double globalRtTolerance,
            double rtSigma,
            List<MS1Spectrum> ms1Spectra,
            MzCalibrationResult ms1Calibration,
            ScoringContext context,
            OspreyScoringContext ospreyContext,
            OspreyPeakData ospreyPeakData,
            CorrelationScoring corrScoring,
            WindowPrefilter prefilter,
            int prefilterIndex,
            List<XicData> precomputedXics,
            List<XicData> precomputedCorrXics)
        {
            if (!_extractor.TryExtract(
                    candidate, windowSpectra, windowRts, rtCalibration,
                    globalRtTolerance, rtSigma, ms1Spectra, ms1Calibration,
                    context, ospreyPeakData, prefilter, prefilterIndex, precomputedXics, precomputedCorrXics,
                    out var ext))
                return null;

            var bestPeak = ext.BestPeak;
            var xics = ext.Xics;
            var apexSpectrum = ext.ApexSpectrum;

            // The extractor has populated ospreyPeakData via Set. Reset the
            // per-candidate byproduct cache (carried over from the previous
            // candidate) BEFORE the median-polish publish below so that byproduct
            // survives to the calculators. Calculator-backed features mirror
            // Skyline's IPeakFeatureCalculator.
            ospreyContext.ClearByproducts();

            // Tukey median-polish inputs + fit (features 15, 16, 19, 20). The crop,
            // WriteMpInputsRow, and Compute stay here because the bisection
            // diagnostics (OspreyDiagnostics) live in the exe layer that
            // Osprey.Scoring cannot reference; the four feature values are
            // computed by the calculators from the published MedianPolishByproduct,
            // and the optional WriteMpDump fires after the feature vector below.
            // Crop XICs to the peak range so the polish operates only on signal,
            // not the wider RT search window. Matches Rust pipeline.rs:5198-5212.
            int peakLen = bestPeak.EndIndex - bestPeak.StartIndex + 1;
            if (peakLen >= 3)
            {
                var peakXics = CropToPeak(xics, bestPeak, peakLen, out var peakRts);

                // Bisection seam: dump (frag_pos, frag_idx, scan_idx, rt,
                // intensity) for every median-polish call. Mirrors the
                // Rust diagnostics::dump_mp_inputs at pipeline.rs:6494.
                // Use the same input buffers passed to the median polish
                // so we capture the exact data the algorithm sees.
                _diagnostics?.WriteMpInputsRow(
                    candidate.Id, apexSpectrum.ScanNumber, peakXics, peakRts);

                var polish = TukeyMedianPolish.Compute(peakXics, peakRts,
                    TukeyMedianPolish.SCORING_MAX_ITERATIONS, TukeyMedianPolish.SCORING_TOLERANCE);
                // Only publish when the fit converged. Every consumer (the four
                // calculators and the WriteMpDump guard) treats a missing byproduct
                // and a byproduct with null Polish identically (family default / no
                // dump), so skipping the publish on a null fit is value-identical and
                // avoids the allocation.
                if (polish != null)
                    ospreyContext.AddInfo(new MedianPolishByproduct(polish, peakXics));
            }

            // The candidate peaks the runner-up features compare the chosen peak with: the ones the
            // pick chose among, or for a peak rescored at override bounds, the ones the first pass
            // would have chosen among.
            if (ext.RunnerUpPeaks != null)
                ospreyContext.AddInfo(new PeakCandidatesByproduct(ext.RunnerUpPeaks, bestPeak));

            // The correlation-shape features score the wider-tolerance XICs, with their own median-polish
            // fit and runner-up coelution; null when there are none, and every feature scores xics.
            bool corrActive = corrScoring != null && ext.CorrelationXics != null;
            if (corrActive)
                corrScoring.Set(ospreyPeakData, ext, peakLen);

            // Build the full PIN feature vector
            double[] features = new double[OspreyFeatureCalculators.FeatureCount];
            // The core features, and of the feature families only those that are recorded. Each
            // computes its own byproduct on first use, so skipping one changes no other feature's value.
            for (int f = 0; f < features.Length; f++)
            {
                if (!IsComputed(f))
                    continue;
                features[f] = corrActive && OspreyFeatureCalculators.ReadsCorrelationXics(f)
                    ? OspreyFeatureCalculators.Get(f).Calculate(corrScoring.Context, corrScoring.PeakData)
                    : OspreyFeatureCalculators.Get(f).Calculate(ospreyContext, ospreyPeakData);
            }

            // Median-polish bisection dump (after the feature vector so the four
            // values are available). Reads the polish + cropped inputs from the
            // byproduct published above; gated by ShouldDumpMpFor as before. The
            // standard parity gate compares Stage 7 + blib, not the OSPREY_DIAGNOSTICS dumps.
            if (peakLen >= 3
                && ospreyContext.TryGetInfo(out MedianPolishByproduct mpByproduct)
                && mpByproduct.Polish != null
                && (_diagnostics?.ShouldDumpMpFor(apexSpectrum.ScanNumber, candidate.ModifiedSequence) ?? false))
            {
                _diagnostics?.WriteMpDump(
                    candidate, apexSpectrum.ScanNumber,
                    bestPeak, peakLen,
                    features[15], features[16], features[19], features[20],
                    mpByproduct.Polish, mpByproduct.PeakXics);
            }

            // Stage 6 reconciliation input: capture the top-N CWT peak
            // candidates ranked by penalized rank score, with each kept
            // candidate's apex/area/snr recomputed over the reference XIC
            // slice. Mirrors Rust run_search at pipeline.rs:6852-6879.
            // The stored coelution_score is the raw mean (NOT the RT-
            // penalized rank score) -- reconciliation has its own RT
            // tolerance logic via consensus RT comparison.
            List<CwtCandidate> cwtCandidatesOut = CaptureCwtCandidates(
                ext.CapturedPeaks, xics, ext.RefXicIdx, ext.RefXicIntensities, context);

            // Build FdrEntry from the winning peak + feature vector (frag
            // blobs, reference-XIC slice, bounds). Extracted to BuildFdrEntry.
            var entry = BuildFdrEntry(
                candidate, bestPeak, features, cwtCandidatesOut,
                xics, ext.RefXicIdx, ext.RefXicIntensities, windowRts, ext.StartScan, apexSpectrum);

            return entry;
        }


        // True when ScoreCandidate computes the PIN feature at featureIndex: a core feature, or a
        // recorded one.
        private bool IsComputed(int featureIndex)
        {
            return featureIndex < OspreyFeatureCalculators.CORE_FEATURE_COUNT || _recordedFeatures == null ||
                   _recordedFeatures[featureIndex];
        }

        // True when ScoreCandidate computes any of the named PIN features.
        private bool AnyComputed(string[] featureNames)
        {
            var remaining = new HashSet<string>(featureNames);
            bool any = false;
            for (int f = 0; f < OspreyFeatureCalculators.FeatureCount; f++)
            {
                if (remaining.Remove(OspreyFeatureCalculators.Get(f).Name) && IsComputed(f))
                    any = true;
            }
            if (remaining.Count > 0)
            {
                // Exception text for internal diagnostics, not displayed to user
                throw new InvalidOperationException(string.Format(@"Unknown feature names: {0}",
                    string.Join(@", ", remaining)));
            }
            return any;
        }

        // The fragment XICs cropped to the peak [StartIndex, StartIndex + peakLen), keyed by fragment
        // index, with their retention times: the median-polish input.
        private static List<KeyValuePair<int, double[]>> CropToPeak(List<XicData> xics, XICPeakBounds bestPeak,
            int peakLen, out double[] peakRts)
        {
            var peakXics = new List<KeyValuePair<int, double[]>>(xics.Count);
            peakRts = new double[peakLen];
            for (int s = 0; s < peakLen; s++)
                peakRts[s] = xics[0].RetentionTimes[bestPeak.StartIndex + s];
            for (int xi = 0; xi < xics.Count; xi++)
            {
                var src = xics[xi].Intensities;
                var slice = new double[peakLen];
                for (int s = 0; s < peakLen; s++)
                    slice[s] = src[bestPeak.StartIndex + s];
                peakXics.Add(new KeyValuePair<int, double[]>(xics[xi].FragmentIndex, slice));
            }
            return peakXics;
        }

        /// <summary>
        /// The window-local scoring state of the correlation-shape features under
        /// OSPREY_CORR_FRAGMENT_TOLERANCE: a second <see cref="OspreyScoringContext"/>, configured at
        /// the wider tolerance (so the 0.45 x / 0.2 x re-extractions are fractions of it), and a second
        /// <see cref="OspreyPeakData"/> presenting the candidate with its wider-tolerance XICs. Reused
        /// across a window's candidates like the primary pair; <see cref="Set"/> fills it per candidate
        /// with the byproducts the harness publishes for the primary context - the median-polish fit and
        /// the runner-up candidates - computed over the wider XICs.
        /// </summary>
        private sealed class CorrelationScoring
        {
            private readonly bool _polish;

            public CorrelationScoring(OspreyConfig corrConfig, ScoringContext context,
                WindowXcorrCache preprocessedXcorr, SpectralScorer scorer, bool polish)
            {
                _polish = polish;
                Context = new OspreyScoringContext(corrConfig);
                Context.SetWindow(context.Resolution, preprocessedXcorr, scorer, context.XcorrScratchPool);
                PeakData = new OspreyPeakData();
            }

            public OspreyScoringContext Context { get; }
            public OspreyPeakData PeakData { get; }

            public void Set(OspreyPeakData primary, ExtractedPeak ext, int peakLen)
            {
                PeakData.SetFrom(primary, ext.CorrelationXics);
                Context.ClearByproducts();
                if (_polish && peakLen >= 3)
                {
                    var peakXics = CropToPeak(ext.CorrelationXics, ext.BestPeak, peakLen, out var peakRts);
                    var polish = TukeyMedianPolish.Compute(peakXics, peakRts,
                        TukeyMedianPolish.SCORING_MAX_ITERATIONS, TukeyMedianPolish.SCORING_TOLERANCE);
                    if (polish != null)
                        Context.AddInfo(new MedianPolishByproduct(polish, peakXics));
                }
                if (ext.CorrelationRunnerUpPeaks != null)
                    Context.AddInfo(new PeakCandidatesByproduct(ext.CorrelationRunnerUpPeaks, ext.BestPeak));
            }
        }

        /// <summary>
        /// Capture the top-N CWT peak candidates (Stage 6 reconciliation input),
        /// ranked by penalized rank score, with each kept candidate's apex / area
        /// / SNR recomputed over the reference-XIC slice. Mirrors Rust run_search
        /// at pipeline.rs:6852-6879. The stored coelution_score is the raw mean
        /// (NOT the RT-penalized rank score) -- reconciliation has its own RT
        /// tolerance logic via consensus RT comparison. Returns null when there
        /// is nothing to capture (override path / no captured peaks / TopN == 0).
        /// </summary>
        private static List<CwtCandidate> CaptureCwtCandidates(
            List<(XICPeakBounds peak, double coelutionScore, double rankScore)> capturedPeaks,
            List<XicData> xics,
            int refXicIdx,
            double[] refXicIntensities,
            ScoringContext context)
        {
            int topN = context.Config.Reconciliation != null
                ? context.Config.Reconciliation.TopNPeaks
                : 0;
            if (capturedPeaks == null || capturedPeaks.Count == 0 || topN <= 0)
                return null;

            // Rust scored_candidates.sort_by at pipeline.rs:7329 is
            // STABLE (slice::sort_by is stable) AND uses f64::total_cmp,
            // which distinguishes -0.0 < +0.0. The default
            // Comparer<double> treats them equal, so two peaks whose
            // rank_score = coelution * rt_penalty * intensityWeight
            // collapses to a signed zero (intensityWeight = 0 when
            // ref_xic intensity at apex is 0; sign comes from
            // coelution) compare as tied under standard <, while
            // Rust orders them positive-then-negative. Pair LINQ
            // OrderByDescending (stable per .NET contract) with
            // TotalOrder.Comparer to match Rust byte-for-byte.
            var ordered = capturedPeaks
                .OrderByDescending(p => p.rankScore, TotalOrder.Comparer)
                .ToList();
            int kept = Math.Min(topN, ordered.Count);
            var cwtCandidatesOut = new List<CwtCandidate>(kept);
            double[] refRts = xics[refXicIdx].RetentionTimes;
            for (int k = 0; k < kept; k++)
            {
                var cap = ordered[k];
                var p = cap.peak;
                int safeStart = Math.Max(0, Math.Min(p.StartIndex, refXicIntensities.Length - 1));
                int safeEnd = Math.Max(safeStart, Math.Min(p.EndIndex, refXicIntensities.Length - 1));
                int apexIdx = safeStart;
                double apexVal = refXicIntensities[safeStart];
                for (int s = safeStart; s <= safeEnd; s++)
                {
                    if (refXicIntensities[s] >= apexVal)
                    {
                        apexVal = refXicIntensities[s];
                        apexIdx = s;
                    }
                }
                double area = PeakDetector.TrapezoidalArea(
                    refRts, refXicIntensities, safeStart, safeEnd);
                double snr = PeakDetector.ComputeSnr(
                    refXicIntensities, apexIdx, safeStart, safeEnd);
                cwtCandidatesOut.Add(new CwtCandidate
                {
                    ApexRt = refRts[apexIdx],
                    StartRt = refRts[safeStart],
                    EndRt = refRts[safeEnd],
                    Area = area,
                    Snr = snr,
                    CoelutionScore = cap.coelutionScore,
                });
            }
            return cwtCandidatesOut;
        }


        /// <summary>
        /// Build the <see cref="FdrEntry"/> for a scored candidate's winning peak.
        /// Serializes the full library fragment list (m/z + relative intensity),
        /// slices the reference XIC across the winning peak's [start..end] window,
        /// and copies the feature vector + bounds onto the entry. Pure: no logging,
        /// no diagnostics -- the six blob/scalar fields mirror Rust
        /// CoelutionScoredEntry::{fragment_mzs, fragment_intensities, reference_xic,
        /// peak.area, peak.signal_to_noise} so the reconciled .scores.parquet
        /// write-back stays byte-identical for cross-impl validation.
        /// </summary>
        private static FdrEntry BuildFdrEntry(
            LibraryEntry candidate,
            XICPeakBounds bestPeak,
            double[] features,
            List<CwtCandidate> cwtCandidatesOut,
            List<XicData> xics,
            int refXicIdx,
            double[] refXicIntensities,
            double[] windowRts,
            int startScan,
            Spectrum apexSpectrum)
        {
            // FragmentMzs / FragmentIntensities iterate the FULL library
            // fragment list (not just the top-N used by XIC extraction)
            // because Rust's parquet writer at pipeline.rs:1620-1631
            // serializes every library fragment.
            var frags = candidate.Fragments;
            int nFrags = frags?.Count ?? 0;
            double[] fragMzs = new double[nFrags];
            float[] fragInts = new float[nFrags];
            if (frags != null)
            {
                for (int fi = 0; fi < nFrags; fi++)
                {
                    fragMzs[fi] = frags[fi].Mz;
                    fragInts[fi] = frags[fi].RelativeIntensity;
                }
            }

            // ReferenceXic{Rts,Intensities} are sliced from the highest-
            // total-intensity fragment XIC across the winning peak's
            // [si..=ei] window, matching Rust's
            // `ref_xic[peak.start_index..=peak.end_index].to_vec()` at
            // pipeline.rs:6538. Use the SAFE indices (clipped by the
            // post-rank apex recompute) for non-override entries; the
            // override path's bestPeak retains its original boundaries.
            double[] refXicRtsAll = xics[refXicIdx].RetentionTimes;
            double[] refXicRts;
            double[] refXicInts;
            if (refXicRtsAll == null || refXicIntensities == null ||
                refXicRtsAll.Length == 0 || refXicIntensities.Length == 0)
            {
                refXicRts = new double[0];
                refXicInts = new double[0];
            }
            else
            {
                int refMaxLen = Math.Min(refXicRtsAll.Length, refXicIntensities.Length);
                int refSi = Math.Max(0, Math.Min(bestPeak.StartIndex, refMaxLen - 1));
                int refEi = Math.Max(refSi, Math.Min(bestPeak.EndIndex, refMaxLen - 1));
                int refLen = refEi - refSi + 1;
                refXicRts = new double[refLen];
                refXicInts = new double[refLen];
                for (int i = 0; i < refLen; i++)
                {
                    refXicRts[i] = refXicRtsAll[refSi + i];
                    refXicInts[i] = refXicIntensities[refSi + i];
                }
            }

            return new FdrEntry
            {
                EntryId = candidate.Id,
                IsDecoy = candidate.IsDecoy,
                Charge = candidate.Charge,
                ScanNumber = apexSpectrum.ScanNumber,
                ApexRt = apexSpectrum.RetentionTime,
                StartRt = windowRts[startScan + bestPeak.StartIndex],
                EndRt = windowRts[startScan + bestPeak.EndIndex],
                CoelutionSum = features[0],
                Score = features[0],
                ModifiedSequence = candidate.ModifiedSequence,
                Features = features,
                CwtCandidates = cwtCandidatesOut,
                FragmentMzs = fragMzs,
                FragmentIntensities = fragInts,
                ReferenceXicRts = refXicRts,
                ReferenceXicIntensities = refXicInts,
                BoundsArea = bestPeak.Area,
                BoundsSnr = bestPeak.SignalToNoise,
            };
        }
    }
}
