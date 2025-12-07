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
                SearchEngine = SearchSettingsControl.SearchEngine.MSAmanda,
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
                SearchEngine = SearchSettingsControl.SearchEngine.MSGFPlus,
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
                SearchEngine = SearchSettingsControl.SearchEngine.Comet,
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
                SearchEngine = SearchSettingsControl.SearchEngine.Comet,
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
                SearchEngine = SearchSettingsControl.SearchEngine.Tide,
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
                SearchEngine = SearchSettingsControl.SearchEngine.MSFragger,
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
                SearchEngine = SearchSettingsControl.SearchEngine.MSFragger,
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
                SearchEngine = SearchSettingsControl.SearchEngine.Comet,
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

                // Run the search
                //Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            SkylineWindow.BeginInvoke(new Action(() => importPeptideSearchDlg.ClickNextButton()));

            if (RedownloadTools || TestSettings.HasMissingDependencies)
            {
                if (TestSettings.SearchEngine == SearchSettingsControl.SearchEngine.MSFragger)
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

                if (TestSettings.SearchEngine != SearchSettingsControl.SearchEngine.MSAmanda)
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

            if (!errorExpected)
            {
                SkylineWindow.BeginInvoke(new Action(importPeptideSearchDlg.Close)); // try to close (don't wait for return)
                var cannotCloseDuringSearchDlg = WaitForOpenForm<MessageDlg>();
                Assert.AreEqual(PeptideSearchResources.SearchControl_CanWizardClose_Cannot_close_wizard_while_the_search_is_running_,
                    cannotCloseDuringSearchDlg.Message);
                OkDialog(cannotCloseDuringSearchDlg, cannotCloseDuringSearchDlg.ClickNo);

                // Cancel search (but don't close wizard)
                RunUI(importPeptideSearchDlg.SearchControl.Cancel);
            }
            else // errorExpected
            {
                TestSettings.ExpectedErrorAction?.Invoke();
                WaitForConditionUI(60000, () => searchSucceeded.HasValue);
                OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.ClickCancelButton);
                return;
            }

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
                    if (TestSettings.SearchEngine == SearchSettingsControl.SearchEngine.MSFragger)
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
            
            if (TestSettings.SearchEngine == SearchSettingsControl.SearchEngine.MSFragger)
                StringAssert.Matches(Settings.Default.SearchToolList[SearchToolType.Java].ExtraCommandlineArgs, new Regex(@"-Xmx\d+M"));
            
            if (TestSettings.SearchEngine == SearchSettingsControl.SearchEngine.Comet)
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
