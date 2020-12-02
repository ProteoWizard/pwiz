using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ClusteredHeatMapTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestClusteredHeatMap()
        {
            // IsPauseForScreenShots = true;
            TestFilesZip = @"TestFunctional\ClusteredHeatMapTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("HeatMapTest.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            Assert.IsNotNull(documentGrid);
            RunUI(()=>documentGrid.ChooseView("PeptideResultValues"));
            WaitForCondition(() => documentGrid.IsComplete);
            var heatMap = ShowDialog<HierarchicalClusterGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            var heatMapResults = heatMap.Results;
            Assert.IsNotNull(heatMap);
            PauseForScreenShot("Normal heat map");
            OkDialog(heatMap, heatMap.Close);

            RunUI(()=>documentGrid.BindingListSource.ClusteringSpec = ClusteringSpec.DEFAULT);
            WaitForCondition(() => documentGrid.IsComplete);
            var expectedRowLabels = documentGrid.BindingListSource.OfType<RowItem>().Select(row =>
                documentGrid.BindingListSource.ItemProperties[0].GetValue(row)?.ToString() ?? string.Empty).ToList();
            expectedRowLabels.Reverse();
            CollectionAssert.AreEqual(expectedRowLabels, heatMapResults.DataSet.RowLabels.ToList());
            var expectedColumnLabels = documentGrid.BindingListSource.ItemProperties.OfType<ColumnPropertyDescriptor>()
                .Where(c => c.PropertyPath.Name == "NormalizedArea").Select(col =>
                    col.PivotedColumnId.PivotKeyCaption.GetCaption(SkylineDataSchema.GetLocalizedSchemaLocalizer())).ToList();
            var actualColumnLabels = heatMapResults.DataSet.DataFrameGroups.First().First().ColumnHeaders;
            CollectionAssert.AreEqual(expectedColumnLabels, actualColumnLabels.ToList());

            RunUI(()=>documentGrid.ChooseView("ThreeColumnGroups"));
            WaitForCondition(() => documentGrid.IsComplete);
            heatMap = ShowDialog<HierarchicalClusterGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            PauseForScreenShot("Heat map with three column groups");
            Assert.AreEqual(3, heatMap.Results.ColumnGroupDendrograms.Count);
            OkDialog(heatMap, heatMap.Close);
        }
    }
}
