/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class SmallMoleculesMzOnlyTest : AbstractUnitTest
    {
        [TestMethod]
        public void ConvertSmallMolMzOnlyFrom37Test()
        {
            var docSmall = ResultsUtil.DeserializeDocument("SmallMoleculesMzOnly_3-7.sky", GetType());
            AssertEx.IsDocumentState(docSmall, 0, 3, 5, 7, 7);
            int iGroup = 0;
            foreach (var nodeGroup in docSmall.MoleculeTransitionGroups)
            {
                if (++iGroup < 6) // The last molecule and its precursors was entered at higher precision
                {
                    // All precursor m/z values should be single-digit precision (entered by a person)
                    Assert.AreEqual(Math.Round(nodeGroup.PrecursorMz.Value, 1), nodeGroup.PrecursorMz.Value);

                    // Check translation to adducts worked as expected
                    var expectedAdduct = Adduct.FromCharge(nodeGroup.PrecursorCharge, Adduct.ADDUCT_TYPE.non_proteomic);
                    if (iGroup == 5)
                        expectedAdduct = expectedAdduct.ChangeIsotopeLabels(60.0);
                    Assert.AreEqual(expectedAdduct, nodeGroup.PrecursorAdduct);
                }
                else
                {
                    var expectedAdduct = Adduct.FromCharge(iGroup-5, Adduct.ADDUCT_TYPE.non_proteomic);
                    Assert.AreEqual(expectedAdduct, nodeGroup.PrecursorAdduct);
                }
            }
            // All product m/z values should be single-digit precision (entered by a person)
            foreach (var nodeTran in docSmall.MoleculeTransitions)
            {
                Assert.AreEqual(Math.Round(nodeTran.Mz, 1), nodeTran.Mz.Value);
                Assert.AreEqual(Adduct.FromChargeNoMass(nodeTran.Transition.Charge), nodeTran.Transition.Adduct);
            }
            AssertEx.Serializable(docSmall);
        }
    }
}
