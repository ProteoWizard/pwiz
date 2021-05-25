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
    public class PasteTransitionListTest : AbstractFunctionalTest
    {
        private string precursor => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_m_z;
        private string product => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Product_m_z;
        private string peptide => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Peptide_Modified_Sequence;
        private string protName => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Protein_Name;
        private string fragName => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Fragment_Name;
        private string label  => Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Label_Type;

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
                // Modifies the selected index of one box so later we can test if it saved 
                thermBoxes[4].SelectedIndex = 2;
            });
            RunDlg<ImportTransitionListErrorDlg>(therm.OkDialog, errDlg => errDlg.Close());
            OkDialog(therm, therm.CancelDialog);
            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();

            // This will paste in the ThermoTransitionList a second time
            var secondtherm = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() => {
                var secondBoxes = secondtherm.ComboBoxes;
                // Checks that the modified index was saved 
                Assert.AreEqual(2, secondBoxes[4].SelectedIndex);
                // Checks that the the text in the combo box reflects the change
                Assert.AreNotEqual(protName, secondBoxes[4].Text);
                secondtherm.CancelDialog();
            });

            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionList.csv")));
            // This will paste in a transition list with headers
            var peptideTransitionList = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());

            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();
            RunUI(() =>
            {
                var peptideBoxes = peptideTransitionList.ComboBoxes;
                // Changes the contents of a combo box
                peptideBoxes[4].SelectedIndex = 1;
            });
            // Clicking the Ok button will the change
            RunDlg<ImportTransitionListErrorDlg>(peptideTransitionList.OkDialog, errDlg => errDlg.Close());
            OkDialog(peptideTransitionList, peptideTransitionList.CancelDialog);

            // This will paste in the same transition list, but with different headers. The program should realize it has
            // different headers and not use the saved list of column names
            SetClipboardText(File.ReadAllText(TestFilesDir.GetTestPath("PeptideTransitionListdiffheaders.csv")));

            var peptideTransitionListDiffHeaders = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();
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

            RunUI(() => SkylineWindow.NewDocument(true)); // Tidy up
        }
    }
}
