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

using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataAnalysis.Clustering;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Clustering;
using pwiz.Skyline.Controls.Lists;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ListClusteringTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestListClustering()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string listName = "MyList";
            var listDesigner = ShowDialog<ListDesigner>(SkylineWindow.AddListDefinition);
            RunUI(()=>listDesigner.ListName = listName);
            SetCellValue(listDesigner.ListPropertiesGrid, 0, 0, "Name");
            SetCellValue(listDesigner.ListPropertiesGrid, 1, 0, "X");
            SetCellValue(listDesigner.ListPropertiesGrid, 1, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.number));
            SetCellValue(listDesigner.ListPropertiesGrid, 2, 0, "Y");
            SetCellValue(listDesigner.ListPropertiesGrid, 2, 1, ListPropertyType.GetAnnotationTypeName(AnnotationDef.AnnotationType.number));
            RunUI(()=>listDesigner.ListPropertiesGrid.CurrentCell = listDesigner.ListPropertiesGrid.Rows[0].Cells[0]);
            OkDialog(listDesigner, listDesigner.OkDialog);
            RunUI(()=>SkylineWindow.ShowList(listName));
            var listGrid = FindOpenForm<ListGridForm>();
            WaitForCondition(() => listGrid.IsComplete);
            // Add some values to the "Name" and "X" columns
            SetClipboardText(TextUtil.LineSeparate("A\t-4", "B\t2", "C\t0", "D\t3", "E\t-3"));
            RunUI(()=>
            {
                listGrid.DataGridView.CurrentCell = listGrid.DataGridView.Rows[0].Cells[0];
                listGrid.DataGridView.SendPaste();
                listGrid.DataGridView.CurrentCell = listGrid.DataGridView.Rows[0].Cells[0];
            });
            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("ABCDE", GetNameValues(listGrid.DataGridView));

            // Press the "Cluster" button and see that the order changes
            RunUI(()=>listGrid.DataboundGridControl.NavBar.ClusterSplitButton.PerformButtonClick());
            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("AECBD", GetNameValues(listGrid.DataGridView));

            // Press the "Cluster" button again and the order goes back to the original
            RunUI(() => listGrid.DataboundGridControl.NavBar.ClusterSplitButton.PerformButtonClick());
            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("ABCDE", GetNameValues(listGrid.DataGridView));

            // Add some numbers to the "Y" column
            SetClipboardText(TextUtil.LineSeparate("1","1","0","3","3"));
            RunUI(() =>
            {
                listGrid.DataGridView.CurrentCell = listGrid.DataGridView.Rows[0].Cells[2];
                listGrid.DataGridView.SendPaste();
                listGrid.DataGridView.CurrentCell = listGrid.DataGridView.Rows[0].Cells[0];
            });
            // Press the Cluster button
            RunUI(() => listGrid.DataboundGridControl.NavBar.ClusterSplitButton.PerformButtonClick());
            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("BDCAE", GetNameValues(listGrid.DataGridView));

            // set to city block distance metric, which will change the clustering of the items
            var clusteringEditor = ShowDialog<ClusteringEditor>(listGrid.NavBar.ShowClusteringEditor);
            RunUI(()=>
            {
                Assert.AreEqual(ClusterMetricType.EUCLIDEAN, clusteringEditor.DistanceMetric);
                clusteringEditor.DistanceMetric = ClusterMetricType.MANHATTAN;
            });
            OkDialog(clusteringEditor, ()=>clusteringEditor.DialogResult = DialogResult.OK);
            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("AEDBC", GetNameValues(listGrid.DataGridView));

            const string reportName = "ListClusteringReport";
            RunDlg<ViewEditor>(listGrid.NavBar.CustomizeView, viewEditor =>
            {
                viewEditor.ViewName = reportName;
                viewEditor.OkDialog();
            });

            WaitForCondition(() => listGrid.IsComplete);
            Assert.AreEqual("AEDBC", GetNameValues(listGrid.DataGridView));
            RunDlg<NameLayoutForm>(listGrid.NavBar.RememberCurrentLayout, nameLayoutForm =>
            {
                nameLayoutForm.LayoutName = "MyLayout";
                nameLayoutForm.OkDialog();
            });
            
            RunDlg<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog, documentSettingsDlg =>
            {
                documentSettingsDlg.SelectTab(DocumentSettingsDlg.TABS.reports);
                documentSettingsDlg.ChooseViewsControl.CheckedViews =
                    new[] {PersistedViews.MainGroup.Id.ViewName(reportName)};
                documentSettingsDlg.OkDialog();
            });
            string skylineDocumentPath = Path.Combine(TestContext.TestDir, "ListClusteringTest.sky");
            RunUI(()=>SkylineWindow.SaveDocument(Path.Combine(TestContext.TestDir, skylineDocumentPath)));
            AssertEx.Serializable(SkylineWindow.Document);
            RunUI(()=>listGrid.DataboundGridControl.ShowHeatMap());
            var heatMap = WaitForOpenForm<HeatMapGraph>();
            Assert.IsNotNull(heatMap);
            RunUI(()=>SkylineWindow.SaveDocument(skylineDocumentPath));
            RunUI(()=>
            {
                SkylineWindow.NewDocument();
                SkylineWindow.OpenFile(skylineDocumentPath);
            });
            listGrid = FindOpenForm<ListGridForm>();
            Assert.IsNotNull(listGrid);
            heatMap = FindOpenForm<HeatMapGraph>();
            Assert.IsNotNull(heatMap);
            Assert.AreSame(listGrid.DataboundGridControl, heatMap.DataboundGridControl);

            OkDialog(listGrid, listGrid.Close);
        }

        public static void SetCellValue(DataGridView grid, int irow, int icol, object value)
        {
            ListDesignerTest.SetCellValue(grid, irow, icol, value);
        }

        private string GetNameValues(DataGridView grid)
        {
            string result = null;
            RunUI(() =>
            {
                result = string.Concat(grid.Rows.OfType<DataGridViewRow>().Select(row => row.Cells[0].Value).OfType<string>());
            });
            return result;
        }
    }
}
