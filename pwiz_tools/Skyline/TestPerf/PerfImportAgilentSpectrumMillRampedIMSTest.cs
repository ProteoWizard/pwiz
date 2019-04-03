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
using System.Collections.Generic;
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
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify consistent import of Agilent IMS ramped CE data in concert with SpectrumMill.
    /// </summary>
    [TestClass]
    public class PerfImportAgilentSpectrumMillRampedIMSTest : AbstractFunctionalTestEx
    {
        private int _testCase;

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void AgilentSpectrumMillSpectralLibTest()
        {
            AgilentSpectrumMillTest(2);
        }

        [TestMethod]
        [Timeout(6000000)]  // Initial download can take a long time
        public void AgilentSpectrumMillRampedIMSImportTest()
        {
            AgilentSpectrumMillTest(1);
        }

        private void AgilentSpectrumMillTest(int testCase)
        {
            // RunPerfTests = true; // Uncomment this to force test to run in UI
            Log.AddMemoryAppender();
            _testCase = testCase;
            TestFilesZip = _testCase ==1 ? "https://skyline.gs.washington.edu/perftests/PerfImportAgilentSpectrumMillRampedIMS2.zip" :
                                           "https://skyline.gs.washington.edu/perftests/PerfImportAgilentSpectrumMillLibTest.zip";
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
            LibraryIonMobilityInfo driftInfoExplicitDT= null;
            Testit(true, ref driftInfoExplicitDT); // Read both CCS and DT
            if (_testCase == 1)
            {
                Testit(true, ref driftInfoExplicitDT); // Force conversion from CCS to DT, compare to previously read DT
                Testit(false, ref driftInfoExplicitDT); // Compare our ability to locate drift peaks, and derive CCS from those, with explicitly provided values
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
        }

        private void Testit(
            bool useDriftTimes, // If false, don't use any drift information in chromatogram extraction
            ref LibraryIonMobilityInfo driftInfoExplicitDT
            )
        {
            bool CCSonly = driftInfoExplicitDT != null;  // If true, force conversion from CCS to DT
            var ext = useDriftTimes ? (CCSonly ? "CCS" : "DT") : "train";
            string skyfile = TestFilesDir.GetTestPath("test_" + ext + ".sky");
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
                peptideSettingsUI.IsUseSpectralLibraryDriftTimes = useDriftTimes;
                peptideSettingsUI.SpectralLibraryDriftTimeResolvingPower = 50;
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

            // Launch import peptide search wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);
            var basename = _testCase==1 ? "40minG_WBP_wide_z2-3_mid_BSA_5pmol_01" : "09_BSAtrypticdigest_5uL_IMQTOF_AltFramesdtramp_dAJS009";
            var nextFile = _testCase == 1 ? null : "10_BSAtrypticdigest_5uL_IMQTOF_AltFramesdtramp_dAJS010.d";
            var searchResults = GetTestPath(basename+".pep.xml");

            var doc = SkylineWindow.Document;

            if (CCSonly || !useDriftTimes)
            {
                // Hide the drift time info provided by SpectrumMill, so we have to convert from CCS
                var mzxmlFile = searchResults.Replace("pep.xml", "mzXML");
                var fileContents = File.ReadAllText(mzxmlFile);
                fileContents = fileContents.Replace(" DT=", " xx="); 
                if (!useDriftTimes)
                    fileContents = fileContents.Replace(" CCS=", " xxx=");
                File.WriteAllText(mzxmlFile, fileContents);                    
            }

            var searchResultsList = new[] {searchResults};
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                                ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchResultsList);
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
            var doc1 = WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes

            if (_testCase == 1)
            {
                AssertEx.IsDocumentState(doc1, null, 1, 34, 45, 135);
            }
            else
            {
                AssertEx.IsDocumentState(doc1, null, 1, 36, 43, 129);
            }
            loadStopwatch.Stop();
            DebugLog.Info("load time = {0}", loadStopwatch.ElapsedMilliseconds);
            var errmsg = "";
            if (!useDriftTimes)
            {
                // Inspect the loaded data directly to derive DT and CCS
                // Verify ability to extract predictions from raw data
                var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                    () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));

                // Simulate user picking Edit Current from the Drift Time Predictor combo control
                var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.AddDriftTimePredictor);
                RunUI(() =>
                {
                    driftTimePredictorDlg.SetPredictorName("test");
                    driftTimePredictorDlg.SetResolvingPower(50);
                    driftTimePredictorDlg.GetDriftTimesFromResults();
                    driftTimePredictorDlg.OkDialog();
                });
                WaitForClosedForm(driftTimePredictorDlg);
                RunUI(() =>
                {
                    peptideSettingsDlg.OkDialog();
                });
                WaitForClosedForm(peptideSettingsDlg);

                var document = SkylineWindow.Document;
                var measuredDTs = document.Settings.PeptideSettings.Prediction.IonMobilityPredictor.MeasuredMobilityIons;
                Assert.IsNotNull(driftInfoExplicitDT, "driftInfoExplicitDT != null");
                var explicitDTs = driftInfoExplicitDT.GetIonMobilityDict();

                string errMsgAll = string.Empty;
                // A handful of peptides that really should have been trained on a clean sample
                // CONSIDER: or are they multiple conformers? They have multiple hits with distinct IM in the pepXML
                var expectedDiffs = LibKeyMap<double>.FromDictionary(new Dictionary<LibKey, double>
                {
                    {new PeptideLibraryKey("LC[+57.0]VLHEK", 2), 18.09  },
                    {new PeptideLibraryKey("EC[+57.0]C[+57.0]DKPLLEK", 3), 7.0},
                    {new PeptideLibraryKey("SHC[+57.0]IAEVEK", 3), 6.0},
                    {new PeptideLibraryKey("DDPHAC[+57.0]YSTVFDK", 2), 24.0}
                }).AsDictionary();
                foreach (var pair in doc1.PeptidePrecursorPairs)
                {
                    string errMsg = string.Empty;
                    var key = new LibKey(pair.NodePep.ModifiedSequence, pair.NodeGroup.PrecursorAdduct);
                    double tolerCCS = 5;
                    if (expectedDiffs.ContainsKey(key))
                    {
                        tolerCCS = expectedDiffs[key] + .1;
                    }
                    if (!explicitDTs.ContainsKey(key))
                    {
                        errMsg += "Could not locate explicit IMS info for " + key +"\n";
                    }
                    var given = explicitDTs[key][0];
                    var measured = measuredDTs[key];
                    var msg = CheckDeltaPct(given.CollisionalCrossSectionSqA ?? 0, measured.CollisionalCrossSectionSqA ?? 0, tolerCCS, "measured CCS", key.ToString());
                    if (!string.IsNullOrEmpty(msg))
                    {
                        errMsg += msg + CheckDeltaPct(given.IonMobility.Mobility.Value, measured.IonMobility.Mobility.Value, -1, "measured drift time", key.ToString());
                    }
                    else
                    {
                        errMsg += CheckDelta(given.IonMobility.Mobility.Value, measured.IonMobility.Mobility.Value, 10.0, "measured drift time", key.ToString());
                    }
                    errMsg += CheckDelta(given.HighEnergyIonMobilityValueOffset, measured.HighEnergyIonMobilityValueOffset, 2.0, "measured drift time high energy offset", key.ToString());
                    if (!string.IsNullOrEmpty(errMsg))
                        errMsgAll += "\n" + errMsg;
                }
                if (!string.IsNullOrEmpty(errMsgAll))
                    Assert.Fail(errMsgAll);
                return;
            }

            LibraryIonMobilityInfo libraryIonMobilityInfo;
            doc1.Settings.PeptideSettings.Libraries.Libraries.First().TryGetIonMobilityInfos(0, out libraryIonMobilityInfo);
            if (driftInfoExplicitDT == null)
            {
                driftInfoExplicitDT = libraryIonMobilityInfo;
            }
            else
            {
                var instrumentInfo = new DataFileInstrumentInfo(new MsDataFileImpl(GetTestPath(basename+".d")));
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
                            errmsg += "No driftinfo value found for " + libKey + "\n";
                        }
                        else
                        {
                            var ionMobilityInfo = infoValueExplicitDT[0];
                            var delta = Math.Abs(ionMobilityInfo.IonMobility.Mobility.Value -calculatedDriftTime.IonMobility.Mobility.Value);
                            var acceptableDelta = (libKey.Sequence.StartsWith("DDPHAC") || libKey.Sequence.EndsWith("VLHEK")) ? 3: 1; // These were ambiguous matches
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
            }

            float tolerance = (float)doc1.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            double maxHeight = 0;
            var results = doc1.Settings.MeasuredResults;
            var numPeaks = _testCase == 1 ?
                new[] {  8, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10,  9, 10, 7, 10, 10, 10, 10, 8, 10, 10, 10, 10, 10, 10,  9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 9, 10, 10, 8, 10, 10, 10, 10, 10 } :
                new[] { 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 8,  9, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10, 10 };

            int npIndex = 0;
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
            Assert.AreEqual(_testCase == 1 ? 2265204 : 1326442, maxHeight, 1);

            // Does CCS show up in reports?
            var expectedDtWindow = _testCase == 1 ? 0.74 : 0.94;
            TestReports(doc1, 0, expectedDtWindow);

            if (nextFile != null)
            {
                // Verify that we can use library generated for one file as the default for another without its own library
                ImportResults(nextFile);
                TestReports(SkylineWindow.Document, 1, expectedDtWindow);
            }

            // And verify roundtrip of ion mobility 
            AssertEx.RoundTrip(SkylineWindow.Document);
            RunUI(() =>
            {
                SkylineWindow.SaveDocument(skyfile);
                SkylineWindow.NewDocument(true);
                SkylineWindow.OpenFile(skyfile);
            });
            TestReports(SkylineWindow.Document, 1, expectedDtWindow);

            // Watch for problem with reimport after changed DT window
            var docResolvingPower = SkylineWindow.Document;
            var peptideSettingsUI2 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                peptideSettingsUI2.IsUseSpectralLibraryDriftTimes = useDriftTimes;
                peptideSettingsUI2.SpectralLibraryDriftTimeResolvingPower = 40;
            });
            OkDialog(peptideSettingsUI2, peptideSettingsUI2.OkDialog);
            var docReimport = WaitForDocumentChangeLoaded(docResolvingPower);
            // Reimport data for a replicate
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = docReimport.Settings.MeasuredResults.Chromatograms;
                dlg.SelectedChromatograms = new[] { chromatograms[0] };
                dlg.ReimportResults();
                dlg.OkDialog();
            });
            WaitForDocumentChangeLoaded(docReimport);
            var expectedDtWindow0 = _testCase == 2 ? 1.175 : 0.92;
            var expectedDtWindow1 = _testCase == 2 ? 0.94 : 0.92;
            TestReports(SkylineWindow.Document, 0, expectedDtWindow0, string.Format(" row {0} case {1} ccsOnly {2}", 0, _testCase, CCSonly));
            TestReports(SkylineWindow.Document, 1, expectedDtWindow1, string.Format(" row {0} case {1} ccsOnly {2}", 1, _testCase, CCSonly));

        }

        private void TestReports(SrmDocument doc1, int row, double expectedDtWindow, string msg = null)
        {
            // Verify reports working for CCS
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

            CheckFieldByName(documentGrid, "IonMobilityMS1", row, _testCase == 1 ? 18.43 : 23.50, msg);
            CheckFieldByName(documentGrid, "IonMobilityFragment", row, (double?)null, msg); // Document is all precursor
            CheckFieldByName(documentGrid, "IonMobilityUnits", row, IonMobilityValue.GetUnitsString(eIonMobilityUnits.drift_time_msec), msg);
            CheckFieldByName(documentGrid, "IonMobilityWindow", row, expectedDtWindow, msg);
            CheckFieldByName(documentGrid, "CollisionalCrossSection", row, _testCase == 1 ? 292.4 : 333.34, msg);
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
