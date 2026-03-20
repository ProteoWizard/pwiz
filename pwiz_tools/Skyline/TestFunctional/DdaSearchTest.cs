/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2020 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DdaSearchTest : AbstractFunctionalTestEx
    {
        public class ExpectedResults
        {
            public Exception expectedException;
            public int proteinCount;
            public int peptideCount;
            public int precursorCount;
            public int transitionCount;
            public int unmappedOrRemovedCount;

            public ExpectedResults(int proteinCount, int peptideCount, int precursorCount, int transitionCount, int unmappedOrRemovedCount = 0)
            {
                this.proteinCount = proteinCount;
                this.peptideCount = peptideCount;
                this.precursorCount = precursorCount;
                this.transitionCount = transitionCount;
                this.unmappedOrRemovedCount = unmappedOrRemovedCount;
            }

            public ExpectedResults(Exception expected)
            {
                expectedException = expected;
            }
        }

        public class DdaTestSettings
        {
            private SearchEngine _searchEngine;
            public SearchEngine SearchEngine
            {
                get => _searchEngine;
                set
                {
                    _searchEngine = value;
                    HasMissingDependencies = !SearchSettingsControl.HasRequiredFilesDownloaded(value);
                }
            }

            public string FastaFilename { get; set; } = "rpal-subset.fasta";
            public string FragmentIons { get; set; }
            public string Ms2Analyzer { get; set; }
            public MzTolerance PrecursorTolerance { get; set; }
            public MzTolerance FragmentTolerance { get; set; }
            public Dictionary<string, string> AdditionalSettings { get; set; }
            public ExpectedResults ExpectedResults { get; set; }
            public ExpectedResults ExpectedResultsFinal { get; set; }
            public Action BeforeSettingsAction { get; set; }
            public Action ExpectedErrorAction { get; set; }
            public bool HasMissingDependencies { get; private set; }
            public bool TestDependencyErrors { get; set; }
        }

        DdaTestSettings TestSettings;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearch()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            // Test that correct error is issued when MSAmanda tries to parse a missing file (enzymes.xml)
            // Automating this test turned out to be more difficult than I thought and not worth the effort.
            //var skylineDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            //File.Move(System.IO.Path.Combine(skylineDir, "enzymes.xml"), System.IO.Path.Combine(skylineDir, "not-the-enzymes-you-are-looking-for.xml"));

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.MSAmanda,
                FragmentIons = "b, y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(25, MzTolerance.Units.ppm),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(133, 334, 396, 1188, 164)
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchMsgfPlus()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(MsgfPlusSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.MSGFPlus,
                FragmentIons = "CID",
                Ms2Analyzer = "Orbitrap/FTICR/Lumos",
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(25, MzTolerance.Units.ppm),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(100, 238, 288, 864, 116)
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchComet()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(CometSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.Comet,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(1.0005),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(145, 338, 392, 1176, 165)
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchCometAutoTolerance()
        {
            // Check that the error from having not enough spectra to do auto-tolerance calculation is report properly.

            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(CometSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.Comet,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(25, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(0.5),
                AdditionalSettings = new Dictionary<string, string>
                {
                    { "auto_fragment_bin_tol", "fail" },
                    { "auto_peptide_mass_tolerance", "fail" }
                },
                ExpectedResultsFinal = new ExpectedResults(new IOException()),
                ExpectedErrorAction = () =>
                {
                    var errorCalculationFailedDlg = WaitForOpenForm<MessageDlg>();
                    StringAssert.Contains(errorCalculationFailedDlg.Message, "Precursor error calculation failed");
                    StringAssert.Contains(errorCalculationFailedDlg.Message, "Fragment error calculation failed");
                    OkDialog(errorCalculationFailedDlg, errorCalculationFailedDlg.ClickOk);
                }
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }
        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchTide()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(TideSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.Tide,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(1.0005),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(107, 245, 297, 891, 118)
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchMsFragger()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(MsFraggerSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.MSFragger,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                AdditionalSettings = new Dictionary<string, string>
                {
                    { "check_spectral_files", "0" },
                    { "calibrate_mass", "0" },
                    //{ "output_report_topN", "5" },
                },
                ExpectedResultsFinal = new ExpectedResults(143, 337, 425, 1275, 165)
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDdaSearchMsFraggerBadFasta()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(MsFraggerSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.MSFragger,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(50, MzTolerance.Units.ppm),
                AdditionalSettings = new Dictionary<string, string>
                {
                    { "check_spectral_files", "0" }
                },
                BeforeSettingsAction = () => File.Delete(GetTestPath(TestSettings.FastaFilename)),
                ExpectedResultsFinal = new ExpectedResults(new FileNotFoundException()),
                ExpectedErrorAction = () =>
                {
                    var fastaFileNotFoundDlg = WaitForOpenForm<MessageDlg>();
                    var expectedMsg = new FileNotFoundException(GetSystemResourceString("IO.FileNotFound_FileName", GetTestPath(TestSettings.FastaFilename))).Message;
                    Assert.AreEqual(expectedMsg, fastaFileNotFoundDlg.Message);
                    OkDialog(fastaFileNotFoundDlg, fastaFileNotFoundDlg.ClickOk);
                }
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        private void CleanupDownloadedFiles(IList<FileDownloadInfo> requiredFiles)
        {
            if (!RedownloadTools)
                return;

            foreach (var requiredFile in requiredFiles)
            {
                if (!requiredFile.Unzip)
                    FileEx.SafeDelete(Path.Combine(requiredFile.InstallPath, requiredFile.Filename));
                else
                {
                    FileEx.SafeDelete(GetCachedZipPath(requiredFile));
                    DirectoryEx.SafeDelete(requiredFile.InstallPath);
                }
            }
        }

        private static string GetCachedZipPath(FileDownloadInfo requiredFile)
        {
            var downloadedZip = requiredFile.DownloadUrl != null
                ? Path.GetFileName(requiredFile.DownloadUrl.LocalPath)
                : requiredFile.Filename + ".zip";
            return Path.Combine(SimpleFileDownloader.GetCachedDownloadsDirectory(),
                downloadedZip);
        }

        [TestMethod]
        public void TestDdaSearchDependencyErrors()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";

            CleanupDownloadedFiles(CometSearchEngine.FilesToDownload);

            TestSettings = new DdaTestSettings
            {
                SearchEngine = SearchEngine.Comet,
                FragmentIons = "b,y",
                Ms2Analyzer = "Default",
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(1.0005),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(145, 338, 392, 1176, 165),
                TestDependencyErrors = true
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        [TestMethod]
        public void TestDdaSearchSettingsPreset()
        {
            TestFilesZip = @"TestFunctional\DdaSearchTest.zip";
            RunFunctionalTest();
        }

        private void TestSettingsPreset()
        {
            // Default preset (built-in)
            const string DEFAULT_PRESET_NAME = "Default";
            const SearchEngine DEFAULT_ENGINE = SearchEngine.MSAmanda;
            const int DEFAULT_MAX_VARIABLE_MODS = 2;
            const double DEFAULT_CUTOFF = 0.01;
            const int DEFAULT_MISSED_CLEAVAGES = 0;

            // Preset 1: MSAmanda DDA with specific tolerances and FASTA settings
            const string PRESET_1_NAME = "MSAmanda - Test Config";
            const ImportPeptideSearchDlg.Workflow PRESET_1_WORKFLOW = ImportPeptideSearchDlg.Workflow.dda;
            const SearchEngine PRESET_1_ENGINE = SearchEngine.MSAmanda;
            const double PRESET_1_PRECURSOR_TOL = 10;
            const MzTolerance.Units PRESET_1_PRECURSOR_UNIT = MzTolerance.Units.ppm;
            const double PRESET_1_FRAGMENT_TOL = 20;
            const MzTolerance.Units PRESET_1_FRAGMENT_UNIT = MzTolerance.Units.ppm;
            const string PRESET_1_FRAGMENT_IONS = "b, y";
            const string PRESET_1_MS2_ANALYZER = "Default";
            const double PRESET_1_CUTOFF = 0.05;
            const string PRESET_1_CHARGES = "2,3,4";
            const int PRESET_1_MISSED_CLEAVAGES = 3;
            const string PRESET_1_ENZYME = "Trypsin";
            const string PRESET_1_FASTA = "rpal-subset.fasta";

            // Preset 2: Comet DDA with different tolerances and FASTA settings
            const string PRESET_2_NAME = "Comet - Test Config";
            const SearchEngine PRESET_2_ENGINE = SearchEngine.Comet;
            const double PRESET_2_PRECURSOR_TOL = 25;
            const MzTolerance.Units PRESET_2_PRECURSOR_UNIT = MzTolerance.Units.ppm;
            const double PRESET_2_FRAGMENT_TOL = 1.0005;
            const MzTolerance.Units PRESET_2_FRAGMENT_UNIT = MzTolerance.Units.mz;
            const string PRESET_2_FRAGMENT_IONS = "b,y";
            const string PRESET_2_MS2_ANALYZER = "Default";
            const double PRESET_2_CUTOFF = 0.01;
            const int PRESET_2_MISSED_CLEAVAGES = 1;

            Settings.Default.SearchSettingsPresets.Clear();
            Settings.Default.SearchSettingsPresets.AddDefaults();

            PrepareDocument("TestSettingsPreset.sky");

            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            // Navigate through wizard to the search settings page, setting values along the way
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dda;
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.AUTO;

                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Match modifications page - uncheck Carbamidomethyl for preset 1
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on mods page");
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck Carbamidomethyl (C)
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Full scan settings page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Import FASTA page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on FASTA page");
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(PRESET_1_FASTA));
                importPeptideSearchDlg.ImportFastaControl.Enzyme = Settings.Default.GetEnzymeByName(PRESET_1_ENZYME);
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = PRESET_1_MISSED_CLEAVAGES;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // DDA search settings page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on search settings page");
            });

            var searchSettingsControl = importPeptideSearchDlg.SearchSettingsControl;

            // Configure and save preset 1
            RunUI(() =>
            {
                searchSettingsControl.SelectedSearchEngine = PRESET_1_ENGINE;
                searchSettingsControl.PrecursorTolerance = new MzTolerance(PRESET_1_PRECURSOR_TOL, PRESET_1_PRECURSOR_UNIT);
                searchSettingsControl.FragmentTolerance = new MzTolerance(PRESET_1_FRAGMENT_TOL, PRESET_1_FRAGMENT_UNIT);
                searchSettingsControl.FragmentIons = PRESET_1_FRAGMENT_IONS;
                searchSettingsControl.Ms2Analyzer = PRESET_1_MS2_ANALYZER;
                searchSettingsControl.CutoffScore = PRESET_1_CUTOFF;
                searchSettingsControl.SetAdditionalSetting("ConsideredCharges", PRESET_1_CHARGES);
            });
            // Verify Carbamidomethyl is unchecked before saving
            RunUI(() =>
            {
                var currentChecked = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                Assert.IsFalse(currentChecked.Contains(@"Carbamidomethyl (C)"),
                    $"Carbamidomethyl should be unchecked before saving preset 1. Checked: [{string.Join(", ", currentChecked)}]");
            });
            RunUI(() => importPeptideSearchDlg.SaveSettingsPreset(PRESET_1_NAME));

            // Remember the FASTA path that was saved (includes the full test path)
            string preset1FastaPath = null;
            RunUI(() => preset1FastaPath = importPeptideSearchDlg.ImportFastaControl.FastaFile);

            // Verify preset was saved and Carbamidomethyl is NOT in its structural mods
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(PRESET_1_NAME));
                Assert.AreEqual(PRESET_1_NAME, importPeptideSearchDlg.SelectedPresetName);
                var savedPreset1 = Settings.Default.SearchSettingsPresets.First(p => p.Name == PRESET_1_NAME);
                Assert.IsFalse(savedPreset1.StructuralModifications.Any(m => m.Name == @"Carbamidomethyl (C)"),
                    "Saved preset 1 should not contain Carbamidomethyl in structural mods. " +
                    $"Actual mods: {string.Join(", ", savedPreset1.StructuralModifications.Select(m => m.Name))}");
            });

            // Configure and save preset 2 with different search engine and FASTA settings
            RunUI(() =>
            {
                searchSettingsControl.SelectedSearchEngine = PRESET_2_ENGINE;
                searchSettingsControl.PrecursorTolerance = new MzTolerance(PRESET_2_PRECURSOR_TOL, PRESET_2_PRECURSOR_UNIT);
                searchSettingsControl.FragmentTolerance = new MzTolerance(PRESET_2_FRAGMENT_TOL, PRESET_2_FRAGMENT_UNIT);
                searchSettingsControl.FragmentIons = PRESET_2_FRAGMENT_IONS;
                searchSettingsControl.Ms2Analyzer = PRESET_2_MS2_ANALYZER;
                searchSettingsControl.CutoffScore = PRESET_2_CUTOFF;
            });
            // Navigate back to change FASTA, mods, and workflow for preset 2
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                // Set to a different enzyme than preset 1
                var enzyme2 = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name != PRESET_1_ENZYME);
                Assert.IsNotNull(enzyme2, "Should have at least 2 enzymes in the enzyme list");
                importPeptideSearchDlg.ImportFastaControl.Enzyme = enzyme2;
                Assert.AreEqual(enzyme2.Name, importPeptideSearchDlg.ImportFastaControl.Enzyme.Name,
                    "Enzyme combo box should reflect the selected enzyme");
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = PRESET_2_MISSED_CLEAVAGES;
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods - re-check Carbamidomethyl for preset 2
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, true); // check Carbamidomethyl (C)
            });
            RunUI(() => importPeptideSearchDlg.SaveSettingsPreset(PRESET_2_NAME));

            // Verify both presets exist
            RunUI(() =>
            {
                var defaultPresetCount = Settings.Default.SearchSettingsPresets.GetDefaults(0).Count();
                Assert.AreEqual(defaultPresetCount + 2, importPeptideSearchDlg.PresetNames.Count()); // defaults + 2 saved
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(DEFAULT_PRESET_NAME));
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(PRESET_1_NAME));
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(PRESET_2_NAME));
            });

            // Navigate forward to search settings and scramble all settings
            RunUI(() =>
            {
                // We're on the mods page after saving preset 2; navigate forward
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 9;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                searchSettingsControl.PrecursorTolerance = new MzTolerance(99, MzTolerance.Units.ppm);
                searchSettingsControl.FragmentTolerance = new MzTolerance(99, MzTolerance.Units.ppm);
                searchSettingsControl.CutoffScore = 0.99;
            });

            // Clear and re-apply preset 1 from the search settings page
            RunUI(() =>
            {
                importPeptideSearchDlg.SelectedPresetName = string.Empty;
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
            });

            // Verify search settings restored on current page
            RunUI(() =>
            {
                Assert.AreEqual(PRESET_1_ENGINE, searchSettingsControl.SelectedSearchEngine);
                Assert.AreEqual(PRESET_1_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001);
                Assert.AreEqual(PRESET_1_PRECURSOR_UNIT, searchSettingsControl.PrecursorTolerance.Unit);
                Assert.AreEqual(PRESET_1_FRAGMENT_TOL, searchSettingsControl.FragmentTolerance.Value, 0.001);
                Assert.AreEqual(PRESET_1_FRAGMENT_UNIT, searchSettingsControl.FragmentTolerance.Unit);
                Assert.AreEqual(PRESET_1_CUTOFF, searchSettingsControl.CutoffScore, 0.001);
                Assert.AreEqual(PRESET_1_CHARGES, searchSettingsControl.AdditionalSettings["ConsideredCharges"].Value);
            });

            // Navigate back to FASTA page and verify those settings too
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual(PRESET_1_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                Assert.AreEqual(preset1FastaPath, importPeptideSearchDlg.ImportFastaControl.FastaFile);
            });

            // Navigate back to spectra page, apply preset 2 from there
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);

                importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME;
            });

            // Navigate forward through all pages, verifying preset 2 settings
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);

                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                Assert.AreEqual(PRESET_2_ENGINE, searchSettingsControl.SelectedSearchEngine);
                Assert.AreEqual(PRESET_2_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001);
                Assert.AreEqual(PRESET_2_PRECURSOR_UNIT, searchSettingsControl.PrecursorTolerance.Unit);
                Assert.AreEqual(PRESET_2_FRAGMENT_TOL, searchSettingsControl.FragmentTolerance.Value, 0.0001);
                Assert.AreEqual(PRESET_2_FRAGMENT_UNIT, searchSettingsControl.FragmentTolerance.Unit);
                Assert.AreEqual(PRESET_2_CUTOFF, searchSettingsControl.CutoffScore, 0.001);
            });

            // Switch back to preset 1 from search settings page, verify it overrides preset 2
            RunUI(() => importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME);
            RunUI(() =>
            {
                Assert.AreEqual(PRESET_1_ENGINE, searchSettingsControl.SelectedSearchEngine);
                Assert.AreEqual(PRESET_1_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001);
                Assert.AreEqual(PRESET_1_CUTOFF, searchSettingsControl.CutoffScore, 0.001);
            });

            // Switch to preset 2, navigate back to FASTA, verify preset 2 FASTA settings
            RunUI(() => importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
            });

            // Test overwriting preset 1 with modified settings
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME; // restore preset 1 settings first
                searchSettingsControl.PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm); // then modify
                importPeptideSearchDlg.SaveSettingsPreset(PRESET_1_NAME);
                // Verify overwritten preset still has no Carbamidomethyl (uses cached mod state)
                var overwrittenPreset = Settings.Default.SearchSettingsPresets.First(p => p.Name == PRESET_1_NAME);
                Assert.IsFalse(overwrittenPreset.StructuralModifications.Any(m => m.Name == @"Carbamidomethyl (C)"),
                    "Overwritten preset 1 should still not contain Carbamidomethyl");
                Assert.AreEqual(1, importPeptideSearchDlg.PresetNames.Count(n => n == PRESET_1_NAME),
                    "Should only have one preset with the same name after overwrite");
            });

            // Test switching to Default preset resets to default settings
            RunUI(() =>
            {
                // Verify Default preset exists
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(DEFAULT_PRESET_NAME),
                    "Default preset should always be in the list");

                // Apply preset 1 first so we have non-default settings
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
            });
            RunUI(() =>
            {
                Assert.AreEqual(PRESET_1_ENGINE, searchSettingsControl.SelectedSearchEngine);
                Assert.AreEqual(PRESET_1_CUTOFF, searchSettingsControl.CutoffScore, 0.001);
            });

            // Switch to Default
            RunUI(() => importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME);
            RunUI(() =>
            {
                Assert.AreEqual(DEFAULT_ENGINE, searchSettingsControl.SelectedSearchEngine,
                    "Default preset should set MSAmanda engine");
                Assert.AreEqual(DEFAULT_MAX_VARIABLE_MODS, searchSettingsControl.MaxVariableMods,
                    "Default preset should set max variable mods to 2");
                Assert.AreEqual(DEFAULT_CUTOFF, searchSettingsControl.CutoffScore, 0.001,
                    "Default preset should set cutoff to 0.01");
            });

            // Navigate back to FASTA page and verify default FASTA settings
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual(DEFAULT_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Default preset should set missed cleavages to 0");
            });

            // Switch back to preset 2 and verify it still applies correctly
            RunUI(() => importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME);
            RunUI(() =>
            {
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Preset 2 missed cleavages should be restored after switching from Default");
            });
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.AreEqual(PRESET_2_ENGINE, searchSettingsControl.SelectedSearchEngine,
                    "Preset 2 engine should be restored after switching from Default");
                Assert.AreEqual(PRESET_2_CUTOFF, searchSettingsControl.CutoffScore, 0.001,
                    "Preset 2 cutoff should be restored after switching from Default");
                Assert.AreEqual(PRESET_2_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001,
                    "Preset 2 precursor tolerance should be restored after switching from Default");
            });

            // Test switching presets without changing page - search settings page
            RunUI(() =>
            {
                // We're on search settings page with preset 2 applied
                Assert.AreEqual(PRESET_2_ENGINE, searchSettingsControl.SelectedSearchEngine);

                // Switch to preset 1 without leaving the page
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_ENGINE, searchSettingsControl.SelectedSearchEngine,
                    "Engine should update immediately when switching presets on search settings page");
                Assert.AreEqual(PRESET_1_CUTOFF, searchSettingsControl.CutoffScore, 0.001,
                    "Cutoff should update immediately when switching presets on search settings page");

                // Switch to Default without leaving the page
                importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME;
                Assert.AreEqual(DEFAULT_ENGINE, searchSettingsControl.SelectedSearchEngine,
                    "Engine should update immediately when switching to Default on search settings page");
                Assert.AreEqual(DEFAULT_CUTOFF, searchSettingsControl.CutoffScore, 0.001,
                    "Cutoff should update immediately when switching to Default on search settings page");

                // Switch back to preset 2
                importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME;
                Assert.AreEqual(PRESET_2_ENGINE, searchSettingsControl.SelectedSearchEngine,
                    "Engine should update immediately when switching back to preset 2 on search settings page");
                Assert.AreEqual(PRESET_2_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001,
                    "Precursor tolerance should update immediately on search settings page");
            });

            // Test switching presets without changing page - FASTA page
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);

                // Currently preset 2
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);

                // Switch to preset 1 without leaving the page
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_ENZYME, importPeptideSearchDlg.ImportFastaControl.Enzyme.Name,
                    "Enzyme should update immediately when switching presets on FASTA page");
                Assert.AreEqual(PRESET_1_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should update immediately when switching presets on FASTA page");
                Assert.IsNotNull(importPeptideSearchDlg.ImportFastaControl.FastaFile,
                    "FASTA file should be set when switching to preset 1 on FASTA page");

                // Switch to preset 2 - verify different enzyme
                var preset2EnzymeName = Settings.Default.SearchSettingsPresets.First(p => p.Name == PRESET_2_NAME).EnzymeName;
                Assert.AreNotEqual(PRESET_1_ENZYME, preset2EnzymeName,
                    "Preset 2 should have a different enzyme than preset 1");
                importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME;
                Assert.AreEqual(preset2EnzymeName, importPeptideSearchDlg.ImportFastaControl.Enzyme.Name,
                    "Enzyme should update immediately when switching to preset 2 on FASTA page");
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should update when switching to preset 2 on FASTA page");

                // Switch to Default without leaving the page
                importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME;
                Assert.AreEqual(DEFAULT_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should update immediately when switching to Default on FASTA page");
                Assert.IsTrue(string.IsNullOrEmpty(importPeptideSearchDlg.ImportFastaControl.FastaFile),
                    "FASTA file should be cleared when switching to Default on FASTA page");

                // Switch back to preset 1
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_ENZYME, importPeptideSearchDlg.ImportFastaControl.Enzyme.Name,
                    "Enzyme should update when switching back to preset 1 on FASTA page");
                Assert.AreEqual(PRESET_1_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should update immediately when switching back to preset 1 on FASTA page");
            });

            // Test switching presets without changing page - modifications page
            // Preset 1 has Carbamidomethyl unchecked; preset 2 has it checked
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);

                // Switch to preset 1 - Carbamidomethyl should be unchecked
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                var modsAfterPreset1 = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                var allMods = importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToList();
                var preset1 = Settings.Default.SearchSettingsPresets.First(p => p.Name == PRESET_1_NAME);
                Assert.IsFalse(modsAfterPreset1.Contains(@"Carbamidomethyl (C)"),
                    $"Preset 1 should have Carbamidomethyl unchecked. HasExplicit={preset1.HasExplicitModifications}, " +
                    $"PresetStructMods=[{string.Join(", ", preset1.StructuralModifications.Select(m => m.Name))}], " +
                    $"CheckedMods=[{string.Join(", ", modsAfterPreset1)}], AllMods=[{string.Join(", ", allMods)}]");

                // Switch to preset 2 - Carbamidomethyl should be checked
                importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME;
                var modsAfterPreset2 = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                Assert.IsTrue(modsAfterPreset2.Contains(@"Carbamidomethyl (C)"),
                    "Preset 2 should have Carbamidomethyl checked");

                // Switch back to preset 1 - Carbamidomethyl should be unchecked again
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                var modsAfterPreset1Again = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                Assert.IsFalse(modsAfterPreset1Again.Contains(@"Carbamidomethyl (C)"),
                    "Preset 1 should still have Carbamidomethyl unchecked after switching back");

                // Switch to Default - should use document mods (Carbamidomethyl checked from document settings)
                importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME;
                var modsAfterDefault = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                Assert.IsTrue(modsAfterDefault.Contains(@"Carbamidomethyl (C)"),
                    "Default preset should restore document mods including Carbamidomethyl");
            });

            // Test switching presets on spectra page changes workflow and IRT
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);

                // Both presets are DDA; manually change to PRM and save a temporary preset
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.prm;
                importPeptideSearchDlg.SaveSettingsPreset("PRM Preset");

                // Switch to preset 1 - should restore DDA workflow
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_WORKFLOW, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Preset 1 should set DDA workflow on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsAuto,
                    "Preset 1 should have Auto IRT standard");

                // Switch to PRM preset - should change to PRM
                importPeptideSearchDlg.SelectedPresetName = "PRM Preset";
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.prm, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "PRM preset should set PRM workflow on spectra page");

                // Switch to Default from PRM - should reset workflow to DDA and IRT to None
                importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME;
                Assert.AreEqual(ImportPeptideSearchDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Default preset should reset workflow to DDA from PRM on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsEmpty,
                    "Default preset should reset IRT standard to None on spectra page");

                // Switch to preset 1 - should restore DDA and Auto IRT
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_WORKFLOW, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Preset 1 should restore DDA workflow on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsAuto,
                    "Preset 1 should restore Auto IRT standard on spectra page");
            });

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
            Settings.Default.SearchSettingsPresets.Clear();
        }

        protected override bool IsRecordMode => false;
        private bool RedownloadTools => !IsRecordMode && IsPass0;

        private string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        private IEnumerable<string> SearchFiles
        {
            get
            {
                return new[]
                {
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_03.mzML")
                };
            }
        }

        private string[] SearchFilesSameName
        {
            get
            {
                return new[]
                {
                    GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
                    GetTestPath(Path.Combine("subdir", "Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"))
                };
            }
        }

        protected override void DoTest()
        {
            if (TestSettings == null)
                TestSettingsPreset();
            else
                TestSearch();
        }

        /// <summary>
        /// Quick test that DDA search works with MSAmanda.
        /// </summary>
        private void TestSearch()
        {
            PrepareDocument("TestDdaSearch.sky");

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.prm; // will go back and switch to DDA
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.AUTO;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                // With only 1 source, no add/remove prefix/suffix dialog

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                // Test going back and switching workflow to DDA. This used to cause an exception.
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dda;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            });

            bool errorExpected = TestSettings.ExpectedResultsFinal.expectedException != null || TestSettings.TestDependencyErrors;

            if (!errorExpected)
            {
                // In PerformDDASearch mode, ClickAddStructuralModification launches edit list dialog
                var editListUI =
                    ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
                RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    editModDlg.SetModification("Oxidation (M)"); // Not L10N
                    editModDlg.OkDialog();
                });

                // Test a non-Unimod mod that won't affect the search
                RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    editModDlg.Modification = new StaticMod("NotUniModMod (U)", "U", null, "C3P1O1", LabelAtoms.None, null, null);
                    editModDlg.OkDialog();
                });

                // Test a N terminal mod with no AA
                RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    editModDlg.Modification = new StaticMod("NotUniModMod (N-term)", null, ModTerminus.N, "C42", LabelAtoms.None, null, null);
                    editModDlg.Modification = editModDlg.Modification.ChangeVariable(true);
                    editModDlg.OkDialog();
                });

                // Test a C terminal mod with no AA: commented out because it's buggy with MSAmanda
                /*RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    editModDlg.Modification = new StaticMod("NotUniModMod (C-term)", null, ModTerminus.C, null, LabelAtoms.None, 1100.01, 1100.01);
                    editModDlg.Modification = editModDlg.Modification.ChangeVariable(true);
                    editModDlg.OkDialog();
                }); */

                // Test a mod with multiple AA specificities
                RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    editModDlg.Modification = new StaticMod("MoreNotUniModMod (C-term)", "K,R", null, null, LabelAtoms.None, 1200.000001, 1200.000001);
                    editModDlg.Modification = editModDlg.Modification.ChangeVariable(true);
                    editModDlg.OkDialog();
                });

                // Add the combined mod to allow interpreting results; put it at the end to uncheck it so it's not used for searches
                RunDlg<EditStaticModDlg>(editListUI.AddItem, editModDlg =>
                {
                    string combinedFormula = Molecule.Parse(UniMod.GetModification("Oxidation (M)", true).Formula).AdjustElementCount("C", 42).ToString();
                    editModDlg.Modification = new StaticMod("ZCombinedNotUniModMod", null, ModTerminus.N, combinedFormula, LabelAtoms.None, null, null);
                    editModDlg.Modification = editModDlg.Modification.ChangeVariable(true);
                    editModDlg.OkDialog();
                });
                OkDialog(editListUI, editListUI.OkDialog);

                // Test back/next buttons
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                });
            }

            // We're on the MS1 full scan settings page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Import FASTA" page.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(TestSettings.FastaFilename));
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Adjust Search Settings" page
            bool? searchSucceeded = null;
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchSettingsPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
            });

            TestSettings.BeforeSettingsAction?.Invoke();

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchSettingsControl.SelectedSearchEngine = TestSettings.SearchEngine;
                foreach (var setting in TestSettings.AdditionalSettings)
                    importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting(setting.Key, setting.Value);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = TestSettings.PrecursorTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = TestSettings.FragmentTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = TestSettings.FragmentIons;
                importPeptideSearchDlg.SearchSettingsControl.Ms2Analyzer = TestSettings.Ms2Analyzer;
                importPeptideSearchDlg.SearchSettingsControl.CutoffScore = 0.1;
            });

            // Save a preset with the current settings before starting the search
            const string SEARCH_PRESET_NAME = "DDA Search Preset";
            RunUI(() => importPeptideSearchDlg.SaveSettingsPreset(SEARCH_PRESET_NAME));

            RunUI(() =>
            {
                // Run the search
                //Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.ClickNextButton()));

            if (RedownloadTools || TestSettings.HasMissingDependencies)
            {
                if (TestSettings.SearchEngine == SearchEngine.MSFragger)
                {
                    var msfraggerDownloaderDlg = TryWaitForOpenForm<MsFraggerDownloadDlg>(2000);
                    if (msfraggerDownloaderDlg != null)
                    {
                        RunUI(() => msfraggerDownloaderDlg.SetValues("Matt (testing download from Skyline)", "Chambers", "chambem2@gmail.com", "UW"));
                        //PauseTest(); // uncomment to test bad email handling (e.g. non-institutional email)
                        RunUI(() => msfraggerDownloaderDlg.SetValues("Matt (testing download from Skyline)", "Chambers", "chambem2@uw.edu", "UW"));

                        if (RedownloadTools)
                        {
                            // Expect MsFraggerDownloadDlg to stay open when HttpClient is canceled or fails
                            TestHttpClientCancellation(msfraggerDownloaderDlg.ClickAccept);
                            TestHttpClientWithNoNetwork(msfraggerDownloaderDlg.ClickAccept);
                        }

                        OkDialog(msfraggerDownloaderDlg, msfraggerDownloaderDlg.ClickAccept);
                    }
                }

                if (TestSettings.SearchEngine != SearchEngine.MSAmanda)
                {
                    var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
                    if (downloaderDlg != null)
                    {
                        if (RedownloadTools)
                        {
                            // Expect download form to stay open when HttpClient is canceled or fails
                            TestHttpClientCancellation(downloaderDlg.ClickYes);
                            downloaderDlg = WaitForOpenForm<MultiButtonMsgDlg>(2000);   // New form will be shown
                            TestHttpClientWithNoNetwork(downloaderDlg.ClickYes);
                            downloaderDlg = WaitForOpenForm<MultiButtonMsgDlg>(2000);   // New form will be shown
                        }

                        OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                        var waitDlg = WaitForOpenForm<LongWaitDlg>();
                        WaitForClosedForm(waitDlg);
                    }
                }
            }

            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            if (TestSettings.TestDependencyErrors)
            {
                TestDependencyErrors(importPeptideSearchDlg);
                return;
            }

            if (errorExpected)
            {
                TestSettings.ExpectedErrorAction?.Invoke();
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);
                OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
                return;
            }

            // Let the first search complete successfully
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText);
            searchSucceeded = null;

            // Verify original data source filenames are preserved when going back after a successful search
            var expectedDataSources = SearchFiles.Take(1).Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
            RunUI(() =>
            {
                // Go back to search settings page (triggers filename restoration)
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);

                // Verify the filenames by checking the data sources
                var actualSources = importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources;
                Assert.AreEqual(expectedDataSources.Length, actualSources.Length,
                    "Data source count should be preserved after going back from successful search");
                for (int i = 0; i < expectedDataSources.Length; i++)
                    Assert.AreEqual(expectedDataSources[i].ToString(), actualSources[i].ToString(),
                        $"Data source {i} should be the original raw file, not a search result file");
            });

            // Start a second search, test cannot-close-during-search, then cancel
            SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.ClickNextButton()));
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));
            RunUI(() => importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success);

            SkylineWindow.BeginInvoke(new Action(importPeptideSearchDlg.Close)); // try to close (don't wait for return)
            var cannotCloseDuringSearchDlg = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(PeptideSearchResources.SearchControl_CanWizardClose_Cannot_close_wizard_while_the_search_is_running_,
                cannotCloseDuringSearchDlg.Message);
            OkDialog(cannotCloseDuringSearchDlg, cannotCloseDuringSearchDlg.ClickNo);

            // Cancel search (but don't close wizard)
            RunUI(importPeptideSearchDlg.SearchControl.Cancel);

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsFalse(searchSucceeded.Value);
            searchSucceeded = null;

            // Go back and test 2 input files with the same name
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFilesSameName.Select(o => (MsDataFileUri) new MsDataFilePath(o)).ToArray();
            });

            // Cancel without changing the replicate names
            {
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                OkDialog(removeSuffix, removeSuffix.CancelDialog);
            }

            // Test with 2 files (different name)
            RunUI(() =>
            {
                // CONSIDER: Why does this end up on the next page after a cancel?
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
            });

            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            {
                var removeSuffix2 = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
                OkDialog(removeSuffix2, () => removeSuffix2.YesDialog());
            }

            RunUI(() =>
            {
                // The document should not have changed, but code used to wait for it to be loaded
                Assert.IsTrue(SkylineWindow.DocumentUI.IsLoaded, TextUtil.LineSeparate("Document not loaded:",
                    TextUtil.LineSeparate(SkylineWindow.DocumentUI.NonLoadedStateDescriptions)));
                // We're on the "Match Modifications" page again.
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck C+57
                for (int i = 1; i < importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.Count(); ++i)
                    importPeptideSearchDlg.MatchModificationsControl.ChangeItem(i, true); // check everything else
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.Count() - 1, false); // uncheck combined mod
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3, 4 };
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod = DecoyGeneration.REVERSE_SEQUENCE;
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = true;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            try
            {
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);

                RunUI(() =>
                {
                    Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText);

                    // If this message is seen, the default config needs to be updated.
                    if (TestSettings.SearchEngine == SearchEngine.MSFragger)
                    {
                        const string parameterNotSuppliedMessage = @"was not supplied. Using default value";
                        Assert.IsFalse(
                            importPeptideSearchDlg.SearchControl.LogText.Contains(parameterNotSuppliedMessage),
                            "The default MSFragger config needs to be updated with new parameters:\r\n" +
                            string.Join("", importPeptideSearchDlg.SearchControl.LogText.Split('\n')
                                .Where(l => l.Contains(parameterNotSuppliedMessage))));
                    }
                });
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", importPeptideSearchDlg.SearchControl.LogText);
            }

            var addIrtPeptidesDlg = ShowDialog<AddIrtPeptidesDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            var recalibrateMessage = ShowDialog<MultiButtonMsgDlg>(addIrtPeptidesDlg.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_,
                Resources.LibraryGridViewDriver_AddToLibrary_This_can_improve_retention_time_alignment_under_stable_chromatographic_conditions_), recalibrateMessage.Message));
            var emptyProteinsDlg = ShowDialog<AssociateProteinsDlg>(recalibrateMessage.OkDialog);
            WaitForConditionUI(() => emptyProteinsDlg.DocumentFinalCalculated);

            var requiredFiles = SearchSettingsControl.GetSearchEngineRequiredFiles(TestSettings.SearchEngine);
            foreach(var requiredFile in requiredFiles)
                Assert.IsTrue(Settings.Default.SearchToolList.ContainsKey(requiredFile.ToolType));
            
            if (TestSettings.SearchEngine == SearchEngine.MSFragger)
                StringAssert.Matches(Settings.Default.SearchToolList[SearchToolType.Java].ExtraCommandlineArgs, new Regex(@"-Xmx\d+M"));
            
            if (TestSettings.SearchEngine == SearchEngine.Comet)
            {
                Assert.IsTrue(Settings.Default.SearchToolList.ContainsKey(SearchToolType.CruxComet));
                Assert.IsTrue(Settings.Default.SearchToolList.ContainsKey(SearchToolType.CruxPercolator));
            }

            RunUI(()=>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount, unmappedOrRemoved;
                /*emptyProteinsDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                if (Environment.Is64BitProcess)
                {
                    // TODO: reenable these checks for 32 bit once intermittent failures are debugged
                    if (IsRecordMode)
                    {
                        Console.WriteLine();
                        Console.WriteLine($@"{proteinCount}, {peptideCount}, {precursorCount}, {transitionCount}");
                    }
                    else
                    {
                        Assert.AreEqual(TestSettings.ExpectedResults.proteinCount, proteinCount);
                        Assert.AreEqual(TestSettings.ExpectedResults.peptideCount, peptideCount);
                        Assert.AreEqual(TestSettings.ExpectedResults.precursorCount, precursorCount);
                        Assert.AreEqual(TestSettings.ExpectedResults.transitionCount, transitionCount);
                    }
                }*/
                emptyProteinsDlg.NewTargetsFinalSync(out proteinCount, out peptideCount, out precursorCount, out transitionCount, out unmappedOrRemoved);
                if (Environment.Is64BitProcess)
                {
                    if (IsRecordMode)
                    {
                        Console.WriteLine($@"ExpectedResultsFinal = new ExpectedResults({proteinCount}, {peptideCount}, {precursorCount}, {transitionCount}, {unmappedOrRemoved})");
                    }
                    else
                    {
                        Assert.AreEqual(TestSettings.ExpectedResultsFinal.proteinCount, proteinCount);
                        Assert.AreEqual(TestSettings.ExpectedResultsFinal.peptideCount, peptideCount);
                        Assert.AreEqual(TestSettings.ExpectedResultsFinal.precursorCount, precursorCount);
                        Assert.AreEqual(TestSettings.ExpectedResultsFinal.transitionCount, transitionCount);
                        Assert.AreEqual(TestSettings.ExpectedResultsFinal.unmappedOrRemovedCount, unmappedOrRemoved);
                    }
                }
            });
            OkDialog(emptyProteinsDlg, emptyProteinsDlg.OkDialog);
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());

            // Verify the preset can be loaded in a new wizard with all settings restored
            VerifyPresetInNewWizard(SEARCH_PRESET_NAME);
        }

        private void VerifyPresetInNewWizard(string presetName)
        {
            var preset = Settings.Default.SearchSettingsPresets.FirstOrDefault(p => p.Name == presetName);
            Assert.IsNotNull(preset, $"Preset '{presetName}' should exist in settings");

            var importPeptideSearchDlg2 = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg2.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg2.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dda;

                // Apply the preset from the first page
                importPeptideSearchDlg2.SelectedPresetName = presetName;
            });

            // Navigate through wizard verifying settings on relevant pages
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);

                // Verify FASTA settings
                if (!string.IsNullOrEmpty(preset.EnzymeName))
                    Assert.AreEqual(preset.EnzymeName, importPeptideSearchDlg2.ImportFastaControl.Enzyme.Name,
                        "Enzyme should be restored from preset in new wizard");
                Assert.AreEqual(preset.MaxMissedCleavages, importPeptideSearchDlg2.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should be restored from preset in new wizard");

                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_settings_page);
            });

            // Verify search settings
            RunUI(() =>
            {
                var searchSettings = importPeptideSearchDlg2.SearchSettingsControl;
                Assert.AreEqual(preset.SearchEngine, searchSettings.SelectedSearchEngine,
                    "Search engine should be restored from preset in new wizard");
                Assert.AreEqual(preset.PrecursorToleranceValue, searchSettings.PrecursorTolerance.Value, 0.001,
                    "Precursor tolerance should be restored from preset in new wizard");
                Assert.AreEqual(preset.PrecursorToleranceUnit, searchSettings.PrecursorTolerance.Unit,
                    "Precursor tolerance unit should be restored from preset in new wizard");
                Assert.AreEqual(preset.FragmentToleranceValue, searchSettings.FragmentTolerance.Value, 0.001,
                    "Fragment tolerance should be restored from preset in new wizard");
                Assert.AreEqual(preset.FragmentToleranceUnit, searchSettings.FragmentTolerance.Unit,
                    "Fragment tolerance unit should be restored from preset in new wizard");
                Assert.AreEqual(preset.CutoffScore, searchSettings.CutoffScore, 0.001,
                    "Cutoff score should be restored from preset in new wizard");
            });

            OkDialog(importPeptideSearchDlg2, importPeptideSearchDlg2.ClickCancelButton);

            Settings.Default.SearchSettingsPresets.Clear();
        }

        private void TestDependencyErrors(ImportPeptideSearchDlg importPeptideSearchDlg)
        {
            bool? searchSucceeded = null;
            RunUI(() => importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success);
            
            // Cancel search (but don't close wizard)
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.SearchControl.CanCancel);  // Avoid a long wait on a cancel that does nothing
                importPeptideSearchDlg.SearchControl.Cancel();
            });
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()));

            // Delete one of the auto-installed dependencies; Skyline will automatically try to redownload it
            var requiredFile = SearchSettingsControl.GetSearchEngineRequiredFiles(TestSettings.SearchEngine).First();
            DirectoryEx.SafeDelete(requiredFile.InstallPath);

            // Rerun search
            searchSucceeded = null;
            SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.ClickNextButton()));

            var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
            if (downloaderDlg != null)
            {
                OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                var waitDlg = WaitForOpenForm<LongWaitDlg>();
                WaitForClosedForm(waitDlg);
            }
            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

                
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()));

            // Set Path and AutoInstalled to make it look like a user picked the installed location;
            // in this case Skyline will pop up the edit control for that tool to fix the location
            Settings.Default.SearchToolList[requiredFile.ToolType].AutoInstalled = false;
            Settings.Default.SearchToolList[requiredFile.ToolType].Path += ".42.exe";

            // Test Edit Search Tools button and check the path
            var editToolListDlg = ShowDialog<EditListDlg<SettingsListBase<SearchTool>, SearchTool>>(SkylineWindow.ShowSearchToolsDlg);
            var editToolDlg = ShowDialog<EditSearchToolDlg>(() =>
            {
                editToolListDlg.SelectItem(requiredFile.ToolType.ToString());
                editToolListDlg.EditItem();
            });
            RunUI(() =>
            {
                StringAssert.EndsWith(editToolDlg.ToolPath, ".42.exe");
                Assert.IsFalse(editToolDlg.SearchTool.AutoInstalled);
            });
            CancelDialog(editToolDlg);
            CancelDialog(editToolListDlg);
            
            // Rerun search
            searchSucceeded = null;
            editToolDlg = ShowDialog<EditSearchToolDlg>(() => importPeptideSearchDlg.ClickNextButton());
            RunUI(() => editToolDlg.ToolPath = editToolDlg.ToolPath.Replace(".42.exe", ""));
            RunUI(editToolDlg.OkDialog); // Purposely using RunUI instead of OkDialog here (we don't need to wait for window closure, wait for searchSucceeded instead)

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", 
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(documentFile)));
        }
    }
}
