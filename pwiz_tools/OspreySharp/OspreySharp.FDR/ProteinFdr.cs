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
using pwiz.OspreySharp.ML;

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

        /// <summary>Best (lowest) run-level precursor q-value for this peptide.</summary>
        public double BestQvalue { get; set; }
    }

    /// <summary>
    /// Result of picked-protein FDR computation.
    /// </summary>
    public class ProteinFdrResult
    {
        /// <summary>Protein group ID to q-value.</summary>
        public Dictionary<uint, double> GroupQvalues { get; set; }

        /// <summary>Protein group ID to posterior error probability.</summary>
        public Dictionary<uint, double> GroupPep { get; set; }

        /// <summary>Protein group ID to best peptide SVM score (target side).</summary>
        public Dictionary<uint, double> GroupScores { get; set; }

        /// <summary>Peptide (modified_sequence) to best protein q-value among its groups.</summary>
        public Dictionary<string, double> PeptideQvalues { get; set; }

        public ProteinFdrResult()
        {
            GroupQvalues = new Dictionary<uint, double>();
            GroupPep = new Dictionary<uint, double>();
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
        private static readonly string DECOY_PREFIX = "DECOY_";
        private static readonly double EPSILON = 1e-16;

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
        /// Compute protein-level FDR using DIA-NN-style composite scoring.
        ///
        /// Uses two complementary scoring metrics, computing q-values independently
        /// on each and taking the minimum:
        /// 1. Composite score (sum of per-peptide log-likelihoods)
        /// 2. Best peptide quality: max(0, 1 - peptide_err)
        /// </summary>
        public static ProteinFdrResult ComputeProteinFdr(
            ProteinParsimonyResult parsimony,
            Dictionary<string, PeptideScore> bestScores,
            double qvalueGate)
        {
            // Count proteotypic peptides per group
            var nPeptidesPerGroup = new Dictionary<uint, int>();
            foreach (var group in parsimony.Groups)
            {
                nPeptidesPerGroup[group.Id] =
                    group.UniquePeptides.Count + group.SharedPeptides.Count;
            }

            // Accumulate scores for target and decoy versions
            var targetComposite = new Dictionary<uint, double>();
            var targetBestQuality = new Dictionary<uint, double>();
            var decoyComposite = new Dictionary<uint, double>();
            var decoyBestQuality = new Dictionary<uint, double>();

            foreach (var kvp in parsimony.PeptideToGroupMap)
            {
                string peptide = kvp.Key;
                var groupIds = kvp.Value;

                double nPep = 1.0;
                foreach (uint gid in groupIds)
                {
                    int count;
                    if (nPeptidesPerGroup.TryGetValue(gid, out count) && count > nPep)
                        nPep = count;
                }

                // Target peptide contribution
                PeptideScore ps;
                if (bestScores.TryGetValue(peptide, out ps))
                {
                    if (!ps.IsDecoy && ps.BestQvalue <= qvalueGate)
                    {
                        double err = 1.0 / (1.0 + Math.Exp(ps.Score));
                        double quality = Math.Max(0.0, 1.0 - err);
                        double compositeContrib = -Math.Log(Math.Max(EPSILON, Math.Min(1.0, err * nPep)));

                        foreach (uint gid in groupIds)
                        {
                            double existing;
                            if (targetComposite.TryGetValue(gid, out existing))
                                targetComposite[gid] = existing + compositeContrib;
                            else
                                targetComposite[gid] = compositeContrib;

                            double existingQ;
                            if (targetBestQuality.TryGetValue(gid, out existingQ))
                                targetBestQuality[gid] = Math.Max(existingQ, quality);
                            else
                                targetBestQuality[gid] = quality;
                        }
                    }
                }

                // Decoy peptide contribution
                string decoyKey = DECOY_PREFIX + peptide;
                PeptideScore decoyPs;
                if (bestScores.TryGetValue(decoyKey, out decoyPs))
                {
                    if (decoyPs.IsDecoy && decoyPs.BestQvalue <= qvalueGate)
                    {
                        double err = 1.0 / (1.0 + Math.Exp(decoyPs.Score));
                        double quality = Math.Max(0.0, 1.0 - err);
                        double compositeContrib = -Math.Log(Math.Max(EPSILON, Math.Min(1.0, err * nPep)));

                        foreach (uint gid in groupIds)
                        {
                            double existing;
                            if (decoyComposite.TryGetValue(gid, out existing))
                                decoyComposite[gid] = existing + compositeContrib;
                            else
                                decoyComposite[gid] = compositeContrib;

                            double existingQ;
                            if (decoyBestQuality.TryGetValue(gid, out existingQ))
                                decoyBestQuality[gid] = Math.Max(existingQ, quality);
                            else
                                decoyBestQuality[gid] = quality;
                        }
                    }
                }
            }

            // Compute q-values independently on each metric
            var qComposite = ComputeProteinQvaluesDiann(
                parsimony.Groups, targetComposite, decoyComposite);
            var qBest = ComputeProteinQvaluesDiann(
                parsimony.Groups, targetBestQuality, decoyBestQuality);

            // Final q-value = min(q_composite, q_best_quality)
            var groupQvalues = new Dictionary<uint, double>();
            var groupScores = new Dictionary<uint, double>();
            foreach (var group in parsimony.Groups)
            {
                double qc, qb;
                if (!qComposite.TryGetValue(group.Id, out qc))
                    qc = 1.0;
                if (!qBest.TryGetValue(group.Id, out qb))
                    qb = 1.0;
                groupQvalues[group.Id] = Math.Min(qc, qb);

                double compositeScore;
                if (!targetComposite.TryGetValue(group.Id, out compositeScore))
                    compositeScore = 0.0;
                groupScores[group.Id] = compositeScore;
            }

            // Compute protein PEP from the composite score
            var targetScoresVec = new List<double>();
            var decoyScoresVec = new List<double>();
            foreach (var group in parsimony.Groups)
            {
                double s;
                if (targetComposite.TryGetValue(group.Id, out s))
                    targetScoresVec.Add(s);
                if (decoyComposite.TryGetValue(group.Id, out s))
                    decoyScoresVec.Add(s);
            }

            var allScores = new double[targetScoresVec.Count + decoyScoresVec.Count];
            var allIsDecoy = new bool[allScores.Length];
            for (int i = 0; i < targetScoresVec.Count; i++)
            {
                allScores[i] = targetScoresVec[i];
                allIsDecoy[i] = false;
            }
            for (int i = 0; i < decoyScoresVec.Count; i++)
            {
                allScores[targetScoresVec.Count + i] = decoyScoresVec[i];
                allIsDecoy[targetScoresVec.Count + i] = true;
            }

            var pepEstimator = PepEstimator.FitDefault(allScores, allIsDecoy);
            var groupPep = new Dictionary<uint, double>();
            foreach (var group in parsimony.Groups)
            {
                double s;
                if (targetComposite.TryGetValue(group.Id, out s))
                    groupPep[group.Id] = pepEstimator.PosteriorError(s);
            }

            // Propagate to peptides: each peptide gets the best (lowest) protein q-value
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
                GroupPep = groupPep,
                GroupScores = groupScores,
                PeptideQvalues = peptideQvalues
            };
        }

        /// <summary>
        /// Compute protein q-values using DIA-NN-style TDC.
        /// For each target protein with score s:
        ///   q = n_decoys_with_score_gte_s / max(1, n_targets_with_score_gte_s)
        /// </summary>
        private static Dictionary<uint, double> ComputeProteinQvaluesDiann(
            IList<ProteinGroup> groups,
            Dictionary<uint, double> targetScores,
            Dictionary<uint, double> decoyScores)
        {
            var allTarget = new List<double>(targetScores.Values);
            var allDecoy = new List<double>(decoyScores.Values);
            allTarget.Sort();
            allDecoy.Sort();

            int nTargetsWith = allTarget.Count;
            int nDecoysWith = allDecoy.Count;

            // For each target protein, compute raw FDR
            var rawQvals = new List<Tuple<double, double, uint>>(); // (score, raw_q, gid)
            foreach (var group in groups)
            {
                double score;
                if (!targetScores.TryGetValue(group.Id, out score) || score <= 0.0)
                    continue;

                int dPos = BinarySearchLeft(allDecoy, score);
                int nDecoysGe = nDecoysWith - dPos;

                int tPos = BinarySearchLeft(allTarget, score);
                int nTargetsGe = nTargetsWith - tPos;

                double q = nTargetsGe > 0
                    ? Math.Min(1.0, (double)nDecoysGe / nTargetsGe)
                    : 1.0;

                rawQvals.Add(Tuple.Create(score, q, group.Id));
            }

            // Sort by score ascending for backward sweep
            rawQvals.Sort((a, b) => a.Item1.CompareTo(b.Item1));

            // Backward sweep: enforce monotonicity
            double minQ = 1.0;
            var result = new Dictionary<uint, double>();
            for (int i = rawQvals.Count - 1; i >= 0; i--)
            {
                minQ = Math.Min(minQ, rawQvals[i].Item2);
                result[rawQvals[i].Item3] = minQ;
            }

            return result;
        }

        /// <summary>
        /// Binary search: find first index where list[index] >= value.
        /// </summary>
        private static int BinarySearchLeft(List<double> sortedList, double value)
        {
            int lo = 0;
            int hi = sortedList.Count;
            while (lo < hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (sortedList[mid] < value)
                    lo = mid + 1;
                else
                    hi = mid;
            }
            return lo;
        }

        /// <summary>
        /// Collect peptide-level scores for protein FDR.
        ///
        /// For each unique modified_sequence, keeps the best SVM score and best q-value
        /// across all files.
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
                        if (entry.RunPrecursorQvalue < ps.BestQvalue)
                            ps.BestQvalue = entry.RunPrecursorQvalue;
                    }
                    else
                    {
                        best[entry.ModifiedSequence] = new PeptideScore
                        {
                            Score = entry.Score,
                            IsDecoy = entry.IsDecoy,
                            BestQvalue = entry.RunPrecursorQvalue
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
