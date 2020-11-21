using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Databinding;
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
            Assert.IsNotNull(heatMap);
            PauseForScreenShot("Normal heat map");
            OkDialog(heatMap, heatMap.Close);

            RunUI(()=>documentGrid.ChooseView("ThreeColumnGroups"));
            WaitForCondition(() => documentGrid.IsComplete);
            heatMap = ShowDialog<HierarchicalClusterGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            PauseForScreenShot("Heat map with three column groups");
            Assert.AreEqual(3, heatMap.Results.ColumnGroupDendrograms.Count);
            OkDialog(heatMap, heatMap.Close);
        }
    }
}
