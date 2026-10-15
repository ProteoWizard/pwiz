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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Training;
using static pwiz.CarafeSharp.Test.OspreyTestRecords;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// <see cref="OspreyMaskingPolicy"/> at the edges of its rules: each threshold exactly at,
    /// just below and just above its value (the next float either side, since Osprey exports
    /// float32 evidence), ties for the top ion, ions hit by several rules, and records with no
    /// matched ions or no ladder at all. <see cref="OspreyTrainingSetTest.TestMaskingPolicy"/>
    /// covers each rule once in the middle of its range.
    /// </summary>
    [TestClass]
    public class OspreyMaskingPolicyTest
    {
        private const string PEPTIDE = @"PEPTIDEK";

        [TestMethod]
        public void TestMaskingThresholds()
        {
            TestCorrelationThreshold();
            TestLowOrdinalRule();
            TestOrdinalFloorAndTopIon();
            TestSharedPeaks();
            TestPolishOutlier();
            TestBoundarySkew();
        }

        [TestMethod]
        public void TestMaskingGatesAndDegenerateInput()
        {
            TestSpectrumGates();
            TestApplicabilityAndScanRange();
            TestPolishIntensities();
            TestDegenerateRecords();
        }

        private static void TestCorrelationThreshold()
        {
            // 0.75 is exact in float32, so the inclusive pass can be tested exactly at the threshold.
            var settings = new OspreyMaskingSettings { MinCorrelation = 0.75 };
            int b4 = BSlot(4);
            AssertCorrelation(settings, b4, 0.75f, false);
            AssertCorrelation(settings, b4, MathF.BitDecrement(0.75f), true);
            AssertCorrelation(settings, b4, MathF.BitIncrement(0.75f), false);
            // Carafe's default 0.8 is not: the float Osprey writes for 0.8 is just above it and
            // passes, and the next float down is masked.
            settings = new OspreyMaskingSettings();
            AssertCorrelation(settings, b4, 0.8f, false);
            AssertCorrelation(settings, b4, MathF.BitDecrement(0.8f), true);
            // An ion without a correlation (under 3 peak scans) cannot pass, nor can a negative one.
            AssertCorrelation(settings, b4, float.NaN, true);
            AssertCorrelation(settings, b4, -1f, true);

            // The rule reads the correlation it is asked for, and only that one.
            var record = CleanRecord(PEPTIDE, 2);
            record.CorrPolish[b4] = 0.1f;
            Assert.AreEqual(1.0, Apply(new OspreyMaskingSettings(), record).Invalid[b4]);
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings { Correlation = MaskingCorrelation.reference }, record).Invalid[b4]);
            record.CorrReference[b4] = 0.1f;
            record.CorrPolish[b4] = 0.95f;
            Assert.AreEqual(1.0, Apply(new OspreyMaskingSettings { Correlation = MaskingCorrelation.reference }, record).Invalid[b4]);
        }

        private static void TestLowOrdinalRule()
        {
            var settings = new OspreyMaskingSettings();
            int b2 = BSlot(2);
            int top = BSlot(7);
            // b2 at exactly half the top ion, correlating 0.85: the correlation rule passes it,
            // the low-ordinal rule (at least half the top, inclusive) does not.
            var record = CleanRecord(PEPTIDE, 2);
            record.ApexIntensity[top] = 1000;
            record.ApexIntensity[b2] = 500;
            record.CorrPolish[b2] = 0.85f;
            var masked = Apply(settings, record);
            Assert.AreEqual(top, masked.TopSlot);
            AssertMaskedBy(masked, b2, OspreyMaskingPolicy.RULE_LOW_ORDINAL);
            // Just under half the top ion is not intense, so the rule does not apply.
            record.ApexIntensity[b2] = MathF.BitDecrement(500f);
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b2]);
            record.ApexIntensity[b2] = 500;

            // The correlation a clean intense low-ordinal ion needs is exclusive: 0.875 (exact
            // in float32) is masked, the next float up is clean.
            var exclusive = new OspreyMaskingSettings { LowOrdinalMinCorrelation = 0.875 };
            record.CorrPolish[b2] = 0.875f;
            Assert.AreEqual(1.0, Apply(exclusive, record).Invalid[b2]);
            record.CorrPolish[b2] = MathF.BitIncrement(0.875f);
            Assert.AreEqual(0.0, Apply(exclusive, record).Invalid[b2]);
            // Carafe's 0.9 has no exact float; the float Osprey writes for it is below 0.9 and masked.
            record.CorrPolish[b2] = 0.9f;
            Assert.AreEqual(1.0, Apply(settings, record).Invalid[b2]);
            record.CorrPolish[b2] = MathF.BitIncrement(0.9f);
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b2]);

            // A one-sided boundary elevation still counts as clean; both sides is a skew, which
            // masks the ion under two rules at once.
            record.XicStart[b2] = 400;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b2]);
            record.XicEnd[b2] = 400;
            masked = Apply(settings, record);
            Assert.AreEqual(2.0, masked.Invalid[b2]);
            AssertMaskedBy(masked, b2, OspreyMaskingPolicy.RULE_SKEW, OspreyMaskingPolicy.RULE_LOW_ORDINAL);

            // The ordinal range: y2 is low-ordinal at -c_ion_min 2, y3 only at 3.
            record = CleanRecord(PEPTIDE, 2);
            record.ApexIntensity[top] = 1000;
            int y2 = YSlot(PEPTIDE, 2);
            int y3 = YSlot(PEPTIDE, 3);
            foreach (int slot in new[] { y2, y3 })
            {
                record.ApexIntensity[slot] = 900;
                record.CorrPolish[slot] = 0.85f;
            }
            masked = Apply(settings, record);
            Assert.AreEqual(1.0, masked.Invalid[y2]);
            Assert.AreEqual(0.0, masked.Invalid[y3]);
            masked = Apply(new OspreyMaskingSettings { LowOrdinalY = 3 }, record);
            Assert.AreEqual(1.0, masked.Invalid[y3]);
            // 0 turns the rule off for that ion type only.
            masked = Apply(new OspreyMaskingSettings { LowOrdinalY = 0 }, record);
            Assert.AreEqual(0.0, masked.Invalid[y2]);
            record.ApexIntensity[b2] = 900;
            record.CorrPolish[b2] = 0.85f;
            Assert.AreEqual(1.0, Apply(new OspreyMaskingSettings { LowOrdinalY = 0 }, record).Invalid[b2]);
            // The intensity fraction is a setting too.
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings { LowOrdinalIntensity = 0.95 }, record).Invalid[b2]);
        }

        private static void TestOrdinalFloorAndTopIon()
        {
            var record = CleanRecord(PEPTIDE, 2);
            int y1 = YSlot(PEPTIDE, 1);
            // The default floor masks b1 and y1 at both charges, whether matched or not.
            var masked = Apply(new OspreyMaskingSettings(), record);
            CollectionAssert.AreEqual(new[] { BSlot(1), BSlot(1, 2), y1, YSlot(PEPTIDE, 1, 2) }, InvalidSlots(masked));
            // A floor of 3 masks the ordinal 2 ions too, on top of the matched-ion rules: an
            // unclean intense b2 counts under both.
            record.CorrPolish[BSlot(2)] = 0.85f;
            masked = Apply(new OspreyMaskingSettings { MinFragmentOrdinal = 3 }, record);
            CollectionAssert.AreEqual(new[] { BSlot(1), BSlot(1, 2), BSlot(2), BSlot(2, 2),
                YSlot(PEPTIDE, 2), YSlot(PEPTIDE, 2, 2), y1, YSlot(PEPTIDE, 1, 2) }, InvalidSlots(masked));
            Assert.AreEqual(8L, masked.MaskedBy[OspreyMaskingPolicy.RULE_ORDINAL]);
            AssertMaskedBy(masked, BSlot(2), OspreyMaskingPolicy.RULE_LOW_ORDINAL, OspreyMaskingPolicy.RULE_ORDINAL);
            record.CorrPolish[BSlot(2)] = 0.95f;
            // With no floor, y1 (the most intense ion here) is the top ion and nothing is masked.
            masked = Apply(new OspreyMaskingSettings { MinFragmentOrdinal = 0 }, record);
            Assert.AreEqual(y1, masked.TopSlot);
            Assert.AreEqual(0, InvalidSlots(masked).Length);

            // The top ion is the most intense above the floor; an ion below it stays in the
            // spectrum, masked, at its intensity relative to the top ion, which can exceed 1.
            record.ApexIntensity[y1] = 5000;
            masked = Apply(new OspreyMaskingSettings(), record);
            int b7 = BSlot(7);
            Assert.AreEqual(b7, masked.TopSlot);
            Assert.AreEqual(5000.0 / record.ApexIntensity[b7], masked.Intensities[y1], 1e-12);
            Assert.AreEqual(1.0, masked.Invalid[y1]);
            Assert.IsNull(masked.RejectReason);

            // A tie for the most intense ion goes to the later slot.
            int b5 = BSlot(5);
            int b6 = BSlot(6);
            record.ApexIntensity[b5] = record.ApexIntensity[b6] = 2000;
            masked = Apply(new OspreyMaskingSettings(), record);
            Assert.AreEqual(b6, masked.TopSlot);
            Assert.AreEqual(1.0, masked.Intensities[b5]);
            Assert.AreEqual(1.0, masked.Intensities[b6]);
        }

        private static void TestSharedPeaks()
        {
            int b3 = BSlot(3);
            int b4 = BSlot(4);
            int b5 = BSlot(5);
            var settings = new OspreyMaskingSettings();
            // Two ions of the precursor whose observed m/z (theoretical plus error) agree within
            // 1e-4 matched one peak; just outside it they are separate peaks.
            var record = CleanRecord(PEPTIDE, 2);
            record.IonMz[b4] = record.IonMz[b3] + 0.9e-4;
            var masked = Apply(settings, record);
            AssertMaskedBy(masked, b3, OspreyMaskingPolicy.RULE_SELF_SHARED);
            AssertMaskedBy(masked, b4, OspreyMaskingPolicy.RULE_SELF_SHARED);
            record.IonMz[b4] = record.IonMz[b3] + 1.1e-4;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b4]);
            // The calibrated error decides, not the theoretical m/z.
            record.IonMz[b4] = record.IonMz[b3] + 0.5;
            record.ApexMzError[b4] = -0.5f;
            Assert.AreEqual(1.0, Apply(settings, record).Invalid[b3]);
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings { MaskSelfShared = false }, record).Invalid[b3]);
            // A chain within the tolerance pair by pair shares all three peaks.
            record.ApexMzError[b4] = 0;
            record.IonMz[b4] = record.IonMz[b3] + 0.8e-4;
            record.IonMz[b5] = record.IonMz[b4] + 0.8e-4;
            masked = Apply(settings, record);
            Assert.AreEqual(3L, masked.MaskedBy[OspreyMaskingPolicy.RULE_SELF_SHARED]);
            // An unmatched ion on the same m/z shares nothing.
            record.IonFlags[b5] &= unchecked((byte)~OspreyIonFlags.MATCHED_AT_APEX);
            record.IonMz[b4] = record.IonMz[b3] + 0.5;
            record.IonMz[b5] = record.IonMz[b3];
            masked = Apply(settings, record);
            Assert.IsFalse(masked.MaskedBy.ContainsKey(OspreyMaskingPolicy.RULE_SELF_SHARED));

            // Another confident precursor at the same apex peak: a count of 0 is no claimant.
            record = CleanRecord(PEPTIDE, 2);
            record.SharedApexCount[b4] = 1;
            Assert.AreEqual(1.0, Apply(settings, record).Invalid[b4]);
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings { MaskSharedApex = false }, record).Invalid[b4]);
            // Co-elution sharing masks only when asked, and then only for the better claimant if asked.
            record.SharedApexCount[b4] = 0;
            record.SharedCoeluteCount[b4] = 2;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b4]);
            var coelution = new OspreyMaskingSettings { MaskSharedCoelution = true };
            masked = Apply(coelution, record);
            AssertMaskedBy(masked, b4, OspreyMaskingPolicy.RULE_SHARED_COELUTION);
            coelution.SharedOnlyWhenBetterClaimant = true;
            Assert.AreEqual(0.0, Apply(coelution, record).Invalid[b4]);
            // The apex claimant flag does not stand for the co-elution one.
            record.IonFlags[b4] |= OspreyIonFlags.BETTER_CLAIMANT_APEX;
            Assert.AreEqual(0.0, Apply(coelution, record).Invalid[b4]);
            record.IonFlags[b4] |= OspreyIonFlags.BETTER_CLAIMANT_COELUTE;
            Assert.AreEqual(1.0, Apply(coelution, record).Invalid[b4]);

            // One ion broken every way at once counts once under each rule.
            record = CleanRecord(PEPTIDE, 2);
            record.SharedApexCount[b4] = 1;
            record.SharedCoeluteCount[b4] = 1;
            record.CorrPolish[b4] = 0.1f;
            record.XicStart[b4] = record.XicEnd[b4] = record.ApexIntensity[b4];
            record.PolishOutlierZ[b4] = 10;
            record.IonMz[b5] = record.IonMz[b4];
            masked = Apply(new OspreyMaskingSettings { MaskSharedCoelution = true, MaxPolishOutlierZ = 3 }, record);
            Assert.AreEqual(6.0, masked.Invalid[b4]);
            AssertMaskedBy(masked, b4, OspreyMaskingPolicy.RULE_SHARED_APEX, OspreyMaskingPolicy.RULE_SELF_SHARED,
                OspreyMaskingPolicy.RULE_SHARED_COELUTION, OspreyMaskingPolicy.RULE_CORRELATION, OspreyMaskingPolicy.RULE_SKEW,
                OspreyMaskingPolicy.RULE_POLISH_OUTLIER);
            // Masked, it still keeps its intensity, and the spectrum still trains.
            Assert.IsTrue(masked.Intensities[b4] > 0);
            Assert.IsNull(masked.RejectReason);
        }

        private static void TestPolishOutlier()
        {
            int b4 = BSlot(4);
            var record = CleanRecord(PEPTIDE, 2);
            // Off by default (an infinite limit), even for an infinite z.
            record.PolishOutlierZ[b4] = float.PositiveInfinity;
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings(), record).Invalid[b4]);
            // The limit is exclusive, and an ion without a z (NaN) passes.
            var settings = new OspreyMaskingSettings { MaxPolishOutlierZ = 3 };
            record.PolishOutlierZ[b4] = 3;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b4]);
            record.PolishOutlierZ[b4] = MathF.BitIncrement(3f);
            AssertMaskedBy(Apply(settings, record), b4, OspreyMaskingPolicy.RULE_POLISH_OUTLIER);
            record.PolishOutlierZ[b4] = float.NaN;
            Assert.AreEqual(0.0, Apply(settings, record).Invalid[b4]);
        }

        private static void TestBoundarySkew()
        {
            var settings = new OspreyMaskingSettings();
            int b4 = BSlot(4);
            int b7 = BSlot(7);
            // Every other ion is 0 at both boundaries, so the 1.5 x median limits are 0 and only
            // the fraction of the ion's own apex decides: 0.10 for an ion at least half the most
            // intense one, 0.25 below that. Both comparisons are strict.
            var record = CleanRecord(PEPTIDE, 2);
            record.ApexIntensity[b7] = 1000;
            record.ApexIntensity[b4] = 500;
            AssertSkew(settings, record, b4, 50f, false);
            AssertSkew(settings, record, b4, MathF.BitIncrement(50f), true);
            AssertSkew(settings, record, b4, 60f, true);
            // Just under half the most intense ion, the 0.25 fraction applies.
            record.ApexIntensity[b4] = MathF.BitDecrement(500f);
            AssertSkew(settings, record, b4, 60f, false);
            record.ApexIntensity[b4] = 400;
            AssertSkew(settings, record, b4, 100f, false);
            AssertSkew(settings, record, b4, MathF.BitIncrement(100f), true);
            Assert.AreEqual(0.0, Apply(new OspreyMaskingSettings { MaskBoundarySkew = false }, record).Invalid[b4]);

            // With the other ions at 10 on both boundaries the limits are 15, which an elevation
            // of 15 does not exceed, whatever its fraction of the apex.
            record = CleanRecord(PEPTIDE, 2);
            for (int slot = 0; slot < record.SlotCount; slot++)
                record.XicStart[slot] = record.XicEnd[slot] = 10;
            record.ApexIntensity[b4] = 40;
            AssertSkew(settings, record, b4, 15f, false);
            AssertSkew(settings, record, b4, MathF.BitIncrement(15f), true);
            // Only ions with an intensity to keep enter the medians: unmatched ions high at the
            // boundaries do not raise the limits.
            var unmatched = Enumerable.Range(0, record.SlotCount).Where(s => !record.Has(s, OspreyIonFlags.MATCHED_AT_APEX)).ToArray();
            Assert.AreEqual(14, unmatched.Length);
            foreach (int slot in unmatched)
                record.XicStart[slot] = record.XicEnd[slot] = 1e6f;
            AssertSkew(settings, record, b4, MathF.BitIncrement(15f), true);
        }

        private static void TestSpectrumGates()
        {
            // 14 matched ions, 10 of them valid (b1 and y1 are below the floor; b2 and y2 are
            // masked here by the low-ordinal rule), and the top ion valid.
            var record = CleanRecord(PEPTIDE, 2);
            record.CorrPolish[BSlot(2)] = record.CorrPolish[YSlot(PEPTIDE, 2)] = 0.85f;
            var masked = Apply(new OspreyMaskingSettings(), record);
            Assert.AreEqual(14, masked.MatchedCount);
            Assert.AreEqual(10, masked.ValidMatchedCount);
            // Each gate passes exactly at its count and rejects one above it.
            Assert.IsNull(Apply(new OspreyMaskingSettings { MinMatchedIons = 14 }, record).RejectReason);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, Apply(new OspreyMaskingSettings { MinMatchedIons = 15 }, record).RejectReason);
            Assert.IsNull(Apply(new OspreyMaskingSettings { MinValidIons = 10 }, record).RejectReason);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_VALID, Apply(new OspreyMaskingSettings { MinValidIons = 11 }, record).RejectReason);
            // The gates are checked in order: too few matched before too few valid before the top ion.
            record.CorrPolish[masked.TopSlot] = 0.1f;
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_TOP_ION_INVALID, Apply(new OspreyMaskingSettings(), record).RejectReason);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_VALID, Apply(new OspreyMaskingSettings { MinValidIons = 11 }, record).RejectReason);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED,
                Apply(new OspreyMaskingSettings { MinValidIons = 11, MinMatchedIons = 15 }, record).RejectReason);
            // Rejected or not, the spectrum reports its intensities and mask.
            masked = Apply(new OspreyMaskingSettings { MinMatchedIons = 15 }, record);
            Assert.AreEqual(1.0, masked.Intensities[masked.TopSlot]);
            Assert.AreEqual(1.0, masked.Invalid[masked.TopSlot]);
        }

        private static void TestApplicabilityAndScanRange()
        {
            // A charge 2 ion of a 1+ precursor is a valid zero only while its charge 1 ion has an
            // m/z; after a non-standard residue both are masked.
            var singly = CleanRecord(PEPTIDE, 1);
            int b4 = BSlot(4);
            singly.IonFlags[b4] = 0;
            var masked = Apply(new OspreyMaskingSettings(), singly);
            Assert.AreEqual(1.0, masked.Invalid[b4]);
            Assert.AreEqual(1.0, masked.Invalid[b4 + 1]);
            Assert.AreEqual(0.0, masked.Invalid[BSlot(5, 2)]);
            Assert.AreEqual(2L, masked.MaskedBy[OspreyMaskingPolicy.RULE_NOT_APPLICABLE]);
            // A 2+ precursor's charge 2 ion without an m/z is masked.
            var doubly = CleanRecord(PEPTIDE, 2);
            doubly.IonFlags[BSlot(5, 2)] = 0;
            Assert.AreEqual(1.0, Apply(new OspreyMaskingSettings(), doubly).Invalid[BSlot(5, 2)]);

            // A matched ion outside the scan window counts as matched but has no intensity to
            // keep, so the matched-ion rules (here a bad correlation) do not see it.
            doubly = CleanRecord(PEPTIDE, 2);
            int b5 = BSlot(5);
            doubly.IonFlags[b5] &= unchecked((byte)~OspreyIonFlags.IN_SCAN_RANGE);
            doubly.CorrPolish[b5] = 0.1f;
            masked = Apply(new OspreyMaskingSettings(), doubly);
            Assert.AreEqual(14, masked.MatchedCount);
            Assert.AreEqual(0.0, masked.Intensities[b5]);
            Assert.AreEqual(0.0, masked.Invalid[b5]);
            masked = Apply(new OspreyMaskingSettings { OutOfRange = OutOfRangeIons.masked }, doubly);
            AssertMaskedBy(masked, b5, OspreyMaskingPolicy.RULE_OUT_OF_RANGE);
            // Masked slots without a kept intensity: b1 and y1 at charge 2 (unmatched), and b5.
            Assert.AreEqual(3, masked.MaskedUnmatchedCount);

            // A matched flag without a positive apex intensity is not a match.
            doubly = CleanRecord(PEPTIDE, 2);
            doubly.ApexIntensity[b5] = 0;
            doubly.ApexIntensity[BSlot(6)] = float.NaN;
            masked = Apply(new OspreyMaskingSettings(), doubly);
            Assert.AreEqual(12, masked.MatchedCount);
            Assert.AreEqual(0.0, masked.Intensities[BSlot(6)]);
        }

        private static void TestPolishIntensities()
        {
            var record = CleanRecord(PEPTIDE, 2);
            for (int slot = 0; slot < record.SlotCount; slot++)
                record.PolishRowEffect[slot] = (float)Math.Log(2.0);
            int b4 = BSlot(4);
            record.PolishRowEffect[b4] = (float)Math.Log(1.0);
            var polish = new OspreyMaskingSettings { IntensitySource = TrainingIntensitySource.polish };
            // Without a median polish fit the apex intensities train, whatever is asked.
            var masked = Apply(polish, record);
            Assert.AreEqual(record.ApexIntensity[b4] / (double)record.ApexIntensity[masked.TopSlot], masked.Intensities[b4], 1e-12);
            // With one, exp(row effect) relative to the top ion's.
            record.MedianPolishFitted = true;
            masked = Apply(polish, record);
            Assert.AreEqual(0.5, masked.Intensities[b4], 1e-6);
            // A matched ion the polish left without a row effect trains as 0 and so is not a
            // valid matched ion, though it is not masked.
            record.PolishRowEffect[b4] = float.NaN;
            masked = Apply(polish, record);
            Assert.AreEqual(0.0, masked.Intensities[b4]);
            Assert.AreEqual(0.0, masked.Invalid[b4]);
            Assert.AreEqual(11, masked.ValidMatchedCount);
        }

        private static void TestDegenerateRecords()
        {
            // Nothing matched: no top ion, every intensity 0, rejected even with no minimums.
            var record = NewRecord(PEPTIDE, 2);
            var noMinimum = new OspreyMaskingSettings { MinMatchedIons = 0, MinValidIons = 0, RequireTopIonValid = false };
            var masked = Apply(noMinimum, record);
            Assert.AreEqual(-1, masked.TopSlot);
            Assert.AreEqual(0, masked.MatchedCount);
            Assert.IsTrue(masked.Intensities.All(v => v == 0));
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, masked.RejectReason);
            Assert.AreEqual(4, masked.MaskedUnmatchedCount);

            // Only ions below the ordinal floor matched: still no top ion, and nothing normalized.
            Match(record, BSlot(1), 500, 0.95f);
            Match(record, YSlot(PEPTIDE, 1), 800, 0.95f);
            masked = Apply(noMinimum, record);
            Assert.AreEqual(-1, masked.TopSlot);
            Assert.AreEqual(2, masked.MatchedCount);
            Assert.IsTrue(masked.Intensities.All(v => v == 0));
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, masked.RejectReason);

            // A dipeptide's ladder is b1 and y1 only, all below the floor.
            var dipeptide = NewRecord(@"GK", 2);
            for (int slot = 0; slot < dipeptide.SlotCount; slot++)
                Match(dipeptide, slot, 100 + slot, 0.95f);
            masked = Apply(noMinimum, dipeptide);
            Assert.AreEqual(4, masked.SlotCount);
            Assert.AreEqual(4, InvalidSlots(masked).Length);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, masked.RejectReason);

            // A single residue has no ladder at all, which is a rejection, not an exception.
            masked = Apply(new OspreyMaskingSettings(), NewRecord(@"K", 2));
            Assert.AreEqual(0, masked.SlotCount);
            Assert.AreEqual(0, masked.Intensities.Length);
            Assert.AreEqual(OspreyMaskingPolicy.REJECT_FEW_MATCHED, masked.RejectReason);
        }

        private static void AssertCorrelation(OspreyMaskingSettings settings, int slot, float correlation, bool expectMasked)
        {
            var record = CleanRecord(PEPTIDE, 2);
            record.CorrPolish[slot] = correlation;
            var masked = Apply(settings, record);
            Assert.AreEqual(expectMasked ? 1.0 : 0.0, masked.Invalid[slot], Text(correlation));
            Assert.AreEqual(expectMasked, masked.MaskedBy.ContainsKey(OspreyMaskingPolicy.RULE_CORRELATION), Text(correlation));
        }

        private static void AssertSkew(OspreyMaskingSettings settings, OspreyTrainingRecord record, int slot, float elevation, bool expectSkew)
        {
            record.XicStart[slot] = record.XicEnd[slot] = elevation;
            var masked = Apply(settings, record);
            Assert.AreEqual(expectSkew, masked.MaskedBy.ContainsKey(OspreyMaskingPolicy.RULE_SKEW), Text(elevation));
            // A one-sided elevation is never a skew.
            record.XicEnd[slot] = 0;
            Assert.IsFalse(Apply(settings, record).MaskedBy.ContainsKey(OspreyMaskingPolicy.RULE_SKEW), Text(elevation));
            record.XicEnd[slot] = elevation;
        }

        private static void AssertMaskedBy(MaskedSpectrum masked, int slot, params string[] rules)
        {
            Assert.AreEqual(rules.Length, (int)masked.Invalid[slot], @"slot " + slot);
            foreach (string rule in rules)
                Assert.IsTrue(masked.MaskedBy.ContainsKey(rule), rule);
        }

        private static MaskedSpectrum Apply(OspreyMaskingSettings settings, OspreyTrainingRecord record)
        {
            return new OspreyMaskingPolicy(settings).Apply(record);
        }

        private static int[] InvalidSlots(MaskedSpectrum masked)
        {
            return Enumerable.Range(0, masked.SlotCount).Where(s => masked.Invalid[s] > 0).ToArray();
        }

        private static string Text(float value)
        {
            return value.ToString(@"R", CultureInfo.InvariantCulture);
        }
    }
}
