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
            var heatMap = ShowDialog<HierarchicalClusterGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            var heatMapResults = heatMap.GraphResults;
            Assert.IsNotNull(heatMap);
            PauseForScreenShot("Normal heat map");
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
            heatMap = ShowDialog<HierarchicalClusterGraph>(()=>documentGrid.DataboundGridControl.ShowHeatMap());
            PauseForScreenShot("Heat map with three column groups");
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
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow(PER_PROTEIN_NAME));
            FoldChangeGrid grid = FindOpenForm<FoldChangeGrid>();
            WaitForCondition(() => grid.DataboundGridControl.IsComplete);
            RunUI(()=>grid.DataboundGridControl.ChooseView("Clustered"));
            WaitForCondition(() => grid.DataboundGridControl.IsComplete && 0 != grid.DataboundGridControl.RowCount);
            var heatMap = ShowDialog<HierarchicalClusterGraph>(()=>grid.DataboundGridControl.ShowHeatMap());
            var pcaPlot = ShowDialog<PcaPlot>(() => grid.DataboundGridControl.ShowPcaPlot());
            OkDialog(heatMap, heatMap.Close);
            OkDialog(pcaPlot, pcaPlot.Close);
        }
    }
}
