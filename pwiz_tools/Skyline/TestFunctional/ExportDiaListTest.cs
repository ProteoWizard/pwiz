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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
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
            TestDirectoryName = "ExportIsolationListTest";
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
                Assert.AreEqual(csvOut, Helpers.LineSeparate(100.5, 101.5, 102.5, 103.5));
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
                Assert.IsTrue(csvOut.StartsWith(Helpers.LineSeparate(790, 826, 806, 646, 582, 718, 626, 698, 798, 862)));
                Assert.AreEqual(5001, csvOut.Split('\n').Length);
            }
        }
    }
}
