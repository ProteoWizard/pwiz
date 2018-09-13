/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.Chemistry;
using pwiz.SkylineTestUtil;

namespace CommonTest
{
    [TestClass]
    public class IsotopeAbundancesTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDefaultIsotopeAbundances()
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
                Assert.AreEqual(1.0, massDistribution.Sum(kvp=>kvp.Value), 1e-8);
                for (int i = 1; i < massDistribution.Count; i++)
                {
                    var massDifference = massDistribution[i].Key - massDistribution[i - 1].Key;
                    Assert.IsTrue(massDifference > .9);
                    // All of the isotopes of the elements have masses that are close to 
                    // an integer number of Daltons apart.
                    Assert.AreEqual(Math.Round(massDifference), massDifference, .1);
                }
            }
        }
    }
}
