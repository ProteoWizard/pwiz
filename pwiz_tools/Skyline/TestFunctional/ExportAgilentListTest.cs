/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test isolation list export for Agilent TOF.
    /// </summary>
    [TestClass]
    public class ExportAgilentListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportAgilentList()
        {
            TestFilesZip = @"TestFunctional\ExportAgilentListTest.zip";
            RunFunctionalTest();
        }

        private CultureInfo _cultureInfo;
        private char _fieldSeparator;

        protected override void DoTest()
        {
            // For now, CSV files are produced with invariant culture because some manufacturers do not handle internationalized CSVs.
            _cultureInfo = CultureInfo.InvariantCulture;
            _fieldSeparator = TextUtil.GetCsvSeparator(_cultureInfo);
            string isolationWidth = string.Format(_cultureInfo, "Narrow (~{0:0.0} m/z)", 1.3);

            // Load document which is already configured for DDA, and contains data for scheduling
            string standardDocumentFile = TestFilesDir.GetTestPath("BSA_Protea_label_free_meth3.sky");
            RunUI(() => SkylineWindow.OpenFile(standardDocumentFile));

            // Export unscheduled DDA list.
            ExportIsolationList(
                "AgilentUnscheduledDda.csv", ExportMethodType.Standard,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 20, 2, "Preferred", 0, "", isolationWidth, 20.4),
                FieldSeparate("True", 444.55002, 20, 2, "Preferred", 0, "", isolationWidth, 19.2));

            // Export scheduled DDA list.
            ExportIsolationList(
                "AgilentScheduledDda.csv", ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 20, 2, "Preferred", 46.790, 2, isolationWidth, 20.4),
                FieldSeparate("True", 444.55002, 20, 2, "Preferred", 39.900, 2, isolationWidth, 19.2));

            // Export unscheduled Targeted list.
            SrmDocument doc = SkylineWindow.Document;
            SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(
                fs => fs.ChangeProductResolution(FullScanMassAnalyzerType.tof, 10000, null))), doc); // Change to Targeted/TOF.
            ExportIsolationList(
                "AgilentUnscheduledTargeted.csv", ExportMethodType.Standard,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 2, 0, "", isolationWidth, 20.4, ""),
                FieldSeparate("True", 444.55002, 2, 0, "", isolationWidth, 19.2, ""));

            // Export scheduled Targeted list.
            ExportIsolationList(
                "AgilentScheduledTargeted.csv", ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 2, 46.790, 2, isolationWidth, 20.4, ""),
                FieldSeparate("True", 444.55002, 2, 39.900, 2, isolationWidth, 19.2, ""));

            // Check error if analyzer is not TOF.
            doc = SkylineWindow.Document;
            SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(
                fs => fs.ChangeProductResolution(FullScanMassAnalyzerType.qit, 1.0, null))), doc); // Change to Targeted/QIT.

            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
                {
                    exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT_TOF;
                });
            RunDlg<MessageDlg>(
                exportMethodDlg.OkDialog,
                messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message, "The product mass analyzer type is not set to TOF in Transition Settings (under the Full Scan tab).");
                    messageDlg.OkDialog();
                });
            OkDialog(exportMethodDlg, exportMethodDlg.CancelDialog);
        }

        private void ExportIsolationList(string csvFilename, ExportMethodType methodType, params string[] checkStrings)
        {
            // Open Export Method dialog, and set method to scheduled or standard.
            string csvPath = TestContext.GetTestPath(csvFilename);
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = ExportInstrumentType.AGILENT_TOF;
                exportMethodDlg.MethodType = methodType;
                Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                Assert.IsFalse(exportMethodDlg.IsMaxTransitionsEnabled);
            });

            if (methodType == ExportMethodType.Standard)
            {
                // Simply close the dialog.
                OkDialog(exportMethodDlg, ()=> exportMethodDlg.OkDialog(csvPath));
            }
            else
            {
                // Close the SchedulingOptionsDlg, then wait for export dialog to close.
                RunDlg<SchedulingOptionsDlg>(() => exportMethodDlg.OkDialog(csvPath), schedulingOptionsDlg => schedulingOptionsDlg.OkDialog());
                WaitForClosedForm(exportMethodDlg);
            }

            // Check for expected output.
            string csvOut = File.ReadAllText(csvPath);
            AssertEx.Contains(csvOut, checkStrings);
        }

        private string FieldSeparate(params object[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; ; )
            {
                object value = values[i++];
                sb.Append(value is double ? string.Format(_cultureInfo, "{0}", value) : value.ToString());
                if (i == values.Length)
                    break;
                sb.Append(_fieldSeparator);
            }
            return sb.ToString();
        }

    }
}
