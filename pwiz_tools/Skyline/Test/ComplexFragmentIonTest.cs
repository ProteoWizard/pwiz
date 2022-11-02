/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Crosslinking;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ComplexFragmentIonTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestNeutralFragmentIonCompareTo()
        {
            var random = new Random((int) DateTime.UtcNow.Ticks);
            var ionChains = new[]
            {
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Precursor),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Y(7)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Y(5)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(5)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(7)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Empty),

            };
            var neutralFragmentIons = ionChains.OrderBy(chain => random.Next())
                .Select(chain => new NeutralFragmentIon(chain, null)).ToList();
            neutralFragmentIons.Sort();
            var sortedIonChains = neutralFragmentIons.Select(ion => ion.IonChain).ToList();
            AssertSameOrder(ionChains, sortedIonChains);
        }

        public static void AssertSameOrder<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedList = expected.ToList();
            var actualList = actual.ToList();
            CollectionAssert.AreEqual(expectedList, actualList, "Expected: {0} Actual: {1}", string.Join(",", expectedList), string.Join(",", actualList));

        }
    }
}
