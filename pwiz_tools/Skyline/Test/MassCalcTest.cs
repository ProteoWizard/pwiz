/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Unit tests of mass calculation
    /// </summary>
    [TestClass]
    public class MassCalcTest : AbstractUnitTest
    {
        /// <summary>
        /// Test the masses in the <see cref="BioMassCalc.DEFAULT_ABUNDANCES"/> object to make
        /// sure they match the base symbol mass values in <see cref="BioMassCalc"/>.
        /// </summary>
        [TestMethod]
        public void BioMassCalcAbundanceMassesTest()
        {
            foreach (var atomAbundance in BioMassCalc.DEFAULT_ABUNDANCES)
            {
                string symbol = atomAbundance.Key;
                var massDistOrdered = atomAbundance.Value.MassesSortedByAbundance();
                TestMass(symbol, massDistOrdered, 0);
                TestMass(symbol + "'", massDistOrdered, 1);
                TestMass(symbol + "\"", massDistOrdered, 2);
            }
        }

        /// <summary>
        /// Test that the mass at a specific index in a mass distribution matches the expected
        /// mass for the symbol in the <see cref="BioMassCalc.MONOISOTOPIC"/> mass calculator,
        /// or (if the mass is an unstable isotope) that the mass is approximately one Dalton
        /// heavier than the heaviest stable isotope of the element.
        /// </summary>
        /// <param name="symbol">Symbol for the atom</param>
        /// <param name="massDistOrdered">Mass distribution to test</param>
        /// <param name="indexMass">Index of the mass to test within the mass distribution</param>
        private static void TestMass(string symbol, IList<KeyValuePair<double, double>> massDistOrdered, int indexMass)
        {
            double massExpected = BioMassCalc.MONOISOTOPIC.GetMass(symbol);
            if (massExpected != 0)
            {
                if (massDistOrdered.Count > indexMass)
                {
                    double massActual = massDistOrdered[indexMass].Key;
                    Assert.AreEqual(massExpected, massActual,
                        string.Format("The mass for {0} was expected to be {1} but was found to be {2}", symbol,
                            massExpected, massActual));
                }
                else
                {
                    Assert.AreEqual(massDistOrdered.Count, indexMass);
                    var maxMass = massDistOrdered.Select(kvp => kvp.Key).Max();
                    Assert.AreEqual(maxMass + 1, massExpected, .1);
                }
            }
        }

        [TestMethod]
        public void SequenceMassCalcTest()
        {
            // Test case that caused unexpected exception when O- was not parsed correctly.
            SequenceMassCalc.ParseModCounts(BioMassCalc.MONOISOTOPIC, "OO-HNHN", new Dictionary<string, int>());            
            
            // Test normal function
            var sequence = new Target("VEDELK");
            var calc = new SequenceMassCalc(MassType.Monoisotopic);
            var expected = new List<KeyValuePair<double, double>>
            {
                new KeyValuePair<double, double>(366.69232575, 0.668595429107379),
                new KeyValuePair<double, double>(367.194009631186, 0.230439133647163),
                new KeyValuePair<double, double>(367.69569373213, 0.0384590257505838),
                new KeyValuePair<double, double>(367.694446060561, 0.017949871952498),
                new KeyValuePair<double, double>(367.19084355, 0.017192511410608),
                new KeyValuePair<double, double>(368.196128166749, 0.00616111554527541),
                new KeyValuePair<double, double>(367.692527431186, 0.00592559754703795),
                new KeyValuePair<double, double>(367.1954645, 0.00513890101333714),
                new KeyValuePair<double, double>(368.197384928765, 0.00425230800778248),
                new KeyValuePair<double, double>(367.697148381186, 0.00177118156340514),
                new KeyValuePair<double, double>(368.697813900475, 0.00101646716687999),
                new KeyValuePair<double, double>(368.19421153213, 0.00098894968507418),
                new KeyValuePair<double, double>(368.1929673, 0.000458171690211896),
                new KeyValuePair<double, double>(368.699089317816, 0.000360958912487784),
                new KeyValuePair<double, double>(368.19883248213, 0.000295600474962106),
                new KeyValuePair<double, double>(368.69657325, 0.000202295887934749),
                new KeyValuePair<double, double>(367.68936135, 0.000189469126984509),
                new KeyValuePair<double, double>(368.694649407319, 0.000157258059028779),
                new KeyValuePair<double, double>(369.199499540822, 0.000109802223366965),
                new KeyValuePair<double, double>(368.695902738459, 0.000109341412261551),
                new KeyValuePair<double, double>(369.198241127809, 7.04324061939316E-05),
                new KeyValuePair<double, double>(368.191045231186, 6.53027220564238E-05),
                new KeyValuePair<double, double>(368.700518794653, 3.19123118597943E-05),
                new KeyValuePair<double, double>(369.19633171026, 2.61321131972594E-05),
                new KeyValuePair<double, double>(369.699928636691, 1.13012567237636E-05),
                new KeyValuePair<double, double>(368.69272933213, 1.08986656450318E-05),
                new KeyValuePair<double, double>(367.69860325, 1.06303400612337E-05),
            };
            var actual = calc.GetMzDistribution(sequence, Adduct.DOUBLY_PROTONATED, IsotopeAbundances.Default).MassesSortedByAbundance();
            for (var i = 0; i < expected.Count; i++)
            {
                Assert.AreEqual(expected[i].Key, actual[i].Key, .0001);
                Assert.AreEqual(expected[i].Value, actual[i].Value, .0001);
            } 
        }
        [TestMethod]
        public void TestSequenceMassCalcNormalizeModifiedSequence()
        {
            const string normalizedModifiedSequence = "ASDF[+6.0]GHIJ";
            Assert.AreEqual(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence("ASDF[6]GHIJ"));
            Assert.AreEqual(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence("ASDF[+6]GHIJ"));
            Assert.AreSame(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence(normalizedModifiedSequence));

            Assert.AreEqual("ASDF[-6.0]GHIJ", SequenceMassCalc.NormalizeModifiedSequence("ASDF[-6]GHIJ"));

            AssertEx.ThrowsException<ArgumentException>(() => SequenceMassCalc.NormalizeModifiedSequence("ASC[Carbomidomethyl C]FGHIJ"));
            AssertEx.ThrowsException<ArgumentException>(() => SequenceMassCalc.NormalizeModifiedSequence("ASC[6"));
        }

        /// <summary>
        /// Tests that <see cref="BioMassCalc.ParseMass(ref string)"/> works correctly and stops at the first minus sign.
        /// </summary>
        [TestMethod]
        public void TestParseMass()
        {
            var bioMassCalc = new BioMassCalc(MassType.Monoisotopic);
            string description = "C'2";
            Assert.AreEqual(26, bioMassCalc.ParseMass(ref description), .01);
            Assert.AreEqual(string.Empty, description);
            description = "-C'2";
            Assert.AreEqual(0, bioMassCalc.ParseMass(ref description));
            Assert.AreEqual("-C'2", description);
            description = "C'2-C2";
            Assert.AreEqual(26, bioMassCalc.ParseMass(ref description), .01);
            Assert.AreEqual("-C2", description);
            description = "C'2";
            Assert.AreEqual(26, bioMassCalc.ParseMassExpression(ref description), .01);
            Assert.AreEqual(string.Empty, description);
            description = "C'2-C2";
            Assert.AreEqual(2, bioMassCalc.ParseMassExpression(ref description), .01);
            Assert.AreEqual(string.Empty, description);
            description = "C'2-C2-N2";
            Assert.AreEqual(2, bioMassCalc.ParseMassExpression(ref description), .01);
            Assert.AreEqual("-N2", description);
            Assert.AreEqual(2, bioMassCalc.CalculateMassFromFormula("C'2-C2"), .01);
            AssertEx.ThrowsException<ArgumentException>(()=>bioMassCalc.CalculateMassFromFormula("C'2-C2-N2"));
        }

        [TestMethod]
        public void TestParseModParts()
        {
            var bioMassCalc = new BioMassCalc(MassType.Monoisotopic);
            CollectionAssert.AreEqual(new[]{"C'2", ""}, SequenceMassCalc.ParseModParts(bioMassCalc, "C'2"));
            CollectionAssert.AreEqual(new[]{"", "C2"}, SequenceMassCalc.ParseModParts(bioMassCalc, "-C2"));
            CollectionAssert.AreEqual(new[]{"C'2", "C2"}, SequenceMassCalc.ParseModParts(bioMassCalc, "C'2-C2"));
        }

        [TestMethod]
        public void TestGetIonFormula()
        {
            SequenceMassCalc sequenceMassCalc = new SequenceMassCalc(MassType.Monoisotopic);
            Assert.AreEqual(147.11, sequenceMassCalc.GetPrecursorMass("K"), .1);
            Assert.AreEqual("C6H14N2O2", sequenceMassCalc.GetMolecularFormula("K"));

            var label13C6K = new StaticMod("label13C6K", "K", null, LabelAtoms.C13);
            sequenceMassCalc.AddStaticModifications(new []{label13C6K});
            Assert.AreEqual(153.11, sequenceMassCalc.GetPrecursorMass("K"), .1);
            Assert.AreEqual("C'6H14N2O2", sequenceMassCalc.GetMolecularFormula("K"));

            var label15N2K = new StaticMod("label15N2K", "K", null, LabelAtoms.N15);
            sequenceMassCalc.AddStaticModifications(new[]{label15N2K});
            Assert.AreEqual(155.11, sequenceMassCalc.GetPrecursorMass("K"), .1);
            Assert.AreEqual("C'6H14N'2O2", sequenceMassCalc.GetMolecularFormula("K"));

            // Check our ability to handle strangely constructed chemical formulas
            Assert.AreEqual(Molecule.Parse("C12H9S2P0").ToString(), Molecule.Parse("C12H9S2").ToString()); // P0 is weird
            Assert.AreEqual(Molecule.Parse("C12H9S2P1").ToString(), Molecule.Parse("C12H9S2P").ToString()); // P1 is weird
            Assert.AreEqual(Molecule.Parse("C12H9S0P").ToString(), Molecule.Parse("C12H9P").ToString()); // S0 is weird, and not at end
        }

        [TestMethod]
        public void TestTokenizeFormula()
        {
            CollectionAssert.AreEqual(new[] {"C'", "6", "Cl", "2", "C", "H", "4", "-", "H", "24", "O"},
                SequenceMassCalc.TokenizeFormula("C'6Cl2CH4-H24O").ToArray());
            // Test garbage characters before an element name.
            CollectionAssert.AreEqual(new[] {"x", "y", "z", "Element"},
                SequenceMassCalc.TokenizeFormula("xyzElement").ToArray());
        }

        [TestMethod]
        public void TestGetHeavyFormula()
        {
            Assert.AreEqual("C'O2", SequenceMassCalc.GetHeavyFormula("CO2", LabelAtoms.C13));
            Assert.AreEqual("C'O2", SequenceMassCalc.GetHeavyFormula("C'O2", LabelAtoms.C13));
            Assert.AreEqual("C'", SequenceMassCalc.GetHeavyFormula("C", LabelAtoms.C13));
            Assert.AreEqual("N'", SequenceMassCalc.GetHeavyFormula("N", LabelAtoms.N15));
            Assert.AreEqual("O'", SequenceMassCalc.GetHeavyFormula("O", LabelAtoms.O18));
            Assert.AreEqual("H'", SequenceMassCalc.GetHeavyFormula("H", LabelAtoms.H2));
            Assert.AreEqual("H\"", SequenceMassCalc.GetHeavyFormula("H", LabelAtoms.H3));
            Assert.AreEqual("H'", SequenceMassCalc.GetHeavyFormula("D", LabelAtoms.H2));
            Assert.AreEqual("H\"", SequenceMassCalc.GetHeavyFormula("T", LabelAtoms.H3));
            Assert.AreEqual("Cl'", SequenceMassCalc.GetHeavyFormula("Cl", LabelAtoms.Cl37));
            Assert.AreEqual("Br'", SequenceMassCalc.GetHeavyFormula("Br", LabelAtoms.Br81));
            Assert.AreEqual("P'", SequenceMassCalc.GetHeavyFormula("P", LabelAtoms.P32));
            Assert.AreEqual("S\"", SequenceMassCalc.GetHeavyFormula("S", LabelAtoms.S33));
            Assert.AreEqual("S'", SequenceMassCalc.GetHeavyFormula("S", LabelAtoms.S34));

            // Make sure IUPAC nicknames don't find their way into our list of heavy symbols
            Assume.IsTrue(BioMassCalc.IsSkylineHeavySymbol("H'"));
            Assume.IsFalse(BioMassCalc.IsSkylineHeavySymbol("D"));
            Assume.IsFalse(BioMassCalc.IsSkylineHeavySymbol("T"));
        }

        [TestMethod]
        public void TestIsotopeAbundancesDefault()
        {
            foreach (var entry in IsotopeAbundances.Default)
            {
                // All element names start with a capital letter 
                Assert.IsTrue(char.IsUpper(entry.Key[0]));
                for (int i = 1; i < entry.Key.Length; i++)
                {
                    Assert.IsTrue(char.IsLower(entry.Key[i]));
                }
                var massDistribution = entry.Value;
                Assert.AreEqual(1.0, massDistribution.Sum(kvp => kvp.Value), 1e-8);
                for (int i = 1; i < massDistribution.Count; i++)
                {
                    var massDifference = massDistribution[i].Key - massDistribution[i - 1].Key;
                    Assert.IsTrue(massDifference > .9);
                    // All of the isotopes of the elements have masses that are close to 
                    // an integer number of Daltons apart.
                    Assert.AreEqual(Math.Round(massDifference), massDifference, .1);
                }
                if (BioMassCalc.MONOISOTOPIC.IsKnownSymbol(entry.Key))
                {
                    Assert.AreEqual(massDistribution.MostAbundanceMass, BioMassCalc.MONOISOTOPIC.GetMass(entry.Key), entry.Key);
                }
                if (BioMassCalc.AVERAGE.IsKnownSymbol(entry.Key))
                {
                    // We allow the average mass to differ by up to .05 Daltons between IsotopeAbundances and BioMassCalc
                    // Consider: Should we try to make these numbers match better?
                    Assert.AreEqual(massDistribution.AverageMass, BioMassCalc.AVERAGE.GetMass(entry.Key), 5e-2, entry.Key);
                }
            }
        }
    }
}