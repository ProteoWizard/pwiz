/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net >
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

//
// Test for Hardklor integration
// 
namespace TestPerf
{

    [TestClass]
    public class FeatureDetectionTest : AbstractFunctionalTestEx
    {
        public struct ExpectedResults
        {
            public int proteinCount;
            public int peptideCount;
            public int precursorCount;
            public int transitionCount;

            public ExpectedResults(int proteinCount, int peptideCount, int precursorCount, int transitionCount)
            {
                this.proteinCount = proteinCount;
                this.peptideCount = peptideCount;
                this.precursorCount = precursorCount;
                this.transitionCount = transitionCount;
            }
        }

        public struct DdaTestSettings
        {
            private SearchSettingsControl.SearchEngine _searchEngine;
            public SearchSettingsControl.SearchEngine SearchEngine
            {
                get => _searchEngine;
                set
                {
                    _searchEngine = value;
                    HasMissingDependencies = !SearchSettingsControl.HasRequiredFilesDownloaded(value);
                }
            }

            public string FragmentIons { get; set; }
            public string Ms2Analyzer { get; set; }
            public MzTolerance PrecursorTolerance { get; set; }
            public MzTolerance FragmentTolerance { get; set; }
            public List<KeyValuePair<string, string>> AdditionalSettings { get; set; }
            public ExpectedResults ExpectedResults { get; set; }
            public ExpectedResults ExpectedResultsFinal { get; set; }
            public bool HasMissingDependencies { get; private set; }
        }

        [TestMethod, NoUnicodeTesting(TestExclusionReason.HARDKLOR_UNICODE_ISSUES), NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestFeatureDetectionTutorialFuture()
        {
            TestFilesZipPaths = new[]
            {
                GetPerfTestDataURL(@"Label-free.zip"), 
                @"TestPerf\FeatureDetectionTest.zip"
            };
            TestFilesPersistent = new []{".RAW"}; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestDirectoryName = "HardklorFeatureDetectionTest";

            RunFunctionalTest();
        }

        private string[] _testFiles = new[]
        {
            "Orbi3_SA_IP_pHis3_01.RAW",
            //"Orbi3_SA_IP_pHis3_02.RAW",
            //"Orbi3_SA_IP_pHis3_03.RAW",
            "Orbi3_SA_IP_SMC1_01.RAW",
            //"Orbi3_SA_IP_SMC1_02.RAW",
            //"Orbi3_SA_IP_SMC1_03.RAW"
        };

        private string GetDataPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(path);
        }

        private string GetTestPath(string path)
        {
            return TestFilesDirs[1].GetTestPath(path);
        }

        private string[] SearchFiles
        {
            get
            {
                return _testFiles.Select(f => GetDataPath(f)).ToArray();
            }
        }

        private string[] SearchFilesSameName
        {
            get
            {
                return new[]
                {
                    SearchFiles[0],
                    Path.Combine(Path.GetDirectoryName(SearchFiles[0]) ?? string.Empty, "subdir", Path.GetFileName(SearchFiles[0]))
                };
            }
        }

        private const int SMALL_MOL_ONLY_PASS = 1;
        protected override void DoTest()
        {
            // First some low level tests
            AssertEx.AreEqual(1.0, ImportPeptideSearch.HardklorSettings.NormalizedContrastAngleFromCosineAngle(1.0));
            AssertEx.AreEqual(1.0,  ImportPeptideSearch.HardklorSettings.CosineAngleFromNormalizedContrastAngle(1.0));
            var from = 0.9;
            var to = ImportPeptideSearch.HardklorSettings.NormalizedContrastAngleFromCosineAngle(from);
            var backAgain = ImportPeptideSearch.HardklorSettings.CosineAngleFromNormalizedContrastAngle(to);
            AssertEx.AreEqual(from, backAgain);

            // IsPauseForScreenShots = true; // enable for quick demo
            for (int pass = 0; pass <= 1;)
            {
                PerformSearchTest(pass++);
            }
        }

        private void PerformSearchTest(int pass)
        {

            TidyBetweenPasses(pass); // For consistent audit log, remove any previous artifacts
            if (pass == 0)
            {
                // Make sure we're testing the mzML conversion
                foreach (var file in SearchFiles)
                {
                    AssertEx.IsTrue(File.Exists(file));
                    var convertedFile = Path.Combine(Path.GetDirectoryName(file) ?? string.Empty,
                        MsconvertDdaConverter.OUTPUT_SUBDIRECTORY, (Path.GetFileNameWithoutExtension(file)+ @".mzML"));
                    if (File.Exists(convertedFile))
                    {
                        File.Delete(convertedFile);
                    }
                }
            }

            var testingForCancelability = pass == 1;
            var expectedPeptideGroups = 1490;
            var expectedPeptides = 11510;
            var expectedPeptideTransitionGroups = 13456;
            var expectedPeptideTransitions = 40368;
            if (pass != SMALL_MOL_ONLY_PASS) 
            {
                // Load a data set that was processed by MaxQuant
                RunUI(() => SkylineWindow.OpenFile(GetDataPath("Label-free.sky")));
                AssertEx.IsDocumentState(SkylineWindow.Document, null, expectedPeptideGroups, expectedPeptides, expectedPeptideTransitionGroups, expectedPeptideTransitions);
            }
            else
            {
                // Start with an empty document
                IsPauseForScreenShots = false;
                RunUI(() =>
                {
                    SkylineWindow.NewDocument(true);
                });
            }

            RunUI(() =>
            {
                SkylineWindow.SaveDocument(GetTestPath($"Pass{pass}.sky"));
                WaitForDocumentLoaded();
            });

            var documentGrid = FindOpenForm<DocumentGridForm>() ?? ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() => documentGrid.ChooseView(pwiz.Skyline.Properties.Resources.ReportSpecList_GetDefaults_Transition_Results));
            EnableDocumentGridColumns(documentGrid,
                pwiz.Skyline.Properties.Resources.ReportSpecList_GetDefaults_Transition_Results, null);
            PauseForScreenShot("Ready to start Wizard (File > Import > Feature Detection...)");
            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowFeatureDetectionDlg);
            importPeptideSearchDlg.IsAutomatedTest = true; // Prevents form-called-by-form blockage TODO(bspratt) there must be a cleaner way

            // We're on the "Select Files to Search" page of the wizard.

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
                AssertEx.AreEqual(ImportPeptideSearchDlg.Workflow.feature_detection, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType); 
            });
            PauseForScreenShot("these are MaxQuant label free data set files");
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            RunUI(() => importResultsNameDlg.Suffix = string.Empty);
            PauseForScreenShot<ImportResultsNameDlg>("Common prefix form");
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            if (testingForCancelability)
            {
                // Test back/next buttons
                PauseForScreenShot("Testing back button");
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                });
                PauseForScreenShot("and forward again");
                importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                RunUI(() => importResultsNameDlg.Suffix = string.Empty);
                PauseForScreenShot<ImportResultsNameDlg>("Common prefix form again");
                OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
            }

            RunUI(() =>
            {
                // We're on the MS1 full scan settings page.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20;
                importPeptideSearchDlg.FullScanSettingsControl.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, testingForCancelability ? 3 : 5);
            });
            PauseForScreenShot(" MS1 full scan settings page - next we'll tweak the search settings");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                importPeptideSearchDlg.SearchSettingsControl.HardklorMinIdotP = 0.98; // Default is 0.9, so this should be a change
                importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01; // Default is 3.0, so this should be a change
                importPeptideSearchDlg.SearchSettingsControl.HardklorMinIntensityPPM = 12.37; // Just a random value
                // The instrument values should be settable since we set "centroided" in Full Scan.
                AssertEx.IsTrue(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);
                importPeptideSearchDlg.SearchSettingsControl.HardklorInstrument = FullScanMassAnalyzerType.tof;
                importPeptideSearchDlg.SearchSettingsControl.HardklorResolution = testingForCancelability ? 60000 : 10000; // 10000 per MS1 filtering tutorial
            });
            if (!testingForCancelability)
            {
                PauseForScreenShot(" Search settings page");
            }
            bool? searchSucceeded = null;
            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            if (testingForCancelability)
            {
                PauseForScreenShot(" Search settings page - next we'll start the mzML conversion if needed then cancel the search");
                RunUI(() =>
                {
                    // Run the search
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });
                TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing

                // Wait for the mzML conversion to complete before canceling
                foreach (var searchFile in SearchFiles)
                {
                    var converted = Path.Combine(Path.GetDirectoryName(searchFile) ?? string.Empty,
                        @"converted",
                        Path.ChangeExtension(Path.GetFileName(searchFile), @"mzML"));
                    WaitForCondition(() => File.Exists(converted));
                }

                RunUI(() =>
                {
                    // Cancel search
                    importPeptideSearchDlg.SearchControl.Cancel();
                });
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);
                Assert.IsFalse(searchSucceeded.Value);
                searchSucceeded = null;
                PauseForScreenShot("search cancelled, now go back and  test 2 input files with the same name in different directories");

                // Go back and test 2 input files with the same name in different directories
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                    importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFilesSameName.Select(o => (MsDataFileUri) new MsDataFilePath(o)).ToArray();
                });
                PauseForScreenShot("same name, different directories");
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                RunUI(() => removeSuffix.Suffix = string.Empty);
                PauseForScreenShot("expected dialog for name reduction - we'll cancel and go back to try unique names");
                OkDialog(removeSuffix, removeSuffix.CancelDialog);
            
                // Test with 2 files
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
                });

                // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
                var removeSuffix2 = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                RunUI(() => removeSuffix2.Suffix = string.Empty);
                PauseForScreenShot("expected dialog for name reduction ");
                OkDialog(removeSuffix2, () => removeSuffix2.YesDialog());
                
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;  // Per MS1 filtering tutorial
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 10000; // Per MS1 filtering tutorial
                    importPeptideSearchDlg.FullScanSettingsControl.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 5);
                });
                PauseForScreenShot("Full scan settings - not set Centroided, so instrument settings on next page should not be operable");
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                    AssertEx.IsFalse(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                });
                RunUI(() =>
                {
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20;
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });
            } // End if testing cancelability

            PauseForScreenShot("Search Settings page -Full scan settings are set Centroided, so instrument setting should be operable");
            RunUI(() =>
            {
                // We're on the "Search Settings" page. These values should be settable since we set "Centroided" in Full Scan.
                AssertEx.IsTrue(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);
            });
            // Now check some value ranges
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIdotP = -1);
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIdotP = 1.1);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIdotP = .9); // Legal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = -1); // Illegal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 11);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01); // Legal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIntensityPPM = -1); // Illegal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIntensityPPM = 200); // Legal - this is ppm, not pct
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIntensityPPM = 2.0E6); // Illegal
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorMinIntensityPPM = 5.0); // Legal

            void ExpectError(Action act)
            {
                RunUI(() =>
                {                    
                    var page = importPeptideSearchDlg.CurrentPage;
                    act();
                    importPeptideSearchDlg.ClickNextButton();
                    AssertEx.AreEqual(page, importPeptideSearchDlg.CurrentPage); // Not expected to advance
                });
            }

            RunUI(() =>
            {
                // Start the search
                var page = importPeptideSearchDlg.CurrentPage;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.AreNotEqual(page, importPeptideSearchDlg.CurrentPage, "stuck?"); // Expected to advance
            });

            try
            {
                WaitForConditionUI(15 * 60000, () => searchSucceeded.HasValue);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", importPeptideSearchDlg.SearchControl.LogText);
            }

            RunUI(() =>
            {
                // Click the "Finish" button.
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
            WaitForGraphs();
            RunUI(() => SkylineWindow.ArrangeGraphs(DisplayGraphsType.Row));
            WaitForGraphs();

            // IsPauseForScreenShots = true; // enable for quick demo
            PauseForScreenShot("complete");

            var doc = SkylineWindow.Document;
            var expectedFeaturesMolecules = 6583;
            var expectedFeaturesTransitionGroups = 8522;
            var expectedFeaturesTransitions = 25566;
            var actualFeaturesMolecules = doc.CustomMolecules.Count();
            var actualFeaturesTransitionGroups = doc.MoleculeTransitionGroupCount - doc.PeptideTransitionGroupCount;
            var actualFeaturesTransitions = doc.MoleculeTransitionCount - doc.PeptideTransitionCount;
            AssertEx.AreEqual(expectedFeaturesMolecules, actualFeaturesMolecules);
            AssertEx.AreEqual(expectedFeaturesTransitionGroups, actualFeaturesTransitionGroups);
            AssertEx.AreEqual(expectedFeaturesTransitions, actualFeaturesTransitions);

            // Verify use of library RT in chromatogram extraction
            var tg = doc.MoleculeTransitions.First(t => t.Transition.Group.IsCustomIon);
            var r = tg.Results.First().First();
            var expectedRT = 19.76576;
            var expectedStartRT = 19.43576;
            var expectedEndRT = 19.99676;
            AssertEx.AreEqual(expectedRT, r.RetentionTime, .01);
            AssertEx.AreEqual(expectedStartRT, r.StartRetentionTime, .01);
            AssertEx.AreEqual(expectedEndRT, r.EndRetentionTime, .01);

            // See if Hardklor output is stable
            var expectedHardklorFiles = @"expected_hardklor_files";
            var expectedFilesPath = GetTestPath(expectedHardklorFiles);
            foreach (var hkExpectedFilePath in Directory.EnumerateFiles(expectedFilesPath))
            {
                var hkActualFilePath = hkExpectedFilePath.Replace(expectedHardklorFiles, Path.Combine(expectedHardklorFiles, @".."));
                var columnTolerances = new Dictionary<int, double>() { { -1, .00015 } }; // Allow a little rounding wiggle in the decimal values
                AssertEx.FileEquals(hkExpectedFilePath, hkActualFilePath, columnTolerances, true);
            }

            if (pass == SMALL_MOL_ONLY_PASS)
            {
                AssertEx.IsDocumentState(doc, null, 1, expectedFeaturesMolecules, expectedFeaturesTransitionGroups, expectedFeaturesTransitions);
            }
            else
            {
                AssertEx.IsDocumentState(doc, null, expectedPeptideGroups + 1, expectedPeptides + expectedFeaturesMolecules, 
                    expectedPeptideTransitionGroups + expectedFeaturesTransitionGroups, expectedPeptideTransitions + expectedFeaturesTransitions);

                /* TODO update this for current test data set
                // Verify that we found every known peptide
                var colName = FindDocumentGridColumn(documentGrid, "Precursor.Peptide").Index;
                var colReplicate = FindDocumentGridColumn(documentGrid, "Results!*.Value.PrecursorResult.PeptideResult.ResultFile.Replicate").Index;
                var colZ = FindDocumentGridColumn(documentGrid, "Precursor.Charge").Index;
                var colMZ = FindDocumentGridColumn(documentGrid, "Precursor.Mz").Index;
                var colFragMZ = FindDocumentGridColumn(documentGrid,"ProductMz").Index;
                var colRT = FindDocumentGridColumn(documentGrid, "Results!*.Value.RetentionTime").Index;
                var colArea = FindDocumentGridColumn(documentGrid, "Results!*.Value.Area").Index;
                var colPeakRank = FindDocumentGridColumn(documentGrid, "Results!*.Value.PeakRank").Index;
                var hits = new HashSet<Hit>();
                var rowCount = documentGrid.DataGridView.RowCount;
                for (var row = 0; row < rowCount; row++)
                {
                    RunUI(() =>
                    {
                        var line = documentGrid.DataGridView.Rows[row];
                        var mzValue = (line.Cells[colMZ].Value as double?) ?? 0;
                        var peakRank = (line.Cells[colPeakRank].Value as int?) ?? 0;
                        var peakArea = (line.Cells[colArea].Value as double?) ?? 0;
                        if (peakRank == 1 && peakArea > 0) // Just get the base peak entry
                        {
                            var hit = new Hit()
                            {
                                name = line.Cells[colName].Value.ToString(),
                                replicate = line.Cells[colReplicate].Value.ToString(),
                                z = (line.Cells[colZ].Value as int?) ?? 0,
                                mz = mzValue,
                                rt = (line.Cells[colRT].Value as double?) ?? 0,
                                area = peakArea
                            };
                            hits.Add(hit);
                        }
                    });
                }

                var matched = new HashSet<Hit>();
                var hitsP = ReduceToBestHits(hits.Where(h => !h.name.StartsWith("mass")).ToList());
                var smallestHitP = hitsP.Min(h => h.area);
                var hitsPSummed = hitsP.Sum(h => h.area);
                var hitsM = ReduceToBestHits(hits.Where(h => h.name.StartsWith("mass")).ToList());
                var smallestHitM = hitsM.Min(h => h.area);
                var hitsMSummed = hitsM.Sum(h => h.area);
                var threshPepPPM = 1.0E6 * smallestHitP / hitsPSummed;
                var threshMolPPM = 1.0E6 * smallestHitM / hitsMSummed;
                foreach (var hitP in hitsP)
                {
                    foreach (var hitM in hitsM)
                    {
                        if ((hitP.z == hitM.z) && (Math.Abs(hitP.mz - hitM.mz) < .1) && (Math.Abs(hitM.rt - hitM.rt) < 1))
                        {
                            matched.Add(hitP);
                            break;
                        }
                    }
                }

                var unmatched = hitsP.Select(h => h.name).Distinct().Where(n => !matched.Any(h => Equals(n, h.name))).ToArray();

                if (unmatched.Any())
                {
                    var msg = "No feature detected to match known peptide(s):\n"+
                              string.Join("\n", unmatched);
                    PauseForScreenShot(msg);
                }

                // Hardklor+Bullseye simply misses some peptides that search engine found
                // For the most part these are down in the grass, so maybe to be expected
                // CONSIDER:(bspratt) debug Hardklor+Bullseye code?
                var expectedMisses = new(string, int)[] // (peptide, charge)
                {
                    // TODO update this for current test data set
                };

                var threshold = hits.Select(h => h.area).Max() * .1;
                var missedHits = hits.Where(h =>
                    !expectedMisses.Any(miss => Equals(h.name, miss.Item1) && Equals(h.z, miss.Item2) && h.area >= threshold)).ToArray();
                AssertEx.IsFalse(missedHits.Any(),
                $"Hardklor did not find features for fairly strong peptides\n{string.Join("\n", misses.Select(u => u.ToString()))}");

                var unexpectedMisses = unmatched.Where(um => !expectedMisses.Contains((um.name, um.z))).ToArray();
                var unexpectedMatches = matched.Where(um => expectedMisses.Contains((um.name, um.z))).ToArray();
                 AssertEx.IsFalse(unexpectedMisses.Any(),
                    $"Expected to find features for peptides\n{string.Join("\n", unexpectedMisses.Select(u => u.ToString()))}");
                AssertEx.IsFalse(unexpectedMatches.Any(),
                $"Did not expected to find features for peptides\n{string.Join("\n", unexpectedMisses.Select(u => u.ToString()))}");
                */
            }

        }

        /* TODO uncomment for hit check
        private HashSet<Hit> ReduceToBestHits(List<Hit> hitSet)
        {
            var bestHits = new HashSet<Hit>();
            var candidates = hitSet.Select(h => (h.name, h.z)).Distinct();
            foreach (var candidate in candidates)
            {
                var sibs = hitSet.Where(h => Equals(candidate, (h.name, h.z))).ToArray();
                if (sibs.Length > 1)
                {
                    var best = sibs[0];
                    foreach (var sib in sibs)
                    {
                        if (sib.area > best.area)
                        {
                            best = sib;
                        }
                    }

                    bestHits.Add(best);
                }
            }

            return bestHits;
        }

        private class Hit : IEquatable<Hit>
        {
            public string name;
            public string replicate;
            public int z;
            public double mz;
            public double rt;
            public double area; // Close as we get to "summed intensity", should be proportional at least?

            public override string ToString()
            {
                return $"{name} {replicate} mz={mz} z={z} RT={rt} a={area}";
            }

            public bool Equals(Hit other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return name == other.name && replicate == other.replicate && z == other.z && mz.Equals(other.mz) && rt.Equals(other.rt);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((Hit)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (name != null ? name.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ z;
                    hashCode = (hashCode * 397) ^ mz.GetHashCode();
                    hashCode = (hashCode * 397) ^ rt.GetHashCode();
                    return hashCode;
                }
            }
        }
        */

        private void TidyBetweenPasses(int pass)
        {
            // In the test loop there are lots of intentional starts and cancels, and multiple passes, which causes file renames,
            // so restore test directory to initial state for stable audit log creation for test purposes
            var preservePass0 = GetTestPath("pass0");
            if (pass == 1)
            {
                // Preserve pass 0 result for possible comparison
                DirectoryEx.SafeDelete(preservePass0);
                Directory.CreateDirectory(preservePass0);
            }

            foreach (var ext in new[]{"*.kro", "*.hk", "*.ms2", "*.conf", "*.unaligned"})
            {
                foreach (var f in new DirectoryInfo(GetTestPath(string.Empty)).EnumerateFiles(ext))
                {
                    if (pass == 0)
                    {
                        FileEx.SafeDelete(f.FullName);
                    }
                    else
                    {
                        File.Move(f.FullName, Path.Combine(preservePass0, f.Name));
                    }
                }
            }
        }
    }
}
