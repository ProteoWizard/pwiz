/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Clustering;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ClusteredHeatMapTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestClusteredHeatMap()
        {
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
            TestGroupComparisonClustering();
            var documentGrid = FindOpenForm<DocumentGridForm>();
            Assert.IsNotNull(documentGrid);
            RunUI(()=>documentGrid.ChooseView("PeptideResultValues"));
            WaitForCondition(() => documentGrid.IsComplete);
            RunUI(()=> documentGrid.DataboundGridControl.ShowHeatMap());
            var heatMap = WaitForOpenForm<HeatMapGraph>();
            WaitForConditionUI(() => null != heatMap.GraphResults);
            var heatMapResults = heatMap.GraphResults;
            Assert.IsNotNull(heatMap);
            OkDialog(heatMap, heatMap.Close);

            RunUI(()=>documentGrid.BindingListSource.ClusteringSpec = ClusteringSpec.DEFAULT);
            WaitForCondition(() => documentGrid.IsComplete);
            List<string> expectedRowLabels = documentGrid.BindingListSource.OfType<RowItem>().Select(row =>
                documentGrid.BindingListSource.ItemProperties[0].GetValue(row)?.ToString() ?? string.Empty).ToList();
            List<string> actualRowLabels = heatMapResults.RowHeaders.Select(header => header.Caption).ToList();
            CollectionAssert.AreEqual(expectedRowLabels, actualRowLabels);
            List<string> expectedColumnLabels = documentGrid.BindingListSource.ItemProperties.OfType<ColumnPropertyDescriptor>()
                .Where(c => c.PropertyPath.Name == "NormalizedArea").Select(col =>
                    col.PivotedColumnId.PivotKeyCaption.GetCaption(SkylineDataSchema.GetLocalizedSchemaLocalizer())).ToList();
            List<string> actualColumnLabels = heatMapResults.ColumnGroups.First().Headers.Select(header=>header.Caption).ToList();
            CollectionAssert.AreEqual(expectedColumnLabels, actualColumnLabels);

            RunUI(()=>documentGrid.ChooseView("ThreeColumnGroups"));
            WaitForCondition(() => documentGrid.IsComplete);
            heatMap = ShowDialog<HeatMapGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            WaitForConditionUI(() => null != heatMap.GraphResults);
            Assert.AreEqual(6, heatMap.GraphResults.ColumnGroups.Count);
            OkDialog(heatMap, heatMap.Close);
        }

        private const string PER_PROTEIN_NAME = "GroupComparisonPerProtein";
        public void TestGroupComparisonClustering()
        {
            RunDlg<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison, dlg =>
            {
                dlg.GroupComparisonDef = GroupComparisonDef.EMPTY
                    .ChangeControlAnnotation("Condition")
                    .ChangeControlValue("Healthy")
                    .ChangeIdentityAnnotation("BioReplicate")
                    .ChangePerProtein(true)
                    .ChangeNormalizationMethod(NormalizationMethod.GLOBAL_STANDARDS);
                dlg.TextBoxName.Text = PER_PROTEIN_NAME;
                dlg.OkDialog();
            });
            var grid = ShowDialog<FoldChangeGrid>(() => SkylineWindow.ShowGroupComparisonWindow(PER_PROTEIN_NAME));
            WaitForCondition(() => grid.DataboundGridControl.IsComplete);
            RunUI(()=>grid.DataboundGridControl.ChooseView("Clustered"));
            WaitForCondition(() => grid.DataboundGridControl.IsComplete && 0 != grid.DataboundGridControl.RowCount);
            RunUI(()=> grid.DataboundGridControl.ShowHeatMap());
            var heatMap = FindOpenForm<HeatMapGraph>();
            WaitForConditionUI(() => heatMap.IsComplete);
            string expectedHeatMapTitle = DataboundGraph.MakeTitle(Resources.HeatMapGraph_RefreshData_Heat_Map,
                new DataGridId(DataGridType.GROUP_COMPARISON, PER_PROTEIN_NAME),
                ViewGroup.BUILT_IN.Id.ViewName("Clustered"));
            Assert.AreEqual(expectedHeatMapTitle, heatMap.TabText);
            var expectedPointCount = heatMap.GraphControl.GraphPane.CurveList.OfType<ClusteredHeatMapItem>().First().Points.Count;
            var filePath = SkylineWindow.DocumentFilePath;
            RunUI(()=>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.NewDocument();
                heatMap.Close();
            });
            Assert.IsNull(FindOpenForm<HeatMapGraph>());
            RunUI(() => SkylineWindow.OpenFile(filePath));
            heatMap = FindOpenForm<HeatMapGraph>();
            Assert.IsNotNull(heatMap);
            WaitForConditionUI(() => heatMap.IsComplete && heatMap.TabText == expectedHeatMapTitle);
            var pointCount = heatMap.GraphControl.GraphPane.CurveList.OfType<ClusteredHeatMapItem>().First().Points
                .Count;
            Assert.AreEqual(expectedPointCount, pointCount);
            grid = FindOpenForm<FoldChangeGrid>();
            Assert.IsNotNull(grid);
            var pcaPlot = ShowDialog<PcaPlot>(() => grid.DataboundGridControl.ShowPcaPlot());
            Assert.IsNotNull(pcaPlot);
            var pcaChoice = new PcaPlot.PcaChoice(2, 3, 1);
            RunUI(()=>pcaPlot.PcaChoiceValue = pcaChoice);
            WaitForConditionUI(() => pcaPlot.IsComplete);
            var expectedPcaPlotTitle = DataboundGraph.MakeTitle(Resources.PcaPlot_RefreshData_PCA_Plot,
                new DataGridId(DataGridType.GROUP_COMPARISON, PER_PROTEIN_NAME),
                ViewGroup.BUILT_IN.Id.ViewName("Clustered"));

            int curveCount = pcaPlot.GraphControl.GraphPane.CurveList.Count;
            Assert.AreNotEqual(0, curveCount);
            Assert.AreEqual(expectedPcaPlotTitle, pcaPlot.TabText);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.NewDocument();
                heatMap.Close();
                pcaPlot.Close();
            });
            Assert.IsNull(FindOpenForm<PcaPlot>());
            RunUI(() => SkylineWindow.OpenFile(filePath));
            pcaPlot = FindOpenForm<PcaPlot>();
            WaitForConditionUI(() => pcaPlot.IsComplete && pcaPlot.TabText == expectedPcaPlotTitle);
            Assert.AreEqual(curveCount, pcaPlot.GraphControl.GraphPane.CurveList.Count);
        }
    }
}
