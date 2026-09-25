/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The b/y ladder the training export writes: AlphaPeptDeep's slot order, applicability by
    /// precursor charge, m/z from the shared fragment mass code, and the mapping from a library
    /// annotation back onto a slot. The slot order is a contract with CarafeSharp, so it is
    /// pinned by position rather than derived from the code under test.
    /// </summary>
    [TestClass]
    public class FragmentLadderTest
    {
        private const double PROTON = PeptideFragmentMass.PROTON_MASS;
        private const double WATER = PeptideFragmentMass.H2O_MASS;

        [TestMethod]
        public void TestFragmentLadder()
        {
            AssertSlotLayout();
            AssertMzValues();
            AssertApplicabilityFollowsPrecursorCharge();
            AssertModificationsShiftTheIonsThatCarryThem();
            AssertStackedModificationsAdd();
            AssertAnnotationsMapOntoSlots();
            AssertOrdinalPastTheEndHasNoMz();
            // A null or one-residue sequence has no cleavage and no slots.
            Assert.AreEqual(0, FragmentLadder.Build(null, null, 2).Length);
            Assert.AreEqual(0, FragmentLadder.Build(@"K", Array.Empty<Modification>(), 2).Length);
        }

        /// <summary>
        /// Slot <c>p*4+t</c>, <c>t</c> in b1, b2, y1, y2; position <c>p</c> holds b(p+1) and
        /// y(L-1-p).
        /// </summary>
        private static void AssertSlotLayout()
        {
            const int length = 7; // PEPTIDE
            Assert.AreEqual(24, FragmentLadder.SlotCount(length));
            Assert.AreEqual(0, FragmentLadder.SlotCount(1));
            Assert.AreEqual(0, FragmentLadder.SlotOf(0, IonType.B, 1));
            Assert.AreEqual(1, FragmentLadder.SlotOf(0, IonType.B, 2));
            Assert.AreEqual(2, FragmentLadder.SlotOf(0, IonType.Y, 1));
            Assert.AreEqual(3, FragmentLadder.SlotOf(0, IonType.Y, 2));
            Assert.AreEqual(4 * 5 + 2, FragmentLadder.SlotOf(5, IonType.Y, 1));
            for (int slot = 0; slot < FragmentLadder.SlotCount(length); slot++)
            {
                int p = slot / 4;
                int t = slot % 4;
                Assert.AreEqual(t < 2 ? IonType.B : IonType.Y, FragmentLadder.IonTypeOf(slot));
                Assert.AreEqual(t % 2 == 0 ? 1 : 2, FragmentLadder.ChargeOf(slot));
                Assert.AreEqual(t < 2 ? p + 1 : length - 1 - p, FragmentLadder.OrdinalOf(slot, length));
            }
        }

        private static void AssertMzValues()
        {
            // PEPTIDE: b1 = P + proton in slot 0; y1 = E + water + proton at the LAST position
            // (p = L-2, slot 22), since position 0 holds y6; b2 at z=2 is (P + E + 2 protons) / 2.
            double p = Residue('P'), e = Residue('E');
            var ladder = FragmentLadder.Build(@"PEPTIDE", null, 2);
            Assert.AreEqual(p + PROTON, ladder[0], 1e-9, @"b1");
            Assert.AreEqual(e + WATER + PROTON, ladder[5 * 4 + 2], 1e-9, @"y1");
            Assert.AreEqual((p + e + 2 * PROTON) / 2, ladder[4 + 1], 1e-9, @"b2 z=2");
            // The complementary pair at every position sums to the singly protonated
            // precursor plus a proton.
            double precursorMh = Residue('P') * 2 + Residue('E') * 2 + Residue('T') + Residue('I') + Residue('D') + WATER + PROTON;
            for (int pos = 0; pos < 6; pos++)
                Assert.AreEqual(precursorMh + PROTON, ladder[pos * 4] + ladder[pos * 4 + 2], 1e-9, @"b + y complement");
            // An ion spanning a residue without a standard mass is not applicable.
            var unknown = FragmentLadder.Build(@"PEXTIDE", null, 2);
            Assert.IsFalse(double.IsNaN(unknown[0]), @"b1 does not reach X");
            Assert.IsTrue(double.IsNaN(unknown[4 * 2]), @"b3 spans X");
        }

        /// <summary>A fragment charge above min(precursor charge, 2) is NaN.</summary>
        private static void AssertApplicabilityFollowsPrecursorCharge()
        {
            var singly = FragmentLadder.Build(@"PEPTIDE", null, 1);
            var triply = FragmentLadder.Build(@"PEPTIDE", null, 3);
            for (int slot = 0; slot < singly.Length; slot++)
            {
                bool z2 = FragmentLadder.ChargeOf(slot) == 2;
                Assert.AreEqual(z2, double.IsNaN(singly[slot]), @"z=1 precursor: only z=1 fragments");
                Assert.IsFalse(double.IsNaN(triply[slot]), @"z=3 precursor: every slot through z=2");
            }
        }

        private static void AssertModificationsShiftTheIonsThatCarryThem()
        {
            // +15.995 on residue 3 (T): b1..b3 unchanged, b4 onward shifted; y ions shifted
            // once they reach position 3.
            var mods = new[] { new Modification { Position = 3, MassDelta = 15.994915 } };
            var plain = FragmentLadder.Build(@"PEPTIDE", null, 2);
            var modified = FragmentLadder.Build(@"PEPTIDE", mods, 2);
            Assert.AreEqual(plain[FragmentLadder.SlotOf(2, IonType.B, 1)], modified[FragmentLadder.SlotOf(2, IonType.B, 1)], 1e-9, @"b3");
            Assert.AreEqual(plain[FragmentLadder.SlotOf(3, IonType.B, 1)] + 15.994915,
                modified[FragmentLadder.SlotOf(3, IonType.B, 1)], 1e-9, @"b4");
            Assert.AreEqual(plain[FragmentLadder.SlotOf(3, IonType.Y, 1)], modified[FragmentLadder.SlotOf(3, IonType.Y, 1)], 1e-9, @"y3");
            Assert.AreEqual(plain[FragmentLadder.SlotOf(2, IonType.Y, 1)] + 15.994915,
                modified[FragmentLadder.SlotOf(2, IonType.Y, 1)], 1e-9, @"y4");
        }

        /// <summary>
        /// An N-terminal modification and a modification of the first residue both land on
        /// position 0 - the DIA-NN loader puts <c>(UniMod:1)M(UniMod:35)</c> there, as the blib
        /// loader does <c>[+42.0]M[+16.0]</c> - so every b ion carries their sum.
        /// </summary>
        private static void AssertStackedModificationsAdd()
        {
            const string sequence = @"MPEPTIDEK";
            var mods = DiannTsvLoader.ParseModifications(@"(UniMod:1)M(UniMod:35)PEPTIDEK");
            Assert.AreEqual(2, mods.Count);
            Assert.IsTrue(mods.All(m => m.Position == 0 && m.MassDelta > 0));
            double stacked = mods.Sum(m => m.MassDelta);
            var plain = FragmentLadder.Build(sequence, null, 2);
            var modified = FragmentLadder.Build(sequence, mods, 2);
            for (int p = 0; p < sequence.Length - 1; p++)
            {
                int b = FragmentLadder.SlotOf(p, IonType.B, 1);
                Assert.AreEqual(plain[b] + stacked, modified[b], 1e-9, string.Format(@"b{0} carries both modifications", p + 1));
                int y = FragmentLadder.SlotOf(p, IonType.Y, 1);
                Assert.AreEqual(plain[y], modified[y], 1e-9, string.Format(@"y{0} does not reach residue 0", sequence.Length - 1 - p));
            }
        }

        /// <summary>A b or y ion longer than the peptide has no m/z (y used to index before the sequence).</summary>
        private static void AssertOrdinalPastTheEndHasNoMz()
        {
            var noMods = PeptideFragmentMass.ModMassesByPosition(null);
            Assert.IsNull(PeptideFragmentMass.CalculateFragmentMz(IonType.B, 8, 1, @"PEPTIDE", noMods, null));
            Assert.IsNull(PeptideFragmentMass.CalculateFragmentMz(IonType.Y, 8, 1, @"PEPTIDE", noMods, null));
            Assert.IsNotNull(PeptideFragmentMass.CalculateFragmentMz(IonType.Y, 7, 1, @"PEPTIDE", noMods, null));
        }

        private static void AssertAnnotationsMapOntoSlots()
        {
            const int length = 7;
            Assert.AreEqual(FragmentLadder.SlotOf(2, IonType.B, 1), FragmentLadder.SlotOf(Annotation(IonType.B, 3, 1), length), @"b3");
            Assert.AreEqual(FragmentLadder.SlotOf(length - 1 - 4, IonType.Y, 2), FragmentLadder.SlotOf(Annotation(IonType.Y, 4, 2), length), @"y4++");
            Assert.AreEqual(-1, FragmentLadder.SlotOf(Annotation(IonType.Y, 7, 1), length), @"y7 of a 7-mer is not a fragment");
            Assert.AreEqual(-1, FragmentLadder.SlotOf(Annotation(IonType.B, 0, 1), length), @"b0");
            Assert.AreEqual(-1, FragmentLadder.SlotOf(Annotation(IonType.Y, 3, 3), length), @"z=3 is off the ladder");
            Assert.AreEqual(-1, FragmentLadder.SlotOf(Annotation(IonType.A, 3, 1), length), @"a ions are off the ladder");
            var loss = Annotation(IonType.Y, 3, 1);
            loss.NeutralLoss = NeutralLossCode.H2O;
            Assert.AreEqual(-1, FragmentLadder.SlotOf(loss, length), @"neutral losses are off the ladder");
            // The annotation and the ladder agree on m/z for every slot.
            var ladder = FragmentLadder.Build(@"PEPTIDE", null, 2);
            for (int slot = 0; slot < ladder.Length; slot++)
            {
                var annotation = Annotation(FragmentLadder.IonTypeOf(slot), FragmentLadder.OrdinalOf(slot, length),
                    FragmentLadder.ChargeOf(slot));
                Assert.AreEqual(slot, FragmentLadder.SlotOf(annotation, length));
                double? mz = PeptideFragmentMass.CalculateFragmentMz(annotation.IonType, annotation.Ordinal,
                    annotation.Charge, @"PEPTIDE", PeptideFragmentMass.ModMassesByPosition(null), null);
                Assert.IsNotNull(mz);
                Assert.AreEqual(mz.Value, ladder[slot], 0.0);
            }
        }

        private static FragmentAnnotation Annotation(IonType ionType, int ordinal, int charge)
        {
            return new FragmentAnnotation { IonType = ionType, Ordinal = (byte)ordinal, Charge = (byte)charge };
        }

        private static double Residue(char aa)
        {
            Assert.IsTrue(PeptideFragmentMass.TryGetResidueMass(aa, out double mass));
            return mass;
        }
    }
}
