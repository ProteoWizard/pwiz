/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.FDR;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The protein-best reduction (<see cref="ProteinBestFeature"/>) on hand-built runs, where every
    /// expected value can be read off the fixture.
    /// </summary>
    [TestClass]
    public class ProteinBestFeatureTest
    {
        private const double TOLERANCE = 1e-12;

        // Peptide A has two charges (1, 2); B (3) and C (4) share protein P1 with it; C is also on
        // Q1 with D (5); E (6) is alone on R1; F (7) is on no protein.
        private static readonly ProteinBestMembership.Precursor[] PRECURSORS =
        {
            new ProteinBestMembership.Precursor(1, @"AAA", new[] { @"P1" }),
            new ProteinBestMembership.Precursor(2, @"AAA", new[] { @"P1" }),
            new ProteinBestMembership.Precursor(3, @"BBB", new[] { @"P1" }),
            new ProteinBestMembership.Precursor(4, @"CCC", new[] { @"P1", @"Q1" }),
            new ProteinBestMembership.Precursor(5, @"DDD", new[] { @"Q1" }),
            new ProteinBestMembership.Precursor(6, @"EEE", new[] { @"R1" }),
            new ProteinBestMembership.Precursor(7, @"FFF", Array.Empty<string>())
        };

        [TestMethod]
        public void TestProteinBestReduction()
        {
            AssertSelfExclusionAcrossCharges();
            AssertTopNPaddedWithFloor();
            AssertSharedPeptideTakesLargest();
            AssertPairedAndOwnDecoys();
            AssertFloorIsExactMedianOfDecoyPrecursors();
            AssertShuffleKeepsGroupSizes();
            AssertOrderIndependentBits();
            AssertSettingsKey();
            AssertModifiedFormsAreOnePeptide();
        }

        /// <summary>A run: rows of (base_id, is decoy, prior score).</summary>
        private sealed class Run
        {
            public readonly List<uint> BaseIds = new List<uint>();
            public readonly List<bool> IsDecoys = new List<bool>();
            public readonly List<double> Scores = new List<double>();

            public Run Target(uint baseId, double score)
            {
                return Add(baseId, false, score);
            }

            public Run Decoy(uint baseId, double score)
            {
                return Add(baseId, true, score);
            }

            private Run Add(uint baseId, bool isDecoy, double score)
            {
                BaseIds.Add(baseId);
                IsDecoys.Add(isDecoy);
                Scores.Add(score);
                return this;
            }

            public double[] Column(int topN, bool decoyOwn, ProteinBestMembership membership = null)
            {
                return ProteinBestFeature.ComputeColumn(membership ?? ProteinBestMembership.Build(PRECURSORS),
                    topN, decoyOwn, BaseIds, IsDecoys, Scores);
            }
        }

        private static void AssertSelfExclusionAcrossCharges()
        {
            // Decoy precursors 1 and 3 at 1 and 3 make a floor of 2. A's second charge scores 200 and
            // a second candidate peak of it 150, and A must read none of them.
            var run = new Run().Target(1, 100).Target(2, 200).Target(2, 150).Target(3, 5)
                .Decoy(1, 1).Decoy(3, 3);
            var column = run.Column(1, false);
            Assert.AreEqual(5, column[0], @"charge 1 of A reads B, not its own other charge");
            Assert.AreEqual(5, column[1], @"charge 2 of A reads B");
            Assert.AreEqual(5, column[2], @"another peak of charge 2 of A reads B");
            Assert.AreEqual(200, column[3], @"B reads A's best precursor");
        }

        private static void AssertTopNPaddedWithFloor()
        {
            var run = new Run().Target(1, 100).Target(3, 5).Target(4, 7).Target(5, 50)
                .Target(6, 9).Target(7, 8).Target(99, 8)
                .Decoy(1, 1).Decoy(3, 3);
            const double floor = 2;
            // A's siblings are C and B: two of three, padded once.
            Assert.AreEqual((7 + 5 + floor) / 3, run.Column(3, false)[0], TOLERANCE);
            // Two of two: the same run reads no floor at all.
            Assert.AreEqual((7 + 5) / 2.0, run.Column(2, false)[0], TOLERANCE);
            foreach (int topN in new[] { 1, 2, 16 })
            {
                var column = run.Column(topN, false);
                // E is alone on its protein, F is on none, and 99 is not in the library.
                Assert.AreEqual(floor, column[4], @"no sibling reads the floor exactly");
                Assert.AreEqual(floor, column[5], @"no protein reads the floor exactly");
                Assert.AreEqual(floor, column[6], @"no library entry reads the floor exactly");
            }
            // A precursor with no row has no score, so it is no sibling: B alone reads A, and D has
            // no row at all, so C reads A.
            var sparse = new Run().Target(1, 100).Target(4, 7).Decoy(1, 1).Decoy(3, 3).Column(1, false);
            Assert.AreEqual(7, sparse[0]);
            Assert.AreEqual(100, sparse[1]);
        }

        private static void AssertSharedPeptideTakesLargest()
        {
            // C on P1 reads A (10) and on Q1 reads D (40): the largest.
            var run = new Run().Target(1, 10).Target(4, 1).Target(5, 40).Decoy(1, 0);
            Assert.AreEqual(40, run.Column(1, false)[1]);
            // With A above D it is P1's value instead, whichever accession comes first.
            run = new Run().Target(1, 50).Target(4, 1).Target(5, 40).Decoy(1, 0);
            Assert.AreEqual(50, run.Column(1, false)[1]);
        }

        private static void AssertPairedAndOwnDecoys()
        {
            var run = new Run().Target(1, 100).Target(3, 5)
                .Decoy(1, 1).Decoy(3, 3);
            // Paired: decoy of A reads what A reads, from target B, even though it has its own
            // decoy sibling; the decoy of B reads what B reads, target A.
            var paired = run.Column(1, false);
            Assert.AreEqual(paired[0], paired[2], @"decoy reads its paired target's value");
            Assert.AreEqual(5, paired[2]);
            Assert.AreEqual(100, paired[3]);
            // A paired decoy reads its target's value even when the target has no row in this run.
            var targetMissing = new Run().Target(3, 5).Decoy(1, 1).Column(1, false);
            Assert.AreEqual(5, targetMissing[1]);
            // Own: the decoy reads decoy siblings only; the targets are unchanged.
            var own = run.Column(1, true);
            Assert.AreEqual(5, own[0]);
            Assert.AreEqual(100, own[1]);
            Assert.AreEqual(3, own[2], @"own decoy reads the decoy of B");
            Assert.AreEqual(1, own[3], @"own decoy reads the decoy of A");
            // Own, and no decoy sibling, reads the floor rather than target evidence.
            var ownAlone = new Run().Target(3, 5).Decoy(1, 1).Column(1, true);
            Assert.AreEqual(1, ownAlone[1], @"the floor of the one decoy precursor");
        }

        private static void AssertFloorIsExactMedianOfDecoyPrecursors()
        {
            // A precursor counts once at its best peak: 1 and 9 (from 2 and 9), then 20.
            var odd = new Run().Decoy(1, 1).Decoy(3, 2).Decoy(3, 9).Decoy(4, 20);
            Assert.AreEqual(9, ProteinBestFeature.DecoyMedianFloor(odd.BaseIds, odd.IsDecoys, odd.Scores));
            var even = new Run().Decoy(1, 1).Decoy(3, 3).Decoy(4, 20).Decoy(5, 21);
            Assert.AreEqual(11.5, ProteinBestFeature.DecoyMedianFloor(even.BaseIds, even.IsDecoys, even.Scores));
            // A run with no decoy has no median, and takes its lowest score.
            var noDecoy = new Run().Target(1, 4).Target(3, -2);
            Assert.AreEqual(-2, ProteinBestFeature.DecoyMedianFloor(noDecoy.BaseIds, noDecoy.IsDecoys, noDecoy.Scores));
            var empty = new Run();
            Assert.AreEqual(0, ProteinBestFeature.DecoyMedianFloor(empty.BaseIds, empty.IsDecoys, empty.Scores));
        }

        private static void AssertShuffleKeepsGroupSizes()
        {
            var precursors = RandomPrecursors(new Random(17), 400, 60);
            var plain = ProteinBestMembership.Build(precursors);
            var shuffled = ProteinBestMembership.Build(precursors, 42UL);
            CollectionAssert.AreEqual(ProteinSizes(plain), ProteinSizes(shuffled), @"every protein keeps its peptide count");
            Assert.AreEqual(plain.PeptideCount, shuffled.PeptideCount);
            Assert.AreEqual(plain.ProteinCount, shuffled.ProteinCount);
            // A peptide's charge states stay one peptide; the assignment moved for most of them.
            Assert.AreEqual(shuffled.PeptideOf(1), shuffled.PeptideOf(2));
            int moved = 0;
            for (int p = 0; p < plain.PeptideCount; p++)
            {
                if (!plain.ProteinsOf(p).SequenceEqual(shuffled.ProteinsOf(p)))
                    moved++;
            }
            Assert.IsTrue(moved > plain.PeptideCount / 2, @"the permutation moved " + moved);
            // Seeded: the same seed the same permutation, another seed another.
            var again = ProteinBestMembership.Build(precursors, 42UL);
            var other = ProteinBestMembership.Build(precursors, 43UL);
            int otherMoved = 0;
            for (int p = 0; p < plain.PeptideCount; p++)
            {
                Assert.IsTrue(shuffled.ProteinsOf(p).SequenceEqual(again.ProteinsOf(p)));
                if (!shuffled.ProteinsOf(p).SequenceEqual(other.ProteinsOf(p)))
                    otherMoved++;
            }
            Assert.IsTrue(otherMoved > 0, @"another seed must give another permutation");
        }

        private static void AssertOrderIndependentBits()
        {
            var random = new Random(23);
            var precursors = RandomPrecursors(random, 400, 60);
            var run = new Run();
            foreach (var precursor in precursors)
            {
                // Some precursors are absent, some repeat as candidate peaks, with ties to the other side.
                if (random.Next(5) == 0)
                    continue;
                int peaks = 1 + random.Next(3);
                for (int k = 0; k < peaks; k++)
                {
                    run.Target(precursor.BaseId, Math.Round(random.NextDouble() * 20, 1));
                    run.Decoy(precursor.BaseId, Math.Round(random.NextDouble() * 20, 1));
                }
            }

            int rows = run.BaseIds.Count;
            var order = Enumerable.Range(0, rows).ToArray();
            for (int i = rows - 1; i >= 1; i--)
            {
                int j = random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            var permuted = new Run();
            foreach (int r in order)
            {
                if (run.IsDecoys[r])
                    permuted.Decoy(run.BaseIds[r], run.Scores[r]);
                else
                    permuted.Target(run.BaseIds[r], run.Scores[r]);
            }

            // The membership from the precursors reversed: its indices are canonical all the same.
            var forward = ProteinBestMembership.Build(precursors, 42UL);
            var backward = ProteinBestMembership.Build(precursors.AsEnumerable().Reverse(), 42UL);
            foreach (int topN in new[] { 1, 4, 16 })
            {
                foreach (bool decoyOwn in new[] { false, true })
                {
                    var expected = run.Column(topN, decoyOwn, forward);
                    var actual = permuted.Column(topN, decoyOwn, backward);
                    for (int s = 0; s < rows; s++)
                    {
                        Assert.AreEqual(BitConverter.DoubleToInt64Bits(expected[order[s]]),
                            BitConverter.DoubleToInt64Bits(actual[s]),
                            string.Format(@"topN {0}, own {1}, row {2}", topN, decoyOwn, s));
                    }
                }
            }
        }

        private static void AssertSettingsKey()
        {
            // --protein-best on is the maximum, the arm the OSPREY_PROTEIN_BEST=max experiment ran.
            Assert.AreEqual(1, OspreyEnvironment.PROTEIN_BEST_TOP_N);
            Assert.AreEqual(string.Empty, OspreyEnvironment.ProteinBestValidityKeySuffix(new OspreyConfig { ProteinBest = false }));

            // The key: empty when off, so no existing directory moves; every setting otherwise.
            Assert.AreEqual(string.Empty, OspreyEnvironment.ProteinBestValidityKeySuffix(0, true, true, 10));
            string max = OspreyEnvironment.ProteinBestValidityKeySuffix(1, false, false, OspreyEnvironment.PROTEIN_BEST_ROUND1_RUNS);
            Assert.AreEqual(@";proteinbest=max;proteinbestdecoy=paired;proteinbestround1runs=10", max);
            var keys = new HashSet<string>
            {
                max,
                OspreyEnvironment.ProteinBestValidityKeySuffix(4, false, false, 10),
                OspreyEnvironment.ProteinBestValidityKeySuffix(1, true, false, 10),
                OspreyEnvironment.ProteinBestValidityKeySuffix(1, false, true, 10),
                OspreyEnvironment.ProteinBestValidityKeySuffix(1, false, false, 0)
            };
            Assert.AreEqual(5, keys.Count, string.Join(@" ", keys));
        }

        private static void AssertModifiedFormsAreOnePeptide()
        {
            // Two modified forms of PEPTMIDE (1, 2) and OTHERK (3) on P1: the forms are one peptide,
            // so neither is evidence for the other, and they count once as OTHERK's siblings.
            var library = new[]
            {
                new LibraryEntry(1, @"PEPTMIDE", @"PEPTMIDE", 2, 500, 10) { ProteinIds = new[] { @"P1" } },
                new LibraryEntry(2, @"PEPTMIDE", @"PEPTM[Oxidation]IDE", 2, 508, 10) { ProteinIds = new[] { @"P1" } },
                new LibraryEntry(3, @"OTHERK", @"OTHERK", 2, 400, 12) { ProteinIds = new[] { @"P1" } },
                new LibraryEntry(1 | LibraryEntry.DECOY_ID_BIT, @"PEPTMIDE", @"PEPTMIDE", 2, 500, 10)
                    { ProteinIds = new[] { @"P1" }, IsDecoy = true }
            };
            var membership = ProteinBestMembership.FromLibrary(library);
            Assert.AreEqual(2, membership.PeptideCount, @"the two forms of PEPTMIDE are one peptide");
            // Decoy precursors 1 and 3 at 1 and 3 make a floor of 2.
            var run = new Run().Target(1, 100).Target(2, 90).Target(3, 5).Decoy(1, 1).Decoy(3, 3);
            var top1 = run.Column(1, false, membership);
            Assert.AreEqual(5, top1[0], @"PEPTMIDE reads OTHERK, not its oxidized form");
            Assert.AreEqual(5, top1[1], @"the oxidized form reads OTHERK, not PEPTMIDE");
            Assert.AreEqual(100, top1[2], @"OTHERK reads the best form of PEPTMIDE");
            var top2 = run.Column(2, false, membership);
            Assert.AreEqual((100 + 2) / 2.0, top2[2], TOLERANCE, @"PEPTMIDE counts once, padded with the floor");
        }

        private static int[] ProteinSizes(ProteinBestMembership membership)
        {
            var sizes = new int[membership.ProteinCount];
            for (int p = 0; p < membership.PeptideCount; p++)
            {
                foreach (int protein in membership.ProteinsOf(p))
                    sizes[protein]++;
            }
            return sizes;
        }

        /// <summary>
        /// Peptides of two charges each, on one to three of the proteins, as precursors with base_ids
        /// from 1. A peptide's charges have their accessions in differing orders, as a library's may.
        /// </summary>
        private static List<ProteinBestMembership.Precursor> RandomPrecursors(Random random, int peptides, int proteins)
        {
            var precursors = new List<ProteinBestMembership.Precursor>(peptides * 2);
            uint baseId = 1;
            for (int p = 0; p < peptides; p++)
            {
                var accessions = Enumerable.Range(0, 1 + random.Next(3))
                    .Select(_ => @"PROT" + random.Next(proteins)).ToArray();
                string peptide = @"PEP" + p;
                precursors.Add(new ProteinBestMembership.Precursor(baseId++, peptide, accessions));
                precursors.Add(new ProteinBestMembership.Precursor(baseId++, peptide, accessions.Reverse().ToArray()));
            }
            return precursors;
        }
    }
}
