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
    /// level input TSV. Emits every non-decoy reported (compaction-surviving) target,
    /// i.e. the peptides actually written to the output, regardless of q-value, with the
    /// raw SVM discriminant (<see cref="FdrEntry.Score"/>) as the <c>score</c> column, so
    /// FDRBench can compute true-FDR via entrapment counting across the reported ranking
    /// without truncation at Osprey's q-value threshold.
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
        /// Resolve the FDRBench output path for a given pass, honoring
        /// <see cref="OspreyConfig.FdrBenchPass"/> as a bitmask of
        /// <see cref="OspreyConfig.FDRBENCH_PASS_1"/> / <see cref="OspreyConfig.FDRBENCH_PASS_2"/>.
        /// Returns <c>null</c> when no <c>--fdrbench</c> path is set or the pass was not requested,
        /// so a caller can guard with a single null check. When only one pass is requested the
        /// exact <c>--fdrbench</c> path is used (backward compatible); when both are requested each
        /// pass gets a <c>.pass1</c> / <c>.pass2</c> stem suffix so the two do not overwrite.
        /// </summary>
        /// <param name="config">The run config (supplies the path and the pass bitmask).</param>
        /// <param name="pass"><see cref="OspreyConfig.FDRBENCH_PASS_1"/> or
        /// <see cref="OspreyConfig.FDRBENCH_PASS_2"/>.</param>
        public static string PathForPass(OspreyConfig config, int pass)
        {
            if (string.IsNullOrEmpty(config.OutputFdrBench) || (config.FdrBenchPass & pass) == 0)
                return null;

            // Single pass -> exact path. Both requested -> suffix each stem so they coexist.
            if (config.FdrBenchPass != (OspreyConfig.FDRBENCH_PASS_1 | OspreyConfig.FDRBENCH_PASS_2))
                return config.OutputFdrBench;

            string dir = Path.GetDirectoryName(config.OutputFdrBench);
            string stem = Path.GetFileNameWithoutExtension(config.OutputFdrBench);
            string ext = Path.GetExtension(config.OutputFdrBench);
            string name = stem + @".pass" + pass.ToString(CultureInfo.InvariantCulture) + ext;
            return string.IsNullOrEmpty(dir) ? name : Path.Combine(dir, name);
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
        /// <param name="skipEntrapmentSeqs">Entrapment sequences to exclude (unmatched orphans); null to write all.</param>
        public static Result WritePeptideInput(
            string path,
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            FdrLevel fdrLevel,
            bool perRun,
            ICollection<string> skipEntrapmentSeqs = null)
        {
            // Protein and Both collapse to precursor-level for this peptide-level writer.
            FdrLevel effectiveLevel = fdrLevel == FdrLevel.Peptide ? FdrLevel.Peptide : FdrLevel.Precursor;

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var result = new Result();
            using (var saver = new FileSaver(path))
            {
                using (var writer = new StreamWriter(saver.SafeName, false))
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
                                if (skipEntrapmentSeqs != null && skipEntrapmentSeqs.Contains(lookup.Peptide))
                                    continue; // excluded orphan entrapment (kept consistent with the manifest)
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

                        // Dictionary enumeration order is not guaranteed stable across runs / runtimes,
                        // so sort by the dedup-key components (modified sequence, then charge) before
                        // writing. The key is unique per surviving row, so this is a total order with no
                        // ties -- deterministic, diff-friendly output. Ordinal compare avoids any
                        // culture-dependent sequence ordering.
                        foreach (var row in best.Values
                            .OrderBy(r => r.Entry.ModifiedSequence, System.StringComparer.Ordinal)
                            .ThenBy(r => r.Entry.Charge))
                        {
                            var lookup = ResolveLibrary(libraryById, row.Entry.EntryId, ref result);
                            if (skipEntrapmentSeqs != null && skipEntrapmentSeqs.Contains(lookup.Peptide))
                                continue; // excluded orphan entrapment (kept consistent with the manifest)
                            writer.WriteLine(FormatRow(lookup.Peptide, row.Entry.ModifiedSequence,
                                row.Entry.Charge, row.QValue, row.Entry.Score, lookup.Protein, null));
                            result.Rows++;
                        }
                    }
                }
                saver.Commit();
            }
            return result;
        }

        /// <summary>
        /// Write a corrected FDRBench pairing manifest derived from the searched
        /// library -- the single source of truth for the run. Emits one row per
        /// distinct non-decoy peptide (target / p_target) classified from its own
        /// protein accessions, so FDRBench can classify every reported peptide and
        /// drops nothing (no <c>remove_invalid_peptides</c>).
        ///
        /// Pairing comes from <paramref name="pairing"/>: the external manifest where
        /// it covers a sequence, reconstructed from the library accessions for the
        /// extras. Entrapment peptides with no target twin
        /// (<see cref="EntrapmentPairing.ExcludedEntrapment"/> -- N-terminal-Met-clip
        /// artifacts) are absent from the pairing and are NOT emitted, so the manifest
        /// has no unpaired entrapment and stock FDRBench's paired estimator does not
        /// crash. FDRBench reads only non-decoy rows and the input TSV excludes
        /// decoys, so decoy rows are unnecessary.
        /// </summary>
        /// <param name="path">Destination manifest TSV path.</param>
        /// <param name="libraryById">The searched library, indexed by id.</param>
        /// <param name="pairing">The reconciled pairing (see <see cref="EntrapmentPairing.Build"/>).</param>
        /// <returns>Number of peptide rows written.</returns>
        public static int WritePairingManifest(
            string path,
            IReadOnlyDictionary<uint, LibraryEntry> libraryById,
            EntrapmentPairing pairing)
        {
            // Distinct non-decoy peptides that have a pair index, in sequence order.
            // A peptide absent from PairIndexBySeq is an excluded orphan entrapment.
            var rows = new SortedDictionary<string, LibraryEntry>(System.StringComparer.Ordinal);
            foreach (var lib in libraryById.Values)
            {
                if (lib == null || lib.Sequence == null)
                    continue;
                if (EntrapmentLibraryClassifier.IsDecoySide(lib.ProteinIds))
                    continue;
                if (!pairing.PairIndexBySeq.ContainsKey(lib.Sequence))
                    continue;
                if (!rows.ContainsKey(lib.Sequence))
                    rows[lib.Sequence] = lib;
            }

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            int written = 0;
            using (var saver = new FileSaver(path))
            {
                using (var writer = new StreamWriter(saver.SafeName, false))
                {
                    writer.NewLine = "\n";
                    writer.WriteLine("sequence\tdecoy\tproteins\tpeptide_type\tpeptide_pair_index");
                    foreach (var kv in rows)
                    {
                        var lib = kv.Value;
                        bool entrap = EntrapmentLibraryClassifier.IsEntrapment(lib.ProteinIds);
                        uint pair = pairing.PairIndexBySeq[lib.Sequence];
                        string protein = FormatProteinField(lib.ProteinIds, out _);
                        writer.WriteLine(string.Join("\t", new[]
                        {
                            lib.Sequence,
                            "No",
                            protein,
                            entrap ? "p_target" : "target",
                            pair.ToString(CultureInfo.InvariantCulture)
                        }));
                        written++;
                    }
                }
                saver.Commit();
            }
            return written;
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
        private static string FormatProteinField(IReadOnlyList<string> ids, out bool truncated)
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
