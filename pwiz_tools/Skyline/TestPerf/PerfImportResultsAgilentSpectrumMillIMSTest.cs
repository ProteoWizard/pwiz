/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using pwiz.Skyline.Alerts;
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
    /// Verify consistent import of Agilent IMS data in concert with SpectrumMill.
    /// </summary>
    [TestClass]
    public class ImportAgilentSpectrumMillIMSTest : AbstractFunctionalTest
    {

        [TestMethod]
        public void AgilentSpectrumMillIMSImportTest()
        {
            // RunPerfTests = true;  // Uncomment to force this to run in UI
            Log.AddMemoryAppender();
            TestFilesZip = GetPerfTestDataURL(@"PerfImportAgilentSpectrumMillIMS.zip");
            TestFilesPersistent = new[] { "40minG_WBP_wide_z2-3" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

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
            string skyfile = TestFilesDir.GetTestPath("test.sky");
            RunUI(() => SkylineWindow.SaveDocument(skyfile));

            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();

            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            string lo = GetTestPath("40minG_WBP_wide_z2-3_low_BSA_5pmol_02.pep.xml");
            string mid = GetTestPath("40minG_WBP_wide_z2-3_mid_BSA_5pmol_01.pep.xml");
            string up = GetTestPath("40minG_WBP_wide_z2-3_up_BSA_5pmol_02.pep.xml");

            string[] searchFiles = { lo, mid, up };
            var doc = SkylineWindow.Document;

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = false;
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
            RunUI(() =>
            {
                var importResultsControl = (ImportResultsControl) importPeptideSearchDlg.ImportResultsControl;
                importResultsControl.ExcludeSpectrumSourceFiles = true;
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(() =>
            {
                importResultsNameDlg.NoDialog();
            });
            WaitForClosedForm(importResultsNameDlg);
            // Skip Match Modifications page.
            RunUI(() =>
            {
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.match_modifications_page, importPeptideSearchDlg.CurrentPage);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new []{2,3,4,5});
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof);
            // Enable use of drift times in spectral library
            Assume.IsTrue(importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.Visible);
            Assume.IsTrue(importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.Enabled);
            var useDriftTimes = true; // For debugging convenience, if you want to see how this works without IM filtering
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.IsUseSpectralLibraryIonMobilities = useDriftTimes);
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.IonMobilityFilterResolvingPower = 50);
            RunUI(() => importPeptideSearchDlg.ClickNextButton()); // Accept the full scan settings

            // We're on the "Import FASTA" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("SwissProt.bsa-mature"));
            });
            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            WaitForClosedForm(importPeptideSearchDlg);
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            var doc1 = WaitForDocumentLoaded(400000);

            AssertEx.IsDocumentState(doc1, null, 1, 40, 54, 162);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;

            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, out chromGroupInfo));

                foreach (var chromGroup in chromGroupInfo)
                {
                    foreach (var tranInfo in chromGroup.TransitionPointSets)
                    {
                        maxHeight = Math.Max(maxHeight, tranInfo.MaxIntensity);
                    }
                }
            }
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            Assert.AreEqual(useDriftTimes ? 972186 : 1643104 , maxHeight, 1);
        }  
    }
}
