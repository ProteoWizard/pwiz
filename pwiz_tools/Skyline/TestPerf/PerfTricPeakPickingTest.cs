/*
 * Original author: Max Horowitz-Gelb <maxhg .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfTricPeakPickingTest : AbstractFunctionalTestEx
    {

        [TestMethod, Timeout(7200000)]
        public void TestTricPeakPickingPerf()
        {
            // RunPerfTests = true;

            TestFilesPersistent = new[] { "." };  // All persistent. No saving
            TestFilesZipPaths = new[] { @"http://proteome.gs.washington.edu/software/test/skyline-perf/TricPeakPickingTest.zip" };

            RunFunctionalTest();
        }

        private string GetTestPath(string path, bool relative = false)
        {
            path = Path.Combine("TricPeakPickingTest", path);
            if (!relative)
                path = TestFilesDir.GetTestPath(path);
            return path;
        }

        public string GetIntlCsvPath(string path, string outDir)
        {
            string localPath = GetTestPath(path);
            string basename = Path.GetFileNameWithoutExtension(path);
            string extension = Path.GetExtension(path);
            string intlPath = Path.Combine(outDir, basename + "Intl" + extension);
            return TextUtil.WriteDsvToCsvLocal(localPath, intlPath, true) ? intlPath : localPath;
        }

        protected override void DoTest()
        {
            // Because presistent files use ".", an explicit output directory is needed
            string outDir = Path.Combine(TestContext.TestDir, GetType().Name);
            Directory.CreateDirectory(outDir); // In case it doesn't already exists

            OpenDocument(GetTestPath("HannesTRIC-retry_12.sky", true));

            {
                // Create mprophet model without retention time squared score
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                RunUI(() =>
                {
                    reintegrateDlg.UseTric = true;
                    reintegrateDlg.OverwriteManual = true;
                    reintegrateDlg.ReintegrateAll = true;
                });
                RunDlg<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel, dlg =>
                {
                    dlg.UsesDecoys = true;
                    dlg.UsesSecondBest = false;
                    dlg.PeakScoringModelName = "TestScoringModel";
                    //Disable squared retention time.
                    string rtSquaredCalc = Resources.MQuestRetentionTimeSquaredPredictionCalc_MQuestRetentionTimeSquaredPredictionCalc_Retention_time_difference_squared;
                    dlg.PeakCalculatorsGrid.Items.First(p => p.Name.Contains(rtSquaredCalc)).IsEnabled
                        = false;
                    dlg.TrainModelClick();
                    dlg.OkDialog();
                });

                // Run TRIC
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }
            
            // Export report
            var tricBoundariesPath = Path.Combine(outDir, "tricBoundaries.csv");
            
            ExportPeakBoundaries(tricBoundariesPath);
            
            // Run reintegrate without alignment
            {
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                RunUI(() =>
                {
                    reintegrateDlg.UseTric = false;
                    reintegrateDlg.OverwriteManual = true;
                    reintegrateDlg.ReintegrateAll = true;
                    reintegrateDlg.ComboPeakScoringModelSelected = "TestScoringModel";
                });
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }

            // Export report            
            var noAlignBoundariesPath = Path.Combine(outDir, "noalignBoundaries.csv");
            
            ExportPeakBoundaries(noAlignBoundariesPath);
            
            // Open gold document
            OpenDocument(GetTestPath("SkylineResult500Peptides-corrected-12.sky", true));

            // Compare peaks picked 
            var comparePeaksDlg = ShowDialog<ComparePeakPickingDlg>(SkylineWindow.ShowCompareModelsDlg);
            AddPeakBoundariesFile(comparePeaksDlg, tricBoundariesPath,"tric", 2);
            AddPeakBoundariesFile(comparePeaksDlg, noAlignBoundariesPath, "noalign", 2);

            // Add OpenSWATH results also
            var openSwathTricPath = GetIntlCsvPath("result_all_target1pcnt_localMST_10pcnt_lld-Mods-Files-Apex.csv", outDir);
            AddPeakBoundariesFile(comparePeaksDlg, openSwathTricPath, "open swath tric", 3);
            var openSwathNoAlignPath = GetIntlCsvPath("noalign_all_1pcnt-Mods-Files-Apex.csv", outDir);
            AddPeakBoundariesFile(comparePeaksDlg, openSwathNoAlignPath, "open swath no align", 3);

            var noAlignCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(noAlignBoundariesPath));
            var tricCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(tricBoundariesPath));
            var openSwathTricCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(openSwathTricPath));
            var openSwathNoAlignCompare =
                comparePeaksDlg.ComparePeakBoundariesList.
                    First(comp => comp.FilePath.Equals(openSwathNoAlignPath));

            // Look at total correct ids at 1% observed FDR
            Assert.AreEqual(3932, comparePeaksDlg.GetYAtCutoffRoc(tricCompare));
            Assert.AreEqual(3735, comparePeaksDlg.GetYAtCutoffRoc(noAlignCompare));
            Assert.AreEqual(3983, comparePeaksDlg.GetYAtCutoffRoc(openSwathTricCompare));
            Assert.AreEqual(3719, comparePeaksDlg.GetYAtCutoffRoc(openSwathNoAlignCompare));

            // Look at observerd fdr at expected 1% FDR
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(tricCompare),0.002,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(noAlignCompare),0.0049,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(openSwathTricCompare),0.0057,0.0001);
            Assert.AreEqual(comparePeaksDlg.GetYAtCutoffQQ(openSwathNoAlignCompare),0.0155,0.0001);

            OkDialog(comparePeaksDlg,comparePeaksDlg.OkDialog);
        }

        private static void AddPeakBoundariesFile(ComparePeakPickingDlg comparePeaksDlg, string path, string name, int warnings)
        {
            var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(comparePeaksDlg.Add);
            RunUI(() =>
            {
                addPeakCompareDlg.IsModel = false;
                addPeakCompareDlg.FileName = name;
                addPeakCompareDlg.FilePath = path;
            });
            // Skip through warnings
            ShowDialog<AlertDlg>(addPeakCompareDlg.OkDialog);
            for (int i = 0; i < warnings; i++)
            {
                var warningDialogue = WaitForOpenForm<AlertDlg>();
                OkDialog(warningDialogue, warningDialogue.OkDialog);
            }
            WaitForClosedForm(addPeakCompareDlg);
        }

        private void ExportPeakBoundaries(string path)
        {
            //Remove if exists
            File.Delete(path);

            var exportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);

            const string reportName = "Precursor Boundaries";
            // Make sure has report template
            if (!exportDlg.ReportNames.Contains(reportName))
            {
                RunDlg<ManageViewsForm>(exportDlg.EditList, editDlg =>
                {
                    editDlg.ImportViews(GetTestPath("PrecursorBoundaries.skyr"));
                    editDlg.OkDialog();
                });
            }
            RunUI(() =>
            {
                exportDlg.ReportName = reportName;
            });
            OkDialog(exportDlg, () => exportDlg.OkDialog(path, TextUtil.CsvSeparator));
        }
    }
}
