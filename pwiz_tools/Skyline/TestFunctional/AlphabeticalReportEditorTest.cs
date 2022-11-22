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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests toggling the "Alphabetical" button at the top of the Report Editor.
    /// </summary>
    [TestClass]
    public class AlphabeticalReportEditorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAlphabeticalReportEditor()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string reportName = "My Custom Report";
            Assert.IsFalse(Settings.Default.AlphabeticalReportEditor);
            RunUI(()=>SkylineWindow.ShowDocumentGrid(true));
            var documentGrid = FindOpenForm<DocumentGridForm>();
            WaitForCondition(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                Assert.IsFalse(viewEditor.Alphabetical);
                viewEditor.Alphabetical = true;
                viewEditor.CancelButton.PerformClick();
            });
            
            // Settings should not be remembered if the dialog is cancelled.
            Assert.IsFalse(Settings.Default.AlphabeticalReportEditor);
            PropertyPath ppProteins = PropertyPath.Root.Property(nameof(SkylineDocument.Proteins)).LookupAllItems();
            PropertyPath ppPeptides = ppProteins.Property(nameof(Protein.Peptides)).LookupAllItems();
            PropertyPath ppInChI = ppPeptides.Property(nameof(Peptide.InChI));
            PropertyPath ppPrecursors = ppPeptides.Property(nameof(Peptide.Precursors)).LookupAllItems();

            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                Assert.IsFalse(viewEditor.Alphabetical);
                SetUiMode(viewEditor, UiModes.MIXED);
                var columnTree = viewEditor.ChooseColumnsTab.AvailableFieldsTree;
                columnTree.SelectColumn(ppInChI);
                viewEditor.ChooseColumnsTab.AddColumn(ppInChI);
                var treeNodePeptide = columnTree.SelectedNode.Parent;
                int peptidePropertyCount = treeNodePeptide.Nodes.Count;
                var propertyPathFirstColumn = columnTree.GetTreeColumn(treeNodePeptide.Nodes[0]).PropertyPath;
                Assert.AreEqual(ppPrecursors, propertyPathFirstColumn);

                Assert.AreEqual(ppInChI, columnTree.GetTreeColumn(columnTree.SelectedNode).PropertyPath);
                viewEditor.Alphabetical = true;
                Assert.AreEqual(ppInChI, columnTree.GetTreeColumn(columnTree.SelectedNode).PropertyPath);
                treeNodePeptide = columnTree.SelectedNode.Parent;
                Assert.AreEqual(peptidePropertyCount, treeNodePeptide.Nodes.Count);
                for (int i = 1; i < treeNodePeptide.Nodes.Count; i++)
                {
                    string text1 = treeNodePeptide.Nodes[i - 1].Text;
                    string text2 = treeNodePeptide.Nodes[i].Text;
                    Assert.IsTrue(StringComparer.CurrentCultureIgnoreCase.Compare(text1, text2) <= 0, "{0} should sort before {1}", text1, text2);
                }

                viewEditor.ViewName = reportName;

                viewEditor.ShowHiddenFields = true;
                viewEditor.OkDialog();
            });
            Assert.IsTrue(Settings.Default.AlphabeticalReportEditor);

            RunDlg<ViewEditor>(documentGrid.NavBar.CustomizeView, viewEditor =>
            {
                Assert.IsTrue(viewEditor.Alphabetical);
                // "Show Hidden Fields" should always start out false
                Assert.IsFalse(viewEditor.ShowHiddenFields);
                viewEditor.ChooseColumnsTab.AddColumn(ppInChI);

                // Switch the UI mode of the report to "proteomic". The InChI column is still in the report, even though it's hidden
                SetUiMode(viewEditor, UiModes.PROTEOMIC);
                Assert.IsFalse(viewEditor.ShowHiddenFields);

                // "Show Hidden Fields" gets turned on when we tell the tree to select a column which is currently hidden
                viewEditor.ChooseColumnsTab.AvailableFieldsTree.SelectColumn(ppInChI);
                Assert.IsTrue(viewEditor.ShowHiddenFields);
                viewEditor.OkDialog();
            });
        }

        private void SetUiMode(ViewEditor viewEditor, string uiMode)
        {
            var parentColumn = viewEditor.ParentColumn;
            var newParentColumn =
                ColumnDescriptor.RootColumn(parentColumn.DataSchema, parentColumn.PropertyType, uiMode);
            var newViewInfo = new ViewInfo(newParentColumn, viewEditor.ViewInfo.ViewSpec.SetUiMode(uiMode));
            viewEditor.SetViewInfo(newViewInfo, Array.Empty<PropertyPath>());
        }
    }
}
