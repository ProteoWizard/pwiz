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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;

namespace pwiz.Osprey.Tasks
{
    /// <summary>
    /// The user-facing end-of-run reports, modeled on DIA-NN's output so the two tools
    /// are directly comparable:
    /// <list type="bullet">
    /// <item><c>&lt;output&gt;.protein_groups.tsv</c> -- DIA-NN's <c>pg_matrix</c> shape
    /// (Protein.Group / Protein.Names / N.Peptides / N.Proteotypic / PG.Q.Value), plus the
    /// actual peptide sequences DIA-NN only gives as counts: the peptides used for the
    /// grouping, and the subset of those that are proteotypic in the LIBRARY.</item>
    /// <item><c>&lt;output&gt;.stats.tsv</c> -- DIA-NN's per-run <c>stats.tsv</c> shape
    /// (one row per replicate: precursors / peptides / proteins), plus a final
    /// experiment-level row.</item>
    /// </list>
    /// These are distinct from the <c>cs_stage7_protein_fdr.tsv</c> cross-impl DIAGNOSTIC
    /// dump (counts only, env-gated); these are default, user-facing, and additive (new
    /// files), so the byte-parity gate -- which compares the blib + the Stage-7 dump -- is
    /// unaffected.
    /// </summary>
    public static class OspreyReportWriter
    {
        /// <summary>
        /// Write both default reports next to <paramref name="config"/>'s output blib.
        /// A no-op (with a warning) when no output path is known. Called once, from the
        /// Stage-7 protein-FDR step, with the experiment-level parsimony + FDR result and
        /// the full per-file entry pool the per-replicate rows re-derive from.
        /// </summary>
        public static void WriteReports(
            SecondPassProteinFdrResult experimentResult,
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            Action<string> logInfo)
        {
            string stem = ReportStem(config);
            if (stem == null)
            {
                logInfo?.Invoke(
                    "Skipping reports: no output path (-o) to derive report file names from.");
                return;
            }

            if (config.WriteProteinReport)
            {
                string path = stem + @".protein_groups.tsv";
                WriteProteinGroups(path, experimentResult, fullLibrary, config);
                logInfo?.Invoke(string.Format(
                    "[COUNT] Wrote protein-group report: {0}", path));
            }

            if (config.WriteSummaryReport)
            {
                string path = stem + @".stats.tsv";
                WriteSummary(path, experimentResult, perFileEntries, fullLibrary, config, logInfo);
                logInfo?.Invoke(string.Format(
                    "[COUNT] Wrote summary report: {0}", path));
            }
        }

        // Report file stem = the output blib without its extension, else the report path,
        // else null (caller skips). Path.ChangeExtension(x, null) drops the extension.
        private static string ReportStem(OspreyConfig config)
        {
            if (!string.IsNullOrEmpty(config.OutputBlib))
                return Path.ChangeExtension(config.OutputBlib, null);
            if (!string.IsNullOrEmpty(config.OutputReport))
                return Path.ChangeExtension(config.OutputReport, null);
            return null;
        }

        // ------------------------------------------------------------------ protein groups

        internal static void WriteProteinGroups(
            string path,
            SecondPassProteinFdrResult result,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            var groups = result.Parsimony.Groups;
            var groupQ = result.ProteinFdr.GroupQvalues;

            // Library-level protein map for the "proteotypic given the library" column:
            // for each DETECTED peptide, ALL protein accessions it maps to in the full
            // library -- including proteins that were never detected. A grouping peptide is
            // library-unique to its group iff every library protein it maps to is inside
            // that group. This is the DIA-NN "Proteotypic" definition (unique in the
            // library, not merely unique among the detected peptides), and it is why we
            // scan the whole library rather than reuse the detected-only parsimony.
            var libPeptideToProteins = BuildLibraryPeptideProteins(fullLibrary, result.DetectedPeptides);

            var rows = new List<string[]>(groups.Count);
            foreach (var g in groups)
            {
                double q = groupQ.TryGetValue(g.Id, out double qv) ? qv : 1.0;
                bool passes = q <= config.EffectiveProteinFdr;

                // Peptides used for the grouping = every detected peptide supporting the
                // group (unique-among-detected + shared-among-detected), sorted for a
                // stable file.
                var grouping = g.UniquePeptides
                    .Concat(g.SharedPeptides)
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToList();

                var accessionSet = new HashSet<string>(g.Accessions, StringComparer.Ordinal);
                var libUnique = new List<string>();
                foreach (string pep in grouping)
                {
                    // Library-unique iff all of the peptide's library proteins are in this
                    // group. A peptide absent from the map (no library entry captured)
                    // cannot be asserted unique, so it is treated as not-unique.
                    if (libPeptideToProteins.TryGetValue(pep, out var libProts) &&
                        libProts.Count > 0 &&
                        libProts.All(accessionSet.Contains))
                    {
                        libUnique.Add(pep);
                    }
                }

                rows.Add(new[]
                {
                    string.Join(@";", g.Accessions),
                    string.Join(@";", g.Accessions.Select(ProteinName)),
                    grouping.Count.ToString(CultureInfo.InvariantCulture),
                    libUnique.Count.ToString(CultureInfo.InvariantCulture),
                    q.ToString(@"0.######e+00", CultureInfo.InvariantCulture),
                    passes ? @"1" : @"0",
                    string.Join(@";", grouping),
                    string.Join(@";", libUnique),
                });
            }

            // Deterministic order: passing first, then most confident, then by accessions
            // (the group identity -- distinct, so the ordering is total). Stable OrderBy
            // chain; column indices are [0]=accessions, [4]=q string, [5]=passes flag.
            var ordered = rows
                .OrderBy(r => r[5] == @"1" ? 0 : 1)
                .ThenBy(r => double.Parse(r[4], CultureInfo.InvariantCulture))
                .ThenBy(r => r[0], StringComparer.Ordinal);

            using (var w = new StreamWriter(path, false))
            {
                w.NewLine = "\n";
                w.WriteLine(string.Join("\t", new[]
                {
                    "Protein.Group", "Protein.Names", "N.Peptides", "N.Proteotypic",
                    "PG.Q.Value", "Passes.PG.FDR", "Grouping.Peptides", "Library.Unique.Peptides"
                }));
                foreach (var r in ordered)
                    w.WriteLine(string.Join("\t", r));
            }
        }

        // Scan the library ONCE, capturing every protein accession each DETECTED peptide
        // maps to (targets only). Keyed by modified_sequence to match the parsimony /
        // detected-peptide sets. Only detected peptides are retained, so the map is
        // O(detected), not O(library).
        private static Dictionary<string, HashSet<string>> BuildLibraryPeptideProteins(
            IList<LibraryEntry> fullLibrary, HashSet<string> detectedPeptides)
        {
            var map = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var entry in fullLibrary)
            {
                if (entry.IsDecoy || !detectedPeptides.Contains(entry.ModifiedSequence))
                    continue;
                if (!map.TryGetValue(entry.ModifiedSequence, out var set))
                {
                    set = new HashSet<string>(StringComparer.Ordinal);
                    map[entry.ModifiedSequence] = set;
                }
                if (entry.ProteinIds != null)
                    foreach (string p in entry.ProteinIds)
                        set.Add(p);
            }
            return map;
        }

        // Display name = the LAST '|'-delimited field, which is the entry name across the
        // formats real libraries carry: sp|P37108|SRP14_HUMAN -> SRP14_HUMAN, tr|... the
        // same, and the combined contaminant form gi|136429|sp|P00761|TRYP_PIG ->
        // TRYP_PIG (index-2 would wrongly yield "sp"). A bare accession -> itself.
        private static string ProteinName(string accession)
        {
            if (string.IsNullOrEmpty(accession))
                return accession;
            int bar = accession.LastIndexOf('|');
            return bar >= 0 && bar < accession.Length - 1 ? accession.Substring(bar + 1) : accession;
        }

        // ------------------------------------------------------------------ summary stats

        private static void WriteSummary(
            string path,
            SecondPassProteinFdrResult experimentResult,
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config,
            Action<string> logInfo)
        {
            var level = config.FdrLevel;
            var rows = new List<string[]>();

            // Per replicate: precursors + peptides passing RUN-level FDR in that file, and
            // an INDEPENDENT run-level protein FDR (its own parsimony + picked-protein FDR
            // on that replicate's detected peptides -- not a slice of the experiment set).
            foreach (var kvp in perFileEntries)
            {
                CountPrecursorsPeptides(new[] { kvp }, level, config, runLevel: true,
                    out int precursors, out int peptides);
                int proteins = ProteinFdrEngine.CountPassingProteinGroups(
                    new List<KeyValuePair<string, List<FdrEntry>>> { kvp }, fullLibrary, config, runLevel: true);
                rows.Add(new[]
                {
                    RunName(kvp.Key),
                    precursors.ToString(CultureInfo.InvariantCulture),
                    peptides.ToString(CultureInfo.InvariantCulture),
                    proteins.ToString(CultureInfo.InvariantCulture),
                });
            }

            // Experiment row: precursors + peptides passing EXPERIMENT-level FDR (a
            // precursor detected in any run counts once), and the experiment-level protein
            // count already computed by the Stage-7 pass.
            CountPrecursorsPeptides(perFileEntries, level, config, runLevel: false,
                out int expPrec, out int expPep);
            int expProteins = 0;
            foreach (var q in experimentResult.ProteinFdr.GroupQvalues.Values)
                if (q <= config.EffectiveProteinFdr) expProteins++;
            rows.Add(new[]
            {
                "Experiment",
                expPrec.ToString(CultureInfo.InvariantCulture),
                expPep.ToString(CultureInfo.InvariantCulture),
                expProteins.ToString(CultureInfo.InvariantCulture),
            });

            using (var w = new StreamWriter(path, false))
            {
                w.NewLine = "\n";
                w.WriteLine(string.Join("\t", new[] { "Run", "Precursors", "Peptides", "Proteins" }));
                foreach (var r in rows)
                    w.WriteLine(string.Join("\t", r));
            }
        }

        // Distinct precursors (modseq + charge) and distinct peptides (modseq) among
        // target entries passing the given FDR scope. Run-level for a replicate row,
        // experiment-level for the experiment row.
        private static void CountPrecursorsPeptides(
            IEnumerable<KeyValuePair<string, List<FdrEntry>>> entries,
            FdrLevel level, OspreyConfig config, bool runLevel,
            out int precursors, out int peptides)
        {
            double gate = runLevel ? config.RunFdr : config.ExperimentFdr;
            var precSet = new HashSet<string>(StringComparer.Ordinal);
            var pepSet = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in entries)
            {
                foreach (var e in kvp.Value)
                {
                    if (e.IsDecoy)
                        continue;
                    double q = runLevel ? e.EffectiveRunQvalue(level) : e.EffectiveExperimentQvalue(level);
                    if (q > gate)
                        continue;
                    precSet.Add(e.ModifiedSequence + "|" + e.Charge.ToString(CultureInfo.InvariantCulture));
                    pepSet.Add(e.ModifiedSequence);
                }
            }
            precursors = precSet.Count;
            peptides = pepSet.Count;
        }

        // Strip directory + extension for a readable run label (the blib RefSpectra source
        // name convention), keeping distinct files distinct.
        private static string RunName(string fileKey)
        {
            if (string.IsNullOrEmpty(fileKey))
                return fileKey;
            return Path.GetFileNameWithoutExtension(fileKey);
        }
    }
}
