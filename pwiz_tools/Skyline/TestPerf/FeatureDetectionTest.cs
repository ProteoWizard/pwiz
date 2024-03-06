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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
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

        public bool UseWiff => false; // ExtensionTestContext.CanImportAbWiff; // Wiff reader fails in msconvert step due to brittle embedded DLL load order when run in RunProcess

        [TestMethod, NoUnicodeTesting(TestExclusionReason.HARDKLOR_UNICODE_ISSUES), NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestHardklorFeatureDetection()
        {
            TestFilesZipPaths = new[]
            {
                UseWiff
                    ? @"https://skyline.ms/tutorials/MS1Filtering_2.zip" 
                    : @"https://skyline.ms/tutorials/MS1FilteringMzml_2.zip", 
                @"TestPerf\FeatureDetectionTest.zip"
            };
            TestDirectoryName = "HardklorFeatureDetectionTest";

            RunFunctionalTest();
        }

        private string GetDataPath(string path)
        {
            var folderMs1Filtering = UseWiff ? "Ms1Filtering" : "Ms1FilteringMzml";
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderMs1Filtering, path));
        }

        private string GetTestPath(string path)
        {
            return TestFilesDirs[1].GetTestPath(path);
        }

        private string[] SearchFiles
        {
            get
            {
                var ext = UseWiff ? ".wiff" : ".mzML";
                return new[]
                {
                    GetDataPath("100803_0001_MCF7_TiB_L")+ext,
                    GetDataPath("100803_0005b_MCF7_TiTip3")+ext
                };
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
            // IsPauseForScreenShots = true; // enable for quick demo
            for (int pass = 0; pass <= SMALL_MOL_ONLY_PASS;)
            {
                PerformSearchTest(pass++);
            }
            VerifyAuditLog();
        }

        private void PerformSearchTest(int pass)
        {

            TidyBetweenPasses(); // For consistent audit log, remove any previous artifacts
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

            if (pass < SMALL_MOL_ONLY_PASS) 
            {
                // Load the document that we have at the end of the MS1 fullscan tutorial
                RunUI(() => SkylineWindow.OpenFile(GetTestPath("Ms1FilteringTutorial-2min.sky")));
            }
            else
            {
                // Start with an empty document this time
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
            PauseForScreenShot("these are the MS1 Filtering Tutorial files");
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot<ImportResultsNameDlg>("Common prefix form");

            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            // Test back/next buttons
            PauseForScreenShot("Testing back button");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
            });
            PauseForScreenShot("and forward again");
            importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            PauseForScreenShot<ImportResultsNameDlg>("Common prefix form again");
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            RunUI(() =>
            {
                // We're on the MS1 full scan settings page.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof; // Per MS1 filtering tutorial
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 10000; // Per MS1 filtering tutorial
            });
            PauseForScreenShot(" MS1 full scan settings page - next we'll tweak the search settings");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = 0.98; // Default is 0.95, so this should be a change
                importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01; // Default is 3.0, so this should be a change
                // The instrument values should be settable since we set them in Full Scan.
                AssertEx.IsFalse(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);

            });
            PauseForScreenShot(" Search settings page - next we'll start the mzML conversion if needed then cancel the search");
            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });
            bool? searchSucceeded = null;
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing

            if (UseWiff)
            {
                // Wait for the mzML conversion to complete before canceling
                foreach (var searchFile in SearchFiles)
                {
                    var converted = Path.Combine(Path.GetDirectoryName(searchFile) ?? string.Empty,
                        @"converted",
                        Path.ChangeExtension(Path.GetFileName(searchFile), @"mzML"));
                    WaitForCondition(() => File.Exists(converted));
                }
            }

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;

                // Cancel search
                importPeptideSearchDlg.SearchControl.Cancel();
            });
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsFalse(searchSucceeded.Value);
            searchSucceeded = null;
            PauseForScreenShot("search cancelled, now go back and  test 2 input files with the same name in different directories");
            TidyBetweenPasses(); // For consistent audit log, remove any previous artifacts

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
            PauseForScreenShot("expected dialog for name reduction ");
            OkDialog(removeSuffix, () => removeSuffix2.YesDialog());

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.tof;  // Per MS1 filtering tutorial
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 10000; // Per MS1 filtering tutorial
            });
            PauseForScreenShot("Full scan settings - not set Centroided (this data set isn't compatible with that), so instrument settings on next page should not be operable");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            RunUI(() =>
            {
                // We're on the "Search Settings" page. These values should not be settable since we did not set "Centroided" in Full Scan.
                AssertEx.IsFalse(importPeptideSearchDlg.SearchSettingsControl.HardklorInstrumentSettingsAreEditable);
            });
            // Now check some value ranges
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = -1);
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = 1.1);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorCorrelationThreshold = .98); // Legal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = -1); // Illegal
            ExpectError(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 11);
            RunUI(() => importPeptideSearchDlg.SearchSettingsControl.HardklorSignalToNoise = 3.01); // Legal

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
                WaitForConditionUI(5 * 60000, () => searchSucceeded.HasValue);
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

            // IsPauseForScreenShots = true; // enable for quick demo
            PauseForScreenShot("complete");

            // See if Hardklor output is stable
            var expectedHardklorFiles = @"expected_hardklor_files";
            var expectedFilesPath = GetTestPath(expectedHardklorFiles);
            foreach (var hkExpectedFilePath in Directory.EnumerateFiles(expectedFilesPath))
            {
                var hkActualFilePath = hkExpectedFilePath.Replace(expectedHardklorFiles, Path.Combine(expectedHardklorFiles, @".."));
                var columnTolerances = new Dictionary<int, double>() { { -1, .00015 } }; // Allow a little rounding wiggle in the decimal values
                AssertEx.FileEquals(hkExpectedFilePath,  hkActualFilePath, columnTolerances, true);
            }

            // Verify use of library RT in chromatogram extraction
            var doc = SkylineWindow.Document;
            var tg = doc.MoleculeTransitions.First(t => t.Transition.Group.IsCustomIon);
            var r = tg.Results.First().First();
            AssertEx.AreEqual(25.486, r.RetentionTime, .01);
            AssertEx.AreEqual(24.407, r.StartRetentionTime, .01);
            AssertEx.AreEqual(27.104, r.EndRetentionTime, .01);

            var expectedFeatures = 906;
            var expectedFeaturesTransitions = 2718;
            if (pass == SMALL_MOL_ONLY_PASS)
            {
                AssertEx.IsDocumentState(SkylineWindow.Document, null, 1, expectedFeatures, expectedFeatures, expectedFeaturesTransitions);
            }
            else
            {
                var expectedPeptideGroups = 11;
                var expectedPeptides = 45;
                var expectedPeptideTransitionGroups = 46;
                var expectedPeptideTransitions = 141;
                AssertEx.IsDocumentState(SkylineWindow.Document, null, expectedPeptideGroups + 1, expectedPeptides + expectedFeatures, 
                    expectedPeptideTransitionGroups + expectedFeatures, expectedPeptideTransitions + expectedFeaturesTransitions);

                // Verify that we found every known peptide
                var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
                RunUI(() => documentGrid.ChooseView(pwiz.Skyline.Properties.Resources.ReportSpecList_GetDefaults_Transition_Results));
                WaitForCondition(() => (documentGrid.RowCount == 2*(expectedPeptideTransitions + expectedFeaturesTransitions))); // Let it initialize

                var propMaxHeight = "Proteins!*.Peptides!*.Precursors!*.Results!*.Value.MaxHeight";
                EnableDocumentGridColumns(documentGrid,
                    pwiz.Skyline.Properties.Resources.ReportSpecList_GetDefaults_Transition_Results,
                    2 * (expectedPeptideTransitions + expectedFeaturesTransitions),
                    new[] { propMaxHeight });

                PauseForScreenShot("document grid");

                var colName = FindDocumentGridColumn(documentGrid, "Precursor.Peptide").Index;
                var colZ = FindDocumentGridColumn(documentGrid, "Precursor.Charge").Index;
                var colMZ = FindDocumentGridColumn(documentGrid, "Precursor.Mz").Index;
                var colFragMZ = FindDocumentGridColumn(documentGrid,"ProductMz").Index;
                var colRT = FindDocumentGridColumn(documentGrid, "Results!*.Value.RetentionTime").Index;
                var colMaxHeight = FindDocumentGridColumn(documentGrid, "Results!*.Value.PrecursorResult.MaxHeight").Index;
                var hits = new HashSet<Hit>();
                var rowCount = documentGrid.DataGridView.RowCount;
                for (var row = 0; row < rowCount; row++)
                {
                    RunUI(() =>
                    {
                        var line = documentGrid.DataGridView.Rows[row];
                        var mzValue = (line.Cells[colMZ].Value as double?) ?? 0;
                        var mzFragValue = (line.Cells[colFragMZ].Value as double?) ?? 0;
                        if (mzFragValue == mzValue) // Just get the M0 entry
                        {
                            var hit = new Hit()
                            {
                                name = line.Cells[colName].Value.ToString(),
                                z = (line.Cells[colZ].Value as int?) ?? 0,
                                mz = mzValue,
                                rt = (line.Cells[colRT].Value as double?) ?? 0,
                                maxHeight = (line.Cells[colMaxHeight].Value as double?) ?? 0
                            };
                            hits.Add(hit);
                        }
                    });
                }

                var unmatched = new HashSet<Hit>();
                var matched = new HashSet<Hit>();
                foreach (var hitP in hits.Where(h => !h.name.StartsWith("mass")))
                {
                    var match = false;
                    foreach (var hitM in hits.Where(hm => hm.name.StartsWith("mass") && hm.z == hitP.z))
                    {
                        if ((Math.Abs(hitP.mz - hitM.mz) < .1) && (Math.Abs(hitM.rt - hitM.rt) < 1))
                        {
                            match = true;
                            matched.Add(hitP);
                            break;
                        }
                    }

                    if (!match)
                    {
                        unmatched.Add(hitP);
                    }
                }

                if (unmatched.Any())
                {
                    var msg = "No feature detected to match known peptide(s):\n"+
                              string.Join("\n", unmatched.Select(u => u.ToString()));
                    PauseForScreenShot(msg);
                }

                // Hardklor+Bullseye simply misses some peptides that search engine found
                // For the most part these are down in the grass, so maybe to be expected
                // CONSIDER:(bspratt) debug Hardklor+Bullseye code?
                var expectedMisses = new[]
                {
                    ("DQVANSAFVER",2), 
                    ("DIDISSPEFK",2), 
                    ("LPSGSGAASPTGSAVDIR",2),
                    ("ISMQDVDLSLGSPK",2),
                    ("ISAPNVDFNLEGPK",2),
                    ("GKGGVTGSPEASISGSKGDLK",3),
                    ("GGVTGSPEASISGSK",2),
                    ("SSKASLGSLEGEAEAEASSPK",3),
                    ("ASLGSLEGEAEAEASSPK",2),
                    ("ASLGSLEGEAEAEASSPKGK",3),
                    ("AEGEWEDQEALDYFSDKESGK",3),
                    ("STFREESPLRIK",3),
                    ("LGGLRPESPESLTSVSR",3),
                    ("DMESPTKLDVTLAK",3),
                    ("ETERASPIKMDLAPSK",3),
                    ("TGSYGALAEITASK",2),
                    ("VVDYSQFQESDDADEDYGR",3),
                    ("VVDYSQFQESDDADEDYGRDSGPPTK",3),
                    ("KETESEAEDNLDDLEK",3)
                };

                var missedHits = hits.Where(h =>
                    expectedMisses.Any(miss => Equals(h.name, miss.Item1) && Equals(h.z, miss.Item2))).ToArray();
                var threshold = hits.Select(h => h.maxHeight).Max() * .1;
                foreach (var miss in missedHits)
                {
                    AssertEx.IsTrue((miss.maxHeight < threshold), $"Hardklor did not find a match for {miss.name} even though it's fairly strong signal");
                }
                var unexpectedMisses = unmatched.Where(um => !expectedMisses.Contains((um.name, um.z))).ToArray();
                var unexpectedMatches = matched.Where(um => expectedMisses.Contains((um.name, um.z))).ToArray();
                AssertEx.IsFalse(unexpectedMisses.Any(),
                    $"Expected to find features for peptides\n{string.Join("\n", unexpectedMisses.Select(u => u.ToString()))}");
                AssertEx.IsFalse(unexpectedMatches.Any(),
                    $"Did not expected to find features for peptides\n{string.Join("\n", unexpectedMisses.Select(u => u.ToString()))}");
            }

        }

        private class Hit : IEquatable<Hit>
        {
            public string name;
            public int z;
            public double mz;
            public double rt;
            public double maxHeight;

            public override string ToString()
            {
                return $"{name} mz={mz} z={z} RT={rt} maxH={maxHeight}";
            }

            public bool Equals(Hit other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return name == other.name && z == other.z && mz.Equals(other.mz) && rt.Equals(other.rt);
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

        private void TidyBetweenPasses()
        {
            // In the test loop there are lots of intentional starts and cancels, and multiple passes, which causes file renames,
            // so restore test directory to initial state for stable audit log creation for test purposes
            foreach (var ext in new[]{"*.kro", "*.hk", "*.ms2", "*.conf"})
            {
                foreach (var f in new DirectoryInfo(GetTestPath(string.Empty)).EnumerateFiles(ext))
                {
                    FileEx.SafeDelete(f.FullName);
                }
            }
        }

        private void VerifyAuditLog()
        {
            var english = "en";
            if (CultureInfo.CurrentCulture.TwoLetterISOLanguageName != english)
            {
                return; // Keep it simple, only worry about one language
            }
            var auditLogActual = Path.Combine(GetTestPath(@".."), "..", this.TestContext.TestName, @"Auditlog", english, this.TestContext.TestName) + ".log";
            var auditLogExpected = GetTestPath(@"TestHardklorFeatureDetection.log");
            AssertEx.FileEquals(auditLogExpected, auditLogActual, null, true);
        }

    }
}
