/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class OpenDocWithBackgroundProteomeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOpenDocWithBackgroundProteome()
        {
            TestFilesZip = @"TestFunctional\OpenDocWithBackgroundProteomeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PRM_Bacillus_heavy.sky")));
            WaitForDocumentLoaded();
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);

            // Use the Peptide Settings dialog to both change the enzyme and add a new library.
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Digest;
                peptideSettingsDlg.ComboEnzymeSelected = "Trypsin/P [KR | -]";
                peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            const string libName = "myLibrary";
            var libListDlg =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsDlg.EditLibraryList);
            var addLibDlg = ShowDialog<EditLibraryDlg>(libListDlg.AddItem);
            RunUI(() =>
            {
                addLibDlg.LibraryName = libName;
                addLibDlg.LibraryPath = TestFilesDir.GetTestPath("rat_cmp_20.blib");
            });
            OkDialog(addLibDlg, addLibDlg.OkDialog);
            OkDialog(libListDlg, libListDlg.OkDialog);
            RunUI(() =>
            {
                peptideSettingsDlg.SetLibraryChecked(0, true);
                peptideSettingsDlg.SetLibraryChecked(1, true);
            });
            // When we OK the dialog, there will be a SrmSettingsDiff.DiffPeptides change (from the enzyme change)
            // but the libraries will not be loaded
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);

            // Make sure that the peptides in the document did not get reset to zero just because the libraries hadn't been loaded
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);
            WaitForDocumentLoaded();
            Assert.AreEqual(2, SkylineWindow.Document.PeptideCount);
        }
    }
}
