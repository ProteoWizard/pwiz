/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class TransitionResultQuantitativeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestTransitionResultQuantitative()
        {
            TestFilesZip = @"TestFunctional\TransitionResultQuantitativeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("TransitionResultQuantitativeTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = WaitForOpenForm<DocumentGridForm>();
            WaitForCondition(() => documentGrid.IsComplete);
            RunDlg<ViewEditor>(documentGrid.DataboundGridControl.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ChooseColumnsTab.RemoveColumns(0, viewEditor.ChooseColumnsTab.ColumnCount);
                PropertyPath ppTransition = PropertyPath.Root
                    .Property(nameof(SkylineDocument.Proteins)).LookupAllItems()
                    .Property(nameof(Protein.Peptides)).LookupAllItems()
                    .Property(nameof(Peptide.Precursors)).LookupAllItems()
                    .Property(nameof(Precursor.Transitions)).LookupAllItems();
                viewEditor.ChooseColumnsTab.AddColumn(ppTransition);
                viewEditor.ChooseColumnsTab.AddColumn(ppTransition.Property(nameof(Transition.Quantitative)));
                PropertyPath ppTransitionResult = ppTransition.Property(nameof(Transition.Results)).DictionaryValues();
                viewEditor.ChooseColumnsTab.AddColumn(ppTransitionResult.Property(nameof(TransitionResult.TransitionResultIsMs1)));
                viewEditor.ChooseColumnsTab.AddColumn(ppTransitionResult.Property(nameof(TransitionResult.TransitionResultIsQuantitative)));
                viewEditor.ViewName = "TransitionResultQuantitativeReport";
                viewEditor.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colTransition = documentGrid.FindColumn(PropertyPath.Root);
                PropertyPath ppTransitionResult = PropertyPath.Root.Property(nameof(Transition.Results)).DictionaryValues();
                var colQuantitative =
                    documentGrid.FindColumn(
                        ppTransitionResult.Property(nameof(TransitionResult.TransitionResultIsQuantitative)));
                Assert.IsNotNull(colQuantitative);
                var colIsMs1 =
                    documentGrid.FindColumn(
                        ppTransitionResult.Property(nameof(TransitionResult.TransitionResultIsMs1)));
                Assert.IsNotNull(colIsMs1);
                for (int i = 0; i < documentGrid.RowCount; i++)
                {
                    var row = documentGrid.DataGridView.Rows[i];
                    var transition = (Transition) row.Cells[colTransition.Index].Value;
                    var isQuantitative = (bool) row.Cells[colQuantitative.Index].Value;
                    var isMs1 = (bool) row.Cells[colIsMs1.Index].Value;
                    Assert.AreEqual(transition.DocNode.IsQuantitative(SkylineWindow.Document.Settings), isQuantitative);
                    Assert.AreEqual(transition.DocNode.IsMs1, isMs1);
                    if (transition.DocNode.Losses != null)
                    {
                        Assert.IsFalse(isMs1);
                    }
                }
            });
        }
    }
}
