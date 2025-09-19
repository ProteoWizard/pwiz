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
    /// Verify consistent import of Bruker DIA PASEF.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerDiaPasefTest : AbstractFunctionalTestEx
    {
        protected override bool IsRecordMode => false;

        [TestMethod, NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING)] // No parallel testing as Bruker reader locks the files it reads
        public void BrukerDiaPasefImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();

            // Version 3 of these early example data files contains hand-corrected DiaFrameMsMsWindows tables.
            // Shortly after they were created Bruker decided that ScanNumEnd should be EXclusive instead of INclusive,
            // leaving those values off by one. The issue, while known (to some!), became apparent during
            // Diagonal PASEF development. The gaps aren't properly handled when we write the 5 array format,
            // which makes it hard to test for consistent results. 
            TestFilesZip = GetPerfTestDataURL(@"PerfImportBrukerDiaPasef_v3.zip");
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
            // Note the expected values as saved in the test file
            var skyfile = TestFilesDir.GetTestPath("snipped.sky");

            RunUI(() =>
            {
                SkylineWindow.OpenFile(skyfile);
            });
            var doc0 = WaitForDocumentLoaded();

            // Update the paths to the .d files mentioned in the skyline doc
            string text = File.ReadAllText(skyfile);
            var savedPath = "c:\\Skyline T&amp;est ^Data\\Perftests\\PerfImportBrukerDiaPasef_v3";
            text = text.Replace(savedPath, PathEx.EscapePathForXML(TestFilesDir.PersistentFilesDir));

            // text = RemoveReplicateReference(text, @"190314_TEN_175mingr_7-35_500nL_HeLa_AIF_MSMS_Slot1-10_1_3423"); // Remove reference to replicate for debug convenience
            // text = RemoveReplicateReference(text, @"single_py1_MSMS_Slot1-10_1_3425"); // Remove reference to replicate for debug convenience
            // text = RemoveReplicateReference(text, @"double_py3_MSMS_Slot1-10_1_3426"); // Remove reference to replicate for debug convenience
            // text = RemoveReplicateReference(text, @"single_py2_MSMS_Slot1-10_1_3427"); // Remove reference to replicate for debug convenience

            File.WriteAllText(skyfile, text);

            var comparisonFileName = TestFilesDir.GetTestPath("expected.txt");
            AssertResult.GetResultsTextForComparison(doc0, text, comparisonFileName, out var maxExpectedHeight);
            LoadNewDocument(true); // Close the current document so we can delete the .skyd
            
            // Load a .sky with mising .skyd, forcing re-import with existing parameters
            var skydFile = skyfile+"d";
            File.Delete(skydFile);


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

            AssertResult.CompareResultsText(doc1, text, TestFilesDir.GetTestPath("actual.txt"), comparisonFileName, maxExpectedHeight);

            // Test isolation scheme import (combined mode only)
            if (!MsDataFileImpl.ForceUncombinedIonMobility)
            {
                var tranSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() => tranSettings.TabControlSel = TransitionSettingsUI.TABS.FullScan);
                var isoEditor = ShowDialog<EditIsolationSchemeDlg>(tranSettings.AddIsolationScheme);
                RunUI(() => isoEditor.UseResults = false);
                ValidateIsolationSchemeImport(isoEditor, "190314_TEN_175mingr_7-35_500nL_HeLa_diaPASEFdouble_py3_MSMS_Slot1-10_1_3426.d",
                    32, 25, null);
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
            // Remove reference to replicate in text of .sky file
            var open = text.IndexOf(string.Format("<replicate name=\"{0}\">", replicateName), StringComparison.Ordinal);
            var close = text.IndexOf("</replicate>", open, StringComparison.Ordinal) + 12;
            var snip = text.Substring(0, open) + text.Substring(close);
            while ((open = snip.IndexOf(replicateName, StringComparison.Ordinal)) != -1) // find precursor_peak, transition_peak
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
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns();
            var imPrecursor = 1.1732;
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, imPrecursor, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "TransitionResult.IonMobilityFragment", row, imPrecursor, msg); // Document is all precursors
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityFilter.IonMobilityUnitsL10NString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.12, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 473.2742, msg);

            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }
    }
}
