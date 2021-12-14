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

using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
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
        private string labelType => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type;
        private string decoy => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Decoy;

        [TestMethod]
        public void TestPasteTransitionList()
        {
            TestFilesZip = @"TestFunctional\PasteTransitionListTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestMissingFragmentMz();

            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionListExtendedHeaders.csv")));
            var therm = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            
            RunUI(() => {
                var thermBoxes = therm.ComboBoxes;
                // Checks that automatically assigning column headers works properly
                Assert.AreEqual(protName, thermBoxes[0].Text);
                Assert.AreEqual(peptide, thermBoxes[1].Text);
                Assert.AreEqual(precursor, thermBoxes[2].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy, thermBoxes[4].Text);
                Assert.AreEqual(product, thermBoxes[5].Text);
                Assert.AreEqual(fragName, thermBoxes[7].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time, thermBoxes[10].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window, thermBoxes[11].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Note, thermBoxes[12].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_S_Lens, thermBoxes[13].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage, thermBoxes[14].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility, thermBoxes[15].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units, thermBoxes[16].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset, thermBoxes[17].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_, thermBoxes[18].Text);
                Assert.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage, thermBoxes[19].Text);
                Assert.AreEqual(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential, thermBoxes[20].Text);
            });
            // RunDlg<ImportTransitionListErrorDlg>(therm.OkDialog, errDlg => errDlg.Close());
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
                peptideBoxes[4].SelectedIndex = 6;
            });
            // Clicking the OK button should save the column locations
            OkDialog(peptideTransitionList, peptideTransitionList.OkDialog);

            // Verify that the correct columns were saved in the settings
            var expectedColumns = new List<string> {protName, peptide, precursor, ignoreColumn, labelType, product, ignoreColumn, fragName};
            expectedColumns.AddRange(Enumerable.Repeat(ignoreColumn, 13)); // The last 13 should all be 'ignore column'
            CollectionAssert.AreEqual(expectedColumns, Settings.Default.CustomImportTransitionListColumnTypesList);
            
            // Paste in the same transition list and verify that the earlier modification was saved
            var peptideTransitionList1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            WaitForDocumentLoaded();
            RunUI(() => 
            {
                var peptideTransitionListBoxes1 = peptideTransitionList1.ComboBoxes;
                Assert.AreEqual(6, peptideTransitionListBoxes1[4].SelectedIndex);
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
            RunUI(() =>
            {
                var transitionBoxes = transitions.ComboBoxes;
                transitionBoxes[0].SelectedIndex = 0; // Set precursor m/z column to 'ignore column'
                transitionBoxes[1].SelectedIndex = 0; // Set product m/z column to 'ignore column'
                transitionBoxes[3].SelectedIndex = 0; // Set peptide modified sequence column to 'ignore column'
            });
            // Change headers of the key columns so that our document will not import
            

            // Click Ok and when it doesn't import due to missing columns close the document
            RunDlg<MessageDlg>(transitions.buttonOk.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            OkDialog(transitions, transitions.CancelDialog);

            // Paste in the same list and verify that the invalid column positions were not saved
            LoadNewDocument(true);
            var transitions1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            WaitForConditionUI(() => transitions1.WindowShown);
            
            RunUI(() =>
            {
                var transitions1Boxes = transitions1.ComboBoxes;
                Assert.AreNotEqual(transitions1Boxes[0].Text, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                Assert.AreNotEqual(transitions1Boxes[1].Text, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                Assert.AreNotEqual(transitions1Boxes[3].Text, Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
            });
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
            RunUI(() =>
            {
                var pep1Boxes = pep1.ComboBoxes;
                Assert.AreNotEqual(pep1Boxes[2].SelectedIndex, 6);
                // Verify that we did use the detected values instead
                Assert.AreEqual(pep1Boxes[4].SelectedIndex, 8);
                Assert.AreEqual(pep1Boxes[11].SelectedIndex, 9);
            });
            
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

            TestPeptideOnlyDoc();
            // We want to ensure that error messages will not be translated even if we are giving peptide error messages in molecule mode
            LoadNewDocument(true);
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules)); // Switch the document to molecule mode

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            RunUI(() => { // Disable the peptide column
                var comboBoxes = dlg.ComboBoxes;
                comboBoxes[1].SelectedIndex = comboBoxes[1].FindStringExact(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
            });
            // As long as the message tells us that we are missing "Peptide Modified Sequence" and not "Molecule Molecule", then everything is working how we want it to
            var msg = ShowDialog<MessageDlg>(dlg.buttonCheckForErrors.PerformClick);
            Assert.IsTrue(msg.Message.Contains(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
            msg.OkDialog();
            OkDialog(dlg, dlg.CancelDialog);

            LoadNewDocument(true); // Tidy up
        }

        private void TestPeptideOnlyDoc()
        {
            // Try a peptide-only document, set up to work with the dimensions of PeptideTransitionList.csv
            CheckDocumentGridAndColumns(Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List,
                9, 21, SrmDocument.DOCUMENT_TYPE.proteomic);
            
        }

        // Tests the current document, important to note that you must have data already in a skyline document for this to work
        private void CheckDocumentGridAndColumns(string viewName,
            int rowCount, int colCount,  // Expected row and column count for document grid
            SrmDocument.DOCUMENT_TYPE expectedDocumentType)
        {
            Assume.AreEqual(expectedDocumentType, SkylineWindow.Document.DocumentType);
            WaitForClosedForm<DocumentGridForm>();
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView(viewName));
            WaitForCondition(() => (documentGrid.RowCount == rowCount)); // Let it initialize
            int iteration = 0;
            WaitForCondition(() =>
            {
                bool result = documentGrid.ColumnCount == colCount;
                if (!result && iteration++ > 9)
                    Assert.AreNotEqual(colCount, documentGrid.ColumnCount);   // Put breakpoint on this line, if you have changed columns and need to update the numbers
                return result;
            }); // Let it initialize

            RunUI(() => documentGrid.Close());
        }

        private void TestMissingFragmentMz()
        {
            // Make sure we properly handle missing fragment mz info
            var saveColumnOrder = Settings.Default.CustomMoleculeTransitionInsertColumnsList;
            var text =  "Peptide Modified Sequence\tPrecursor m/z\tFragment Name\tProduct Charge\nPEPTIDER\t478.738\ty1\t1\nPEPTIDER\t478.738\ty2\t1";
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.textBox1.Text =
                text.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));

            var errDlg = ShowDialog<MessageDlg>(columnSelectDlg.CheckForErrors);
            RunUI(() =>
            {
                Assert.IsTrue(errDlg.Message.Contains(Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_) &&
                              errDlg.Message.Contains(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z),
                    string.Format(
                        "Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                        Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_ +
                        Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                        errDlg.Message));
            });
            OkDialog(errDlg, errDlg.Close);

            OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);
            WaitForClosedForm(importDialog);

            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

    }
}
