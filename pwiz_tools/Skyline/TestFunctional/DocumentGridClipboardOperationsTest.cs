/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DocumentGridClipboardOperationsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGridClipboardOperations()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Need to be in mixed UI mode to see both options for paste dialog
            RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed));

            var importDialog3 = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            string text1 = TextUtil.LineSeparate(
                "Drugs\tCaffeine\tLoss of CHO\tC8H10N4O2\tC7H9N4O\t1\t1",
                "Drugs\tCaffeine\tLoss of CH3NCO\tC8H10N4O2\tC6H7N3O\t1\t1",
                "Drugs\tAmphetamine\tLoss of Ammonia\tC9H13N\tC9H11\t1\t1"
            );
            var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog3.TransitionListText = text1);

            RunUI(() => {
                colDlg.radioMolecule.PerformClick();
                colDlg.SetSelectedColumnTypes(Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name, 
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Name,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge,
                    Resources.PasteDlg_UpdateMoleculeType_Product_Charge
                );
            });

            OkDialog(colDlg, colDlg.OkDialog);

            RunUI(() => SkylineWindow.SequenceTree.SelectPath(new IdentityPath(SequenceTree.NODE_INSERT_ID)));
            RunDlg<PasteDlg>(SkylineWindow.ShowPastePeptidesDlg, pasteDlg =>
            {
                string text = TextUtil.LineSeparate("RPKPQQFFGLM\tSubstance P", 
                    "DVPKSDQFVGLM\tKassinin");
                SetClipboardText(text);
                pasteDlg.PastePeptides();
                pasteDlg.OkDialog();
            });
            
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Molecules));
            WaitForConditionUI(() => documentGrid.IsComplete);
            Assert.AreEqual(4, documentGrid.DataGridView.Rows.Count);
            foreach (var molecule in SkylineWindow.Document.Molecules)
            {
                Assert.IsNull(molecule.Note);
                Assert.IsNull(molecule.ExplicitRetentionTime);
            }
            // Test ability to set explicit retention time for peptides and small molecules
            var explicitRetentionTime = 10.0;
            RunUI(() =>
            {
                var colExplicitRetentionTime =
                    documentGrid.FindColumn(PropertyPath.Root.Property("ExplicitRetentionTime"));
                documentGrid.DataGridView.CurrentCell =  documentGrid.DataGridView.Rows[0].Cells[colExplicitRetentionTime.Index];
                documentGrid.DataGridView.CurrentCell.Value = explicitRetentionTime;
                for (int iRow = 0; iRow < documentGrid.DataGridView.RowCount; iRow++)
                {
                    documentGrid.DataGridView.Rows[iRow].Cells[colExplicitRetentionTime.Index].Selected = true;
                }
            });
            RunUI(() => documentGrid.DataboundGridControl.FillDown());
            foreach (var molecule in SkylineWindow.Document.Molecules)
            {
                Assert.IsNull(molecule.Note);
                Assert.AreEqual(explicitRetentionTime, molecule.ExplicitRetentionTime.RetentionTime);    
            }
            RunUI(() =>
            {
                var colNote = documentGrid.FindColumn(PropertyPath.Root.Property("Note"));
                documentGrid.DataGridView.CurrentCell = documentGrid.DataGridView.Rows[0].Cells[colNote.Index];
                documentGrid.DataGridView.CurrentCell.Value = "PeptideNote";
                for (int iRow = 0; iRow < documentGrid.DataGridView.RowCount; iRow++)
                {
                    documentGrid.DataGridView.Rows[iRow].Cells[colNote.Index].Selected = true;
                }
            });
            RunUI(()=>documentGrid.DataboundGridControl.FillDown());
            foreach (var molecule in SkylineWindow.Document.Molecules)
            {
                Assert.AreEqual("PeptideNote", molecule.Note);
                Assert.AreEqual(explicitRetentionTime, molecule.ExplicitRetentionTime.RetentionTime);
            }
            // Undoing the fill down operation will take us back to just the first peptide having
            // a note.
            RunUI(SkylineWindow.Undo);
            WaitForConditionUI(() => documentGrid.IsComplete);
            foreach (var molecule in SkylineWindow.Document.Molecules)
            {
                if (ReferenceEquals(molecule, SkylineWindow.Document.Molecules.First()))
                {
                    Assert.AreEqual("PeptideNote", molecule.Note);
                }
                else
                {
                    Assert.IsNull(molecule.Note);
                }
                Assert.AreEqual(explicitRetentionTime, molecule.ExplicitRetentionTime.RetentionTime);
            }
        }
    }
}
