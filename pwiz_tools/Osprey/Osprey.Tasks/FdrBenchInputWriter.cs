/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/maccoss/osprey)
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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// Writes an FDRBench (https://github.com/Noble-Lab/FDRBench) peptide / precursor
    /// level input TSV. Emits every non-decoy target Osprey scored (regardless of
    /// q-value) with the raw SVM discriminant (<see cref="FdrEntry.Score"/>) as the
    /// <c>score</c> column, so FDRBench can compute true-FDR via entrapment counting
    /// across the full ranking without truncation at Osprey's q-value threshold.
    ///
    /// Entrapment sequences (marked by <c>_p_target</c> in protein accessions) are
    /// targets in Osprey's world and pass through unchanged; decoys are excluded.
    ///
    /// Port of the Rust osprey-io <c>output/fdrbench.rs</c>
    /// (<c>write_fdrbench_peptide_input</c>). Invoke FDRBench with
    /// <c>-score 'score:1'</c> (higher is better).
    /// </summary>
    public static class FdrBenchInputWriter
    {
        /// <summary>
        /// Mask for extracting the target-side base id from <see cref="FdrEntry.EntryId"/>.
        /// The high bit is set for decoys; clearing it yields the library entry id shared
        /// by a target and its paired decoy.
        /// </summary>
        private const uint BASE_ID_MASK = 0x7FFFFFFF;

        /// <summary>
        /// Maximum width of the <c>protein</c> column, in characters. FDRBench's bundled
        /// Univocity CSV parser caps each column at 4096 characters and aborts the run
        /// when that limit is exceeded; shared peptides (e.g. ZNF / olfactory receptor
        /// families) can map to hundreds of <c>;</c>-joined IDs, which trips the cap. We
        /// keep a margin under 4096 so the surrounding tabs and a truncation marker fit.
        /// </summary>
        private const int MAX_PROTEIN_FIELD_CHARS = 4000;

        /// <summary>Counts returned by <see cref="WritePeptideInput"/> for caller-side logging.</summary>
        public struct Result
        {
            public int Rows;
            public int MissingLibrary;
            public int TruncatedProtein;
        }

        /// <summary>
        /// Write the FDRBench peptide / precursor-level input TSV to <paramref name="path"/>.
        ///
        /// With <paramref name="perRun"/> = false, rows are deduplicated to one per precursor
        /// (level Precursor / Both) or one per peptide (level Peptide), keeping the minimum
        /// experiment-level q-value (ties broken by highest discriminant). With
        /// <paramref name="perRun"/> = true, every (precursor, file) observation is emitted
        /// with the per-run q-value and a <c>run</c> column.
        /// </summary>
        /// <param name="path">Destination TSV path; parent directories are created.</param>
        /// <param name="perFileEntries">Per-file FDR entries (key = source file / run name).</param>
        /// <param name="libraryById">Library entries indexed by id, for protein / sequence lookup.</param>
        /// <param name="fdrLevel">Drives which q-value field is emitted and the dedup key.
        /// <see cref="FdrLevel.Both"/> collapses to precursor-level for output.</param>
        /// <param name="perRun">If true, emit one row per (precursor, file); else dedup across runs.</param>
        public static Result WritePeptideInput(
            string path,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            FdrLevel fdrLevel,
            bool perRun)
        {
            // Protein and Both collapse to precursor-level for this peptide-level writer.
            FdrLevel effectiveLevel = fdrLevel == FdrLevel.Peptide ? FdrLevel.Peptide : FdrLevel.Precursor;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var result = new Result();
            using (var writer = new StreamWriter(path, false))
            {
                writer.NewLine = "\n"; // emit '\n' line endings for the TSV body
                writer.WriteLine(perRun
                    ? "peptide\tmod_peptide\tcharge\tq_value\tscore\tprotein\trun"
                    : "peptide\tmod_peptide\tcharge\tq_value\tscore\tprotein");

                if (perRun)
                {
                    foreach (var fileEntries in perFileEntries)
                    {
                        string runName = fileEntries.Key;
                        foreach (var entry in fileEntries.Value)
                        {
                            if (entry.IsDecoy)
                                continue;
                            var lookup = ResolveLibrary(libraryById, entry.EntryId, ref result);
                            double q = entry.EffectiveRunQvalue(effectiveLevel);
                            writer.WriteLine(FormatRow(lookup.Peptide, entry.ModifiedSequence,
                                entry.Charge, q, entry.Score, lookup.Protein, runName));
                            result.Rows++;
                        }
                    }
                }
                else
                {
                    // Dedup across files: keep best (min q-value, ties by max score) per dedup key.
                    var best = new Dictionary<string, BestRow>();
                    foreach (var fileEntries in perFileEntries)
                    {
                        foreach (var entry in fileEntries.Value)
                        {
                            if (entry.IsDecoy)
                                continue;
                            double q = entry.EffectiveExperimentQvalue(effectiveLevel);
                            string key = DedupKey(entry, effectiveLevel);
                            BestRow cur;
                            if (!best.TryGetValue(key, out cur)
                                || q < cur.QValue
                                || (q == cur.QValue && entry.Score > cur.Score))
                            {
                                best[key] = new BestRow { QValue = q, Score = entry.Score, Entry = entry };
                            }
                        }
                    }

                    foreach (var row in best.Values)
                    {
                        var lookup = ResolveLibrary(libraryById, row.Entry.EntryId, ref result);
                        writer.WriteLine(FormatRow(lookup.Peptide, row.Entry.ModifiedSequence,
                            row.Entry.Charge, row.QValue, row.Entry.Score, lookup.Protein, null));
                        result.Rows++;
                    }
                }
            }
            return result;
        }

        /// <summary>Format one TSV row; <paramref name="runName"/> null omits the trailing run column.</summary>
        private static string FormatRow(string peptide, string modSeq, byte charge,
            double qValue, double score, string protein, string runName)
        {
            string row = string.Join("\t", new[]
            {
                peptide,
                modSeq,
                charge.ToString(CultureInfo.InvariantCulture),
                qValue.ToString(@"E10", CultureInfo.InvariantCulture),
                score.ToString(@"E10", CultureInfo.InvariantCulture),
                protein
            });
            return runName == null ? row : row + "\t" + runName;
        }

        /// <summary>Resolve an entry id to its (sequence, protein-field), updating warning counts.</summary>
        private static LibLookup ResolveLibrary(IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            uint entryId, ref Result result)
        {
            uint baseId = entryId & BASE_ID_MASK;
            LibraryEntry lib;
            if (!libraryById.TryGetValue(baseId, out lib))
            {
                result.MissingLibrary++;
                return new LibLookup { Peptide = string.Empty, Protein = string.Empty };
            }
            bool truncated;
            string protein = FormatProteinField(lib.ProteinIds, out truncated);
            if (truncated)
                result.TruncatedProtein++;
            return new LibLookup { Peptide = lib.Sequence ?? string.Empty, Protein = protein };
        }

        /// <summary>
        /// Format a protein-ID list for the TSV, truncating when the joined string would exceed
        /// <see cref="MAX_PROTEIN_FIELD_CHARS"/>. On truncation, returns
        /// <c>&lt;kept&gt;;...+N_more</c> where N is the number of IDs dropped.
        /// </summary>
        private static string FormatProteinField(IList<string> ids, out bool truncated)
        {
            truncated = false;
            if (ids == null || ids.Count == 0)
                return string.Empty;
            string joined = string.Join(@";", ids);
            if (joined.Length <= MAX_PROTEIN_FIELD_CHARS)
                return joined;

            truncated = true;
            int markerReserve = (@";...+" + ids.Count.ToString(CultureInfo.InvariantCulture) + @"_more").Length;
            int budget = System.Math.Max(0, MAX_PROTEIN_FIELD_CHARS - markerReserve);

            int kept = 0;
            int len = 0;
            for (int i = 0; i < ids.Count; i++)
            {
                int sepLen = i == 0 ? 0 : 1;
                if (len + sepLen + ids[i].Length > budget)
                    break;
                len += sepLen + ids[i].Length;
                kept = i + 1;
            }

            if (kept == 0)
            {
                // First ID alone exceeds the budget: hard-truncate it and append the marker.
                string head = ids[0].Length > budget ? ids[0].Substring(0, budget) : ids[0];
                return head + @"...+" + ids.Count.ToString(CultureInfo.InvariantCulture) + @"_more";
            }
            int dropped = ids.Count - kept;
            return string.Join(@";", ids.Take(kept))
                + @";...+" + dropped.ToString(CultureInfo.InvariantCulture) + @"_more";
        }

        /// <summary>Dedup key: modified sequence for peptide level, (modseq, charge) otherwise.</summary>
        private static string DedupKey(FdrEntry entry, FdrLevel level)
        {
            return level == FdrLevel.Peptide
                ? entry.ModifiedSequence
                : entry.ModifiedSequence + @"@" + entry.Charge.ToString(CultureInfo.InvariantCulture);
        }

        private struct LibLookup
        {
            public string Peptide;
            public string Protein;
        }

        private class BestRow
        {
            public double QValue;
            public double Score;
            public FdrEntry Entry;
        }
    }
}
