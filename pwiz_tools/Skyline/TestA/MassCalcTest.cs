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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
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
        /// mass for the symbol in the <see cref="BioMassCalc.MONOISOTOPIC"/> mass calculator.
        /// </summary>
        /// <param name="symbol">Symbol for the atom</param>
        /// <param name="massDistOrdered">Mass distribution to test</param>
        /// <param name="indexMass">Index of the mass to test within the mass distribution</param>
        private static void TestMass(string symbol, IList<KeyValuePair<double, double>> massDistOrdered, int indexMass)
        {
            double massExpected = BioMassCalc.MONOISOTOPIC.GetMass(symbol);
            if (massExpected != 0)
            {
                double massActual = massDistOrdered[indexMass].Key;
                Assert.AreEqual(massExpected, massActual,
                    string.Format("The mass for {0} was expected to be {1} but was found to be {2}", symbol, massExpected, massActual));
            }
        }

        [TestMethod]
        public void SequenceMassCalcTest()
        {
            // Test case that caused unexpected exception when O- was not parsed correctly.
            SequenceMassCalc.ParseModCounts(BioMassCalc.MONOISOTOPIC, "OO-HNHN", new Dictionary<string, int>());            
        }

        [TestMethod]
        public void TestSequenceMassCalcNormalizeModifiedSequence()
        {
            const string normalizedModifiedSequence = "ASDF[+6.0]GHIJ";
            Assert.AreEqual(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence("ASDF[6]GHIJ"));
            Assert.AreEqual(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence("ASDF[+6]GHIJ"));
            Assert.AreEqual(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence("ASDF[+6.000123]GHIJ"));
            Assert.AreSame(normalizedModifiedSequence, SequenceMassCalc.NormalizeModifiedSequence(normalizedModifiedSequence));

            Assert.AreEqual("ASDF[-6.0]GHIJ", SequenceMassCalc.NormalizeModifiedSequence("ASDF[-6]GHIJ"));

            AssertEx.ThrowsException<ArgumentException>(() => SequenceMassCalc.NormalizeModifiedSequence("ASC[Carbomidomethyl C]FGHIJ"));
            AssertEx.ThrowsException<ArgumentException>(() => SequenceMassCalc.NormalizeModifiedSequence("ASC[6"));
        }
    }
}