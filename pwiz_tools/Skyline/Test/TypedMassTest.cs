/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests to verify operation of the TypedMass class
    /// </summary>
    [TestClass]
    public class TypedMassTest : AbstractUnitTest
    {
    
        /// <summary>
        /// Tests the TypedMass class.
        /// </summary>
        [TestMethod]
        public void TypedMassUnitTest()
        {
            var a = new TypedMass(1, MassType.Average);
            var b = new TypedMass(2, MassType.Average);
            var c = new TypedMass(0, MassType.Average);
            var d = new TypedMass(1, MassType.Average);
            var e = new TypedMass(1, MassType.Monoisotopic);
            var f = new TypedMass(1, MassType.MonoisotopicHeavy);
            var g = new TypedMass(-1, MassType.MonoisotopicMassH);
            var h = new TypedMass(-1, MassType.Average);

            AssertEx.AreNotEqual(a, b, "these are not equal");
            AssertEx.AreEqual(a, a);
            AssertEx.AreEqual(a, d);
            AssertEx.IsTrue(a == d);
            AssertEx.IsFalse(a != d);
            AssertEx.IsTrue(a < b);
            AssertEx.IsTrue(b > a);
            AssertEx.IsTrue(a <= b);
            AssertEx.IsTrue(b >= a);
            AssertEx.AreEqual(TypedMass.ZERO_AVERAGE_MASSNEUTRAL, c);
            AssertEx.AreEqual(e, f);
            AssertEx.AreEqual(TypedMass.ZERO_AVERAGE_MASSNEUTRAL, d - a);
            AssertEx.AreEqual(b, a + d);
            AssertEx.AreEqual(b, a * 2);
            AssertEx.AreEqual(g.Value, (d - b).Value); // Can't compare directly because g is mono and d-b is average 
            AssertEx.AreEqual(-1.0, g);
            AssertEx.AreEqual(h, d - b); // Can't compare directly because g is mono and d-b is average 
        }
    }
}