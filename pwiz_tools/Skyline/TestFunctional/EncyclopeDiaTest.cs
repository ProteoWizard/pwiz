/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Menus;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class EncyclopeDiaTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEncyclopeDia()
        {
            TestFilesZip = @"TestFunctional\EncyclopediaTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string libName = "elibtest";
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Library);
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("elibtest.elib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(()=>peptideSettingsUi.PickedLibraries = new []{libName});
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            var libraryViewer = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            var filterMatchedPeptidesDlg = ShowDialog<FilterMatchedPeptidesDlg>(libraryViewer.AddAllPeptides);
            OkDialog(filterMatchedPeptidesDlg, filterMatchedPeptidesDlg.OkDialog);
            var confirmationMessage = WaitForOpenForm<MultiButtonMsgDlg>();
            OkDialog(confirmationMessage, confirmationMessage.OkDialog);
            Assert.AreEqual(48, SkylineWindow.Document.PeptideCount);
            foreach (var transitionGroup in SkylineWindow.Document.PeptideTransitionGroups)
            {
                Assert.AreNotEqual(0, transitionGroup.TransitionCount, "Transition group {0} has no transitions", transitionGroup);
            }
            OkDialog(libraryViewer, libraryViewer.Close);

            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0];
            Assert.IsNotNull(library);
            Assert.IsInstanceOfType(library, typeof(EncyclopeDiaLibrary));
            Assert.IsTrue(library.HasExplicitBounds);
            Assert.IsTrue(library.UseExplicitPeakBounds);

            // Verify that trying to generate decoys generates a warning about explicit peak bounds
            RunDlg<MultiButtonMsgDlg>(()=>SkylineWindow.ShowGenerateDecoysDlg(), dlg=>
            {
                Assert.AreEqual(MenusResources.RefineMenu_ShowGenerateDecoysDlg_Are_you_sure_you_want_to_add_decoys_to_this_document_, dlg.Message);
                dlg.ClickCancel();
            });
            var multiButtonMsgDlg = ShowDialog<MultiButtonMsgDlg>(() => SkylineWindow.ShowGenerateDecoysDlg());
            RunDlg<GenerateDecoysDlg>(multiButtonMsgDlg.BtnYesClick, generateDecoysDlg => generateDecoysDlg.Close());
        }
    }
}
