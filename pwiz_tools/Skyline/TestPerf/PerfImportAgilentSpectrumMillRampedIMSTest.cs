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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Agilent IMS ramped CE data in concert with SpectrumMill.
    /// </summary>
    [TestClass]
    public class PerfImportAgilentSpectrumMillRampedIMSTest : AbstractFunctionalTest
    {

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void AgilentSpectrumMillRampedIMSImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in UI
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportAgilentSpectrumMillRampedIMS.zip";
            TestFilesPersistent = new[] { ".d" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

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
            bool useDriftTimes = true; // false;  // If false, don't use any drift information in chromatogram extraction
            bool CCSonly = false; // If true, force conversion from CCS to DT
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            Testit(useDriftTimes, CCSonly);
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
        }

        private void Testit(
            bool useDriftTimes, // If false, don't use any drift information in chromatogram extraction
            bool CCSonly // If true, force conversion from CCS to DT
            )
        {
            string skyfile = TestFilesDir.GetTestPath("test.sky");
            RunUI(() => SkylineWindow.SaveDocument(skyfile));



            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();

            // Enable use of drift times in spectral library
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                peptideSettingsUI.IsUseSpectralLibraryDriftTimes = useDriftTimes;
                peptideSettingsUI.SpectralLibraryDriftTimeResolvingPower = 50;
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            string one = GetTestPath("40mingradient_IMS_AllIons_ramped_01.pep.xml");
            string two = GetTestPath("40mingradient_IMS_AllIons_ramped_02.pep.xml");

            string[] searchFiles = { one,two };
            var doc = SkylineWindow.Document;

            if (CCSonly)
            {
                // Hide the drift time info provided by SpectrumMill, so we have to convert from CCS
                foreach (var file in searchFiles)
                {
                    var mzxmlFile = file.Replace("pep.xml", "mzXML");
                    var fileContents = File.ReadAllText(mzxmlFile);
                    fileContents = fileContents.Replace(" DT=", " xx="); 
                    File.WriteAllText(mzxmlFile, fileContents);                    
                }
            }


            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                                ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore = 0.95;
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = false;
            });

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
                importResultsControl.UpdateResultsFiles(new []{TestFilesDirs[0].PersistentFilesDir}, true); // Go look in the persistent files dir
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(() =>
            {
                importResultsNameDlg.NoDialog();
            });
            WaitForClosedForm(importResultsNameDlg);
            // Modifications are already set up, so that page should get skipped.
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new []{2,3,4,5});
            RunUI(() => importPeptideSearchDlg.ClickNextButton()); // Accept the full scan settings

            // We're on the "Import FASTA" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("SwissProt.bsa-mature"));
                importPeptideSearchDlg.ClickNextButton();
            });
            WaitForClosedForm(importPeptideSearchDlg);
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            var doc1 = WaitForDocumentLoaded(400000);

            AssertEx.IsDocumentState(doc1, null, 1, 40, 48, 144);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);

            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;

            var numPeaks = useDriftTimes ?
                new[] { 8, 8, 10, 10, 10, 10, 10, 10, 10, 10, 9, 10, 10, 4, 7, 10, 10, 10, 10, 10, 10, 4, 10, 10, 10, 10, 9, 6, 10, 10, 10, 10, 10, 5, 10, 10, 10, 10, 10, 4, 10, 10, 10, 10, 8, 10, 10, 10 } :
                new[] { 10, 10, 10, 10, 9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 9, 10 };
            int npIndex = 0;
            var errmsg = "";
            foreach (var pair in doc1.PeptidePrecursorPairs)
            {
                ChromatogramGroupInfo[] chromGroupInfo;
                Assert.IsTrue(results.TryLoadChromatogram(0, pair.NodePep, pair.NodeGroup,
                    tolerance, true, out chromGroupInfo));

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
            Assert.AreEqual(useDriftTimes ? 4209178 : 4912494, maxHeight, 1);
        }  
    }
}
