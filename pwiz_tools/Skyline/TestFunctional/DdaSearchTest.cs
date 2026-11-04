/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
            /// <summary>
            /// Target plus decoy hits the search engine reports scoring for each file of the final search,
            /// in file order. When set, a failed search is repeated, and so is a final search reporting
            /// other counts.
            /// </summary>
            public int[] ExpectedScoredHits { get; set; }
            public Action BeforeSettingsAction { get; set; }
            public Action ExpectedErrorAction { get; set; }
            public bool HasMissingDependencies { get; private set; }
            public bool TestDependencyErrors { get; set; }
            /// <summary>
            /// After the run, run again into the document, which then has its library, to test the
            /// question of whether to replace that library or add to it.
            /// </summary>
            public bool TestDocumentLibraryPrompt { get; set; }
        }

        private const int MAX_SEARCH_ATTEMPTS = 3;

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
                // Counts for the standalone MS Amanda (3.0.22.864) + Percolator; only percolator-validated
                // PSMs enter the library, so these are lower than the retired in-process integration's.
                ExpectedResultsFinal = new ExpectedResults(79, 192, 241, 723, 93),
                // TODO: Remove ExpectedScoredHits, and with it the search retry, once an MS Amanda release fixes
                // the race in its lazy static Peptide._aminoAcids initialization
                // (https://github.com/hgb-bin-proteomics/MSAmanda/issues/9). Threads scoring during MS Amanda's
                // startup can silently lose candidates: on a 32-thread machine about 1 in 6 searches came up
                // short, which shows in the hits MS Amanda reports scoring ("Scored 158 peptide hits and 46
                // decoy peptide hits" for the first file), so a short search is repeated.
                ExpectedScoredHits = new[] { 158 + 46, 121 + 34 },
                TestDocumentLibraryPrompt = true
            };

            RunFunctionalTest();
            Assert.IsFalse(IsRecordMode);
        }

        /// <summary>
        /// The search settings page collects a maximum number of variable modifications per peptide and
        /// hands it to every search engine through SetModifications. MS Amanda takes it as MaxNoDynModifs
        /// in the settings XML the wrapper generates, and nothing else in the pipeline reads that value
        /// back, so a settings XML built without it would look correct everywhere but the search.
        /// </summary>
        [TestMethod]
        public void TestMaxVariableModsReachesSettingsXml()
        {
            var mods = new[]
            {
                new StaticMod(@"Carbamidomethyl (C)", @"C", null, @"C2H3ON"),
                new StaticMod(@"Oxidation (M)", @"M", null, @"O").ChangeVariable(true)
            };

            using (var searchEngine = new MSAmandaSearchWrapper())
            {
                // One parameter, one control: MaxNoDynModifs is driven by the max variable mods
                // setting, so it must not also appear in the additional settings grid.
                Assert.IsFalse(searchEngine.AdditionalSettings.ContainsKey(@"MaxNoDynModifs"),
                    "MaxNoDynModifs should not be an additional setting");

                foreach (int maxVariableMods in new[] { 0, 2, 9 })
                {
                    searchEngine.SetModifications(mods, maxVariableMods);
                    AssertEx.Contains(searchEngine.BuildSettingsXml(), string.Format(CultureInfo.InvariantCulture,
                        @"<MaxNoDynModifs>{0}</MaxNoDynModifs>", maxVariableMods));
                }
            }
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
                Ms2Analyzer = DdaSearchResources.CometSearchEngine_Ms2Analyzer_Low_resolution,
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
                AdditionalSettings = new Dictionary<string, string>(),
                ExpectedResultsFinal = new ExpectedResults(106, 249, 301, 903, 121) // net8 Comet: PercolatorPepXmlAnnotator now excludes Percolator-unmatched PSMs (was 145, 338, 392, 1176, 165)
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
                Ms2Analyzer = DdaSearchResources.CometSearchEngine_Ms2Analyzer_Low_resolution,
                PrecursorTolerance = new MzTolerance(25, MzTolerance.Units.ppm),
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
                Ms2Analyzer = DdaSearchResources.CometSearchEngine_Ms2Analyzer_Low_resolution,
                PrecursorTolerance = new MzTolerance(15, MzTolerance.Units.ppm),
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
            const string MSFRAGGER_DIA_PRESET = "MSFragger high-high DIA (Q-Exactive, Q-Tof)";

            // Preset 1: MSAmanda DDA with specific tolerances and FASTA settings
            const string PRESET_1_NAME = "MSAmanda - Test Config";
            const PeptideSearchWizardDlg.Workflow PRESET_1_WORKFLOW = PeptideSearchWizardDlg.Workflow.dda;
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
            const string PRESET_2_FRAGMENT_IONS = "b,y";
            string PRESET_2_MS2_ANALYZER = DdaSearchResources.CometSearchEngine_Ms2Analyzer_Low_resolution;
            const double PRESET_2_CUTOFF = 0.01;
            const int PRESET_2_MISSED_CLEAVAGES = 1;

            Settings.Default.SearchSettingsPresets.Clear();
            Settings.Default.SearchSettingsPresets.AddDefaults();

            PrepareDocument("TestSettingsPreset.sky");

            var importPeptideSearchDlg = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            // Navigate through wizard to the search settings page, setting values along the way
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.dda;
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.AUTO;

                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Match modifications page - uncheck Carbamidomethyl for preset 1
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on mods page");
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck Carbamidomethyl (C)
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // Import FASTA page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                Assert.IsTrue(importPeptideSearchDlg.SettingsPresetVisible, "Preset should be visible on FASTA page");
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(PRESET_1_FASTA));
                importPeptideSearchDlg.ImportFastaControl.Enzyme = Settings.Default.GetEnzymeByName(PRESET_1_ENZYME);
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = PRESET_1_MISSED_CLEAVAGES;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                // DDA search settings page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
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
                // Comet controls fragment tolerance via MS2 Analyzer selection
                searchSettingsControl.FragmentIons = PRESET_2_FRAGMENT_IONS;
                searchSettingsControl.Ms2Analyzer = PRESET_2_MS2_ANALYZER;
                searchSettingsControl.CutoffScore = PRESET_2_CUTOFF;
            });
            // Navigate back to change FASTA, mods, and workflow for preset 2
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                // Set to a different enzyme than preset 1
                var enzyme2 = Settings.Default.EnzymeList.FirstOrDefault(e => e.Name != PRESET_1_ENZYME);
                Assert.IsNotNull(enzyme2, "Should have at least 2 enzymes in the enzyme list");
                importPeptideSearchDlg.ImportFastaControl.Enzyme = enzyme2;
                Assert.AreEqual(enzyme2.Name, importPeptideSearchDlg.ImportFastaControl.Enzyme.Name,
                    "Enzyme combo box should reflect the selected enzyme");
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = PRESET_2_MISSED_CLEAVAGES;
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods - re-check Carbamidomethyl for preset 2
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, true); // check Carbamidomethyl (C)

                // The preset that is still selected must not undo that change when the user leaves the page and comes back
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                Assert.IsTrue(importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.Contains(@"Carbamidomethyl (C)"));
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

            // Preset change on non-first page triggers warning dialog
            // Test cancelling: should keep preset 2 selected
            {
                var warnDlg = ShowDialog<WarnOnPresetChangeDlg>(() =>
                    importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME);
                OkDialog(warnDlg, warnDlg.ClickCancel);
            }
            RunUI(() => AssertEx.AreEqual(PRESET_2_NAME, importPeptideSearchDlg.SelectedPresetName));

            // Test OK with "don't show again": should switch to preset 1 and suppress future warnings
            {
                var warnDlg = ShowDialog<WarnOnPresetChangeDlg>(() =>
                    importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME);
                RunUI(() => warnDlg.DontShowAgain = true);
                OkDialog(warnDlg, warnDlg.OkDialog);
            }

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages = 9;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                Assert.AreEqual(PRESET_1_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                Assert.AreEqual(preset1FastaPath, importPeptideSearchDlg.ImportFastaControl.FastaFile);
            });

            // Navigate back to spectra page, apply preset 2 from there
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);

                importPeptideSearchDlg.SelectedPresetName = PRESET_2_NAME;
            });

            // Navigate forward through all pages, verifying preset 2 settings
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                Assert.AreEqual(PRESET_2_MISSED_CLEAVAGES, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);

                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
                Assert.AreEqual(PRESET_2_ENGINE, searchSettingsControl.SelectedSearchEngine);
                Assert.AreEqual(PRESET_2_PRECURSOR_TOL, searchSettingsControl.PrecursorTolerance.Value, 0.001);
                Assert.AreEqual(PRESET_2_PRECURSOR_UNIT, searchSettingsControl.PrecursorTolerance.Unit);
                Assert.AreEqual(PRESET_2_MS2_ANALYZER, searchSettingsControl.Ms2Analyzer);
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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);

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
                // FASTA file is NOT cleared when switching presets (only set if preset specifies one)

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
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);

                // Switch to preset 1 - Carbamidomethyl should be unchecked
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                var modsAfterPreset1 = importPeptideSearchDlg.MatchModificationsControl.CheckedModificationNames.ToList();
                Assert.IsFalse(modsAfterPreset1.Contains(@"Carbamidomethyl (C)"),
                    "Preset 1 should have Carbamidomethyl unchecked");

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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);

                // Both presets are DDA; manually change to PRM and save a temporary preset
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.prm;
                importPeptideSearchDlg.SaveSettingsPreset("PRM Preset");

                // Switch to preset 1 - should restore DDA workflow
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_WORKFLOW, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Preset 1 should set DDA workflow on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsAuto,
                    "Preset 1 should have Auto IRT standard");

                // Switch to PRM preset - should change to PRM
                importPeptideSearchDlg.SelectedPresetName = "PRM Preset";
                Assert.AreEqual(PeptideSearchWizardDlg.Workflow.prm, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "PRM preset should set PRM workflow on spectra page");

                // Switch to Default from PRM - should reset workflow to DDA and IRT to None
                importPeptideSearchDlg.SelectedPresetName = DEFAULT_PRESET_NAME;
                Assert.AreEqual(PeptideSearchWizardDlg.Workflow.dda, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Default preset should reset workflow to DDA from PRM on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsEmpty,
                    "Default preset should reset IRT standard to None on spectra page");

                // Switch to MSFragger DIA preset - should set DIA workflow
                importPeptideSearchDlg.SelectedPresetName = MSFRAGGER_DIA_PRESET;
                Assert.AreEqual(PeptideSearchWizardDlg.Workflow.dia, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "MSFragger DIA preset should set DIA workflow");

                // Switch to preset 1 - should restore DDA and Auto IRT
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
                Assert.AreEqual(PRESET_1_WORKFLOW, importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType,
                    "Preset 1 should restore DDA workflow on spectra page");
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards.IsAuto,
                    "Preset 1 should restore Auto IRT standard on spectra page");
            });

            // A preset chosen on a later page cannot change which pages the workflow chosen on the first page shows
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.SelectedPresetName = MSFRAGGER_DIA_PRESET;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings, not the DIA conversion page
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
            });

            // In a DIA workflow, a preset chosen on the search settings page also chooses how its engine
            // searches the DIA spectra, and the page offers that engine
            RunUI(() =>
            {
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.dia;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // DIA search method
                Assert.AreEqual(PeptideSearchWizardDlg.Pages.converter_settings_page, importPeptideSearchDlg.CurrentPage);
                importPeptideSearchDlg.ConverterSettingsControl.UseDiaUmpire = true;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.AreEqual(PeptideSearchWizardDlg.Pages.dda_search_settings_page, importPeptideSearchDlg.CurrentPage);
                Assert.AreNotEqual(SearchEngine.DIANN, searchSettingsControl.SelectedSearchEngine);

                importPeptideSearchDlg.SelectedPresetName = DiannHelpers.PRESET_ORBITRAP;
                Assert.IsFalse(importPeptideSearchDlg.ConverterSettingsControl.UseDiaUmpire);
                AssertEx.AreEqualDeep(new[] { SearchEngine.DIANN }, searchSettingsControl.SearchEngineChoices.ToArray());
                Assert.AreEqual(SearchEngine.DIANN, searchSettingsControl.SelectedSearchEngine);

                // An engine chosen over the preset's stays chosen when the page offers the engines again
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // DIA search method
                importPeptideSearchDlg.ConverterSettingsControl.UseDiaUmpire = true;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                importPeptideSearchDlg.SelectedPresetName = MSFRAGGER_DIA_PRESET;
                Assert.AreEqual(SearchEngine.MSFragger, searchSettingsControl.SelectedSearchEngine);
                searchSettingsControl.SelectedSearchEngine = SearchEngine.Comet;
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // DIA search method
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.AreEqual(SearchEngine.Comet, searchSettingsControl.SelectedSearchEngine);

                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // DIA search method
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                importPeptideSearchDlg.SelectedPresetName = PRESET_1_NAME;
            });

            // Test importing presets from .params files
            TestImportParamsFilePresets(importPeptideSearchDlg);

            // Test Edit List dialog Import and Share functionality
            TestEditListImportShare();

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
            Settings.Default.SearchSettingsPresets.Clear();
        }

        private void TestImportParamsFilePresets(RunPeptideSearchDlg importPeptideSearchDlg)
        {
            const string COMET_PRESET_NAME = "Imported Comet";
            const string MSFRAGGER_PRESET_NAME = "Imported MSFragger";

            // Create a temp Comet .params file
            var cometParamsPath = Path.Combine(TestContext.TestRunDirectory, "test_comet.params");
            File.WriteAllText(cometParamsPath, string.Join("\r\n",
                @"peptide_mass_tolerance = 20.0",
                @"peptide_mass_units = 2",
                @"fragment_bin_tol = 0.02",
                @"fragment_bin_offset = 0.0",
                @"search_enzyme_number = 1",
                @"allowed_missed_cleavage = 2",
                @"max_variable_mods_in_peptide = 3",
                @"variable_mod1 = 15.9949 M 0 3",
                @"variable_mod2 = 0.0 X 0 3",
                @"add_C_cysteine = 57.021464",
                @"add_G_glycine = 0.0",
                @"isotope_error = 2",
                @"theoretical_fragment_ions = 0",
                @"spectrum_batch_size = 15000",
                @"[COMET_ENZYME_INFO]",
                @"0.  No_enzyme              0      -           -",
                @"1.  Trypsin                1      KR          P"));

            // Create a temp MSFragger .params file
            var msFraggerParamsPath = Path.Combine(TestContext.TestRunDirectory, "test_fragger.params");
            File.WriteAllText(msFraggerParamsPath, string.Join("\r\n",
                @"precursor_true_tolerance = 20",
                @"precursor_true_units = 1",
                @"fragment_mass_tolerance = 20",
                @"fragment_mass_units = 1",
                @"search_enzyme_name_1 = stricttrypsin",
                @"search_enzyme_cut_1 = KR",
                @"search_enzyme_nocut_1 = ",
                @"search_enzyme_sense_1 = C",
                @"allowed_missed_cleavage_1 = 2",
                @"num_enzyme_termini = 2",
                @"max_variable_mods_per_peptide = 3",
                @"variable_mod_01 = 15.9949 M 3",
                @"variable_mod_02 = 42.0106 [^ 1",
                @"add_C_cysteine = 57.02146",
                @"add_G_glycine = 0.0",
                @"fragment_ion_series = b,y",
                @"calibrate_mass = 2",
                @"deisotope = 1"));

            // Test direct parser API (Comet)
            var cometPreset = SearchSettingsParamsFileParser.ImportFromFile(cometParamsPath, COMET_PRESET_NAME);
            AssertEx.AreEqual(SearchEngine.Comet, cometPreset.SearchEngine);
            AssertEx.AreEqual(20.0, cometPreset.PrecursorToleranceValue);
            AssertEx.AreEqual(MzTolerance.Units.ppm, cometPreset.PrecursorToleranceUnit);
            AssertEx.AreEqual(0.0, cometPreset.FragmentToleranceValue); // Comet uses MS2 Analyzer instead
            AssertEx.AreEqual(DdaSearchResources.CometSearchEngine_Ms2Analyzer_High_resolution, cometPreset.Ms2Analyzer);
            AssertEx.AreEqual(@"Trypsin", cometPreset.EnzymeName);
            AssertEx.AreEqual(2, cometPreset.MaxMissedCleavages);
            AssertEx.AreEqual(3, cometPreset.MaxVariableMods);
            Assert.IsTrue(cometPreset.HasExplicitModifications);
            Assert.IsTrue(cometPreset.StructuralModifications.Any(m =>
                m.Name.Contains(@"Oxid") || Math.Abs((m.MonoisotopicMass ?? 0) - 15.9949) < 0.01));
            Assert.IsTrue(cometPreset.StructuralModifications.Any(m =>
                m.Name.Contains(@"Carbamidomethyl") || Math.Abs((m.MonoisotopicMass ?? 0) - 57.021) < 0.01));
            Assert.IsNotNull(cometPreset.AdditionalSettingsXml);
            AssertEx.Contains(cometPreset.AdditionalSettingsXml, @"fragment_bin_tol");
            AssertEx.Contains(cometPreset.AdditionalSettingsXml, @"isotope_error");

            // Test direct parser API (MSFragger)
            var msFraggerPreset = SearchSettingsParamsFileParser.ImportFromFile(msFraggerParamsPath, MSFRAGGER_PRESET_NAME);
            AssertEx.AreEqual(SearchEngine.MSFragger, msFraggerPreset.SearchEngine);
            AssertEx.AreEqual(20.0, msFraggerPreset.PrecursorToleranceValue);
            AssertEx.AreEqual(MzTolerance.Units.ppm, msFraggerPreset.PrecursorToleranceUnit);
            AssertEx.AreEqual(20.0, msFraggerPreset.FragmentToleranceValue);
            AssertEx.AreEqual(MzTolerance.Units.ppm, msFraggerPreset.FragmentToleranceUnit);
            AssertEx.AreEqual(@"Trypsin", msFraggerPreset.EnzymeName);
            AssertEx.AreEqual(2, msFraggerPreset.MaxMissedCleavages);
            AssertEx.AreEqual(3, msFraggerPreset.MaxVariableMods);
            AssertEx.AreEqual(@"b,y", msFraggerPreset.FragmentIons);
            Assert.IsTrue(msFraggerPreset.HasExplicitModifications);
            Assert.IsTrue(msFraggerPreset.StructuralModifications.Any(m =>
                m.Name.Contains(@"Oxid") || Math.Abs((m.MonoisotopicMass ?? 0) - 15.9949) < 0.01));
            Assert.IsTrue(msFraggerPreset.StructuralModifications.Any(m =>
                m.Name.Contains(@"Carbamidomethyl") || Math.Abs((m.MonoisotopicMass ?? 0) - 57.021) < 0.01));
            // Acetylation on protein N-term
            Assert.IsTrue(msFraggerPreset.StructuralModifications.Any(m =>
                Math.Abs((m.MonoisotopicMass ?? 0) - 42.0106) < 0.01));

            // Test UI import (on spectra page)
            RunUI(() =>
            {
                // Import Comet params file
                importPeptideSearchDlg.ImportSettingsPreset(cometParamsPath, COMET_PRESET_NAME);

                // Verify preset was applied
                AssertEx.AreEqual(COMET_PRESET_NAME, importPeptideSearchDlg.SelectedPresetName);
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(COMET_PRESET_NAME));

                // Import MSFragger params file
                importPeptideSearchDlg.ImportSettingsPreset(msFraggerParamsPath, MSFRAGGER_PRESET_NAME);

                // Verify preset was applied
                AssertEx.AreEqual(MSFRAGGER_PRESET_NAME, importPeptideSearchDlg.SelectedPresetName);
                Assert.IsTrue(importPeptideSearchDlg.PresetNames.Contains(MSFRAGGER_PRESET_NAME));
            });

            // Clean up temp files
            File.Delete(cometParamsPath);
            File.Delete(msFraggerParamsPath);
        }

        private void TestEditListImportShare()
        {
            var sharePath = Path.Combine(TestContext.TestRunDirectory, "shared_presets.skysp");

            // Open Edit List dialog from the preset combo's "Edit list..." option
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<SearchSettingsPreset>, SearchSettingsPreset>>(
                () => FindOpenForm<RunPeptideSearchDlg>().SelectEditList());

            // Get names from the editable list (excludes built-in defaults)
            IList<string> editableNames = null;
            RunUI(() =>
            {
                editableNames = editListDlg.GetAllEdited().Select(p => p.Name).ToList();
            });
            Assert.IsTrue(editableNames.Count > 0);

            // Share all items to file
            RunUI(() => editListDlg.ShareItemsToFile(sharePath));
            Assert.IsTrue(File.Exists(sharePath));

            // Remove all user presets from the dialog
            RunUI(() =>
            {
                while (editListDlg.ListCount > 0)
                {
                    editListDlg.SelectLastItem();
                    editListDlg.RemoveItem();
                }
                AssertEx.AreEqual(0, editListDlg.ListCount);
            });

            // Import from the shared file
            RunUI(() => editListDlg.ImportItemsFromFile(sharePath));

            // Verify all editable presets were restored
            RunUI(() =>
            {
                foreach (var name in editableNames)
                {
                    Assert.IsTrue(editListDlg.GetAllEdited().Any(p => p.Name == name),
                        $"Preset '{name}' should exist after import");
                }
            });

            // OK the dialog to apply changes
            OkDialog(editListDlg, editListDlg.OkDialog);

            // Verify presets are in the settings (both defaults and user presets)
            foreach (var name in editableNames)
            {
                Assert.IsTrue(Settings.Default.SearchSettingsPresets.Any(p => p.Name == name),
                    $"Preset '{name}' should be in settings after Edit List OK");
            }

            // Test that Edit List works after removing default presets from settings
            // (regression: ReloadList assumed all defaults were present in the list)
            Settings.Default.SearchSettingsPresets.Clear();
            // Add back only one default (fewer than ExcludeDefaults expects)
            var firstDefault = Settings.Default.SearchSettingsPresets.GetDefaults(0).First();
            Settings.Default.SearchSettingsPresets.Add(firstDefault);

            // Opening Edit List should not throw despite missing defaults
            var editListDlg2 = ShowDialog<EditListDlg<SettingsListBase<SearchSettingsPreset>, SearchSettingsPreset>>(
                () => FindOpenForm<RunPeptideSearchDlg>().SelectEditList());
            OkDialog(editListDlg2, editListDlg2.OkDialog);

            // Restore defaults for cleanup
            Settings.Default.SearchSettingsPresets.Clear();
            Settings.Default.SearchSettingsPresets.AddDefaults();

            File.Delete(sharePath);
        }

        protected override bool IsRecordMode => false;

        /// <summary>
        /// Whether to throw away the downloaded tools first, so that the download and install path gets
        /// tested too. Not when running as a parallel test client: the downloaded archives live in a
        /// cache directory shared by every client, so deleting them pulls the file out from under
        /// whichever other client happens to be installing from it at the time.
        /// </summary>
        private bool RedownloadTools => !IsRecordMode && IsPass0 && !IsParallelClient;

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
            var importPeptideSearchDlg = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.prm; // will go back and switch to DDA
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.AUTO;
                Assert.IsTrue(importPeptideSearchDlg.BuildPepSearchLibControl.IrtRunOptionsVisible);
                importPeptideSearchDlg.BuildPepSearchLibControl.RecalibrateIrtStandard = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                // With only 1 source, no add/remove prefix/suffix dialog

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                // Test going back and switching workflow to DDA. This used to cause an exception.
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.dda;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
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

                // Test back/next buttons, and that the checked modifications survive leaving the page in
                // either direction and coming back
                RunUI(() =>
                {
                    var modsControl = importPeptideSearchDlg.MatchModificationsControl;
                    var firstMod = modsControl.MatchedModifications.First();
                    bool firstModChecked = modsControl.CheckedModificationNames.Contains(firstMod);
                    modsControl.ChangeItem(0, !firstModChecked);
                    var checkedMods = string.Join(@", ", modsControl.CheckedModificationNames);

                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                    AssertEx.AreEqual(checkedMods, string.Join(@", ", modsControl.CheckedModificationNames));

                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                    Assert.IsTrue(importPeptideSearchDlg.ClickBackButton());

                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                    AssertEx.AreEqual(checkedMods, string.Join(@", ", modsControl.CheckedModificationNames));

                    modsControl.ChangeItem(0, firstModChecked);
                });
            }

            // We're on the "Import FASTA" page: everything the search needs comes before the extraction settings.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                Assert.IsNotNull(importPeptideSearchDlg.ImportFastaControl.ProteinAssociationOptions);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(TestSettings.FastaFilename));
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            // We're on the "Adjust Search Settings" page
            TryWaitForOpenForm(typeof(PeptideSearchWizardDlg.DDASearchSettingsPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
            });

            TestSettings.BeforeSettingsAction?.Invoke();

            RunUI(() =>
            {
                importPeptideSearchDlg.SearchSettingsControl.SelectedSearchEngine = TestSettings.SearchEngine;
                foreach (var setting in TestSettings.AdditionalSettings)
                    importPeptideSearchDlg.SearchSettingsControl.SetAdditionalSetting(setting.Key, setting.Value);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = TestSettings.PrecursorTolerance;
                if (TestSettings.FragmentTolerance != null)
                    importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = TestSettings.FragmentTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = TestSettings.FragmentIons;
                importPeptideSearchDlg.SearchSettingsControl.Ms2Analyzer = TestSettings.Ms2Analyzer;
                importPeptideSearchDlg.SearchSettingsControl.CutoffScore = 0.1;
            });

            // Save a preset with the current settings before starting the search
            const string SEARCH_PRESET_NAME = "DDA Search Preset";
            RunUI(() => importPeptideSearchDlg.SaveSettingsPreset(SEARCH_PRESET_NAME));

            // Leaving the search settings page makes sure the search tools are installed
            SkylineWindow.BeginInvoke(() => importPeptideSearchDlg.ClickNextButton());
            if (RedownloadTools || TestSettings.HasMissingDependencies)
                AcceptSearchToolDownloads();

            // We're on the MS1 full scan settings page, the last page before the run.
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.full_scan_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
                // This first run only searches: the test cancels it, or runs it again, so it must not
                // go on to apply a document if the search finishes first
                importPeptideSearchDlg.AcceptSearchResults = () => false;
            });

            SkylineWindow.BeginInvoke(() => importPeptideSearchDlg.ClickNextButton()); // Run
            TryWaitForOpenForm(typeof(PeptideSearchWizardDlg.DDASearchPage));   // Stop to show this form during form testing

            if (TestSettings.TestDependencyErrors)
            {
                TestDependencyErrors(importPeptideSearchDlg);
                return;
            }

            if (errorExpected)
            {
                TestSettings.ExpectedErrorAction?.Invoke();
                WaitForConditionUI(60000, () => importPeptideSearchDlg.RunSucceeded.HasValue);
                OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
                return;
            }

            // Test cannot-close-during-search, then cancel the run
            SkylineWindow.BeginInvoke(importPeptideSearchDlg.Close); // try to close (don't wait for return)
            var cannotCloseDuringSearchDlg = WaitForOpenForm<MessageDlg>();
            Assert.AreEqual(PeptideSearchResources.SearchControl_CanWizardClose_Cannot_close_wizard_while_the_search_is_running_,
                cannotCloseDuringSearchDlg.Message);
            OkDialog(cannotCloseDuringSearchDlg, cannotCloseDuringSearchDlg.ClickNo);

            // Cancel the run (but don't close wizard)
            RunUI(importPeptideSearchDlg.SearchControl.Cancel);
            WaitForConditionUI(60000, () => importPeptideSearchDlg.RunSucceeded.HasValue);
            RunUI(() =>
            {
                Assert.IsFalse(importPeptideSearchDlg.RunSucceeded.Value);
                Assert.IsFalse(importPeptideSearchDlg.IsRunCompleted);
            });

            // The diagnosis for a search Percolator scored nothing in, now that percolator is installed.
            // It runs percolator, which writes its help to stderr, and this used to deadlock. A hang is
            // killed after 30 seconds with the same message, so the time taken is what tells them apart.
            if (TestSettings.SearchEngine == SearchEngine.MSAmanda)
            {
                var diagnosisTime = Stopwatch.StartNew();
                AssertEx.AreEqual(string.Format(DdaSearchResources.MSAmandaSearchWrapper_Percolator_ran_but_scored_nothing,
                    MSAmandaSearchWrapper.PercolatorDirectory), MSAmandaSearchWrapper.DiagnosePercolator());
                AssertEx.IsTrue(diagnosisTime.Elapsed < TimeSpan.FromSeconds(15),
                    $@"Percolator diagnosis took {diagnosisTime.Elapsed.TotalSeconds:F1} seconds");
            }

            // Go back and test 2 input files with the same name
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Spectra
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);

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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);
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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.match_modifications_page);
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(0, false); // uncheck C+57
                for (int i = 1; i < importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.Count(); ++i)
                    importPeptideSearchDlg.MatchModificationsControl.ChangeItem(i, true); // check everything else
                importPeptideSearchDlg.MatchModificationsControl.ChangeItem(importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.Count() - 1, false); // uncheck combined mod
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // FASTA
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod = DecoyGeneration.REVERSE_SEQUENCE;
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = true;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3, 4 };
            });

            try
            {
                RunUntilSearchAccepted(importPeptideSearchDlg, TestSettings.ExpectedScoredHits);

                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.RunSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText);

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
                // The run applied the document and started importing results: nothing to go back to
                Assert.IsTrue(importPeptideSearchDlg.IsRunCompleted);
                Assert.IsFalse(importPeptideSearchDlg.IsBackButtonEnabled);

                var proteinAssociation = importPeptideSearchDlg.LastRunResult.ProteinAssociation;

                // The FASTA import's message for each protein it adds stays out of the run log
                var logText = importPeptideSearchDlg.SearchControl.LogText;
                foreach (var proteinName in proteinAssociation.DocumentFinal.PeptideGroups.Select(group => group.Name))
                    AssertEx.DoesNotContain(logText, string.Format(ModelResources.FastaImporter_Import_Adding_protein__0__, proteinName));
                // Nor does the placeholder name of a standard made from library peptides
                AssertEx.DoesNotContain(logText, XmlNamedElement.NAME_INTERNAL);
                // Some proteins have only standard peptides, so the summary explains the lower protein count
                Assert.IsTrue(proteinAssociation.IrtStandardProteinsRemoved > 0);
                var documentFinal = proteinAssociation.DocumentFinal;
                AssertEx.Contains(logText, string.Format(PeptideSearchResources.RunPeptideSearchDlg_AddSummary_After_dropping_proteins_in_the_iRT_standard__the_document_has__0__proteins___1__peptides___2__precursors_and__3__transitions_,
                    documentFinal.MoleculeGroupCount, documentFinal.MoleculeCount, documentFinal.MoleculeTransitionGroupCount, documentFinal.MoleculeTransitionCount));

                AssociateProteinsResults.GetFinalTargetCounts(proteinAssociation.DocumentFinal, proteinAssociation.ProteinAssociation,
                    out var proteinCount, out var peptideCount, out var precursorCount, out var transitionCount, out var unmappedOrRemoved);
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
            OkDialog(importPeptideSearchDlg, () => importPeptideSearchDlg.ClickNextButton()); // Finish
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());

            if (TestSettings.TestDocumentLibraryPrompt)
                TestDocumentLibraryPrompt(SEARCH_PRESET_NAME);

            // Verify the preset can be loaded in a new wizard with all settings restored
            VerifyPresetInNewWizard(SEARCH_PRESET_NAME);
        }

        /// <summary>
        /// Answers the search tool download prompts that leaving the search settings page shows when
        /// a tool is missing.
        /// </summary>
        private void AcceptSearchToolDownloads()
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

            // Every engine reaching this point may still need the generic download-
            // confirmation dialog handled. MSFragger's binary is downloaded by
            // MsFraggerDownloadDlg above, but its Java and Crux dependencies still come
            // through here; MSAmanda and the other engines download their tool here too
            // (all on-demand on net8). Excluding any engine leaves its dialog unhandled.
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

        /// <summary>
        /// Runs from the last page before the run and waits for the run to finish. Tests that set
        /// <see cref="DdaTestSettings.ExpectedScoredHits"/> repeat a search that fails or that the search
        /// engine reports scoring other than <paramref name="expectedScoredHits"/>: the search results are
        /// rejected before the library is built, and the test goes back and runs again. After the last
        /// attempt the results are accepted, so a search that keeps coming up short still fails.
        /// </summary>
        /// <param name="importPeptideSearchDlg">The wizard, on the last page before the run</param>
        /// <param name="expectedScoredHits">Scored hits per file, or null to repeat only a failed search</param>
        private void RunUntilSearchAccepted(RunPeptideSearchDlg importPeptideSearchDlg, int[] expectedScoredHits)
        {
            for (int attempt = 1; ; attempt++)
            {
                bool lastAttempt = attempt >= MAX_SEARCH_ATTEMPTS || TestSettings.ExpectedScoredHits == null;
                var acceptSearchResults = MakeAcceptSearchResults(importPeptideSearchDlg, lastAttempt ? null : expectedScoredHits);
                RunUI(() => importPeptideSearchDlg.AcceptSearchResults = acceptSearchResults);
                SkylineWindow.BeginInvoke(() => importPeptideSearchDlg.ClickNextButton()); // Run
                WaitForConditionUI(60000, () => importPeptideSearchDlg.RunSucceeded.HasValue,
                    () => importPeptideSearchDlg.SearchControl.LogText);
                if (lastAttempt || GetUIValue(() => importPeptideSearchDlg.RunSucceeded.Value))
                    return;

                // Report the log of a failed attempt, so it is not lost to the retry.
                Console.WriteLine(GetUIValue(() => importPeptideSearchDlg.SearchControl.LogText));
                RunUI(() =>
                {
                    AssertEx.IsTrue(importPeptideSearchDlg.ClickBackButton());
                    AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.full_scan_settings_page);
                });
            }
        }

        /// <summary>
        /// Accepts the search results when the search scored <paramref name="expectedScoredHits"/>, or
        /// always when that is null.
        /// </summary>
        private Func<bool> MakeAcceptSearchResults(RunPeptideSearchDlg importPeptideSearchDlg, int[] expectedScoredHits)
        {
            return () =>
            {
                if (expectedScoredHits == null)
                    return true;
                var scoredHits = GetScoredHits(importPeptideSearchDlg.SearchControl.LogText);
                if (IsRecordMode)
                {
                    Console.WriteLine($@"ExpectedScoredHits = new[] {{ {string.Join(@", ", scoredHits)} }}");
                    return true;
                }
                if (scoredHits.SequenceEqual(expectedScoredHits))
                    return true;
                Console.WriteLine($@"Search scored {string.Join(@", ", scoredHits)} hits, expected {string.Join(@", ", expectedScoredHits)} - repeating search");
                return false;
            };
        }

        /// <summary>
        /// Runs again into the document, which now has its library: clicking Run asks whether to replace
        /// that library or add the new results to it, before anything runs, and Cancel leaves the user on
        /// the last page. Searching one of the two files again shows which happened: adding keeps the
        /// other file's results, replacing does not.
        /// </summary>
        private void TestDocumentLibraryPrompt(string presetName)
        {
            AssertEx.AreEqual(2, GetDocumentLibraryFileCount());

            RunAgainOnOneFile(presetName, false);
            AssertEx.AreEqual(2, GetDocumentLibraryFileCount());

            RunAgainOnOneFile(presetName, true);
            AssertEx.AreEqual(1, GetDocumentLibraryFileCount());
        }

        private int GetDocumentLibraryFileCount()
        {
            WaitForDocumentLoaded();
            return GetUIValue(() =>
            {
                var libraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
                AssertEx.IsTrue(libraries.TryGetDocumentLibrary(out var documentLibrary));
                return documentLibrary.LibraryFiles.Count;
            });
        }

        private void RunAgainOnOneFile(string presetName, bool overwrite)
        {
            var dlg = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);
            RunUI(() =>
            {
                dlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                dlg.SelectedPresetName = presetName; // The search's settings, including its FASTA
                AssertEx.IsTrue(dlg.ClickNextButton()); // Modifications
                AssertEx.IsTrue(dlg.ClickNextButton()); // FASTA
                AssertEx.IsTrue(dlg.ClickNextButton()); // Search settings
                AssertEx.IsTrue(dlg.ClickNextButton()); // Full scan settings, the last page before the run
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.full_scan_settings_page, dlg.CurrentPage);
                dlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };
            });

            var docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            var expectedQuestion = string.Format(PeptideSearchResources.RunPeptideSearchDlg_PrepareRun_A_spectral_library_already_exists_at___0___Overwrite_it__or_append_the_new_results_to_it_,
                docBlibPath);
            var question = ShowDialog<MultiButtonMsgDlg>(() => dlg.ClickNextButton()); // Run
            RunUI(() => AssertEx.AreEqual(expectedQuestion, question.Message));
            OkDialog(question, question.BtnCancelClick);
            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.full_scan_settings_page, dlg.CurrentPage);
                AssertEx.IsFalse(dlg.RunSucceeded.HasValue);
            });

            question = ShowDialog<MultiButtonMsgDlg>(() => dlg.ClickNextButton()); // Run
            OkDialog(question, overwrite ? (Action) question.BtnYesClick : question.Btn1Click);
            WaitForConditionUI(120000, () => dlg.RunSucceeded.HasValue, () => dlg.SearchControl.LogText);
            RunUI(() => AssertEx.IsTrue(dlg.RunSucceeded.Value, dlg.SearchControl.LogText));
            OkDialog(dlg, () => dlg.ClickNextButton()); // Finish
        }

        private static List<int> GetScoredHits(string searchLog)
        {
            // MS Amanda's own log line, e.g. "Scored 158 peptide hits and 46 decoy peptide hits in 0.12 sec"
            return Regex.Matches(searchLog, @"Scored (\d+) peptide hits and (\d+) decoy peptide hits")
                .Select(match => int.Parse(match.Groups[1].Value) + int.Parse(match.Groups[2].Value)).ToList();
        }

        private void VerifyPresetInNewWizard(string presetName)
        {
            var preset = Settings.Default.SearchSettingsPresets.FirstOrDefault(p => p.Name == presetName);
            Assert.IsNotNull(preset, $"Preset '{presetName}' should exist in settings");

            // The DDA search just imported results. That makes the document briefly report
            // IsLoaded==true while mProphet auto-training is still pending (IsAutoTrain==true):
            // AutoTrainManager only starts training once the document first becomes loaded, then
            // commits a retrained document that re-triggers background retention-time alignment
            // (ResultFileAlignments), making the document not-loaded again for a moment.
            // ShowRunPeptideSearchDlg refuses to open unless the *committed* Document.IsLoaded, so
            // waiting only on IsLoaded (or WaitForDocumentLoaded, which checks DocumentUI) returns
            // too early and lets the wizard reopen race that not-loaded window - the intermittent
            // "document must be fully loaded before running a peptide search" failure seen on
            // slower/contended nightly agents. Wait for the document to be fully settled: loaded
            // AND auto-train complete.
            WaitForConditionUI(() => SkylineWindow.Document.IsLoaded &&
                                     !SkylineWindow.Document.Settings.PeptideSettings.Integration.IsAutoTrain,
                () => TextUtil.LineSeparate("Document not fully settled before reopening Run Peptide Search wizard:",
                    "IsAutoTrain=" + SkylineWindow.Document.Settings.PeptideSettings.Integration.IsAutoTrain,
                    TextUtil.LineSeparate(SkylineWindow.Document.NonLoadedStateDescriptions)));

            // Regression tripwire for the race above: at this point auto-train must be finished. If
            // the wait is ever weakened back to a plain IsLoaded/WaitForDocumentLoaded check this
            // fails deterministically, because auto-train is still pending the instant such a wait
            // returns (it needs ~tens of ms after the document first loads to commit).
            RunUI(() => Assert.IsFalse(SkylineWindow.Document.Settings.PeptideSettings.Integration.IsAutoTrain,
                "Document not fully settled before reopening wizard - mProphet auto-train still pending"));

            var importPeptideSearchDlg2 = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == PeptideSearchWizardDlg.Pages.spectra_page);
                importPeptideSearchDlg2.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).Take(1).ToArray();
                importPeptideSearchDlg2.BuildPepSearchLibControl.WorkflowType = PeptideSearchWizardDlg.Workflow.dda;

                // Apply the preset from the first page
                importPeptideSearchDlg2.SelectedPresetName = presetName;
            });

            // Navigate through wizard verifying settings on relevant pages
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // Mods
                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // FASTA
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == PeptideSearchWizardDlg.Pages.import_fasta_page);

                // Verify FASTA settings
                if (!string.IsNullOrEmpty(preset.EnzymeName))
                    Assert.AreEqual(preset.EnzymeName, importPeptideSearchDlg2.ImportFastaControl.Enzyme.Name,
                        "Enzyme should be restored from preset in new wizard");
                Assert.AreEqual(preset.MaxMissedCleavages, importPeptideSearchDlg2.ImportFastaControl.MaxMissedCleavages,
                    "Missed cleavages should be restored from preset in new wizard");

                Assert.IsTrue(importPeptideSearchDlg2.ClickNextButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg2.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
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

        /// <summary>
        /// Starts on the run page with a run that stops after its search (AcceptSearchResults is false), so
        /// the test can go back to the search settings page, whose leaving checks the search tools, and run again.
        /// </summary>
        private void TestDependencyErrors(RunPeptideSearchDlg importPeptideSearchDlg)
        {
            bool? searchSucceeded = null;
            RunUI(() => importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success);
            
            // Cancel search (but don't close wizard)
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.SearchControl.CanCancel);  // Avoid a long wait on a cancel that does nothing
                importPeptideSearchDlg.SearchControl.Cancel();
            });
            WaitForConditionUI(60000, () => importPeptideSearchDlg.RunSucceeded.HasValue);

            GoBackToSearchSettings(importPeptideSearchDlg);

            // Delete one of the auto-installed dependencies; Skyline will automatically try to redownload it
            var requiredFile = SearchSettingsControl.GetSearchEngineRequiredFiles(TestSettings.SearchEngine).First();
            DirectoryEx.SafeDelete(requiredFile.InstallPath);

            // Rerun search
            searchSucceeded = null;
            SkylineWindow.BeginInvoke(() => importPeptideSearchDlg.ClickNextButton());

            var downloaderDlg = TryWaitForOpenForm<MultiButtonMsgDlg>(2000);
            if (downloaderDlg != null)
            {
                OkDialog(downloaderDlg, downloaderDlg.ClickYes);
                var waitDlg = WaitForOpenForm<LongWaitDlg>();
                WaitForClosedForm(waitDlg);
            }
            RunToSearchEnd(importPeptideSearchDlg, () => searchSucceeded);
            Assert.IsTrue(searchSucceeded.Value);

            GoBackToSearchSettings(importPeptideSearchDlg);

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
            RunUI(editToolDlg.OkDialog); // Purposely using RunUI instead of OkDialog here (we don't need to wait for window closure, wait for the page instead)

            RunToSearchEnd(importPeptideSearchDlg, () => searchSucceeded);
            Assert.IsTrue(searchSucceeded.Value);

            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
        }

        /// <summary>
        /// From the run page, after a run stopped, goes back to the search settings page.
        /// </summary>
        private void GoBackToSearchSettings(RunPeptideSearchDlg importPeptideSearchDlg)
        {
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Full scan
                Assert.IsTrue(importPeptideSearchDlg.ClickBackButton()); // Search settings
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.dda_search_settings_page);
            });
        }

        /// <summary>
        /// After leaving the search settings page, waits for the full-scan page, runs, and waits for the
        /// search to finish and the run (which stops after the search) to end.
        /// </summary>
        private void RunToSearchEnd(RunPeptideSearchDlg importPeptideSearchDlg, Func<bool?> searchSucceeded)
        {
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == PeptideSearchWizardDlg.Pages.full_scan_settings_page);
            SkylineWindow.BeginInvoke(() => importPeptideSearchDlg.ClickNextButton()); // Run
            WaitForConditionUI(60000, () => searchSucceeded().HasValue);
            WaitForConditionUI(60000, () => importPeptideSearchDlg.RunSucceeded.HasValue);
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
