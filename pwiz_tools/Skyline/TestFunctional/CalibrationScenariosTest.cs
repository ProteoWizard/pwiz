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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline;
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
                RunScenario(scenarioName, false);
                RunScenario(scenarioName, true);
            }
        }

        private void RunScenario(string scenarioName, bool forceMixedModeUI)
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(scenarioName + ".sky")));
            if (forceMixedModeUI)
            {
                // See how UI-mode affects the lis of available reports
                RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.mixed));
            }
            RunUI(() => SkylineWindow.ShareDocument(TestContext.GetTestResultsPath(scenarioName) + ".sky.zip", ShareType.COMPLETE));
            var reports = new[]
            {
                "CalibrationCurves",
                "PeptideResultQuantification"
            };
            foreach (var report in reports)
            {
                var reportName = (Program.ModeUI == SrmDocument.DOCUMENT_TYPE.small_molecules)
                    ? ("Molecule" + report.Replace("Peptide", string.Empty))
                    : report;
                ExportReport(scenarioName, reportName);
            }
        }

        private void ExportReport(string scenarioName, string reportName)
        {
            string baseName = TestContext.GetTestResultsPath(scenarioName);
            var exportReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            RunUI(() =>
            {
                exportReportDlg.ReportName = reportName;
                exportReportDlg.SetUseInvariantLanguage(true);
            });
            var outName = baseName + "_" + reportName;
            var fileName = outName + "_.csv";
            OkDialog(exportReportDlg, () => exportReportDlg.OkDialog(fileName, ','));
            WaitForConditionUI(() => File.Exists(fileName));
            AssertEx.NoDiff(File.ReadAllText(TestFilesDir.GetTestPath(scenarioName + "_" + reportName + ".csv")), File.ReadAllText(fileName));
        }
    }
}
