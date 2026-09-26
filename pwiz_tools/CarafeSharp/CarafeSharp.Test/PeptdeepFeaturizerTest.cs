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
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.Models;

namespace pwiz.CarafeSharp.Test
{
    /// <summary>
    /// Checks the AlphaPeptDeep input featurization against values worked out by hand from
    /// alphabase's modification compositions and peptdeep's featurizer rules.
    /// </summary>
    [TestClass]
    public class PeptdeepFeaturizerTest
    {
        [TestMethod]
        public void TestModificationFeatures()
        {
            AssertFeature(@"Carbamidomethyl@C", (@"C", 2), (@"H", 3), (@"N", 1), (@"O", 1));
            AssertFeature(@"Oxidation@M", (@"O", 1));
            AssertFeature(@"Phospho@S", (@"H", 1), (@"O", 3), (@"P", 1));
            AssertFeature(@"Deamidated@N", (@"H", -1), (@"N", -1), (@"O", 1));
            // Isotope labels have their own slots after the elements.
            AssertFeature(@"TMT6plex@K", (@"H", 20), (@"C", 8), (@"13C", 4), (@"N", 1), (@"15N", 1), (@"O", 2));
            // The space-to-underscore alias alphabase registers resolves to the same row.
            CollectionAssert.AreEqual(PeptdeepFeaturizer.GetModFeature(@"Acetyl@Protein N-term"),
                PeptdeepFeaturizer.GetModFeature(@"Acetyl@Protein_N-term"));
            // An unknown modification contributes nothing, as in peptdeep.
            Assert.IsTrue(PeptdeepFeaturizer.GetModFeature(@"NotAModification@X").All(v => v == 0));
            Assert.AreEqual(109, PeptdeepConstants.MOD_FEATURE_SIZE);
        }

        [TestMethod]
        public void TestResidueAndModificationMasses()
        {
            // alphabase residue masses, computed from its amino_acid.yaml formulas.
            Assert.AreEqual(71.0371137851, AlphabaseMasses.GetResidueMass('A'), 1e-9);
            Assert.AreEqual(103.0091849595, AlphabaseMasses.GetResidueMass('C'), 1e-9);
            Assert.AreEqual(156.1011110240, AlphabaseMasses.GetResidueMass('R'), 1e-9);
            Assert.AreEqual(18.01056468403, AlphabaseMasses.H2O, 1e-10);
            Assert.AreEqual(57.0214637207, ModificationTable.Get(@"Carbamidomethyl@C").Mass, 1e-9);
            Assert.AreEqual(15.9949146196, ModificationTable.Get(@"Oxidation@M").Mass, 1e-9);
            // Oxidation's side-chain loss is below the importance level alphabase keeps.
            Assert.AreEqual(0, ModificationTable.Get(@"Oxidation@M").GetModLossMass(), 0);
            Assert.AreEqual(97.9768955734, ModificationTable.Get(@"Phospho@S").GetModLossMass(), 1e-9);
        }

        [TestMethod]
        public void TestPositionsAndScalars()
        {
            var peptides = new[]
            {
                new PeptideForm(@"ACDK", new[] { @"Acetyl@Protein N-term", @"Carbamidomethyl@C" }, new[] { 0, 2 }),
                new PeptideForm(@"MCDK", new[] { @"Oxidation@M" }, new[] { 1 }),
            };
            using (var aa = PeptdeepFeaturizer.AaIndices(peptides))
            {
                CollectionAssert.AreEqual(new long[] { 2, 6 }, aa.shape);
                CollectionAssert.AreEqual(new long[] { 0, 1, 3, 4, 11, 0, 0, 13, 3, 4, 11, 0 }, aa.data<long>().ToArray());
            }
            using (var mods = PeptdeepFeaturizer.ModFeatures(peptides))
            {
                CollectionAssert.AreEqual(new long[] { 2, 6, 109 }, mods.shape);
                float[] values = mods.data<float>().ToArray();
                // Acetyl at site 0 lands on the N-terminal pad token: C(2) H(2) O(1).
                Assert.AreEqual(2f, values[Index(0, 0, 0)]);
                Assert.AreEqual(2f, values[Index(0, 0, 1)]);
                Assert.AreEqual(1f, values[Index(0, 0, 3)]);
                // Carbamidomethyl on residue 2.
                Assert.AreEqual(3f, values[Index(0, 2, 1)]);
                // Oxidation on residue 1 of the second peptide.
                Assert.AreEqual(1f, values[Index(1, 1, 3)]);
                Assert.AreEqual(0f, values[Index(1, 0, 3)]);
            }
            using (var charges = PeptdeepFeaturizer.Charges(new[] { 2, 3 }))
                CollectionAssert.AreEqual(new[] { 2 * 0.1f, 3 * 0.1f }, charges.data<float>().ToArray());
            using (var instruments = PeptdeepFeaturizer.InstrumentIndices(new[] { @"Eclipse", @"Exploris", @"Astral", @"Stellar" }))
            {
                // Eclipse and Astral are Lumos (1), Exploris is QE (0), anything unlisted is Lumos.
                CollectionAssert.AreEqual(new long[] { 1, 0, 1, 1 }, instruments.data<long>().ToArray());
            }
            Assert.ThrowsException<ArgumentException>(() => PeptdeepFeaturizer.AaIndices(
                new[] { new PeptideForm(@"PEPTIDE"), new PeptideForm(@"PEPTIDES") }));
        }

        private static int Index(int batch, int position, int feature)
        {
            return (batch * 6 + position) * 109 + feature;
        }

        private static void AssertFeature(string modName, params (string Element, int Count)[] expected)
        {
            double[] feature = PeptdeepFeaturizer.GetModFeature(modName);
            var want = new double[PeptdeepConstants.MOD_FEATURE_SIZE];
            foreach (var (element, count) in expected)
                want[Array.IndexOf(PeptdeepConstants.MOD_ELEMENTS, element)] = count;
            CollectionAssert.AreEqual(want, feature, modName);
        }
    }
}
