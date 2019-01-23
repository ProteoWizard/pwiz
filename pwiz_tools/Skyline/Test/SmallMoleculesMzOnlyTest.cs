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
using System.Collections.Generic;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class SmallMoleculesMzOnlyTest : AbstractUnitTest
    {
        [TestMethod]
        public void ConvertSmallMolMzOnlyFrom37Test()
        {
            var docSmall = ResultsUtil.DeserializeDocument("SmallMoleculesMzOnly_3-7.sky", GetType());
            AssertEx.IsDocumentState(docSmall, 0, 4, 8, 13, 13);
            int iGroup = 0;
            var iTran = 0;
            var heavies = new HashSet<int> { 2, 3, 5, 11, 13 }; // Indexes of nodes expected to be !isLight
            var mzPrecursorDeclared = new[]
            {
                "146.2",
                "155.2",
                "500.",
                "300.",
                "320.",
                "177.044724",
                "88.522088",
                "819.42",
                "1639.7",
                "351.217698",
                "355.242805",
                "335.001097",
                "339.247891"
            };
            foreach (var nodeGroup in docSmall.MoleculeTransitionGroups)
            {
                Adduct expectedPrecursorAdduct;
                switch (++iGroup)
                {
                    case 6:
                        expectedPrecursorAdduct = Adduct.M_PLUS;
                        break;
                    case 7:
                        expectedPrecursorAdduct = Adduct.M_PLUS_2;
                        break;
                    case 8:
                        expectedPrecursorAdduct = Adduct.M_MINUS_2.ChangeIsotopeLabels(-.86055);
                        break;
                    case 9:
                        expectedPrecursorAdduct = Adduct.M_MINUS;
                        break;
                    case 10:
                        expectedPrecursorAdduct = Adduct.M_MINUS_H;
                        break;
                    case 11:
                        expectedPrecursorAdduct = Adduct.FromString("[M4H2-H]", Adduct.ADDUCT_TYPE.non_proteomic, null);
                        break;
                    case 12:
                        expectedPrecursorAdduct = Adduct.M_MINUS_H.ChangeIsotopeLabels(-.221687, 6);
                        break;
                    case 13:
                        expectedPrecursorAdduct = Adduct.FromString("[M4H2-H]", Adduct.ADDUCT_TYPE.non_proteomic, null);
                        break;
                    default:
                        // Check translation to adducts worked as expected
                        expectedPrecursorAdduct =
                            Adduct.FromCharge(nodeGroup.PrecursorCharge, Adduct.ADDUCT_TYPE.non_proteomic);
                        if (iGroup == 5)
                            expectedPrecursorAdduct = expectedPrecursorAdduct.ChangeIsotopeLabels(60.0);
                        break;
                }
                var prec = mzPrecursorDeclared[iGroup - 1].Split('.')[1].Length;
                Assert.AreEqual(double.Parse(mzPrecursorDeclared[iGroup-1], CultureInfo.InvariantCulture), Math.Round(nodeGroup.PrecursorMz, prec), "mz iGroup="+iGroup);
                Assert.AreEqual(expectedPrecursorAdduct, nodeGroup.PrecursorAdduct, "iGroup="+iGroup);
                Assert.AreEqual(heavies.Contains(iGroup), !nodeGroup.IsLight, "iGroup=" + iGroup);
                // Most product m/z values should be single-digit precision (entered by a person)
                foreach (var nodeTran in nodeGroup.Transitions)
                {
                    if (++iTran < 7)
                    {
                        Assert.AreEqual(Math.Round(nodeTran.Mz, 1), nodeTran.Mz.Value);
                    }
                    var expectedTransitionAdduct = nodeTran.IsMs1 ? expectedPrecursorAdduct : Adduct.FromChargeNoMass(nodeTran.Transition.Charge);
                    Assert.AreEqual(expectedTransitionAdduct, nodeTran.Transition.Adduct, "iTran=" + iTran);
                }
            }

            AssertEx.Serializable(docSmall);
        }
    }
}
