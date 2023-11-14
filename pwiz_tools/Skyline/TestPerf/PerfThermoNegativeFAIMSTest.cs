/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
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

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

//
// Test for proper handling of Thermo FAIMS ion mobility data 
//   - Negative CoV values should be allowed (tested in TestEditCustomMoleculeDlg())
//   - On export of transition lists, CoV value should be pulled from ion mobility library as needed
//Also some general fixes for ion mobility handling but not FAIMS specific
//   - Ion mobility libraries should support molecules described only as name+mass(was assuming a chemical formula would be available)
//   - Viewing raw scan data should show the scan's ion mobility value when available
//   - Populating an ion mobility library via "Use Results" didn't work properly when two molecules shared the same name but different details (in this case, one defined by formula and the other by mass only)
//   - User should not be able to modify a precursor in the Targets tree such that it has an ion mobility value but no ion mobility units (tested in TestEditCustomMoleculeDlg())
// 

namespace TestPerf // Tests in this namespace are skipped unless the RunPerfTests attribute is set true
{
    [TestClass]
    public class PerfThermoNegativeFAIMSTest : AbstractFunctionalTestEx
    {
        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestThermoNegativeFAIMS()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfThermoNegativeFAIMS.zip");
            TestFilesPersistent = new[] { "02142020_Lumos_FAIMS_1_0-10_004.raw", "02142020_Lumos_FAIMS_1_-50-40_002.raw" }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            RunFunctionalTest();
        }

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }


        protected override void DoTest()
        {
            // This data set uses negative CoV values, and also has precursors that are defined mass-only - two things we had trouble with
            const string skyFile = "ThermoNegativeFAIMSTest.sky";
            Program.ExtraRawFileSearchFolder = TestFilesDir.PersistentFilesDir; // So we don't have to reload the raw files, which have probably moved relative to skyd file 
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(skyFile)));
            var document = WaitForDocumentLoaded();

            // Now inspect the pre-loaded chromatograms for ion mobility
            var transitionSettingsDlg = ShowDialog<TransitionSettingsUI>(
                () => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.IonMobility));
            var ionMobilityLibraryDlg = ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsDlg.IonMobilityControl.AddIonMobilityLibrary);
            var libName = "ThermoNegativeFAIMSTest";
            RunUI(() =>
            {
                ionMobilityLibraryDlg.CreateDatabaseFile(GetTestPath(libName + IonMobilityDb.EXT));
                ionMobilityLibraryDlg.SetOffsetHighEnergySpectraCheckbox(false);
                ionMobilityLibraryDlg.GetIonMobilitiesFromResults();
            });
            OkDialog(ionMobilityLibraryDlg, () => ionMobilityLibraryDlg.OkDialog());
            WaitForConditionUI(()=>Equals(transitionSettingsDlg.IonMobilityControl.SelectedIonMobilityLibrary, libName));
            OkDialog(transitionSettingsDlg, transitionSettingsDlg.OkDialog);
            document = WaitForDocumentChangeLoaded(document);

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.ToArray();
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(document);

            TestReports();

            // On export of transition lists, CoV value should be pulled from ion mobility library as needed
            TestExportTransitionList();
        }

        private void TestExportTransitionList()
        {
            var filePathActual = GetTestPath("actual.csv");
            FileEx.SafeDelete(filePathActual);

            var exportDialog = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));

            // Export CE optimization transition list
            RunUI(() =>
            {
                exportDialog.InstrumentType = ExportInstrumentType.THERMO_QUANTIVA;
                exportDialog.ExportStrategy = ExportStrategy.Single;
                exportDialog.MethodType = ExportMethodType.Standard;
                exportDialog.OptimizeType = ExportOptimize.NONE;
                exportDialog.WriteCompensationVoltages = true;
            });
            MultiButtonMsgDlg errDlg1 = null;
            ShowAndDismissDlg<MultiButtonMsgDlg>(() => exportDialog.OkDialog(filePathActual),
                // Expect The_settings_for_this_document_do_not_match_the_instrument_type...
                errDlg =>
                {
                    errDlg1 = errDlg;
                    RunUI(errDlg.ClickNo);
                });
            // Expect You_are_missing_compensation_voltages_for_the_following...
            var errDlg2 = FindOpenForms<MultiButtonMsgDlg>().FirstOrDefault(f => f != errDlg1);
            // ReSharper disable once PossibleNullReferenceException
            RunUI(errDlg2.ClickOk); 

            WaitForCondition(() => File.Exists(filePathActual));

            var actual = File.ReadAllLines(filePathActual);
            var expected = File.ReadAllLines(GetTestPath("expected.csv"));

            for (var i = 0; i < Math.Min(expected.Length, actual.Length); i++)
            {
                AssertEx.AreEqual(expected[i], actual[i], $@"transitions differ at line {i}");
            }
            AssertEx.AreEqual(expected.Length, actual.Length, @"different transition count");

        }

        private bool IsRecordMode { get { return false; } }

        private void TestReports(string msg = null)
        {
            // Verify reports working for CoV
            var expectedIM = new double?[]
            {
                // MS1 ionMobility Values recorded
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -50, -50, -50, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, null, null, null, null, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10,
                -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -10, -50, null, null, null, -10, -10, -10,
                -40, null, null, -10, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
                null, null
            };
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns(expectedIM.Length);

            for (var row = 0; row < documentGrid.DataGridView.Rows.Count; row++)
            {
                var expectedPrecursorIM = IsRecordMode ? null : expectedIM[row];
                CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, expectedPrecursorIM, msg, IsRecordMode);
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }
    }
}