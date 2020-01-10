/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker PASEF in concert with Mascot.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerDiaPasefTest : AbstractFunctionalTestEx
    {
        private bool IsRecordMode { get { return false; } }

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void BrukerDiaPasefImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportBrukerDiaPasef_v2.zip";
            TestFilesPersistent = new[] { ".d" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();

            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        protected override void DoTest()
        {
            
            // Load a .sky with mising .skyd, forcing re-import with existing parameters
            // This simplifies the test code since we have six different PASEF modes to deal with here
            string skyfile = TestFilesDir.GetTestPath("snipped.sky");
            
            // Update the paths to the .d files mentioned in the skyline doc
            string text = File.ReadAllText(skyfile);
            text = text.Replace(@"PerfImportBrukerDiaPasef", TestFilesDir.PersistentFilesDir);
            text = RemoveReplicateReference(text, @"diagonalSWATH_MSMS_Slot1-10_1_3420"); // Remove reference to replicate with file type that we don't need to handle at this time
            text = RemoveReplicateReference(text, @"SWATHlike_MSMS_Slot1-10_1_3421"); // Remove reference to replicate with file type that we don't need to handle at this time
            File.WriteAllText(skyfile, text);

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();
            var doc = SkylineWindow.Document;
            RunUI(() =>
            {
                Settings.Default.ImportResultsSimultaneousFiles = (int) MultiFileLoader.ImportResultsSimultaneousFileOptions.many;
                SkylineWindow.OpenFile(skyfile);
            });

            var doc1 = WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            AssertEx.IsDocumentState(doc1, null, 1, 34, 34, 204);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out var chromGroupInfo));

                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
            }
            Assert.AreEqual(205688.75, maxHeight, 1); 

            // Test isolation scheme import (combined mode only)
            if (!MsDataFileImpl.ForceUncombinedIonMobility)
            {
                var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() => tranSettings.TabControlSel = TransitionSettingsUI.TABS.FullScan);
                var isoEditor = ShowDialog<EditIsolationSchemeDlg>(tranSettings.AddIsolationScheme);
                RunUI(() => isoEditor.UseResults = false);
                ValidateIsolationSchemeImport(isoEditor, "190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFdouble_py3_MSMS_Slot1-10_1_3426.d",
                    32, 25, null);
                ValidateIsolationSchemeImport(isoEditor, "190314_TEN_175mingr_7-35_500nL_HeLa_SWATHlike_MSMS_Slot1-10_1_3421.d",
                    24, 25, 0.5);
                OkDialog(isoEditor, isoEditor.CancelDialog);
                OkDialog(tranSettings, tranSettings.CancelDialog);
            }

            // Does CCS show up in reports?
            TestReports(doc1);

        }

        private void ValidateIsolationSchemeImport(EditIsolationSchemeDlg isoEditor, string fileName,
            int windowCount, int windowWidth, double? margin)
        {
            RunDlg<OpenDataSourceDialog>(isoEditor.ImportRanges, openData =>
            {
                openData.SelectFile(TestFilesDir.GetTestPath(fileName));
                openData.Open();
            });
            WaitForConditionUI(() => windowCount == (isoEditor.GetIsolationWindows()?.Count ?? 0));
            RunUI(() =>
            {
                var listIsolationWindows = isoEditor.GetIsolationWindows();
                Assert.AreEqual(windowCount, listIsolationWindows.Count);
                foreach (var isolationWindow in listIsolationWindows)
                {
                    Assert.AreEqual(windowWidth, isolationWindow.End - isolationWindow.Start, 
                        string.Format("Range {0} to {1} does not have width {2}", isolationWindow.Start, isolationWindow.End, windowWidth));
                    Assert.AreEqual(margin, isolationWindow.StartMargin);
                    Assert.AreEqual(margin, isolationWindow.EndMargin);
                }
            });
        }

        private string RemoveReplicateReference(string text, string replicateName)
        {
            // Remove reference to replicate with file type that we don't need to handle at this time
            var open = text.IndexOf(string.Format("<replicate name=\"{0}\">", replicateName), StringComparison.Ordinal);
            var close = text.IndexOf("</replicate>", open, StringComparison.Ordinal) + 12;
            var snip = text.Substring(0, open) + text.Substring(close);
            while ((open = snip.IndexOf(replicateName, StringComparison.Ordinal)) != -1)
            {
                open = snip.LastIndexOf('<', open);
                close = snip.IndexOf(">", open, StringComparison.Ordinal);
                snip = snip.Substring(0, open) + snip.Substring(close + 1);
            }
            return snip;
        }


        private void TestReports(SrmDocument doc1, string msg = null)
        {
            // Verify reports working for CCS
            var row = 0;
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            EnableDocumentGridColumns(documentGrid,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                doc1.PeptideTransitionCount,
                new[]
                {
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityMS1",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityFragment",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityUnits",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityWindow"
                });
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, 1.1732, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityFragment", row, 1.1732, msg); 
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityValue.GetUnitsString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.12, msg);
            CheckFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 473.2742, msg);
            EnableDocumentGridColumns(documentGrid,
                Resources.ReportSpecList_GetDefaults_Peptide_RT_Results,
                doc1.PeptideCount * doc1.MeasuredResults.Chromatograms.Count, null);
            foreach (var rt in new[] {
                14.35, 14.34, 14.33, 14.33, 14.15, 14.12, 14.11, 14.11, 14.63, 14.61, 14.61, 14.61, 14.75, 14.74, 14.72, 14.73, 14.06, 14.04,
                14.03, 14.03, 14.43, 14.43, 14.42, 14.43, 14.36, 14.37, 14.35, 14.35, 14.31, 14.31, 14.29, 14.28, 14.48, 14.49, 14.47, 14.48,
                14.69, 14.67, 14.67, 14.67, 14.61, 14.34, 14.34, 14.35, 14.25, 14.25, 14.22, 14.23, 14.37, 14.36, 14.35, 14.35, 14.51, 14.52,
                14.5, 14.5, 14.24, 14.25, 14.22, 14.23, 14.81, 14.78, 14.78, 14.78, 14.63, 14.61, 14.61, 14.61, 14.48, 14.46, 14.46, 14.47,
                14.52, 14.49, 14.49, 14.49, 14.67, 14.65, 14.65, 14.65, 14.46, 14.45, 14.45, 14.45, 14.44, 14.43, 14.42, 14.43, 14.24, 14.24,
                14.25, 14.25, 14.48, 14.46, 14.45, 14.44, 14.19, 14.16, 14.16, 14.17, 14.38, 14.34, 14.34, 14.36, 14.88, 14.86, 14.86, 14.85,
                14.22, 14.22, 14.21, 14.21, 14.19, 14.19, 14.18, 14.18, 14.11, 14.09, 14.09, 14.1, 14.72, 14.7, 14.71, 14.71, 14.64, 14.61,
                14.62, 14.61, 14.12, 14.1, 14.1, 14.1, 14.23, 14.21, 14.2, 14.2
            })
            {
                CheckFieldByName(documentGrid, "PeptideRetentionTime", row++, rt, msg, true);
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

        private static int ValuesRecordedCount;

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, double? expected, string msg = null, bool recordValues = false)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as double?;
                Assert.AreEqual(expected.HasValue, val.HasValue, name + (msg ?? string.Empty));
                if (!IsRecordMode || !recordValues)
                    Assert.AreEqual(expected ?? 0, val ?? 0, 0.005, name + (msg ?? string.Empty));
                else
                {
                    Console.Write(@"{0:0.##}, ", val);
                    if (++ValuesRecordedCount % 18 == 0)
                        Console.WriteLine();
                }
            });
        }

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, string expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as string;
                Assert.AreEqual(expected, val, name + (msg ?? string.Empty));
            });
        }
    }
}
