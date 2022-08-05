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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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
        public void BrukerDiaPasefImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = GetPerfTestDataURL(@"PerfImportBrukerDiaPasef_v2.zip");
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
                AssertEx.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, out var chromGroupInfo));

                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
            }
            AssertEx.AreEqual(205688.75, maxHeight, 1); 

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
            TestReports();

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
                AssertEx.AreEqual(windowCount, listIsolationWindows.Count);
                foreach (var isolationWindow in listIsolationWindows)
                {
                    AssertEx.AreEqual(windowWidth, isolationWindow.End - isolationWindow.Start, 
                        string.Format("Range {0} to {1} does not have width {2}", isolationWindow.Start, isolationWindow.End, windowWidth));
                    AssertEx.AreEqual(margin, isolationWindow.StartMargin);
                    AssertEx.AreEqual(margin, isolationWindow.EndMargin);
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


        private void TestReports(string msg = null)
        {
            // Verify reports working for CCS
            var row = 0;
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns(813); // Would be 816 byt default, but single_py2 replicate really doesn't have signal for KPLVIIAEDVDGEALSTLVLNR fragments
            var imPrecursor = 1.1732;
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, imPrecursor, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "TransitionResult.IonMobilityFragment", row, imPrecursor, msg); // Document is all precursors
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityFilter.IonMobilityUnitsL10NString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.12, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 473.2742, msg);
            EnableDocumentGridColumns(documentGrid,
                Resources.ReportSpecList_GetDefaults_Peptide_RT_Results,
                SkylineWindow.Document.PeptideCount * SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
            foreach (var rt in new[] {
                14.35, 14.34, 14.33, 14.33, 14.15, 14.12, 14.11, 14.11, 14.63, 14.61, 14.61, 14.61, 14.75, 14.74, 14.72, 14.73, 14.06, 14.04,
                14.03, 14.03, 14.43, 14.43, 14.42, 14.43, 14.36, 14.37, 14.35, 14.35, 14.31, 14.31, 14.29, 14.28, 14.48, 14.49, 14.47, 14.48,
                14.69, 14.67, 14.67, 14.67, 14.61, 14.34, 14.34, 14.35, 14.25, 14.25, 14.22, 14.23, 14.37, 14.36, 14.35, 14.35, 14.51, 14.52,
                14.5, 14.5, 14.24, 14.25, 14.22, 14.23, 14.81, 14.78, 14.78, 14.79, 14.63, 14.61, 14.61, 14.61, 14.48, 14.46, 14.46, 14.47,
                14.52, 14.49, 14.49, 14.49, 14.67, 14.65, 14.65, 14.65, 14.46, 14.45, 14.45, 14.45, 14.43, 14.43, 14.42, 14.43, 14.24, 14.24,
                14.25, 14.25, 14.48, 14.46, 14.45, 14.44, 14.19, 14.16, 14.16, 14.17, 14.38, 14.34, 14.34, 14.36, 14.88, 14.86, 14.86, 14.85,
                14.22, 14.22, 14.21, 14.21, 14.19, 14.19, 14.18, 14.18, 14.11, 14.09, 14.09, 14.1, 14.72, 14.7, 14.71, 14.71, 14.64, 14.61,
                14.62, 14.61, 14.12, 14.1, 14.1, 14.1, 14.23, 14.21, 14.2, 14.2
            })
            {
                CheckDocumentResultsGridFieldByName(documentGrid, "PeptideRetentionTime", row++, rt, msg, IsRecordMode);
            }

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

    }
}
