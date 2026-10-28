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
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
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
    /// library, and the DIA pages import the runs. A second run on the same document then
    /// searches the library the first run predicted, in place of predicting one, overwriting the
    /// document library with the user's consent.
    /// </summary>
    [TestClass]
    public class OspreySearchTest : AbstractFunctionalTestEx
    {
        /// <summary>
        /// Names a local Osprey.exe to test against, for a developer's loop; without it the test
        /// looks for an Osprey build beside the Skyline source and then downloads the pinned package.
        /// </summary>
        public const string ENV_OSPREY_EXE = OspreyTestUtil.ENV_OSPREY_EXE;

        /// <summary>
        /// Osprey names the library it predicts from the FASTA after the output, in the working
        /// folder: &lt;output stem&gt;.predicted.blib.
        /// </summary>
        private const string PREDICTED_LIBRARY_SUFFIX = @".predicted";

        private const string WIZARD_LOG_FIRST_RUN = @"OspreySearchControlLog.txt";
        private const string WIZARD_LOG_SECOND_RUN = @"OspreySearchControlLog-rerun.txt";

        /// <summary>How long <see cref="RunSearch"/> waits for Osprey on the subset; a perf test on full runs passes its own.</summary>
        public const int SEARCH_TIMEOUT_MILLIS = 15 * 60 * 1000;

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestOspreySearch()
        {
            TestFilesZip = @"TestFunctional\OspreySearchTest.zip";
            RunFunctionalTest();
        }

        /// <summary>The settings the test gives in the Additional Settings grid: a flag of every search, and a prediction's collision energy.</summary>
        private const string ADDITIONAL_FLAG = @"verbose";
        private const string ADDITIONAL_NCE = @"nce";
        private const string ADDITIONAL_NCE_VALUE = @"30";

        /// <summary>The settings preset the test saves from the first wizard's page and finds in the second's.</summary>
        private const string PRESET_NAME = @"Night preset";
        private const double PRESET_CHANGED_RUN_FDR = 0.05;

        protected override void DoTest()
        {
            string ospreyExe = OspreyTestUtil.EnsureOsprey(TestContext);
            // The additional settings start at Osprey's defaults, whatever an earlier wizard on this machine left
            Settings.Default.OspreyAdditionalSettings = null;
            Settings.Default.OspreySettingsPresets.RemoveKey(PRESET_NAME);
            OspreyProperties.ClearCache();
            VerifyLatestBuildOnTeamCity();
            PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTest.sky"));

            // The install gate: with Osprey registered at a path that does not exist, the menu item
            // offers the download and canceling it opens no wizard. A developer build, as the tests
            // run, offers the latest successful master build; a release the pinned one. On TeamCity
            // that package is the one EnsureOsprey just installed in the Tools folder, which the gate
            // would register again instead of offering, so it is moved aside for the check.
            const string bogusPath = @"C:\__osprey_not_installed__\Osprey.exe";
            OspreyTestUtil.RegisterOsprey(bogusPath);
            AssertEx.IsFalse(OspreyHelpers.UsesPinnedPackage);
            var package = OspreyHelpers.GetPackageDownloadInfo();
            string packageDir = Path.GetDirectoryName(package.ToolPath);
            string asideDir = packageDir + @"-aside";
            bool packageInstalled = Directory.Exists(packageDir);
            if (packageInstalled)
                Directory.Move(packageDir, asideDir);
            try
            {
                var downloadPrompt = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ShowOspreySearchDlg);
                AssertEx.Contains(downloadPrompt.Message, OspreyHelpers.GetPackageVersion(package));
                OkDialog(downloadPrompt, downloadPrompt.BtnCancelClick);
                AssertEx.IsNull(FindOpenForm<OspreySearchDlg>());
            }
            finally
            {
                if (packageInstalled)
                    Directory.Move(asideDir, packageDir);
            }

            OspreyTestUtil.RegisterOsprey(ospreyExe);

            string fastaFilepath = TestFilesDir.GetTestPath("stellar-subset-proteins.fasta");
            string[] diaFilePaths = Directory.GetFiles(TestFilesDir.FullPath, "*.mzML").OrderBy(p => p).ToArray();
            AssertEx.AreEqual(3, diaFilePaths.Length);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            string workDir = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath);

            // The first run: the library is predicted from the FASTA; the existing-library
            // alternative is off.
            var searchDlg = OpenWizardOnFastaPage(diaFilePaths);
            RunUI(() =>
            {
                AssertEx.IsFalse(searchDlg.UseExistingLibrary);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });
            AdvanceToSearchSettings(searchDlg);
            SetAdditionalSettings(searchDlg, true);
            SaveAndReapplyPreset(searchDlg);
            VerifyCommandLine(searchDlg, fastaFilepath, diaFilePaths, null);
            RunSearch(searchDlg, docBlibPath, workDir, TestFilesDir.GetTestPath(WIZARD_LOG_FIRST_RUN));
            var additionalSettings = searchDlg.SearchSettingsControl.AdditionalSettings;
            FinishImport(searchDlg, diaFilePaths, docBlibPath);
            VerifyAuditLogSettings(additionalSettings, true);

            // The second run, on the document the first one made: Osprey searches the library
            // it predicted in the first run, and overwrites the document library.
            string predictedLibraryPath = Path.Combine(workDir,
                Path.GetFileNameWithoutExtension(SkylineWindow.DocumentFilePath) + PREDICTED_LIBRARY_SUFFIX + BiblioSpecLiteSpec.EXT);
            AssertEx.FileExists(predictedLibraryPath, @"the first run did not leave its predicted library in the working folder");
            var libraryBefore = GetDocumentLibrary();

            searchDlg = OpenWizardOnFastaPage(diaFilePaths);
            RunUI(() =>
            {
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
                searchDlg.UseExistingLibrary = true;
                // The document library is the search's output, so it is refused as the input.
                searchDlg.ExistingLibraryPath = docBlibPath;
            });
            var refusal = ShowDialog<MessageDlg>(searchDlg.NextPage);
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.OspreySearchDlg_ValidateExistingLibraryPath_The_existing_library_cannot_be_the_document_library__0___which_the_search_writes_,
                docBlibPath), refusal.Message);
            OkDialog(refusal, refusal.OkDialog);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.fasta_page, searchDlg.CurrentPage);
                searchDlg.ExistingLibraryPath = predictedLibraryPath;
            });
            AdvanceToSearchSettings(searchDlg);
            VerifyPresetPersisted(searchDlg);
            SetAdditionalSettings(searchDlg, false);
            VerifyCommandLine(searchDlg, fastaFilepath, diaFilePaths, predictedLibraryPath);
            RunSearch(searchDlg, docBlibPath, workDir, TestFilesDir.GetTestPath(WIZARD_LOG_SECOND_RUN));
            additionalSettings = searchDlg.SearchSettingsControl.AdditionalSettings;
            FinishImport(searchDlg, diaFilePaths, docBlibPath);
            VerifyAuditLogSettings(additionalSettings, false);

            // The document reads the library Osprey rewrote, not the one it loaded before the
            // overwrite: the second run replaces the document's Library object.
            AssertEx.IsFalse(ReferenceEquals(libraryBefore, GetDocumentLibrary()),
                @"the document still holds the library object loaded before the overwrite");
        }

        /// <summary>
        /// Opens the wizard, fills the data files page with <paramref name="diaFilePaths"/> and
        /// moves to the FASTA page.
        /// </summary>
        public static OspreySearchDlg OpenWizardOnFastaPage(string[] diaFilePaths)
        {
            var searchDlg = ShowDialog<OspreySearchDlg>(SkylineWindow.ShowOspreySearchDlg);
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchDlg.Pages.data_files_page, searchDlg.CurrentPage);
                searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                    .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileNameWithoutExtension(p), p)).ToArray();
                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.fasta_page, searchDlg.CurrentPage);
            });
            return searchDlg;
        }

        /// <summary>
        /// From the FASTA page (or the library model page, which a predicted library gets after it
        /// and whose defaults predict with the pretrained models) through the modifications page
        /// (the document's: Carbamidomethyl C fixed) to the search settings page, set for the
        /// Stellar subset's unit resolution.
        /// </summary>
        public static void AdvanceToSearchSettings(OspreySearchDlg searchDlg)
        {
            RunUI(() =>
            {
                if (searchDlg.CurrentPage == OspreySearchDlg.Pages.fasta_page)
                {
                    searchDlg.NextPage();
                    if (!searchDlg.UseExistingLibrary)
                    {
                        AssertEx.AreEqual(OspreySearchDlg.Pages.library_model_page, searchDlg.CurrentPage);
                        AssertEx.IsTrue(searchDlg.UseExistingModel);
                        AssertEx.IsTrue(searchDlg.Model.IsDefault);
                    }
                }
                if (searchDlg.CurrentPage == OspreySearchDlg.Pages.library_model_page)
                    searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.modifications_page, searchDlg.CurrentPage);
                AssertEx.AreEqual(1, searchDlg.FixedMods.Count());
                AssertEx.AreEqual(0, searchDlg.VariableMods.Count());

                searchDlg.NextPage();
                AssertEx.AreEqual(OspreySearchDlg.Pages.search_settings_page, searchDlg.CurrentPage);
                searchDlg.IsHighResolution = false;
                AssertEx.IsFalse(searchDlg.IsFragmentTolerancePpm);
                searchDlg.Threads = Environment.ProcessorCount;
            });
        }

        /// <summary>
        /// The import's audit log entry (the Import Peptide Search wizard's) records the Osprey
        /// search's settings: the pages' settings under their names, and the additional settings
        /// the run changed, each as the setting renders itself. Only the settings in the run's
        /// scopes are recorded: the flag is a search setting and is there for both runs; the
        /// collision energy is a library setting, so a search of an existing library
        /// (<paramref name="predictsLibrary"/> false) leaves it out though its value is kept.
        /// </summary>
        private static void VerifyAuditLogSettings(OspreyAdditionalSettings settings, bool predictsLibrary)
        {
            var entry = SkylineWindow.Document.AuditLog.AuditLogEntries;
            AssertEx.AreEqual(MessageType.imported_peptide_search, entry.UndoRedo.Type);
            string allInfo = TextUtil.LineSeparate(entry.AllInfo.Select(message => message.ToString()));
            AssertEx.Contains(allInfo, PropertyNames.ImportPeptideSearchSettings_OspreySearchSettings,
                PropertyNames.OspreySearchSettings_RunFdr, PropertyNames.OspreySearchSettings_NonDefaultAdditionalSettings,
                settings.Settings[ADDITIONAL_FLAG].AuditLogText);
            string nceText = settings.Settings[ADDITIONAL_NCE].AuditLogText;
            if (predictsLibrary)
                AssertEx.Contains(allInfo, nceText);
            else
                AssertEx.DoesNotContain(allInfo, nceText);
        }

        /// <summary>
        /// The Additional Settings grid from the Search Settings page: the settings of the search
        /// the pages configured, each under its label from the installed Osprey, in the category
        /// of its help section. For a predicted library (<paramref name="predictsLibrary"/>) the
        /// prediction's settings are among them and the flag and the collision energy are set; for
        /// an existing library they are not, and the flag is found still set from the first run,
        /// the collision energy kept but out of scope. A value the argument does not take is
        /// refused with its reason, and Cancel puts back the values the grid opened with.
        /// </summary>
        private static void SetAdditionalSettings(OspreySearchDlg searchDlg, bool predictsLibrary)
        {
            var settings = searchDlg.SearchSettingsControl.AdditionalSettings;
            string flagLabel = settings.Properties.Find(ADDITIONAL_FLAG).Label;
            string nceLabel = settings.Properties.Find(ADDITIONAL_NCE).Label;
            var grid = ShowDialog<OspreySettingsGridDlg>(searchDlg.SearchSettingsControl.ShowAdditionalSettings);
            RunUI(() =>
            {
                var labels = grid.Labels.ToArray();
                AssertEx.IsTrue(labels.Contains(flagLabel));
                AssertEx.AreEqual(!predictsLibrary, grid.GetValue(flagLabel));
                AssertEx.AreEqual(predictsLibrary, labels.Contains(nceLabel));
                // The form-bound settings are the pages', not the grid's
                AssertEx.IsFalse(labels.Contains(settings.Properties.Find(@"threads").Label));
                // The rows are grouped under their help sections' titles
                var categories = grid.Categories.ToArray();
                AssertEx.IsTrue(categories.Contains(settings.Properties.Find(ADDITIONAL_FLAG).Group));
                if (predictsLibrary)
                {
                    AssertEx.IsTrue(categories.Contains(settings.Properties.Find(ADDITIONAL_NCE).Group));
                    grid.SetValue(flagLabel, true.ToString());
                    grid.SetValue(nceLabel, ADDITIONAL_NCE_VALUE);
                    // The refusal of a value the argument does not take, as the grid gives it
                    AssertEx.ThrowsException<ArgumentException>(() => grid.SetValue(nceLabel, @"hot"),
                        x => AssertEx.Contains(x.Message, nceLabel));
                    AssertEx.ThrowsException<ArgumentOutOfRangeException>(() => grid.SetValue(nceLabel, @"-1"),
                        x => AssertEx.Contains(x.Message, nceLabel));
                    AssertEx.AreEqual(ADDITIONAL_NCE_VALUE, grid.GetValue(nceLabel));
                }
            });
            OkDialog(grid, grid.OkDialog);
            AssertEx.AreEqual(true, settings.Settings[ADDITIONAL_FLAG].Value);
            AssertEx.AreEqual(ADDITIONAL_NCE_VALUE, settings.Settings[ADDITIONAL_NCE].Value);
            // Cancel puts back the values the grid opened with
            grid = ShowDialog<OspreySettingsGridDlg>(searchDlg.SearchSettingsControl.ShowAdditionalSettings);
            RunUI(() =>
            {
                grid.SetValue(flagLabel, false.ToString());
                AssertEx.AreEqual(false, settings.Settings[ADDITIONAL_FLAG].Value);
            });
            OkDialog(grid, grid.CancelDialog);
            AssertEx.AreEqual(true, settings.Settings[ADDITIONAL_FLAG].Value);
        }

        /// <summary>
        /// The settings preset: Save... keeps the page's values, the additional settings included,
        /// under the name the user gives; choosing the preset again after the values change brings
        /// them back; a second save under the name asks before replacing it.
        /// </summary>
        private static void SaveAndReapplyPreset(OspreySearchDlg searchDlg)
        {
            var control = searchDlg.SearchSettingsControl;
            var settings = control.AdditionalSettings;
            RunUI(() =>
            {
                AssertEx.IsNull(control.SettingsPresetName);
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
            });
            var nameDlg = ShowDialog<PresetNameDlg>(control.ShowSavePresetDlg);
            RunUI(() => nameDlg.PresetName = PRESET_NAME);
            OkDialog(nameDlg, nameDlg.OkDialog);
            RunUI(() =>
            {
                AssertEx.AreEqual(PRESET_NAME, control.SettingsPresetName);
                AssertEx.IsTrue(control.SettingsPresets.TryGetValue(PRESET_NAME, out var preset));
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, preset.RunFdr);
                AssertEx.IsFalse(preset.IsHighResolution);
                AssertEx.Contains(preset.AdditionalSettingsXml, ADDITIONAL_FLAG, ADDITIONAL_NCE);
                // Change the page, then choose the preset again: its values return
                control.RunFdr = PRESET_CHANGED_RUN_FDR;
                settings.Settings[ADDITIONAL_FLAG].Value = false;
                control.SettingsPresetName = PRESET_NAME;
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
                AssertEx.AreEqual(true, settings.Settings[ADDITIONAL_FLAG].Value);
                AssertEx.AreEqual(ADDITIONAL_NCE_VALUE, settings.Settings[ADDITIONAL_NCE].Value);
            });
            // Saving under the name again asks before replacing the preset
            nameDlg = ShowDialog<PresetNameDlg>(control.ShowSavePresetDlg);
            RunUI(() => nameDlg.PresetName = PRESET_NAME);
            var overwrite = ShowDialog<MessageDlg>(nameDlg.OkDialog);
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.SearchSettingsControl_OverwriteSettingsPreset_A_settings_preset_named__0__already_exists__Do_you_want_to_replace_it_,
                PRESET_NAME), overwrite.Message);
            OkDialog(overwrite, overwrite.ClickYes);
            RunUI(() =>
            {
                AssertEx.AreEqual(PRESET_NAME, control.SettingsPresetName);
                AssertEx.IsTrue(control.SettingsPresets.ContainsKey(PRESET_NAME));
            });
        }

        /// <summary>
        /// The preset saved in the first wizard is listed in the second, with none chosen until the
        /// user chooses one, and choosing it brings its values back to the new page.
        /// </summary>
        private static void VerifyPresetPersisted(OspreySearchDlg searchDlg)
        {
            var control = searchDlg.SearchSettingsControl;
            RunUI(() =>
            {
                AssertEx.IsNull(control.SettingsPresetName);
                AssertEx.IsTrue(control.SettingsPresets.ContainsKey(PRESET_NAME));
                control.RunFdr = PRESET_CHANGED_RUN_FDR;
                control.SettingsPresetName = PRESET_NAME;
                AssertEx.AreEqual(PRESET_NAME, control.SettingsPresetName);
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
                AssertEx.AreEqual(true, control.AdditionalSettings.Settings[ADDITIONAL_FLAG].Value);
            });
        }

        /// <summary>
        /// The command line the wizard builds, checked before the search runs: the one command
        /// from the FASTA with the document's digest settings, or from
        /// <paramref name="existingLibraryPath"/> with no library-build options at all, and the
        /// document library as the output either way; the additional settings after the pages',
        /// the prediction's only with a predicted library.
        /// </summary>
        private void VerifyCommandLine(OspreySearchDlg searchDlg, string fastaFilepath, string[] diaFilePaths, string existingLibraryPath)
        {
            OspreyConfig config = null;
            RunUI(() => config = searchDlg.GetSearchConfig());
            string commandLine = OspreyHelpers.BuildCommandLine(config);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_PREFIX + ADDITIONAL_FLAG + @" ");
            AssertEx.AreEqual(existingLibraryPath == null,
                commandLine.Contains(OspreyHelpers.ARG_PREFIX + ADDITIONAL_NCE + @" " + ADDITIONAL_NCE_VALUE + @" "));
            if (existingLibraryPath == null)
            {
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_FASTA + @" " + fastaFilepath.Quote());
                // A test's Tools folder has no CUDA runtime, so Use GPU starts unchecked
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_DEVICE + @" " + OspreyHelpers.DEVICE_CPU);
                AssertEx.Contains(commandLine, @"--fix-mod 1 ");  // Carafe's Carbamidomethyl of C
                AssertEx.Contains(commandLine, @"--var-mod 0 ");  // Carafe's spelling of none
                AssertEx.Contains(commandLine, @"--enzyme 1 ");   // Carafe's Trypsin, matched on behavior
                AssertEx.Contains(commandLine, @"--min-pep-charge 2 ", @"--max-pep-charge 3 "); // The DIA defaults for a default document
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_CULTURE + @" " + CultureInfo.CurrentUICulture.Name); // Osprey writes in Skyline's language
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_TIMESTAMP); // The log on disk is stamped; the run page strips the stamps
                AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_LIBRARY + @" "));
            }
            else
            {
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_LIBRARY + @" " + existingLibraryPath.Quote());
                // The predicted library pairs its decoys with their targets, so Osprey searches with them.
                AssertEx.Contains(commandLine, OspreyHelpers.ARG_DECOYS_IN_LIBRARY);
                AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_FASTA + @" "));
                AssertEx.IsFalse(commandLine.Contains(OspreyHelpers.ARG_DEVICE + @" "));
                AssertEx.IsFalse(commandLine.Contains(@"--enzyme "));
            }
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_OUTPUT + @" " +
                BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath).Quote());
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_RESOLUTION + @" " + OspreyHelpers.RESOLUTION_UNIT);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_FRAGMENT_UNIT + @" " + OspreyHelpers.FRAGMENT_UNIT_MZ);
            foreach (var path in diaFilePaths)
                AssertEx.Contains(commandLine, path.Quote());
        }

        /// <summary>
        /// Run from the search settings page and wait for Osprey. When the document library exists
        /// already (a re-run), Run asks before overwriting it. The progress bar must have moved,
        /// which is the "NN%" lines of Osprey's progress reporter parsing through ProcessRunner.
        /// The wizard's log is saved to <paramref name="logPath"/> whether the search succeeds or not.
        /// </summary>
        public static void RunSearch(OspreySearchDlg searchDlg, string docBlibPath, string workDir, string logPath,
            int searchTimeoutMillis = SEARCH_TIMEOUT_MILLIS)
        {
            // Subscribe before Run, so an Osprey that fails at once cannot finish before the subscription.
            bool? searchSucceeded = null;
            searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
            // An earlier search's console log rolls aside under its own last write before this one starts
            string stdoutLogPath = Path.Combine(workDir, OspreyHelpers.STDOUT_LOG_NAME);
            DateTime? previousLogWritten = File.Exists(stdoutLogPath) ? File.GetLastWriteTime(stdoutLogPath) : null;
            if (File.Exists(docBlibPath))
            {
                var overwritePrompt = ShowDialog<MultiButtonMsgDlg>(searchDlg.NextPage);
                AssertEx.AreEqual(string.Format(
                    PeptideSearchResources.OspreySearchDlg_StartSearch_The_document_library__0__already_exists__Overwrite_it_with_the_results_of_this_search_,
                    docBlibPath), overwritePrompt.Message);
                OkDialog(overwritePrompt, overwritePrompt.ClickOk); // The Overwrite button
            }
            else
            {
                RunUI(searchDlg.NextPage);
            }
            int maxPercent = 0;
            try
            {
                WaitForConditionUI(searchTimeoutMillis, () =>
                {
                    maxPercent = Math.Max(maxPercent, searchDlg.SearchControl.PercentComplete);
                    return searchSucceeded.HasValue;
                });
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText(logPath, searchDlg.SearchControl.LogText);
            }
            AssertEx.IsTrue(maxPercent > 0, @"Osprey's progress lines did not advance the progress bar");

            AssertEx.AreEqual(docBlibPath, searchDlg.SearchControl.OutputLibraryPath);
            AssertEx.FileExists(docBlibPath);
            // The run page's Open Log button opens the console output teed to the working folder
            AssertEx.FileExists(stdoutLogPath);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.SearchControl.CanOpenLog);
                AssertEx.AreEqual(stdoutLogPath, searchDlg.SearchControl.LogFilePath);
            });
            if (previousLogWritten.HasValue)
                AssertEx.FileExists(FileTimeEx.GetTimeStampedPath(stdoutLogPath, previousLogWritten.Value));
        }

        /// <summary>
        /// Finish: the Import Peptide Search wizard opens on its existing-library route, past the
        /// library page, with the searched runs on the chromatograms page, and its DIA pages add
        /// the library's peptides and import the runs. Ends with the document saved and the
        /// wizard closed, so another run can start from it. The chromatogram import gets
        /// <paramref name="importTimeoutMillis"/>, which a perf test on full-size runs raises.
        /// </summary>
        public static void FinishImport(OspreySearchDlg searchDlg, string[] diaFilePaths, string docBlibPath,
            int importTimeoutMillis = WAIT_TIME)
        {
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
            VerifyDocumentLibrary(docBlibPath);

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
            using (new WaitDocumentChange(null, true, importTimeoutMillis))
            {
                OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            }
            WaitForClosedForm(importPeptideSearchDlg);
            WaitForClosedForm(searchDlg);
            WaitForDocumentLoaded(importTimeoutMillis);

            var doc = SkylineWindow.Document;
            AssertEx.IsTrue(doc.PeptideCount > 0, @"no peptides matched the Osprey library");
            AssertEx.IsTrue(doc.Settings.HasResults);
            AssertEx.AreEqual(diaFilePaths.Length, doc.Settings.MeasuredResults.Chromatograms.Count);
            VerifyDocumentLibrary(docBlibPath);
            RunUI(() => SkylineWindow.SaveDocument());
        }

        /// <summary>
        /// The document has exactly one document library, at <paramref name="docBlibPath"/>, and
        /// the Library object it holds is the one the library manager loaded from that file.
        /// </summary>
        private static void VerifyDocumentLibrary(string docBlibPath)
        {
            var libraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            var docLibSpecs = libraries.LibrarySpecs.Where(s => s.IsDocumentLibrary).ToArray();
            AssertEx.AreEqual(1, docLibSpecs.Length, @"expected exactly one document library");
            AssertEx.AreEqual(docBlibPath, docLibSpecs[0].FilePath);
            var docLib = GetDocumentLibrary();
            Assert.IsNotNull(docLib, @"the document library is not loaded");
            AssertEx.IsTrue(ReferenceEquals(docLib, SkylineWindow.LibraryManager.TryGetLibrary(docLibSpecs[0])),
                @"the document's library object is not the one the library manager holds for its file");
        }

        public static Library GetDocumentLibrary()
        {
            var libraries = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            return libraries.TryGetDocumentLibrary(out var docLib) ? docLib : null;
        }

        /// <summary>A new document with the default settings, saved at <paramref name="documentPath"/>.</summary>
        public static void PrepareDocument(string documentPath)
        {
            RunUI(SkylineWindow.NewDocument);
            // The default settings, charge 2 only: the wizard predicts the library for charges 2 and
            // 3 on a default document, as the Import Peptide Search wizard's DIA transition settings
            // page sets them, and with charge 2 alone too few of the Stellar subset's precursors are
            // in its isolation window to pass the FDR.
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(documentPath));
        }

        /// <summary>
        /// The TeamCity resolution is checked on every run, whichever Osprey the test then uses: the
        /// latest successful build names a versioned win-x64 zip and its artifact URL answers a HEAD
        /// request, without downloading the package.
        /// </summary>
        private static void VerifyLatestBuildOnTeamCity()
        {
            var downloadInfo = OspreyHelpers.GetLatestBuildDownloadInfo();
            AssertEx.IsTrue(downloadInfo.Filename.StartsWith(@"Osprey-") && downloadInfo.Filename.EndsWith(@"-win-x64"),
                @"unexpected Osprey package name " + downloadInfo.Filename);
            // The version the prompt names is the folder name's middle: the pinned package's is the pinned version
            AssertEx.AreEqual(OspreyHelpers.OSPREY_VERSION, OspreyHelpers.GetPackageVersion(OspreyHelpers.OspreyDownloadInfo));
            AssertEx.AreEqual(@"Osprey-" + OspreyHelpers.GetPackageVersion(downloadInfo) + @"-win-x64", downloadInfo.Filename);
            AssertEx.AreEqual(Path.Combine(ToolDescriptionHelpers.GetToolsDirectory(), downloadInfo.Filename, OspreyHelpers.OSPREY_EXE),
                downloadInfo.ToolPath);
            using var client = new HttpClientWithProgress();
            AssertEx.IsTrue(client.Head(downloadInfo.DownloadUrl), @"no artifact at " + downloadInfo.DownloadUrl);
        }
    }
}
