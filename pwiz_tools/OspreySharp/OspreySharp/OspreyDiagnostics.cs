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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using pwiz.OspreySharp.Chromatography;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Scoring;

namespace pwiz.OspreySharp
{
    /// <summary>
    /// Central access point for OSPREY_DUMP_* / OSPREY_DIAG_* environment
    /// variables and the cross-implementation bisection dumps they gate.
    /// Moves the diagnostic serialization code out of the analysis pipeline
    /// so the production classes can focus on correctness + performance.
    ///
    /// Dump format must remain byte-for-byte stable -- these files are
    /// diffed against the Rust osprey reference to bisect cross-impl drift.
    /// Use <see cref="F10"/> for doubles, invariant culture, and the same
    /// field ordering as the Rust equivalents.
    /// </summary>
    public static class OspreyDiagnostics
    {
        // ----- Env-var gate flags (read once at process start) -----

        /// <summary>
        /// OSPREY_DUMP_CAL_SAMPLE: dump the calibration sample list
        /// (cs_cal_sample.txt) AND the calibration scalars+grid
        /// (cs_cal_scalars.txt / cs_cal_grid.txt).
        /// </summary>
        public static readonly bool DumpCalSample = IsOne(@"OSPREY_DUMP_CAL_SAMPLE");

        /// <summary>OSPREY_CAL_SAMPLE_ONLY: exit after cs_cal_sample.txt dump.</summary>
        public static readonly bool CalSampleOnly = IsOne(@"OSPREY_CAL_SAMPLE_ONLY");

        /// <summary>
        /// OSPREY_DUMP_CAL_WINDOWS: dump per-entry calibration window
        /// selection (cs_cal_windows.txt).
        /// </summary>
        public static readonly bool DumpCalWindows = IsOne(@"OSPREY_DUMP_CAL_WINDOWS");

        /// <summary>OSPREY_CAL_WINDOWS_ONLY: exit after cs_cal_windows.txt dump.</summary>
        public static readonly bool CalWindowsOnly = IsOne(@"OSPREY_CAL_WINDOWS_ONLY");

        /// <summary>
        /// OSPREY_DUMP_CAL_MATCH: dump per-entry calibration match info
        /// (cs_cal_match.txt).
        /// </summary>
        public static readonly bool DumpCalMatch = IsOne(@"OSPREY_DUMP_CAL_MATCH");

        /// <summary>OSPREY_CAL_MATCH_ONLY: exit after cs_cal_match.txt dump.</summary>
        public static readonly bool CalMatchOnly = IsOne(@"OSPREY_CAL_MATCH_ONLY");

        /// <summary>
        /// OSPREY_DUMP_LDA_SCORES: dump per-entry LDA discriminant + q-value
        /// after LDA scoring (cs_lda_scores.txt).
        /// </summary>
        public static readonly bool DumpLdaScores = IsOne(@"OSPREY_DUMP_LDA_SCORES");

        /// <summary>OSPREY_LDA_SCORES_ONLY: exit after cs_lda_scores.txt dump.</summary>
        public static readonly bool LdaScoresOnly = IsOne(@"OSPREY_LDA_SCORES_ONLY");

        /// <summary>
        /// OSPREY_DUMP_LOESS_INPUT: dump the (lib_rt, measured_rt) pairs
        /// fed to LOESS (cs_loess_input.txt).
        /// </summary>
        public static readonly bool DumpLoessInput = IsOne(@"OSPREY_DUMP_LOESS_INPUT");

        /// <summary>OSPREY_LOESS_INPUT_ONLY: exit after cs_loess_input.txt dump.</summary>
        public static readonly bool LoessInputOnly = IsOne(@"OSPREY_LOESS_INPUT_ONLY");

        /// <summary>
        /// OSPREY_DUMP_PERCOLATOR: dump per-precursor Stage 5 state
        /// (score, pep, 4 q-values) after first-pass Percolator FDR
        /// completes and before first-pass protein FDR / compaction.
        /// Cross-impl parity gate (cs_stage5_percolator.tsv).
        /// </summary>
        public static readonly bool DumpPercolator = IsOne(@"OSPREY_DUMP_PERCOLATOR");

        /// <summary>OSPREY_PERCOLATOR_ONLY: exit after cs_stage5_percolator.tsv dump.</summary>
        public static readonly bool PercolatorOnly = IsOne(@"OSPREY_PERCOLATOR_ONLY");

        /// <summary>
        /// OSPREY_DIAG_XIC_ENTRY_ID: the entry ID to dump chromatogram for
        /// during calibration scoring. null = no dump.
        /// </summary>
        public static readonly uint? DiagXicEntryId = ParseNullableUint(@"OSPREY_DIAG_XIC_ENTRY_ID");

        /// <summary>
        /// OSPREY_DIAG_XIC_PASS: the calibration pass (1 or 2) at which to
        /// dump the XIC. Defaults to 1 because bisection walks downstream:
        /// until pass 1 chromatograms match, comparing pass 2 is premature.
        /// </summary>
        public static readonly int DiagXicPass = ParseIntOrDefault(@"OSPREY_DIAG_XIC_PASS", 1);

        /// <summary>
        /// OSPREY_DIAG_SEARCH_ENTRY_IDS: comma-separated entry IDs whose
        /// main-search XIC extraction should dump a
        /// cs_search_xic_entry_&lt;ID&gt;.txt. Does NOT exit.
        /// </summary>
        public static readonly HashSet<uint> DiagSearchEntryIds = ParseIdSet(@"OSPREY_DIAG_SEARCH_ENTRY_IDS");

        /// <summary>
        /// OSPREY_DIAG_MP_SCAN: the MS2 scan number whose median polish
        /// should dump cs_mp_diag.txt (filtered additionally to a specific
        /// DECOY_ALQFAQWWK target by historical convention). null = no dump.
        /// </summary>
        public static readonly int? DiagMpScan = ParseNullableInt(@"OSPREY_DIAG_MP_SCAN");

        // ----- Shared utilities -----

        /// <summary>
        /// Format a double with 10 decimal places using round-half-to-even
        /// (banker's) to match Rust's {:.10} formatter. .NET Framework's F10
        /// default is round-half-away-from-zero, which flips the last digit
        /// on exact .5 values (e.g. 4271.60400390625 -> 63 in F10 vs 62 in
        /// Rust). Use this helper in every cross-impl diagnostic dump.
        /// </summary>
        public static string F10(double v)
        {
            return Math.Round(v, 10, MidpointRounding.ToEven)
                .ToString(@"F10", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Log the "aborting after dump" message for the given env var and
        /// call Environment.Exit(0). Used after *_ONLY dumps where the
        /// bisection diff is the only output we care about.
        /// </summary>
        public static void ExitAfterDump(string varName)
        {
            LogAction(string.Format(@"[BISECT] {0} set - aborting after dump", varName));
            Environment.Exit(0);
        }

        /// <summary>
        /// Delegate for logging. The pipeline hooks this to its LogInfo so
        /// dump messages flow through the standard logging channel.
        /// </summary>
        public static Action<string> LogAction { get; set; } = Console.WriteLine;

        // ----- Cal sample dump -----

        /// <summary>
        /// Dump the calibration sample (targets) to
        /// &lt;fileName&gt;.cs_cal_sample.txt, sorted ordinally by tuple
        /// string for stable diff with the Rust dump. Also called when
        /// config.WritePin is set (used by downstream dev tools).
        /// </summary>
        public static void WriteCalSampleDump(string fileName, IEnumerable<LibraryEntry> sampledEntries)
        {
            string dumpPath = fileName + ".cs_cal_sample.txt";
            var tuples = new List<string>();
            foreach (var e in sampledEntries)
            {
                if (e.IsDecoy)
                    continue;
                tuples.Add(string.Format(CultureInfo.InvariantCulture,
                    "{0}\t{1}\t{2}\t{3:F4}\t{4:F4}",
                    e.Id, e.ModifiedSequence, e.Charge, e.PrecursorMz, e.RetentionTime));
            }
            tuples.Sort(StringComparer.Ordinal);
            using (var w = new StreamWriter(dumpPath))
            {
                w.WriteLine("id\tmodseq\tcharge\tmz\trt");
                foreach (var t in tuples)
                    w.WriteLine(t);
            }
            LogAction(string.Format(CultureInfo.InvariantCulture,
                @"[COUNT] Wrote calibration sample: {0} ({1} targets)",
                dumpPath, tuples.Count));
        }

        // ----- Cal scalars + grid dump (gated by same DumpCalSample flag) -----

        /// <summary>
        /// Dump calibration grid scalars (cs_cal_scalars.txt) and per-cell
        /// grid contents (cs_cal_grid.txt) for direct diff with the Rust
        /// calibration sampler.
        /// </summary>
        public static void WriteCalScalarsAndGridDump(
            IReadOnlyList<LibraryEntry> targets,
            IReadOnlyList<LibraryEntry> decoys,
            int binsPerAxis,
            double rtMin, double rtMax, double mzMin, double mzMax,
            double rtRange, double mzRange, double rtBinWidth, double mzBinWidth,
            int nOccupied, int perCell, ulong seed,
            List<int>[,] grid)
        {
            using (var w = new StreamWriter(@"cs_cal_scalars.txt"))
            {
                w.WriteLine(@"n_targets" + "\t" + targets.Count);
                w.WriteLine(@"n_decoys" + "\t" + decoys.Count);
                w.WriteLine(@"bins_per_axis" + "\t" + binsPerAxis);
                w.WriteLine(@"rt_min" + "\t" + rtMin.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"rt_max" + "\t" + rtMax.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"mz_min" + "\t" + mzMin.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"mz_max" + "\t" + mzMax.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"rt_range" + "\t" + rtRange.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"mz_range" + "\t" + mzRange.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"rt_bin_width" + "\t" + rtBinWidth.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"mz_bin_width" + "\t" + mzBinWidth.ToString(@"F17", CultureInfo.InvariantCulture));
                w.WriteLine(@"n_occupied" + "\t" + nOccupied);
                w.WriteLine(@"per_cell" + "\t" + perCell);
                w.WriteLine(@"seed" + "\t" + seed);
            }
            using (var w = new StreamWriter(@"cs_cal_grid.txt"))
            {
                w.WriteLine("rt_bin\tmz_bin\tcount\ttarget_ids");
                for (int r = 0; r < binsPerAxis; r++)
                {
                    for (int c = 0; c < binsPerAxis; c++)
                    {
                        var cell = grid[r, c];
                        if (cell.Count == 0)
                            continue;
                        var ids = new List<uint>(cell.Count);
                        foreach (int ti in cell)
                            ids.Add(targets[ti].Id);
                        ids.Sort();
                        var sb = new StringBuilder();
                        for (int k = 0; k < ids.Count; k++)
                        {
                            if (k > 0)
                                sb.Append(',');
                            sb.Append(ids[k]);
                        }
                        w.WriteLine("{0}\t{1}\t{2}\t{3}", r, c, cell.Count, sb.ToString());
                    }
                }
            }
        }

        // ----- Cal windows dump (collected in parallel, written serially) -----

        private static ConcurrentBag<string> s_calWindowRows;

        /// <summary>True if cal-window rows are currently being collected.</summary>
        public static bool CalWindowsCollecting { get { return s_calWindowRows != null; } }

        /// <summary>
        /// Begin collecting per-entry cal-window rows. Call before the
        /// parallel scoring loop if <see cref="DumpCalWindows"/> is set.
        /// </summary>
        public static void StartCalWindowCollection()
        {
            s_calWindowRows = DumpCalWindows ? new ConcurrentBag<string>() : null;
        }

        /// <summary>Add a row to the cal-window dump (thread-safe).</summary>
        public static void AddCalWindowRow(LibraryEntry entry, IsolationWindow iso,
            double expectedRt, double rtLo, double rtHi)
        {
            if (s_calWindowRows == null)
                return;
            s_calWindowRows.Add(string.Format(CultureInfo.InvariantCulture,
                "{0}\t{1}\t{2}\t{3:F6}\t{4:F6}\t{5:F6}\t{6:F6}\t{7:F6}\t{8:F6}\t{9:F6}",
                entry.Id,
                entry.IsDecoy ? 1 : 0,
                entry.Charge,
                entry.PrecursorMz,
                entry.RetentionTime,
                iso.LowerBound,
                iso.UpperBound,
                expectedRt,
                rtLo,
                rtHi));
        }

        /// <summary>
        /// Write the collected cal-window rows (cs_cal_windows.txt), sorted
        /// ordinally by row for stable diff with the Rust dump. Resets the
        /// collection so the next pass can start fresh.
        /// </summary>
        public static void WriteCalWindowsDump(int passNumber)
        {
            if (s_calWindowRows == null)
                return;
            var rows = new List<string>(s_calWindowRows);
            rows.Sort(StringComparer.Ordinal);
            using (var w = new StreamWriter(@"cs_cal_windows.txt"))
            {
                w.WriteLine("entry_id\tis_decoy\tcharge\tprecursor_mz\tlibrary_rt\tiso_lower\tiso_upper\texpected_rt\trt_window_start\trt_window_end");
                foreach (var r in rows)
                    w.WriteLine(r);
            }
            LogAction(string.Format(CultureInfo.InvariantCulture,
                @"[COUNT] Wrote calibration windows dump (pass {0}): cs_cal_windows.txt ({1} rows)",
                passNumber, rows.Count));
            s_calWindowRows = null;
        }

        // ----- Cal match dump -----

        /// <summary>
        /// Dump per-entry calibration match info (cs_cal_match.txt), sorted
        /// by entry_id for stable diff with rust_cal_match.txt. Writes a row
        /// for EVERY sampled entry (matched or not), 11 columns, F10-formatted.
        /// </summary>
        public static void WriteCalMatchDump(int passNumber,
            IEnumerable<CalibrationMatch> matches,
            IEnumerable<LibraryEntry> sampledEntries,
            IDictionary<uint, KeyValuePair<double, double>> matchRts,
            IDictionary<uint, double> snrByEntryId)
        {
            string dumpPath = @"cs_cal_match.txt";
            var matchById = new Dictionary<uint, CalibrationMatch>();
            foreach (var m in matches)
                matchById[m.EntryId] = m;

            var sortedSampled = new List<LibraryEntry>(sampledEntries);
            sortedSampled.Sort((a, b) => a.Id.CompareTo(b.Id));

            int nMatched = 0, nUnmatched = 0;
            var inv = CultureInfo.InvariantCulture;
            using (var w = new StreamWriter(dumpPath))
            {
                w.WriteLine("entry_id\tis_decoy\tcharge\thas_match\tscan\tapex_rt\tcorrelation\tlibcosine\ttop6\txcorr\tsnr");
                foreach (var entry in sortedSampled)
                {
                    CalibrationMatch m;
                    if (matchById.TryGetValue(entry.Id, out m))
                    {
                        KeyValuePair<double, double> rtPair;
                        matchRts.TryGetValue(entry.Id, out rtPair);
                        double snr;
                        if (!snrByEntryId.TryGetValue(entry.Id, out snr))
                            snr = 0.0;
                        w.WriteLine(string.Format(inv,
                            "{0}\t{1}\t{2}\t1\t{3}\t{4:F10}\t{5:F10}\t{6:F10}\t{7}\t{8:F10}\t{9:F10}",
                            entry.Id,
                            entry.IsDecoy ? 1 : 0,
                            entry.Charge,
                            m.ScanNumber,
                            rtPair.Value,
                            m.CorrelationScore,
                            m.LibcosineApex,
                            m.Top6MatchedApex,
                            m.XcorrScore,
                            snr));
                        nMatched++;
                    }
                    else
                    {
                        w.WriteLine("{0}\t{1}\t{2}\t0\t\t\t\t\t\t\t",
                            entry.Id,
                            entry.IsDecoy ? 1 : 0,
                            entry.Charge);
                        nUnmatched++;
                    }
                }
            }
            LogAction(string.Format(inv,
                @"[COUNT] Wrote calibration match dump (pass {0}): {1} ({2} matched, {3} unmatched)",
                passNumber, dumpPath, nMatched, nUnmatched));
        }

        // ----- LDA scores dump -----

        /// <summary>
        /// Dump per-entry LDA discriminant + q-value (cs_lda_scores.txt),
        /// sorted by entry_id, F10-formatted.
        /// </summary>
        public static void WriteLdaScoresDump(int passNumber, IEnumerable<CalibrationMatch> matchArray)
        {
            var sortedByEntry = matchArray.OrderBy(m => m.EntryId).ToArray();
            var inv = CultureInfo.InvariantCulture;
            using (var w = new StreamWriter(@"cs_lda_scores.txt"))
            {
                w.WriteLine("entry_id\tis_decoy\tdiscriminant\tq_value");
                foreach (var m in sortedByEntry)
                {
                    w.WriteLine(string.Format(inv,
                        "{0}\t{1}\t{2:F10}\t{3:F10}",
                        m.EntryId,
                        m.IsDecoy ? 1 : 0,
                        m.DiscriminantScore,
                        m.QValue));
                }
            }
            LogAction(string.Format(inv,
                @"[COUNT] Wrote LDA scores dump (pass {0}): cs_lda_scores.txt ({1} entries)",
                passNumber, sortedByEntry.Length));
        }

        // ----- LOESS input dump -----

        /// <summary>
        /// Dump the (lib_rt, measured_rt) pairs fed to LOESS
        /// (cs_loess_input.txt), sorted by (lib_rt, measured_rt), at F17
        /// precision (matches Rust's `{:.17}`).
        /// </summary>
        public static void WriteLoessInputDump(int passNumber, double[] libRts, double[] measuredRts)
        {
            var pairs = new List<KeyValuePair<double, double>>(libRts.Length);
            for (int i = 0; i < libRts.Length; i++)
                pairs.Add(new KeyValuePair<double, double>(libRts[i], measuredRts[i]));
            pairs.Sort((a, b) =>
            {
                int c = a.Key.CompareTo(b.Key);
                if (c != 0)
                    return c;
                return a.Value.CompareTo(b.Value);
            });
            var inv = CultureInfo.InvariantCulture;
            using (var w = new StreamWriter(@"cs_loess_input.txt"))
            {
                w.WriteLine(string.Format(inv,
                    "# n_library_rts={0} n_measured_rts={1}", libRts.Length, measuredRts.Length));
                w.WriteLine("idx\tlib_rt\tmeasured_rt");
                for (int i = 0; i < pairs.Count; i++)
                {
                    w.WriteLine(string.Format(inv, "{0}\t{1:F17}\t{2:F17}",
                        i, pairs[i].Key, pairs[i].Value));
                }
            }
            LogAction(string.Format(inv,
                @"[COUNT] Wrote LOESS input dump (pass {0}): cs_loess_input.txt ({1} pairs)",
                passNumber, pairs.Count));
        }

        // ----- Calibration summary dump -----

        /// <summary>
        /// Dump 11 key calibration scalars to cs_cal_summary.txt so the
        /// post-LOESS calibration state can be compared to Rust's cal
        /// JSON. Emits MS1/MS2 mean/sd/count/tolerance + RT n_points /
        /// r_squared / residual_sd at F17 precision. Does not gate on
        /// an env var -- always runs when calibration completes, at
        /// essentially zero cost.
        /// </summary>
        public static void WriteCalibrationSummary(
            RTCalibration rtCal,
            MzCalibrationResult ms1Cal,
            MzCalibrationResult ms2Cal)
        {
            var inv = CultureInfo.InvariantCulture;
            using (var w = new StreamWriter(@"cs_cal_summary.txt"))
            {
                Action<string, double> writeD = (key, val) =>
                    w.WriteLine(key + "\t" + val.ToString(@"F17", inv));
                Action<string, int> writeI = (key, val) =>
                    w.WriteLine(key + "\t" + val.ToString(inv));

                if (ms1Cal != null)
                {
                    writeD("ms1.mean",      ms1Cal.Mean);
                    writeD("ms1.sd",        ms1Cal.SD);
                    writeI("ms1.count",     ms1Cal.Count);
                    writeD("ms1.tolerance", ms1Cal.AdjustedTolerance ?? 0.0);
                }
                if (ms2Cal != null)
                {
                    writeD("ms2.mean",      ms2Cal.Mean);
                    writeD("ms2.sd",        ms2Cal.SD);
                    writeI("ms2.count",     ms2Cal.Count);
                    writeD("ms2.tolerance", ms2Cal.AdjustedTolerance ?? 0.0);
                }
                if (rtCal != null)
                {
                    var stats = rtCal.Stats();
                    writeI("rt.n_points",    stats.NPoints);
                    writeD("rt.r_squared",   stats.RSquared);
                    writeD("rt.residual_sd", stats.ResidualSD);
                }
            }
            LogAction(string.Format(inv,
                @"[COUNT] Wrote calibration summary: cs_cal_summary.txt (11 scalars)"));
        }

        // ----- Per-entry calibration XIC dump -----

        /// <summary>
        /// True if the current entry should produce a cs_xic_entry_&lt;ID&gt;.txt
        /// dump during calibration scoring. Checks both the target entry ID
        /// and the target pass number.
        /// </summary>
        public static bool ShouldDumpCalXicFor(uint entryId, int currentPass)
        {
            return DiagXicEntryId.HasValue &&
                DiagXicEntryId.Value == entryId &&
                currentPass == DiagXicPass;
        }

        /// <summary>
        /// Write the per-entry calibration XIC dump (cs_xic_entry_&lt;ID&gt;.txt).
        /// Includes LOESS stats (pass 2), pass calculations, candidates, top-6
        /// fragments, and all extracted XICs. After writing, calls
        /// Environment.Exit(0) -- the bisection only cares about this one
        /// file, there is no need to let the remaining parallel workers
        /// finish scoring.
        /// </summary>
        public static void WriteCalXicEntryDumpAndExit(LibraryEntry entry, int currentPass,
            RTCalibration calibrationModel, double expectedRt, double initialTolerance,
            double rtSlope, double rtIntercept,
            IReadOnlyList<Spectrum> candidateSpectra,
            IReadOnlyList<XicData> xics)
        {
            string diagXicPath = @"cs_xic_entry_" + entry.Id + @".txt";
            var inv = CultureInfo.InvariantCulture;
            using (var dw = new StreamWriter(diagXicPath))
            {
                dw.WriteLine(string.Format(inv,
                    @"# per-entry chromatogram dump for entry_id={0} (pass {1})",
                    entry.Id, currentPass));
                dw.WriteLine(string.Format(inv,
                    @"# {0} ({1}, charge={2}, lib_rt={3:F10}, mz={4:F10})",
                    entry.ModifiedSequence, entry.Sequence, entry.Charge,
                    entry.RetentionTime, entry.PrecursorMz));

                if (calibrationModel != null)
                {
                    var loessStats = calibrationModel.Stats();
                    dw.WriteLine("# LOESS MODEL (pass 2 RT calibration)");
                    dw.WriteLine(string.Format(inv, "# loess.n_points={0}", loessStats.NPoints));
                    dw.WriteLine(string.Format(inv, "# loess.r_squared={0:F10}", loessStats.RSquared));
                    dw.WriteLine(string.Format(inv, "# loess.residual_sd={0:F10}", loessStats.ResidualSD));
                    dw.WriteLine(string.Format(inv, "# loess.mean_residual={0:F10}", loessStats.MeanResidual));
                    dw.WriteLine(string.Format(inv, "# loess.max_residual={0:F10}", loessStats.MaxResidual));
                    dw.WriteLine(string.Format(inv, "# loess.p20_abs_residual={0:F10}", loessStats.P20AbsResidual));
                    dw.WriteLine(string.Format(inv, "# loess.p80_abs_residual={0:F10}", loessStats.P80AbsResidual));
                    dw.WriteLine(string.Format(inv, "# loess.mad={0:F10}", loessStats.MAD));
                }
                dw.WriteLine("# PASS CALCULATIONS");
                dw.WriteLine(string.Format(inv, "# pass.library_rt={0:F10}", entry.RetentionTime));
                dw.WriteLine(string.Format(inv, "# pass.expected_rt={0:F10}", expectedRt));
                dw.WriteLine(string.Format(inv, "# pass.tolerance={0:F10}", initialTolerance));
                dw.WriteLine(string.Format(inv, "# pass.rt_window_lo={0:F10}", expectedRt - initialTolerance));
                dw.WriteLine(string.Format(inv, "# pass.rt_window_hi={0:F10}", expectedRt + initialTolerance));
                dw.WriteLine(string.Format(inv, "# pass.rt_slope={0:F10}", rtSlope));
                dw.WriteLine(string.Format(inv, "# pass.rt_intercept={0:F10}", rtIntercept));

                dw.WriteLine("# n_post_prefilter_candidates=" + candidateSpectra.Count);
                dw.WriteLine("# CANDIDATES (post-prefilter, sorted by RT)");
                dw.WriteLine("candidate\tscan_idx\tscan_number\trt\tiso_lower\tiso_upper");
                for (int i = 0; i < candidateSpectra.Count; i++)
                {
                    var iso = candidateSpectra[i].IsolationWindow;
                    dw.WriteLine(string.Format(inv,
                        "candidate\t{0}\t{1}\t{2}\t{3}\t{4}",
                        i, candidateSpectra[i].ScanNumber,
                        F10(candidateSpectra[i].RetentionTime),
                        F10(iso.LowerBound), F10(iso.UpperBound)));
                }

                var sortedByIntensity = new List<KeyValuePair<int, float>>(entry.Fragments.Count);
                for (int fi = 0; fi < entry.Fragments.Count; fi++)
                    sortedByIntensity.Add(new KeyValuePair<int, float>(fi, entry.Fragments[fi].RelativeIntensity));
                sortedByIntensity.Sort((a, b) => b.Value.CompareTo(a.Value));
                int topN = Math.Min(6, sortedByIntensity.Count);

                dw.WriteLine("# TOP-6 FRAGMENTS (selected by intensity desc)");
                dw.WriteLine("topfrag\ttop_idx\tlib_idx\tlib_mz\tlib_intensity");
                for (int rank = 0; rank < topN; rank++)
                {
                    int fi = sortedByIntensity[rank].Key;
                    var fobj = entry.Fragments[fi];
                    dw.WriteLine(string.Format(inv,
                        "topfrag\t{0}\t{1}\t{2}\t{3}",
                        rank, fi, F10(fobj.Mz), F10(fobj.RelativeIntensity)));
                }

                dw.WriteLine("# EXTRACTED XICS (lib_idx, scan_idx, rt, intensity)");
                dw.WriteLine("xic\tlib_idx\tscan_idx\trt\tintensity");
                foreach (var xic in xics)
                {
                    for (int i = 0; i < xic.RetentionTimes.Length; i++)
                    {
                        dw.WriteLine(string.Format(inv,
                            "xic\t{0}\t{1}\t{2}\t{3}",
                            xic.FragmentIndex, i, F10(xic.RetentionTimes[i]), F10(xic.Intensities[i])));
                    }
                }
            }
            LogAction(string.Format(inv,
                @"[BISECT] OSPREY_DIAG_XIC_ENTRY_ID matched on pass {0} - wrote {1} and exiting",
                currentPass, diagXicPath));
            Environment.Exit(0);
        }

        // ----- Main-search XIC dump (non-exiting, per entry) -----

        /// <summary>
        /// True if the given entry ID should produce a
        /// cs_search_xic_entry_&lt;ID&gt;.txt dump during the main search.
        /// </summary>
        public static bool ShouldDumpSearchXicFor(uint entryId)
        {
            return DiagSearchEntryIds != null && DiagSearchEntryIds.Contains(entryId);
        }

        /// <summary>
        /// Write the per-entry main-search XIC dump
        /// (cs_search_xic_entry_&lt;ID&gt;.txt). Does NOT exit so multiple
        /// entries can be collected in one run.
        /// </summary>
        public static void WriteSearchXicDump(LibraryEntry candidate,
            double expectedRt, double rtTolerance,
            int startScan, int endScan, int rangeLen,
            IReadOnlyList<Spectrum> windowSpectra,
            IReadOnlyList<XicData> xics)
        {
            string dumpPath = @"cs_search_xic_entry_" + candidate.Id + @".txt";
            var inv = CultureInfo.InvariantCulture;
            using (var dw = new StreamWriter(dumpPath))
            {
                dw.WriteLine(string.Format(inv,
                    @"# search XIC dump for entry_id={0}", candidate.Id));
                dw.WriteLine(string.Format(inv,
                    @"# {0} ({1}, charge={2}, lib_rt={3:F10}, mz={4:F10})",
                    candidate.ModifiedSequence, candidate.Sequence, candidate.Charge,
                    candidate.RetentionTime, candidate.PrecursorMz));
                dw.WriteLine(string.Format(inv,
                    @"# is_decoy={0}", candidate.IsDecoy ? 1 : 0));
                dw.WriteLine(string.Format(inv,
                    @"# expected_rt={0:F10}", expectedRt));
                dw.WriteLine(string.Format(inv,
                    @"# rt_tolerance={0:F10}", rtTolerance));
                dw.WriteLine(string.Format(inv,
                    @"# scan_range=[{0}..{1}] n_scans={2}",
                    startScan, endScan, rangeLen));
                dw.WriteLine("# CANDIDATES (scan_idx, scan_number, rt)");
                dw.WriteLine("candidate\tscan_idx\tscan_number\trt");
                for (int i = startScan; i <= endScan; i++)
                {
                    dw.WriteLine(string.Format(inv,
                        "candidate\t{0}\t{1}\t{2:F10}",
                        i - startScan, windowSpectra[i].ScanNumber,
                        windowSpectra[i].RetentionTime));
                }
                dw.WriteLine("# EXTRACTED XICS (lib_idx, scan_idx, rt, intensity)");
                dw.WriteLine("xic\tlib_idx\tscan_idx\trt\tintensity");
                foreach (var xic in xics)
                {
                    for (int i = 0; i < xic.RetentionTimes.Length; i++)
                    {
                        dw.WriteLine(string.Format(inv,
                            "xic\t{0}\t{1}\t{2}\t{3}",
                            xic.FragmentIndex, i, F10(xic.RetentionTimes[i]), F10(xic.Intensities[i])));
                    }
                }
            }
            LogAction(string.Format(inv,
                @"[BISECT] Search XIC dump for entry {0}: {1} xics, {2} scans -> {3}",
                candidate.Id, xics.Count, rangeLen, dumpPath));
        }

        // ----- Median polish scan dump -----

        /// <summary>
        /// True if the median polish diagnostic should fire for the given
        /// apex scan number AND a candidate whose modified sequence starts
        /// with DECOY_ALQFAQWWK (historical bisection target).
        /// </summary>
        public static bool ShouldDumpMpFor(uint apexScanNumber, string candidateModifiedSequence)
        {
            return DiagMpScan.HasValue &&
                (int)apexScanNumber == DiagMpScan.Value &&
                candidateModifiedSequence != null &&
                candidateModifiedSequence.StartsWith(@"DECOY_ALQFAQWWK");
        }

        /// <summary>
        /// Write the median polish diagnostic (cs_mp_diag.txt).
        /// </summary>
        public static void WriteMpDump(LibraryEntry candidate, uint apexScanNumber,
            XICPeakBounds bestPeak, int peakLen,
            double mpCosine, double mpResidualRatio, double mpMinFragmentR2, double mpResidualCorr,
            TukeyMedianPolishResult polish,
            IList<KeyValuePair<int, double[]>> peakXics)
        {
            var inv = CultureInfo.InvariantCulture;
            using (var dw = new StreamWriter(@"cs_mp_diag.txt"))
            {
                dw.WriteLine(string.Format(inv,
                    @"# Median polish diagnostic for {0} scan={1}",
                    candidate.ModifiedSequence, apexScanNumber));
                dw.WriteLine(string.Format(inv,
                    @"# peak range: start={0} apex={1} end={2} len={3}",
                    bestPeak.StartIndex, bestPeak.ApexIndex, bestPeak.EndIndex, peakLen));
                dw.WriteLine(string.Format(inv,
                    @"# mp_cosine={0:F10} mp_rr={1:F10} mp_r2={2:F10} mp_rc={3:F10}",
                    mpCosine, mpResidualRatio, mpMinFragmentR2, mpResidualCorr));
                dw.WriteLine("# ELUTION PROFILE (ColEffects)");
                for (int ep = 0; ep < polish.ColEffects.Length; ep++)
                    dw.WriteLine(string.Format(inv,
                        "elution\t{0}\t{1:F10}", ep, polish.ColEffects[ep]));
                dw.WriteLine("# FRAGMENT EFFECTS (RowEffects)");
                for (int fe = 0; fe < polish.RowEffects.Length; fe++)
                    dw.WriteLine(string.Format(inv,
                        "frag_effect\t{0}\t{1:F10}", fe, polish.RowEffects[fe]));
                dw.WriteLine(string.Format(inv,
                    @"# grand_mean={0:F10}", polish.Overall));
                dw.WriteLine(string.Format(inv,
                    @"# n_iterations={0} converged={1}", polish.NIterations, polish.Converged));
                dw.WriteLine("# INPUT MATRIX (frag_idx, scan_idx, value)");
                for (int xi = 0; xi < peakXics.Count; xi++)
                    for (int s = 0; s < peakXics[xi].Value.Length; s++)
                        dw.WriteLine(string.Format(inv,
                            "input\t{0}\t{1}\t{2:F10}", xi, s, peakXics[xi].Value[s]));
            }
            LogAction(@"[BISECT] Wrote median polish diagnostic: cs_mp_diag.txt");
        }

        // ----- Env var parsing helpers -----

        private static bool IsOne(string name)
        {
            return Environment.GetEnvironmentVariable(name) == @"1";
        }

        private static uint? ParseNullableUint(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return null;
            uint result;
            if (uint.TryParse(v, out result))
                return result;
            return null;
        }

        private static int? ParseNullableInt(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return null;
            int result;
            if (int.TryParse(v, out result))
                return result;
            return null;
        }

        private static int ParseIntOrDefault(string name, int fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return fallback;
            int result;
            if (int.TryParse(v, out result))
                return result;
            return fallback;
        }

        private static HashSet<uint> ParseIdSet(string name)
        {
            string v = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(v))
                return null;
            var set = new HashSet<uint>();
            foreach (string part in v.Split(','))
            {
                uint id;
                if (uint.TryParse(part.Trim(), out id))
                    set.Add(id);
            }
            return set.Count > 0 ? set : null;
        }

        /// <summary>
        /// Dump per-precursor Stage 5 (first-pass Percolator FDR) state to
        /// cs_stage5_percolator.tsv, mirroring Rust's rust_stage5_percolator.tsv.
        /// All four q-values plus score and pep are populated on every FdrEntry
        /// at this point (before compaction or first-pass protein FDR), so the
        /// cross-impl diff sees both targets and decoys.
        ///
        /// Columns: file_name, entry_id, charge, modified_sequence, is_decoy,
        /// score, pep, run_precursor_q, run_peptide_q, experiment_precursor_q,
        /// experiment_peptide_q. Rows sorted by (file_name, entry_id) for
        /// stable human inspection; Compare-Percolator.ps1 hash-joins on the
        /// composite key and is sort-order-agnostic. Floats use G17 (17-digit
        /// roundtrippable) to avoid .NET Framework's historical "R"-format
        /// roundtrip bugs.
        /// </summary>
        public static void WriteStage5PercolatorDump(List<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            const string path = @"cs_stage5_percolator.tsv";
            var inv = CultureInfo.InvariantCulture;

            var rows = new List<KeyValuePair<string, FdrEntry>>();
            foreach (var kvp in perFileEntries)
            {
                string fileName = kvp.Key;
                foreach (var e in kvp.Value)
                    rows.Add(new KeyValuePair<string, FdrEntry>(fileName, e));
            }
            rows.Sort((a, b) =>
            {
                int cmp = string.CompareOrdinal(a.Key, b.Key);
                if (cmp != 0) return cmp;
                return a.Value.EntryId.CompareTo(b.Value.EntryId);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine(@"file_name	entry_id	charge	modified_sequence	is_decoy	score	pep	run_precursor_q	run_peptide_q	experiment_precursor_q	experiment_peptide_q");
                foreach (var row in rows)
                {
                    var e = row.Value;
                    sw.Write(row.Key);
                    sw.Write('\t'); sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Charge.ToString(inv));
                    sw.Write('\t'); sw.Write(e.ModifiedSequence ?? string.Empty);
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(e.Score.ToString(@"G17", inv));
                    sw.Write('\t'); sw.Write(e.Pep.ToString(@"G17", inv));
                    sw.Write('\t'); sw.Write(e.RunPrecursorQvalue.ToString(@"G17", inv));
                    sw.Write('\t'); sw.Write(e.RunPeptideQvalue.ToString(@"G17", inv));
                    sw.Write('\t'); sw.Write(e.ExperimentPrecursorQvalue.ToString(@"G17", inv));
                    sw.Write('\t'); sw.WriteLine(e.ExperimentPeptideQvalue.ToString(@"G17", inv));
                }
            }
            LogAction(string.Format(@"Wrote Stage 5 Percolator dump: {0} ({1} rows)", path, rows.Count));
        }
    }
}
