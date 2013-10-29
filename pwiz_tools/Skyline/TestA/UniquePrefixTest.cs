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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
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
            var testStrings = new [,]
            {
                // Original name on left, expected result on right.
                {"A", "A"},
                {"A", "A"}, // duplicate name (on purpose)
                {"AB", "AB"},
                {"ABC", "ABC"},
                {"ABCD", "ABCD"},
                {"ABCDE", "ABCDE"},
                {"ABCDEF", "ABCDEF"},
                {"ABCDEFG", "ABC…EFG"},
                {"ABCDEFG", "ABC…EFG"}, // duplicate name (on purpose)
                {"ABCDEFGH", "ABC…FGH"},
                {"ABCDEFGHI", "ABC…GHI"},

                {"ABCE", "ABCE"},

                {"ABDEFGHI", "ABD"},

                {"ABEFGHI", "ABE…GHI"},
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

            var strings = new List<string>();
            for (int i = 0; i < testStrings.GetLength(0); i++)
            {
                strings.Add(testStrings[i, 0]);
            }
            var prefixGenerator = new UniquePrefixGenerator(strings, 3);
            for (int i = 0; i < testStrings.GetLength(0); i++)
            {
                var expected = testStrings[i, 1];
                var uniquePrefix = prefixGenerator.GetUniquePrefix(testStrings[i, 0]);
                Assert.AreEqual(expected, uniquePrefix);
            }
        }
    }
}
