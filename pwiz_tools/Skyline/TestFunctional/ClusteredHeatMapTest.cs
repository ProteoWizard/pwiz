using log4net.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
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

            // Use the Results Grid to show a weird heat map where two distinct sets of columns have been pivoted
            RunUI(()=>
            {
                SkylineWindow.ShowResultsGrid(true);
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 0);
            });
            var resultsGrid = FindOpenForm<LiveResultsGrid>();
            WaitForCondition(() => resultsGrid.IsComplete);
            // Add columns which are pivoted on two different axes.
            // There will be the columns "Area" and "Background" which both come from "Transition.Results!*.Value"
            // And there will be "ProductMz" which comes from Transition.Precursor.Transitions!*
            RunDlg<ViewEditor>(resultsGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = "ViewWithMultiplePivotedColumnSets";
                var chooseColumnsTab = viewEditor.ChooseColumnsTab;
                chooseColumnsTab.AddColumn(PropertyPath.Parse("PrecursorResult.PeptideResult.ResultFile.Replicate"));
                // Add these two columns which are pivoted along the Transition.Results!*.Value axis
                chooseColumnsTab.AddColumn(PropertyPath.Parse("Transition.Results!*.Value.Area"));
                chooseColumnsTab.AddColumn(PropertyPath.Parse("Transition.Results!*.Value.Background"));
                // Add this one column which is pivoted along the Transition.Precursor.Transitions!* axis
                chooseColumnsTab.AddColumn(PropertyPath.Parse("Transition.Precursor.Transitions!*.ProductMz"));
                viewEditor.OkDialog();
            });
            WaitForCondition(() => resultsGrid.IsComplete);
            heatMap = ShowDialog<HierarchicalClusterGraph>(() => resultsGrid.DataboundGridControl.ShowHeatMap());
            Assert.IsNotNull(heatMap);
            PauseForScreenShot("Heat map with two dendrograms above the column");
            OkDialog(heatMap, heatMap.Close);
        }
    }
}
