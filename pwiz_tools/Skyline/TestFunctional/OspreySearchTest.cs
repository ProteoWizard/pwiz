/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// The Osprey Search wizard end to end on Osprey's Stellar subset: three sDIA mzML files and a
    /// synthetic protein FASTA whose records are the subset library's tryptic peptides, so Osprey
    /// predicts a small library from the FASTA with Carafe and searches it in a minute or two.
    /// Finish hands the .blib Osprey wrote to the Import Peptide Search wizard as the document
    /// library, and the DIA pages import the runs.
    /// </summary>
    [TestClass]
    public class OspreySearchTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Names a local Osprey.exe to test against, for a developer's loop; without it the test
        /// looks for an Osprey build beside the Skyline source and then downloads the pinned package.
        /// </summary>
        public const string ENV_OSPREY_EXE = @"SKYLINE_TEST_OSPREY_EXE";

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearch()
        {
            TestFilesZip = @"TestFunctional\OspreySearchTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string ospreyExe = EnsureOsprey();
            PrepareDocument("OspreySearchTest.sky");

            // The install gate: with Osprey registered at a path that does not exist, the menu item
            // offers the download and canceling it opens no wizard.
            const string bogusPath = @"C:\__osprey_not_installed__\Osprey.exe";
            RegisterOsprey(bogusPath);
            var downloadPrompt = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ShowOspreySearchDlg);
            AssertEx.Contains(downloadPrompt.Message, OspreyHelpers.OSPREY_VERSION);
            OkDialog(downloadPrompt, downloadPrompt.BtnCancelClick);
            AssertEx.IsNull(FindOpenForm<OspreySearchDlg>());

            RegisterOsprey(ospreyExe);
            var searchDlg = ShowDialog<OspreySearchDlg>(SkylineWindow.ShowOspreySearchDlg);

            string fastaFilepath = TestFilesDir.GetTestPath("stellar-subset-proteins.fasta");
            string[] diaFilePaths = Directory.GetFiles(TestFilesDir.FullPath, "*.mzML").OrderBy(p => p).ToArray();
            AssertEx.AreEqual(3, diaFilePaths.Length);

            // Page 0: data files
            RunUI(() => AssertEx.AreEqual(OspreySearchDlg.Pages.data_files_page, searchDlg.CurrentPage));
            RunUI(() => searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileNameWithoutExtension(p), p)).ToArray());

            // Page 1: FASTA - the library is predicted from it; the existing-library alternative is off
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.fasta_page, searchDlg.CurrentPage);
                AssertEx.IsFalse(searchDlg.UseExistingLibrary);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });

            // Page 2: modifications - the document's (Carbamidomethyl C fixed)
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.modifications_page, searchDlg.CurrentPage);
                AssertEx.AreEqual(1, searchDlg.FixedMods.Count());
                AssertEx.AreEqual(0, searchDlg.VariableMods.Count());
            });

            // Page 3: search settings - the Stellar subset is unit resolution
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.search_settings_page, searchDlg.CurrentPage);
                searchDlg.IsHighResolution = false;
                AssertEx.IsFalse(searchDlg.IsFragmentTolerancePpm);
                searchDlg.Threads = Environment.ProcessorCount;
            });
            VerifyCommandLine(searchDlg, fastaFilepath, diaFilePaths);

            // Page 4: run, and wait for Osprey; the progress bar must have moved, which is the
            // "NN%" lines of Osprey's progress reporter parsing through ProcessRunner.
            RunUI(searchDlg.NextPage);
            int maxPercent = 0;
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                WaitForConditionUI(15 * 60 * 1000, () =>
                {
                    maxPercent = Math.Max(maxPercent, searchDlg.SearchControl.PercentComplete);
                    return searchSucceeded.HasValue;
                });
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText(TestFilesDir.GetTestPath("OspreySearchControlLog.txt"), searchDlg.SearchControl.LogText);
            }
            AssertEx.IsTrue(maxPercent > 0, @"Osprey's progress lines did not advance the progress bar");

            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            AssertEx.AreEqual(docBlibPath, searchDlg.SearchControl.OutputLibraryPath);
            AssertEx.FileExists(docBlibPath);
            string workDir = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath);
            AssertEx.FileExists(Path.Combine(workDir, OspreyHelpers.STDOUT_LOG_NAME));

            // Finish: the Import Peptide Search wizard opens on its existing-library route, past the
            // library page, with the searched runs on the chromatograms page.
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.chromatograms_page, importPeptideSearchDlg.CurrentPage);
                var foundPaths = importPeptideSearchDlg.ImportResultsControl.FoundResultsFiles.Select(f => f.Path).ToHashSet();
                AssertEx.AreEqual(diaFilePaths.Length, foundPaths.Count);
                foreach (var p in diaFilePaths)
                    AssertEx.IsTrue(foundPaths.Contains(p), $@"missing pre-populated file in Found list: {p}");
                AssertEx.IsFalse(importPeptideSearchDlg.ImportResultsControl.MissingResultsFiles.Any());
            });

            // The library is the document library, as a BlibBuild result would be.
            var docLibSpec = SkylineWindow.Document.Settings.PeptideSettings.Libraries.LibrarySpecs.FirstOrDefault(s => s.IsDocumentLibrary);
            Assert.IsNotNull(docLibSpec, @"Osprey's library was not added as the document library");
            AssertEx.AreEqual(docBlibPath, docLibSpec.FilePath);

            // Three files share a common prefix and suffix, so the wizard offers to shorten the names.
            var removePrefix = ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            OkDialog(removePrefix, removePrefix.YesDialog);

            RunUI(() =>
            {
                // The match modifications page is skipped on the existing-library route.
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.transition_settings_page, importPeptideSearchDlg.CurrentPage);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.full_scan_settings_page, importPeptideSearchDlg.CurrentPage);
                importPeptideSearchDlg.FullScanSettingsControl.IsolationSchemeName =
                    PropertiesResources.IsolationSchemeList_GetDefaults_Results_only;
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.AreEqual(ImportPeptideSearchDlg.Pages.import_fasta_page, importPeptideSearchDlg.CurrentPage);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = false;
            });

            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => associateProteinsDlg.DocumentFinalCalculated);
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            }
            WaitForDocumentLoaded();

            var doc = SkylineWindow.Document;
            AssertEx.IsTrue(doc.PeptideCount > 0, @"no peptides matched the Osprey library");
            AssertEx.IsTrue(doc.Settings.HasResults);
            AssertEx.AreEqual(diaFilePaths.Length, doc.Settings.MeasuredResults.Chromatograms.Count);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// The command line the wizard builds, checked before the search runs: the one command
        /// from the FASTA, the document library as the output, and the document's digest settings.
        /// </summary>
        private void VerifyCommandLine(OspreySearchDlg searchDlg, string fastaFilepath, string[] diaFilePaths)
        {
            OspreyConfig config = null;
            RunUI(() => config = searchDlg.GetSearchConfig());
            string commandLine = OspreyHelpers.BuildCommandLine(config);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_FASTA + @" " + fastaFilepath.Quote());
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_OUTPUT + @" " +
                BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath).Quote());
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_RESOLUTION + @" " + OspreyHelpers.RESOLUTION_UNIT);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_FRAGMENT_UNIT + @" " + OspreyHelpers.FRAGMENT_UNIT_MZ);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_DEVICE + @" " + OspreyHelpers.DEVICE_AUTO);
            AssertEx.Contains(commandLine, @"--fix-mod 1 ");  // Carafe's Carbamidomethyl of C
            AssertEx.Contains(commandLine, @"--var-mod 0 ");  // Carafe's spelling of none
            AssertEx.Contains(commandLine, @"--enzyme 1 ");   // Carafe's Trypsin, matched on behavior
            AssertEx.Contains(commandLine, @"--min-pep-charge 2 ", @"--max-pep-charge 3 "); // The DIA defaults for a default document
            AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_LIBRARY + @" "));
            foreach (var path in diaFilePaths)
                AssertEx.Contains(commandLine, path.Quote());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            // The default settings, charge 2 only: the wizard predicts the library for charges 2 and
            // 3 on a default document, as the Import Peptide Search wizard's DIA transition settings
            // page sets them, and with charge 2 alone too few of the Stellar subset's precursors are
            // in its isolation window to pass the FDR.
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }

        private static void RegisterOsprey(string ospreyExe)
        {
            RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.Osprey))
                    Settings.Default.SearchToolList.Remove(Settings.Default.SearchToolList[SearchToolType.Osprey]);
                Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.Osprey,
                    ospreyExe, string.Empty, Path.GetDirectoryName(ospreyExe), false));
            });
        }

        /// <summary>
        /// The Osprey.exe to test: the one SKYLINE_TEST_OSPREY_EXE names, else a build in the Osprey
        /// project beside the Skyline source (the developer's loop), else the pinned package from
        /// the tool mirror (TeamCity).
        /// </summary>
        private string EnsureOsprey()
        {
            string fromEnvironment = Environment.GetEnvironmentVariable(ENV_OSPREY_EXE);
            if (!string.IsNullOrEmpty(fromEnvironment))
            {
                AssertEx.FileExists(fromEnvironment, ENV_OSPREY_EXE + @" names a file that does not exist");
                return fromEnvironment;
            }
            foreach (var configuration in new[] { @"Debug", @"Release" })
            {
                string sibling = Path.GetFullPath(TestContext.GetProjectDirectory(
                    Path.Combine(@"..", @"Osprey", @"Osprey", @"bin", @"x64", configuration, @"net10.0", OspreyHelpers.OSPREY_EXE)));
                if (File.Exists(sibling))
                    return sibling;
            }
            RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.Osprey))
                    Settings.Default.SearchToolList.Remove(Settings.Default.SearchToolList[SearchToolType.Osprey]);
            });
            var downloadInfo = OspreyHelpers.OspreyDownloadInfo;
            AssertEx.IsTrue(SimpleFileDownloader.DownloadRequiredFiles(new[] { downloadInfo }, new SilentProgressMonitor()),
                @"failed to download Osprey " + OspreyHelpers.OSPREY_VERSION);
            AssertEx.FileExists(downloadInfo.ToolPath);
            return downloadInfo.ToolPath;
        }
    }
}
