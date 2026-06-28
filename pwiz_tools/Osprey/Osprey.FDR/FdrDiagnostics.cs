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
            keys.Sort(StringComparer.Ordinal);
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
