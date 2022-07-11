/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Waters IMS data as we work on various code optimizations.
    /// Also verifies proper operation of BiblioSpec with regards to reading pusher frequency
    /// from the raw files (this was actually broken when this test was written).
    /// </summary>
    [TestClass]
    public class ImportWatersIMSTest : AbstractFunctionalTest
    {

        [TestMethod] 
        public void WatersIMSImportTest()
        {
            Log.AddMemoryAppender();
            TestFilesZip = GetPerfTestDataURL(DateTime.Now.DayOfYear % 2 == 0
                ? @"PerfImportResultsWatersIMSv2.zip" // v2 has _func003.cdt file removed, to test our former assumption that lockmass would have IMS data if other functions did and vice verse
                : @"PerfImportResultsWatersIMS.zip");
            TestFilesPersistent = new[] { "ID12692_01_UCA168_3727_040714.raw", "ID12692_01_UCA168_3727_040714_IA_final_fragment.csv" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place

            MsDataFileImpl.PerfUtilFactory.IssueDummyPerfUtils = false; // Turn on performance measurement

            RunFunctionalTest();
            
            var logs = Log.GetMemoryAppendedLogEvents();
            var stats = PerfUtilFactory.SummarizeLogs(logs, TestFilesPersistent); // Show summary
            var log = new Log("Summary");
            if (TestFilesDirs != null)
                log.Info(stats.Replace(TestFilesDir.PersistentFilesDir, "")); // Remove tempfile info from log
        }

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }


        protected override void DoTest()
        {
            string skyfile = TestFilesDir.GetTestPath("Mix1_SkylineIMS_Test-reimport_RP50.sky");

            RunUI(() => SkylineWindow.OpenFile(skyfile));

                var doc0 = WaitForDocumentLoaded();
                AssertEx.IsDocumentState(doc0, null, 4, 218, 429, 3176);

                Stopwatch loadStopwatch = new Stopwatch();
                loadStopwatch.Start();
                // Launch import peptide search wizard
                var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            
                const string ID12692Base = "ID12692_01_UCA168_3727_040714";
                string ID12692Search = GetTestPath( ID12692Base + "_IA_final_fragment.csv");

                string[] searchFiles = { ID12692Search };
                var doc = SkylineWindow.Document;
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                    importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                    importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = true;
                });
                WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
                doc = WaitForDocumentChange(doc);

                // Verify document library was built
                string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(skyfile);
                string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
                Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
                var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                Assert.IsTrue(librarySettings.HasDocumentLibrary);

                // We're on the "Extract Chromatograms" page of the wizard.
                // All the files should be found, and we should
                // just be able to move to the next page.
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page));
                RunUI(() => importPeptideSearchDlg.ClickNextButton());

                // Skip Match Modifications page.
                RunUI(() =>
                {
                    AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.match_modifications_page, importPeptideSearchDlg.CurrentPage);
                    AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });

                // Make sure we're set up for ion mobility filtering - these settings should come from skyline file
                AssertEx.IsTrue(importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.IsUseSpectralLibraryIonMobilities);
                AssertEx.AreEqual(IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power, importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.WindowWidthType);
                AssertEx.AreEqual(50, importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.IonMobilityFilterResolvingPower);

                // Accept the full scan settings, lockmass correction dialog should appear
                var lockmassDlg = ShowDialog<ImportResultsLockMassDlg>(() => importPeptideSearchDlg.ClickNextButton()); 
                /* Lockmass correction for IMS data added to Waters DLL limitations Oct 2016, but this data does not need it
                RunUI(() =>
                {
                    var mz = 785.8426;  // Glu-Fib ESI 2+, per Will T
                    lockmassDlg.LockmassPositive = mz;
                    lockmassDlg.LockmassNegative = mz;
                    lockmassDlg.LockmassTolerance = 10.0;
                });
                */
                RunUI(lockmassDlg.OkDialog);
                WaitForClosedForm<ImportResultsLockMassDlg>();

                // Add FASTA also skipped because filter for document peptides was chosen.

                WaitForClosedForm(importPeptideSearchDlg);
                WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes

                var doc1 = WaitForDocumentLoaded(400000);
                AssertEx.IsDocumentState(doc1, null, 4, 63, 6, 42);  // Was 4, 63, 4, 30 before drift time based charge state detection was added to final_fragments reader

                loadStopwatch.Stop();
                DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

                float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
                double maxHeight = 0;
                var results = doc1.Settings.MeasuredResults;

                var numPeaks = new[] {10, 10, 10, 10, 10, 10, 10};
                int npIndex = 0;
                var errmsg = "";
                foreach (var pair in doc1.PeptidePrecursorPairs)
                {
                    ChromatogramGroupInfo[] chromGroupInfo;
                    Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup, tolerance,
                        out chromGroupInfo));

                    foreach (var chromGroup in chromGroupInfo)
                    {
                        if (numPeaks[npIndex] !=  chromGroup.NumPeaks)
                            errmsg += String.Format("unexpected peak count {0} instead of {1} in chromatogram {2}\r\n", chromGroup.NumPeaks, numPeaks[npIndex], npIndex);
                        npIndex++;
                        foreach (var tranInfo in chromGroup.TransitionPointSets)
                        {
                            maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                        }
                    }
                }
                Assert.IsTrue(errmsg.Length == 0, errmsg);
                Assert.AreEqual(3617.529, maxHeight, 1);
        }  
    }
}
