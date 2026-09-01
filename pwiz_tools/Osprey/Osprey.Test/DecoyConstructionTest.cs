/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Scoring;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// How a decoy spectrum is CONSTRUCTED, as opposed to which sequence is chosen.
    /// Two rules are pinned here, both of which moved entrapment-measured FDP by
    /// multiples and neither of which any other test can see:
    ///
    /// 1. A decoy fragment keeps its target's ion type and ordinal. The intensity is
    ///    copied, so relabelling b as y (which Osprey did until this was measured) puts
    ///    y-like intensities on b ions and inverts the decoy's intensity structure
    ///    relative to any real peptide. Entrapment-measured FDP at a claimed 1% q was
    ///    10.9% on Stellar with the swap and 1.5% without.
    /// 2. A candidate whose theoretical ladder nearly coincides with its target's is
    ///    rejected, and the cycling fallback supplies another. Such a decoy cannot lose
    ///    the target/decoy competition on fragment evidence, so it is not an honest null.
    ///
    /// The Rust implementation carries the same two rules and the same numbers, so a
    /// change here that is not mirrored there will also break cross-impl parity.
    /// </summary>
    [TestClass]
    public class DecoyConstructionTest
    {
        private const double TOLERANCE = 1e-9;

        /// <summary>
        /// Isobaric pair: the internal residues of AILLAK reverse to ALLIA, and because
        /// I and L have identical residue masses every b and y rung of ALLIAK coincides
        /// exactly with AILLAK's. Different string, same spectrum: the case the gate
        /// exists for.
        /// </summary>
        private const string ISOBARIC_TARGET = "AILLAK";
        private const string ISOBARIC_REVERSAL = "ALLIAK";

        [TestMethod]
        public void DecoyFragmentKeepsIonTypeAndOrdinal()
        {
            var generator = new DecoyGenerator(Enzyme.Trypsin);
            var target = new LibraryEntry(1, @"PEPTIDEK", @"PEPTIDEK", 2, 500.0, 10.0);
            target.Fragments = new[]
            {
                Fragment(300.0, 1.0f, IonType.B, 3),
                Fragment(400.0, 0.5f, IonType.Y, 4)
            };

            var decoy = generator.Generate(target);
            Assert.AreEqual(@"EDITPEPK", decoy.Sequence);
            Assert.AreEqual(2, decoy.Fragments.Count);

            // Same ion type, same ordinal, same relative intensity. A b3 that came back
            // as y5 would be the swap returning.
            Assert.AreEqual(IonType.B, decoy.Fragments[0].Annotation.IonType);
            Assert.AreEqual(3, decoy.Fragments[0].Annotation.Ordinal);
            Assert.AreEqual(1.0f, decoy.Fragments[0].RelativeIntensity);
            Assert.AreEqual(IonType.Y, decoy.Fragments[1].Annotation.IonType);
            Assert.AreEqual(4, decoy.Fragments[1].Annotation.Ordinal);
            Assert.AreEqual(0.5f, decoy.Fragments[1].RelativeIntensity);

            // Only the m/z is recomputed, and it is the decoy sequence's own b3 / y4,
            // not the target's. Reading them off the ladder keeps this test honest about
            // WHICH masses are expected rather than restating the implementation.
            double[] decoyLadder = DecoyGenerator.TheoreticalLadder(@"EDITPEPK");
            Assert.AreEqual(SinglyChargedB(decoyLadder, 3), decoy.Fragments[0].Mz, TOLERANCE);
            Assert.AreEqual(SinglyChargedY(decoyLadder, 4), decoy.Fragments[1].Mz, TOLERANCE);
        }

        [TestMethod]
        public void OverlapGateRejectsAnIsobaricNearDuplicate()
        {
            // Rejected: every rung coincides, so the ratio is 1.0 against a 0.4 budget.
            Assert.IsFalse(DecoyGenerator.IsCandidateAcceptable(ISOBARIC_TARGET, ISOBARIC_REVERSAL));

            // Accepted: an ordinary tryptic reversal. Without this half the test would
            // pass just as well with a gate that rejected everything, which would drop
            // every peptide from the search.
            Assert.IsTrue(DecoyGenerator.IsCandidateAcceptable(@"PEPTIDEK", @"EDITPEPK"));
        }

        [TestMethod]
        public void GenerationNeverEmitsARejectedCandidate()
        {
            // The end-to-end consequence of the gate: whatever sequence the fallback
            // settles on, it is not the isobaric twin the plain reversal would have
            // produced. Asserted against the generator rather than the predicate so a
            // gate that was never wired into generation would fail here.
            var config = new OspreyConfig();
            var targets = new List<LibraryEntry>
            {
                new LibraryEntry(1, ISOBARIC_TARGET, ISOBARIC_TARGET, 2, 500.0, 10.0)
            };
            // Fragments are required: GenerateAllWithCollisionDetection SKIPS a fragment-less
            // entry outright, so without these the assertion below would hold over an EMPTY
            // decoy list and pass no matter what the gate did.
            foreach (var t in targets)
                t.Fragments = new[] { Fragment(300.0, 1.0f, IonType.B, 3) };

            var decoys = DecoyGenerator.GenerateAllWithCollisionDetection(
                targets, config, null, false, out _);

            Assert.AreEqual(1, decoys.Count, @"the cycling fallback should still supply a decoy");
            Assert.IsFalse(decoys.Any(d => d.Sequence == ISOBARIC_REVERSAL),
                @"The fragment-overlap gate must reject the isobaric reversal");

            // SCOPE LIMIT, stated so this test is not read as more than it is. Since the I/L
            // collision check landed, THIS fixture no longer exercises the overlap gate at
            // generation time: ISOBARIC_TARGET and ISOBARIC_REVERSAL both normalise to the same
            // I/L form, so the collision check rejects the reversal first and short-circuits
            // past IsCandidateAcceptable. What this test still proves is that generation emits
            // neither - which is its stated purpose - and the overlap gate's own behaviour is
            // covered by the predicate assertions above.
            //
            // A fixture that reaches the overlap gate at generation time needs a reversal with
            // ladder overlap > 0.40 that is NOT an I/L twin, and that is not trivial to build:
            // for a C-terminus-preserving reversal, high overlap essentially requires a
            // near-palindrome or an isobaric-symmetric sequence. Worth adding with the
            // decoy-gate work rather than guessed at here.
            Assert.IsFalse(DecoyGenerator.IsCandidateAcceptable(ISOBARIC_TARGET, ISOBARIC_REVERSAL),
                @"the overlap gate must independently reject the isobaric reversal");
        }

        [TestMethod]
        public void CollisionCheckRejectsADecoyIsobaricToADifferentTarget()
        {
            // AAEESLR reverses to LSEEAAR, which is I/L-isobaric to the SECOND target below -
            // one of the 742 real collisions measured over the Astral target set.
            const string collider = "AAEESLR";
            const string reversal = "LSEEAAR";
            const string twin = "ISEEAAR";

            // The property that makes this worth its own check: the fragment-overlap gate
            // PASSES this candidate. That gate compares a candidate to its OWN source target,
            // and against AAEESLR the ladder of LSEEAAR is ordinary. The collision is with a
            // DIFFERENT target, which that gate never looks at - so the two are independent
            // rather than redundant, and an exact-string audit reports 0 here.
            Assert.IsTrue(DecoyGenerator.IsCandidateAcceptable(collider, reversal));

            var config = new OspreyConfig();
            var targets = new List<LibraryEntry>
            {
                new LibraryEntry(1, collider, collider, 2, 500.0, 10.0),
                new LibraryEntry(2, twin, twin, 2, 500.0, 10.0)
            };
            // Fragments are required: GenerateAllWithCollisionDetection SKIPS a fragment-less
            // entry outright, which would make the rejection assertion below pass vacuously.
            foreach (var t in targets)
                t.Fragments = new[] { Fragment(300.0, 1.0f, IonType.B, 3) };

            var decoys = DecoyGenerator.GenerateAllWithCollisionDetection(
                targets, config, null, false, out _);

            Assert.IsFalse(decoys.Any(d => d.Sequence == reversal),
                @"A decoy I/L-isobaric to a real target is not a valid null and must be rejected");

            // The cycling fallback must still supply decoys. Without this the test would pass
            // just as well with a check that rejected every candidate.
            Assert.AreEqual(2, decoys.Count, @"both targets should still receive a decoy");
        }

        [TestMethod]
        public void LadderDropsOnlyTheIonsSpanningAnUnknownResidue()
        {
            // Selenocysteine (U) is absent from the standard residue table and is real in
            // UniProt-derived libraries (GPX1, GPX4, SELENOP, TXNRD1). A LEADING unknown
            // is the case that matters: deriving y ions as (total - prefix) makes total
            // NaN and empties the whole ladder, and an empty ladder is read as "accept",
            // which silently switches the gate off for exactly those peptides.
            double[] leading = DecoyGenerator.TheoreticalLadder(@"UPEPTIDEK");
            Assert.AreEqual(8, leading.Length,
                @"Every b ion spans the leading U, but all eight y ions survive");

            // An interior unknown drops the ions that span it from both series, and only
            // those: b1-b3 and y1-y4 of PEPUIDEK never cross position 4.
            double[] interior = DecoyGenerator.TheoreticalLadder(@"PEPUIDEK");
            Assert.AreEqual(7, interior.Length);

            // Control: a clean sequence yields the full 2*(n-1) ladder.
            Assert.AreEqual(14, DecoyGenerator.TheoreticalLadder(@"PEPTIDEK").Length);

            // Too short to have any cleavage site at all.
            Assert.AreEqual(0, DecoyGenerator.TheoreticalLadder(@"K").Length);
        }

        private static LibraryFragment Fragment(double mz, float intensity, IonType ionType, byte ordinal)
        {
            return new LibraryFragment
            {
                Mz = mz,
                RelativeIntensity = intensity,
                Annotation = new FragmentAnnotation { IonType = ionType, Ordinal = ordinal, Charge = 1 }
            };
        }

        /// <summary>
        /// The ladder interleaves b and y for each cleavage site, so b{ordinal} sits at
        /// 2*(ordinal-1) and y{ordinal} one past it. Valid only for a sequence with no
        /// unknown residues, where no rung is dropped.
        /// </summary>
        private static double SinglyChargedB(double[] ladder, int ordinal)
        {
            return ladder[2 * (ordinal - 1)];
        }

        private static double SinglyChargedY(double[] ladder, int ordinal)
        {
            return ladder[2 * (ordinal - 1) + 1];
        }
    }
}
