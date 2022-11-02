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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests using the ImportPeptideSearch dialog when the PeptideRankId is something that is
    /// not supported by .blib files.
    /// </summary>
    [TestClass]
    public class InvalidPeptideRankIdTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestInvalidPeptideRankId()
        {
            TestFilesZip = @"TestFunctional\InvalidPeptideRankIdTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string nistLibraryName = "MyNistLibrary";

            // Add a NIST library to the document
            var peptideSettingsUi = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            peptideSettingsUi.TabControlSel = PeptideSettingsUI.TABS.Library;
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUi.EditLibraryList);

            RunDlg<EditLibraryDlg>(libListDlg.AddItem, addLibraryDlg =>
            {
                addLibraryDlg.LibraryName = nistLibraryName;
                addLibraryDlg.LibraryPath = TestFilesDir.GetTestPath("YeastMini.msp");
                addLibraryDlg.OkDialog();
            });
            OkDialog(libListDlg, libListDlg.OkDialog);
            
            RunUI(()=>
            {
                peptideSettingsUi.PickedLibraries = new[] {nistLibraryName};
                // This rank id is supported by NIST libraries, but not BiblioSpec.
                peptideSettingsUi.RankID = NistLibSpecBase.PEP_RANK_TFRATIO;
            });
            OkDialog(peptideSettingsUi, peptideSettingsUi.OkDialog);
            WaitForDocumentLoaded();
            Assert.AreEqual(1, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
            Assert.AreEqual(NistLibSpecBase.PEP_RANK_TFRATIO, SkylineWindow.Document.Settings.PeptideSettings.Libraries.RankId);
            RunUI(()=>SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("InvalidPeptideRankIdTest.sky")));

            // Bring up the Import Peptide Search dialog and go as far as creating a library
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            RunUI(() =>
            {
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new []{TestFilesDir.GetTestPath("modless.pride.xml")});
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => importPeptideSearchDlg.ClickNextButton());

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
            WaitForDocumentLoaded();
            Assert.AreEqual(2, SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.Count);
        }
    }
}
