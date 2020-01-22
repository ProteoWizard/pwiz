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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.Databinding.RowActions;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests the "Actions" dropdown button on the Document Grid which allows removing peaks and deleting nodes.
    /// </summary>
    [TestClass]
    public class RowActionsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRowActions()
        {
            TestFilesZip = @"TestFunctional\RowActionsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestDeletePeptides();
            TestRemovePrecursorPeaks();
            TestRemovePivotedPeptides();
        }

        /// <summary>
        /// Tests using the "Delete Peptides" button. Selects all of the rows,
        /// and ensures that all peptides got deleted from the document.
        /// </summary>
        private void TestDeletePeptides()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RowActionsTest.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(new ViewName(ViewGroup.BUILT_IN.Id,
                Resources.SkylineViewContext_GetDocumentGridRowSources_Peptides)));
            WaitForConditionUI(() => documentGrid.IsComplete);
            VerifyActionsEnabled(documentGrid.DataboundGridControl, DeleteNodesAction.Peptides, DeleteNodesAction.Proteins);
            RunUI(() =>
            {
                documentGrid.DataGridView.SelectAll();
            });
            var deletePeptidesButton = GetDropDownItems(documentGrid.NavBar.ActionsButton)
                .FirstOrDefault(item => item.Text == DeleteNodesAction.Peptides.GetMenuItemText(SrmDocument.DOCUMENT_TYPE.proteomic));
            Assert.IsNotNull(deletePeptidesButton);
            var alertDlg = ShowDialog<AlertDlg>(deletePeptidesButton.PerformClick);
            Assert.AreNotEqual(0, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(string.Format(Resources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__peptides_, SkylineWindow.Document.MoleculeCount),
                alertDlg.Message);
            OkDialog(alertDlg, alertDlg.ClickOk);
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual(0, SkylineWindow.Document.MoleculeGroupCount);
            alertDlg = ShowDialog<AlertDlg>(SkylineWindow.NewDocument);
            OkDialog(alertDlg, alertDlg.ClickNo);
        }

        /// <summary>
        /// Tests using the "Remove Precursor Peaks" button.
        /// This test removes peaks from the "614" precursor of the "SIV..." peptide.
        /// It removes peaks from the replicates "S_1" and "S_3".
        /// </summary>
        private void TestRemovePrecursorPeaks()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RowActionsTest.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.DataboundGridControl.ChooseView(new ViewName(PersistedViews.MainGroup.Id,
                "TransitionAreas")));
            WaitForConditionUI(() => documentGrid.IsComplete);

            var peptideDocNode = SkylineWindow.Document.Peptides.Skip(1).First();
            StringAssert.StartsWith(peptideDocNode.Peptide.Sequence, "SIV");
            Assert.AreEqual(2, peptideDocNode.TransitionGroupCount);
            foreach (var precursor in peptideDocNode.TransitionGroups)
            {
                Assert.AreEqual(3, precursor.Results.Count);
                for (int iReplicate = 0; iReplicate < 3; iReplicate++)
                {
                    Assert.AreEqual(1, precursor.Results[iReplicate].Count);
                    var precursorChromInfo = precursor.Results[iReplicate].First();
                    Assert.IsNotNull(precursorChromInfo.Area);
                }
            }
            RunUI(() =>
            {
                var colPeptide = documentGrid.FindColumn(PropertyPath.Parse("Precursor.Peptide"));
                Assert.IsNotNull(colPeptide);
                var colPrecursorMz = documentGrid.FindColumn(PropertyPath.Parse("Precursor.Mz"));
                Assert.IsNotNull(colPrecursorMz);
                var colReplicate = documentGrid.FindColumn(PropertyPath.Parse("Results!*.Value.PrecursorResult.PeptideResult.ResultFile.Replicate"));
                Assert.IsNotNull(colReplicate);
                for (int iRow = 0; iRow < documentGrid.RowCount; iRow++)
                {
                    var row = documentGrid.DataGridView.Rows[iRow];
                    var peptide = (Skyline.Model.Databinding.Entities.Peptide) row.Cells[colPeptide.Index].Value;
                    var precursorMz = (double) row.Cells[colPrecursorMz.Index].Value;
                    var replicate = (Replicate) row.Cells[colReplicate.Index].Value;
                    if (peptide.Sequence.StartsWith("SIV") && Math.Abs(precursorMz - 614) < 1 &&
                        (replicate.Name == "S_1" || replicate.Name == "S_3"))
                    {
                        row.Selected = true;
                    }
                }
            });
            var menuItem = GetDropDownItems(documentGrid.NavBar.ActionsButton)
                .FirstOrDefault(item => item.Text == RemovePeaksAction.Precursors.GetMenuItemText(SrmDocument.DOCUMENT_TYPE.proteomic));
            Assert.IsNotNull(menuItem);
            var confirmDialog = ShowDialog<AlertDlg>(menuItem.PerformClick);
            string expectedMessage = string.Format(
                Resources.RemovePrecursors_GetConfirmRemoveMessage_Are_you_sure_you_want_to_remove_these__0__peaks_from_one_precursor_, 
                2);
            Assert.AreEqual(expectedMessage, confirmDialog.Message);
            OkDialog(confirmDialog, confirmDialog.ClickOk);
            peptideDocNode = SkylineWindow.Document.Peptides.Skip(1).First();
            Assert.AreEqual(2, peptideDocNode.TransitionGroupCount);
            for (int iPrecursor = 0; iPrecursor < 2; iPrecursor++)
            {
                var precursor = (TransitionGroupDocNode) peptideDocNode.Children[iPrecursor];
                Assert.AreEqual(3, precursor.Results.Count);
                for (int iReplicate = 0; iReplicate < 3; iReplicate++)
                {
                    Assert.AreEqual(1, precursor.Results[iReplicate].Count);
                    var precursorChromInfo = precursor.Results[iReplicate].First();
                    bool shouldHaveValue = iPrecursor == 0 || iReplicate == 1;
                    Assert.AreEqual(shouldHaveValue, precursorChromInfo.Area.HasValue);
                }
            }
            var alertDlg = ShowDialog<AlertDlg>(SkylineWindow.NewDocument);
            OkDialog(alertDlg, alertDlg.ClickNo);
        }

        /// <summary>
        /// Tests deleting peptides when the view has been pivoted.
        /// This test deletes the peptides which are in rows 4 through 9.
        /// </summary>
        private void TestRemovePivotedPeptides()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RowActionsTest.sky")));
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            var viewName = new ViewName(PersistedViews.MainGroup.Id, "TransitionAreas");
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(viewName);
            });
            Assert.AreEqual(4, SkylineWindow.Document.MoleculeCount);

            var layoutList = documentGrid.DataboundGridControl.BindingListSource.ViewContext.GetViewLayoutList(viewName);
            var layout = layoutList.FindLayout("GroupedByPeptideAndReplicate");
            Assert.IsNotNull(layout);
            RunUI(()=>documentGrid.DataboundGridControl.BindingListSource.ApplyLayout(layout));
            WaitForConditionUI(() => documentGrid.IsComplete);
            WaitForConditionUI(() => 3 == documentGrid.DataGridView.ColumnCount);
            Assert.AreEqual(12, documentGrid.DataGridView.RowCount);
            RunUI(() =>
            {
                for (int i = 0; i < documentGrid.DataGridView.RowCount; i++)
                {
                    documentGrid.DataGridView.Rows[i].Selected = i >= 3 && i < 9;
                }
            });
            var deletePeptidesAction = GetDropDownItems(documentGrid.NavBar.ActionsButton)
                .FirstOrDefault(item => item.Text == DeleteNodesAction.Peptides.GetMenuItemText(SrmDocument.DOCUMENT_TYPE.proteomic));
            Assert.IsNotNull(deletePeptidesAction);
            var confirmDialog = ShowDialog<AlertDlg>(deletePeptidesAction.PerformClick);
            string expectedMessage = string.Format(
                Resources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__peptides_, 2);
            Assert.AreEqual(expectedMessage, confirmDialog.Message);
            OkDialog(confirmDialog, confirmDialog.ClickOk);
            Assert.AreEqual(2, SkylineWindow.Document.MoleculeCount);
            Assert.AreEqual("GLVLIAFSQYLQQCPFDEHVK", SkylineWindow.Document.Molecules.First().Peptide.Sequence);
            Assert.AreEqual("LVNELTEFAK", SkylineWindow.Document.Molecules.Skip(1).First().Peptide.Sequence);
            var alertDlg = ShowDialog<AlertDlg>(SkylineWindow.NewDocument);
            OkDialog(alertDlg, alertDlg.ClickNo);
        }

        public void VerifyActionsEnabled(DataboundGridControl databoundGridControl, params RowAction[] rowActions)
        {
            var enabledMenuItems = new HashSet<string>(rowActions.Select(action => action.GetMenuItemText(SrmDocument.DOCUMENT_TYPE.proteomic)));
            var menuItems = GetDropDownItems(databoundGridControl.NavBar.ActionsButton);
            Assert.AreNotEqual(0, menuItems.Length);
            foreach (var item in menuItems)
            {
                Assert.AreEqual(enabledMenuItems.Contains(item.Text), item.Enabled);
            }
        }

        private ToolStripMenuItem[] GetDropDownItems(ToolStripDropDownItem button)
        {
            ToolStripMenuItem[] items = null;
            RunUI(() =>
            {
                button.ShowDropDown();
                items = button.DropDownItems.OfType<ToolStripMenuItem>().ToArray();
                button.HideDropDown();
            });
            return items;
        }
    }
}
