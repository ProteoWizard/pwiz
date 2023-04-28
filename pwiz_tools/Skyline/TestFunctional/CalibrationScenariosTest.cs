/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// This test generates files which are intended to be committed to the TargetedMS project
    /// in the "CalibrationScenariosTest" folder:
    /// https://github.com/LabKey/targetedms/tree/develop/test/sampledata/TargetedMS/Quantification/CalibrationScenariosTest
    /// </summary>
    [TestClass]
    public class CalibrationScenariosTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestCalibrationScenarios()
        {
            TestFilesZip = @"TestFunctional\CalibrationScenariosTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestContext.EnsureTestResultsDir();
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            RunUI(() => manageViewsForm.ImportViews(TestFilesDir.GetTestPath("CalibrationReports.skyr")));
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            var scenarioNames = new []
            {
                "CalibrationTest",
                "CalibrationExcludedTest",
                "p180test_calibration_DukeApril2016",
                "MergedDocuments",
                "BilinearCalibrationTest",
                "LinearInLogSpace"
            };
            foreach (var scenarioName in scenarioNames)
            {
                RunScenario(scenarioName);
            }
        }

        private void RunScenario(string scenarioName)
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(scenarioName + ".sky")));
            string baseName = TestContext.GetTestResultsPath(scenarioName);
            RunUI(() => SkylineWindow.ShareDocument(baseName + ".sky.zip", ShareType.COMPLETE));
            var reports = new[]
            {
                "CalibrationCurves",
                "PeptideResultQuantification"
            };
            foreach (String report in reports)
            {
                ExportReport(baseName, report);
            }
        }

        private void ExportReport(string baseName, string reportName)
        {
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.ReportName = reportName;
                exportReportDlg.SetUseInvariantLanguage(true);
            });
            OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(baseName + "_" + reportName + ".csv", ','));
        }
    }
}
