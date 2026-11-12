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
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Computes, for the survivors Stage 6 reconciliation would move, the CWT peak candidates
    /// first-pass scoring ranked for them (issue #5024). Rust stores the top-N list of every row
    /// in its scores parquet for its planner, which reads it for under 1% of rows; Osprey
    /// computes those rows' lists from the run's spectra when the planner asks, with the
    /// detection Stage 4 ran (<see cref="CoelutionScorer.RecomputeCwtCandidates"/>) under the
    /// search settings Stage 4 resolved, so the lists are the ones Stage 4 would have stored, bit
    /// for bit.
    ///
    /// <para>What Stage 4 searched with, and so what this searches with: the run's FIRST-PASS RT
    /// calibration (not the refined one the rescore uses) - or, for a run whose calibration
    /// failed, the library-to-run RT range mapping calibration starts from
    /// (<see cref="RtRangeMapping.FallbackRtMap"/>) - and the run's MS2 mass calibration. The
    /// OSPREY_* settings that change detection must be the ones Stage 4 ran under;
    /// <see cref="PerFileRescoreTask"/> carries them in its validity key.</para>
    ///
    /// <para>Memory: the windows holding a consulted precursor are loaded a few at a time
    /// (the run's thread share), each released when its rows are computed, as Stage 4 scores
    /// them.</para>
    /// </summary>
    internal static class CwtCandidateRecompute
    {
        /// <summary>
        /// The CWT peak candidates of each of <paramref name="rows"/>, in order: Stage 4's top-N
        /// list for the row, empty when the run's input is not available here or the row's
        /// precursor and apex scan match no isolation window.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<CwtCandidate>> ForRows(
            IReadOnlyList<FdrEntry> rows,
            RescoreRunSpectra spectra,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            IReadOnlyList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            RTCalibration firstPassCalibration,
            PipelineContext ctx)
        {
            var lists = new IReadOnlyList<CwtCandidate>[rows.Count];
            if (rows.Count == 0 || spectra.InputFile == null)
                return lists;

            var spectraIndex = spectra.Index;
            // The settings first-pass scoring resolved: a fresh clone, because resolving them writes
            // the calibrated fragment tolerance into the configuration.
            var context = new ScoringContext(config.ShallowClone(), spectra.FileName);
            ScoringTaskShared.Pipeline(ctx).ApplySearchSettings(firstPassCalibration, spectra.Ms2Calibration, context,
                string.Empty, false, out double rtTolerance, out double rtSigma);
            var searchCalibration = firstPassCalibration ?? FallbackRtMap(config, fullLibrary, spectraIndex);
            var scorer = new CoelutionScorer(null);

            // The windows each row could have been scored in, by precursor m/z.
            var windows = spectraIndex.IsolationWindows;
            var rowsByWindow = new List<int>[windows.Count];
            var libraryEntries = new LibraryEntry[rows.Count];
            for (int r = 0; r < rows.Count; r++)
            {
                if (!libraryById.TryGetValue(rows[r].EntryId, out var libraryEntry) ||
                    libraryEntry.Fragments == null || libraryEntry.Fragments.Count == 0)
                {
                    continue;
                }
                libraryEntries[r] = libraryEntry;
                for (int w = 0; w < windows.Count; w++)
                {
                    if (windows[w].Contains(libraryEntry.PrecursorMz))
                        (rowsByWindow[w] ?? (rowsByWindow[w] = new List<int>())).Add(r);
                }
            }

            // Compute each row in the window whose spectra hold its apex scan - Stage 4 kept the
            // row it scored there. Overlapping windows sharing a scan would share its spectra, so
            // the lowest such window is taken, whatever order the windows finish in.
            var fromWindow = new int[rows.Count];
            for (int r = 0; r < rows.Count; r++)
                fromWindow[r] = int.MaxValue;
            var lockObj = new object();
            var provider = new StreamingWindowSpectraProvider(spectraIndex, spectra.Ms2Calibration);
            var windowIndices = Enumerable.Range(0, windows.Count).Where(w => rowsByWindow[w] != null).ToList();
            Parallel.ForEach(windowIndices, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, config.NThreads) },
                w =>
                {
                    var windowSpectra = provider.GetCalibratedWindow((int)Math.Round(windows[w].Center * 10.0));
                    if (windowSpectra == null || windowSpectra.Count == 0)
                        return;
                    CoelutionScorer.SortWindowSpectra(windowSpectra);
                    double[] windowRts = CoelutionScorer.WindowRetentionTimes(windowSpectra);
                    var scans = new HashSet<uint>(windowSpectra.Select(s => s.ScanNumber));
                    foreach (int r in rowsByWindow[w])
                    {
                        if (!scans.Contains(rows[r].ScanNumber))
                            continue;
                        var list = scorer.RecomputeCwtCandidates(libraryEntries[r], windowSpectra, windowRts,
                            searchCalibration, rtTolerance, rtSigma, spectra.Ms1Spectra, spectra.Ms1Calibration,
                            context, out _);
                        lock (lockObj)
                        {
                            if (w < fromWindow[r])
                            {
                                fromWindow[r] = w;
                                lists[r] = list;
                            }
                        }
                    }
                });
            return lists;
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
    }

    /// <summary>
    /// One run's spectra cache index, MS1 spectra and mass calibrations, as PerFileRescoring
    /// reads them - loaded on first use and then shared by the run's planning, which computes
    /// CWT candidates from them, and its rescore. A run whose planning asks for nothing and that
    /// has nothing to rescore never loads them.
    /// </summary>
    internal sealed class RescoreRunSpectra
    {
        private readonly Lazy<Loaded> _loaded;

        /// <param name="fileName">The run's stem.</param>
        /// <param name="inputFile">The run's input, or null when it is not among this task's
        /// inputs - then nothing can be loaded, and the run's candidates are empty.</param>
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
