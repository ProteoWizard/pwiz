/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests using the ExportAnnotationsDlg to export some properties and read them back in
    /// </summary>
    [TestClass]
    public class ExportAnnotationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportAnnotations()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            string text1 = TextUtil.LineSeparate(
                "C8H10N4O2\t1\tC7H9N4O\t1",
                "C8H10N4O2\t1\tC6H7N3O\t1",
                "C9H13N\t1\tC9H11\t1"
            );
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = text1);

            RunUI(() => {
                colDlg.radioMolecule.PerformClick();
                colDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge);
            });

            OkDialog(colDlg, colDlg.OkDialog);

            var originalDocument = SkylineWindow.Document;
            // First export a CSV where all of the properties are blank
            var blankPropertiesCsv = Path.Combine(TestContext.TestDir, "blankProperties.csv");
            ExportProperties(blankPropertiesCsv);

            // Use the document grid to set the ExplicitCollisionEnergy, ExplicitRetentionTime, and ExplicitRetentionTimeWindow
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView(Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List));
            WaitForConditionUI(() => documentGrid.IsComplete);
            var colExplicitCollisionEnergy =
                documentGrid.FindColumn(PropertyPath.Parse("ExplicitCollisionEnergy"));
            // The three columns we want to paste into are all next to each other. The first two rows belong
            // to the same Precursor. Set the focus to the second row, and the first column that we are pasting into
            RunUI(() =>
            {
                documentGrid.DataGridView.CurrentCell =
                    documentGrid.DataGridView.Rows[1].Cells[colExplicitCollisionEnergy.Index];
                var text = TextUtil.LineSeparate(string.Join("\t", 1.5, 2.5, 3.5), string.Join("\t", 4.5, 5.5, 6.5));
                SetClipboardText(text);
                documentGrid.DataGridView.SendPaste();
            });
            var documentWithAnnotations = SkylineWindow.Document;
            Assert.AreNotEqual(originalDocument, documentWithAnnotations);
            var propertiesCsv = Path.Combine(TestContext.TestDir, "properties.csv");
            ExportProperties(propertiesCsv);

            // Reading back in the blank CSV file should obliterate the properties
            RunUI(()=>SkylineWindow.ImportAnnotations(blankPropertiesCsv));
            Assert.AreEqual(originalDocument, SkylineWindow.Document);
            // Importing the properties in the second CSV should bring them back
            RunUI(()=>SkylineWindow.ImportAnnotations(propertiesCsv));
            Assert.AreEqual(documentWithAnnotations, SkylineWindow.Document);
        }

        /// <summary>
        /// Export the "ExplicitCollisionEnergy", "ExplicitRetentionTime" and "ExplicitRetentionTimeWindow" properties.
        /// </summary>
        private void ExportProperties(string filename)
        {
            var exportAnnotationsDlg = ShowDialog<ExportAnnotationsDlg>(SkylineWindow.ShowExportAnnotationsDlg);
            RunUI(() =>
            {
                exportAnnotationsDlg.SelectedHandlers = exportAnnotationsDlg.Handlers
                    .Where(handler => handler is PrecursorHandler || handler is MoleculeHandler);   
                exportAnnotationsDlg.SelectedProperties = new[]
                    {"ExplicitCollisionEnergy", "ExplicitRetentionTime", "ExplicitRetentionTimeWindow"};
                exportAnnotationsDlg.ExportAnnotations(filename);
            });
            OkDialog(exportAnnotationsDlg, ()=>exportAnnotationsDlg.DialogResult = DialogResult.OK);
        }
    }
}
