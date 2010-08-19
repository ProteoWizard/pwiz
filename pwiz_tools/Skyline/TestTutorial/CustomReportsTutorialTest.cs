/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Custom Reports and Results Grid
    /// </summary>
    [TestClass]
    public class CustomReportsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCustomReportsTutorial()
        {
            TestFilesZip = @"https://brendanx-uw1.gs.washington.edu/tutorials/CustomReports.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Custom Reports and Results Grid

            // Data Overview, p. 2
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath(@"CustomReports\Study7_example.sky")));
            RunDlg<FindPeptideDlg>(SkylineWindow.ShowFindPeptideDlg, findPeptideDlg =>
            {
                findPeptideDlg.Sequence = "HGFLPR";
                findPeptideDlg.OkDialog();
            });
            RunUI(() =>
            {
                Assert.AreEqual("HGFLPR", SkylineWindow.SequenceTree.SelectedNode.Text);
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForCondition(() => !SkylineWindow.GraphPeakArea.IsHidden);
            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            WaitForCondition(() => SkylineWindow.GraphPeakArea.IsHidden);

            // Creating a Simple Custom Report, p. 3
            var exportReportDlg = ShowDialog<ExportReportDlg>(SkylineWindow.ShowExportReportDialog);
            var editReportListDlg = ShowDialog<EditListDlg<SettingsListBase<ReportSpec>, ReportSpec>>(exportReportDlg.EditList);
            var pivotReportDlg = ShowDialog<PivotReportDlg>(editReportListDlg.AddItem);
            RunUI(() =>
            {
                pivotReportDlg.ReportName = "Overview";
                Assert.IsTrue(pivotReportDlg.TrySelect(new Identifier("Peptides", "Sequence")));
                pivotReportDlg.AddSelectedColumn();
                Assert.AreEqual(1, pivotReportDlg.ColumnCount);
                var expectedFields = new[]
                {
                     new Identifier("ProteinName"), new Identifier("ProteinDescription"),
                     new Identifier("ProteinSequence"), new Identifier("ProteinNote"),
                     new Identifier("Results")
                };
                foreach(Identifier id in expectedFields)
                {
                   Assert.IsTrue(pivotReportDlg.TrySelect(id));
                }
                var columnsToAdd = new[] 
                { 
                    new Identifier("Peptides", "Precursors", "IsotopeLabelType"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "BestRetentionTime"),
                    new Identifier("Peptides", "Precursors", "PrecursorResults", "TotalArea") 
                };
                foreach(Identifier id in columnsToAdd)
                {
                    Assert.IsTrue(pivotReportDlg.TrySelect(id));
                    pivotReportDlg.AddSelectedColumn();
                }
                Assert.AreEqual(4, pivotReportDlg.ColumnCount);
                pivotReportDlg.PivotReplicate = true;
            });
            RunDlg<PreviewReportDlg>(pivotReportDlg.ShowPreview, previewReportDlg =>
            {
                Assert.AreEqual(20, previewReportDlg.RowCount);
                Assert.AreEqual(58, new List<string>(previewReportDlg.ColumnHeaderNames).Count);
                previewReportDlg.OkDialog();
            });
            RunUI(() =>
            {
                pivotReportDlg.OkDialog();
                editReportListDlg.OkDialog();
                exportReportDlg.CancelClick();
            });
        }
    }
}
