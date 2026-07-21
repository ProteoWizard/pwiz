/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4) <noreply .at. anthropic.com>
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

// Protein parsimony and picked-protein FDR control
//
// Implements native C# protein inference:
// - Protein grouping: proteins with identical peptide sets are merged
// - Subset elimination: groups whose peptides are a strict subset of another are removed
// - Shared peptide handling: All (default), Razor, or Unique modes
// - Picked-protein FDR: target-decoy competition at the protein group level
//
// Port of osprey-fdr/src/protein.rs.

using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Osprey.Core;

namespace pwiz.Osprey.FDR
{
    /// <summary>
    /// A protein group: proteins sharing identical peptide sets after parsimony.
    /// </summary>
    public class ProteinGroup
    {
        /// <summary>Unique ID for this group.</summary>
        public uint Id { get; set; }

        /// <summary>Protein accessions in this group (identical peptide sets).</summary>
        public List<string> Accessions { get; set; }

        /// <summary>Peptide sequences unique to this group (modified_sequence).</summary>
        public HashSet<string> UniquePeptides { get; set; }

        /// <summary>Peptide sequences shared with other groups (modified_sequence).</summary>
        public HashSet<string> SharedPeptides { get; set; }

        public ProteinGroup()
        {
            Accessions = new List<string>();
            UniquePeptides = new HashSet<string>();
            SharedPeptides = new HashSet<string>();
        }
    }

    /// <summary>
    /// Result of protein parsimony analysis.
    /// </summary>
    public class ProteinParsimonyResult
    {
        /// <summary>Protein groups after parsimony.</summary>
        public List<ProteinGroup> Groups { get; set; }

        /// <summary>Peptide (modified_sequence) to protein group ID(s) mapping.</summary>
        public Dictionary<string, List<uint>> PeptideToGroupMap { get; set; }

        public ProteinParsimonyResult()
        {
            Groups = new List<ProteinGroup>();
            PeptideToGroupMap = new Dictionary<string, List<uint>>();
        }
    }

    /// <summary>
    /// Peptide-level data for protein FDR scoring.
    /// </summary>
    public class PeptideScore
    {
        /// <summary>Best SVM discriminant score for this peptide across all files.</summary>
        public double Score { get; set; }

        /// <summary>Whether this peptide is a decoy.</summary>
        public bool IsDecoy { get; set; }

        /// <summary>
        /// Best (lowest) run-level <b>peptide</b> q-value for this peptide across all files.
        /// Used as the target-side gate in picked-protein FDR (Savitski 2015 convention):
        /// only targets with <c>BestQvalue &lt;= gate</c> are eligible to contribute.
        /// Rust's <c>collect_best_peptide_scores</c> uses <c>run_peptide_qvalue</c>
        /// (not precursor q-value) for the same reason.
        /// </summary>
        public double BestQvalue { get; set; }
    }

    /// <summary>
    /// Result of picked-protein FDR computation. Only target winners appear in
    /// <see cref="GroupQvalues"/> and <see cref="GroupScores"/>; decoy winners are
    /// statistical machinery for the cumulative FDR computation and are not
    /// exposed. Protein-level posterior error probability (PEP) is intentionally
    /// not computed -- use peptide-level PEP for downstream confidence (matches
    /// Rust <c>ProteinFdrResult</c>).
    /// </summary>
    public class ProteinFdrResult
    {
        /// <summary>Protein group ID to q-value (target winners only).</summary>
        public Dictionary<uint, double> GroupQvalues { get; set; }

        /// <summary>Protein group ID to best peptide SVM score (target winners only).</summary>
        public Dictionary<uint, double> GroupScores { get; set; }

        /// <summary>Peptide (modified_sequence) to best protein q-value among its groups.</summary>
        public Dictionary<string, double> PeptideQvalues { get; set; }

        public ProteinFdrResult()
        {
            GroupQvalues = new Dictionary<uint, double>();
            GroupScores = new Dictionary<uint, double>();
            PeptideQvalues = new Dictionary<string, double>();
        }
    }

    /// <summary>
    /// The computed artifacts of a first-pass protein-FDR run, returned by
    /// <c>ProteinFdr.RunFirstPassProteinFdr</c> so the caller can log
    /// summary counts and emit the Stage-6 diagnostic dump WITHOUT recomputing
    /// parsimony / FDR. The run has already propagated <c>RunProteinQvalue</c>
    /// onto the stubs; these are the same intermediate objects it used.
    /// </summary>
    public class FirstPassProteinFdrResult
    {
        /// <summary>Target peptides passing peptide-level run FDR (the detected set).</summary>
        public HashSet<string> DetectedPeptides { get; }

        /// <summary>Parsimony grouping built from the detected peptides.</summary>
        public ProteinParsimonyResult Parsimony { get; }

        /// <summary>Best peptide-level scores collected across all files.</summary>
        public Dictionary<string, PeptideScore> BestScores { get; }

        /// <summary>The picked-protein FDR result (group + peptide q-values).</summary>
        public ProteinFdrResult ProteinFdr { get; }

        public FirstPassProteinFdrResult(
            HashSet<string> detectedPeptides,
            ProteinParsimonyResult parsimony,
            Dictionary<string, PeptideScore> bestScores,
            ProteinFdrResult proteinFdr)
        {
            DetectedPeptides = detectedPeptides;
            Parsimony = parsimony;
            BestScores = bestScores;
            ProteinFdr = proteinFdr;
        }
    }

    /// <summary>
    /// Streaming first-pass protein-FDR reducer (issue #4355 struct-shrink S2): builds the
    /// detected-peptide set + per-peptide best scores from rows fed one at a time, then runs
    /// the identical parsimony + picked-protein FDR the buffer overloads run. Lets the
    /// bounded per-file consumer stream each row's <c>(modifiedSequence, isDecoy, score,
    /// runPeptideQvalue)</c> straight off the <c>.1st-pass.fdr_scores.bin</c> sidecar
    /// (score / run_peptide_q) + the parquet scalars (modseq / isDecoy) -- read in the Tasks
    /// layer, which owns the disk I/O -- WITHOUT holding the resident <see cref="FdrProjection"/>
    /// buffer + the parallel <c>FdrProjectionOutputs</c> array. Both reductions
    /// (detected-gate + best max-score / min-q) are order-independent and a modified sequence
    /// maps to a single target/decoy label, so any streaming order reproduces the resident
    /// <see cref="ProteinFdr.CollectBestPeptideScores(IList{KeyValuePair{string, List{FdrEntry}}})"/>
    /// path byte-identically. The caller then patches each entry's <c>run_protein_qvalue</c>
    /// onto the sidecar from <see cref="FirstPassProteinFdrResult.ProteinFdr"/>'s
    /// <c>PeptideQvalues</c> (replacing the resident <c>PropagateRunProteinQvalues</c> +
    /// phase-2 patch with one streaming pass).
    /// </summary>
    public sealed class FirstPassProteinFdrAccumulator
    {
        private readonly double _runFdr;
        private readonly HashSet<string> _detectedPeptides = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, PeptideScore> _bestScores = new Dictionary<string, PeptideScore>();

        public FirstPassProteinFdrAccumulator(double runFdr)
        {
            _runFdr = runFdr;
        }

        /// <summary>
        /// Fold one row into the detected-peptide set (targets passing peptide-level run FDR,
        /// matching Rust pipeline.rs:4301) and the per-peptide best (max) score / best (min)
        /// run peptide q-value reduction (matching
        /// <see cref="ProteinFdr.CollectBestPeptideScores(IList{KeyValuePair{string, List{FdrEntry}}})"/>).
        /// </summary>
        public void Add(string modifiedSequence, bool isDecoy, double score, double runPeptideQvalue)
        {
            if (!isDecoy && runPeptideQvalue <= _runFdr)
                _detectedPeptides.Add(modifiedSequence);

            PeptideScore ps;
            if (_bestScores.TryGetValue(modifiedSequence, out ps))
            {
                if (score > ps.Score)
                    ps.Score = score;
                if (runPeptideQvalue < ps.BestQvalue)
                    ps.BestQvalue = runPeptideQvalue;
            }
            else
            {
                _bestScores[modifiedSequence] = new PeptideScore
                {
                    Score = score,
                    IsDecoy = isDecoy,
                    BestQvalue = runPeptideQvalue
                };
            }
        }

        /// <summary>
        /// Run the identical parsimony + picked-protein FDR the buffer path runs and return
        /// the artifacts (the caller logs summary counts + patches the sidecar's
        /// <c>run_protein_qvalue</c> from <see cref="ProteinFdrResult.PeptideQvalues"/>). The
        /// cross-impl best-peptide-scores dump fires here, at the same point the resident
        /// <see cref="ProteinFdr.CollectBestPeptideScores(IList{KeyValuePair{string, List{FdrEntry}}})"/>
        /// emits it (after the reduction is complete).
        /// </summary>
        public FirstPassProteinFdrResult Finish(IList<LibraryEntry> fullLibrary, OspreyConfig config)
        {
            if (FdrDiagnostics.DumpBestPeptideScores)
                FdrDiagnostics.WriteBestPeptideScoresDump(_bestScores);

            var parsimony = ProteinFdr.BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, _detectedPeptides);
            var proteinFdr = ProteinFdr.ComputeProteinFdr(parsimony, _bestScores, config.RunFdr);

            return new FirstPassProteinFdrResult(
                _detectedPeptides, parsimony, _bestScores, proteinFdr);
        }
    }

    /// <summary>
    /// The computed artifacts of a second-pass / run-wide protein-FDR run, returned
    /// by <see cref="ProteinFdrEngine.RunSecondPass"/> so the Tasks-layer caller can
    /// emit the Stage-7 detected-peptides and protein-FDR diagnostic dumps (and the
    /// <c>Stage7ProteinFdrOnly</c> early-exit decision) WITHOUT recomputing parsimony
    /// / FDR. The run has already propagated <c>RunProteinQvalue</c> and
    /// <c>ExperimentProteinQvalue</c> onto the stubs; these are the same intermediate
    /// objects it used.
    /// </summary>
    public class SecondPassProteinFdrResult
    {
        /// <summary>Target peptides passing experiment-level run FDR (the detected set).</summary>
        public HashSet<string> DetectedPeptides { get; }

        /// <summary>Parsimony grouping built from the detected peptides.</summary>
        public ProteinParsimonyResult Parsimony { get; }

        /// <summary>The picked-protein FDR result (group + peptide q-values).</summary>
        public ProteinFdrResult ProteinFdr { get; }

        public SecondPassProteinFdrResult(
            HashSet<string> detectedPeptides,
            ProteinParsimonyResult parsimony,
            ProteinFdrResult proteinFdr)
        {
            DetectedPeptides = detectedPeptides;
            Parsimony = parsimony;
            ProteinFdr = proteinFdr;
        }
    }

    /// <summary>
    /// Protein parsimony and FDR computation.
    /// Port of osprey-fdr/src/protein.rs.
    /// </summary>
    public static class ProteinFdr
    {
        private const string DECOY_PREFIX = "DECOY_";

        /// <summary>
        /// Build protein parsimony from the spectral library.
        ///
        /// Groups proteins with identical peptide sets, eliminates subsets, and
        /// classifies peptides as unique or shared. Only target entries are used.
        /// </summary>
        /// <param name="libraryEntries">Spectral library entries.</param>
        /// <param name="mode">Shared peptide handling mode.</param>
        /// <param name="detectedPeptides">If non-null, only include library entries whose
        /// modified_sequence is in this set.</param>
        public static ProteinParsimonyResult BuildProteinParsimony(
            IList<LibraryEntry> libraryEntries,
            SharedPeptideMode mode,
            HashSet<string> detectedPeptides)
        {
            // Step 1: Build bipartite graph from target entries only
            var peptideToProteins = new Dictionary<string, HashSet<string>>();
            var proteinToPeptides = new Dictionary<string, HashSet<string>>();

            foreach (var entry in libraryEntries)
            {
                if (entry.IsDecoy)
                    continue;
                if (detectedPeptides != null && !detectedPeptides.Contains(entry.ModifiedSequence))
                    continue;
                foreach (string protein in entry.ProteinIds)
                {
                    HashSet<string> pepSet;
                    if (!peptideToProteins.TryGetValue(entry.ModifiedSequence, out pepSet))
                    {
                        pepSet = new HashSet<string>();
                        peptideToProteins[entry.ModifiedSequence] = pepSet;
                    }
                    pepSet.Add(protein);

                    HashSet<string> protSet;
                    if (!proteinToPeptides.TryGetValue(protein, out protSet))
                    {
                        protSet = new HashSet<string>();
                        proteinToPeptides[protein] = protSet;
                    }
                    protSet.Add(entry.ModifiedSequence);
                }
            }

            // Step 2: Group proteins with identical peptide sets
            var peptideSetToAccessions = new Dictionary<string, List<string>>();
            foreach (var kvp in proteinToPeptides)
            {
                var sortedPeptides = new List<string>(kvp.Value);
                sortedPeptides.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to build a canonical "|"-joined set key; equal peptide strings are byte-identical so tie order does not change the key
                string key = string.Join("|", sortedPeptides);

                List<string> accessions;
                if (!peptideSetToAccessions.TryGetValue(key, out accessions))
                {
                    accessions = new List<string>();
                    peptideSetToAccessions[key] = accessions;
                }
                accessions.Add(kvp.Key);
            }

            // Build (peptideSet, accessions) pairs sorted by peptide count descending.
            // HashSet<string> instead of SortedSet — IsSubsetOf in Step 3 is the
            // O(N^2) hot path on Stage 7, and HashSet membership tests are
            // ~10x faster than SortedSet's binary search for the typical
            // 5K-group / 5-30-peptide workload (Stellar 3-file dropped from
            // 6.3s to ~0.5s after the swap). Iteration order of HashSet is
            // unspecified, but the only downstream use is populating the
            // peptideToGroups dictionary, which is itself unordered.
            var groups = new List<KeyValuePair<HashSet<string>, List<string>>>();
            foreach (var kvp in peptideSetToAccessions)
            {
                var peptideSet = new HashSet<string>(kvp.Key.Split('|'), StringComparer.Ordinal);
                groups.Add(new KeyValuePair<HashSet<string>, List<string>>(peptideSet, kvp.Value));
            }
            // Array.Sort OK: subset elimination below only removes a group when its count is
            // STRICTLY less than a retained group's, so equal-count groups never eliminate one
            // another and the retained SET is invariant under tie order. Tie hazard, conversion
            // deferred: equal-count groups' relative order still sets their GroupId assignment
            // in Step 4, which is the same GroupId-order class as the #4362 canonical incident.
            // Left byte-identical here; the parsimony rewrite (#4357) is the right place to pin
            // a stable secondary key. Not a #4362 approved U-site.
            groups.Sort((a, b) => b.Key.Count.CompareTo(a.Key.Count)); // Array.Sort OK: (see above) retained SET is tie-invariant; GroupId-order tie hazard deferred to #4357

            // Step 3: Subset elimination — rarest-peptide candidate scan (issue #4357).
            //
            // The naive form scanned every already-retained group for each group,
            // which is O(groups^2 x peptides) — tens of seconds to minutes at
            // SEA-AD scale (tens of thousands of protein groups). A proper superset
            // of group A must contain ALL of A's peptides, hence A's rarest peptide.
            // So we index peptide -> retained-group indices INCREMENTALLY (only
            // groups already appended to retained), and for each A test only the
            // retained groups sharing A's rarest peptide. This prunes only groups
            // that provably cannot be supersets, so it drops exactly the same
            // groups in exactly the same order as the pairwise scan — byte-identical
            // protein grouping — while running near-linearly.
            //
            // Invariants (see issue #4357): the DESCENDING sort above is left
            // untouched (its unstable tie order is part of the golden output);
            // groups are iterated in that order and non-subsets appended to
            // retained in that same order (retained order == gid in Step 4); and
            // the drop test is the identical proper-subset predicate against
            // already-retained groups only.

            // Global peptide -> group-count over ALL groups, used only to pick each
            // group's rarest peptide (the pivot minimizing the candidate set). This
            // choice does not affect correctness, only which peptide's candidate
            // list is scanned.
            var peptideGroupCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                foreach (string peptide in group.Key)
                {
                    int count;
                    peptideGroupCount.TryGetValue(peptide, out count);
                    peptideGroupCount[peptide] = count + 1;
                }
            }

            var retained = new List<KeyValuePair<HashSet<string>, List<string>>>();
            // peptide -> indices into retained of groups (already appended) containing it.
            var peptideToRetained = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            foreach (var group in groups)
            {
                var peptideSet = group.Key;

                // Pick the rarest peptide of this group as the pivot: any proper
                // superset must contain it, so its (typically short) candidate list
                // is sufficient to find every possible superset.
                string pivot = null;
                int pivotCount = int.MaxValue;
                foreach (string peptide in peptideSet)
                {
                    int count = peptideGroupCount[peptide];
                    if (count < pivotCount)
                    {
                        pivotCount = count;
                        pivot = peptide;
                    }
                }

                bool isSubset = false;
                List<int> candidates;
                if (pivot != null && peptideToRetained.TryGetValue(pivot, out candidates))
                {
                    foreach (int candidateIdx in candidates)
                    {
                        var larger = retained[candidateIdx];
                        if (peptideSet.Count < larger.Key.Count && peptideSet.IsSubsetOf(larger.Key))
                        {
                            isSubset = true;
                            break;
                        }
                    }
                }

                if (!isSubset)
                {
                    int idx = retained.Count;
                    retained.Add(group);
                    foreach (string peptide in peptideSet)
                    {
                        List<int> list;
                        if (!peptideToRetained.TryGetValue(peptide, out list))
                        {
                            list = new List<int>();
                            peptideToRetained[peptide] = list;
                        }
                        list.Add(idx);
                    }
                }
            }

            // Step 4: Assign group IDs and build peptide -> group mapping
            var resultGroups = new List<ProteinGroup>(retained.Count);
            var peptideToGroups = new Dictionary<string, List<uint>>();

            for (int idx = 0; idx < retained.Count; idx++)
            {
                uint gid = (uint)idx;
                var peptideSet = retained[idx].Key;
                var accessions = retained[idx].Value;

                foreach (string peptide in peptideSet)
                {
                    List<uint> groupList;
                    if (!peptideToGroups.TryGetValue(peptide, out groupList))
                    {
                        groupList = new List<uint>();
                        peptideToGroups[peptide] = groupList;
                    }
                    groupList.Add(gid);
                }

                resultGroups.Add(new ProteinGroup
                {
                    Id = gid,
                    Accessions = accessions,
                    UniquePeptides = new HashSet<string>(),
                    SharedPeptides = new HashSet<string>()
                });
            }

            // Classify peptides as unique or shared
            foreach (var kvp in peptideToGroups)
            {
                string peptide = kvp.Key;
                var groupIds = kvp.Value;
                if (groupIds.Count == 1)
                {
                    resultGroups[(int)groupIds[0]].UniquePeptides.Add(peptide);
                }
                else
                {
                    foreach (uint gid in groupIds)
                        resultGroups[(int)gid].SharedPeptides.Add(peptide);
                }
            }

            // Step 5: Apply shared peptide mode
            switch (mode)
            {
                case SharedPeptideMode.All:
                    // No reassignment needed
                    break;

                case SharedPeptideMode.Razor:
                {
                    // Iterative greedy set cover, mirroring Rust
                    // osprey-fdr/src/protein.rs. Each round selects the GROUP with the
                    // most unique peptides that still owns at least one unassigned shared
                    // peptide, claims ALL of that group's remaining shared peptides (in
                    // sorted order), repoints them to the winner, and repeats until no
                    // shared peptides remain. The winner is chosen GLOBALLY each round --
                    // not by iterating the shared peptides in dictionary order -- so the
                    // result is deterministic and path-independent. The former per-peptide
                    // greedy assigned each shared peptide independently in Dictionary
                    // (hash) order, which both diverged from Rust on cascading topologies
                    // and was not stable across processes under .NET randomized string
                    // hashing. Tiebreak on equal unique counts is the lowest group ID.
                    var sharedPeptides = new List<string>();
                    foreach (var kvp in peptideToGroups)
                    {
                        if (kvp.Value.Count > 1)
                            sharedPeptides.Add(kvp.Key);
                    }
                    // Sort ordinally so the round-by-round processing order matches Rust's
                    // byte-wise String sort and is independent of dictionary iteration.
                    sharedPeptides.Sort(StringComparer.Ordinal); // Array.Sort OK: distinct peptide-map keys (no ties), so ordinal order is total and equals Rust's stable String sort
                    var unassigned = new HashSet<string>(sharedPeptides, StringComparer.Ordinal);

                    while (unassigned.Count > 0)
                    {
                        // Pick the group with the most unique peptides that still owns an
                        // unassigned shared peptide. resultGroups is indexed by group ID,
                        // so iterating it ascending with a strict '>' resolves equal-count
                        // ties to the lowest group ID automatically. The unique count is
                        // read live, so peptides claimed in earlier rounds raise a group's
                        // count for later rounds (the greedy cascade).
                        ProteinGroup bestGroup = null;
                        foreach (var group in resultGroups)
                        {
                            bool ownsUnassigned = false;
                            foreach (string p in group.SharedPeptides)
                            {
                                if (unassigned.Contains(p))
                                {
                                    ownsUnassigned = true;
                                    break;
                                }
                            }
                            if (!ownsUnassigned)
                                continue;
                            if (bestGroup == null ||
                                group.UniquePeptides.Count > bestGroup.UniquePeptides.Count)
                            {
                                bestGroup = group;
                            }
                        }

                        if (bestGroup == null)
                            break; // no group owns any unassigned shared peptide

                        // Claim all of the winner's still-unassigned shared peptides, in
                        // sorted order for deterministic processing.
                        var claimed = new List<string>();
                        foreach (string p in bestGroup.SharedPeptides)
                        {
                            if (unassigned.Contains(p))
                                claimed.Add(p);
                        }
                        claimed.Sort(StringComparer.Ordinal); // Array.Sort OK: distinct shared-peptide strings from a HashSet (no ties); matches Rust claimed.sort()

                        foreach (string peptide in claimed)
                        {
                            // Remove from every group's shared set, add to the winner's
                            // unique set, and repoint the map to the winner alone.
                            foreach (uint gid in peptideToGroups[peptide])
                                resultGroups[(int)gid].SharedPeptides.Remove(peptide);
                            bestGroup.UniquePeptides.Add(peptide);
                            peptideToGroups[peptide] = new List<uint> { bestGroup.Id };
                            unassigned.Remove(peptide);
                        }
                    }
                    break;
                }

                case SharedPeptideMode.Unique:
                {
                    var sharedPeptides = new List<string>();
                    foreach (var kvp in peptideToGroups)
                    {
                        if (kvp.Value.Count > 1)
                            sharedPeptides.Add(kvp.Key);
                    }

                    foreach (string peptide in sharedPeptides)
                    {
                        var groupIds = peptideToGroups[peptide];
                        foreach (uint gid in groupIds)
                            resultGroups[(int)gid].SharedPeptides.Remove(peptide);
                        peptideToGroups.Remove(peptide);
                    }
                    break;
                }
            }

            return new ProteinParsimonyResult
            {
                Groups = resultGroups,
                PeptideToGroupMap = peptideToGroups
            };
        }

        /// <summary>
        /// Compute picked-protein FDR (Savitski et al. 2015). Mirrors Rust
        /// <c>osprey-fdr/src/protein.rs::compute_protein_fdr</c>.
        ///
        /// For each protein group:
        /// <list type="number">
        /// <item><description><b>Score by best peptide.</b> Target-side score = max
        /// SVM discriminant over target peptides whose best peptide-level q-value is
        /// &lt;= <paramref name="qvalueGate"/> (Savitski's gate, restricts analysis
        /// to "proteins we'd actually report"). Decoy-side score = max SVM
        /// discriminant over ALL decoy peptides for the group (the decoy side is
        /// NOT gated; gating decoys would create survivorship bias and collapse the
        /// null distribution).</description></item>
        /// <item><description><b>Pairwise picking.</b> Each group produces exactly
        /// one winner: target wins on <c>target_score &gt;= decoy_score</c>
        /// (ties to target). Groups with only one side win that side. Groups with
        /// no passing peptides are skipped.</description></item>
        /// <item><description><b>Cumulative FDR on winners</b> sorted by SVM score
        /// descending (tiebreak group_id ascending). At each rank,
        /// <c>q = cum_decoys / cum_targets</c>, capped at 1.0.</description></item>
        /// <item><description><b>Backward sweep</b> enforces monotonicity (lower
        /// score → non-decreasing q-value). Only target winners are emitted in
        /// <see cref="ProteinFdrResult.GroupQvalues"/>; decoy winners are
        /// statistical machinery and not exposed.</description></item>
        /// <item><description><b>Peptide propagation.</b> Each peptide's q-value is
        /// the min q-value among the protein groups it belongs to (peptides whose
        /// only groups lost the pair stay at q = 1.0).</description></item>
        /// </list>
        ///
        /// <b>Why picked-protein:</b> classical protein-level TDC suffers decoy
        /// over-representation in the low-scoring region. Pairwise picking
        /// produces a balanced winner pool by construction so cumulative FDR is
        /// well-calibrated. <b>Why SVM (not q-value or PEP):</b> losing decoys
        /// have q=1.0 / PEP≈1.0 which collapses the null distribution; raw SVM
        /// gives every entry a well-defined score on the same scale.
        ///
        /// Reference: Savitski MM et al., "A Scalable Approach for Protein False
        /// Discovery Rate Estimation in Large Proteomic Data Sets,"
        /// Mol Cell Proteomics. 2015;14(9):2394-2404.
        /// </summary>
        public static ProteinFdrResult ComputeProteinFdr(
            ProteinParsimonyResult parsimony,
            Dictionary<string, PeptideScore> bestScores,
            double qvalueGate)
        {
            // Step 1: Per-group max SVM score on each side.
            // - Target side: gated by best peptide-level q-value <= qvalueGate.
            // - Decoy side: NOT gated (forms the null distribution).
            // Iterate parsimony.PeptideToGroupMap which is keyed by the target
            // modified_sequence; the decoy side is looked up via DECOY_<seq>.
            var targetScore = new Dictionary<uint, double>();
            var decoyScore = new Dictionary<uint, double>();

            foreach (var kvp in parsimony.PeptideToGroupMap)
            {
                string peptide = kvp.Key;
                var groupIds = kvp.Value;

                PeptideScore tps;
                if (bestScores.TryGetValue(peptide, out tps) &&
                    !tps.IsDecoy && tps.BestQvalue <= qvalueGate)
                {
                    foreach (uint gid in groupIds)
                    {
                        double existing;
                        if (targetScore.TryGetValue(gid, out existing))
                        {
                            if (tps.Score > existing)
                                targetScore[gid] = tps.Score;
                        }
                        else
                        {
                            targetScore[gid] = tps.Score;
                        }
                    }
                }

                PeptideScore dps;
                if (bestScores.TryGetValue(DECOY_PREFIX + peptide, out dps) && dps.IsDecoy)
                {
                    foreach (uint gid in groupIds)
                    {
                        double existing;
                        if (decoyScore.TryGetValue(gid, out existing))
                        {
                            if (dps.Score > existing)
                                decoyScore[gid] = dps.Score;
                        }
                        else
                        {
                            decoyScore[gid] = dps.Score;
                        }
                    }
                }
            }

            // Step 2: Pair picking. Iterate parsimony.Groups in deterministic
            // order. Each group yields one winner: target if t >= d, else
            // decoy. Carry a sorted-accessions string on each winner so the
            // Step 3 sort tiebreak can use a cross-impl-deterministic key.
            // The numeric GroupId is HashMap-iteration-order-derived in
            // BuildProteinParsimony and so picks different positions on
            // ties cross-impl once upstream arithmetic is bit-equal.
            var winners = new List<ProteinWinner>();
            foreach (var group in parsimony.Groups)
            {
                // Build sort_key once per group: accessions sorted then
                // joined with semicolons (matches the Rust port and the
                // Stage 7 diagnostic dump).
                var sortedAccs = new List<string>(group.Accessions);
                sortedAccs.Sort(StringComparer.Ordinal); // Array.Sort OK: sorted only to build a canonical ";"-joined sortKey; equal accession strings are byte-identical so tie order does not change the key
                string sortKey = string.Join(";", sortedAccs);

                bool hasT = targetScore.TryGetValue(group.Id, out double t);
                bool hasD = decoyScore.TryGetValue(group.Id, out double d);
                if (hasT && hasD)
                {
                    if (t >= d)
                        winners.Add(new ProteinWinner { GroupId = group.Id, SortKey = sortKey, Score = t, IsDecoy = false });
                    else
                        winners.Add(new ProteinWinner { GroupId = group.Id, SortKey = sortKey, Score = d, IsDecoy = true });
                }
                else if (hasT)
                {
                    winners.Add(new ProteinWinner { GroupId = group.Id, SortKey = sortKey, Score = t, IsDecoy = false });
                }
                else if (hasD)
                {
                    winners.Add(new ProteinWinner { GroupId = group.Id, SortKey = sortKey, Score = d, IsDecoy = true });
                }
            }

            // Step 3: Cumulative FDR. Sort winners by score descending,
            // tiebreak by sorted accessions ASCENDING — cross-impl-
            // deterministic, unlike GroupId which is HashMap-iteration-
            // order from BuildProteinParsimony.
            winners.Sort((a, b) => // Array.Sort OK: SortKey is the sorted-accessions string from BuildProteinParsimony, which assigns a unique accessions list to each ProteinGroup (identical sets are merged), so the comparator never returns 0 and unstable-sort tie reorder cannot fire
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                return string.CompareOrdinal(a.SortKey, b.SortKey);
            });

            var rawQvalues = new double[winners.Count];
            int cumTargets = 0;
            int cumDecoys = 0;
            for (int i = 0; i < winners.Count; i++)
            {
                if (winners[i].IsDecoy)
                    cumDecoys++;
                else
                    cumTargets++;
                rawQvalues[i] = cumTargets > 0
                    ? Math.Min(1.0, (double)cumDecoys / cumTargets)
                    : 1.0;
            }

            // Step 4: Backward sweep for monotonicity. Emit only target winners
            // to GroupQvalues / GroupScores.
            var groupQvalues = new Dictionary<uint, double>();
            var groupScores = new Dictionary<uint, double>();
            var monotonicQvalues = new double[winners.Count];
            double minQ = 1.0;
            for (int i = winners.Count - 1; i >= 0; i--)
            {
                if (rawQvalues[i] < minQ)
                    minQ = rawQvalues[i];
                monotonicQvalues[i] = minQ;
                var w = winners[i];
                if (!w.IsDecoy)
                {
                    groupQvalues[w.GroupId] = minQ;
                    groupScores[w.GroupId] = w.Score;
                }
            }

            // Cross-impl bisection dump (env-var-gated, no-op in production).
            // The flag check short-circuits the LINQ projection below; in
            // the disabled-dump path this whole block is one field read.
            // Dump function lives in FdrDiagnostics so the file I/O stays
            // isolated from the protein-FDR algorithm code.
            if (FdrDiagnostics.DumpStage7Winners)
            {
                FdrDiagnostics.WriteStage7WinnersDump(
                    winners.Select(w => (w.Score, w.IsDecoy)).ToList(),
                    rawQvalues, monotonicQvalues);
            }

            // Step 5: Propagate to peptides. Each peptide's q-value is the min
            // (best) q-value across its groups. Peptides whose only groups lost
            // the pair stay at q = 1.0.
            var peptideQvalues = new Dictionary<string, double>();
            foreach (var kvp in parsimony.PeptideToGroupMap)
            {
                string peptide = kvp.Key;
                var groupIds = kvp.Value;
                double bestQ = 1.0;
                foreach (uint gid in groupIds)
                {
                    double q;
                    if (groupQvalues.TryGetValue(gid, out q) && q < bestQ)
                        bestQ = q;
                }
                peptideQvalues[peptide] = bestQ;
            }

            return new ProteinFdrResult
            {
                GroupQvalues = groupQvalues,
                GroupScores = groupScores,
                PeptideQvalues = peptideQvalues
            };
        }

        private struct ProteinWinner
        {
            public uint GroupId;
            public string SortKey; // sorted-accessions string for cross-impl tiebreak
            public double Score;
            public bool IsDecoy;
        }

        /// <summary>
        /// Collect peptide-level scores for protein FDR.
        ///
        /// For each unique modified_sequence, keeps the best (max) SVM score and the
        /// best (min) run-level <b>peptide</b> q-value across all files.
        /// Mirrors Rust <c>collect_best_peptide_scores</c>: picked-protein gates on
        /// peptide-level FDR per Savitski 2015, not precursor-level.
        /// </summary>
        public static Dictionary<string, PeptideScore> CollectBestPeptideScores(
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries)
        {
            var best = new Dictionary<string, PeptideScore>();
            foreach (var file in perFileEntries)
            {
                foreach (var entry in file.Value)
                {
                    PeptideScore ps;
                    if (best.TryGetValue(entry.ModifiedSequence, out ps))
                    {
                        if (entry.Score > ps.Score)
                            ps.Score = entry.Score;
                        if (entry.RunPeptideQvalue < ps.BestQvalue)
                            ps.BestQvalue = entry.RunPeptideQvalue;
                    }
                    else
                    {
                        best[entry.ModifiedSequence] = new PeptideScore
                        {
                            Score = entry.Score,
                            IsDecoy = entry.IsDecoy,
                            BestQvalue = entry.RunPeptideQvalue
                        };
                    }
                }
            }

            // Cross-impl bisection dump (env-var-gated, no-op in production).
            // See FdrDiagnostics.WriteBestPeptideScoresDump for context.
            if (FdrDiagnostics.DumpBestPeptideScores)
                FdrDiagnostics.WriteBestPeptideScoresDump(best);

            return best;
        }

        /// <summary>
        /// Propagate protein q-values to FdrEntry stubs.
        /// </summary>
        public static void PropagateProteinQvalues(
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            ProteinFdrResult proteinFdr,
            bool setRun,
            bool setExperiment)
        {
            foreach (var file in perFileEntries)
            {
                foreach (var entry in file.Value)
                {
                    double q;
                    if (!proteinFdr.PeptideQvalues.TryGetValue(entry.ModifiedSequence, out q))
                        q = 1.0;
                    if (setRun)
                        entry.RunProteinQvalue = q;
                    if (setExperiment)
                        entry.ExperimentProteinQvalue = q;
                }
            }
        }

        /// <summary>
        /// First-pass protein FDR: build parsimony from peptides passing peptide-level
        /// run FDR, run picked-protein FDR at <see cref="OspreyConfig.RunFdr"/> (1x Savitski
        /// gate), and write the resulting q-values into <see cref="FdrEntry.RunProteinQvalue"/>
        /// on every stub. Mirrors Rust <c>pipeline.rs::run_analysis</c> first-pass block
        /// (around line 4292). Caller is responsible for any logging, dump diagnostics,
        /// and downstream consumption.
        ///
        /// Used by FirstJoinTask for the in-process pipeline (runs after first-pass FDR,
        /// before compaction) and by PerFileRescoreTask for the <c>--task SecondPassFDR</c>
        /// rehydration path (runs after sidecar load, before compaction) so the protein-
        /// rescue branch of compaction has fresh <c>RunProteinQvalue</c> values matching
        /// what Rust computes inline. Without it, the rehydrated C# pipeline used only
        /// the <c>RunProteinQvalue</c> values stored in the 1st-pass FDR sidecar; for
        /// single-file <c>--task SecondPassFDR</c> runs that left 19 peptides outside Rust's
        /// post-compaction detected set on Stellar Single, causing a 1-protein delta in
        /// Stage 7 picked-protein output.
        /// </summary>
        public static FirstPassProteinFdrResult RunFirstPassProteinFdr(
            IList<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            IList<LibraryEntry> fullLibrary,
            OspreyConfig config)
        {
            // Detected-peptide gate: targets passing peptide-level run FDR.
            // Matches Rust pipeline.rs:4301 (e.run_peptide_qvalue <= config.run_fdr).
            var detectedPeptides = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kvp in perFileEntries)
            {
                foreach (var entry in kvp.Value)
                {
                    if (!entry.IsDecoy && entry.RunPeptideQvalue <= config.RunFdr)
                        detectedPeptides.Add(entry.ModifiedSequence);
                }
            }

            var parsimony = BuildProteinParsimony(
                fullLibrary, config.SharedPeptides, detectedPeptides);
            var bestScores = CollectBestPeptideScores(perFileEntries);
            var proteinFdr = ComputeProteinFdr(parsimony, bestScores, config.RunFdr);

            // Set RunProteinQvalue ONLY. ExperimentProteinQvalue is set by the
            // post-output Stage 7 second-pass protein FDR (Rust's second-pass).
            PropagateProteinQvalues(perFileEntries, proteinFdr,
                setRun: true, setExperiment: false);

            // Return the computed artifacts so the caller can log summary counts
            // and emit the Stage-6 diagnostic dump without recomputing them.
            return new FirstPassProteinFdrResult(
                detectedPeptides, parsimony, bestScores, proteinFdr);
        }

    }
}
