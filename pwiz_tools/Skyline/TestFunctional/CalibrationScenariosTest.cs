using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline.Controls.Databinding;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
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
            var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
            var manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
            RunUI(() => manageViewsForm.ImportViews(TestFilesDir.GetTestPath("CalibrationReports.skyr")));
            OkDialog(manageViewsForm, manageViewsForm.OkDialog);
            OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            var scenarioNames = new []
            {
                "CalibrationTest",
                "p180test_calibration_DukeApril2016",
                "MergedDocuments"
            };
            foreach (var scenarionName in scenarioNames)
            {
                RunScenario(scenarionName);
            }
        }

        private void RunScenario(string scenarioName)
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath(scenarioName + ".sky")));
            if (null != TestContext.TestRunResultsDirectory)
            {
                var directory = Path.Combine(TestContext.TestRunResultsDirectory, "CalibrationScenarioTest");
                Directory.CreateDirectory(directory);
                string baseName = Path.Combine(directory, scenarioName);
                RunUI(() => SkylineWindow.ShareDocument(baseName + ".sky.zip", true));
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
