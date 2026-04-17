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

// Tests for OspreySharp.FDR module
// Ported from Rust test suite in osprey-fdr

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.FDR;

namespace pwiz.OspreySharp.Test
{
    [TestClass]
    public class FdrTest
    {
        // ============================================================
        // FdrController: CompeteAndFilter tests
        // ============================================================

        #region CompeteAndFilter Tests

        [TestMethod]
        public void TestCompetitionTargetWins()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("target_1", 0.9, false, 1),
                new CompetitionItem("decoy_1", 0.7, true, 1 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(1, result.NTargetWins);
            Assert.AreEqual(0, result.NDecoyWins);
            Assert.AreEqual(1, result.PassingTargets.Count);
            Assert.AreEqual("target_1", result.PassingTargets[0].Name);
        }

        [TestMethod]
        public void TestCompetitionDecoyWins()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("target_1", 0.5, false, 1),
                new CompetitionItem("decoy_1", 0.8, true, 1 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(0, result.NTargetWins);
            Assert.AreEqual(1, result.NDecoyWins);
            Assert.AreEqual(0, result.PassingTargets.Count);
        }

        [TestMethod]
        public void TestCompetitionTieGoesToDecoy()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("target_1", 0.75, false, 1),
                new CompetitionItem("decoy_1", 0.75, true, 1 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(0, result.NTargetWins);
            Assert.AreEqual(1, result.NDecoyWins);
            Assert.AreEqual(0, result.PassingTargets.Count);
        }

        [TestMethod]
        public void TestCompetitionTargetWithoutDecoy()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("t1", 0.5, false, 1)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(1, result.NTargetWins);
            Assert.AreEqual(0, result.NDecoyWins);
            Assert.AreEqual(1, result.PassingTargets.Count);
        }

        [TestMethod]
        public void TestCompetitionAllDecoysWin()
        {
            var controller = new FdrController(0.01);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("t1", 0.1, false, 1),
                new CompetitionItem("d1", 0.9, true, 1 | 0x80000000),
                new CompetitionItem("t2", 0.2, false, 2),
                new CompetitionItem("d2", 0.8, true, 2 | 0x80000000),
                new CompetitionItem("t3", 0.3, false, 3),
                new CompetitionItem("d3", 0.7, true, 3 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(0, result.NTargetWins);
            Assert.AreEqual(3, result.NDecoyWins);
            Assert.AreEqual(0, result.PassingTargets.Count);
        }

        [TestMethod]
        public void TestCompetitionMultiplePairsAllTargetsWin()
        {
            var controller = new FdrController(0.01);
            var matches = new List<CompetitionItem>
            {
                new CompetitionItem("t1", 0.9, false, 1),
                new CompetitionItem("d1", 0.1, true, 1 | 0x80000000),
                new CompetitionItem("t2", 0.8, false, 2),
                new CompetitionItem("d2", 0.2, true, 2 | 0x80000000),
                new CompetitionItem("t3", 0.7, false, 3),
                new CompetitionItem("d3", 0.3, true, 3 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(3, result.NTargetWins);
            Assert.AreEqual(0, result.NDecoyWins);
            Assert.AreEqual(3, result.PassingTargets.Count);
            Assert.IsTrue(result.FdrAtThreshold < 0.001);
        }

        [TestMethod]
        public void TestCompetitionFdrCalculation()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                // Pair 1: target wins with score 0.95
                new CompetitionItem("target_1", 0.95, false, 1),
                new CompetitionItem("decoy_1", 0.50, true, 1 | 0x80000000),
                // Pair 2: target wins with score 0.90
                new CompetitionItem("target_2", 0.90, false, 2),
                new CompetitionItem("decoy_2", 0.40, true, 2 | 0x80000000),
                // Pair 3: target wins with score 0.85
                new CompetitionItem("target_3", 0.85, false, 3),
                new CompetitionItem("decoy_3", 0.30, true, 3 | 0x80000000),
                // Pair 4: target wins with score 0.80
                new CompetitionItem("target_4", 0.80, false, 4),
                new CompetitionItem("decoy_4", 0.20, true, 4 | 0x80000000),
                // Pair 5: DECOY wins with score 0.82
                new CompetitionItem("target_5", 0.75, false, 5),
                new CompetitionItem("decoy_5", 0.82, true, 5 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            // Winners sorted: 0.95(T), 0.90(T), 0.85(T), 0.82(D), 0.80(T)
            // Walk: 1T,0D->0%, 2T,0D->0%, 3T,0D->0%, 3T,1D->33%, 4T,1D->25%
            // Only 3 targets pass at 10% FDR
            Assert.AreEqual(4, result.NTargetWins);
            Assert.AreEqual(1, result.NDecoyWins);
            Assert.AreEqual(3, result.PassingTargets.Count);
        }

        [TestMethod]
        public void TestCompetitionFdrRecoversAfterSpike()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>();

            // Pair 1: target wins with score 0.99
            matches.Add(new CompetitionItem("target_1", 0.99, false, 1));
            matches.Add(new CompetitionItem("decoy_1", 0.10, true, 1 | 0x80000000));
            // Pair 2: target wins with score 0.98
            matches.Add(new CompetitionItem("target_2", 0.98, false, 2));
            matches.Add(new CompetitionItem("decoy_2", 0.10, true, 2 | 0x80000000));
            // Pair 3: DECOY wins with score 0.97
            matches.Add(new CompetitionItem("target_3", 0.50, false, 3));
            matches.Add(new CompetitionItem("decoy_3", 0.97, true, 3 | 0x80000000));

            // Add 10 more target winners with scores 0.90 down to 0.81
            for (uint i = 4; i <= 13; i++)
            {
                double score = 0.90 - (i - 4) * 0.01;
                matches.Add(new CompetitionItem("target_" + i, score, false, i));
                matches.Add(new CompetitionItem("decoy_" + i, 0.05, true, i | 0x80000000));
            }

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(12, result.NTargetWins);
            Assert.AreEqual(1, result.NDecoyWins);
            // FDR recovers after spike: 12 targets pass
            Assert.AreEqual(12, result.PassingTargets.Count);
            Assert.IsTrue(result.FdrAtThreshold <= 0.10);
        }

        [TestMethod]
        public void TestCompetitionKeepsBestScorePerPeptide()
        {
            var controller = new FdrController(0.10);
            var matches = new List<CompetitionItem>
            {
                // Target 1 scored twice - keep best (0.9)
                new CompetitionItem("t1_v1", 0.7, false, 1),
                new CompetitionItem("t1_v2", 0.9, false, 1),
                // Decoy 1 scored twice - keep best (0.5)
                new CompetitionItem("d1_v1", 0.3, true, 1 | 0x80000000),
                new CompetitionItem("d1_v2", 0.5, true, 1 | 0x80000000)
            };

            var result = controller.CompeteAndFilter(
                matches, m => m.Score, m => m.IsDecoy, m => m.EntryId);

            Assert.AreEqual(1, result.NTargetWins);
            Assert.AreEqual(0, result.NDecoyWins);
            Assert.AreEqual(1, result.PassingTargets.Count);
            Assert.AreEqual("t1_v2", result.PassingTargets[0].Name);
        }

        #endregion

        // ============================================================
        // FdrController: FilterByQvalue and CountAtThresholds tests
        // ============================================================

        #region FilterByQvalue and CountAtThresholds Tests

        [TestMethod]
        public void TestFilterByQvalue()
        {
            var controller = new FdrController(0.05);
            var items = new List<string> { "a", "b", "c", "d" };
            var qvalues = new List<double> { 0.01, 0.03, 0.10, 0.20 };

            var filtered = controller.FilterByQvalue(items, qvalues);

            Assert.AreEqual(2, filtered.Count);
            Assert.AreEqual("a", filtered[0]);
            Assert.AreEqual("b", filtered[1]);
        }

        [TestMethod]
        public void TestCountAtThresholds()
        {
            var controller = new FdrController(0.01);
            var qvalues = new List<double> { 0.001, 0.005, 0.02, 0.03, 0.08, 0.15 };

            var counts = controller.CountAtThresholds(qvalues);

            Assert.AreEqual(1, counts.At001);
            Assert.AreEqual(2, counts.At01);
            Assert.AreEqual(4, counts.At05);
            Assert.AreEqual(5, counts.At10);
            Assert.AreEqual(6, counts.Total);
        }

        #endregion

        // ============================================================
        // Percolator tests
        // ============================================================

        #region Percolator Tests

        [TestMethod]
        public void TestPercolatorBasic()
        {
            var entries = new List<PercolatorEntry>();

            // 20 targets with high feature values
            for (int i = 0; i < 20; i++)
            {
                entries.Add(MakePercolatorEntry(
                    string.Format("file1_{0}", i), "file1",
                    string.Format("PEPTIDE{0}", i), 2, false, (uint)(i + 1),
                    new[] { 4.0 + i * 0.1, 5.0 - i * 0.05 }));
            }

            // 20 paired decoys with low feature values
            for (int i = 0; i < 20; i++)
            {
                entries.Add(MakePercolatorEntry(
                    string.Format("file1_d{0}", i), "file1",
                    string.Format("DECOY{0}", i), 2, true, (uint)(i + 1) | 0x80000000,
                    new[] { 0.5 + i * 0.05, 1.0 + i * 0.02 }));
            }

            var config = new PercolatorConfig { MaxIterations = 3 };
            var results = PercolatorFdr.RunPercolator(entries, config);

            // Should have results for all entries
            Assert.AreEqual(40, results.Entries.Count);

            // Targets should generally have higher scores than decoys
            double avgTarget = 0.0;
            double avgDecoy = 0.0;
            int nTarget = 0;
            int nDecoy = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (!entries[i].IsDecoy)
                {
                    avgTarget += results.Entries[i].Score;
                    nTarget++;
                }
                else
                {
                    avgDecoy += results.Entries[i].Score;
                    nDecoy++;
                }
            }
            avgTarget /= nTarget;
            avgDecoy /= nDecoy;
            Assert.IsTrue(avgTarget > avgDecoy,
                string.Format("avg_target={0} should be > avg_decoy={1}", avgTarget, avgDecoy));

            // Should have fold weights
            Assert.AreEqual(3, results.FoldWeights.Count);
        }

        [TestMethod]
        public void TestPercolatorEmpty()
        {
            var config = new PercolatorConfig();
            var results = PercolatorFdr.RunPercolator(new List<PercolatorEntry>(), config);
            Assert.AreEqual(0, results.Entries.Count);
        }

        [TestMethod]
        public void TestFoldAssignmentPeptideGrouping()
        {
            var labels = new[] { false, false, false, true, true, true };
            var peptides = new[]
            {
                "PEPTIDEK", "PEPTIDEK", "ANOTHERONE",
                "KEDITPEP", "KEDITPEP", "ENOREHTONA"
            };
            var entryIds = new uint[]
            {
                1, 2, 3,
                1 | 0x80000000, 2 | 0x80000000, 3 | 0x80000000
            };

            var folds = PercolatorFdr.CreateStratifiedFoldsByPeptide(
                labels, peptides, entryIds, 3);

            // All charge states of PEPTIDEK (targets) should be in same fold
            Assert.AreEqual(folds[0], folds[1], "Same peptide targets should share fold");

            // Target-decoy pairs must be in the same fold
            Assert.AreEqual(folds[0], folds[3],
                "Target PEPTIDEK z2 and its decoy must share fold");
            Assert.AreEqual(folds[1], folds[4],
                "Target PEPTIDEK z3 and its decoy must share fold");
            Assert.AreEqual(folds[2], folds[5],
                "Target ANOTHERONE and its decoy must share fold");

            // All PEPTIDEK entries in same fold
            Assert.AreEqual(folds[0], folds[4], "All PEPTIDEK entries should share fold");
        }

        [TestMethod]
        public void TestSubsampleKeepsTargetDecoyPairs()
        {
            var labels = new List<bool>();
            var entryIds = new List<uint>();
            var peptides = new List<string>();
            for (uint i = 1; i <= 10; i++)
            {
                labels.Add(false);
                entryIds.Add(i);
                peptides.Add("PEPTIDE" + i);
                labels.Add(true);
                entryIds.Add(i | 0x80000000);
                peptides.Add("DECOY" + i);
            }

            var selected = PercolatorFdr.SubsampleByPeptideGroup(
                labels.ToArray(), entryIds.ToArray(), peptides.ToArray(), 10, 42);

            var selectedSet = new HashSet<int>(selected);

            // Every selected target must have its paired decoy also selected
            foreach (int idx in selected)
            {
                uint baseId = entryIds[idx] & 0x7FFFFFFF;
                bool isDecoy = labels[idx];

                int pairedIdx = -1;
                for (int j = 0; j < labels.Count; j++)
                {
                    if (isDecoy)
                    {
                        if (!labels[j] && entryIds[j] == baseId)
                        {
                            pairedIdx = j;
                            break;
                        }
                    }
                    else
                    {
                        if (labels[j] && (entryIds[j] & 0x7FFFFFFF) == baseId)
                        {
                            pairedIdx = j;
                            break;
                        }
                    }
                }

                if (pairedIdx >= 0)
                {
                    Assert.IsTrue(selectedSet.Contains(pairedIdx),
                        string.Format(
                            "Entry at idx {0} (base_id={1}, decoy={2}) selected but paired entry at idx {3} is missing",
                            idx, baseId, isDecoy, pairedIdx));
                }
            }
        }

        [TestMethod]
        public void TestBestPrecursorPerPeptide()
        {
            var indices = new[] { 0, 1, 2, 3, 4 };
            var scores = new[] { 5.0, 3.0, 7.0, 2.0, 6.0 };
            var labels = new[] { false, false, false, true, true };
            var peptides = new[] { "PEPK", "PEPK", "OTHER", "PEPK", "OTHER" };

            var best = PercolatorFdr.BestPrecursorPerPeptide(indices, scores, labels, peptides);

            // Should have one entry per unique peptide string
            Assert.AreEqual(2, best.Length);
            Assert.IsTrue(best.Contains(0), "PEPK best should be index 0 (score 5.0)");
            Assert.IsTrue(best.Contains(2), "OTHER best should be index 2 (score 7.0)");
        }

        [TestMethod]
        public void TestConservativeQvalues()
        {
            // 3 targets, 1 decoy (already sorted by score desc)
            var scores = new[] { 10.0, 9.0, 8.0, 7.0 };
            var isDecoy = new[] { false, false, true, false };
            var q = new double[4];

            PercolatorFdr.ComputeConservativeQvalues(scores, isDecoy, q);

            // FDR with +1: pos0: (0+1)/1=1.0, pos1: (0+1)/2=0.5, pos2: (1+1)/2=1.0, pos3: (1+1)/3=0.667
            // Backward pass: [0.5, 0.5, 0.667, 0.667]
            Assert.AreEqual(0.5, q[0], 1e-10, "q[0]");
            Assert.AreEqual(0.5, q[1], 1e-10, "q[1]");
            Assert.AreEqual(2.0 / 3.0, q[2], 1e-10, "q[2]");
            Assert.AreEqual(2.0 / 3.0, q[3], 1e-10, "q[3]");
        }

        [TestMethod]
        public void TestCompeteAndCount()
        {
            // 5 targets easily beat 5 decoys
            var scores = new[] { 10.0, 9.0, 8.0, 7.0, 6.0, 1.0, 0.5, 0.2, 0.1, 0.05 };
            var labels = new[]
            {
                false, false, false, false, false, true, true, true, true, true
            };
            var entryIds = new uint[]
            {
                1, 2, 3, 4, 5,
                1 | 0x80000000, 2 | 0x80000000, 3 | 0x80000000, 4 | 0x80000000, 5 | 0x80000000
            };

            int n = PercolatorFdr.CountPassingConservative(scores, labels, entryIds, 0.50);
            Assert.AreEqual(5, n);
        }

        #endregion

        // ============================================================
        // Protein FDR tests
        // ============================================================

        #region Protein FDR Tests

        [TestMethod]
        public void TestBasicParsimonyGrouping()
        {
            // Three proteins: P1 has {A, B, C}, P2 has {A, B, C} (identical), P3 has {D, E}
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1", "P2" }, false),
                MakeLibEntry(2, "PEPTIDEB", new[] { "P1", "P2" }, false),
                MakeLibEntry(3, "PEPTIDEC", new[] { "P1", "P2" }, false),
                MakeLibEntry(4, "PEPTIDED", new[] { "P3" }, false),
                MakeLibEntry(5, "PEPTIDEE", new[] { "P3" }, false)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.All, null);

            // P1 and P2 should be merged into one group
            Assert.AreEqual(2, result.Groups.Count);

            // One group has 3 peptides, other has 2
            var sizes = result.Groups
                .Select(g => g.UniquePeptides.Count + g.SharedPeptides.Count)
                .OrderBy(x => x)
                .ToList();
            Assert.AreEqual(2, sizes[0]);
            Assert.AreEqual(3, sizes[1]);
        }

        [TestMethod]
        public void TestSubsetElimination()
        {
            // P1 has {A, B, C}, P2 has {A, B} (subset of P1) -> P2 eliminated
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1", "P2" }, false),
                MakeLibEntry(2, "PEPTIDEB", new[] { "P1", "P2" }, false),
                MakeLibEntry(3, "PEPTIDEC", new[] { "P1" }, false)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.All, null);

            Assert.AreEqual(1, result.Groups.Count);
            Assert.IsTrue(result.Groups[0].Accessions.Contains("P1"));
        }

        [TestMethod]
        public void TestSharedPeptidesAllMode()
        {
            // P1 has {A, B, shared}, P2 has {C, D, shared}
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1" }, false),
                MakeLibEntry(2, "PEPTIDEB", new[] { "P1" }, false),
                MakeLibEntry(3, "SHARED", new[] { "P1", "P2" }, false),
                MakeLibEntry(4, "PEPTIDEC", new[] { "P2" }, false),
                MakeLibEntry(5, "PEPTIDED", new[] { "P2" }, false)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.All, null);

            Assert.AreEqual(2, result.Groups.Count);
            Assert.AreEqual(2, result.PeptideToGroupMap["SHARED"].Count);
        }

        [TestMethod]
        public void TestSharedPeptidesRazorMode()
        {
            // P1 has {A, B, C, shared}, P2 has {D, shared}
            // P1 has more unique peptides, so SHARED should be assigned to P1
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1" }, false),
                MakeLibEntry(2, "PEPTIDEB", new[] { "P1" }, false),
                MakeLibEntry(3, "PEPTIDEC", new[] { "P1" }, false),
                MakeLibEntry(4, "SHARED", new[] { "P1", "P2" }, false),
                MakeLibEntry(5, "PEPTIDED", new[] { "P2" }, false)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.Razor, null);

            Assert.AreEqual(2, result.Groups.Count);
            // SHARED should map to only one group
            Assert.AreEqual(1, result.PeptideToGroupMap["SHARED"].Count);

            // The group with P1 should have SHARED as unique now
            var p1Group = result.Groups.First(g => g.Accessions.Contains("P1"));
            Assert.IsTrue(p1Group.UniquePeptides.Contains("SHARED"));
            Assert.AreEqual(0, p1Group.SharedPeptides.Count);
        }

        [TestMethod]
        public void TestSharedPeptidesUniqueMode()
        {
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1" }, false),
                MakeLibEntry(2, "SHARED", new[] { "P1", "P2" }, false),
                MakeLibEntry(3, "PEPTIDEC", new[] { "P2" }, false)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.Unique, null);

            // SHARED should not be in the mapping
            Assert.IsFalse(result.PeptideToGroupMap.ContainsKey("SHARED"));
        }

        [TestMethod]
        public void TestPickedProteinFdr()
        {
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1" }, false),
                MakeLibEntry(2, "PEPTIDEB", new[] { "P2" }, false)
            };

            var parsimony = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.All, null);

            var bestScores = new Dictionary<string, PeptideScore>
            {
                ["PEPTIDEA"] = new PeptideScore { Score = 5.0, IsDecoy = false, BestQvalue = 0.001 },
                ["DECOY_PEPTIDEA"] = new PeptideScore { Score = 2.0, IsDecoy = true, BestQvalue = 0.5 },
                ["PEPTIDEB"] = new PeptideScore { Score = 1.0, IsDecoy = false, BestQvalue = 0.005 },
                ["DECOY_PEPTIDEB"] = new PeptideScore { Score = 3.0, IsDecoy = true, BestQvalue = 0.3 }
            };

            var fdrResult = ProteinFdr.ComputeProteinFdr(parsimony, bestScores, 1.0);

            var p1Group = parsimony.Groups.First(g => g.Accessions.Contains("P1"));
            Assert.IsTrue(fdrResult.GroupQvalues.ContainsKey(p1Group.Id));

            // PEPTIDEA should have a protein q-value
            Assert.IsTrue(fdrResult.PeptideQvalues.ContainsKey("PEPTIDEA"));
        }

        [TestMethod]
        public void TestDecoyEntriesExcludedFromParsimony()
        {
            var library = new List<LibraryEntry>
            {
                MakeLibEntry(1, "PEPTIDEA", new[] { "P1" }, false),
                MakeLibEntry(2, "DECOY_PEPTIDEA", new[] { "DECOY_P1" }, true)
            };

            var result = ProteinFdr.BuildProteinParsimony(library, SharedPeptideMode.All, null);

            Assert.AreEqual(1, result.Groups.Count);
            CollectionAssert.AreEqual(new[] { "P1" }, result.Groups[0].Accessions);
        }

        [TestMethod]
        public void TestCollectBestPeptideScoresUsesSvmScore()
        {
            var entries = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("file1", new List<FdrEntry>
                {
                    new FdrEntry
                    {
                        EntryId = 1, IsDecoy = false, Charge = 2,
                        CoelutionSum = 5.0, Score = 3.0,
                        RunPrecursorQvalue = 0.001,
                        ModifiedSequence = "PEPTIDEA"
                    },
                    new FdrEntry
                    {
                        EntryId = 1, IsDecoy = false, Charge = 2,
                        CoelutionSum = 6.0, Score = 5.0,
                        RunPrecursorQvalue = 0.05,
                        ModifiedSequence = "PEPTIDEA"
                    }
                })
            };

            var best = ProteinFdr.CollectBestPeptideScores(entries);

            // Best is the one with higher SVM score (5.0), not higher coelution_sum
            Assert.AreEqual(5.0, best["PEPTIDEA"].Score, 1e-10);
            Assert.IsFalse(best["PEPTIDEA"].IsDecoy);
        }

        #endregion

        // ============================================================
        // Test helpers
        // ============================================================

        private class CompetitionItem
        {
            public string Name { get; set; }
            public double Score { get; set; }
            public bool IsDecoy { get; set; }
            public uint EntryId { get; set; }

            public CompetitionItem(string name, double score, bool isDecoy, uint entryId)
            {
                Name = name;
                Score = score;
                IsDecoy = isDecoy;
                EntryId = entryId;
            }
        }

        private static PercolatorEntry MakePercolatorEntry(
            string id, string file, string peptide, byte charge,
            bool isDecoy, uint entryId, double[] features)
        {
            return new PercolatorEntry
            {
                Id = id,
                FileName = file,
                Peptide = peptide,
                Charge = charge,
                IsDecoy = isDecoy,
                EntryId = entryId,
                Features = features
            };
        }

        private static LibraryEntry MakeLibEntry(
            uint id, string modifiedSequence, string[] proteinIds, bool isDecoy)
        {
            return new LibraryEntry(id, modifiedSequence, modifiedSequence, 2, 500.0, 30.0)
            {
                ProteinIds = new List<string>(proteinIds),
                IsDecoy = isDecoy
            };
        }
    }
}
