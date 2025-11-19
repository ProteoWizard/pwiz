/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /*
     * ReplicatePivotColumns provides utility methods to group ItemProperties
     * into replicate groups. This can provide a distinction between replicate
     * variable, replicate constant and non-replicate item properties. These
     * tests validate that item properties are qualified as expected when
     * parsing a set of properties. 
     */
    [TestClass]
    public class ReplicatePivotColumnsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReplicatePivotColumns()
        {
            TestFilesZip = @"TestFunctional\ReplicatePivotColumnsTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestMultiInjectReplicates();
            TestFoldChangeGrid();
        }

        private void TestMultiInjectReplicates()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Worm.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() => documentGrid.ChooseView("PeptideNormalizedAreas"));
            WaitForConditionUI(() => documentGrid.IsComplete);
            RunUI(() =>
            {
                var itemProperties = documentGrid.BindingListSource.ItemProperties;
                var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);
                Assert.IsNotNull(replicatePivotColumns);
                var propertyPathFileName = PropertyPath.Parse("Results!*.Value.ResultFile.FileName");
                var fileNameProperties = itemProperties.OfType<ColumnPropertyDescriptor>()
                    .Where(pd => propertyPathFileName.Equals(pd.DisplayColumn.PropertyPath)).ToList();
                Assert.AreEqual(2, fileNameProperties.Count);
                // The first file name properties is not constant because the first replicate has multiple injections
                Assert.IsFalse(replicatePivotColumns.IsConstantColumn(fileNameProperties[0]));
                Assert.IsTrue(replicatePivotColumns.IsConstantColumn(fileNameProperties[1]));
            });
        }

        private void TestFoldChangeGrid()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("GroupComparisonTest.sky"));
                SkylineWindow.ShowGroupComparisonWindow("GroupComparison");
            });
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            RunUI(() => foldChangeGrid.DataboundGridControl.ChooseView("FoldChangeDetailReport"));
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            RunUI(() =>
            {
                var itemProperties = foldChangeGrid.DataboundGridControl.BindingListSource.ItemProperties;
                var replicatePivotColumns = ReplicatePivotColumns.FromItemProperties(itemProperties);
                var propertyPathReplicateReplicateGroup =
                    PropertyPath.Parse("ReplicateAbundances!*.Value.ReplicateGroup");
                var replicateGroupProperties = itemProperties.OfType<ColumnPropertyDescriptor>().Where(pd =>
                    propertyPathReplicateReplicateGroup.Equals(pd.DisplayColumn.PropertyPath)).ToList();
                Assert.AreNotEqual(0, replicateGroupProperties.Count);
                foreach (var pp in replicateGroupProperties)
                {
                    Assert.IsTrue(replicatePivotColumns.IsConstantColumn(pp));
                }
            });
        }
    }
}
