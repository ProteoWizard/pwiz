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
using pwiz.Skyline.Util.Extensions;
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
            TestEmptyTransitionList();
            TestMissingFragmentMz();

            // Import a peptide transition list with many recognized headers.
            var lines = File.ReadAllLines(TestFilesDir.GetTestPath("PeptideTransitionListExtendedHeaders.csv"));
            var formColSelections = TestHeaderScenario(lines, true);

            // Save the column selections that actually got used and saved to settings.
            // This includes the correction made to fix the IMS header conflict.
            var savedColSelections = Settings.Default.CustomImportTransitionListColumnTypesList;

            // Replace header line with the column selections as they appeared initially
            // in the ImportTransitionListColumnSelectDlg these will be in the current
            // locale language, to make sure we aren't accidentally english-dependent
            lines[0] = formColSelections.ToDsvLine(TextUtil.SEPARATOR_CSV);
            TestHeaderScenario(lines, true);

            // Remove header line, did we properly remember the order?
            RunUI(() => Settings.Default.CustomImportTransitionListColumnTypesList = savedColSelections);
            lines = lines.Skip(1).ToArray();
            // Note that the returned values here will be localized without the conflict
            formColSelections = TestHeaderScenario(lines);

            // The saved columns should always be in invariant format, which means
            // we just tested using saved invariant settings.
            var invariantColSelections = ImportTransitionListColumnSelectDlg.ColumnNamesInvariant(formColSelections, false);
            Assert.IsTrue(savedColSelections.SequenceEqual(invariantColSelections));

            // If we are currently running in a translated language, however
            if (!formColSelections.SequenceEqual(savedColSelections))
            {
                // Verify that we can use settings saved in locale (for backward compatibility)
                RunUI(() => Settings.Default.CustomImportTransitionListColumnTypesList = formColSelections);
                TestHeaderScenario(lines);
            }

            LoadNewDocument(true);

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            // This will paste in a transition list with headers
            var peptideTransitionList = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() =>
            {
                var peptideBoxes = peptideTransitionList.CurrentColSelections();
                // Column positions are only saved if at least one combobox is changed, so
                // change a couple in order to trigger column saving
                peptideBoxes[14] = ignoreColumn;
                peptideBoxes[4] = labelType;
                peptideTransitionList.SetSelectedColumnTypes(peptideBoxes.ToArray());
            });
            // Clicking the OK button should save the column locations
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(peptideTransitionList, peptideTransitionList.OkDialog);
            }

            // Verify that the correct columns were saved in the settings (N.B. we save the invariant strings to the settings, though we will read localized strings for backward compatibility)
            var expectedColumns = new List<string> {protName, peptide, precursor, ignoreColumn, labelType, product, ignoreColumn, fragName};
            expectedColumns.AddRange(Enumerable.Repeat(ignoreColumn, 13)); // The last 13 should all be 'ignore column'
            CollectionAssert.AreEqual(ImportTransitionListColumnSelectDlg.ColumnNamesInvariant(expectedColumns, false), Settings.Default.CustomImportTransitionListColumnTypesList);
            
            // Paste in the same transition list and verify that the earlier modification was saved
            var peptideTransitionList1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            RunUI(() => Assert.AreEqual(labelType, peptideTransitionList1.CurrentColSelections()[4]));
            OkDialog(peptideTransitionList1, peptideTransitionList1.CancelDialog);

            LoadNewDocument(true);

            // This will paste in the same transition list, but with different headers. The program should realize it has
            // different headers and not use the saved list of column names
            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionListdiffheaders.csv")));

            var peptideTransitionListDiffHeaders = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            RunUI(() =>
            {
                var diffPeptideBoxes = peptideTransitionListDiffHeaders.CurrentColSelections();
                // Checks that the program did not use the saved indices
                Assert.AreNotEqual(diffPeptideBoxes[4], 1);
            });
            OkDialog(peptideTransitionListDiffHeaders, peptideTransitionListDiffHeaders.CancelDialog);

            // Now check UI interactions with a bad import file
            LoadNewDocument(false);

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            var dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() => {
                // Checks that two comboboxes cannot have the same header (unless it is Ignore Column)
                dlg.SetSelectedColumnTypes(dlg.SupportedColumnTypes[1]); // Set 1st column
                dlg.SetSelectedColumnTypes(null, dlg.SupportedColumnTypes[1]); // Leave 1st column alone, try to set 2nd column to same value
                var columnTypes = dlg.CurrentColSelections();
                Assert.AreNotEqual(columnTypes[0], columnTypes[1]);

                dlg.SetSelectedColumnTypes(null, null, ignoreColumn, ignoreColumn); // Leave first two columns alone, set next two to "ignore"
                columnTypes = dlg.CurrentColSelections();
                Assert.AreEqual(columnTypes[2], columnTypes[3]);
                // Checks resizing of comboboxes 
                var oldBoxWidth = dlg.ColumnTypeControlWidths[0];
                dlg.dataGrid.Columns[0].Width -= 20;
                Assert.AreNotEqual(oldBoxWidth, dlg.ColumnTypeControlWidths[0]);
            });
            // Clicking the "Check for Errors" button should bring up the special column missing dialog
            RunDlg<MessageDlg>(dlg.buttonCheckForErrors.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            // Clicking OK while input is invalid should also pop up the error list
            RunDlg<MessageDlg>(dlg.buttonOk.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            // Only way out without fixing the columns is to cancel
            OkDialog(dlg, dlg.CancelDialog);

            // Now verify that we do not save invalid sets of headers
            LoadNewDocument(false);

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("ThermoTransitionList.csv")));

            // Change headers of the key columns so that our document will not import
            var transitions = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            RunUI(() =>
            {
                transitions.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Set precursor m/z column to 'ignore column'
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column, // Set product m/z column to 'ignore column'
                    null, // No change
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column); // Set peptide modified sequence column to 'ignore column'
            });

            // Click Ok and when it doesn't import due to missing columns close the document
            RunDlg<MessageDlg>(transitions.buttonOk.PerformClick, messageDlg => { messageDlg.OkDialog(); });   // Dismiss it
            OkDialog(transitions, transitions.CancelDialog);

            // Paste in the same list and verify that the invalid column positions were not saved
            LoadNewDocument(true);
            var transitions1 = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            WaitForConditionUI(() => transitions1.WindowShown);
            
            RunUI(() =>
            {
                var transitions1Boxes = transitions1.CurrentColSelections();
                Assert.AreNotEqual(transitions1Boxes[0], Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                Assert.AreNotEqual(transitions1Boxes[1], Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
                Assert.AreNotEqual(transitions1Boxes[3], Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
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
                // Changes the combo box currently labeled "fragment name" to "ignore column"
                pep.SetSelectedColumnTypes(null, null, null, null, null, null, null, ignoreColumn);
            });
            OkDialog(pep, pep.OkDialog);
            
            // Verify that the change and other columns were saved in the settings
            Assert.AreEqual(ImportTransitionListColumnSelectDlg.ColumnNameInvariant(ignoreColumn), Settings.Default.CustomImportTransitionListColumnTypesList[7]);
            Assert.AreEqual(ImportTransitionListColumnSelectDlg.ColumnNameInvariant(protName), Settings.Default.CustomImportTransitionListColumnTypesList[0]);
            Assert.AreEqual(ImportTransitionListColumnSelectDlg.ColumnNameInvariant(peptide), Settings.Default.CustomImportTransitionListColumnTypesList[1]);

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
                var pep1Boxes = pep1.CurrentColSelections();
                Assert.AreNotEqual(pep1Boxes[2], pep1.SupportedColumnTypes[6]);
                // Verify that we did use the detected values instead
                Assert.AreEqual(pep1Boxes[4], pep1.SupportedColumnTypes[8]);
                Assert.AreEqual(pep1Boxes[11], pep1.SupportedColumnTypes[9]);
            });
            
            // Close the document
            OkDialog(pep1, pep1.CancelDialog);

            // Now check UI interactions with a bad import file whose headers we correct in the dialog
            using (new CheckDocumentState(1,2,2,9))
            {
                LoadNewDocument(false);

                SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
                dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

                // Correct the header assignments
                RunUI(() => {
                    dlg.SetSelectedColumnTypes(
                        null,
                        Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence,
                        Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z,
                        null, null, null, null, null, null, null, null, null, null, null,
                        Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column);
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

            RunUI(() => {
                dlg.radioPeptide.PerformClick();
                dlg.SetSelectedColumnTypes(
                    null, // No change
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Ignore_Column,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z);
            });
            // As long as the message tells us that we are missing "Peptide Modified Sequence" and not "Molecule Molecule", then everything is working how we want it to
            var msg = ShowDialog<MessageDlg>(dlg.buttonCheckForErrors.PerformClick);
            Assert.IsTrue(msg.Message.Contains(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence));
            OkDialog(msg, msg.OkDialog);
            OkDialog(dlg, dlg.CancelDialog);

            LoadNewDocument(true); // Tidy up
        }

        /// <summary>
        /// Test transition list import for various header and column selection states
        /// </summary>
        /// <param name="lines">The lines to be imported</param>
        /// <param name="expectHeaderConflict">True if a conflict in the IMS columns is expected</param>
        /// <returns>A list of strings with the localized text of the combo box selections when
        /// <see cref="ImportTransitionListColumnSelectDlg"/> was first shown</returns>
        private List<string> TestHeaderScenario(string[] lines, bool expectHeaderConflict = false)
        {
            LoadNewDocument(true);
            var doc = SkylineWindow.Document;
            SetClipboardText(TextUtil.LineSeparate(lines));
            var nextHeader = string.Empty;
            var therm = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            int invK0Col = 0;
            int covCol = 0;
            var thermBoxes = new List<string>();
            RunUI(() =>
            {
                thermBoxes = therm.CurrentColSelections();
                // Checks that automatically assigning column headers works properly
                AssertEx.AreEqual(protName, thermBoxes[0]);
                AssertEx.AreEqual(peptide, thermBoxes[1]);
                AssertEx.AreEqual(precursor, thermBoxes[2]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Energy, thermBoxes[4]);
                AssertEx.AreEqual(product, thermBoxes[5]);
                AssertEx.AreEqual(fragName, thermBoxes[7]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time, thermBoxes[10]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Retention_Time_Window, thermBoxes[11]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Note, thermBoxes[12]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_S_Lens, thermBoxes[13]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Cone_Voltage, thermBoxes[14]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility__msec_, thermBoxes[15]);
                if (expectHeaderConflict)
                    AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility__1_K0_, thermBoxes[invK0Col = 16]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility, thermBoxes[17]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_Units, thermBoxes[18]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Ion_Mobility_High_Energy_Offset, thermBoxes[19]);
                AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Collision_Cross_Section__sq_A_, thermBoxes[20]);
                if (expectHeaderConflict)
                    AssertEx.AreEqual(Resources.PasteDlg_UpdateMoleculeType_Explicit_Compensation_Voltage, thermBoxes[covCol = 21]);
                AssertEx.AreEqual(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Explicit_Declustering_Potential, thermBoxes[22]);
            });
            if (expectHeaderConflict)
            {
                // Attempt to proceed, should get an error about conflicting ion mobility declarations
                foreach (var col in new[] { invK0Col, covCol }) // Turn off conflicting columns one at a time
                {
                    var errDlg = ShowDialog<ImportTransitionListErrorDlg>(therm.OkDialog);
                    AssertEx.IsTrue(errDlg.ErrorList.Any(err =>
                        err.ErrorMessage.Contains(Resources
                            .SmallMoleculeTransitionListReader_ReadPrecursorOrProductColumns_Multiple_ion_mobility_declarations)));
                    OkDialog(errDlg, errDlg.Close);
                    RunUI(() => therm.ComboBoxes[col].SelectedIndex = 0); // Ignore the 1/K0 and CoV values
                }
            }

            OkDialog(therm, therm.OkDialog);
            doc = WaitForDocumentChange(doc);
            var transitionGroupDocNodes = doc.MoleculeTransitionGroups.ToArray();
            AssertEx.AreEqual(0.5, transitionGroupDocNodes[0].ExplicitValues.IonMobility);
            AssertEx.AreEqual(330, transitionGroupDocNodes[0].ExplicitValues.CollisionalCrossSectionSqA);
            AssertEx.AreEqual(0.6, transitionGroupDocNodes[1].ExplicitValues.IonMobility);
            AssertEx.AreEqual(330.3, transitionGroupDocNodes[1].ExplicitValues.CollisionalCrossSectionSqA);
            var transitionDocNodes = doc.MoleculeTransitions.ToArray();
            AssertEx.AreEqual(9, transitionDocNodes[0].ExplicitValues.DeclusteringPotential);
            AssertEx.AreEqual(1.1, transitionDocNodes[1].ExplicitValues.SLens);
            AssertEx.AreEqual(1.12, transitionDocNodes[1].ExplicitValues.ConeVoltage);
            AssertEx.AreEqual(-0.2, transitionDocNodes[1].ExplicitValues.IonMobilityHighEnergyOffset);
            AssertEx.AreEqual(-0.3, transitionDocNodes[2].ExplicitValues.IonMobilityHighEnergyOffset);

            return thermBoxes;
        }

        private void TestPeptideOnlyDoc()
        {
            // Try a peptide-only document, set up to work with the dimensions of PeptideTransitionList.csv
            CheckDocumentGridAndColumns(Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List,
                9, 21, SrmDocument.DOCUMENT_TYPE.proteomic);
            
        }

        private void TestEmptyTransitionList()
        {
            var text = "Peptide Modified Sequence\tPrecursor m/z\tFragment Name\tProduct Charge\n\n";
            var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            var errDlg = ShowDialog<MessageDlg>(() => importDialog.TransitionListText = text); // Testing that we catch the exception properly
            OkDialog(errDlg, errDlg.Close);
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
            var columnSelectDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText =
                text.Replace(".", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator));

            var errDlg = ShowDialog<MessageDlg>(columnSelectDlg.CheckForErrors);
            RunUI(() =>
            {
                Assert.IsTrue(errDlg.Message.Contains(Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_) &&
                              errDlg.Message.Contains(Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z),
                    "Unexpected value in paste dialog error window:\r\nexpected \"{0}\"\r\ngot \"{1}\"",
                    Resources.ImportTransitionListErrorDlg_ImportTransitionListErrorDlg_This_transition_list_cannot_be_imported_as_it_does_not_provide_values_for_ +
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z,
                    errDlg.Message);
            });
            OkDialog(errDlg, errDlg.Close);

            OkDialog(columnSelectDlg, columnSelectDlg.CancelDialog);
            WaitForClosedForm(importDialog);

            RunUI(() => Settings.Default.CustomMoleculeTransitionInsertColumnsList = saveColumnOrder);
        }

    }
}
