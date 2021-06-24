/*
 * Original author: Paige Pratt,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for ImportTransitionListColumnSelectDlg.
    /// </summary>
    [TestClass]
    public class PasteTransitionListTest : AbstractFunctionalTestEx
    {
        private string precursor => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z;
        private string product => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z;
        private string peptide => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence;
        private string protName => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name;
        private string fragName => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name;
        private string label  => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type;
        private string ignoreColumn => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column;
        private string decoy => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy;

        [TestMethod]
        public void TestPasteTransitionList()
        {
            TestFilesZip = @"TestFunctional\PasteTransitionListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        { 
            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("ThermoTransitionList.csv")));
            var therm = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            
            RunUI(() => {
                var thermBoxes = therm.ComboBoxes;
                // Checks that automatically assigning column headers works properly
                Assert.AreEqual(precursor, thermBoxes[0].Text);
                Assert.AreEqual(product, thermBoxes[1].Text);
                Assert.AreEqual(peptide, thermBoxes[3].Text);
                Assert.AreEqual(protName, thermBoxes[4].Text);
                Assert.AreEqual(fragName, thermBoxes[5].Text);
                Assert.AreEqual(label, thermBoxes[7].Text);
            });
            RunDlg<ImportTransitionListErrorDlg>(therm.OkDialog, errDlg => errDlg.Close());
            OkDialog(therm, therm.CancelDialog);


            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            // This will paste in a transition list with headers
            var peptideTransitionList = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var peptideBoxes = peptideTransitionList.ComboBoxes;
                // Column positions are only saved if at least one combobox is changed, so
                // change a couple in order to trigger column saving
                peptideBoxes[14].SelectedIndex = 0;
                peptideBoxes[4].SelectedIndex = 1;
            });
            // Clicking the OK button should save the column locations
            OkDialog(peptideTransitionList, peptideTransitionList.OkDialog);

            // Verify that the correct columns were saved in the settings
            var expectedColumns = new [] {protName, peptide, precursor, ignoreColumn, decoy, product, ignoreColumn, fragName};
            // expectedColumns is the same length as targetColumns for comparison
            var actualColumns = 
                Settings.Default.CustomImportTransitionListColumnTypesList.GetRange(0, expectedColumns.Length);
            CollectionAssert.AreEqual(expectedColumns, actualColumns);
            
            // Paste in the same transition list and verify that the earlier modification was saved
            var peptideTransitionList1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            WaitForDocumentLoaded();
            RunUI(() => 
            {
                var peptideTransitionListBoxes1 = peptideTransitionList1.ComboBoxes;
                Assert.AreEqual(1, peptideTransitionListBoxes1[4].SelectedIndex);
                peptideTransitionList1.CancelDialog();
            });

            // This will paste in the same transition list, but with different headers. The program should realize it has
            // different headers and not use the saved list of column names
            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionListdiffheaders.csv")));

            var peptideTransitionListDiffHeaders = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            LoadNewDocument(true);
            RunUI(() =>
            {
                var diffPeptideBoxes = peptideTransitionListDiffHeaders.ComboBoxes;
                // Checks that the program did not use the saved indices
                Assert.AreNotEqual(diffPeptideBoxes[4].SelectedIndex, 1);

                peptideTransitionListDiffHeaders.CancelDialog();
            });

            // Now check UI interactions with a bad import file
            RunUI(() => SkylineWindow.NewDocument());
            WaitForDocumentLoaded();

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            var dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() => {
                var comboBoxes = dlg.ComboBoxes;
                // Checks that two comboboxes cannot have the same header (unless it is Ignore Column)
                comboBoxes[0].SelectedIndex = 1;
                comboBoxes[1].SelectedIndex = 1;
                Assert.AreNotEqual(comboBoxes[0], comboBoxes[1]);

                comboBoxes[2].SelectedIndex = 0;
                comboBoxes[3].SelectedIndex = 0;
                Assert.AreEqual(comboBoxes[2].Text, comboBoxes[3].Text);
                // Checks resizing of comboboxes 
                var oldBoxWidth = comboBoxes[0].Width;
                dlg.dataGrid.Columns[0].Width -= 20;
                Assert.AreNotEqual(oldBoxWidth, comboBoxes[0].Width);
            });
            // Clicking the "Check for Errors" button should bring up the special column missing dialog
            RunDlg<MessageDlg>(dlg.buttonCheckForErrors.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            // Clicking OK while input is invalid should also pop up the error list
            RunDlg<MessageDlg>(dlg.buttonOk.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            // Only way out without fixing the columns is to cancel
            OkDialog(dlg, dlg.CancelDialog);

            // Now verify that we do not save invalid sets of headers
            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("ThermoTransitionList.csv")));
            var transitions = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            // Change headers of the key columns so that our document will not import
            var transitionBoxes = transitions.ComboBoxes;
            transitionBoxes[0].SelectedIndex = 0; // Set precursor m/z column to 'ignore column'
            transitionBoxes[1].SelectedIndex = 0; // Set product m/z column to 'ignore column'
            transitionBoxes[3].SelectedIndex = 0; // Set peptide modified sequence column to 'ignore column'

            // Click Ok and when it doesn't import due to missing columns close the document
            RunDlg<MessageDlg>(transitions.buttonOk.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            OkDialog(transitions, transitions.CancelDialog);

            // Paste in the same list and verify that the invalid column positions were not saved
            LoadNewDocument(true);

            var transitions1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            var transitions1Boxes = transitions1.ComboBoxes;
            Assert.AreNotEqual(transitions1Boxes[0].SelectedIndex, 0);
            Assert.AreNotEqual(transitions1Boxes[1].SelectedIndex, 0);
            Assert.AreNotEqual(transitions1Boxes[3].SelectedIndex, 0);

            // Close the document
            OkDialog(transitions1, transitions1.CancelDialog);

            // Now verify that we do not use saved column positions on newly pasted
            // transition lists when they do not work
            LoadNewDocument(true);

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList_no_headers.csv")));
            var pep = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // Change a column and click OK so the column positions are saved
            RunUI(() =>
            {
                var pepBoxes = pep.ComboBoxes;
                // Changes the combo box currently labeled "fragment name" to "ignore column"
                pepBoxes[7].SelectedIndex = 0;
            });
            OkDialog(pep, pep.OkDialog);
            
            // Verify that the change and other columns were saved in the settings
            Assert.AreEqual(ignoreColumn,Settings.Default.CustomImportTransitionListColumnTypesList[7]);
            Assert.AreEqual(protName, Settings.Default.CustomImportTransitionListColumnTypesList[0]);
            Assert.AreEqual(peptide, Settings.Default.CustomImportTransitionListColumnTypesList[1]);

            // Verify the document state we expect
            AssertEx.IsDocumentState(SkylineWindow.Document, null, null, 2, 2, 9);

            // Paste in a new transition list with the precursor m/z column in a new location
            LoadNewDocument(true);
            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList_no_headers_new_order.csv")));
            var pep1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            // We should realize our saved column positions are invalid and discard them
            // Verify that we did not use the saved position of "precursor m/z"
            var pep1Boxes = pep1.ComboBoxes;
            Assert.AreNotEqual(pep1Boxes[2].SelectedIndex, 6);
            // Verify that we did use the detected values instead
            Assert.AreEqual(pep1Boxes[4].SelectedIndex, 6);
            Assert.AreEqual(pep1Boxes[11].SelectedIndex, 7);
            // Close the document
            OkDialog(pep1, pep1.CancelDialog);

            // Now check UI interactions with a bad import file whose headers we correct in the dialog
            using (new CheckDocumentState(1,2,2,9))
            {
                RunUI(() => SkylineWindow.NewDocument());
                WaitForDocumentLoaded();

                SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
                dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

                // Correct the header assignments
                RunUI(() => {
                    var comboBoxes = dlg.ComboBoxes;
                    comboBoxes[1].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence);
                    comboBoxes[2].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
                    comboBoxes[14].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                });

                // Clicking the "Check for Errors" button should bring up the no-error list dialog
                RunDlg<MessageDlg>(dlg.buttonCheckForErrors.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it

                // Clicking OK while input is valid should proceed without delay
                OkDialog(dlg, dlg.OkDialog);
            }

            LoadNewDocument(true); // Tidy up
        }
    }
}
