/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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

using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class BlibMoleculeListsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestBlibMoleculeLists()
        {
            TestFilesZip = @"TestFunctional\BlibMoleculeListsTest.zip";
            RunFunctionalTest();
        }

        /// <summary>
        /// Test the use of Molecule List Names as "Proteins" in .blib export/import
        /// They should round trip, resulting in a document with the same Molecule List names
        /// as the one that generate the spectral library
        /// </summary>
        protected override void DoTest()
        {

            // Export and check spectral library
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("MoleculeGroups.sky")));
            var docOrig = WaitForDocumentLoaded();
            var moleculeLists = docOrig.MoleculeGroups.Select(g => g.Name).ToHashSet(); // Note the list names
            var exported = "MolListsTest";
            var exportedBlib = exported+ BiblioSpecLiteSpec.EXT;
            var exporteDFullPath = TestFilesDir.GetTestPath(exportedBlib);
            var libraryExporter = new SpectralLibraryExporter(SkylineWindow.Document, SkylineWindow.DocumentFilePath);
            libraryExporter.ExportSpectralLibrary(exporteDFullPath, null);
            Assert.IsTrue(File.Exists(exporteDFullPath));

            // Now import the .blib and populate document from that
            RunUI(() => SkylineWindow.NewDocument(true));

            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);

            Assert.IsNotNull(peptideSettingsUI);
            var editListUI = ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);

            RunDlg<EditLibraryDlg>(editListUI.AddItem, addLibUI =>
            {
                var nameTextBox = (TextBox)addLibUI.Controls.Find("textName", true)[0];
                Assert.IsNotNull(nameTextBox);
                var pathTextBox = (TextBox)addLibUI.Controls.Find("textPath", true)[0];
                Assert.IsNotNull(pathTextBox);
                nameTextBox.Text = exported;
                pathTextBox.Text = exporteDFullPath;
                addLibUI.OkDialog();
            });
            RunUI(editListUI.OkDialog);
            WaitForClosedForm(editListUI);

            // Make sure the libraries actually show up in the peptide settings dialog before continuing.
            WaitForConditionUI(() => peptideSettingsUI.AvailableLibraries.Length > 0);
            RunUI(() => Assert.IsFalse(peptideSettingsUI.IsSettingsChanged));

            // Add all the molecules in the library
            RunUI(() => SkylineWindow.ViewSpectralLibraries());
            var viewLibraryDlg = FindOpenForm<ViewLibraryDlg>();
            var docBefore = WaitForProteinMetadataBackgroundLoaderCompletedUI();

            RunDlg<MultiButtonMsgDlg>(viewLibraryDlg.AddAllPeptides, messageDlg =>
            {
                var addLibraryMessage =
                    string.Format(Resources.ViewLibraryDlg_CheckLibraryInSettings_The_library__0__is_not_currently_added_to_your_document, exported);
                StringAssert.StartsWith(messageDlg.Message, addLibraryMessage);
                messageDlg.DialogResult = DialogResult.Yes;
            });
            var filterPeptidesDlg = WaitForOpenForm<FilterMatchedPeptidesDlg>();
            RunDlg<MultiButtonMsgDlg>(filterPeptidesDlg.OkDialog, addLibraryPepsDlg =>
            {
                addLibraryPepsDlg.Btn1Click();
            });

            OkDialog(filterPeptidesDlg, filterPeptidesDlg.OkDialog);

            var docAfter = WaitForDocumentChange(docBefore);

            OkDialog(viewLibraryDlg, viewLibraryDlg.Close);
            RunUI(() => peptideSettingsUI.OkDialog());
            WaitForClosedForm(peptideSettingsUI);

            // Expect two molecule lists instead of the old single "Library Molecules" list
            var newMoleculeLists = docAfter.MoleculeGroups.Select(g => g.Name).ToArray(); 
            AssertEx.AreEqual(2, newMoleculeLists.Length);
            foreach (var name in newMoleculeLists)
            {
                AssertEx.IsTrue(moleculeLists.Contains(name));
            }
        }
    }
}
