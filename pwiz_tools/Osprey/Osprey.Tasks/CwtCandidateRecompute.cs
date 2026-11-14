/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.IO;
using System.Linq;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Resolves the new peak of each survivor Stage 6 reconciliation moves, inside the rescore's
    /// own window loop (issue #5024): the CWT peak candidates first-pass scoring ranked for the
    /// survivor are computed from the window's spectra as the rescore loads them, the planner's
    /// rule picks a candidate or imputes boundaries, and the window scores the survivor there.
    /// Rust stores the top-N list of every row in its scores parquet for its planner, which reads
    /// it for under 1% of rows; Osprey computes those rows' lists where the rescore already holds
    /// the spectra, so each run's spectra are read once.
    ///
    /// <para>The candidates are computed with the detection Stage 4 ran
    /// (<see cref="CoelutionScorer.RecomputeCwtCandidates"/>) under the search settings Stage 4
    /// resolved, so they are the ones Stage 4 would have stored, bit for bit: the run's FIRST-PASS
    /// RT calibration (not the refined one the rescore scores with) - or, for a run whose
    /// calibration failed, the library-to-run RT range mapping calibration starts from
    /// (<see cref="RtRangeMapping.FallbackRtMap"/>) - and the run's MS2 mass calibration. The
    /// OSPREY_* settings that change detection must be the ones Stage 4 ran under;
    /// <see cref="PerFileRescoreTask"/> carries them in its validity key.</para>
    ///
    /// <para>Stage 4 stored a survivor's row from one window, the one whose spectra hold its apex
    /// scan; that window's action is the run's plan, and it must find the peak again at that
    /// scan (<see cref="Verify"/>). A precursor that falls in two overlapping windows is scored in
    /// both, as Stage 4 scored it; the other window resolves it from its own spectra, which Stage
    /// 4 also did. Isolation schemes with margins produce such precursors (issue #5034).</para>
    /// </summary>
    internal sealed class CwtCandidateRecompute : IWindowBoundaryResolver
    {
        private readonly string _fileName;
        private readonly Dictionary<uint, MovedSurvivor> _moved;
        private readonly ScoringContext _context;
        private readonly RTCalibration _searchCalibration;
        private readonly double _rtTolerance;
        private readonly double _rtSigma;
        private readonly List<MS1Spectrum> _ms1Spectra;
        private readonly MzCalibrationResult _ms1Calibration;
        private readonly CoelutionScorer _scorer = new CoelutionScorer(null);

        /// <param name="moves">The survivors the run's plan moves, each with its move.</param>
        /// <param name="spectra">The run's spectra, as the rescore reads them.</param>
        /// <param name="fullLibrary">The library, for a failed calibration's fallback map.</param>
        /// <param name="config">The run's rescore configuration (cloned here).</param>
        /// <param name="firstPassCalibration">The run's Stage 3 RT calibration, null when it
        /// failed.</param>
        /// <param name="ctx">The pipeline, for the search settings.</param>
        internal CwtCandidateRecompute(
            IReadOnlyList<(FdrEntry Survivor, ReconcileMove Move)> moves,
            RescoreRunSpectra spectra,
            IReadOnlyList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            RTCalibration firstPassCalibration,
            PipelineContext ctx)
        {
            _fileName = spectra.FileName;
            _moved = new Dictionary<uint, MovedSurvivor>(moves.Count);
            foreach (var move in moves)
                _moved[move.Survivor.EntryId] = new MovedSurvivor(move.Survivor, move.Move);

            // The settings first-pass scoring resolved: a fresh clone, because resolving them writes
            // the calibrated fragment tolerance into the configuration.
            _context = new ScoringContext(config.ShallowClone(), _fileName);
            ScoringTaskShared.Pipeline(ctx).ApplySearchSettings(firstPassCalibration, spectra.Ms2Calibration, _context,
                string.Empty, false, out _rtTolerance, out _rtSigma);
            _searchCalibration = firstPassCalibration ?? FallbackRtMap(config, fullLibrary, spectra.Index);
            _ms1Spectra = spectra.Ms1Spectra;
            _ms1Calibration = spectra.Ms1Calibration;
        }

        public bool Resolves(uint entryId)
        {
            return _moved.ContainsKey(entryId);
        }

        public (double Apex, double Start, double End) Resolve(LibraryEntry entry, IsolationWindow window,
            List<Spectrum> windowSpectra, double[] windowRts)
        {
            var moved = _moved[entry.Id];
            var candidates = _scorer.RecomputeCwtCandidates(entry, windowSpectra, windowRts,
                _searchCalibration, _rtTolerance, _rtSigma, _ms1Spectra, _ms1Calibration, _context,
                out uint? apexScan);
            var action = moved.Move.Resolve(moved.Survivor.ApexRt, candidates);
            uint stage4Scan = moved.Survivor.ScanNumber;
            moved.Record(window, windowSpectra.Any(s => s.ScanNumber == stage4Scan), action,
                apexScan == stage4Scan);
            return OverrideBounds(action);
        }

        /// <summary>
        /// The run's plan: each moved survivor's action, keyed by entry_id, as the window holding its
        /// Stage 4 apex scan resolved it. Throws <see cref="InvalidDataException"/>, reported per
        /// run, when any survivor was not resolved there or its peak was not found again at that
        /// scan: a search that has drifted from Stage 4's - a setting Stage 4 read that this one
        /// did not, or a calibration that is not the one Stage 4 searched with - would plan the
        /// run wrong without a sign. On SEA-AD and the subsets every survivor reproduces; a wrong
        /// calibration moved about half of them.
        /// </summary>
        internal IReadOnlyDictionary<uint, ReconcileAction> Verify(out int multiWindow)
        {
            var actions = new Dictionary<uint, ReconcileAction>(_moved.Count);
            int notReproduced = 0;
            multiWindow = 0;
            foreach (var kvp in _moved)
            {
                var moved = kvp.Value;
                if (moved.Action == null || !moved.Reproduced)
                    notReproduced++;
                else
                    actions[kvp.Key] = moved.Action;
                if (moved.WindowCount > 1)
                    multiWindow++;
            }
            if (notReproduced > 0)
                throw NotReproduced(_fileName, notReproduced, _moved.Count);
            return actions;
        }

        /// <summary>
        /// The (apex, start, end) boundaries the rescore scores <paramref name="action"/> at: a
        /// re-picked candidate's own, or boundaries imputed around the expected RT.
        /// </summary>
        internal static (double Apex, double Start, double End) OverrideBounds(ReconcileAction action)
        {
            if (action is ReconcileAction.UseCwtPeak useCwt)
                return (useCwt.ApexRt, useCwt.StartRt, useCwt.EndRt);
            var forced = (ReconcileAction.ForcedIntegration)action;
            return (forced.ExpectedRt, forced.ExpectedRt - forced.HalfWidth, forced.ExpectedRt + forced.HalfWidth);
        }

        /// <summary>
        /// The error for a run whose first-pass peaks this cannot reproduce - reported per run,
        /// as a damaged input is, rather than planned from candidates Stage 4 never found.
        /// </summary>
        internal static InvalidDataException NotReproduced(string fileName, int notReproduced, int rowCount)
        {
            return new InvalidDataException(string.Format(
                OspreyTasksResources.CwtCandidateRecompute_ForRows_Could_not_reproduce_the_first_pass_peaks_of__0__of__1__precursors_in__2___Score_this_run_again_from_the_start_,
                notReproduced, rowCount, fileName));
        }

        /// <summary>
        /// The search centre Stage 4 used for a run with no first-pass RT calibration
        /// (<see cref="ScoringContext.FallbackRtMap"/>): the range mapping when calibration ran and
        /// failed, none when it was off or a calibration file was loaded in its place
        /// (OSPREY_LOAD_CALIBRATION), as <c>PerFileScoringTask.ResolveCalibration</c> decides.
        /// </summary>
        private static RTCalibration FallbackRtMap(OspreyConfig config, IReadOnlyList<LibraryEntry> fullLibrary,
            SpectraWindowIndex spectraIndex)
        {
            string loadCalPath = OspreyEnvironment.LoadCalibrationPath;
            if (!string.IsNullOrEmpty(loadCalPath) && File.Exists(loadCalPath))
                return null;
            if (!config.RtCalibration.Enabled)
                return null;
            return RtRangeMapping.Measure(fullLibrary, spectraIndex.AllMs2Rts).FallbackRtMap();
        }

        /// <summary>
        /// One moved survivor and what the windows scoring it resolved. Written from the window
        /// threads, so <see cref="Record"/> locks.
        /// </summary>
        private sealed class MovedSurvivor
        {
            private readonly object _lock = new object();
            private double _homeCenter = double.PositiveInfinity;

            public MovedSurvivor(FdrEntry survivor, ReconcileMove move)
            {
                Survivor = survivor;
                Move = move;
            }

            public FdrEntry Survivor { get; }
            public ReconcileMove Move { get; }

            /// <summary>The action the window holding the Stage 4 apex scan resolved.</summary>
            public ReconcileAction Action { get; private set; }

            /// <summary>True when that window found the peak again at the Stage 4 apex scan.</summary>
            public bool Reproduced { get; private set; }

            /// <summary>The number of windows that resolved this survivor.</summary>
            public int WindowCount { get; private set; }

            /// <summary>
            /// Record one window's resolution. Of windows holding the apex scan - overlapping
            /// windows sharing a scan would share its spectra - the lowest is taken, whatever order
            /// the windows finish in.
            /// </summary>
            public void Record(IsolationWindow window, bool holdsApexScan, ReconcileAction action, bool reproduced)
            {
                lock (_lock)
                {
                    WindowCount++;
                    if (!holdsApexScan || window.Center >= _homeCenter)
                        return;
                    _homeCenter = window.Center;
                    Action = action;
                    Reproduced = reproduced;
                }
            }
        }
    }

    /// <summary>
    /// One run's spectra cache index, MS1 spectra and mass calibrations, as PerFileRescoring
    /// reads them - loaded when the run's rescore first needs them. A run with nothing to rescore
    /// never loads them.
    /// </summary>
    internal sealed class RescoreRunSpectra
    {
        private readonly Lazy<Loaded> _loaded;

        /// <param name="fileName">The run's stem.</param>
        /// <param name="inputFile">The run's input, or null when it is not among this task's
        /// inputs - then nothing can be loaded.</param>
        /// <param name="ctx">For the loaders' messages.</param>
        public RescoreRunSpectra(string fileName, string inputFile, PipelineContext ctx)
        {
            FileName = fileName;
            InputFile = inputFile;
            _loaded = new Lazy<Loaded>(() => new Loaded(inputFile, fileName, ctx));
        }

        public string FileName { get; }
        public string InputFile { get; }

        public SpectraWindowIndex Index => _loaded.Value.Index;
        public List<MS1Spectrum> Ms1Spectra => _loaded.Value.Ms1Spectra;
        public MzCalibrationResult Ms2Calibration => _loaded.Value.Ms2Calibration;
        public MzCalibrationResult Ms1Calibration => _loaded.Value.Ms1Calibration;

        /// <summary>The first-pass RT MAD the run's <c>.calibration.json</c> records, if any.</summary>
        public double? RtMadFromCalJson => _loaded.Value.RtMadFromCalJson;

        private sealed class Loaded
        {
            public Loaded(string inputFile, string fileName, PipelineContext ctx)
            {
                // Stream this file's MS2 by isolation window from the .spectra.bin cache the
                // original Stage 1-4 run wrote, instead of materializing the whole ~6 GB
                // resident List<Spectrum>: build the seekable SpectraWindowIndex (MS1 + the
                // isolation windows come from it too) and load each window on demand. Stage 6
                // REQUIRES that cache; there is no mzML fallback -- a rescore without the cache
                // is a deployment error, not a reason to re-read the 6 GB mzML
                // (LoadSpectraForRescore throws).
                Index = ScoringTaskShared.LoadSpectraForRescore(inputFile, fileName,
                    OspreyTasksResources.PerFileScoringTask_LoadJoinOnlyScores_Re_scoring, true, ctx);
                Ms1Spectra = Index.Ms1Spectra.ToList();

                // Load the sibling .calibration.json so the search uses the same MS2/MS1 mass
                // calibrations the original Stage 1-4 run used. The file is written by the
                // original ProcessFile call and read here -- same disk-roundtrip path the worker
                // uses.
                ScoringTaskShared.LoadMassCalibrations(inputFile, OspreyTasksResources.PerFileScoringTask_LoadJoinOnlyScores_Re_scoring,
                    out MzCalibrationResult ms2Cal, out MzCalibrationResult ms1Cal, out double? rtMad);
                Ms2Calibration = ms2Cal;
                Ms1Calibration = ms1Cal;
                RtMadFromCalJson = rtMad;
            }

            public SpectraWindowIndex Index { get; }
            public List<MS1Spectrum> Ms1Spectra { get; }
            public MzCalibrationResult Ms2Calibration { get; }
            public MzCalibrationResult Ms1Calibration { get; }
            public double? RtMadFromCalJson { get; }
        }
    }
}
