/*
 * Original author: Rita Chupalov <rita .at. uw .edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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

using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Testing z+1 and z+2 ions feature.
    /// </summary>
    [TestClass]
    public class EADZIonsTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestEADIons()
        {
            Run(@"TestFunctional\EADZIonsTest.zip");
        }

        protected override void DoTest()
        {
            //Making sure the input aliases are unique across all ion types to avoid input ambiguity.
            Assert.IsTrue(Enumerable.Range(0, (int) IonType.zhh + 1).SelectMany(i => ((IonType) i).GetInputAliases(), (i, s) => s)
                .GroupBy(s => s, (s, enumerable) => enumerable.Count()).All(i => i == 1));

            OpenDocument("EADZIonsTest.sky");
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.FragmentTypes = "c, z., z'";
                transitionSettingsUI.IonCount = 5;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            RunUI(() => 
            { 
                SkylineWindow.ShowZHIons(true);
                SkylineWindow.ShowZHHIons(true);
            });

            WaitForGraphs();
            var libMatch = SkylineWindow.GraphSpectrum.DisplayedSpectrum;
            ImportResults("FilteredScans\\LITV56_EAD" + ExtensionTestContext.ExtMzml);
            WaitForGraphs();
            FindNode((505.5810).ToString("F4", LocalizationHelper.CurrentCulture) + "+++");

            var testIons = new[]{
                new {type = IonType.zh, offset = 8},
                new {type = IonType.zh, offset = 5},
                new {type = IonType.zhh, offset = 5}
            };

            TransitionGroupDocNode selectedPrecursor = null;
            RunUI(() =>
            {
                selectedPrecursor = (SkylineWindow.SelectedNode as TransitionGroupTreeNode)?.DocNode;
            });
            Assert.IsNotNull(selectedPrecursor);
            var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
            ClickChromatogram(6.46, 2600.0);

            foreach (var testIon in testIons)
            {
                var testNode = new Transition(selectedPrecursor.Id as TransitionGroup, testIon.type, testIon.offset, 1, Adduct.M_PLUS);
                //check for the peaks in the library spectrum
                Assert.IsTrue( libMatch.PeaksMatched.Any(peak =>
                    peak.MatchedIons[0].IonType.Equals(testNode.IonType) &&
                    peak.MatchedIons[0].Ordinal.Equals(testNode.Ordinal)));
                //check for the transitions in the tree
                Assert.IsTrue(selectedPrecursor.Children.Any(t => (t is TransitionDocNode trans) &&
                    trans.Transition.GetFragmentIonName(CultureInfo.CurrentCulture).StartsWith(testNode.FragmentIonName)));
                //check for the chromatograms
                Assert.IsTrue(graphChrom.GraphPane.CurveList.Any(curve => curve.Label.Text.StartsWith(testNode.FragmentIonName)));
                //check that the ions are present in the full scan viewer.
                Assert.IsTrue(SkylineWindow.GraphFullScan.ZedGraphControl.GraphPane.CurveList.Any(c =>
                    c.Label.Text.StartsWith(testNode.FragmentIonName)));
            }
            using (new CheckDocumentState(1, 1, 1, 6))
            {
                var pickList1 = ShowDialog<PopupPickList>(SkylineWindow.ShowPickChildrenInTest);
                RunUI(() =>
                {
                    pickList1.ApplyFilter(false);
                    pickList1.ToggleFind();
                    pickList1.SearchString = "z.";
                    Assert.AreEqual(65, pickList1.ItemNames.Count());
                    pickList1.SearchString = "z'";
                    Assert.AreEqual(65, pickList1.ItemNames.Count());
                    pickList1.SetItemChecked(4, true);
                });
                RunUI(pickList1.OnOk);
            }

        }
    }
}
