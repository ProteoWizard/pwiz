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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;

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
            DefineAnnotation(editListDlg, "proteinText", AnnotationDef.AnnotationTarget.protein, AnnotationDef.AnnotationType.text, null);
            DefineAnnotation(editListDlg, "peptideItems", AnnotationDef.AnnotationTarget.peptide,
                             AnnotationDef.AnnotationType.value_list, new[] {"one","two","three"});
            DefineAnnotation(editListDlg, "precursorTrueFalse", AnnotationDef.AnnotationTarget.precursor,
                             AnnotationDef.AnnotationType.true_false, null);
            DefineAnnotation(editListDlg, "transitionText", AnnotationDef.AnnotationTarget.transition, 
                AnnotationDef.AnnotationType.text, null);
            DefineAnnotation(editListDlg, "precursorResultItems", AnnotationDef.AnnotationTarget.precursor_result,
                             AnnotationDef.AnnotationType.value_list, new[] {"a", "b", "c"});
            DefineAnnotation(editListDlg, "transitionTrueFalse", AnnotationDef.AnnotationTarget.transition_result,
                             AnnotationDef.AnnotationType.true_false, null);
            DefineAnnotation(editListDlg, "all",
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein |
                             AnnotationDef.AnnotationTarget.protein, 
                             AnnotationDef.AnnotationType.text, null);
            OkDialog(editListDlg, ()=>editListDlg.DialogResult = DialogResult.OK);
            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.Close);
            // Open the .sky file
            RunUI(() =>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini.sky")));
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
            Assert.IsNull(SkylineWindow.Document.PeptideGroups.First().Annotations.GetAnnotation("proteinText"));
            RunUI(() => SetAnnotationValue(editNoteDlg, "proteinText", "proteinTextValue"));
            OkDialog(editNoteDlg, ()=>editNoteDlg.DialogResult = DialogResult.OK);
            Assert.AreEqual("proteinTextValue",
                            SkylineWindow.Document.PeptideGroups.First().Annotations.GetAnnotation("proteinText"));
            // Show the ResultsGrid
            var resultsGridForm = ShowDialog<ResultsGridForm>(() => SkylineWindow.ShowResultsGrid(true));
            var resultsGrid = resultsGridForm.ResultsGrid;
            RunUI(()=>
                      {
                          SkylineWindow.SequenceTree.Nodes[0].Expand();
                          SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Expand();
                      });
            // Select the first Precursor in the SequenceTree
            var precursorTreeNode = (SrmTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Nodes[0];
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = precursorTreeNode);
            var chromInfo = ((TransitionGroupDocNode)precursorTreeNode.Model).Results[0][0];
            Assert.IsNull(chromInfo.Annotations.GetAnnotation("precursorResultItems"));
            WaitForGraphPanesToUpdate();
            // Show the "precursorResultItems" annotation column
            var chooseColumnsDlg = ShowDialog<ColumnChooser>(resultsGridForm.ChooseColumns);
            RunUI(()=>chooseColumnsDlg.CheckedListBox.SetItemChecked(
                chooseColumnsDlg.CheckedListBox.Items.IndexOf("precursorResultItems"), true));
            OkDialog(chooseColumnsDlg, ()=>chooseColumnsDlg.DialogResult=DialogResult.OK);
            var colPrecursorResultItems =
                resultsGrid.Columns[
                    AnnotationDef.AnnotationPrefix + "precursorResultItems"];
            // Set the annotation value on the first two rows in the ResultsGrid.
            // The annotation is a dropdown with values {blank, "a", "b", "c"}
            Assert.IsNotNull(colPrecursorResultItems);
            Debug.Assert(colPrecursorResultItems != null);  // For ReSharper
            RunUI(()=>
                      {
                          var cell = resultsGrid.Rows[0].Cells[colPrecursorResultItems.Index];
                          resultsGrid.CurrentCell = cell;
                          resultsGrid.BeginEdit(true);
                          ((ComboBox)resultsGrid.EditingControl).SelectedIndex = 2;
                          resultsGrid.EndEdit();
                          cell = resultsGrid.Rows[1].Cells[colPrecursorResultItems.Index];
                          resultsGrid.CurrentCell = cell;
                          resultsGrid.BeginEdit(true);
                          ((ComboBox)resultsGrid.EditingControl).SelectedIndex = 1;
                          resultsGrid.EndEdit();
                      });
            // Assert that the annotations have their new values.
            var precursorDocNode = ((TransitionGroupDocNode) precursorTreeNode.Model);
            Assert.AreEqual("b", precursorDocNode.Results[0][0]
                .Annotations.GetAnnotation("precursorResultItems"));
            Assert.AreEqual("a", precursorDocNode.Results[0][1]
                .Annotations.GetAnnotation("precursorResultItems"));
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
