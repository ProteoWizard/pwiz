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

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for ImportTransitionListColumnSelectDlg.
    /// </summary>
    [TestClass]
    public class PasteTransitionListTest : AbstractFunctionalTest
    {
        private string precursor = "Precursor m/z";
        private string product = "Product m/z";
        private string peptide = "Peptide Modified Sequence";
        private string protName = "Protein Name";
        private string fragName = "Fragment Name";
        private string preCharge = "Precursor Charge";
        private string label = "Label Type";

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

            SetClipboardText(System.IO.File.ReadAllText(TestFilesDir.GetTestPath("ThermoTransitionList.csv")));
            var therm = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
            
            RunUI(() => {
                var thermBoxes = therm.ComboBoxes;

                // Checks that automatically assigning column headers works properly
                Assert.AreEqual(precursor, thermBoxes[0].Text);
                Assert.AreEqual(product, thermBoxes[1].Text);
                Assert.AreEqual(peptide, thermBoxes[3].Text);
                Assert.AreEqual(protName, thermBoxes[4].Text);
                Assert.AreEqual(fragName, thermBoxes[5].Text);
                Assert.AreEqual(preCharge, thermBoxes[6].Text);
                Assert.AreEqual(label, thermBoxes[7].Text);
                // Modifies the selected index of one of the combo boxes and then verifies that it was saved 
                thermBoxes[4].SelectedIndex = 2;
                therm.CancelDialog();
                SkylineWindow.NewDocument();
                WaitForDocumentLoaded();
                Assert.AreEqual(2, thermBoxes[4].SelectedIndex);
                Assert.AreNotEqual(protName, thermBoxes[4].Text);

            });

            RunUI(() => SkylineWindow.NewDocument());

            WaitForDocumentLoaded();

            SetClipboardText(System.IO.File.ReadAllText(TestFilesDir.GetTestPath("Peptide Transition List.csv")));

            RunUI(() => {
                var dlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => SkylineWindow.Paste());
                var comboBoxes = dlg.ComboBoxes;

                comboBoxes[0].SelectedIndex = 1;
                comboBoxes[1].SelectedIndex = 1;
                Assert.AreNotEqual(comboBoxes[0], comboBoxes[1]);

                comboBoxes[2].SelectedIndex = 0;
                comboBoxes[3].SelectedIndex = 0;
                Assert.AreEqual(comboBoxes[2].Text, comboBoxes[3].Text);

                var oldBoxWidth = comboBoxes[0].Width;
                dlg.dataGrid.Columns[0].Width -= 20;
                Assert.AreNotEqual(oldBoxWidth, comboBoxes[0].Width);
            });
            
            var importTransitionListErrorDlg = ShowDialog<ImportTransitionListErrorDlg>(() => dlg.buttonCheckForErrors.PerformClick());

            // ReSharper disable once AccessToModifiedClosure (The okAction is executed immediately inside OkDialog so there is no chance of importTransitionListErrorDlg being modified)
            OkDialog(importTransitionListErrorDlg, () => importTransitionListErrorDlg.DialogResult = DialogResult.OK);

            importTransitionListErrorDlg = ShowDialog<ImportTransitionListErrorDlg>(() => dlg.DialogResult = DialogResult.OK);

            OkDialog(importTransitionListErrorDlg, () => importTransitionListErrorDlg.DialogResult = DialogResult.OK);

            
        }
    }
}
