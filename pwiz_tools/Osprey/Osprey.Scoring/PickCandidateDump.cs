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

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace pwiz.Osprey.Scoring
{
    /// <summary>
    /// Thread-safe per-file collector for the OSPREY_PICK_DUMP_CANDIDATES dump. The
    /// per-candidate rank loop in <see cref="PeakDataExtractor"/> runs under the per-window
    /// <c>Parallel.For</c>, so rows are accumulated in a <see cref="ConcurrentBag{T}"/> during
    /// scoring and flushed ONCE, per input mzML, by the orchestrator
    /// (<c>PerFileScoringTask.ProcessFile</c>) after the parallel region completes. One
    /// instance lives on the per-file <see cref="ScoringContext"/>, so a live instance is
    /// inherently isolated per input even under <c>--parallel-files</c>.
    ///
    /// The captured terms are the EXACT raw values the picker computes (coelution,
    /// ln_intensity, rt_penalty, median_polish) so a downstream trainer learns weights on
    /// precisely what <c>PickLdaModel</c> consumes at inference time.
    /// </summary>
    public sealed class PickCandidateDump
    {
        // TSV column order. Kept as a single source of truth so the header and the row
        // formatting below cannot drift apart.
        private const string HeaderLine =
            "base_id\tis_decoy\tcand_index\tcoelution\tln_intensity\trt_penalty\t" +
            "median_polish\tapex_rt\tstart_rt\tend_rt\tis_picked";

        private readonly ConcurrentBag<Row> _rows = new ConcurrentBag<Row>();

        /// <summary>One captured CWT candidate peak of one precursor.</summary>
        public sealed class Row
        {
            public uint BaseId;        // Id & 0x7FFFFFFF
            public bool IsDecoy;       // (Id & 0x80000000) != 0
            public int CandIndex;      // the rank-loop index pi
            public double Coelution;   // coelutionScore (mean pairwise Pearson over the peak window)
            public double LnIntensity; // Math.Log(1 + apexIntensity)
            public double RtPenalty;   // exp(-(rtResidual^2)/(2 sigma^2))
            public double MedianPolish;// CandidateLibCosine (median-polish cosine vs library; neutral 1.0 on failure)
            public double ApexRt;
            public double StartRt;
            public double EndRt;
            public bool IsPicked;      // this candidate is the argmax (the peak actually chosen)
        }

        /// <summary>Append a batch of rows for one precursor. Thread-safe.</summary>
        public void AddRows(List<Row> rows)
        {
            if (rows == null)
                return;
            foreach (var r in rows)
                _rows.Add(r);
        }

        /// <summary>
        /// Write all accumulated rows to <paramref name="path"/> as a tab-separated file with a
        /// header row, invariant-culture round-trippable doubles ("R"), in a deterministic order
        /// (base_id, then target-before-decoy, then cand_index) so repeat runs produce identical
        /// files. Overwrites any existing file. A no-op that writes nothing when no rows were
        /// captured, so the later override-only scoring passes never clobber the first-pass dump.
        ///
        /// STREAMS row-by-row through a buffered <see cref="StreamWriter"/> rather than
        /// materializing one giant string: on Stellar the candidate set exceeds the CLR's
        /// contiguous-string limit, so a single <c>StringBuilder.ToString()</c> threw
        /// OutOfMemoryException. Each row formats into a reused per-row buffer, so no single
        /// allocation ever scales with the row count. Clears the bag after the write.
        /// </summary>
        public void Flush(string path)
        {
            if (_rows.IsEmpty)
                return;

            // Stable LINQ ordering (base_id, target-before-decoy, cand_index) for a
            // deterministic file across repeat runs. LINQ OrderBy/ThenBy rather than
            // List.Sort: the latter is introsort (unstable) and tripwired by the
            // cross-impl parity guard, even though this triple is already a unique key.
            var rows = _rows
                .OrderBy(r => r.BaseId)
                .ThenBy(r => r.IsDecoy) // false (target) before true (decoy)
                .ThenBy(r => r.CandIndex)
                .ToList();

            var inv = CultureInfo.InvariantCulture;
            // One small StringBuilder reused per row (cleared each iteration), so the largest
            // live allocation is a single line -- never the whole file.
            var line = new StringBuilder(96);
            using (var writer = new StreamWriter(path, false) { NewLine = "\n" })
            {
                writer.WriteLine(HeaderLine);
                foreach (var r in rows)
                {
                    line.Clear();
                    line.Append(r.BaseId.ToString(inv)).Append('\t')
                        .Append(r.IsDecoy ? '1' : '0').Append('\t')
                        .Append(r.CandIndex.ToString(inv)).Append('\t')
                        .Append(r.Coelution.ToString("R", inv)).Append('\t')
                        .Append(r.LnIntensity.ToString("R", inv)).Append('\t')
                        .Append(r.RtPenalty.ToString("R", inv)).Append('\t')
                        .Append(r.MedianPolish.ToString("R", inv)).Append('\t')
                        .Append(r.ApexRt.ToString("R", inv)).Append('\t')
                        .Append(r.StartRt.ToString("R", inv)).Append('\t')
                        .Append(r.EndRt.ToString("R", inv)).Append('\t')
                        .Append(r.IsPicked ? '1' : '0');
                    writer.WriteLine(line.ToString());
                }
            }

            // Drain the bag so a subsequent (override rescore) pass on the same context starts
            // empty. ConcurrentBag.Clear() is net8.0-only; TryTake works on net472 too.
            while (_rows.TryTake(out _))
            {
            }
        }
    }
}
