/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for Annotations.
    /// </summary>
    [TestClass]
    public class AnnotationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestAnnotations()
        {
            TestFilesZip = @"TestFunctional\AnnotationTest.zip";
            RunFunctionalTest();
        }

        private const string COL_PROTEIN_TEXT = "proteinText";
        private const string COL_PRECURSOR_RESULTS_ITEMS = "precursor-Result+Items";

        /// <summary>
        /// Test annotations.  Defines some annotations, then opens up a Skyline document that has 
        /// results and optimization steps in it.  Enables the annotations in the document, and sets
        /// some values.
        /// </summary>
        protected override void DoTest()
        {
            var chooseAnnotationsDlg = ShowDialog<ChooseAnnotationsDlg>(SkylineWindow.ShowAnnotationsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(chooseAnnotationsDlg.EditList);
            // Define the annotations that we are going to be using in this test.
            RunUI(editListDlg.ResetList);
            DefineAnnotation(editListDlg, COL_PROTEIN_TEXT, AnnotationDef.AnnotationTarget.protein, AnnotationDef.AnnotationType.text, null);
            DefineAnnotation(editListDlg, "peptide Items", AnnotationDef.AnnotationTarget.peptide,
                             AnnotationDef.AnnotationType.value_list, new[] {"one","two","three"});
            DefineAnnotation(editListDlg, "precursor 'True' 'False'", AnnotationDef.AnnotationTarget.precursor,
                             AnnotationDef.AnnotationType.true_false, null);
            DefineAnnotation(editListDlg, "transition_Text", AnnotationDef.AnnotationTarget.transition, 
                AnnotationDef.AnnotationType.text, null);
            DefineAnnotation(editListDlg, COL_PRECURSOR_RESULTS_ITEMS, AnnotationDef.AnnotationTarget.precursor_result,
                             AnnotationDef.AnnotationType.value_list, new[] {"a", "b", "c"});
            DefineAnnotation(editListDlg, "transition_True|False_", AnnotationDef.AnnotationTarget.transition_result,
                             AnnotationDef.AnnotationType.true_false, null);
            DefineAnnotation(editListDlg, "\"all\"",
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein, 
                             AnnotationDef.AnnotationType.text, null);
            DefineAnnotation(editListDlg, "precursor transition text",
                             AnnotationDef.AnnotationTarget.precursor_result |
                             AnnotationDef.AnnotationTarget.transition_result, AnnotationDef.AnnotationType.text,
                             new[] {"x", "y", "z"});
            OkDialog(editListDlg, ()=>editListDlg.DialogResult = DialogResult.OK);
            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.Close);
            // Open the .sky file
            RunUI(() =>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini_missing_results.sky")));
            // Turn on the annotations
            chooseAnnotationsDlg = ShowDialog<ChooseAnnotationsDlg>(SkylineWindow.ShowAnnotationsDialog);
            RunUI(() =>
                      {
                          var checkedListBox = chooseAnnotationsDlg.AnnotationsCheckedListBox;
                          for (int i = 0; i < checkedListBox.Items.Count; i++)
                          {
                              checkedListBox.SetItemChecked(i, true);
                          }
                      });
            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.OkDialog);
            // Edit the _note_ on the root node.
            var editNoteDlg = ShowDialog<EditNoteDlg>(SkylineWindow.EditNote);
            Assert.IsNull(SkylineWindow.Document.PeptideGroups.First().Annotations.GetAnnotation(COL_PROTEIN_TEXT));
            RunUI(() => SetAnnotationValue(editNoteDlg, COL_PROTEIN_TEXT, "proteinTextValue"));
            OkDialog(editNoteDlg, ()=>editNoteDlg.DialogResult = DialogResult.OK);
            Assert.AreEqual("proteinTextValue",
                            SkylineWindow.Document.PeptideGroups.First().Annotations.GetAnnotation(COL_PROTEIN_TEXT));
            // Show the ResultsGrid
            var resultsGridForm = ShowDialog<ResultsGridForm>(() => SkylineWindow.ShowResultsGrid(true));
            var resultsGrid = resultsGridForm.ResultsGrid;
            RunUI(()=>
                      {
                          SkylineWindow.SequenceTree.Nodes[0].Expand();
                          SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Expand();
                          SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0].Expand();                          
                      });
            // Select the first Precursor in the SequenceTree
            var precursorTreeNode = (SrmTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0];
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = precursorTreeNode);
            var chromInfo = ((TransitionGroupDocNode)precursorTreeNode.Model).Results[0][0];
            Assert.IsNull(chromInfo.Annotations.GetAnnotation(COL_PRECURSOR_RESULTS_ITEMS));
            WaitForGraphPanesToUpdate();
            // Show the "precursorResultItems" annotation column
            var chooseColumnsDlg = ShowDialog<ColumnChooser>(resultsGridForm.ChooseColumns);
            RunUI(()=>chooseColumnsDlg.CheckedListBox.SetItemChecked(
                chooseColumnsDlg.CheckedListBox.Items.IndexOf(COL_PRECURSOR_RESULTS_ITEMS), true));
            OkDialog(chooseColumnsDlg, ()=>chooseColumnsDlg.DialogResult=DialogResult.OK);
            var colPrecursorResultItems =
                resultsGrid.Columns[
                    AnnotationDef.GetColumnName(COL_PRECURSOR_RESULTS_ITEMS)];
            // Set the annotation value on the first two rows in the ResultsGrid.
            // The annotation is a dropdown with values {blank, "a", "b", "c"}
            Assert.IsNotNull(colPrecursorResultItems);
            DataGridViewCell cell;
            RunUI(() =>
            {
                cell = resultsGrid.Rows[0].Cells[colPrecursorResultItems.Index];
                resultsGrid.CurrentCell = cell;
                resultsGrid.BeginEdit(true);
                ((ComboBox)resultsGrid.EditingControl).SelectedIndex = 2;
                resultsGrid.EndEdit();
            });
            cell = null;
            RunUI(() =>
            {
                cell = resultsGrid.Rows[1].Cells[colPrecursorResultItems.Index];
                resultsGrid.CurrentCell = cell;
                resultsGrid.BeginEdit(true);
                ((ComboBox)resultsGrid.EditingControl).SelectedIndex = 1;
                resultsGrid.EndEdit();
            });

            // Assert that the annotations have their new values.
            var precursorDocNode = ((TransitionGroupDocNode)precursorTreeNode.Model);
            Assert.AreEqual("b", precursorDocNode.Results[0][0]
                .Annotations.GetAnnotation(COL_PRECURSOR_RESULTS_ITEMS));
            Assert.AreEqual("a", precursorDocNode.Results[0][1]
                .Annotations.GetAnnotation(COL_PRECURSOR_RESULTS_ITEMS));

            // Test multiselect here.
            RunUI(() =>
            {
                // Annotations applying to transitions as well as precursors should be visible.
                DataGridViewColumn col = resultsGrid.Columns[AnnotationDef.GetColumnName("precursor transition text")];
                Assert.IsNotNull(col);
                cell = resultsGrid.Rows[0].Cells[col.Index];
                Assert.IsTrue(cell.Visible);
                // Select a transition node in addition to the precursor node already selected.
                SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes[0];
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
            });
            WaitForGraphPanesToUpdate();
            RunUI(() =>
            {
                const string annotationTestText = "Test";
                // Annotations applying to both precursors and transitions should still be 
                // visible. 
                Assert.IsTrue(cell.Visible);
                // Set value for that annotation.
                resultsGrid.CurrentCell = cell;
                resultsGrid.BeginEdit(true);
                resultsGrid.EditingControl.Text = annotationTestText;
                resultsGrid.EndEdit();
                // Annotations applying just to precursors should no longer be visible.
                cell = resultsGrid.Rows[0].Cells[colPrecursorResultItems.Index];
                Assert.IsFalse(cell.Visible);
                precursorDocNode = (TransitionGroupDocNode)
                    ((TransitionGroupTreeNode)SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0]).Model;
                // Check all annotations have the new value. 
                foreach (TransitionGroupChromInfo info in precursorDocNode.Results[0])
                {
                    Assert.IsTrue(info.Annotations.ListAnnotations().Contains(pair =>
                        Equals(pair.Value, annotationTestText)));
                }
            });
            // Multiselect transitions only.
            RunUI(() =>
                      {
                          SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                          SkylineWindow.SequenceTree.SelectedNode =
                              SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes[3];
                      });
            WaitForGraphPanesToUpdate();
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.KeysOverride = Keys.Shift;
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes[0];
            });
            WaitForGraphPanesToUpdate();
            RunUI(() =>
            {
                // Since only transitions are selected, transition _note column should be visible.
                cell = resultsGrid.Rows[0].Cells[resultsGrid.TransitionNoteColumn.Index];
                Assert.IsTrue(cell.Visible);
                resultsGrid.CurrentCell = cell;
                resultsGrid.BeginEdit(true);
                resultsGrid.EditingControl.Text = "Test2";
                resultsGrid.EndEdit();
                // Check all nodes have received the correct values.
                foreach(TransitionTreeNode nodeTree in SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes)
                {
                    foreach (TransitionChromInfo info in ((TransitionDocNode) nodeTree.Model).Results[0])
                    {
                      Assert.AreEqual("Test2", info.Annotations.Note);
                    }
                }
                // Test multiselect node without results - annotation columns should no longer be visible.
                TreeNodeMS precursorNoResults = (TreeNodeMS) SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[1];
                SkylineWindow.SequenceTree.SelectedPaths = new List<IdentityPath>
                {
                    SkylineWindow.SequenceTree.SelectedPath,
                    SkylineWindow.SequenceTree.GetNodePath(precursorNoResults)
                };
                Assert.IsFalse(resultsGrid.GetAvailableColumns().Contains(resultsGrid.PrecursorNoteColumn));
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
            });
        }

        private static void DefineAnnotation(EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef> dialog,
            String name, AnnotationDef.AnnotationTarget targets, AnnotationDef.AnnotationType type, IList<string> items)
        {
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(dialog.AddItem);
            RunUI(()=>
                      {
                          defineAnnotationDlg.AnnotationName = name;
                          defineAnnotationDlg.AnnotationType = type;
                          defineAnnotationDlg.AnnotationTargets = targets;
                          defineAnnotationDlg.Items = items ?? new string[0];
                      });
            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
        }

        private static void SetAnnotationValue(EditNoteDlg editNoteDlg, String annotationName, String value)
        {
            for (int i = 0; i < editNoteDlg.DataGridView.Rows.Count; i++)
            {
                var row = editNoteDlg.DataGridView.Rows[i];
                if (!annotationName.Equals(row.Cells[0].Value))
                {
                    continue;
                }
                row.Cells[1].Value = value;
                return;
            }
            throw new ArgumentException("Could not find annotation " + annotationName);
        }

        private static void WaitForGraphPanesToUpdate()
        {
            while (true)
            {
                bool graphsNeedUpdating = false;
                RunUI(() => graphsNeedUpdating = SkylineWindow.IsGraphUpdatePending);
                if (!graphsNeedUpdating)
                {
                    return;
                }
                Thread.Sleep(100);
            }
        }
    }
}
