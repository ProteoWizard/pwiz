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
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;
using ZedGraph;

// ReSharper disable LocalizableElement

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class GroupComparisonTest : AbstractFunctionalTestEx
    {
        private bool _asSmallMolecules;

        [TestMethod]
        public void TestGroupComparison()
        {
            TestFilesZip = @"TestFunctional\GroupComparisonTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestGroupComparisonAsSmallMolecules()
        {
            if (SkipSmallMoleculeTestVersions())
            {
                return;
            }

            _asSmallMolecules = true;
            TestFilesZip = @"TestFunctional\GroupComparisonTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath(_asSmallMolecules ? "msstatstest.converted_to_small_molecules.sky" : "msstatstest.sky"));
            });
            WaitForDocumentLoaded();
            DefineOneTwoGroupComparison();
            CopyOneTwoGroupComparison();
        }

        /// <summary>
        /// Define a group comparison comparing DilutionNumber 1 to 2
        /// </summary>
        private void DefineOneTwoGroupComparison()
        {
            var editGroupComparisonDlg = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);
            RunUI(() =>
            {
                Assert.IsTrue(string.IsNullOrEmpty(editGroupComparisonDlg.TextBoxName.Text));
                editGroupComparisonDlg.TextBoxName.Text = "One-Two";
                editGroupComparisonDlg.ComboControlAnnotation.SelectedItem = "DilutionNumber";
            });
            WaitForConditionUI(() => editGroupComparisonDlg.ComboControlValue.Items.Count > 0);
            RunUI(() =>
            {
                editGroupComparisonDlg.ComboControlValue.SelectedItem = "1";
                editGroupComparisonDlg.ComboCaseValue.SelectedItem = "2";
                editGroupComparisonDlg.RadioScopePerProtein.Checked = true;
            });
            var foldChangeGrid = ShowDialog<FoldChangeGrid>(editGroupComparisonDlg.ShowPreview);
            TryWaitForConditionUI(() => foldChangeGrid.DataboundGridControl.RowCount == 3);
            RunUI(() =>
            {
                Assert.AreEqual(3, foldChangeGrid.DataboundGridControl.RowCount);
                editGroupComparisonDlg.RadioScopePerPeptide.Checked = true;
            });
            TryWaitForCondition(() => foldChangeGrid.DataboundGridControl.RowCount == 4);
            RunUI(() => Assert.AreEqual(4, foldChangeGrid.DataboundGridControl.RowCount));
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
            WaitForClosedForm<FoldChangeGrid>();
        }

        /// <summary>
        /// Copy the OneTwo Group Comparison to a new group comparison which compares groups 1 and 3.
        /// </summary>
        private void CopyOneTwoGroupComparison()
        {
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<GroupComparisonDef>, GroupComparisonDef>>(
                documentSettingsDlg.EditGroupComparisonList);
            RunUI(()=>editListDlg.SelectItem("One-Two"));
            var editGroupComparisonDlg = ShowDialog<EditGroupComparisonDlg>(editListDlg.CopyItem);
            RunUI(() =>
            {
                editGroupComparisonDlg.TextBoxName.Text = "One-Three";
                editGroupComparisonDlg.ComboCaseValue.SelectedItem = "3";
            });
            // Show the preview window
            var foldChangeGrid = ShowDialog<FoldChangeGrid>(editGroupComparisonDlg.ShowPreview);
            TryWaitForCondition(() => foldChangeGrid.DataboundGridControl.RowCount == 4);
            RunUI(() => Assert.AreEqual(4, foldChangeGrid.DataboundGridControl.RowCount));
            // Show the graph window
            var foldChangeGraph = ShowDialog<FoldChangeBarGraph>(foldChangeGrid.ShowGraph);
            WaitForConditionUI(() => foldChangeGraph.ZedGraphControl.GraphPane.CurveList.Any());
            RunUI(() =>
            {
                Assert.AreEqual(foldChangeGrid.DataboundGridControl.RowCount, foldChangeGraph.ZedGraphControl.GraphPane.CurveList.First().Points.Count);
            });
            // Assert that sorting in the grid window affects the order of points in the graph window.
            var foldChangeGridControl = foldChangeGrid.DataboundGridControl;
            var foldChangeResultColumn = foldChangeGridControl.FindColumn(PropertyPath.Root.Property("FoldChangeResult"));
            RunUI(() =>
            {
                foldChangeGridControl.SetSortDirection(foldChangeGridControl.GetPropertyDescriptor(foldChangeResultColumn), ListSortDirection.Ascending);
            });
            WaitForConditionUI(() => foldChangeGridControl.IsComplete && !foldChangeGraph.IsUpdatePending);
            var values = GetYValues(foldChangeGraph.ZedGraphControl.GraphPane.CurveList.First().Points);
            var sortedValues = (double[]) values.Clone();
            Array.Sort(sortedValues);
            CollectionAssert.AreEqual(sortedValues, values);
            RunUI(() =>
            {
                foldChangeGridControl.SetSortDirection(foldChangeGridControl.GetPropertyDescriptor(foldChangeResultColumn), ListSortDirection.Descending);
            });
            WaitForConditionUI(() => foldChangeGridControl.IsComplete && !foldChangeGraph.IsUpdatePending);
            values = GetYValues(foldChangeGraph.ZedGraphControl.GraphPane.CurveList.First().Points);
            CollectionAssert.AreNotEqual(sortedValues, values);
            Array.Reverse(sortedValues);
            CollectionAssert.AreEqual(sortedValues, values);
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
            OkDialog(editListDlg, editListDlg.OkDialog);
            RunUI(()=>
            {
                var checkedListBox = documentSettingsDlg.GroupComparisonsCheckedListBox;
                checkedListBox.SetItemCheckState(checkedListBox.Items.IndexOf("One-Three"), CheckState.Checked);
            });
            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);
        }

        private double[] GetYValues(IPointList pointList)
        {
            return Enumerable.Range(0, pointList.Count).Select(i => pointList[i].Y).ToArray();
        }
    }
}
