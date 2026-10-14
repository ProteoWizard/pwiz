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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.CarafeSharp.Proteome;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Checks the entrapment and decoy sequence generators, the similarity gate and the
    /// modified-precursor masses against values printed by Carafe's own Java classes.
    /// </summary>
    [TestClass]
    public class EntrapmentSequencesTest
    {
        [TestMethod]
        public void TestShuffleReverseCycle()
        {
            // Shuffle with seed 42 attempt 0, seed 42 attempt 5, and seed 24 attempt 0.
            AssertShuffles(@"PEPTIDEK", @"EEPPDTIK", @"ETDIPPEK", @"DETEPIPK");
            AssertShuffles(@"AAAAAAAAAAAAAAAAGATCLER", @"AEAAAAAAGAATAAAALAAAACR", @"AALAAAAAAAGATAACEAAAAAR", @"ACTAAAAAAAAAAAALAAAGAER");
            AssertShuffles(@"ELVISLIVESK", @"EVEVLLSIISK", @"ELLIESVSVIK", @"LLEISSIVEVK");
            AssertShuffles(@"ACDEFGHIKLMNPQRSTVWY", @"LTIEFMHWCRQDSPVGKNAY", @"VTWMLEFGSCDAKHIRQPNY", @"DRCKNSMLIGWPTHQAFEVY");
            AssertShuffles(@"LMDLIGDR", @"DLIGLDMR", @"GDLMIDLR", @"IMGLLDDR");
            AssertShuffles(@"MK", @"MK", @"MK", @"MK");

            // Reversal, then rotation by 1, 3 and 25 (modulo the length before the C terminus).
            AssertReverseAndCycles(@"PEPTIDEK", @"EDITPEPK", @"EPTIDEPK", @"TIDEPEPK", @"IDEPEPTK");
            AssertReverseAndCycles(@"AAAAAAAAAAAAAAAAGATCLER", @"ELCTAGAAAAAAAAAAAAAAAAR",
                @"AAAAAAAAAAAAAAAGATCLEAR", @"AAAAAAAAAAAAAGATCLEAAAR", @"AAAAAAAAAAAAAGATCLEAAAR");
            AssertReverseAndCycles(@"ACDEFGHIKLMNPQRSTVWY", @"WVTSRQPNMLKIHGFEDCAY",
                @"CDEFGHIKLMNPQRSTVWAY", @"EFGHIKLMNPQRSTVWACDY", @"HIKLMNPQRSTVWACDEFGY");
            AssertReverseAndCycles(@"MK", @"MK", @"MK", @"MK", @"MK");
        }

        [TestMethod]
        public void TestDecoyAndEntrapmentGeneration()
        {
            var targets = new HashSet<string>
            {
                @"LMDLIGDR", @"DGLLDMLR", @"PEPTIDEK", @"ELVISLIVESK", @"SEVLLSIVLEK", @"AAAAAAAAAAAAAAAAGATCLER",
                @"LEVELAK", @"GGGGGGGGKAAAAAAAAK", @"AAAAAAAAKGGGGGGGGGR",
            };
            var targetsIl = EntrapmentSequences.IlNormalizedSet(targets);
            // Gated decoys reject an I/L twin of a target (DGILDMLR, SEVILSIVLEK) and fall back to a
            // rotation; ungated ones reject only an exact target.
            AssertGenerated(@"LMDLIGDR", targets, targetsIl, @"DLIGDLMR", @"DGILDMLR", @"DLIGLDMR", @"DLIGLDMR");
            AssertGenerated(@"ELVISLIVESK", targets, targetsIl, @"LVISLIVESEK", @"SEVILSIVLEK", @"EVEVLLSIISK", @"EVEVLLSIISK");
            // The gate rejects the first shuffle as too similar, so a retry is used.
            AssertGenerated(@"LEVELAK", targets, targetsIl, @"ALEVELK", @"ALEVELK", @"LLEAEVK", @"ELVLEAK");
            AssertGenerated(@"EDITPEPK", targets, targetsIl, @"DITPEPEK", @"DITPEPEK", @"PDTPEEIK", @"TIDPEEPK");
            // Every rotation of a repeat overlaps its target too much.
            AssertGenerated(@"ASASASASK", targets, targetsIl, null, @"SASASASAK", @"SASSSAAAK", @"AASSSASAK");
            // No permutation of a repeat differs from it.
            AssertGenerated(@"AAAK", targets, targetsIl, null, null, null, null);
            AssertGenerated(@"KAAAAAAAAK", targets, targetsIl, @"AAAAAAAAKK", @"AAAAAAAAKK", @"AAAAAAAKAK", @"AAAAAAAKAK");
            Assert.AreEqual(@"LLLLLK", EntrapmentSequences.IlNormalize(@"LIILIK"));
        }

        [TestMethod]
        public void TestSimilarityGate()
        {
            AssertOverlap(@"EIVELEK", @"EEVEILK", 0.3333333333333333, true);
            // Isobaric I/L: the ladders coincide completely.
            AssertOverlap(@"LMDLIGDR", @"IMDLLGDR", 1.0, false);
            AssertOverlap(@"PEPTIDEK", @"EDITPEPK", 0.14285714285714285, true);
            AssertOverlap(@"PEPTIDEK", @"TPEPIDEK", 0.5714285714285714, false);
            // An unknown residue removes only the ions spanning it.
            AssertOverlap(@"PEPTIDEK", @"PEPXIDEK", 1.0, false);
            AssertOverlap(@"XPEPTIDEK", @"PEPTIDEXK", 0.125, true);
            AssertOverlap(@"K", @"K", 0.0, true);

            CollectionAssert.AreEqual(new[]
            {
                98.06003600000001, 147.11279599999997, 227.102626, 276.15538599999996, 324.15538599999996, 391.182326,
                425.203066, 504.266386, 538.2871260000001, 605.3140660000001, 653.314066, 702.3668260000002, 782.356656,
                831.4094160000002,
            }, DecoySimilarityGate.TheoreticalLadder(@"PEPTIDEK"));
            CollectionAssert.AreEqual(new[]
            {
                98.06003600000001, 147.11279599999997, 227.102626, 276.15538599999996, 324.15538599999996, 391.182326,
                504.266386,
            }, DecoySimilarityGate.TheoreticalLadder(@"PEPXIDEK"));
            Assert.AreEqual(927.4549000000001, ResidueMasses.PeptideNeutralMass(@"PEPTIDEK"));
            Assert.IsNull(ResidueMasses.PeptideNeutralMass(@"PEPUIDEK"));
        }

        [TestMethod]
        public void TestModifiedPrecursorMasses()
        {
            // compomics Peptide.getMass() for Carafe's default peptidoforms, printed by Java.
            const double ox = 15.99491461956;
            const double cam = 57.02146372057;
            Assert.AreEqual(927.45492704071, CompomicsMasses.PeptideMass(@"PEPTIDEK", new double[0]));
            Assert.AreEqual(744.2427091538, CompomicsMasses.PeptideMass(@"MCMCK", new[] { ox, cam, cam }));
            Assert.AreEqual(728.24779453424, CompomicsMasses.PeptideMass(@"MCMCK", new[] { cam, cam }));
            Assert.AreEqual(2467.14128516526, CompomicsMasses.PeptideMass(@"ACDEFGHIKLMNPQRSTVWY", new[] { ox, cam }));
            Assert.AreEqual(2451.1463705457, CompomicsMasses.PeptideMass(@"ACDEFGHIKLMNPQRSTVWY", new[] { cam }));
            Assert.AreEqual(1210.42432162118, CompomicsMasses.PeptideMass(@"MMMMMMMMK", new[] { ox }));
            Assert.AreEqual(1194.4294070016201, CompomicsMasses.PeptideMass(@"MMMMMMMMK", new double[0]));
            Assert.AreEqual(937.69397553761, CompomicsMasses.PeptideMass(@"LLLLLLLK", new double[0]));
            Assert.AreEqual(1055.5135045459901, CompomicsMasses.PeptideMass(@"QPEPTIDEK", new double[0]));

            // MMMMMMMMK is 598.2 unmodified and 606.2 oxidized at charge 2.
            var nTerm = new HashSet<string>();
            Assert.IsTrue(MzFilter(@"1", @"2", nTerm, 600, 700).Fits(@"MMMMMMMMK"));
            Assert.IsFalse(MzFilter(@"1", @"0", nTerm, 600, 700).Fits(@"MMMMMMMMK"));
            Assert.IsFalse(MzFilter(@"1", @"no", nTerm, 600, 700).Fits(@"MMMMMMMMK"));
            // Protein N-term acetylation (id 5) applies only to a recorded protein N terminus.
            Assert.IsFalse(MzFilter(@"0", @"5", nTerm, 480, 490).Fits(@"PEPTIDEK"));
            nTerm.Add(@"PEPTIDEK");
            Assert.IsTrue(MzFilter(@"0", @"5", nTerm, 480, 490).Fits(@"PEPTIDEK"));
            // CCK is 177.1 at charge 2 unmodified, 234.1 with the default fixed Carbamidomethyl C.
            Assert.IsFalse(MzFilter(@"1", @"2", nTerm, 170, 180).Fits(@"CCK"));
            Assert.IsTrue(MzFilter(@"0", @"2", nTerm, 170, 180).Fits(@"CCK"));
        }

        private static ModifiedPrecursorMzFilter MzFilter(string fixedMods, string variableMods, ISet<string> nTerm,
            double minMz, double maxMz)
        {
            var settings = new ModificationSettings { FixedModifications = fixedMods, VariableModifications = variableMods };
            return new ModifiedPrecursorMzFilter(settings, new[] { 2 }, minMz, maxMz, nTerm);
        }

        private static void AssertShuffles(string sequence, string seed42, string seed42Attempt5, string seed24)
        {
            Assert.AreEqual(seed42, EntrapmentSequences.ShufflePreservingCterm(sequence, 42));
            Assert.AreEqual(seed42Attempt5, EntrapmentSequences.ShufflePreservingCterm(sequence, 42, 5));
            Assert.AreEqual(seed24, EntrapmentSequences.ShufflePreservingCterm(sequence, 24));
        }

        private static void AssertReverseAndCycles(string sequence, string reversed, string cycle1, string cycle3, string cycle25)
        {
            Assert.AreEqual(reversed, EntrapmentSequences.ReversePreservingCterm(sequence));
            Assert.AreEqual(cycle1, EntrapmentSequences.CyclePreservingCterm(sequence, 1));
            Assert.AreEqual(cycle3, EntrapmentSequences.CyclePreservingCterm(sequence, 3));
            Assert.AreEqual(cycle25, EntrapmentSequences.CyclePreservingCterm(sequence, 25));
        }

        private static void AssertGenerated(string sequence, ISet<string> targets, ISet<string> targetsIl,
            string gatedDecoy, string ungatedDecoy, string gatedEntrapment, string ungatedEntrapment)
        {
            Assert.AreEqual(gatedDecoy, EntrapmentSequences.GenerateReverseDecoy(sequence, targets, targetsIl, true), sequence);
            Assert.AreEqual(ungatedDecoy, EntrapmentSequences.GenerateReverseDecoy(sequence, targets, targetsIl, false), sequence);
            Assert.AreEqual(gatedEntrapment, EntrapmentSequences.GenerateShuffledEntrapment(sequence, 42, targets, targetsIl, true), sequence);
            Assert.AreEqual(ungatedEntrapment, EntrapmentSequences.GenerateShuffledEntrapment(sequence, 42, targets, targetsIl, false), sequence);
        }

        private static void AssertOverlap(string target, string candidate, double overlap, bool acceptable)
        {
            Assert.AreEqual(overlap, DecoySimilarityGate.FragmentOverlap(target, candidate), target + @"/" + candidate);
            Assert.AreEqual(acceptable, DecoySimilarityGate.IsCandidateAcceptable(target, candidate), target + @"/" + candidate);
        }
    }
}
