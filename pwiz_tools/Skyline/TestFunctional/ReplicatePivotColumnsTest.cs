using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
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
