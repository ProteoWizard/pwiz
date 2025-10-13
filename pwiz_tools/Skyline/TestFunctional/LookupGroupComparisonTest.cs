/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class LookupGroupComparisonTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLookupGroupComparison()
        {
            TestFilesZip = @"TestFunctional\LookupGroupComparisonTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string groupComparisonName = "Peptides by Condition";
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RatsWithSamples.sky"));
            });

            RunLongDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, editGroupComparisonDlg =>
            {
                RunUI(()=>
                {
                    editGroupComparisonDlg.TextBoxName.Text = groupComparisonName;
                    SelectComboItem(editGroupComparisonDlg.ComboControlAnnotation, " Condition");
                    SelectComboItem(editGroupComparisonDlg.ComboIdentityAnnotation, " Name");
                });
                WaitForConditionUI(() => editGroupComparisonDlg.ComboControlValue.Items.Count > 1);
                RunUI(() =>
                {
                    SelectComboItem(editGroupComparisonDlg.ComboControlValue, "Healthy");
                });
            }, editGroupComparisonDlg=>editGroupComparisonDlg.OkDialog());
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow(groupComparisonName));
            var groupComparisonGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => groupComparisonGrid.IsComplete);
            RunUI(() =>
            {
                Assert.AreEqual(125, groupComparisonGrid.DataboundGridControl.RowCount);
            });
        }
    }
}
