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

// Tests for Osprey.FDR module
// Ported from Rust test suite in osprey-fdr

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;

namespace pwiz.Osprey.Test
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
                    "file1", string.Format("PEPTIDE{0}", i), 2, false, (uint)(i + 1),
                    new[] { 4.0 + i * 0.1, 5.0 - i * 0.05 }));
            }

            // 20 paired decoys with low feature values
            for (int i = 0; i < 20; i++)
            {
                entries.Add(MakePercolatorEntry(
                    "file1", string.Format("DECOY{0}", i), 2, true, (uint)(i + 1) | 0x80000000,
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

        /// <summary>
        /// The index-zip result write-back
        /// (<see cref="PercolatorEngine.ApplyPercolatorResults"/>) must place each
        /// PercolatorResult onto its own FdrEntry stub, byte-identical to the
        /// psm_id-keyed resultMap re-join it replaced (issue #4355 step (b)). Both
        /// SVM paths return results index-aligned to the PercolatorEntry input, which
        /// the builder emits one-per-stub in nested (file, entry) order, so the
        /// write-back is a pure positional zip. This drives synthetic index-aligned
        /// results through both strategies on a multi-file fixture and asserts
        /// identical Score + five q-values. The write-back is oblivious to which SVM
        /// path produced the results, so this single order-agnostic check covers the
        /// direct and streaming result orderings alike.
        /// </summary>
        [TestMethod]
        public void TestApplyPercolatorResultsIndexZipMatchesPsmIdMap()
        {
            var indexZipStubs = BuildWritebackFixture();
            var psmIdMapStubs = BuildWritebackFixture();

            // Synthetic per-stub results, index-aligned to the nested (file, entry)
            // walk -- the exact contract PercolatorFdr's direct and streaming result
            // assembly guarantee. Distinct values per row so any misbinding surfaces.
            var results = new PercolatorResults { Entries = new List<PercolatorResult>() };
            int seq = 0;
            foreach (var kvp in indexZipStubs)
            {
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    results.Entries.Add(new PercolatorResult
                    {
                        Score = 100.0 + seq,
                        RunPrecursorQvalue = 0.100 + seq * 0.001,
                        RunPeptideQvalue = 0.200 + seq * 0.001,
                        ExperimentPrecursorQvalue = 0.300 + seq * 0.001,
                        ExperimentPeptideQvalue = 0.400 + seq * 0.001,
                        Pep = 0.500 + seq * 0.001
                    });
                    seq++;
                }
            }

            // Production write-back: the positional index zip under test.
            PercolatorEngine.ApplyPercolatorResults(indexZipStubs, results);

            // Oracle: the removed psm_id map re-join, reconstructed on an independent
            // copy of the same fixture and the same results.
            ApplyLegacyPsmIdWriteback(psmIdMapStubs, results);

            // Every stub must carry identical Score + q-values under both strategies.
            AssertWritebackEqual(indexZipStubs, psmIdMapStubs);
        }

        /// <summary>
        /// Streaming-path coverage for the index-zip write-back (issue #4355 step
        /// (b), risk #5). The direct Percolator path (which Stellar exercises) and
        /// the streaming path (which the >600K-entry / many-file production workload
        /// uses) build their result lists in separate loops; the write-back's
        /// correctness rests on BOTH returning results index-aligned to the flat
        /// PercolatorEntry input. This drives the REAL streaming assembler
        /// (<see cref="PercolatorFdr.ScorePopulationAndComputeFdr"/>, the loop the
        /// design cites for the streaming path) end-to-end on a small paired
        /// target/decoy fixture, then applies the index zip and the removed psm_id
        /// map to those real streaming-produced results and asserts identical Score
        /// + five q-values. Driving it with resident features exercises the exact
        /// streaming result-assembly order; only the feature source (per-file reload
        /// vs resident) differs at scale, never the row order.
        /// </summary>
        [TestMethod]
        public void TestApplyPercolatorResultsStreamingOrderIndexZipMatchesPsmIdMap()
        {
            const int nFeat = 3;
            var featureInfos = new[]
            {
                new OspreyFeatureInfo("feat_a", "Feature A", false),
                new OspreyFeatureInfo("feat_b", "Feature B", false),
                new OspreyFeatureInfo("feat_c", "Feature C", false)
            };

            var indexZipStubs = BuildStreamingWritebackFixture(nFeat);
            var psmIdMapStubs = BuildStreamingWritebackFixture(nFeat);

            // Flat PercolatorEntry list in the nested (file, entry) order the write-
            // back walks. All rows carry a resident nFeat-vector.
            var built = PercolatorEntryBuilder.Build(
                indexZipStubs, nFeat, streamFeatures: false,
                out int nWith, out int nWithout, out int nTargets, out int nDecoys);
            Assert.AreEqual(120, built.Count);
            Assert.AreEqual(120, nWith);
            Assert.AreEqual(0, nWithout);
            Assert.AreEqual(60, nTargets);
            Assert.AreEqual(60, nDecoys);

            // Train a model, then score the whole population through the streaming
            // assembler (this is the path the direct branch does NOT take).
            var config = new PercolatorConfig { MaxIterations = 3, FeatureInfos = featureInfos };
            PercolatorResults trainResults = PercolatorFdr.RunPercolator(built, config);
            PercolatorResults streamingResults =
                PercolatorFdr.ScorePopulationAndComputeFdr(built, trainResults, config);

            // The streaming assembler must return one result per entry, in order.
            Assert.AreEqual(built.Count, streamingResults.Entries.Count);

            // Index zip (production) vs the removed psm_id map (oracle), both applied
            // to these real streaming-assembled results.
            PercolatorEngine.ApplyPercolatorResults(indexZipStubs, streamingResults);
            ApplyLegacyPsmIdWriteback(psmIdMapStubs, streamingResults);
            AssertWritebackEqual(indexZipStubs, psmIdMapStubs);
        }

        /// <summary>
        /// Byte-identity risk #1 (issue #4355 step (b) increment ii, the single
        /// highest risk): <see cref="FdrProjectionSet.BuildFromEntries"/> must assign
        /// <see cref="FdrProjection.PeptideId"/> in <see cref="StringComparison.Ordinal"/>
        /// order of the DISTINCT modified sequences, so grouping/sorting by peptide_id
        /// reproduces the ordinal string ordering the training subsample
        /// (<c>SubsampleByPeptideGroup</c>) depends on. Uses mixed-case sequences so a
        /// case-insensitive comparer would produce a different (wrong) order,
        /// pinning that the assignment is case-sensitive Ordinal.
        /// </summary>
        [TestMethod]
        public void TestFdrProjectionPeptideIdOrdinalInvariant()
        {
            // Insertion order deliberately NOT ordinal, with duplicates across files.
            // Ordinal order of the distinct set is: "AAA" < "B" < "b" < "peptide"
            // (uppercase 'B'=66 sorts before lowercase 'b'=98, before 'p'=112).
            var seqs = new[] { "peptide", "B", "AAA", "b", "AAA", "peptide" };
            var fileA = new List<FdrEntry>();
            for (int i = 0; i < seqs.Length; i++)
            {
                fileA.Add(new FdrEntry
                {
                    EntryId = (uint)(i + 1),
                    ModifiedSequence = seqs[i],
                    Charge = 2,
                    IsDecoy = false
                });
            }
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("fileA", fileA)
            };

            var set = FdrProjectionSet.BuildFromEntries(perFile);

            // Distinct table is strictly Ordinal-ascending and matches the expected order.
            CollectionAssert.AreEqual(
                new[] { "AAA", "B", "b", "peptide" }, set.PeptideById);
            for (int i = 1; i < set.PeptideById.Length; i++)
            {
                Assert.IsTrue(
                    string.CompareOrdinal(set.PeptideById[i - 1], set.PeptideById[i]) < 0,
                    "PeptideById must be strictly Ordinal-ascending and de-duplicated");
            }

            // Each row's peptide_id resolves back to its own modified sequence, and the
            // id ordering reproduces the ordinal string ordering (id_a < id_b iff
            // Ordinal(seq_a, seq_b) < 0) -- the invariant the subsample relies on.
            var rows = set.PerFile[0].Value;
            Assert.AreEqual(fileA.Count, rows.Count);
            for (int i = 0; i < rows.Count; i++)
                Assert.AreEqual(seqs[i], set.PeptideById[rows[i].PeptideId]);
            for (int a = 0; a < rows.Count; a++)
            {
                for (int b = 0; b < rows.Count; b++)
                {
                    int idCmp = rows[a].PeptideId.CompareTo(rows[b].PeptideId);
                    int ordCmp = string.CompareOrdinal(seqs[a], seqs[b]);
                    Assert.AreEqual(Math.Sign(ordCmp), Math.Sign(idCmp),
                        string.Format("peptide_id order must track Ordinal for '{0}' vs '{1}'",
                            seqs[a], seqs[b]));
                }
            }
        }

        /// <summary>
        /// Memory-regression guard (issue #4355 struct-shrink S0/S1). FdrProjection is the
        /// resident peak buffer over the whole pre-compaction population, so its width
        /// multiplies by ~189M rows at 400 files -- the entire (iv) memory win depends on it
        /// staying the lean 8-field SVM buffer (identity/drive + CoelutionSum + Score), with
        /// the six q-value OUTPUTS split off into the per-pass IFdrOutputSink. Re-adding a
        /// q-value (or any) field here stays byte-identical -- every FDR gate passes -- but
        /// SILENTLY regresses the resident buffer back toward 80 B. This fails loudly instead:
        /// if it fires, route the new output through the sink, not onto the struct.
        /// </summary>
        [TestMethod]
        public void TestFdrProjectionStructStaysLean()
        {
            var fieldNames = typeof(FdrProjection)
                .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEqual(
                new[] { "Charge", "CoelutionSum", "EntryId", "FileIdx", "IsDecoy", "ParquetIndex", "PeptideId", "Score" },
                fieldNames,
                "FdrProjection's fields changed -- a q-value OUTPUT field was likely re-added onto the " +
                "resident peak buffer. That stays byte-identical but regresses the memory win toward 80 B. " +
                "Route new outputs through the IFdrOutputSink (FdrProjectionOutput.cs), not the struct (#4355 S0/S1).");
        }

        /// <summary>
        /// Memory-regression guard (issue #4355 increment B). The 1st pass builds the
        /// projection with <c>releaseStubs:true</c> so each file's FdrEntry stub list is
        /// cleared the instant its rows are built -- the full projection never coexists with
        /// the full stub buffer (kills the "projection built" spike). Asserts the release
        /// actually happens (input lists emptied) AND is byte-neutral: the projection is
        /// field-identical whether built with <c>releaseStubs</c> true or false.
        /// </summary>
        [TestMethod]
        public void TestBuildFromEntriesReleaseStubsClearsInputAndIsByteNeutral()
        {
            List<KeyValuePair<string, List<FdrEntry>>> MakeInput() =>
                new List<KeyValuePair<string, List<FdrEntry>>>
                {
                    new KeyValuePair<string, List<FdrEntry>>("fileA", new List<FdrEntry>
                    {
                        new FdrEntry { EntryId = 1u, ParquetIndex = 0u, ModifiedSequence = "PEPTIDE",
                            Charge = 2, IsDecoy = false, CoelutionSum = 1.5, Score = 0.1 },
                        new FdrEntry { EntryId = 2u, ParquetIndex = 1u, ModifiedSequence = "AAA",
                            Charge = 3, IsDecoy = true, CoelutionSum = 2.5, Score = 0.2 },
                    }),
                    new KeyValuePair<string, List<FdrEntry>>("fileB", new List<FdrEntry>
                    {
                        new FdrEntry { EntryId = 3u, ParquetIndex = 0u, ModifiedSequence = "PEPTIDE",
                            Charge = 2, IsDecoy = false, CoelutionSum = 3.5, Score = 0.3 },
                    }),
                };

            var keep = MakeInput();
            var released = MakeInput();
            var setKeep = FdrProjectionSet.BuildFromEntries(keep, releaseStubs: false);
            var setReleased = FdrProjectionSet.BuildFromEntries(released, releaseStubs: true);

            Assert.IsTrue(keep.TrueForAll(kvp => kvp.Value.Count > 0),
                "releaseStubs:false must NOT clear the input stubs");
            Assert.IsTrue(released.TrueForAll(kvp => kvp.Value.Count == 0),
                "releaseStubs:true must clear every file's FdrEntry stub list (issue #4355 B).");

            CollectionAssert.AreEqual(setKeep.PeptideById, setReleased.PeptideById);
            Assert.AreEqual(setKeep.PerFile.Count, setReleased.PerFile.Count);
            for (int f = 0; f < setKeep.PerFile.Count; f++)
            {
                var rk = setKeep.PerFile[f].Value;
                var rr = setReleased.PerFile[f].Value;
                Assert.AreEqual(rk.Count, rr.Count);
                for (int i = 0; i < rk.Count; i++)
                {
                    Assert.AreEqual(rk[i].EntryId, rr[i].EntryId);
                    Assert.AreEqual(rk[i].ParquetIndex, rr[i].ParquetIndex);
                    Assert.AreEqual(rk[i].PeptideId, rr[i].PeptideId);
                    Assert.AreEqual(rk[i].FileIdx, rr[i].FileIdx);
                    Assert.AreEqual(rk[i].Charge, rr[i].Charge);
                    Assert.AreEqual(rk[i].IsDecoy, rr[i].IsDecoy);
                    Assert.AreEqual(rk[i].CoelutionSum, rr[i].CoelutionSum, 0.0);
                    Assert.AreEqual(rk[i].Score, rr[i].Score, 0.0);
                }
            }
        }

        /// <summary>
        /// The lean projection must round-trip every field it still carries (issue
        /// #4355 struct-shrink S0): the identity/drive slice + CoelutionSum + Score.
        /// The six q-value OUTPUTS no longer live on the struct (they flow through the
        /// per-pass <see cref="IFdrOutputSink"/>), so they are not asserted here.
        /// Builds an FdrEntry with a distinct non-default value in every carried field
        /// and asserts the FdrProjection row reproduces each one, that the interned
        /// peptide table resolves the sequence, and that the file index is set.
        /// </summary>
        [TestMethod]
        public void TestFdrProjectionRoundTripsFields()
        {
            var e = new FdrEntry
            {
                EntryId = 0x8000002Au,
                ParquetIndex = 77u,
                IsDecoy = true,
                Charge = 4,
                CoelutionSum = 123.456,
                Score = -2.5,
                RunPrecursorQvalue = 0.011,
                RunPeptideQvalue = 0.022,
                RunProteinQvalue = 0.033,
                ExperimentPrecursorQvalue = 0.044,
                ExperimentPeptideQvalue = 0.055,
                Pep = 0.066,
                ModifiedSequence = "PEPTIDER"
            };
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("only", new List<FdrEntry> { e })
            };

            var set = FdrProjectionSet.BuildFromEntries(perFile);
            var p = set.PerFile[0].Value[0];

            Assert.AreEqual(e.EntryId, p.EntryId);
            Assert.AreEqual(e.ParquetIndex, p.ParquetIndex);
            Assert.AreEqual(e.IsDecoy, p.IsDecoy);
            Assert.AreEqual(e.Charge, p.Charge);
            Assert.AreEqual(e.CoelutionSum, p.CoelutionSum, 0.0);
            Assert.AreEqual(e.Score, p.Score, 0.0);
            Assert.AreEqual(0, p.FileIdx);
            Assert.AreEqual(e.ModifiedSequence, set.PeptideById[p.PeptideId]);

            // WithScore replaces only Score, preserving the identity/drive slice.
            var scored = p.WithScore(9.0);
            Assert.AreEqual(9.0, scored.Score, 0.0);
            Assert.AreEqual(e.EntryId, scored.EntryId);
            Assert.AreEqual(e.ParquetIndex, scored.ParquetIndex);
            Assert.AreEqual(e.CoelutionSum, scored.CoelutionSum, 0.0);
            Assert.AreEqual(e.IsDecoy, scored.IsDecoy);
        }

        /// <summary>
        /// The projection index-zip write-back
        /// (<see cref="PercolatorEngine.ApplyPercolatorResultsToProjection"/>) must
        /// place the same Score + five q-values onto each row as the FdrEntry
        /// write-back places on the corresponding stub, given identical
        /// index-aligned results. This is the q-value source the 1st-pass sidecar
        /// (hence the survivor reload) reads, so a divergence here would move the
        /// reloaded survivor buffer off the legacy oracle.
        /// </summary>
        [TestMethod]
        public void TestApplyPercolatorResultsToProjectionMatchesFdrEntry()
        {
            var fdrStubs = BuildWritebackFixture();
            var projSet = FdrProjectionSet.BuildFromEntries(BuildWritebackFixture());

            var results = new PercolatorResults { Entries = new List<PercolatorResult>() };
            int seq = 0;
            foreach (var kvp in fdrStubs)
            {
                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    results.Entries.Add(new PercolatorResult
                    {
                        Score = 100.0 + seq,
                        RunPrecursorQvalue = 0.100 + seq * 0.001,
                        RunPeptideQvalue = 0.200 + seq * 0.001,
                        ExperimentPrecursorQvalue = 0.300 + seq * 0.001,
                        ExperimentPeptideQvalue = 0.400 + seq * 0.001,
                        Pep = 0.500 + seq * 0.001
                    });
                    seq++;
                }
            }

            PercolatorEngine.ApplyPercolatorResults(fdrStubs, results);
            // The lean struct only takes Score via WithScore; the five q-values are
            // handed to the sink (issue #4355 struct-shrink S0). Capture them and
            // compare against the FdrEntry oracle's stub values.
            var sink = new CapturingSink();
            PercolatorEngine.ApplyPercolatorResultsToProjection(projSet.PerFile, results, sink);

            Assert.AreEqual(fdrStubs.Count, projSet.PerFile.Count);
            for (int f = 0; f < fdrStubs.Count; f++)
            {
                var stubList = fdrStubs[f].Value;
                var projList = projSet.PerFile[f].Value;
                Assert.AreEqual(stubList.Count, projList.Count);
                for (int i = 0; i < stubList.Count; i++)
                {
                    Assert.AreEqual(stubList[i].Score, projList[i].Score, 0.0);
                    Assert.AreEqual(stubList[i].Score, sink.ScoreAt(f, i), 0.0);
                    var q = sink.QAt(f, i);
                    Assert.AreEqual(stubList[i].RunPrecursorQvalue, q.RunPrecursorQvalue, 0.0);
                    Assert.AreEqual(stubList[i].RunPeptideQvalue, q.RunPeptideQvalue, 0.0);
                    Assert.AreEqual(stubList[i].ExperimentPrecursorQvalue, q.ExperimentPrecursorQvalue, 0.0);
                    Assert.AreEqual(stubList[i].ExperimentPeptideQvalue, q.ExperimentPeptideQvalue, 0.0);
                    Assert.AreEqual(stubList[i].Pep, q.Pep, 0.0);
                }
            }
        }

        /// <summary>
        /// Minimal <see cref="IFdrOutputSink"/> for the projection parity tests: records
        /// each row's Score + <see cref="FdrQValues"/> by (fileIdx, rowIdx) so the test
        /// can compare the streamed outputs against the FdrEntry oracle now that the lean
        /// struct no longer stores them (issue #4355 struct-shrink S0).
        /// </summary>
        private sealed class CapturingSink : IFdrOutputSink
        {
            private readonly Dictionary<(int, int), double> _scores = new Dictionary<(int, int), double>();
            private readonly Dictionary<(int, int), FdrQValues> _q = new Dictionary<(int, int), FdrQValues>();

            public void Accept(int fileIdx, int rowIdx, uint entryId, bool isDecoy,
                double score, in FdrQValues q)
            {
                _scores[(fileIdx, rowIdx)] = score;
                _q[(fileIdx, rowIdx)] = q;
            }

            public void Finish(Action<string> logInfo)
            {
            }

            public double ScoreAt(int fileIdx, int rowIdx) => _scores[(fileIdx, rowIdx)];
            public FdrQValues QAt(int fileIdx, int rowIdx) => _q[(fileIdx, rowIdx)];
        }

        /// <summary>
        /// End-to-end projection RunPercolatorFdr equivalence (the survivor-reload
        /// equivalence at the unit level): the projection
        /// <see cref="PercolatorEngine.RunPercolatorFdr(FdrProjectionSet,OspreyConfig,OspreyFeatureInfo[],System.Action{string},IFdrOutputSink,PercolatorDiagnosticsConfig,string,System.Func{string,System.Collections.Generic.IReadOnlyList{double[]}})"/>
        /// overload must produce byte-identical Score + q-values to the FdrEntry
        /// <see cref="PercolatorEngine.RunPercolatorFdr(System.Collections.Generic.List{System.Collections.Generic.KeyValuePair{string,System.Collections.Generic.List{FdrEntry}}},OspreyConfig,OspreyFeatureInfo[],System.Action{string},PercolatorDiagnosticsConfig,string,System.Func{string,System.Collections.Generic.IReadOnlyList{double[]}})"/>
        /// overload -- the flag-off byte-identity ORACLE -- on the same input, at the
        /// shared production config (default MaxTrainSize, so both overloads take their
        /// DIRECT dispatch: train RunPercolator on the full population with the Stage 5
        /// standardizer fit on ALL entries). This is the exact equivalence the 2nd-pass
        /// survivor reload preserves.
        ///
        /// The fixture deliberately gives each precursor MULTIPLE observations (several
        /// scans per base_id) so best-per-precursor dedup collapses the training pool to
        /// a strict SUBSET of the population. That is the property that makes the
        /// direct-vs-streaming DISPATCH observable (issue #4374): the direct path fits
        /// the standardizer on all observations, the streaming path fits it on the
        /// best-per-precursor subset only, so the two produce DIFFERENT scores on this
        /// fixture. If the projection overload ever silently reverts to always-streaming
        /// (dropping the below-threshold direct fork), it fits the standardizer on the
        /// subset while the FdrEntry oracle keeps fitting it on the full population, and
        /// every Score/q-value here diverges -- turning this test red. A single-
        /// observation fixture (where dedup is a no-op and streaming == direct) would NOT
        /// catch that regression; multi-observation is load-bearing.
        /// (<see cref="TestProjectionStreamingMatchesFdrEntryStreaming"/> covers the
        /// subsample-forced streaming primitive above the threshold.)
        /// </summary>
        [TestMethod]
        public void TestProjectionRunPercolatorFdrMatchesFdrEntry()
        {
            const int nFeat = 3;
            var featureInfos = new[]
            {
                new OspreyFeatureInfo("feat_a", "Feature A", false),
                new OspreyFeatureInfo("feat_b", "Feature B", false),
                new OspreyFeatureInfo("feat_c", "Feature C", false)
            };
            var config = new OspreyConfig(); // RunFdr 0.01, Percolator, Precursor

            // Multi-observation fixture: best-per-precursor dedup is non-trivial, so the
            // DIRECT path (standardizer on all) and the STREAMING path (standardizer on
            // the deduped subset) give different results -- the property that lets this
            // test gate the below-threshold direct dispatch.
            var fdrStubs = BuildMultiObservationEquivFixture(nFeat, out var featuresA);
            var fdrStubs2 = BuildMultiObservationEquivFixture(nFeat, out var featuresB);
            var projSet = FdrProjectionSet.BuildFromEntries(fdrStubs2);

            // FdrEntry oracle overload: at the default MaxTrainSize this dispatches to
            // the direct RunPercolator (standardizer fit on the full population).
            PercolatorEngine.RunPercolatorFdr(
                fdrStubs, config, featureInfos, s => { }, null, "First-pass",
                f => featuresA[f]);
            // Projection overload under test: must take the SAME direct dispatch and
            // therefore match the oracle byte-for-byte. The lean struct takes Score; the
            // q-values are captured off the sink (issue #4355 struct-shrink S0).
            var sink = new CapturingSink();
            PercolatorEngine.RunPercolatorFdr(
                projSet, config, featureInfos, s => { }, sink, null, "First-pass",
                f => featuresB[f]);

            // Both overloads sort their buffers, so compare keyed -- EntryId repeats
            // across a precursor's observations, so key by (fileIdx, ParquetIndex),
            // which is unique per observation and shared identically by both buffers
            // (the projection's null resolver copies the stub's ParquetIndex).
            var refByKey = new Dictionary<(int, uint), FdrEntry>();
            for (int f = 0; f < fdrStubs.Count; f++)
            {
                foreach (var e in fdrStubs[f].Value)
                    refByKey[(f, e.ParquetIndex)] = e;
            }
            int compared = 0;
            for (int f = 0; f < projSet.PerFile.Count; f++)
            {
                var rows = projSet.PerFile[f].Value;
                for (int r = 0; r < rows.Count; r++)
                {
                    var proj = rows[r];
                    Assert.IsTrue(refByKey.TryGetValue((f, proj.ParquetIndex), out FdrEntry e),
                        "every projection row must have its FdrEntry reference");
                    Assert.AreEqual(e.EntryId, proj.EntryId);
                    Assert.AreEqual(e.Score, proj.Score, 0.0);
                    var q = sink.QAt(f, r);
                    Assert.AreEqual(e.RunPrecursorQvalue, q.RunPrecursorQvalue, 0.0);
                    Assert.AreEqual(e.RunPeptideQvalue, q.RunPeptideQvalue, 0.0);
                    Assert.AreEqual(e.ExperimentPrecursorQvalue, q.ExperimentPrecursorQvalue, 0.0);
                    Assert.AreEqual(e.ExperimentPeptideQvalue, q.ExperimentPeptideQvalue, 0.0);
                    Assert.AreEqual(e.Pep, q.Pep, 0.0);
                    compared++;
                }
            }
            Assert.AreEqual(refByKey.Count, compared);
        }

        /// <summary>
        /// Issue #4355 step (b) increment iii (the transient-SVM-stack collapse, gate
        /// 1): the projection-native STREAMING score+compete path
        /// (<see cref="PercolatorEngine.RunStreamingIntoProjection"/>) must
        /// produce byte-identical Score + five q-values to the legacy
        /// <see cref="PercolatorEntry"/> streaming path
        /// (<see cref="PercolatorEngine.RunPercolatorStreaming"/> + the index-zip
        /// write-back) on the same fixture, same per-file feature loader, and same
        /// input order -- proving the collapse changed only WHERE THE DATA LIVES, not
        /// the parity-locked SVM training, subsample, standardizer, PEP ordering, or
        /// q-value math. This test forces an actual peptide-grouped subsample with a
        /// small MaxTrainSize; the end-to-end always-streaming projection overload (issue
        /// #4374 removed the direct-vs-streaming dispatch, so it streams at every scale)
        /// is covered by <see cref="TestProjectionRunPercolatorFdrMatchesFdrEntry"/>.
        /// </summary>
        [TestMethod]
        public void TestProjectionStreamingMatchesFdrEntryStreaming()
        {
            const int nFeat = 3;
            var featureInfos = new[]
            {
                new OspreyFeatureInfo("feat_a", "Feature A", false),
                new OspreyFeatureInfo("feat_b", "Feature B", false),
                new OspreyFeatureInfo("feat_c", "Feature C", false)
            };

            // Identical fixtures for the two paths (deterministic builder); each is
            // fed to its path in the SAME fixture order (no sort), so a positional
            // compare stands in for a keyed one and isolates the score/compete
            // equivalence from the (separately tested) canonical sort.
            var fdrStubs = BuildProjectionEquivFixture(nFeat, out var featuresA);
            var fdrStubs2 = BuildProjectionEquivFixture(nFeat, out var featuresB);
            var projSet = FdrProjectionSet.BuildFromEntries(fdrStubs2);

            // MaxTrainSize = 60 forces the streaming dispatch (160 entries > 120) AND
            // an actual peptide-grouped subsample (160 dedup > 60), exercising the
            // full streaming flow (dedup -> subsample -> train-on-subset -> score-all).
            var percConfig = new PercolatorConfig
            {
                MaxIterations = 10,
                NFolds = 3,
                Seed = 42,
                TrainFdr = 0.01,
                TestFdr = 0.01,
                MaxTrainSize = 60,
                FeatureInfos = featureInfos
            };

            // Legacy PercolatorEntry streaming path (the byte-identity oracle).
            var percEntries = PercolatorEntryBuilder.Build(
                fdrStubs, nFeat, streamFeatures: true,
                out int nWith, out int nWithout, out int nTargets, out int nDecoys);
            Assert.AreEqual(160, percEntries.Count);
            Assert.AreEqual(80, nTargets);
            Assert.AreEqual(80, nDecoys);
            PercolatorResults streamingResults = PercolatorEngine.RunPercolatorStreaming(
                percEntries, percConfig, s => { }, "First-pass", f => featuresA[f]);
            PercolatorEngine.ApplyPercolatorResults(fdrStubs, streamingResults);

            // Projection-native streaming path (the change under test). Score lands on
            // the lean struct; the five q-values are captured off the sink (issue #4355
            // struct-shrink S0).
            var sink = new CapturingSink();
            bool abort = PercolatorEngine.RunStreamingIntoProjection(
                projSet.PerFile, projSet.PeptideById, percConfig, s => { }, "First-pass",
                f => featuresB[f], sink);
            Assert.IsFalse(abort);

            Assert.AreEqual(fdrStubs.Count, projSet.PerFile.Count);
            for (int f = 0; f < fdrStubs.Count; f++)
            {
                var stubList = fdrStubs[f].Value;
                var projList = projSet.PerFile[f].Value;
                Assert.AreEqual(stubList.Count, projList.Count);
                for (int i = 0; i < stubList.Count; i++)
                {
                    Assert.AreEqual(stubList[i].EntryId, projList[i].EntryId);
                    Assert.AreEqual(stubList[i].Score, projList[i].Score, 0.0);
                    var q = sink.QAt(f, i);
                    Assert.AreEqual(stubList[i].RunPrecursorQvalue, q.RunPrecursorQvalue, 0.0);
                    Assert.AreEqual(stubList[i].RunPeptideQvalue, q.RunPeptideQvalue, 0.0);
                    Assert.AreEqual(stubList[i].ExperimentPrecursorQvalue, q.ExperimentPrecursorQvalue, 0.0);
                    Assert.AreEqual(stubList[i].ExperimentPeptideQvalue, q.ExperimentPeptideQvalue, 0.0);
                    Assert.AreEqual(stubList[i].Pep, q.Pep, 0.0);
                }
            }
        }

        /// <summary>
        /// Paired target/decoy fixture for the projection equivalence test: 2 files x
        /// 40 pairs, well-separated target (high) vs decoy (low) features so the SVM
        /// trains cleanly. Each row carries ParquetIndex = its within-file row index,
        /// CoelutionSum = features[0], and the per-file feature rows are returned via
        /// <paramref name="featuresByFile"/> so the streaming loader resolves each row
        /// by ParquetIndex -- the exact per-file reload shape the production path uses.
        /// </summary>
        private static List<KeyValuePair<string, List<FdrEntry>>> BuildProjectionEquivFixture(
            int nFeat, out Dictionary<string, List<double[]>> featuresByFile)
        {
            featuresByFile = new Dictionary<string, List<double[]>>();
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>();
            uint idCounter = 0;
            int scan = 0;
            for (int file = 0; file < 2; file++)
            {
                string fileName = string.Format("file{0}", file);
                var list = new List<FdrEntry>();
                var featureRows = new List<double[]>();
                for (int i = 0; i < 40; i++)
                {
                    idCounter++;
                    var targetFeatures = new double[nFeat];
                    var decoyFeatures = new double[nFeat];
                    for (int j = 0; j < nFeat; j++)
                    {
                        targetFeatures[j] = 4.0 + (file * 40 + i) * 0.03 + j * 0.1;
                        decoyFeatures[j] = 0.5 + (file * 40 + i) * 0.02 + j * 0.1;
                    }

                    list.Add(new FdrEntry
                    {
                        EntryId = idCounter,
                        ParquetIndex = (uint)featureRows.Count,
                        ModifiedSequence = string.Format("PEPTIDE{0}", file * 40 + i),
                        Charge = 2,
                        ScanNumber = (uint)(++scan),
                        IsDecoy = false,
                        CoelutionSum = targetFeatures[0]
                    });
                    featureRows.Add(targetFeatures);

                    list.Add(new FdrEntry
                    {
                        EntryId = idCounter | 0x80000000u,
                        ParquetIndex = (uint)featureRows.Count,
                        ModifiedSequence = string.Format("DECOY{0}", file * 40 + i),
                        Charge = 2,
                        ScanNumber = (uint)(++scan),
                        IsDecoy = true,
                        CoelutionSum = decoyFeatures[0]
                    });
                    featureRows.Add(decoyFeatures);
                }
                perFile.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, list));
                featuresByFile[fileName] = featureRows;
            }
            return perFile;
        }

        /// <summary>
        /// Paired target/decoy fixture with MULTIPLE observations per precursor for the
        /// direct-dispatch equivalence gate (<see cref="TestProjectionRunPercolatorFdrMatchesFdrEntry"/>):
        /// 2 files x 20 precursors x 3 scans each, well-separated target (high) vs decoy
        /// (low) features. Every observation of a precursor shares the precursor's
        /// EntryId (so <c>SelectBestPerPrecursor</c> collapses the 3 scans to 1) but
        /// carries DISTINCT feature values (each scan offsets by <c>k</c>), so the Stage 5
        /// standardizer's mean/std over ALL 240 observations differs measurably from over
        /// the 80-row best-per-precursor subset. That difference is what makes the direct
        /// path (standardizer on all) and the streaming path (standardizer on the subset)
        /// produce different results -- the property the equivalence test relies on to
        /// detect a projection overload that skips its below-threshold direct dispatch.
        /// Rows for a precursor are appended consecutively with increasing scan AND
        /// increasing ParquetIndex, so within each (EntryId, Charge) group the projection
        /// sort (EntryId, Charge, ParquetIndex) yields the identical order the FdrEntry
        /// sort (EntryId, Charge, ScanNumber, ParquetIndex) does. CoelutionSum =
        /// features[0], the value best-per-precursor ranks on (highest scan kept).
        /// </summary>
        private static List<KeyValuePair<string, List<FdrEntry>>> BuildMultiObservationEquivFixture(
            int nFeat, out Dictionary<string, List<double[]>> featuresByFile)
        {
            const int filesCount = 2;
            const int precursorsPerFile = 20;
            const int obsPerPrecursor = 3;
            featuresByFile = new Dictionary<string, List<double[]>>();
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>();
            uint scan = 0;
            for (int file = 0; file < filesCount; file++)
            {
                string fileName = string.Format("file{0}", file);
                var list = new List<FdrEntry>();
                var featureRows = new List<double[]>();
                for (int p = 0; p < precursorsPerFile; p++)
                {
                    // EntryId unique per (file, precursor); the decoy bit distinguishes
                    // the target/decoy base_id, exactly as the production stubs do.
                    uint targetId = (uint)(file * 1000 + p + 1);
                    uint decoyId = targetId | 0x80000000u;
                    string pepSeq = string.Format("PEPTIDE{0}_{1}", file, p);
                    string decoySeq = string.Format("DECOY{0}_{1}", file, p);
                    for (int k = 0; k < obsPerPrecursor; k++)
                    {
                        var targetFeatures = new double[nFeat];
                        var decoyFeatures = new double[nFeat];
                        for (int j = 0; j < nFeat; j++)
                        {
                            targetFeatures[j] = 4.0 + p * 0.05 + k * 0.7 + j * 0.1;
                            decoyFeatures[j] = 0.5 + p * 0.04 + k * 0.6 + j * 0.1;
                        }

                        list.Add(new FdrEntry
                        {
                            EntryId = targetId,
                            ParquetIndex = (uint)featureRows.Count,
                            ModifiedSequence = pepSeq,
                            Charge = 2,
                            ScanNumber = ++scan,
                            IsDecoy = false,
                            CoelutionSum = targetFeatures[0]
                        });
                        featureRows.Add(targetFeatures);

                        list.Add(new FdrEntry
                        {
                            EntryId = decoyId,
                            ParquetIndex = (uint)featureRows.Count,
                            ModifiedSequence = decoySeq,
                            Charge = 2,
                            ScanNumber = ++scan,
                            IsDecoy = true,
                            CoelutionSum = decoyFeatures[0]
                        });
                        featureRows.Add(decoyFeatures);
                    }
                }
                perFile.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, list));
                featuresByFile[fileName] = featureRows;
            }
            return perFile;
        }

        /// <summary>
        /// The post-training feature weight + percent-contribution report: with a
        /// clean target/decoy separation, the emitted percentages must sum to ~100%
        /// (the mean-difference decomposition is exact by linearity), the surfaced
        /// averaged weights must be populated, and the printed unexpected-direction
        /// flag must obey Skyline's weight-sign rule
        /// <c>IsReversedScore XOR (weight &lt; 0)</c>. A feature whose trained weight
        /// disagrees with its declared direction gets the "(unexpected direction)"
        /// suffix; reporting must never perturb the q-values.
        /// </summary>
        [TestMethod]
        public void TestFeatureContributionReport()
        {
            // 3 features; targets clearly separate on all three. Feature 1 is
            // declared reversed (lower-is-better) yet targets here have the HIGHER
            // value -> the trained weight comes out positive, so
            // reversed(true) XOR (w<0 == false) == true -> flagged unexpected.
            var entries = new List<PercolatorEntry>();
            for (int i = 0; i < 60; i++)
            {
                entries.Add(MakePercolatorEntry(
                    "file1", string.Format("PEPTIDE{0}", i), 2, false, (uint)(i + 1),
                    new[] { 4.0 + i * 0.02, 4.0 + i * 0.02, 4.0 + i * 0.02 }));
            }
            for (int i = 0; i < 60; i++)
            {
                entries.Add(MakePercolatorEntry(
                    "file1", string.Format("DECOY{0}", i), 2, true, (uint)(i + 1) | 0x80000000,
                    new[] { 0.5 + i * 0.02, 0.5 + i * 0.02, 0.5 + i * 0.02 }));
            }

            var config = new PercolatorConfig
            {
                MaxIterations = 3,
                FeatureInfos = new[]
                {
                    new OspreyFeatureInfo("feat_a", "Feature A", false),
                    new OspreyFeatureInfo("feat_b", "Feature B", true),
                    new OspreyFeatureInfo("feat_c", "Feature C", false)
                }
            };

            string report;
            string defaultReport;
            PercolatorResults results;
            var savedOut = OspreyOutput.Out;
            bool savedVerbose = OspreyOutput.Verbose;
            try
            {
                // Default console (verbose off): the model sanity-check table must NOT
                // appear -- it is gated behind --verbose (issue #4364).
                var defaultCapture = new StringWriter();
                OspreyOutput.Out = defaultCapture;
                OspreyOutput.Verbose = false;
                PercolatorFdr.RunPercolator(entries, config);
                defaultReport = defaultCapture.ToString();

                // Verbose console: the table is emitted.
                var capture = new StringWriter();
                OspreyOutput.Out = capture;
                OspreyOutput.Verbose = true;
                results = PercolatorFdr.RunPercolator(entries, config);
                report = capture.ToString();
            }
            finally
            {
                OspreyOutput.Out = savedOut;
                OspreyOutput.Verbose = savedVerbose;
            }

            // The contribution decomposition is surfaced on the results regardless of
            // the verbose gate (the gate only controls the printed table).
            Assert.IsNotNull(results.FeatureContributions);
            var features = results.FeatureContributions.Features;
            Assert.AreEqual(3, features.Count);

            // The model sanity-check block appears only under --verbose, reframed away
            // from importance/weight wording (issue #4364).
            StringAssert.Contains(report,
                "Model sanity check -- feature share of target-decoy separation");
            Assert.IsFalse(defaultReport.Contains("Model sanity check"),
                "the feature share table must be gated behind --verbose");

            // Parse the percent column from the three feature rows. The table rows
            // are "<4 spaces><label><coefficient F4><percent F1>%"; match on the
            // coefficient-then-percent shape so the unrelated "{F1}% at {P0} FDR"
            // training-progress lines (which have "(" / " at " around the percent)
            // are not picked up.
            var percents = new List<double>();
            foreach (Match m in Regex.Matches(report,
                         @"^    \S.*\s-?\d+\.\d{4}\s+(-?\d+\.\d)%",
                         RegexOptions.Multiline))
                percents.Add(double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            Assert.AreEqual(3, percents.Count,
                "expected exactly three percent rows in the contribution table");
            double total = percents.Sum();
            Assert.AreEqual(100.0, total, 0.2,
                string.Format("contribution percentages should sum to ~100% (got {0})", total));

            // The flag must match Skyline's weight-sign rule on the surfaced model:
            // both the decomposition object's IsUnexpectedDirection and the printed
            // row must agree with reversed XOR (coefficient < 0).
            for (int j = 0; j < 3; j++)
            {
                bool expectedFlag = features[j].IsReversedScore ^ (features[j].Coefficient < 0.0);
                Assert.AreEqual(expectedFlag, features[j].IsUnexpectedDirection,
                    string.Format("Feature {0} object flag mismatch (weight={1})",
                        (char)('A' + j), features[j].Coefficient));
                bool rowFlagged = Regex.IsMatch(report,
                    @"Feature " + (char)('A' + j) + @"\b.*\(unexpected direction\)");
                Assert.AreEqual(expectedFlag, rowFlagged,
                    string.Format("Feature {0} printed-flag mismatch (weight={1})",
                        (char)('A' + j), features[j].Coefficient));
            }

            // Feature B (declared reversed, but targets are higher here) must be the
            // flagged one: its trained weight is positive.
            Assert.IsTrue(features[1].Coefficient > 0.0,
                "fixture should drive a positive weight on the declared-reversed feature B");
            StringAssert.Contains(report, "(unexpected direction)");

            // Reporting did not disturb scoring: targets still outscore decoys.
            double avgTarget = 0.0, avgDecoy = 0.0;
            int nT = 0, nD = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].IsDecoy) { avgDecoy += results.Entries[i].Score; nD++; }
                else { avgTarget += results.Entries[i].Score; nT++; }
            }
            Assert.IsTrue(avgTarget / nT > avgDecoy / nD);
        }

        /// <summary>
        /// The pure <see cref="FeatureContributions"/> calculation (no I/O): from a
        /// small synthetic averaged model with a known target-decoy separation, the
        /// per-feature percents must sum to ~100%, the composite must be
        /// non-degenerate, and the unexpected-direction flag must obey Skyline's
        /// weight-sign rule <c>IsReversedScore XOR (weight &lt; 0)</c> -- here a
        /// hand-set reversed feature carrying a positive weight reports
        /// <c>IsUnexpectedDirection == true</c>.
        /// </summary>
        [TestMethod]
        public void TestFeatureContributions()
        {
            // 3 features, 4 targets / 4 decoys; targets sit one unit above decoys on
            // every feature (clean positive separation -> composite > 0). Feature 1
            // is declared reversed yet given a positive weight -> flagged unexpected.
            var avgWeights = new[] { 2.0, 1.5, 0.5 };
            int nFeatures = avgWeights.Length;
            long nTarget = 4, nDecoy = 4;
            var sumTarget = new double[nFeatures];
            var sumDecoy = new double[nFeatures];
            for (int j = 0; j < nFeatures; j++)
            {
                sumTarget[j] = nTarget * (1.0 + j);   // target mean = 1 + j
                sumDecoy[j] = nDecoy * (0.0 + j);     // decoy mean  = j  -> gap = 1
            }
            var featureInfos = new[]
            {
                new OspreyFeatureInfo("feat_a", "Feature A", false),
                new OspreyFeatureInfo("feat_b", "Feature B", true),
                new OspreyFeatureInfo("feat_c", "Feature C", false)
            };

            var contributions = new FeatureContributions(avgWeights, sumTarget, sumDecoy,
                nTarget, nDecoy, featureInfos);

            Assert.IsFalse(contributions.IsDegenerate);
            Assert.IsTrue(contributions.Composite > 0.0);
            Assert.AreEqual(nFeatures, contributions.Features.Count);

            // The per-feature percents sum to ~100% by linearity.
            double totalPercent = contributions.Features.Sum(f => f.Percent);
            Assert.AreEqual(100.0, totalPercent, 1e-9);

            // Canonical feature-index order is preserved (not display-sorted).
            for (int j = 0; j < nFeatures; j++)
            {
                var f = contributions.Features[j];
                Assert.AreEqual(j, f.Index);
                Assert.AreEqual(featureInfos[j].Label, f.Label);
                Assert.AreEqual(avgWeights[j], f.Coefficient);
                Assert.AreEqual(1.0, f.TargetDecoyMeanGap, 1e-12);   // gap is 1 for every feature
                Assert.AreEqual(avgWeights[j], f.Weighted, 1e-12);   // w_j * 1.0
                bool expectedFlag = featureInfos[j].IsReversedScore ^ (avgWeights[j] < 0.0);
                Assert.AreEqual(expectedFlag, f.IsUnexpectedDirection);
            }

            // Feature B is declared reversed but carries a positive weight -> unexpected.
            Assert.IsTrue(contributions.Features[1].IsReversedScore);
            Assert.IsTrue(contributions.Features[1].IsUnexpectedDirection);
            Assert.IsFalse(contributions.Features[0].IsUnexpectedDirection);
            Assert.IsFalse(contributions.Features[2].IsUnexpectedDirection);
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
            string file, string peptide, byte charge,
            bool isDecoy, uint entryId, double[] features)
        {
            return new PercolatorEntry
            {
                FileName = file,
                Peptide = peptide,
                Charge = charge,
                IsDecoy = isDecoy,
                EntryId = entryId,
                Features = features
            };
        }

        /// <summary>
        /// Two files of FdrEntry stubs with distinct (modified_sequence, charge,
        /// scan_number) per row so the legacy 4-component psm_id is collision-free --
        /// the regime the removed resultMap re-join was correct in. Scores / q-values
        /// start at their FdrEntry defaults so the write-back is what changes them.
        /// </summary>
        private static List<KeyValuePair<string, List<FdrEntry>>> BuildWritebackFixture()
        {
            var fileA = new List<FdrEntry>();
            for (int i = 0; i < 4; i++)
            {
                fileA.Add(new FdrEntry
                {
                    EntryId = (uint)(i + 1),
                    ModifiedSequence = string.Format("PEPTIDE{0}", i),
                    Charge = 2,
                    ScanNumber = (uint)(1000 + i),
                    IsDecoy = false
                });
            }
            var fileB = new List<FdrEntry>();
            for (int i = 0; i < 3; i++)
            {
                fileB.Add(new FdrEntry
                {
                    EntryId = (uint)(i + 1) | 0x80000000,
                    ModifiedSequence = string.Format("DECOY{0}", i),
                    Charge = 3,
                    ScanNumber = (uint)(2000 + i),
                    IsDecoy = true
                });
            }
            return new List<KeyValuePair<string, List<FdrEntry>>>
            {
                new KeyValuePair<string, List<FdrEntry>>("fileA", fileA),
                new KeyValuePair<string, List<FdrEntry>>("fileB", fileB)
            };
        }

        /// <summary>
        /// Two files of paired target/decoy FdrEntry stubs (30 pairs each) carrying
        /// resident <paramref name="nFeat"/>-vectors, so the flat PercolatorEntry
        /// list can be trained and scored through the real streaming assembler. A
        /// global per-row index keeps (modified_sequence, scan_number) distinct so
        /// the legacy psm_id is collision-free; well-separated target (high) vs decoy
        /// (low) features give distinct per-entry scores so a misaligned write-back
        /// would surface as a mismatch.
        /// </summary>
        private static List<KeyValuePair<string, List<FdrEntry>>> BuildStreamingWritebackFixture(int nFeat)
        {
            var perFile = new List<KeyValuePair<string, List<FdrEntry>>>();
            int scan = 0;
            for (int file = 0; file < 2; file++)
            {
                var list = new List<FdrEntry>();
                for (int i = 0; i < 30; i++)
                {
                    int idx = file * 30 + i;
                    var targetFeatures = new double[nFeat];
                    var decoyFeatures = new double[nFeat];
                    for (int j = 0; j < nFeat; j++)
                    {
                        targetFeatures[j] = 4.0 + idx * 0.02;
                        decoyFeatures[j] = 0.5 + idx * 0.02;
                    }
                    list.Add(new FdrEntry
                    {
                        EntryId = (uint)(idx + 1),
                        ModifiedSequence = string.Format("PEPTIDE{0}", idx),
                        Charge = 2,
                        ScanNumber = (uint)(++scan),
                        IsDecoy = false,
                        CoelutionSum = targetFeatures[0],
                        Features = targetFeatures
                    });
                    list.Add(new FdrEntry
                    {
                        EntryId = (uint)(idx + 1) | 0x80000000,
                        ModifiedSequence = string.Format("DECOY{0}", idx),
                        Charge = 2,
                        ScanNumber = (uint)(++scan),
                        IsDecoy = true,
                        CoelutionSum = decoyFeatures[0],
                        Features = decoyFeatures
                    });
                }
                perFile.Add(new KeyValuePair<string, List<FdrEntry>>(
                    string.Format("file{0}", file), list));
            }
            return perFile;
        }

        /// <summary>
        /// Replicates the psm_id-keyed resultMap write-back removed in issue #4355
        /// step (b): build a "{file}_{modseq}_{charge}_{scan}" -> result map (results
        /// are index-aligned to the nested walk, so entry k keys on result k) then
        /// re-join each stub by that key. The oracle the positional index zip must
        /// match on a collision-free fixture.
        /// </summary>
        private static void ApplyLegacyPsmIdWriteback(
            List<KeyValuePair<string, List<FdrEntry>>> perFileEntries,
            PercolatorResults results)
        {
            var resultMap = new Dictionary<string, PercolatorResult>();
            int k = 0;
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    string id = LegacyPsmId(kvp.Key, e);
                    Assert.IsFalse(resultMap.ContainsKey(id),
                        string.Format("fixture psm_id {0} is not unique", id));
                    resultMap[id] = results.Entries[k++];
                }
            }
            foreach (var kvp in perFileEntries)
            {
                foreach (var e in kvp.Value)
                {
                    PercolatorResult r = resultMap[LegacyPsmId(kvp.Key, e)];
                    e.Score = r.Score;
                    e.RunPrecursorQvalue = r.RunPrecursorQvalue;
                    e.RunPeptideQvalue = r.RunPeptideQvalue;
                    e.ExperimentPrecursorQvalue = r.ExperimentPrecursorQvalue;
                    e.ExperimentPeptideQvalue = r.ExperimentPeptideQvalue;
                    e.Pep = r.Pep;
                }
            }
        }

        private static string LegacyPsmId(string fileName, FdrEntry e)
        {
            return string.Format("{0}_{1}_{2}_{3}",
                fileName, e.ModifiedSequence, e.Charge, e.ScanNumber);
        }

        private static void AssertWritebackEqual(
            List<KeyValuePair<string, List<FdrEntry>>> actual,
            List<KeyValuePair<string, List<FdrEntry>>> expected)
        {
            Assert.AreEqual(expected.Count, actual.Count);
            for (int f = 0; f < expected.Count; f++)
            {
                var expList = expected[f].Value;
                var actList = actual[f].Value;
                Assert.AreEqual(expList.Count, actList.Count);
                for (int i = 0; i < expList.Count; i++)
                {
                    FdrEntry exp = expList[i];
                    FdrEntry act = actList[i];
                    Assert.AreEqual(exp.Score, act.Score, 0.0);
                    Assert.AreEqual(exp.RunPrecursorQvalue, act.RunPrecursorQvalue, 0.0);
                    Assert.AreEqual(exp.RunPeptideQvalue, act.RunPeptideQvalue, 0.0);
                    Assert.AreEqual(exp.ExperimentPrecursorQvalue, act.ExperimentPrecursorQvalue, 0.0);
                    Assert.AreEqual(exp.ExperimentPeptideQvalue, act.ExperimentPeptideQvalue, 0.0);
                    Assert.AreEqual(exp.Pep, act.Pep, 0.0);
                }
            }
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
