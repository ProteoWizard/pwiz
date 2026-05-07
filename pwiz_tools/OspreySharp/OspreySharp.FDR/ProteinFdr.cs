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
using pwiz.OspreySharp.Core;

namespace pwiz.OspreySharp.FDR
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
                sortedPeptides.Sort(StringComparer.Ordinal);
                string key = string.Join("|", sortedPeptides);

                List<string> accessions;
                if (!peptideSetToAccessions.TryGetValue(key, out accessions))
                {
                    accessions = new List<string>();
                    peptideSetToAccessions[key] = accessions;
                }
                accessions.Add(kvp.Key);
            }

            // Build (peptideSet, accessions) pairs sorted by peptide count descending
            var groups = new List<KeyValuePair<SortedSet<string>, List<string>>>();
            foreach (var kvp in peptideSetToAccessions)
            {
                var peptideSet = new SortedSet<string>(kvp.Key.Split('|'));
                groups.Add(new KeyValuePair<SortedSet<string>, List<string>>(peptideSet, kvp.Value));
            }
            groups.Sort((a, b) => b.Key.Count.CompareTo(a.Key.Count));

            // Step 3: Subset elimination
            var retained = new List<KeyValuePair<SortedSet<string>, List<string>>>();
            foreach (var group in groups)
            {
                bool isSubset = false;
                foreach (var larger in retained)
                {
                    if (group.Key.Count < larger.Key.Count && group.Key.IsSubsetOf(larger.Key))
                    {
                        isSubset = true;
                        break;
                    }
                }
                if (!isSubset)
                    retained.Add(group);
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
                    var sharedPeptides = new List<string>();
                    foreach (var kvp in peptideToGroups)
                    {
                        if (kvp.Value.Count > 1)
                            sharedPeptides.Add(kvp.Key);
                    }

                    foreach (string peptide in sharedPeptides)
                    {
                        var groupIds = peptideToGroups[peptide];
                        // Find group with most unique peptides (tiebreak: lowest group ID)
                        uint bestGid = groupIds[0];
                        int bestCount = resultGroups[(int)bestGid].UniquePeptides.Count;
                        for (int i = 1; i < groupIds.Count; i++)
                        {
                            int count = resultGroups[(int)groupIds[i]].UniquePeptides.Count;
                            if (count > bestCount ||
                                (count == bestCount && groupIds[i] < bestGid))
                            {
                                bestCount = count;
                                bestGid = groupIds[i];
                            }
                        }

                        // Remove from all groups' shared sets, add to best group's unique set
                        foreach (uint gid in groupIds)
                            resultGroups[(int)gid].SharedPeptides.Remove(peptide);
                        resultGroups[(int)bestGid].UniquePeptides.Add(peptide);

                        // Update peptide_to_groups to point only to the best group
                        peptideToGroups[peptide] = new List<uint> { bestGid };
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
            // order. Each group yields one winner: target if t >= d, else decoy.
            var winners = new List<ProteinWinner>();
            foreach (var group in parsimony.Groups)
            {
                bool hasT = targetScore.TryGetValue(group.Id, out double t);
                bool hasD = decoyScore.TryGetValue(group.Id, out double d);
                if (hasT && hasD)
                {
                    if (t >= d)
                        winners.Add(new ProteinWinner { GroupId = group.Id, Score = t, IsDecoy = false });
                    else
                        winners.Add(new ProteinWinner { GroupId = group.Id, Score = d, IsDecoy = true });
                }
                else if (hasT)
                {
                    winners.Add(new ProteinWinner { GroupId = group.Id, Score = t, IsDecoy = false });
                }
                else if (hasD)
                {
                    winners.Add(new ProteinWinner { GroupId = group.Id, Score = d, IsDecoy = true });
                }
            }

            // Step 3: Cumulative FDR. Sort winners by score descending,
            // tiebreak by group_id ascending for determinism.
            winners.Sort((a, b) =>
            {
                int cmp = b.Score.CompareTo(a.Score);
                if (cmp != 0) return cmp;
                return a.GroupId.CompareTo(b.GroupId);
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
            double minQ = 1.0;
            for (int i = winners.Count - 1; i >= 0; i--)
            {
                if (rawQvalues[i] < minQ)
                    minQ = rawQvalues[i];
                var w = winners[i];
                if (!w.IsDecoy)
                {
                    groupQvalues[w.GroupId] = minQ;
                    groupScores[w.GroupId] = w.Score;
                }
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
    }
}
