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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Bruker PASEF in concert with Mascot.
    /// </summary>
    [TestClass]
    public class PerfImportBrukerPasefMascotTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void BrukerPasefMascotImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = GetPerfTestDataURL(@"PerfImportBrukerPasefMascot.zip");
            TestFilesPersistent = new[] { ".d", ".dat" }; // List of file basenames that we'd like to unzip alongside parent zipFile, and (re)use in place

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

        private string CheckDelta(double expected, double actual, double delta, string what, string key)
        {
            return (Math.Abs(actual - expected) > delta)
                ? what + " " + string.Format("{0:F02}", actual) + " differs from expected " + string.Format("{0:F02}", expected) + " by " + string.Format("{0:F02}", Math.Abs(actual - expected)) + " for " + key + "\n"
                : string.Empty;

        }

        private string CheckDeltaPct(double expected, double actual, double delta, string what, string key)
        {
            double pctDiff = (actual == 0) ? ((expected == 0) ? 0 : 100) : (100*Math.Abs(actual - expected)/actual);
            return (pctDiff > delta)
                ? what + " " + string.Format("{0:F02}", actual) + " differs from expected " + string.Format("{0:F02}", expected) + " by " + string.Format("{0:F02}", pctDiff) + "% for " + key + "\n"
                : string.Empty;

        }

        protected override void DoTest()
        {
            string skyfile = TestFilesDir.GetTestPath("test_pasef_mascot.sky");
            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                SkylineWindow.SaveDocument(skyfile);
            });



            Stopwatch loadStopwatch = new Stopwatch();
            loadStopwatch.Start();

            // Enable use of drift times in spectral library
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                transitionSettingsUI.IonMobilityControl.IsUseSpectralLibraryIonMobilities = true;
                transitionSettingsUI.IonMobilityControl.IonMobilityFilterResolvingPower = 50;
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            
            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            var nextFile = "FZGC A 100 ng_Slot1-46_01_440.d";
            var searchResults = GetTestPath("F264099.dat");

            var doc = SkylineWindow.Document;

            var searchResultsList = new[] {searchResults};
            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchResultsList);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);
            RunUI(() =>
            {
                importPeptideSearchDlg.BuildPepSearchLibControl.Grid.SetScoreThreshold(0.05);
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = false;
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            var ambiguousDlg = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck); // Expect the ambiguous matches dialog
            OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
            doc = WaitForDocumentChange(doc);

            // Verify document library was built
            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(skyfile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            AssertEx.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            AssertEx.IsTrue(librarySettings.HasDocumentLibrary);
            // We're on the "Extract Chromatograms" page of the wizard.
            // All the files should be found, and we should
            // just be able to move to the next page.
            RunUI(() => AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page));
            RunUI(() =>
            {
                var importResultsControl = (ImportResultsControl) importPeptideSearchDlg.ImportResultsControl;
                importResultsControl.ExcludeSpectrumSourceFiles = false;
                importResultsControl.UpdateResultsFiles(new []{TestFilesDirs[0].PersistentFilesDir}, true); // Go look in the persistent files dir
            });
            if (searchResultsList.Length > 1)
            {
                // Deal with the common name start dialog
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
                RunUI(() =>
                {
                    importResultsNameDlg.NoDialog();
                });
                WaitForClosedForm(importResultsNameDlg);
            }
            else
            {
                RunUI(() => importPeptideSearchDlg.ClickNextButtonNoCheck());
            }

            // Skip Match Modifications page.
            RunUI(() =>
            {
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.match_modifications_page, importPeptideSearchDlg.CurrentPage);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            RunUI(() => AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page));

            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new []{2,3,4,5});
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof);
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.SetUseSpectralLibraryIonMobilities(true));

            // Verify error handling in ion mobility control
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.SetResolvingPowerText("fish"));
            var errDlg = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(() => errDlg.OkDialog());

            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.IonMobilityFiltering.SetResolvingPower(40));

            RunUI(() => importPeptideSearchDlg.ClickNextButton()); // Accept the full scan settings
            // We're on the "Import FASTA" page of the wizard.
            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("human_and_yeast.fasta"));
            });
            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);

            WaitForClosedForm(importPeptideSearchDlg);
            var doc1 = WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes
            AssertEx.IsDocumentState(doc1, null, 7906, 28510, 28510, 85530);
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);
            var errmsg = "";
            
            LibraryIonMobilityInfo libraryIonMobilityInfo;
            doc1.Settings.PeptideSettings.Libraries.Libraries.First().TryGetIonMobilityInfos(doc1.MoleculeLibKeys.ToArray(), 0, out libraryIonMobilityInfo);
            var driftInfoExplicitDT = libraryIonMobilityInfo;
            var instrumentInfo = new DataFileInstrumentInfo(new MsDataFileImpl(GetTestPath(nextFile)));
            var dictExplicitDT = driftInfoExplicitDT.GetIonMobilityDict();
            foreach (var pep in doc1.Peptides)
            {
                foreach (var nodeGroup in pep.TransitionGroups)
                {
                    var calculatedDriftTime = doc1.Settings.GetIonMobilityFilter(
                        pep, nodeGroup, null, libraryIonMobilityInfo, instrumentInfo, 0).IonMobilityAndCCS;
                    var libKey = new LibKey(pep.ModifiedSequence, nodeGroup.PrecursorAdduct);
                    IonMobilityAndCCS[] infoValueExplicitDT;
                    if (!dictExplicitDT.TryGetValue(libKey, out infoValueExplicitDT))
                    {
                        errmsg += "No ionMobility value found for " + libKey + "\n";
                    }
                    else
                    {
                        var ionMobilityInfo = infoValueExplicitDT[0];
                        var delta = Math.Abs(ionMobilityInfo.IonMobility.Mobility.Value -calculatedDriftTime.IonMobility.Mobility.Value);
                        var acceptableDelta = 1;
                        if (delta > acceptableDelta)
                        {
                            errmsg += String.Format("calculated DT ({0}) and explicit DT ({1}, CCS={4}) do not agree (abs delta = {2}) for {3}\n",
                                calculatedDriftTime.IonMobility, ionMobilityInfo.IonMobility,
                                delta, libKey,
                                ionMobilityInfo.CollisionalCrossSectionSqA??0);
                        }
                    }
                }
            }
            
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
            AssertEx.IsTrue(errmsg.Length == 0, errmsg);
            AssertEx.AreEqual(1093421, maxHeight, 1);
            
            // Does CCS show up in reports?
            TestReports();
        }

        private void TestReports(string msg = null)
        {
            // Verify reports working for CCS
            var row = 0;
            var documentGrid = EnableDocumentGridIonMobilityResultsColumns();
            var imPrecursor = 0.832;
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityMS1", row, imPrecursor, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "TransitionResult.IonMobilityFragment", row, imPrecursor, msg); // Document is all precursor
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityUnits", row, IonMobilityFilter.IonMobilityUnitsL10NString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.IonMobilityWindow", row, 0.04, msg);
            CheckDocumentResultsGridFieldByName(documentGrid, "PrecursorResult.CollisionalCrossSection", row, 337.4821, msg);
            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

    }
}
