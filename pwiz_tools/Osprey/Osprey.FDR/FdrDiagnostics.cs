/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.7) <noreply .at. anthropic.com>
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

// Cross-impl bisection dumps for FDR-level diagnostics. Functions in this
// class are env-var-gated by static bools (evaluated once at class load)
// and no-op in production runs. They write small TSVs to the current
// working directory so they can be diffed against the Rust osprey-fdr
// crate's matching dumps in osprey-fdr/src/diagnostics.rs.
//
// This is a per-project diagnostics class for Osprey.FDR; the
// top-level project has its own OspreyDiagnostics (which cannot be
// referenced from here due to layering). Naming kept distinct
// (FdrDiagnostics vs Diagnostics in Osprey.Core) to avoid
// collision when both namespaces are imported.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    public static class FdrDiagnostics
    {
        private static bool IsOne(string name)
        {
            return Environment.GetEnvironmentVariable(name) == @"1";
        }

        /// <summary>
        /// OSPREY_DUMP_STAGE7_WINNERS: dump the full cumulative-FDR winners
        /// list (target + decoy together) to cs_stage7_winners.tsv after the
        /// sort in ComputeProteinFdr. Columns: rank, score, is_decoy,
        /// raw_qvalue, monotonic_qvalue. The existing
        /// WriteStage7ProteinFdrDump (in OspreyDiagnostics) emits only target
        /// winners' scores; decoy-winner scores are not exposed there,
        /// hiding cross-impl divergences driven by decoy-winner scores or
        /// sort-position interleaving in the cumulative sweep.
        /// </summary>
        public static readonly bool DumpStage7Winners = IsOne(@"OSPREY_DUMP_STAGE7_WINNERS");

        /// <summary>
        /// OSPREY_DUMP_BEST_PEPTIDE_SCORES: dump the per-modseq aggregated
        /// best-score map from CollectBestPeptideScores to
        /// cs_best_peptide_scores.tsv. Surfaces the protein-FDR input set so
        /// upstream aggregation divergences (e.g. different per-peptide max
        /// scores from compaction asymmetry) can be diffed directly.
        /// </summary>
        public static readonly bool DumpBestPeptideScores = IsOne(@"OSPREY_DUMP_BEST_PEPTIDE_SCORES");

        /// <summary>
        /// OSPREY_DUMP_COASSIGN_ROWS: directory to write the peak co-assignment
        /// panel's OWN INPUT to, one row per pool observation, plus the sealed
        /// acceptance boundaries. Unset (the default) writes nothing.
        ///
        /// <para>Directory-valued rather than the usual <c>=1</c> because the
        /// question this answers is always an A/B between two binaries, and two
        /// runs writing <c>cs_*.tsv</c> into a shared working directory overwrite
        /// each other. Naming the destination per side removes the copy-out race
        /// that costs more time than the runs.</para>
        ///
        /// <para>The panel reduces the pool to counts, so a moved count says only
        /// THAT something moved. These are the fields <c>BuildCoAssignment</c>
        /// actually reads - score, persisted experiment aggregate, both effective
        /// q-values, apex RT, identity - beside the verdict the builder reached
        /// for the row. Diffing two sides row-wise separates the three ways the
        /// panel can move: a changed per-entry VALUE, a changed MEMBERSHIP verdict
        /// at an unchanged value, or an unchanged pool re-cut by a moved
        /// BOUNDARY.</para>
        /// </summary>
        public static readonly string DumpCoAssignRowsDir =
            Environment.GetEnvironmentVariable(@"OSPREY_DUMP_COASSIGN_ROWS");

        /// <summary>
        /// Streaming sink for <see cref="DumpCoAssignRowsDir"/>: writes
        /// <c>cs_coassign_pass&lt;N&gt;_rows.tsv</c> a row at a time rather than
        /// accumulating, since the pass-2 pool is the survivor set of every file
        /// and the panel's whole scaling argument is that nothing holds it twice.
        /// Returns null when the gate is unset, so callers can null-test instead
        /// of paying for the row projection.
        /// </summary>
        public static CoAssignRowDump CreateCoAssignRowDump(int pass)
        {
            return string.IsNullOrWhiteSpace(DumpCoAssignRowsDir)
                ? null
                : new CoAssignRowDump(DumpCoAssignRowsDir, pass);
        }

        /// <summary>
        /// The <see cref="CreateCoAssignRowDump"/> sink. Disposing closes both files;
        /// a run that throws mid-panel still leaves the rows written so far, which is
        /// the point of streaming them.
        /// </summary>
        public sealed class CoAssignRowDump : IDisposable
        {
            private readonly string _dir;
            private readonly int _pass;
            private readonly int _seq;
            private readonly StreamWriter _rows;

            /// <summary>
            /// The first unused sequence number in <paramref name="dir"/>. One run builds this
            /// panel more than once - the straight-through Stage 7 and then any
            /// <c>--task ModelDiagnostics</c> regeneration over the same directory - and those
            /// are DIFFERENT pools whenever the regeneration path is what is under suspicion.
            /// Overwriting silently hands back the last one under the first one's name, which is
            /// the failure this whole dump exists to avoid making by hand.
            /// </summary>
            private static int NextSequence(string dir, int pass)
            {
                int seq = 0;
                while (File.Exists(NameFor(dir, pass, seq, @"rows")))
                    seq++;
                return seq;
            }

            private static string NameFor(string dir, int pass, int seq, string kind)
            {
                var inv = CultureInfo.InvariantCulture;
                return Path.Combine(dir, seq == 0
                    ? string.Format(inv, @"cs_coassign_pass{0}_{1}.tsv", pass, kind)
                    : string.Format(inv, @"cs_coassign_pass{0}_{1}.{2}.tsv", pass, kind, seq));
            }

            public CoAssignRowDump(string dir, int pass)
            {
                _dir = dir;
                _pass = pass;
                Directory.CreateDirectory(dir);
                _seq = NextSequence(dir, pass);
                _rows = new StreamWriter(NameFor(dir, pass, _seq, @"rows"));
                _rows.WriteLine(
                    "file_idx\tfile\tentry_id\tbase_id\tis_decoy\tclass\tscore\texp_agg_score\t" +
                    "run_q\texp_q\tapex_rt\tcharge\tincluded\tmodified_sequence");
            }

            /// <summary>
            /// One pool observation as the panel saw it. <paramref name="included"/> is the
            /// builder's own gate verdict, recorded rather than recomputed so a diff cannot
            /// disagree with the panel about what it counted.
            /// </summary>
            public void WriteRow(int fileIdx, string fileName, uint entryId, uint baseId, bool isDecoy,
                string entrapmentClass, double score, double experimentAggregateScore,
                double runQvalue, double experimentQvalue, double apexRt, byte charge,
                bool included, string modifiedSequence)
            {
                var inv = CultureInfo.InvariantCulture;
                _rows.WriteLine(string.Format(inv,
                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}\t{13}",
                    fileIdx, fileName, entryId, baseId,
                    isDecoy ? "true" : "false", entrapmentClass,
                    Diagnostics.FormatF64Roundtrip(score),
                    Diagnostics.FormatF64Roundtrip(experimentAggregateScore),
                    Diagnostics.FormatF64Roundtrip(runQvalue),
                    Diagnostics.FormatF64Roundtrip(experimentQvalue),
                    Diagnostics.FormatF64Roundtrip(apexRt),
                    charge, included ? "true" : "false", modifiedSequence));
            }

            /// <summary>
            /// The sealed boundaries, written once. Every per-row verdict above is a
            /// comparison against one of these, so a diff that finds no moved row but a
            /// moved boundary has still explained the panel.
            /// </summary>
            public void WriteCutoffs(Func<int, double> runCutoffByFile, string[] runNames,
                double experimentCutoff, double experimentCutoffInStratum,
                double experimentCutoffOffStratum, int acceptedInStratum, int acceptedOffStratum)
            {
                var inv = CultureInfo.InvariantCulture;
                using (var sw = new StreamWriter(NameFor(_dir, _pass, _seq, @"cutoffs")))
                {
                    sw.WriteLine("scope\tfile_idx\tfile\tvalue");
                    for (int f = 0; f < runNames.Length; f++)
                    {
                        sw.WriteLine(string.Format(inv, "run\t{0}\t{1}\t{2}",
                            f, runNames[f], Diagnostics.FormatF64Roundtrip(runCutoffByFile(f))));
                    }
                    sw.WriteLine(string.Format(inv, "experiment\t-1\t-\t{0}",
                        Diagnostics.FormatF64Roundtrip(experimentCutoff)));
                    sw.WriteLine(string.Format(inv, "experimentInStratum\t-1\t-\t{0}",
                        Diagnostics.FormatF64Roundtrip(experimentCutoffInStratum)));
                    sw.WriteLine(string.Format(inv, "experimentOffStratum\t-1\t-\t{0}",
                        Diagnostics.FormatF64Roundtrip(experimentCutoffOffStratum)));
                    sw.WriteLine(string.Format(inv, "acceptedInStratum\t-1\t-\t{0}", acceptedInStratum));
                    sw.WriteLine(string.Format(inv, "acceptedOffStratum\t-1\t-\t{0}", acceptedOffStratum));
                }
            }

            public void Dispose()
            {
                _rows.Dispose();
            }
        }

        /// <summary>
        /// Write cs_stage7_winners.tsv. Caller passes a (score, is_decoy)
        /// tuple list in sort order plus the parallel q-value arrays. Check
        /// <see cref="DumpStage7Winners"/> first to skip the LINQ projection
        /// on the disabled-dump path.
        /// </summary>
        public static void WriteStage7WinnersDump(
            IList<(double Score, bool IsDecoy)> winners,
            double[] rawQvalues,
            double[] monotonicQvalues)
        {
            const string path = @"cs_stage7_winners.tsv";
            var inv = CultureInfo.InvariantCulture;
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("rank\tscore\tis_decoy\traw_qvalue\tmonotonic_qvalue");
                for (int i = 0; i < winners.Count; i++)
                {
                    sw.WriteLine(string.Format(inv, "{0}\t{1}\t{2}\t{3}\t{4}",
                        i,
                        Diagnostics.FormatF64Roundtrip(winners[i].Score),
                        winners[i].IsDecoy ? "true" : "false",
                        Diagnostics.FormatF64Roundtrip(rawQvalues[i]),
                        Diagnostics.FormatF64Roundtrip(monotonicQvalues[i])));
                }
            }
        }

        /// <summary>
        /// Write cs_best_peptide_scores.tsv. Rows sorted by
        /// modified_sequence for stable cross-impl diff.
        /// </summary>
        public static void WriteBestPeptideScoresDump(Dictionary<string, PeptideScore> best)
        {
            const string path = @"cs_best_peptide_scores.tsv";
            var inv = CultureInfo.InvariantCulture;
            var keys = new List<string>(best.Keys);
            keys.Sort(StringComparer.Ordinal); // Array.Sort OK: diagnostic dump only (not parity-sensitive); keys are unique dictionary keys so the comparator never ties anyway
            using (var sw = new StreamWriter(path))
            {
                sw.WriteLine("modified_sequence\tscore\tis_decoy\tbest_qvalue");
                foreach (var seq in keys)
                {
                    var ps = best[seq];
                    sw.WriteLine(string.Format(inv, "{0}\t{1}\t{2}\t{3}",
                        seq,
                        Diagnostics.FormatF64Roundtrip(ps.Score),
                        ps.IsDecoy ? "true" : "false",
                        Diagnostics.FormatF64Roundtrip(ps.BestQvalue)));
                }
            }
        }
    }
}
