/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests adding all of the peptides from a spectral library where all of the following are true:
    /// 1. When the ViewLibraryDlg was brought up, the library was not part of the Skyline document.
    /// 2. The Oxidation(M) modification was not part of the document
    /// 3. There are entries in the spectral library which have the same peptide sequence, but different
    /// states of Oxidation(M).
    /// This test makes sure that Skyline does not choke by adding the same peptide to the same PeptideGroup more than
    /// once.
    /// </summary>
    [TestClass]
    public class AddAllPeptidesWithModificationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAddAllPeptidesWithModifications()
        {
            TestFilesZip = @"TestFunctional\AddAllPeptidesWithModificationsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("AddAllPeptidesTest.sky")));
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, pepSettingsUi =>
            {
                pepSettingsUi.PickedLibraries = new string[0];
                pepSettingsUi.OkDialog();
            });
            RunUI(()=>SkylineWindow.ViewSpectralLibraries());
            var addModificationsDlg = WaitForOpenForm<AddModificationsDlg>();
            OkDialog(addModificationsDlg, addModificationsDlg.OkDialogAll);
            var viewLibraryDlg = FindOpenForm<ViewLibraryDlg>();
            ShowAndDismissDlg<MultiButtonMsgDlg>(viewLibraryDlg.AddAllPeptides, messageDlg =>
            {
                var addLibraryMessage =
                    string.Format(
                        Resources
                            .ViewLibraryDlg_CheckLibraryInSettings_The_library__0__is_not_currently_added_to_your_document,
                        "PeptidesWithModifications");
                StringAssert.StartsWith(messageDlg.Message, addLibraryMessage);
                messageDlg.DialogResult = DialogResult.Yes;
            });
            var filterPeptidesDlg = WaitForOpenForm<FilterMatchedPeptidesDlg>();
            OkDialog(filterPeptidesDlg, filterPeptidesDlg.OkDialog);
            var multiButtonMessageDlg = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(multiButtonMessageDlg, multiButtonMessageDlg.BtnYesClick);
            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);
            Assert.AreEqual(true, SkylineWindow.Document.PeptideGroups.First().AutoManageChildren);

            // Also make sure that changing the enzyme in Peptide Settings does not cause any problems.
            foreach (var enzyme in new EnzymeList().Skip(2).Take(2))
            {
                RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, pepSettingsUi =>
                {
                    pepSettingsUi.ComboEnzymeSelected = enzyme.ToString();
                    pepSettingsUi.OkDialog();
                });
            }
        }
    }
}
