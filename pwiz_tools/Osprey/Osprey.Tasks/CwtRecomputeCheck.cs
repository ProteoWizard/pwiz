/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using pwiz.Osprey.Chromatography;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR.Reconciliation;
using pwiz.Osprey.IO;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The OSPREY_CWT_RECOMPUTE_CHECK diagnostic (issue #5024): measures whether Stage 6 can
    /// recompute, from the run's spectra and first-pass calibration, the CWT peak candidates
    /// Stage 4 stores in the <c>cwt_candidates</c> column, for exactly the rows reconciliation
    /// consults them for - the precursors the planner gave a non-Keep action.
    ///
    /// <para>For each such row it finds the isolation window first-pass scoring scored it in
    /// (a window whose m/z range holds the precursor and whose spectra hold the row's apex scan),
    /// recomputes the list with <see cref="CoelutionScorer.RecomputeCwtCandidates"/> under the
    /// search settings first-pass scoring resolved for the first-pass (not the refined)
    /// calibration, and compares it with the stored list: count, then the RTs reconciliation reads
    /// (apex, start, end - and with them the order, since the planner records a candidate index)
    /// bit for bit, and separately the area, SNR and coelution score it does not read.</para>
    ///
    /// <para>Writes nothing but log lines, and reads only what the rescore already loaded plus
    /// the run's stored candidates, so the run's outputs are unchanged.</para>
    /// </summary>
    internal static class CwtRecomputeCheck
    {
        private const int MAX_EXAMPLES = 5;

        /// <summary>
        /// Check one run and log its summary line, and up to <see cref="MAX_EXAMPLES"/>
        /// disagreements. <paramref name="run"/> is the run after planning, its actions keyed
        /// against its survivors; <paramref name="firstPassCalibration"/> is the calibration
        /// first-pass scoring searched with, null when the run's calibration failed.
        /// </summary>
        internal static void Run(
            RunRescoreInputs run,
            string parquetPath,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            OspreyConfig config,
            RTCalibration firstPassCalibration,
            SpectraWindowIndex spectraIndex,
            MzCalibrationResult ms2Calibration,
            MzCalibrationResult ms1Calibration,
            List<MS1Spectrum> ms1Spectra,
            PipelineContext ctx)
        {
            string fileName = run.FileName;
            var rows = ConsultedRows(run);
            var tally = new Tally();
            if (rows.Count == 0 || firstPassCalibration == null)
            {
                // A failed calibration searched around the library RT mapping, which Stage 6 does
                // not keep, so its rows are reported rather than recomputed.
                if (firstPassCalibration == null)
                    tally.Uncalibrated = rows.Count;
                LogSummary(fileName, rows.Count, tally, ctx);
                return;
            }

            var stored = ParquetScoreCache.LoadCwtCandidatesFromParquet(parquetPath);

            // The settings first-pass scoring resolved: a fresh clone, because resolving them writes
            // the calibrated fragment tolerance into the configuration.
            var context = new ScoringContext(config.ShallowClone(), fileName);
            ScoringTaskShared.Pipeline(ctx).ApplySearchSettings(firstPassCalibration, ms2Calibration, context,
                string.Empty, false, out double rtTolerance, out double rtSigma);
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

            // Recompute each row in every window whose spectra hold its apex scan: one window,
            // unless the row's precursor was isolated by overlapping windows sharing that scan.
            var recomputed = new List<Recomputed>[rows.Count];
            var provider = new StreamingWindowSpectraProvider(spectraIndex, ms2Calibration);
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
                            firstPassCalibration, rtTolerance, rtSigma, ms1Spectra, ms1Calibration, context,
                            out uint? apexScan);
                        var result = new Recomputed(w, list ?? new List<CwtCandidate>(), apexScan);
                        lock (recomputed)
                            (recomputed[r] ?? (recomputed[r] = new List<Recomputed>())).Add(result);
                    }
                });

            var examples = new List<string>();
            for (int r = 0; r < rows.Count; r++)
            {
                var entry = rows[r];
                if (!entry.ParquetIndex.HasValue || entry.ParquetIndex.Value >= stored.Count)
                {
                    tally.Unindexed++;
                    continue;
                }
                var storedList = stored[(int)entry.ParquetIndex.Value] ?? new List<CwtCandidate>();
                tally.StoredCandidates += storedList.Count;
                var results = recomputed[r];
                if (results == null)
                {
                    tally.WindowNotFound++;
                    continue;
                }
                // Overlapping windows: report the row, and take the window that reproduces it.
                Recomputed chosen = results.OrderBy(c => c.WindowIndex).First();
                if (results.Count > 1)
                {
                    tally.WindowAmbiguous++;
                    chosen = results.OrderBy(c => c.WindowIndex)
                        .FirstOrDefault(c => Compare(storedList, c.Candidates) == Agreement.exact) ?? chosen;
                }
                if (chosen.ApexScan != entry.ScanNumber)
                    tally.ApexScanDiffers++;
                switch (Compare(storedList, chosen.Candidates))
                {
                    case Agreement.exact:
                        tally.Exact++;
                        break;
                    case Agreement.secondary_only:
                        tally.SecondaryOnly++;
                        AddExample(examples, @"secondary", entry, windows[chosen.WindowIndex], chosen, storedList);
                        break;
                    default:
                        tally.RtOrOrder++;
                        AddExample(examples, @"rt-order", entry, windows[chosen.WindowIndex], chosen, storedList);
                        break;
                }
            }

            LogSummary(fileName, rows.Count, tally, ctx);
            foreach (string example in examples)
                ctx.LogInfo(LogTag.BISECT, LogKey.Format(LogKey.CWT_RECOMPUTE_MISMATCH, example));
        }

        // The survivors the planner gave a non-Keep action, in survivor order: the rows whose
        // stored candidates it consulted.
        private static List<FdrEntry> ConsultedRows(RunRescoreInputs run)
        {
            var indices = new List<int>();
            foreach (var kvp in run.ReconciliationActions)
            {
                if (kvp.Key.FileName == run.FileName &&
                    (kvp.Value is ReconcileAction.UseCwtPeak || kvp.Value is ReconcileAction.ForcedIntegration))
                {
                    indices.Add(kvp.Key.Index);
                }
            }
            indices.Sort(); // Array.Sort OK: distinct survivor indices, so no ties
            return indices.Select(i => run.Survivors[i]).ToList();
        }

        private static Agreement Compare(List<CwtCandidate> stored, List<CwtCandidate> recomputed)
        {
            if (stored.Count != recomputed.Count)
                return Agreement.rt_or_order;
            bool secondary = false;
            for (int i = 0; i < stored.Count; i++)
            {
                var s = stored[i];
                var c = recomputed[i];
                if (!SameBits(s.ApexRt, c.ApexRt) || !SameBits(s.StartRt, c.StartRt) || !SameBits(s.EndRt, c.EndRt))
                    return Agreement.rt_or_order;
                if (!SameBits(s.Area, c.Area) || !SameBits(s.Snr, c.Snr) || !SameBits(s.CoelutionScore, c.CoelutionScore))
                    secondary = true;
            }
            return secondary ? Agreement.secondary_only : Agreement.exact;
        }

        private static bool SameBits(double a, double b)
        {
            return BitConverter.DoubleToInt64Bits(a) == BitConverter.DoubleToInt64Bits(b);
        }

        private static void LogSummary(string fileName, int rows, Tally tally, PipelineContext ctx)
        {
            ctx.LogInfo(LogTag.BISECT, LogKey.Format(LogKey.CWT_RECOMPUTE_CHECK,
                @"file={0} rows={1} stored_candidates={2} exact={3} rt_order_mismatch={4} secondary_only={5} " +
                @"window_ambiguous={6} window_not_found={7} apex_scan_differs={8} uncalibrated={9} unindexed={10}",
                fileName, rows, tally.StoredCandidates, tally.Exact, tally.RtOrOrder, tally.SecondaryOnly,
                tally.WindowAmbiguous, tally.WindowNotFound, tally.ApexScanDiffers, tally.Uncalibrated, tally.Unindexed));
        }

        private static void AddExample(List<string> examples, string kind, FdrEntry entry, IsolationWindow window,
            Recomputed chosen, List<CwtCandidate> stored)
        {
            if (examples.Count >= MAX_EXAMPLES)
                return;
            examples.Add(string.Format(CultureInfo.InvariantCulture,
                @"kind={0} entry_id={1} charge={2} scan={3} recomputed_apex_scan={4} window={5:R} stored={6} recomputed={7}",
                kind, entry.EntryId, entry.Charge, entry.ScanNumber,
                chosen.ApexScan.HasValue ? chosen.ApexScan.Value.ToString(CultureInfo.InvariantCulture) : @"none",
                window.Center, FormatList(stored), FormatList(chosen.Candidates)));
        }

        // apex/start/end/area/snr/coelution per candidate, round-trip formatted.
        private static string FormatList(List<CwtCandidate> candidates)
        {
            var sb = new StringBuilder(@"[");
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (i > 0)
                    sb.Append(@"; ");
                sb.Append(string.Format(CultureInfo.InvariantCulture, @"{0:R}/{1:R}/{2:R}/{3:R}/{4:R}/{5:R}",
                    c.ApexRt, c.StartRt, c.EndRt, c.Area, c.Snr, c.CoelutionScore));
            }
            return sb.Append(@"]").ToString();
        }

        private enum Agreement
        {
            exact,
            secondary_only,
            rt_or_order,
        }

        private sealed class Recomputed
        {
            public Recomputed(int windowIndex, List<CwtCandidate> candidates, uint? apexScan)
            {
                WindowIndex = windowIndex;
                Candidates = candidates;
                ApexScan = apexScan;
            }

            public int WindowIndex { get; }
            public List<CwtCandidate> Candidates { get; }
            public uint? ApexScan { get; }
        }

        private sealed class Tally
        {
            public int StoredCandidates;
            public int Exact;
            public int RtOrOrder;
            public int SecondaryOnly;
            public int WindowAmbiguous;
            public int WindowNotFound;
            public int ApexScanDiffers;
            public int Uncalibrated;
            public int Unindexed;
        }
    }
}
