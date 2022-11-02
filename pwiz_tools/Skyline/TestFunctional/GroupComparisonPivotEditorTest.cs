/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    // ReSharper disable AccessToModifiedClosure
    [TestClass]
    public class GroupComparisonPivotEditorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestGroupComparisonPivotEditor()
        {
            TestFilesZip = @"TestFunctional\GroupComparisonPivotEditorTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("GroupComparisonPivotEditorTest.sky")));
            var foldChangeGrid = ShowDialog<FoldChangeGrid>(()=>SkylineWindow.ShowGroupComparisonWindow("ComparedToS1"));
            WaitForConditionUI(() => null != foldChangeGrid.DataboundGridControl.NavBar.ViewContext);
            RunUI(() => foldChangeGrid.DataboundGridControl.ChooseView("WithFoldChangeValues"));
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);
            var pivotEditor =
                ShowDialog<PivotEditor>(() => foldChangeGrid.DataboundGridControl.NavBar.ShowPivotDialog(false));
            RunUI(() =>
            {
                SelectItemsByName(pivotEditor.AvailableColumnList, "Group");
                pivotEditor.AddColumnHeader();
                SelectItemsByName(pivotEditor.AvailableColumnList, "Protein");
                pivotEditor.AddRowHeader();
                SelectItemsByName(pivotEditor.AvailableColumnList, "Log 2 Fold Change", "Fold Change");
                pivotEditor.SelectAggregateOperation(AggregateOperation.Mean);
                pivotEditor.AddValue();
                pivotEditor.SelectAggregateOperation(AggregateOperation.StdDev);
                pivotEditor.AddValue();
                pivotEditor.SelectAggregateOperation(AggregateOperation.Cv);
                pivotEditor.AddValue();
            });
            OkDialog(pivotEditor, pivotEditor.OkDialog);
            WaitForConditionUI(() => foldChangeGrid.DataboundGridControl.IsComplete);

            string originalFormat = foldChangeGrid.DataboundGridControl.DataGridView.Columns[1].DefaultCellStyle.Format;
            const string newFormat = "0.0000E+0";
            Assert.AreNotEqual(newFormat, originalFormat);

            int columnCount = foldChangeGrid.DataboundGridControl.ColumnCount;
            for (int i = 1; i < columnCount; i++)
            {
                var chooseFormatDlg = ShowDialog<ChooseFormatDlg>(() =>
                    foldChangeGrid.DataboundGridControl.ShowFormatDialog(foldChangeGrid.DataboundGridControl
                        .DataGridView.Columns[i]));
                RunUI(() => chooseFormatDlg.FormatText = newFormat);
                OkDialog(chooseFormatDlg, ()=>chooseFormatDlg.DialogResult = DialogResult.OK);
                RunUI(()=>foldChangeGrid.DataboundGridControl.DataGridView.Columns[i].Width += i);
            }
            RunUI(() =>
            {
                foldChangeGrid.DataboundGridControl.SetSortDirection(foldChangeGrid.DataboundGridControl.BindingListSource.ItemProperties[0], ListSortDirection.Ascending);
            });
            WaitForCondition(() => foldChangeGrid.DataboundGridControl.IsComplete);
            RunUI(() =>
            {
                foldChangeGrid.DataboundGridControl.SetSortDirection(foldChangeGrid.DataboundGridControl.BindingListSource.ItemProperties[0], ListSortDirection.Descending);
            });
            WaitForCondition(() => foldChangeGrid.DataboundGridControl.IsComplete);
            for (int i = 1; i < columnCount; i++)
            {
                Assert.AreEqual(newFormat,
                    foldChangeGrid.DataboundGridControl.DataGridView.Columns[i].DefaultCellStyle.Format);
            }

            var nameLayoutForm = ShowDialog<NameLayoutForm>(foldChangeGrid.DataboundGridControl.NavBar.RememberCurrentLayout);
            RunUI(()=>nameLayoutForm.LayoutName = "TestLayout");
            OkDialog(nameLayoutForm, nameLayoutForm.OkDialog);
            RunUI(()=>SkylineWindow.SaveDocument());
        }

        private void SelectItemsByName(ListView listView, params string[] names)
        {
            listView.SelectedIndices.Clear();
            for (int i = 0; i < listView.Items.Count; i++)
            {
                if (names.Contains(listView.Items[i].Text))
                {
                    listView.SelectedIndices.Add(i);
                }
            }
        }
    }
}
