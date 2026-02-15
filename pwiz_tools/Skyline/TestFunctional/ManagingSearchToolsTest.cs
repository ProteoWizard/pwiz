/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Tools;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ManagingSearchToolsTest : AbstractFunctionalTest
    {
        string oldEncyclopediaDir => Path.GetFullPath("Tools_managingTest\\EncyclopeDIA"); // relative to working dir 
        
        /// <summary>
        /// Functional test for editing search tools
        /// </summary>
        [TestMethod]
        public void TestManagingSearchTools()
        {
            TestFilesZip = @"TestFunctional\ManagingSearchToolsTest.data";
            
            // make sure tools directory does not exist so Skyline will try to copy over "old" tools
            DirectoryEx.SafeDelete(ToolDescriptionHelpers.GetToolsDirectory());
            DirectoryEx.SafeDelete(oldEncyclopediaDir);
            using var tempDir = new TemporaryDirectory(oldEncyclopediaDir);
            
            RunFunctionalTest();
        }

        protected override void InitializeSkylineSettings()
        {
            base.InitializeSkylineSettings();

            // add an auto-installed tool to an "old" version (a location other than the current version's tools directory)
            string oldEncyclopediaFilepath = Path.Combine(oldEncyclopediaDir, "encyclopedia.bat");
            File.Copy(GetTestPath("encyclopedia.bat"), oldEncyclopediaFilepath);
            Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.EncyclopeDIA, oldEncyclopediaFilepath, "", Path.GetDirectoryName(oldEncyclopediaFilepath), true));
        }

        private EditListDlg<SettingsListBase<SearchTool>, SearchTool> EditToolListDlg { get; set; }

        string GetTestPath(string relativePath)
        {
            return Path.Combine(TestContext.GetProjectDirectory(TestFilesZip), relativePath);
        }
        
        protected override void DoTest()
        {
            // test that the auto-installed tool was copied to the current version's tools directory
            StringAssert.StartsWith(Settings.Default.SearchToolList[SearchToolType.EncyclopeDIA].Path, ToolDescriptionHelpers.GetToolsDirectory());
            AssertEx.FileExists(Settings.Default.SearchToolList[SearchToolType.EncyclopeDIA].Path);

            EditToolListDlg = ShowDialog<EditListDlg<SettingsListBase<SearchTool>, SearchTool>>(SkylineWindow.ShowSearchToolsDlg);
            
            TestAddItem(SearchToolType.MSFragger);
            TestInputErrors();

            TestAddItem(SearchToolType.CruxPercolator);

            OkDialog(EditToolListDlg);
            
            // test the tool list saved
            EditToolListDlg = ShowDialog<EditListDlg<SettingsListBase<SearchTool>, SearchTool>>(SkylineWindow.ShowSearchToolsDlg);
            RunUI(() =>
            {
                var items = EditToolListDlg.GetAll().ToList();
                Assert.AreEqual(3, items.Count);
            });
            OkDialog(EditToolListDlg);

            TestDdaSearchWithManualTool();
        }

        private void TestAddItem(SearchToolType toolName)
        {
            var editToolDlg = ShowDialog<EditSearchToolDlg>(EditToolListDlg.AddItem);
            int oldCount = 0;
            RunUI(() =>
            {
                oldCount = EditToolListDlg.GetAll().Count();
                editToolDlg.ToolName = toolName;
                editToolDlg.ToolPath = TestFilesDir.GetTestPath("msfragger.bat");
                editToolDlg.ExtraCommandlineArgs = "--foo";
            });
            OkDialog(editToolDlg);
            
            RunUI(() =>
            {
                var items = EditToolListDlg.GetAll().ToList();
                Assert.AreEqual(oldCount+1, items.Count);
                Assert.IsTrue(items.Any(t => t.Name == toolName));
            });
        }

        private void TestInputErrors()
        {
            // test duplicate tool error
            var editToolDlg = ShowDialog<EditSearchToolDlg>(EditToolListDlg.AddItem);
            RunUI(() =>
            {
                editToolDlg.ToolName = SearchToolType.MSFragger;
                editToolDlg.ToolPath = TestFilesDir.GetTestPath("msfragger.bat");
            });
            var errorDlg = ShowDialog<MessageDlg>(editToolDlg.OkDialog);
            Assert.AreEqual(string.Format(ToolsUIResources.EditSearchToolDlg_OkDialog_The_tool__0__is_already_configured_, "MSFragger"), errorDlg.Message);
            OkDialog(errorDlg);

            // test empty path error
            RunUI(() =>
            {
                editToolDlg.ToolName = SearchToolType.CruxPercolator;
                editToolDlg.ToolPath = "";
            });
            errorDlg = ShowDialog<MessageDlg>(editToolDlg.OkDialog);
            Assert.AreEqual(Resources.AddPeakCompareDlg_OkDialog_File_path_cannot_be_empty_, errorDlg.Message);
            OkDialog(errorDlg);

            // test bad path error
            const string badFileName = "msfragger.not";
            RunUI(() =>
            {
                editToolDlg.ToolPath = badFileName;
            });
            errorDlg = ShowDialog<MessageDlg>(editToolDlg.OkDialog);
            Assert.AreEqual(string.Format(ToolsUIResources.EditSearchToolDlg_OkDialog_The_file__0__does_not_exist_, badFileName), errorDlg.Message);
            OkDialog(errorDlg);
            
            CancelDialog(editToolDlg);
        }

        private void TestDdaSearchWithManualTool()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"TestFunctional\DdaSearchTest.zip");

            // add dependencies
            EditToolListDlg = ShowDialog<EditListDlg<SettingsListBase<SearchTool>, SearchTool>>(SkylineWindow.ShowSearchToolsDlg);
            foreach (var requiredFile in MsFraggerSearchEngine.FilesToDownload)
            {
                if (!Settings.Default.SearchToolList.ContainsKey(requiredFile.ToolType.ToString()))
                    TestAddItem(requiredFile.ToolType);
            }
            OkDialog(EditToolListDlg);

            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings", doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(testFilesDir.GetTestPath("TestDdaSearch.sky")));

            var TestSettings = new DdaSearchTest.DdaTestSettings
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
                ExpectedResultsFinal = new DdaSearchTest.ExpectedResults(143, 337, 425, 1275, 165)
            };
            
            var SearchFiles = new[]
            {
                testFilesDir.GetTestPath("Rpal_Std_2d_FullMS_Orbi30k_MSMS_Orbi7k_Centroid_Run1_102006_02.mzML"),
            };

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            // We're on the "Build Spectral Library" page of the wizard.

            // We're on the "Match Modifications" page. Add M+16 mod.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = SearchFiles.Select(o => (MsDataFileUri)new MsDataFilePath(o)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dda;
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.AUTO;
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                // With only 1 source, no add/remove prefix/suffix dialog

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
            });

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
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(testFilesDir.GetTestPath(TestSettings.FastaFilename));
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

            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.DDASearchPage));   // Stop to show this form during form testing
            RunUI(() =>
            {
                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });
            
            var errorDlg = WaitForOpenForm<MessageDlg>();
            StringAssert.Contains(errorDlg.DetailMessage, "Just kidding, there's no MSFragger here!");
            OkDialog(errorDlg);

            WaitForConditionUI(60000, () => searchSucceeded.HasValue);

            
            OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.Close);

            testFilesDir.Cleanup();
        }
    }
}
