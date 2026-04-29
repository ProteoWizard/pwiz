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
using pwiz.OspreySharp.FDR;
using pwiz.OspreySharp.FDR.Reconciliation;
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
    /// Use invariant culture and the same field ordering as the Rust
    /// equivalents. Two floating-point formats are in use by convention:
    /// <list type="bullet">
    /// <item><description>Stages 1-4 dumps use <see cref="F10"/>
    /// (10 decimal places, round-half-to-even), matching the Rust
    /// equivalents for those stages.</description></item>
    /// <item><description>Stage 5+ dumps (standardizer, subsample, SVM
    /// weights, Percolator) use
    /// <see cref="Diagnostics.FormatF64Roundtrip"/>, which emits the
    /// shortest decimal that round-trips to the same f64 -- matching
    /// Rust's ryu-based <c>format!("{}", v)</c> output byte-for-byte.
    /// Do not switch these fields to F10 or to raw "G17".</description>
    /// </item>
    /// </list>
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
        /// OSPREY_DUMP_CONSENSUS: dump the per-peptide consensus RT planning
        /// state at the start of Stage 6 (cs_stage6_consensus.tsv) for
        /// cross-impl parity at the planning checkpoint.
        /// </summary>
        public static readonly bool DumpConsensus = IsOne(@"OSPREY_DUMP_CONSENSUS");

        /// <summary>OSPREY_CONSENSUS_ONLY: exit after cs_stage6_consensus.tsv dump.</summary>
        public static readonly bool ConsensusOnly = IsOne(@"OSPREY_CONSENSUS_ONLY");

        /// <summary>
        /// OSPREY_DUMP_MULTICHARGE: dump the per-file multi-charge consensus
        /// rescore targets (cs_stage6_multicharge.tsv) for cross-impl parity.
        /// </summary>
        public static readonly bool DumpMulticharge = IsOne(@"OSPREY_DUMP_MULTICHARGE");

        /// <summary>OSPREY_MULTICHARGE_ONLY: exit after cs_stage6_multicharge.tsv dump.</summary>
        public static readonly bool MultichargeOnly = IsOne(@"OSPREY_MULTICHARGE_ONLY");

        /// <summary>
        /// OSPREY_DUMP_REFIT: dump per-file refined-calibration statistics
        /// produced by CalibrationRefit (cs_stage6_refit.tsv) for cross-impl
        /// parity.
        /// </summary>
        public static readonly bool DumpRefit = IsOne(@"OSPREY_DUMP_REFIT");

        /// <summary>OSPREY_REFIT_ONLY: exit after cs_stage6_refit.tsv dump.</summary>
        public static readonly bool RefitOnly = IsOne(@"OSPREY_REFIT_ONLY");

        /// <summary>
        /// OSPREY_DUMP_RECONCILIATION: dump the per-(file, entry)
        /// ReconcileAction map produced by ReconciliationPlanner.Plan
        /// to cs_stage6_reconciliation.tsv. Mirrors the action variant
        /// + apex/start/end/half_width fields the Rust dump emits, so
        /// cross-impl parity at the planning step can be checked
        /// independently of the per-file rescoring that follows.
        /// </summary>
        public static readonly bool DumpReconciliation = IsOne(@"OSPREY_DUMP_RECONCILIATION");

        /// <summary>OSPREY_RECONCILIATION_ONLY: exit after cs_stage6_reconciliation.tsv dump.</summary>
        public static readonly bool ReconciliationOnly = IsOne(@"OSPREY_RECONCILIATION_ONLY");

        /// <summary>
        /// OSPREY_DUMP_CALIBRATION: dump the loaded calibration JSON
        /// arrays (library_rts + fitted_values) for each file to
        /// cs_stage6_calibration.tsv as the C# join-only path loads
        /// them. Diffing against rust_stage6_calibration.tsv localizes
        /// whether cross-impl divergence in InversePredict enters at
        /// the JSON f64 parser (decimal-to-binary) or inside the LOESS
        /// interpolation arithmetic. No _ONLY companion: pair with a
        /// later dump's _ONLY (e.g. OSPREY_INV_PREDICT_ONLY) to
        /// short-circuit after all files have written their rows.
        /// </summary>
        public static readonly bool DumpCalibration = IsOne(@"OSPREY_DUMP_CALIBRATION");

        /// <summary>OSPREY_CALIBRATION_ONLY: exit after cs_stage6_calibration.tsv dump.</summary>
        public static readonly bool CalibrationOnly = IsOne(@"OSPREY_CALIBRATION_ONLY");

        /// <summary>
        /// OSPREY_DUMP_INV_PREDICT: capture per-detection
        /// (apex_rt, library_rt, weight) triples flowing into
        /// ConsensusRts.Compute and dump them to cs_stage6_inv_predict.tsv
        /// for ULP-level bisection of consensus_library_rt cross-impl
        /// divergence. Diffing against rust_stage6_inv_predict.tsv
        /// localizes whether the divergence enters at Parquet f64 decode
        /// (apex_rt diverges) or inside LOESS InversePredict (only
        /// library_rt diverges).
        /// </summary>
        public static readonly bool DumpInvPredict = IsOne(@"OSPREY_DUMP_INV_PREDICT");

        /// <summary>OSPREY_INV_PREDICT_ONLY: exit after cs_stage6_inv_predict.tsv dump.</summary>
        public static readonly bool InvPredictOnly = IsOne(@"OSPREY_INV_PREDICT_ONLY");

        /// <summary>
        /// OSPREY_DUMP_PROTEIN_FDR: dump the per-peptide first-pass protein
        /// FDR state (gate input + ranking input + propagated output) to
        /// cs_stage6_protein_fdr.tsv after RunFirstPassProteinFdr completes.
        /// Used to bisect the SQC[UniMod:4]LQVPER borderline cross-impl
        /// divergence in run_protein_qvalue: matching against
        /// rust_stage6_protein_fdr.tsv shows whether the gate input
        /// (best_qvalue), the ranking input (score), or the algorithm output
        /// (protein_qvalue) is responsible.
        /// </summary>
        public static readonly bool DumpProteinFdr = IsOne(@"OSPREY_DUMP_PROTEIN_FDR");

        /// <summary>OSPREY_PROTEIN_FDR_ONLY: exit after cs_stage6_protein_fdr.tsv dump.</summary>
        public static readonly bool ProteinFdrOnly = IsOne(@"OSPREY_PROTEIN_FDR_ONLY");

        /// <summary>
        /// OSPREY_DUMP_LOESS_FIT: dump the per-point LOESS fit state of the
        /// Stage 6 refit RTCalibration to cs_stage6_loess_fit.tsv. Used to
        /// bisect the refit ULP divergence: if (library_rt, fitted_value,
        /// abs_residual) match cross-impl, the divergence is in the
        /// stats computation (R²/SD/MAD). If fitted_value diverges, the
        /// LOESS smoother arithmetic itself differs.
        /// </summary>
        public static readonly bool DumpLoessFit = IsOne(@"OSPREY_DUMP_LOESS_FIT");

        /// <summary>OSPREY_LOESS_FIT_ONLY: exit after cs_stage6_loess_fit.tsv dump.</summary>
        public static readonly bool LoessFitOnly = IsOne(@"OSPREY_LOESS_FIT_ONLY");

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
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	entry_id	charge	modified_sequence	is_decoy	score	pep	run_precursor_q	run_peptide_q	experiment_precursor_q	experiment_peptide_q");
                foreach (var row in rows)
                {
                    var e = row.Value;
                    sw.Write(row.Key);
                    sw.Write('\t'); sw.Write(e.EntryId.ToString(inv));
                    sw.Write('\t'); sw.Write(e.Charge.ToString(inv));
                    sw.Write('\t'); sw.Write(e.ModifiedSequence ?? string.Empty);
                    sw.Write('\t'); sw.Write(e.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(e.Score));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(e.Pep));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(e.RunPrecursorQvalue));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(e.RunPeptideQvalue));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(e.ExperimentPrecursorQvalue));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(e.ExperimentPeptideQvalue));
                }
            }
            LogAction(string.Format(@"Wrote Stage 5 Percolator dump: {0} ({1} rows)", path, rows.Count));
        }

        // ---- Stage 6 planning dumps ----

        /// <summary>
        /// Dump the per-peptide consensus RT planning state to
        /// cs_stage6_consensus.tsv. Mirrors Rust dump_stage6_consensus.
        /// Columns: is_decoy, modified_sequence, consensus_library_rt,
        /// median_peak_width, n_runs_detected, apex_library_rt_mad. Rows
        /// are emitted in the order produced by ConsensusRts.Compute, which
        /// sorts by (is_decoy, modified_sequence) for deterministic output.
        /// apex_library_rt_mad is empty when fewer than 3 detections
        /// contributed.
        /// </summary>
        public static void WriteStage6ConsensusDump(IReadOnlyList<PeptideConsensusRT> consensus)
        {
            const string path = @"cs_stage6_consensus.tsv";

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"is_decoy	modified_sequence	consensus_library_rt	median_peak_width	n_runs_detected	apex_library_rt_mad");
                foreach (var c in consensus)
                {
                    sw.Write(c.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(c.ModifiedSequence ?? string.Empty);
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(c.ConsensusLibraryRt));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(c.MedianPeakWidth));
                    sw.Write('\t'); sw.Write(c.NRunsDetected.ToString(CultureInfo.InvariantCulture));
                    sw.Write('\t');
                    if (c.ApexLibraryRtMad.HasValue)
                        sw.Write(Diagnostics.FormatF64Roundtrip(c.ApexLibraryRtMad.Value));
                    sw.WriteLine();
                }
            }
            LogAction(string.Format(@"Wrote Stage 6 consensus dump: {0} ({1} rows)", path, consensus.Count));
        }

        /// <summary>
        /// Dump the per-file multi-charge consensus rescore targets to
        /// cs_stage6_multicharge.tsv. Mirrors Rust dump_stage6_multicharge.
        /// Columns: file_name, entry_id, consensus_apex, consensus_start,
        /// consensus_end. Rows sorted by (file_name, entry_id) for stable
        /// diff. The dump uses the stable library entry id (FdrEntry.EntryId)
        /// instead of the per-file Vec position, so cross-impl comparison is
        /// invariant to whether the implementation has compacted the
        /// per-file FdrEntry list before computing multi-charge consensus.
        /// </summary>
        public static void WriteStage6MultichargeDump(
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<string, IReadOnlyList<(int Index, double Apex, double Start, double End)>> perFileTargets)
        {
            const string path = @"cs_stage6_multicharge.tsv";

            // Resolve Index -> EntryId via the matching per-file entries list.
            var entriesByFile = new Dictionary<string, IReadOnlyList<FdrEntry>>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
                entriesByFile[kvp.Key] = kvp.Value;

            var rows = new List<(string FileName, uint EntryId, double Apex, double Start, double End)>();
            foreach (var kvp in perFileTargets)
            {
                IReadOnlyList<FdrEntry> entries;
                if (!entriesByFile.TryGetValue(kvp.Key, out entries))
                    continue;
                foreach (var t in kvp.Value)
                {
                    if (t.Index < 0 || t.Index >= entries.Count)
                        continue;
                    rows.Add((kvp.Key, entries[t.Index].EntryId, t.Apex, t.Start, t.End));
                }
            }
            rows.Sort((a, b) =>
            {
                int cmp = string.CompareOrdinal(a.FileName, b.FileName);
                if (cmp != 0) return cmp;
                return a.EntryId.CompareTo(b.EntryId);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	entry_id	consensus_apex	consensus_start	consensus_end");
                foreach (var r in rows)
                {
                    sw.Write(r.FileName);
                    sw.Write('\t'); sw.Write(r.EntryId.ToString(CultureInfo.InvariantCulture));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(r.Apex));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(r.Start));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(r.End));
                }
            }
            LogAction(string.Format(@"Wrote Stage 6 multi-charge dump: {0} ({1} rows)", path, rows.Count));
        }

        /// <summary>
        /// Dump per-file refined-calibration statistics to
        /// cs_stage6_refit.tsv. Mirrors Rust dump_stage6_refit. Columns:
        /// file_name, n_points, r_squared, residual_sd, mad. Files where the
        /// refit failed (insufficient points) are absent. Rows sorted by
        /// file_name for stable diff.
        /// </summary>
        public static void WriteStage6RefitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations)
        {
            const string path = @"cs_stage6_refit.tsv";

            var fileNames = new List<string>(refinedCalibrations.Keys);
            fileNames.Sort(StringComparer.Ordinal);

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	n_points	r_squared	residual_sd	mad");
                foreach (var fileName in fileNames)
                {
                    var stats = refinedCalibrations[fileName].Stats();
                    sw.Write(fileName);
                    sw.Write('\t'); sw.Write(stats.NPoints.ToString(CultureInfo.InvariantCulture));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(stats.RSquared));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(stats.ResidualSD));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(stats.MAD));
                }
            }
            LogAction(string.Format(@"Wrote Stage 6 refit dump: {0} ({1} rows)", path, fileNames.Count));
        }

        /// <summary>
        /// Dump the per-(file, entry) <see cref="ReconcileAction"/> map
        /// produced by <see cref="ReconciliationPlanner.Plan"/> to
        /// <c>cs_stage6_reconciliation.tsv</c>. Used for cross-impl parity
        /// of the planner output -- pairs with the Rust
        /// <c>dump_stage6_reconciliation</c> helper. Columns:
        /// <c>file_name, entry_id, action, apex_or_expected_rt, start_rt,
        /// end_rt, half_width, candidate_index</c>. Cells that don't
        /// apply to a given action stay empty. Rows are sorted by
        /// <c>(file_name, entry_id)</c> for stable diffing.
        /// </summary>
        public static void WriteStage6ReconciliationDump(
            IReadOnlyDictionary<(string File, int Index), ReconcileAction> actions,
            IReadOnlyList<KeyValuePair<string, IReadOnlyList<FdrEntry>>> perFileEntries)
        {
            const string path = @"cs_stage6_reconciliation.tsv";

            // Build (file, entryId, action) rows. The planner's key is
            // (file, list-index); we resolve back to the entry_id via
            // perFileEntries so the dump is sortable by entry_id and
            // joinable with other dumps that key off entry_id.
            var perFileEntriesMap = new Dictionary<string, IReadOnlyList<FdrEntry>>(
                perFileEntries.Count);
            foreach (var kvp in perFileEntries)
                perFileEntriesMap[kvp.Key] = kvp.Value;

            var rows = new List<(string FileName, uint EntryId, ReconcileAction Action)>(
                actions.Count);
            foreach (var kvp in actions)
            {
                IReadOnlyList<FdrEntry> entries;
                if (!perFileEntriesMap.TryGetValue(kvp.Key.File, out entries))
                    continue;
                if (kvp.Key.Index < 0 || kvp.Key.Index >= entries.Count)
                    continue;
                rows.Add((kvp.Key.File, entries[kvp.Key.Index].EntryId, kvp.Value));
            }
            rows.Sort((a, b) =>
            {
                int cmp = string.CompareOrdinal(a.FileName, b.FileName);
                if (cmp != 0) return cmp;
                return a.EntryId.CompareTo(b.EntryId);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	entry_id	action	apex_or_expected_rt	start_rt	end_rt	half_width	candidate_index");
                foreach (var row in rows)
                {
                    sw.Write(row.FileName);
                    sw.Write('\t'); sw.Write(row.EntryId.ToString(CultureInfo.InvariantCulture));
                    sw.Write('\t');
                    var useCwt = row.Action as ReconcileAction.UseCwtPeak;
                    var forced = row.Action as ReconcileAction.ForcedIntegration;
                    if (useCwt != null)
                    {
                        sw.Write(@"use_cwt_peak");
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(useCwt.ApexRt));
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(useCwt.StartRt));
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(useCwt.EndRt));
                        sw.Write('\t'); // half_width empty
                        sw.Write('\t'); sw.WriteLine(useCwt.CandidateIndex.ToString(CultureInfo.InvariantCulture));
                    }
                    else if (forced != null)
                    {
                        sw.Write(@"forced_integration");
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(forced.ExpectedRt));
                        sw.Write('\t'); // start_rt empty
                        sw.Write('\t'); // end_rt empty
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(forced.HalfWidth));
                        sw.WriteLine('\t'); // candidate_index empty
                    }
                    else
                    {
                        // Defensive: planner shouldn't return Keep, but emit
                        // a row anyway so a future regression is visible.
                        sw.Write(@"keep");
                        sw.WriteLine("\t\t\t\t\t");
                    }
                }
            }
            LogAction(string.Format(@"Wrote Stage 6 reconciliation dump: {0} ({1} rows)",
                path, rows.Count));
        }

        /// <summary>
        /// Append the loaded calibration arrays for one file to
        /// cs_stage6_calibration.tsv. Mirrors Rust dump_stage6_calibration.
        /// Header is written on the first call (file does not yet exist),
        /// subsequent calls append. Each call writes one row per
        /// (libraryRts[i], fittedValues[i]) pair. Used for cross-impl
        /// JSON-decode bisection — see DumpCalibration docs.
        /// </summary>
        public static void WriteStage6CalibrationDump(
            string fileName, double[] libraryRts, double[] fittedValues)
        {
            const string path = @"cs_stage6_calibration.tsv";
            bool headerNeeded = !File.Exists(path);
            using (var sw = new StreamWriter(path, append: true))
            {
                sw.NewLine = "\n";
                if (headerNeeded)
                    sw.WriteLine(@"file_name	idx	library_rt	fitted_value");
                int n = Math.Min(libraryRts.Length, fittedValues.Length);
                for (int i = 0; i < n; i++)
                {
                    sw.Write(fileName);
                    sw.Write('\t'); sw.Write(i.ToString(CultureInfo.InvariantCulture));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(libraryRts[i]));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(fittedValues[i]));
                }
            }
            LogAction(string.Format(
                @"Appended {0} calibration rows for {1} to {2}",
                Math.Min(libraryRts.Length, fittedValues.Length), fileName, path));
        }

        /// <summary>
        /// Dump the per-detection (apex_rt, library_rt, weight) trace
        /// captured by ConsensusRts.Compute to cs_stage6_inv_predict.tsv.
        /// Mirrors Rust dump_stage6_inv_predict. Columns: file_name,
        /// is_decoy, modified_sequence, apex_rt, library_rt, weight. Rows
        /// sorted by (is_decoy, modified_sequence, file_name) for stable
        /// diff. Used for ULP bisection of consensus_library_rt cross-impl
        /// divergence: if apex_rt diverges the bug is in Parquet f64 decode,
        /// if only library_rt diverges the bug is in LOESS InversePredict.
        /// </summary>
        public static void WriteStage6InvPredictDump(IList<InvPredictRecord> records)
        {
            const string path = @"cs_stage6_inv_predict.tsv";

            var sorted = new List<InvPredictRecord>(records);
            sorted.Sort((a, b) =>
            {
                int cmp = a.IsDecoy.CompareTo(b.IsDecoy);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(a.ModifiedSequence, b.ModifiedSequence);
                if (cmp != 0) return cmp;
                cmp = string.CompareOrdinal(a.FileName, b.FileName);
                if (cmp != 0) return cmp;
                // Same (is_decoy, modseq, file_name) ties happen when a peptide
                // has multiple charge-state detections in one file. Tiebreak on
                // the data fields so the order is deterministic regardless of
                // sort stability (List<T>.Sort is not stable; Rust's
                // sort_by IS stable but the input order also differs cross-impl,
                // so a complete data tiebreak is the only way to get byte-parity).
                cmp = a.ApexRt.CompareTo(b.ApexRt);
                if (cmp != 0) return cmp;
                return a.LibraryRt.CompareTo(b.LibraryRt);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	is_decoy	modified_sequence	apex_rt	library_rt	weight");
                foreach (var r in sorted)
                {
                    sw.Write(r.FileName);
                    sw.Write('\t'); sw.Write(r.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(r.ModifiedSequence ?? string.Empty);
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(r.ApexRt));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(r.LibraryRt));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(r.Weight));
                }
            }
            LogAction(string.Format(
                @"Wrote Stage 6 inverse-predict dump: {0} ({1} rows)",
                path, sorted.Count));
        }

        /// <summary>
        /// Dump the per-peptide first-pass protein FDR state to
        /// cs_stage6_protein_fdr.tsv. Mirrors Rust dump_stage6_protein_fdr.
        /// One row per peptide that appears in best_scores (the union of
        /// target + decoy modified_sequences seen across all per-file
        /// FdrEntry stubs at the moment first-pass protein FDR runs).
        /// Columns: is_decoy, modified_sequence, best_qvalue, score,
        /// protein_qvalue. best_qvalue is the input gate (peptide-level
        /// run q-value, min across files). score is the input ranking
        /// (max SVM discriminant across files). protein_qvalue is the
        /// propagated output -- the value PropagateProteinQvalues will
        /// write to FdrEntry.RunProteinQvalue (1.0 if the peptide is
        /// not in proteinFdr.PeptideQvalues, matching the
        /// PropagateProteinQvalues default). Rows sorted by
        /// (is_decoy, modified_sequence) for stable diff.
        /// </summary>
        public static void WriteStage6ProteinFdrDump(
            IDictionary<string, PeptideScore> bestScores,
            IDictionary<string, double> peptideQvalues)
        {
            const string path = @"cs_stage6_protein_fdr.tsv";

            var rows = new List<KeyValuePair<string, PeptideScore>>(bestScores);
            rows.Sort((a, b) =>
            {
                int cmp = a.Value.IsDecoy.CompareTo(b.Value.IsDecoy);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.Key, b.Key);
            });

            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"is_decoy	modified_sequence	best_qvalue	score	protein_qvalue");
                foreach (var row in rows)
                {
                    var ps = row.Value;
                    double q;
                    if (!peptideQvalues.TryGetValue(row.Key, out q))
                        q = 1.0;
                    sw.Write(ps.IsDecoy ? @"true" : @"false");
                    sw.Write('\t'); sw.Write(row.Key ?? string.Empty);
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(ps.BestQvalue));
                    sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(ps.Score));
                    sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(q));
                }
            }
            LogAction(string.Format(
                @"Wrote Stage 6 first-pass protein FDR dump: {0} ({1} rows)",
                path, rows.Count));
        }

        /// <summary>
        /// Dump the per-point LOESS fit state of every refit RTCalibration
        /// to cs_stage6_loess_fit.tsv. Mirrors Rust dump_stage6_loess_fit.
        /// One row per (file_name, idx) into the refit's library_rts +
        /// fitted_values + abs_residuals arrays. Rows sorted by
        /// (file_name, idx) for stable diff. The refit dump captures
        /// scalar stats (R²/SD/MAD); this dump captures the LOESS curve
        /// itself so a stats-vs-smoother bisection is possible.
        /// </summary>
        public static void WriteStage6LoessFitDump(
            IReadOnlyDictionary<string, RTCalibration> refinedCalibrations)
        {
            const string path = @"cs_stage6_loess_fit.tsv";

            var fileNames = new List<string>(refinedCalibrations.Keys);
            fileNames.Sort(StringComparer.Ordinal);

            int totalRows = 0;
            using (var sw = new StreamWriter(path))
            {
                sw.NewLine = "\n";
                sw.WriteLine(@"file_name	idx	library_rt	fitted_value	abs_residual");
                foreach (var fileName in fileNames)
                {
                    var cal = refinedCalibrations[fileName];
                    var libRts = cal.LibraryRts;
                    var fitted = cal.FittedValues;
                    var residuals = cal.AbsResiduals;
                    int n = Math.Min(libRts.Length, Math.Min(fitted.Length, residuals.Length));
                    for (int i = 0; i < n; i++)
                    {
                        sw.Write(fileName);
                        sw.Write('\t'); sw.Write(i.ToString(CultureInfo.InvariantCulture));
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(libRts[i]));
                        sw.Write('\t'); sw.Write(Diagnostics.FormatF64Roundtrip(fitted[i]));
                        sw.Write('\t'); sw.WriteLine(Diagnostics.FormatF64Roundtrip(residuals[i]));
                    }
                    totalRows += n;
                }
            }
            LogAction(string.Format(
                @"Wrote Stage 6 LOESS fit dump: {0} ({1} rows across {2} files)",
                path, totalRows, fileNames.Count));
        }
    }
}
