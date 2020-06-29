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
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
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
    public class PerfImportBrukerPasefMascotTest : AbstractFunctionalTestEx
    {

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void BrukerPasefMascotImportTest()
        {
            // RunPerfTests = true; // Uncomment this to force test to run in IDE
            Log.AddMemoryAppender();
            TestFilesZip = "https://skyline.gs.washington.edu/perftests/PerfImportBrukerPasefMascot.zip";
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
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                peptideSettingsUI.IsUseSpectralLibraryDriftTimes = true;
                peptideSettingsUI.SpectralLibraryDriftTimeResolvingPower = 50;
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            var nextFile = "FZGC A 100 ng_Slot1-46_01_440.d";
            var searchResults = GetTestPath("F264099.dat");

            var doc = SkylineWindow.Document;

            var searchResultsList = new[] {searchResults};
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                                ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchResultsList);
                importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore = 0.95;
                importPeptideSearchDlg.BuildPepSearchLibControl.FilterForDocumentPeptides = false;
            });
            var ambiguousDlg = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck); // Expect the ambiguous matches dialog
            OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
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
            // Modifications are already set up, so that page should get skipped.
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new []{2,3,4,5});
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof);
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.UseSpectralLibraryIonMobilityValuesControl.SetUseSpectralLibraryDriftTimes(true));

            // Verify error handling in ion mobility control
            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.UseSpectralLibraryIonMobilityValuesControl.SetResolvingPowerText("fish"));
            var errDlg = ShowDialog<MessageDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            RunUI(() => errDlg.OkDialog());

            RunUI(() => importPeptideSearchDlg.FullScanSettingsControl.UseSpectralLibraryIonMobilityValuesControl.SetResolvingPower(40));

            RunUI(() => importPeptideSearchDlg.ClickNextButton()); // Accept the full scan settings
            // We're on the "Import FASTA" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
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
                    double windowDT;
                    var calculatedDriftTime = doc1.Settings.GetIonMobility(
                        pep, nodeGroup, null, libraryIonMobilityInfo, instrumentInfo, 0, out windowDT);
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
            Assert.IsTrue(errmsg.Length == 0, errmsg);
            Assert.AreEqual(1093421, maxHeight, 1); 

            // Does CCS show up in reports?
            TestReports(doc1);
        }

        private void TestReports(SrmDocument doc1, string msg = null)
        {
            // Verify reports working for CCS
            var row = 0;
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            EnableDocumentGridColumns(documentGrid,
                Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List,
                doc1.PeptideTransitionCount * doc1.MeasuredResults.Chromatograms.Count,
                new[]
                {
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.CollisionalCrossSection",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityMS1",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityFragment",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityUnits",
                    "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.IonMobilityWindow"
                });
            CheckFieldByName(documentGrid, "IonMobilityMS1", row, 0.832, msg);
            CheckFieldByName(documentGrid, "IonMobilityFragment", row, (double?)null, msg); // Document is all precursor
            CheckFieldByName(documentGrid, "IonMobilityUnits", row, IonMobilityFilter.IonMobilityUnitsL10NString(eIonMobilityUnits.inverse_K0_Vsec_per_cm2), msg);
            CheckFieldByName(documentGrid, "IonMobilityWindow", row, 0.04, msg);
            CheckFieldByName(documentGrid, "CollisionalCrossSection", row, 337.4821, msg);
            // And clean up after ourselves
            RunUI(() => documentGrid.Close());
        }

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, double? expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value.PrecursorResult." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as double?;
                Assert.AreEqual(expected.HasValue, val.HasValue, name + (msg ?? string.Empty));
                Assert.AreEqual(expected ?? 0, val ?? 0, 0.005, name + (msg ?? string.Empty));
            });
        }

        private void CheckFieldByName(DocumentGridForm documentGrid, string name, int row, string expected, string msg = null)
        {
            var col = FindDocumentGridColumn(documentGrid, "Results!*.Value.PrecursorResult." + name);
            RunUI(() =>
            {
                // By checking the 1th row we check both the single file and two file cases
                var val = documentGrid.DataGridView.Rows[row].Cells[col.Index].Value as string;
                Assert.AreEqual(expected, val, name + (msg ?? string.Empty));
            });
        }
    }
}
