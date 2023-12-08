/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RemoveAnnotationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRemoveAnnotation()
        {
            TestFilesZip = @"TestFunctional\RemoveAnnotationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RemoveAnnotationTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView("Precursor Results Annotations"));
            WaitForCondition(() => documentGrid.IsComplete);
            PropertyPath ppPeptideResult =
                PropertyPath.Root.Property(nameof(Precursor.Results)).DictionaryValues();
            PropertyPath ppCarryoverProblem = ppPeptideResult.Property(AnnotationDef.ANNOTATION_PREFIX + "carryover problem");
            PropertyPath ppRtSchedulingProblem =
                ppPeptideResult.Property(AnnotationDef.ANNOTATION_PREFIX + "RT scheduling problem");
            RunUI(() =>
            {
                var colCarryoverProblem = documentGrid.FindColumn(ppCarryoverProblem);
                Assert.AreEqual(typeof(DataGridViewCheckBoxColumn), colCarryoverProblem.GetType());
                Assert.IsFalse(colCarryoverProblem.ReadOnly);
                CollectionAssert.DoesNotContain(GetColumnValues(colCarryoverProblem), null);
                var colRtSchedulingProblem = documentGrid.FindColumn(ppRtSchedulingProblem);
                Assert.AreEqual(typeof(AnnotationValueListDataGridViewColumn), colRtSchedulingProblem.GetType());
                Assert.IsFalse(colRtSchedulingProblem.ReadOnly);
                var nulls = new object[documentGrid.RowCount];
                CollectionAssert.AreNotEqual(nulls, GetColumnValues(colRtSchedulingProblem));
            });
            RunDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.annotations);
                SetItemChecked(documentSettingsDlg.AnnotationsCheckedListBox, "carryover problem", false);
                SetItemChecked(documentSettingsDlg.AnnotationsCheckedListBox, "RT scheduling problem", false);
                documentSettingsDlg.OkDialog();
            });
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var colCarryoverProblem = documentGrid.FindColumn(ppCarryoverProblem);
                Assert.AreEqual(typeof(DataGridViewTextBoxColumn), colCarryoverProblem.GetType());
                Assert.IsTrue(colCarryoverProblem.ReadOnly);
                var nulls = new object[documentGrid.RowCount];
                CollectionAssert.AreEqual(nulls, GetColumnValues(colCarryoverProblem));
                var colRtSchedulingProblem = documentGrid.FindColumn(ppRtSchedulingProblem);
                Assert.AreEqual(typeof(DataGridViewTextBoxColumn), colRtSchedulingProblem.GetType());
                Assert.IsTrue(colRtSchedulingProblem.ReadOnly);
                CollectionAssert.AreEqual(nulls, GetColumnValues(colRtSchedulingProblem));
            });
        }

        private void SetItemChecked(CheckedListBox checkedListBox, string key, bool value)
        {
            int index = checkedListBox.Items.IndexOf(key);
            Assert.IsTrue(index >= 0, "Could not find {0}", key);
            checkedListBox.SetItemChecked(index, value);
        }

        private List<object> GetColumnValues(DataGridViewColumn column)
        {
            var grid = column.DataGridView;
            return Enumerable.Range(0, grid.RowCount).Select(row => grid.Rows[row].Cells[column.Index].Value).ToList();
        }
    }
}
