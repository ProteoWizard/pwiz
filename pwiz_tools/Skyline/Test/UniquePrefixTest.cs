/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Graphs;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for UniquePrefixTest
    /// </summary>
    [TestClass]
    public class UniquePrefixTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestUniquePrefix()
        {
            var testSequences = new[,]
            {
                // Original name on left, expected result on right.
                {null, null},
                {"", ""},
                {"A", "A"},
                {"A", "A"}, // duplicate name (on purpose)
                {"AB", "AB"},
                {"ABC", "ABC"},
                {"ABCD", "ABCD"},
                {"ABCDE", "ABCDE"},
                {"ABCDEF", "ABCDEF"},
                {"ABCDEFG", "ABCDEFG"},
                {"ABCDEFG", "ABCDEFG"}, // duplicate name (on purpose)
                {"ABCDEFGH", "ABC…FGH"},
                {"ABCDEFGHI", "ABC…GHI"},

                {"ABCE", "ABCE"},

                {"ABDEFGHI", "ABD…"},

                {"ABEFGHI", "ABEFGHI"},
                {"ABEFGHIJ", "ABE…HIJ"},
                {"ABEFHI", "ABEFHI"},

                {"ABFFFGHI", "ABF(5)"},
                {"ABFFFFGHI", "ABF(6)"},
                {"ABFFFFAFGHI", "ABF…FA…"},
                {"ABFFFAFFGHI", "ABF…A…"},

                {"ABGAABAABAGHI", "ABG…B…B…"},
                {"ABGAAbAABAGHI", "ABG…b…B…"},
                {"ABGAABAAbAGHI", "ABG…B…b…"},
                {"ABGAAB[80]AAB[99]AGHI", "ABG…b…b…"}
            };
            var testCustomIons = new[,]
            {
                // Original name on left, expected result on right.
                {"C32:0", "C32:0"},
                {"C32:1", "C32:1"},
                {"C32:2", "C32:2"},
                {"C32:2", "C32:2"}, // Duplicated on purpose
                {"C30:0", "C30:0"},
                {"C[30]:0", "C[30]:0"},
                {"C[400]:0", "C[400]…"},
                {"C11:0 fish breath (H2O)", "C11:0…(H2O)"},
                {"C11:0 fish breath (NH3)", "C11:0…(NH3)"},
                {"C12:0 fish breath", "C12:0…"},
                {"C15:0 fish breath", "C15:0(14)"},
                {"C15:0 doggy breath", "C15:0(15)"},
                {"C16:0 fishy breath", "C16:0…f…"},
                {"C16:0 doggy breath", "C16:0…d…"},
                {"C14", "C14"},
                {"C14:1", "C14:1"},
                {"C14:1-OH", "C14:1-…"},
                {"C14:2", "C14:2"},
                {"C14:2-OH", "C14:2-…"}
            };

            for (var loop = 2; loop > 0; loop--)
            {
                var strings = new List<UniquePrefixGenerator.TargetLabel>();
                var commontext = (loop == 1) ? String.Empty : "Delta Niner Foxtrot "; // non-peptides should strip leading common text

                for (int i = 0; i < testSequences.GetLength(0); i++)
                {
                    strings.Add(new UniquePrefixGenerator.TargetLabel(testSequences[i, 0], true));
                }
                for (int i = 0; i < testCustomIons.GetLength(0); i++)
                {
                    strings.Add(new UniquePrefixGenerator.TargetLabel(commontext + testCustomIons[i, 0], false));
                }
                var prefixGenerator = new UniquePrefixGenerator(strings, 3);
                for (int i = 0; i < testSequences.GetLength(0); i++)
                {
                    var expected = testSequences[i, 1];
                    var uniquePrefix = prefixGenerator.GetUniquePrefix(testSequences[i, 0], true);
                    Assert.AreEqual(expected, uniquePrefix);
                }
                for (int i = 0; i < testCustomIons.GetLength(0); i++)
                {
                    var expected = testCustomIons[i, 1];
                    var uniquePrefix = prefixGenerator.GetUniquePrefix(commontext + testCustomIons[i, 0], false);
                    Assert.AreEqual(expected, uniquePrefix);
                }
            }
        }

        /// <summary>
        /// Tests fix for an exception that used to occur where, if there were multiple peptides
        /// of different length that shared 3 letter prefixes and suffixes with each other, an
        /// exception would be thrown by GetUniquePrefix.
        /// </summary>
        [TestMethod]
        public void TestPrefixAndSuffixMatches()
        {
            string[] peptides = 
            {
                "LATQSNEITIPVTFESRAqLGGPEAAK",
                "LATqSNEITIPVTFESRAQLGGPEAAK",
                "LATQSnEITIPVTFESRAQLGGPEAAK",
                "LATQSnEITIPVTFESRAQLGGPEAAKSDETAAK",
                "LATQSNEITIPVTFESRAQLGGPEAAK",
                "LATQSNEITIPVTFESRAQLGGPEAAKSDETAAK",
                "LATQSNEITIPVTFESRAqLGGPEAAKSDETAAK",
            };
            var uniquePrefixGenerator = new UniquePrefixGenerator(peptides.Select(
                sequence => new UniquePrefixGenerator.TargetLabel(sequence, true)), 3);
            Dictionary<string, string> peptideAbbreviations = new Dictionary<string, string>();
            foreach (string peptide in peptides)
            {
                string abbreviation = uniquePrefixGenerator.GetUniquePrefix(peptide, true);
                Assert.IsTrue(abbreviation.Length <= peptide.Length);
                peptideAbbreviations.Add(peptide, abbreviation);
            }
            CollectionAssert.AllItemsAreUnique(peptideAbbreviations.Values);
        }
    }
}
