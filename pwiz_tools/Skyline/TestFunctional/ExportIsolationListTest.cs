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
    /// Test DDA and Targeted isolation list export.
    /// </summary>
    [TestClass]
    public class ExportIsolationListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportIsolationList()
        {
            TestFilesZip = @"TestFunctional\ExportIsolationListTest.zip";
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

            // Export Agilent unscheduled DDA list.
            ExportIsolationList(
                "AgilentUnscheduledDda.csv",
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 20, 2, "Preferred", 0, "", isolationWidth, 20.4),
                FieldSeparate("True", 444.55002, 20, 3, "Preferred", 0, "", isolationWidth, 19.2));

            // Export Agilent scheduled DDA list.
            ExportIsolationList(
                "AgilentScheduledDda.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetDdaHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 20, 2, "Preferred", 46.790, 2, isolationWidth, 20.4),
                FieldSeparate("True", 444.55002, 20, 3, "Preferred", 39.900, 2, isolationWidth, 19.2));

            // Export Thermo unscheduled DDA list.
            ExportIsolationList(
                "ThermoUnscheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Standard,
                ThermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(582.318971, "Positive", "", "", 20.4, 2, "LVNELTEFAK (light)"),
                FieldSeparate(444.55002, "Positive", "", "", 19.2, 3, "IKNLQS[+80.0]LDPSH (light)"));

            // Export Thermo scheduled DDA list.
            ExportIsolationList(
                "ThermoScheduledDda.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.None, ExportMethodType.Scheduled,
                ThermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(582.318971, "Positive", 46.79, 47.79, 20.4, 2, "LVNELTEFAK (light)"),
                FieldSeparate(444.55002, "Positive", 39.9, 40.90, 19.2, 3, "IKNLQS[+80.0]LDPSH (light)"));

            // Export Agilent unscheduled Targeted list.
            ExportIsolationList(
                "AgilentUnscheduledTargeted.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 2, 0, "", isolationWidth, 20.4, ""),
                FieldSeparate("True", 444.55002, 3, 0, "", isolationWidth, 19.2, ""));

            // Export Agilent scheduled Targeted list.
            ExportIsolationList(
                "AgilentScheduledTargeted.csv", 
                ExportInstrumentType.AGILENT_TOF, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                AgilentIsolationListExporter.GetTargetedHeader(_fieldSeparator),
                FieldSeparate("True", 582.318971, 2, 46.790, 2, isolationWidth, 20.4, ""),
                FieldSeparate("True", 444.55002, 3, 39.900, 2, isolationWidth, 19.2, ""));

            // Export Thermo unscheduled Targeted list.
            ExportIsolationList(
                "ThermoUnscheduledTargeted.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.Targeted, ExportMethodType.Standard,
                ThermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(582.318971, "Positive", "", "", 20.4, 2, "LVNELTEFAK (light)"),
                FieldSeparate(444.55002, "Positive", "", "", 19.2, 3, "IKNLQS[+80.0]LDPSH (light)"));

            // Export Thermo scheduled Targeted list.
            ExportIsolationList(
                "ThermoScheduledTargeted.csv", 
                ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanAcquisitionMethod.Targeted, ExportMethodType.Scheduled,
                ThermoQExactiveIsolationListExporter.GetHeader(_fieldSeparator),
                FieldSeparate(582.318971, "Positive", 46.79, 47.79, 20.4, 2, "LVNELTEFAK (light)"),
                FieldSeparate(444.55002, "Positive", 39.9, 40.90, 19.2, 3, "IKNLQS[+80.0]LDPSH (light)"));

            // Check error if analyzer is not set correctly.
            CheckMassAnalyzer(ExportInstrumentType.AGILENT_TOF, FullScanMassAnalyzerType.tof);
            CheckMassAnalyzer(ExportInstrumentType.THERMO_Q_EXACTIVE, FullScanMassAnalyzerType.orbitrap);
        }

        private void ExportIsolationList(
            string csvFilename, string instrumentType, FullScanAcquisitionMethod acquisitionMethod, 
            ExportMethodType methodType, params string[] checkStrings)
        {
            // Set acquisition method, mass analyzer type, resolution, etc.
            SrmDocument doc = SkylineWindow.Document;
            if (Equals(instrumentType, ExportInstrumentType.AGILENT_TOF))
            {
                SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeProductResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeAcquisitionMethod(acquisitionMethod, null))), doc);
            }
            else
            {
                SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeProductResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeAcquisitionMethod(acquisitionMethod, null))), doc);
            }

            // Open Export Method dialog, and set method to scheduled or standard.
            string csvPath = TestContext.GetTestPath(csvFilename);
            var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() =>
            {
                exportMethodDlg.InstrumentType = instrumentType;
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

        private static void CheckMassAnalyzer(string instrumentType, FullScanMassAnalyzerType massAnalyzer)
        {
            SrmDocument doc = SkylineWindow.Document;
            // NOTE: Change to a DIFFERENT mass analyzer than we expect.
            if (!Equals(instrumentType, ExportInstrumentType.AGILENT_TOF))
            {
                SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeProductResolution(FullScanMassAnalyzerType.tof, 10000, null)
                    .ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null))), doc);
            }
            else
            {
                SkylineWindow.SetDocument(doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeProductResolution(FullScanMassAnalyzerType.orbitrap, 60000, 400)
                    .ChangeAcquisitionMethod(FullScanAcquisitionMethod.Targeted, null))), doc);
            }

            var exportMethodDlg =
                ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() => { exportMethodDlg.InstrumentType = instrumentType; });
            RunDlg<MessageDlg>(
                exportMethodDlg.OkDialog,
                messageDlg =>
                {
                    AssertEx.Contains(messageDlg.Message, "The precursor mass analyzer type is not set to");
                    messageDlg.OkDialog();
                });
            OkDialog(exportMethodDlg, exportMethodDlg.CancelDialog);
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
