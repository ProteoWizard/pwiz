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
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Osprey in Run Peptide Search, end to end on Osprey's Stellar subset: three sDIA mzML files and
    /// a synthetic protein FASTA whose records are the subset library's tryptic peptides, so Osprey
    /// predicts a small library from the FASTA with Carafe and searches it in a minute or two. The
    /// library Osprey writes becomes the document library, and the run imports the runs. A second
    /// run on the same document then searches the library the first run predicted, in place of
    /// predicting one, replacing the document library with the user's consent.
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

        /// <summary>The wizard preset the test saves in the first run and applies in the second.</summary>
        private const string PRESET_NAME = @"Night preset";
        private const double PRESET_CHANGED_RUN_FDR = 0.05;

        protected override void DoTest()
        {
            string ospreyExe = OspreyTestUtil.EnsureOsprey(TestContext);
            // The additional settings start at Osprey's defaults, whatever an earlier wizard on this machine left
            Settings.Default.OspreyAdditionalSettings = null;
            OspreyProperties.ClearCache();
            bool savedWarnOnPresetChange = Settings.Default.WarnOnPresetChange;
            try
            {
                Settings.Default.WarnOnPresetChange = false; // Presets change on later pages here
                RunOspreySearches(ospreyExe);
            }
            finally
            {
                Settings.Default.WarnOnPresetChange = savedWarnOnPresetChange;
                var preset = Settings.Default.SearchSettingsPresets.FirstOrDefault(p => p.Name == PRESET_NAME);
                if (preset != null)
                    Settings.Default.SearchSettingsPresets.Remove(preset);
            }
        }

        private void RunOspreySearches(string ospreyExe)
        {
            VerifyLatestBuildOnTeamCity();
            PrepareDocument(TestFilesDir.GetTestPath("OspreySearchTest.sky"));

            string fastaFilepath = TestFilesDir.GetTestPath("stellar-subset-proteins.fasta");
            string[] diaFilePaths = Directory.GetFiles(TestFilesDir.FullPath, "*.mzML").OrderBy(p => p).ToArray();
            AssertEx.AreEqual(3, diaFilePaths.Length);
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            string workDir = OspreyHelpers.GetWorkDirectory(SkylineWindow.DocumentFilePath);

            // The install gate: with Osprey registered at a path that does not exist, leaving the DIA
            // search method page offers the download, and canceling it stays on the page. A developer
            // build, as the tests run, offers the latest successful master build; a release the pinned
            // one. On TeamCity that package is the one EnsureOsprey just installed in the Tools folder,
            // which the gate would register again instead of offering, so it is moved aside for the check.
            var dlg = OpenWizardOnDiaMethodPage(diaFilePaths, fastaFilepath);
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
                var downloadPrompt = ShowDialog<MultiButtonMsgDlg>(() => dlg.ClickNextButton());
                AssertEx.Contains(downloadPrompt.Message, OspreyHelpers.GetPackageVersion(package));
                OkDialog(downloadPrompt, downloadPrompt.BtnCancelClick);
                RunUI(() => AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.converter_settings_page, dlg.CurrentPage));
            }
            finally
            {
                if (packageInstalled)
                    Directory.Move(asideDir, packageDir);
            }
            OspreyTestUtil.RegisterOsprey(ospreyExe);

            // The first run: the library is predicted from the FASTA
            RunUI(() => AssertEx.IsTrue(dlg.ConverterSettingsControl.PredictLibrary));
            AdvanceToSearchSettings(dlg);
            SetAdditionalSettings(dlg, true);
            SaveAndReapplyPreset(dlg);
            var engine = RunSearch(dlg, docBlibPath, workDir, TestFilesDir.GetTestPath(WIZARD_LOG_FIRST_RUN));
            VerifyCommandLine(engine.RunConfig, fastaFilepath, diaFilePaths, null);
            var additionalSettings = dlg.SearchSettingsControl.OspreySettingsControl.AdditionalSettings;
            FinishImport(dlg, diaFilePaths, docBlibPath);
            VerifyAuditLogSettings(additionalSettings, true);

            // The second run, on the document the first one made: Osprey searches the library
            // it predicted in the first run, and the run replaces the document library.
            string predictedLibraryPath = Path.Combine(workDir,
                Path.GetFileNameWithoutExtension(SkylineWindow.DocumentFilePath) + PREDICTED_LIBRARY_SUFFIX + BiblioSpecLiteSpec.EXT);
            AssertEx.FileExists(predictedLibraryPath, @"the first run did not leave its predicted library in the working folder");
            var libraryBefore = GetDocumentLibrary();

            var rerunDlg = OpenWizardOnDiaMethodPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                rerunDlg.ConverterSettingsControl.PredictLibrary = false;
                // The document library is replaced by the run, so it is refused as the library to search
                rerunDlg.ConverterSettingsControl.ExistingLibraryPath = docBlibPath;
            });
            var refusal = ShowDialog<MessageDlg>(() => rerunDlg.ClickNextButton());
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.RunPeptideSearchDlg_ValidateExistingLibraryPath_The_library__0__cannot_be_searched__the_run_replaces_it_with_the_library_Osprey_writes_,
                docBlibPath), refusal.Message);
            OkDialog(refusal, refusal.OkDialog);
            // So is the library Osprey writes, which a search deletes before it starts
            RunUI(() => rerunDlg.ConverterSettingsControl.ExistingLibraryPath = engine.OutputPath);
            refusal = ShowDialog<MessageDlg>(() => rerunDlg.ClickNextButton());
            AssertEx.AreEqual(string.Format(
                PeptideSearchResources.RunPeptideSearchDlg_ValidateExistingLibraryPath_The_library__0__cannot_be_searched__the_run_replaces_it_with_the_library_Osprey_writes_,
                engine.OutputPath), refusal.Message);
            OkDialog(refusal, refusal.OkDialog);
            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.converter_settings_page, rerunDlg.CurrentPage);
                rerunDlg.ConverterSettingsControl.ExistingLibraryPath = predictedLibraryPath;
            });
            AdvanceToSearchSettings(rerunDlg);
            VerifyPresetPersisted(rerunDlg);
            SetAdditionalSettings(rerunDlg, false);
            engine = RunSearch(rerunDlg, docBlibPath, workDir, TestFilesDir.GetTestPath(WIZARD_LOG_SECOND_RUN));
            VerifyCommandLine(engine.RunConfig, fastaFilepath, diaFilePaths, predictedLibraryPath);
            additionalSettings = rerunDlg.SearchSettingsControl.OspreySettingsControl.AdditionalSettings;
            FinishImport(rerunDlg, diaFilePaths, docBlibPath);
            VerifyAuditLogSettings(additionalSettings, false);

            // The document reads the library Osprey wrote, not the one it loaded before the run
            AssertEx.IsFalse(ReferenceEquals(libraryBefore, GetDocumentLibrary()),
                @"the document still holds the library object loaded before the overwrite");

            // A preset for another engine chosen on the Library Model page leaves it nothing to ask, and
            // Next goes on to the search settings page for that engine
            var presetDlg = OpenWizardOnDiaMethodPage(diaFilePaths, fastaFilepath);
            RunUI(() =>
            {
                AssertEx.IsTrue(presetDlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.library_model_page, presetDlg.CurrentPage);
                presetDlg.SelectedPresetName = DiannHelpers.PRESET_ORBITRAP;
                AssertEx.AreEqual(SearchEngine.DIANN, presetDlg.ConverterSettingsControl.DirectDiaSearchEngine);
                AssertEx.IsTrue(presetDlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.dda_search_settings_page, presetDlg.CurrentPage);
                AssertEx.AreEqual(SearchEngine.DIANN, presetDlg.SearchSettingsControl.SelectedSearchEngine);
            });
            OkDialog(presetDlg, presetDlg.ClickCancelButton);
        }

        /// <summary>
        /// Opens Run Peptide Search from the Osprey Search menu item, set up to search DIA spectra with
        /// Osprey, adds <paramref name="diaFilePaths"/>, accepts the shortened replicate names, keeps the
        /// document's modifications (Carbamidomethyl C), sets the FASTA and moves to the DIA search method page.
        /// </summary>
        public static RunPeptideSearchDlg OpenWizardOnDiaMethodPage(string[] diaFilePaths, string fastaFilepath)
        {
            var dlg = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowOspreySearchDlg);
            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.spectra_page, dlg.CurrentPage);
                AssertEx.AreEqual(PeptideSearchWizardDlg.Workflow.dia, dlg.BuildPepSearchLibControl.WorkflowType);
                dlg.BuildPepSearchLibControl.InputFileType = PeptideSearchWizardDlg.InputFile.dia_raw;
                dlg.BuildPepSearchLibControl.DdaSearchDataSources = diaFilePaths
                    .Select(p => (MsDataFileUri) new MsDataFilePath(p)).ToArray();
            });
            // Three files share a common prefix and suffix, so the wizard offers to shorten the names
            var removePrefix = ShowDialog<ImportResultsNameDlg>(() => dlg.ClickNextButton());
            OkDialog(removePrefix, removePrefix.YesDialog);
            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.match_modifications_page, dlg.CurrentPage);
                AssertEx.AreEqualDeep(new[] { @"Carbamidomethyl (C)" },
                    dlg.MatchModificationsControl.CheckedModificationNames.ToArray());
                AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.import_fasta_page, dlg.CurrentPage);
                // Osprey supplies the peak boundaries and q-values, so no decoys and no mProphet model
                AssertEx.IsTrue(string.IsNullOrEmpty(dlg.ImportFastaControl.DecoyGenerationMethod));
                AssertEx.IsFalse(dlg.ImportFastaControl.AutoTrain);
                dlg.ImportFastaControl.SetFastaContent(fastaFilepath);
                AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.converter_settings_page, dlg.CurrentPage);
                AssertEx.IsFalse(dlg.ConverterSettingsControl.UseDiaUmpire);
                AssertEx.AreEqual(SearchEngine.Osprey, dlg.ConverterSettingsControl.DirectDiaSearchEngine);
            });
            return dlg;
        }

        /// <summary>
        /// From the DIA search method page (or the Library Model page, which a predicted library gets
        /// after it and whose defaults predict with the pretrained models) to the search settings page,
        /// which shows Osprey's settings, set for the Stellar subset's unit resolution.
        /// </summary>
        public static void AdvanceToSearchSettings(RunPeptideSearchDlg dlg)
        {
            RunUI(() =>
            {
                if (dlg.CurrentPage == PeptideSearchWizardDlg.Pages.converter_settings_page)
                {
                    AssertEx.IsTrue(dlg.ClickNextButton());
                    if (dlg.ConverterSettingsControl.PredictLibrary)
                    {
                        AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.library_model_page, dlg.CurrentPage);
                        AssertEx.IsTrue(dlg.LibraryModelControl.UseExistingModel);
                        AssertEx.IsTrue(dlg.LibraryModelControl.Model.IsDefault);
                    }
                }
                if (dlg.CurrentPage == PeptideSearchWizardDlg.Pages.library_model_page)
                    AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.dda_search_settings_page, dlg.CurrentPage);
                AssertEx.AreEqual(SearchEngine.Osprey, dlg.SearchSettingsControl.SelectedSearchEngine);
                AssertEx.AreEqualDeep(new[] { SearchEngine.Osprey }, dlg.SearchSettingsControl.SearchEngineChoices.ToArray());
                var ospreySettings = dlg.SearchSettingsControl.OspreySettingsControl;
                AssertEx.IsTrue(ospreySettings.Visible);
                ospreySettings.IsHighResolution = false;
                AssertEx.IsFalse(ospreySettings.IsFragmentTolerancePpm);
                ospreySettings.Threads = Environment.ProcessorCount;
            });
        }

        /// <summary>
        /// The import's audit log entry records the Osprey search's settings: the page's settings under
        /// their names, and the additional settings the run changed, each as the setting renders itself.
        /// Only the settings in the run's scopes are recorded: the flag is a search setting and is there
        /// for both runs; the collision energy is a library setting, so a search of an existing library
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
        /// The Additional Settings grid from the search settings page: the settings of the search the
        /// pages configured, each under its label from the installed Osprey, in the category of its help
        /// section. For a predicted library (<paramref name="predictsLibrary"/>) the prediction's settings
        /// are among them and the flag and the collision energy are set; for an existing library they are
        /// not, and the flag is found still set from the first run, the collision energy kept but out of
        /// scope. A value the argument does not take is refused with its reason, and Cancel puts back the
        /// values the grid opened with.
        /// </summary>
        private static void SetAdditionalSettings(RunPeptideSearchDlg dlg, bool predictsLibrary)
        {
            var control = dlg.SearchSettingsControl.OspreySettingsControl;
            var settings = control.AdditionalSettings;
            string flagLabel = settings.Properties.Find(ADDITIONAL_FLAG).Label;
            string nceLabel = settings.Properties.Find(ADDITIONAL_NCE).Label;
            var grid = ShowDialog<OspreySettingsGridDlg>(control.ShowAdditionalSettings);
            RunUI(() =>
            {
                var labels = grid.Labels.ToArray();
                AssertEx.IsTrue(labels.Contains(flagLabel));
                AssertEx.AreEqual(!predictsLibrary, grid.GetValue(flagLabel));
                AssertEx.AreEqual(predictsLibrary, labels.Contains(nceLabel));
                // The form-bound settings are the page's, not the grid's
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
            var gridToCancel = ShowDialog<OspreySettingsGridDlg>(control.ShowAdditionalSettings);
            RunUI(() =>
            {
                gridToCancel.SetValue(flagLabel, false.ToString());
                AssertEx.AreEqual(false, settings.Settings[ADDITIONAL_FLAG].Value);
            });
            OkDialog(gridToCancel, gridToCancel.CancelDialog);
            AssertEx.AreEqual(true, settings.Settings[ADDITIONAL_FLAG].Value);
        }

        /// <summary>
        /// The wizard preset keeps Osprey's settings, the additional settings included, with the FASTA,
        /// enzyme and modifications; choosing the preset again after the values change brings them back.
        /// </summary>
        private static void SaveAndReapplyPreset(RunPeptideSearchDlg dlg)
        {
            var control = dlg.SearchSettingsControl.OspreySettingsControl;
            var settings = control.AdditionalSettings;
            RunUI(() =>
            {
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
                dlg.SaveSettingsPreset(PRESET_NAME);
                var preset = Settings.Default.SearchSettingsPresets.First(p => p.Name == PRESET_NAME);
                AssertEx.AreEqual(SearchEngine.Osprey, preset.SearchEngine);
                AssertEx.AreEqual(SearchWorkflowType.dia, preset.Workflow);
                Assert.IsNotNull(preset.OspreySettings);
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, preset.OspreySettings.RunFdr);
                AssertEx.IsFalse(preset.OspreySettings.IsHighResolution);
                AssertEx.Contains(preset.OspreySettings.AdditionalSettingsXml, ADDITIONAL_FLAG, ADDITIONAL_NCE);

                // Change the page, then choose the preset again: its values return
                control.RunFdr = PRESET_CHANGED_RUN_FDR;
                settings.Settings[ADDITIONAL_FLAG].Value = false;
                dlg.SelectedPresetName = null;
                dlg.SelectedPresetName = PRESET_NAME;
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
                AssertEx.IsFalse(control.IsHighResolution);
                AssertEx.AreEqual(true, settings.Settings[ADDITIONAL_FLAG].Value);
                AssertEx.AreEqual(ADDITIONAL_NCE_VALUE, settings.Settings[ADDITIONAL_NCE].Value);
            });
        }

        /// <summary>
        /// The preset saved in the first run is listed in the second, and choosing it on the search
        /// settings page brings its Osprey settings back to the new page.
        /// </summary>
        private static void VerifyPresetPersisted(RunPeptideSearchDlg dlg)
        {
            var control = dlg.SearchSettingsControl.OspreySettingsControl;
            RunUI(() =>
            {
                AssertEx.IsTrue(dlg.PresetNames.Contains(PRESET_NAME));
                control.RunFdr = PRESET_CHANGED_RUN_FDR;
                dlg.SelectedPresetName = PRESET_NAME;
                AssertEx.AreEqual(PRESET_NAME, dlg.SelectedPresetName);
                AssertEx.AreEqual(SearchEngine.Osprey, dlg.SearchSettingsControl.SelectedSearchEngine);
                AssertEx.AreEqual(OspreySearchSettingsControl.DEFAULT_FDR, control.RunFdr);
                AssertEx.AreEqual(true, control.AdditionalSettings.Settings[ADDITIONAL_FLAG].Value);
            });
        }

        /// <summary>
        /// The command line the run used: the one command from the FASTA with the document's digest
        /// settings, or from <paramref name="existingLibraryPath"/> with no library-build options at all,
        /// and Osprey's library in its working folder as the output either way; the additional settings
        /// after the page's, the prediction's only with a predicted library.
        /// </summary>
        private void VerifyCommandLine(OspreyConfig config, string fastaFilepath, string[] diaFilePaths, string existingLibraryPath)
        {
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
                AssertEx.Contains(commandLine, @"--min-pep-charge 2 ", @"--max-pep-charge 3 "); // The DIA transition settings for a default document
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
            string docPath = SkylineWindow.DocumentFilePath;
            string ospreyOutputPath = Path.Combine(OspreyHelpers.GetWorkDirectory(docPath),
                Path.GetFileName(BiblioSpecLiteSpec.GetLibraryFileName(docPath)));
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_OUTPUT + @" " + ospreyOutputPath.Quote());
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_RESOLUTION + @" " + OspreyHelpers.RESOLUTION_UNIT);
            AssertEx.Contains(commandLine, OspreyHelpers.ARG_FRAGMENT_UNIT + @" " + OspreyHelpers.FRAGMENT_UNIT_MZ);
            foreach (var path in diaFilePaths)
                AssertEx.Contains(commandLine, path.Quote());
        }

        /// <summary>
        /// From the search settings page through the transition and full-scan settings pages to Run, and
        /// waits for the run. When the document library exists already (a re-run), Run asks before
        /// replacing it, with no choice to add to it. The progress bar must have moved, which is the "NN%"
        /// lines of Osprey's progress reporter parsing through ProcessRunner. The run page's log is saved to
        /// <paramref name="logPath"/> whether the run succeeds or not. Returns the engine that searched.
        /// </summary>
        public static OspreySearchEngine RunSearch(RunPeptideSearchDlg dlg, string docBlibPath, string workDir, string logPath,
            int searchTimeoutMillis = SEARCH_TIMEOUT_MILLIS)
        {
            // An earlier search's console log rolls aside under its own last write before this one starts
            string stdoutLogPath = Path.Combine(workDir, OspreyHelpers.STDOUT_LOG_NAME);
            DateTime? previousLogWritten = File.Exists(stdoutLogPath) ? File.GetLastWriteTime(stdoutLogPath) : null;
            OspreySearchEngine engine = null;
            RunUI(() =>
            {
                AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.transition_settings_page, dlg.CurrentPage);
                engine = (OspreySearchEngine) dlg.BuildPepSearchLibControl.ImportPeptideSearch.SearchEngine;
                AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.full_scan_settings_page, dlg.CurrentPage);
                dlg.FullScanSettingsControl.IsolationSchemeName = PropertiesResources.IsolationSchemeList_GetDefaults_Results_only;
            });
            if (File.Exists(docBlibPath) && SkylineWindow.Document.Settings.HasDocumentLibrary)
            {
                var overwritePrompt = ShowDialog<MultiButtonMsgDlg>(() => dlg.ClickNextButton());
                AssertEx.AreEqual(string.Format(
                    PeptideSearchResources.OspreySearchDlg_StartSearch_The_document_library__0__already_exists__Overwrite_it_with_the_results_of_this_search_,
                    docBlibPath), overwritePrompt.Message);
                OkDialog(overwritePrompt, overwritePrompt.ClickOk); // The Overwrite button
            }
            else
            {
                SkylineWindow.BeginInvoke(() => dlg.ClickNextButton());
            }
            int maxPercent = 0;
            try
            {
                WaitForConditionUI(searchTimeoutMillis, () =>
                {
                    maxPercent = Math.Max(maxPercent, dlg.SearchControl.PercentComplete);
                    return dlg.RunSucceeded.HasValue;
                });
                RunUI(() => AssertEx.IsTrue(dlg.RunSucceeded.Value, dlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText(logPath, dlg.SearchControl.LogText);
            }
            AssertEx.IsTrue(maxPercent > 0, @"Osprey's progress lines did not advance the progress bar");

            // Osprey wrote its library in its working folder, and the run made it the document library
            AssertEx.FileExists(engine.OutputPath);
            AssertEx.FileExists(docBlibPath);
            // The run page's Open Log button opens the console output teed to the working folder
            AssertEx.FileExists(stdoutLogPath);
            RunUI(() =>
            {
                AssertEx.IsTrue(dlg.SearchControl.CanOpenLog);
                AssertEx.AreEqual(stdoutLogPath, dlg.SearchControl.LogFilePath);
            });
            if (previousLogWritten.HasValue)
                AssertEx.FileExists(FileTimeEx.GetTimeStampedPath(stdoutLogPath, previousLogWritten.Value));
            return engine;
        }

        /// <summary>
        /// Finish: the run has made the document library and is importing the runs; the wizard closes
        /// and the import completes. Ends with the document saved, so another run can start from it.
        /// The chromatogram import gets <paramref name="importTimeoutMillis"/>, which a perf test on
        /// full-size runs raises.
        /// </summary>
        public static void FinishImport(RunPeptideSearchDlg dlg, string[] diaFilePaths, string docBlibPath,
            int importTimeoutMillis = WAIT_TIME)
        {
            OkDialog(dlg, () => dlg.ClickNextButton());
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
            // The default settings, charge 2 only: the run predicts the library for the charges the DIA
            // transition settings page sets (2 and 3), since that page comes before the run, and with
            // charge 2 alone too few of the Stellar subset's precursors are in its isolation window to
            // pass the FDR.
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
