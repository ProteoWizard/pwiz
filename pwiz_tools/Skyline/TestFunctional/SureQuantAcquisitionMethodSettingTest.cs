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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that choosing "SureQuant" in the Transition Settings dialog also changes the other required settings
    /// </summary>
    [TestClass]
    public class SureQuantAcquisitionMethodSettingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSureQuantAcquisitionMethodSetting()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var transitionSettingsUi = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(()=>
            {
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.FullScan;

                // Make sure that the combo box is displaying the localized text for the acquisition methods.
                var comboAcquisitionMethod = transitionSettingsUi.ComboAcquisitionMethod;
                CollectionAssert.AreEqual(comboAcquisitionMethod.Items.Cast<FullScanAcquisitionMethod>().ToList(), FullScanAcquisitionMethod.AVAILABLE.ToList());
                foreach (var item in comboAcquisitionMethod.Items)
                {
                    Assert.IsInstanceOfType(item, typeof(FullScanAcquisitionMethod));
                    var acquisitionMethod = (FullScanAcquisitionMethod) item;
                    Assert.AreEqual(acquisitionMethod.Label, comboAcquisitionMethod.GetItemText(acquisitionMethod));
                }

                // Verify that choosing the "SureQuant" acquisition method also changes MzMatchTolerance and Triggered Acquisition
                Assert.AreEqual(TransitionInstrument.DEFAULT_MZ_MATCH_TOLERANCE, transitionSettingsUi.MZMatchTolerance);
                Assert.IsFalse(transitionSettingsUi.TriggeredAcquisition);
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.SureQuant;
                Assert.IsTrue(transitionSettingsUi.TriggeredAcquisition);
                Assert.AreEqual(TransitionSettingsUI.SureQuantMzMatchTolerance, transitionSettingsUi.MZMatchTolerance);
                transitionSettingsUi.SelectedTab = TransitionSettingsUI.TABS.Instrument;
            });

            // Verify that unchecking "Triggered Acquisition" prompts to switch to PRM
            
            // Verify that clicking cancel keeps triggered acquisition selected
            RunDlg<AlertDlg>(()=>transitionSettingsUi.TriggeredAcquisition = false, alertDlg =>
            {
                alertDlg.ClickCancel();
            });
            RunUI(() =>
            {
                Assert.AreEqual(FullScanAcquisitionMethod.SureQuant, transitionSettingsUi.AcquisitionMethod);
                Assert.IsTrue(transitionSettingsUi.TriggeredAcquisition);
            });
            // Verify that clicking OK switches the acquisition method to "PRM"
            RunDlg<AlertDlg>(()=>transitionSettingsUi.TriggeredAcquisition = false, alertDlg =>
            {
                alertDlg.ClickOk();
            });
            RunUI(() =>
            {
                Assert.AreEqual(FullScanAcquisitionMethod.PRM, transitionSettingsUi.AcquisitionMethod);
                Assert.IsFalse(transitionSettingsUi.TriggeredAcquisition);
            });
            OkDialog(transitionSettingsUi, transitionSettingsUi.OkDialog);
        }
    }
}
