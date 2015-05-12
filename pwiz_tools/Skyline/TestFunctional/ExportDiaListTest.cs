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
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test the "Export Isolation List" features of Skyline's "Export" menu item.
    /// </summary>
    [TestClass]
    public class ExportDiaListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExportDiaList()
        {
            TestDirectoryName = "ExportDiaListTest";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            Directory.CreateDirectory(TestContext.TestDir);

            {
                var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                    {
                        fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                        fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                        //fullScanDlg.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                        fullScanDlg.ProductMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                    });

                // Open the isolation scheme dialog and calculate dialog.
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(fullScanDlg.AddIsolationScheme);
                RunUI(() =>
                    {
                        editDlg.IsolationSchemeName = "TestExport1";
                        editDlg.UseResults = false;
                    });

                // Define a simple isolation scheme.
                RunDlg<CalculateIsolationSchemeDlg>(
                    editDlg.Calculate,
                    calcDlg =>
                    {
                        calcDlg.Start = 100;
                        calcDlg.End = 104;
                        calcDlg.WindowWidth = 1;
                        calcDlg.OkDialog();
                    });
                OkDialog(editDlg, editDlg.OkDialog);
                OkDialog(fullScanDlg, fullScanDlg.OkDialog);

                // Export simple isolation scheme.
                string csvPath = TestContext.GetTestPath("TestExport1.csv");
                RunDlg<ExportMethodDlg>(
                    (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList)),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE;
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                        Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                        Assert.IsFalse(exportMethodDlg.IsMaxTransitionsEnabled);

                        exportMethodDlg.OkDialog(csvPath);
                    });

                // Check for expected output.
                string csvOut = File.ReadAllText(csvPath);
                Assert.AreEqual(csvOut, LineSeparateMzs(100.5, 101.5, 102.5, 103.5));
            }

            {
                var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                    {
                        fullScanDlg.MaxInclusions = 5000;
                        fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                        fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                    });

                // Open the isolation scheme dialog and calculate dialog.
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(fullScanDlg.AddIsolationScheme);
                RunUI(() =>
                {
                    editDlg.IsolationSchemeName = "TestExport2";
                    editDlg.UseResults = false;
                });

                // Define Jarrett Egertson's multliplexed isolation scheme. 
                RunDlg<CalculateIsolationSchemeDlg>(
                    editDlg.Calculate,
                    calcDlg =>
                    {
                        calcDlg.Start = 500;
                        calcDlg.End = 900;
                        calcDlg.WindowWidth = 4;
                        calcDlg.WindowsPerScan = 5;
                        calcDlg.OkDialog();
                    });
                OkDialog(editDlg, editDlg.OkDialog);
                OkDialog(fullScanDlg, fullScanDlg.OkDialog);

                // Export multiplexed isolation scheme.
                string csvPath = TestContext.GetTestPath("TestExport2.csv");
                RunDlg<ExportMethodDlg>(
                    (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList)),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE;
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                        Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                        Assert.IsFalse(exportMethodDlg.IsMaxTransitionsEnabled);

                        exportMethodDlg.CalculationTime = 3;
                        // To debug generated cycles:
                        //exportMethodDlg.DebugCycles = true;
                        exportMethodDlg.OkDialog(csvPath);
                    });

                // Check for expected output.
                string csvOut = File.ReadAllText(csvPath);
                Assert.IsTrue(csvOut.StartsWith(LineSeparateMzs(790.0, 826.0, 806.0, 646.0, 582.0, 718.0, 626.0, 698.0, 798.0, 862.0)));
                Assert.AreEqual(5001, csvOut.Split('\n').Length);
            }

            {
                // Check for export of an overlapped DIA method.
                var fullScanDlg = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    fullScanDlg.MaxInclusions = 5000;
                    fullScanDlg.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                    fullScanDlg.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                });

                // Open the isolation scheme dialog and calculate dialog.
                var editDlg = ShowDialog<EditIsolationSchemeDlg>(fullScanDlg.AddIsolationScheme);
                RunUI(() =>
                {
                    editDlg.IsolationSchemeName = "TestExport3";
                    editDlg.UseResults = false;
                });

                // Define an overlapped isolation scheme. 
                RunDlg<CalculateIsolationSchemeDlg>(
                    editDlg.Calculate,
                    calcDlg =>
                    {
                        calcDlg.Start = 500;
                        calcDlg.End = 600;
                        calcDlg.WindowWidth = 20;
                        calcDlg.Deconvolution = EditIsolationSchemeDlg.DeconvolutionMethod.OVERLAP;
                        calcDlg.OkDialog();
                    });
                OkDialog(editDlg, editDlg.OkDialog);
                OkDialog(fullScanDlg, fullScanDlg.OkDialog);

                // Export overlapped isolation scheme.
                string csvPath = TestContext.GetTestPath("TestExport3.csv");
                RunDlg<ExportMethodDlg>(
                    (() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList)),
                    exportMethodDlg =>
                    {
                        exportMethodDlg.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE;
                        Assert.IsFalse(exportMethodDlg.IsOptimizeTypeEnabled);
                        Assert.IsTrue(exportMethodDlg.IsTargetTypeEnabled);
                        Assert.IsFalse(exportMethodDlg.IsDwellTimeVisible);
                        Assert.IsFalse(exportMethodDlg.IsMaxTransitionsEnabled);

                        exportMethodDlg.OkDialog(csvPath);
                    });

                // Check for expected output.
                string csvOut = File.ReadAllText(csvPath);
                Assert.IsTrue(csvOut == LineSeparateMzs(510, 530, 550, 570, 590, 610, 500, 520, 540, 560, 580, 600));
            }

            {
                // Check errors for instruments that can't export a DIA method.
                var exportMethodDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.Method));

                string[] nonDiaInstruments =
                    {
                        ExportInstrumentType.ABI_TOF,
                        ExportInstrumentType.THERMO_LTQ
                    };

                foreach (var i in nonDiaInstruments)
                {
                    var instrument = i;
                    RunUI(() =>
                    {
                        exportMethodDlg.InstrumentType = instrument;
                    });
                    RunDlg<MessageDlg>(
                        exportMethodDlg.OkDialog,
                        messageDlg =>
                        {
                            AssertEx.AreComparableStrings(Resources.ExportMethodDlg_OkDialog_Export_of_DIA_method_is_not_supported_for__0__, messageDlg.Message, 1);
                            messageDlg.OkDialog();
                        });
                }

                OkDialog(exportMethodDlg, exportMethodDlg.CancelDialog);
            }
        }

        public static string LineSeparateMzs(params double[] mzValues)
        {
            var sb = new StringBuilder();
            foreach (var value in mzValues)
                sb.AppendLine(SequenceMassCalc.PersistentMZ(value).ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
