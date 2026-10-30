/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DiannSearchTest : AbstractFunctionalTestEx
    {
        // Which version this test invocation should drive — set by the TestMethod entry
        // points before calling RunFunctionalTest. DoTest reads it to pick the download info.
        private string _versionUnderTest;

        // When true, the test runs search settings that DIA-NN 1.9.1 reports zero
        // precursors against (q-value 0.01 + tiny inputs + 1.9.1's --gen-spec-lib
        // heuristic) and asserts that the run stops with a friendly empty-results
        // message instead of handing the empty lib.parquet off to BlibBuild (which would
        // otherwise abort with a confusing "process cannot access the file" error).
        private bool _expectEmptyResults;

        // The DIA file searched. DIA-NN reads mzML itself, but not mzXML, which Skyline converts to mzML first.
        private string _searchFileName = @"23aug2017_hela_serum_timecourse_wide_1c.mzML";

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiannSearch()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            _versionUnderTest = DiannHelpers.DIANN_VERSION;
            RunFunctionalTest();
        }

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoUnicodeTesting(@"DIA-NN 1.9.1 doesn't support UTF-8 paths properly.")]
        public void TestDiannSearch1_9_1()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            // Per-version suffix isolates each TestMethod's results directory so a stale
            // file handle on one (e.g. an Explorer window or Windows index) doesn't break
            // the other test's startup-time cleanup of the shared zip basename folder.
            TestFilesZipSuffix = @"1_9_1";
            _versionUnderTest = DiannHelpers.DIANN_191_VERSION;
            RunFunctionalTest();
        }

        /// <summary>
        /// Repro for the "no peptides" case: 1.9.1 + strict 1% FDR drops every ID from
        /// the tiny test inputs, producing a zero-row lib.parquet. Without the DIA-NN
        /// engine's empty-results check, BlibBuild would be invoked on the empty
        /// parquet, hit abort_current_library on the empty redundant.blib, fail to
        /// remove the file (unfinalized SQLite statements keep the handle open), and
        /// surface a confusing "process cannot access the file" IOException.
        /// </summary>
        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoUnicodeTesting(@"DIA-NN 1.9.1 doesn't support UTF-8 paths properly.")]
        public void TestDiannSearchEmptyResults1_9_1()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            TestFilesZipSuffix = @"empty_1_9_1";
            _versionUnderTest = DiannHelpers.DIANN_191_VERSION;
            _expectEmptyResults = true;
            RunFunctionalTest();
        }

        /// <summary>
        /// A format DIA-NN cannot read is converted to mzML before the search, and the results
        /// of the converted file still reach the replicate named after the original.
        /// </summary>
        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiannSearchConverted()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            TestFilesZipSuffix = @"converted";
            _versionUnderTest = DiannHelpers.DIANN_VERSION;
            _searchFileName = @"23aug2017_hela_serum_timecourse_wide_1c.mzXML";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            VerifyDiannCfgParser();

            // Skyline command-line is version-agnostic — same wizard flow drives either
            // 2.5.0 (academic) or 1.9.1 (open license), selected by the TestMethod entry.
            var info = _versionUnderTest == DiannHelpers.DIANN_191_VERSION
                ? DiannHelpers.Diann191DownloadInfo
                : DiannHelpers.DiannDownloadInfo;
            RunSearchAgainstDiannVersion(_versionUnderTest, info);
        }

        private void RunSearchAgainstDiannVersion(string version, FileDownloadInfo downloadInfo)
        {
            // Per-version doc keeps both runs' artifacts side-by-side for diff/debug.
            PrepareDocument($"DiannSearchTest_{version}.sky");

            // SimpleFileDownloader's FileAlreadyDownloaded check consults SearchToolList
            // for SearchToolType.DIANN and returns that path if set — so if a previous
            // iteration (e.g. 2.5.0) registered its binary, the 1.9.1 download is skipped
            // because the 2.5.0 binary "already exists". Clear the entry so each iteration
            // checks the per-version path.
            RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                    Settings.Default.SearchToolList.Remove(
                        Settings.Default.SearchToolList[SearchToolType.DIANN]);
            });

            // SimpleFileDownloader normally rewrites URLs to the Skyline test mirror under
            // unit tests. The mirror only has DIANN-2.5.0; for 1.9.1 we need to hit GitHub
            // directly. Toggle UseOriginalURLs around the call (and only around the call
            // — leave the global state untouched for any subsequent download).
            var progress = new SilentProgressMonitor();
            bool savedUseOriginalUrls = Skyline.Program.UseOriginalURLs;
            try
            {
                if (version == DiannHelpers.DIANN_191_VERSION)
                    Skyline.Program.UseOriginalURLs = true;
                AssertEx.IsTrue(SimpleFileDownloader.DownloadRequiredFiles(new[] { downloadInfo }, progress),
                    $@"failed to download DIA-NN {version}");
            }
            finally
            {
                Skyline.Program.UseOriginalURLs = savedUseOriginalUrls;
            }
            string realDiannPath = downloadInfo.ToolPath;
            AssertEx.IsTrue(File.Exists(realDiannPath),
                $@"DIA-NN {version} not at expected path after download: {realDiannPath}");

            // Point SearchToolList at a path that doesn't exist on disk so DiannBinary
            // resolves to a non-existent path (forcing the install check to prompt),
            // regardless of what's actually present in the default Tools directory.
            const string bogusPath = @"C:\__diann_not_installed__\diann.exe";
            var customPreset = MakeCustomPreset();
            bool savedWarnOnPresetChange = Settings.Default.WarnOnPresetChange;
            RunUI(() =>
            {
                if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                    Settings.Default.SearchToolList.Remove(
                        Settings.Default.SearchToolList[SearchToolType.DIANN]);
                Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN,
                    bogusPath, string.Empty, Path.GetDirectoryName(bogusPath), false));
                Settings.Default.SearchSettingsPresets.Add(customPreset);
                Settings.Default.WarnOnPresetChange = false; // Presets change on later pages here
            });
            try
            {
                RunSearch(version, realDiannPath, customPreset);
            }
            finally
            {
                DiannHelpers.RegisteredDiannPathOverride = null;
                RunUI(() =>
                {
                    Settings.Default.WarnOnPresetChange = savedWarnOnPresetChange;
                    foreach (var name in new[] { customPreset.Name, SNAPSHOT_PRESET_NAME })
                    {
                        var preset = Settings.Default.SearchSettingsPresets.FirstOrDefault(p => p.Name == name);
                        if (preset != null)
                            Settings.Default.SearchSettingsPresets.Remove(preset);
                    }
                });
            }
        }

        private const string SNAPSHOT_PRESET_NAME = @"DIA-NN unit-test snapshot";

        private void RunSearch(string version, string realDiannPath, SearchSettingsPreset customPreset)
        {
            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library.fasta");
            // Single-file search exercises the no-MBR path. Multi-file coverage belongs
            // in a perf test; this functional test is intentionally minimal.
            string[] diaFilePaths =
            {
                TestFilesDir.GetTestPath(_searchFileName)
            };
            string documentDir = Path.GetDirectoryName(SkylineWindow.DocumentFilePath) ?? string.Empty;

            // The DIA-NN Search menu item opens Run Peptide Search set up to search DIA spectra with DIA-NN
            var dlg = ShowDialog<RunPeptideSearchDlg>(SkylineWindow.ShowDiannSearchDlg);
            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.spectra_page, dlg.CurrentPage);
                AssertEx.AreEqual(PeptideSearchWizardDlg.Workflow.dia, dlg.BuildPepSearchLibControl.WorkflowType);
                dlg.BuildPepSearchLibControl.InputFileType = PeptideSearchWizardDlg.InputFile.dia_raw;
                dlg.BuildPepSearchLibControl.DdaSearchDataSources = diaFilePaths
                    .Select(p => (MsDataFileUri) new MsDataFilePath(p)).ToArray();

                // A DIA-NN preset chosen on the first page reaches the DIA-NN engine, which the search
                // settings page makes only once the DIA search method page has chosen DIA-NN
                dlg.SelectedPresetName = customPreset.Name;
                AssertEx.IsTrue(dlg.ClickNextButton()); // 1 file: no prefix dialog
            });

            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.match_modifications_page, dlg.CurrentPage);
                var checkedMods = dlg.MatchModificationsControl.CheckedModificationNames.ToList();
                AssertEx.IsTrue(checkedMods.Contains(@"Carbamidomethyl (C)"), string.Join(@", ", checkedMods));
                AssertEx.IsTrue(checkedMods.Contains(@"Oxidation (M)"), string.Join(@", ", checkedMods));
                AssertEx.IsTrue(dlg.ClickNextButton());
            });

            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.import_fasta_page, dlg.CurrentPage);
                AssertEx.AreEqual(@"Trypsin/P", dlg.ImportFastaControl.Enzyme?.Name);
                AssertEx.AreEqual(2, dlg.ImportFastaControl.MaxMissedCleavages);
                // DIA-NN supplies peak boundaries, so no decoys and no mProphet model unless the user asks
                AssertEx.IsTrue(string.IsNullOrEmpty(dlg.ImportFastaControl.DecoyGenerationMethod));
                AssertEx.IsFalse(dlg.ImportFastaControl.AutoTrain);
                dlg.ImportFastaControl.SetFastaContent(fastaFilepath);
                AssertEx.IsTrue(dlg.ClickNextButton());
            });

            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.converter_settings_page, dlg.CurrentPage);
                AssertEx.IsFalse(dlg.ConverterSettingsControl.UseDiaUmpire);
                AssertEx.AreEqual(SearchEngine.DIANN, dlg.ConverterSettingsControl.DirectDiaSearchEngine);
                AssertEx.IsTrue(dlg.ClickNextButton());
            });

            RunUI(() =>
            {
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.dda_search_settings_page, dlg.CurrentPage);
                var settings = dlg.SearchSettingsControl;
                AssertEx.AreEqual(SearchEngine.DIANN, settings.SelectedSearchEngine);
                AssertEx.AreEqualDeep(new[] { SearchEngine.DIANN }, settings.SearchEngineChoices.ToArray());
                VerifyPresetApplied(dlg, 15, 25, 0.05, 6, 40, 2, 5);

                // Round trip through a saved preset
                dlg.SaveSettingsPreset(SNAPSHOT_PRESET_NAME);
                var snapshot = Settings.Default.SearchSettingsPresets.First(p => p.Name == SNAPSHOT_PRESET_NAME);
                AssertEx.AreEqual(SearchEngine.DIANN, snapshot.SearchEngine);
                AssertEx.AreEqual(15.0, snapshot.PrecursorToleranceValue);
                AssertEx.AreEqual(25.0, snapshot.FragmentToleranceValue);
                AssertEx.AreEqual(0.05, snapshot.CutoffScore);
                AssertEx.AreEqual(@"Trypsin/P", snapshot.EnzymeName);
                AssertEx.AreEqual(2, snapshot.MaxMissedCleavages);
                AssertEx.AreEqual(SearchWorkflowType.dia, snapshot.Workflow);
                AssertEx.IsTrue(snapshot.HasExplicitModifications);
                AssertEx.AreEqual(2, snapshot.StructuralModifications.Count);
                AssertEx.IsTrue(snapshot.AdditionalSettingsXml?.Contains(@"MinPepLen") == true);
                AssertEx.IsTrue(snapshot.AdditionalSettingsXml?.Contains(@"MaxPrecursorCharge") == true);

                // A built-in DIA-NN preset applied over it sets every value back
                dlg.SelectedPresetName = DiannHelpers.PRESET_ORBITRAP;
                VerifyPresetApplied(dlg, 10, 20, 0.01, 7, 30, 2, 3);
                AssertEx.AreEqual(@"Trypsin", dlg.ImportFastaControl.Enzyme?.Name);
                AssertEx.AreEqual(1, dlg.ImportFastaControl.MaxMissedCleavages);

                // The settings of this search. 1.9.1's --gen-spec-lib heuristic drops every
                // confident ID from a tiny input at strict 1% FDR. Loosening to 30% gives the
                // heuristic enough candidates that some pass through and the lib parquet gets
                // populated, and the library build keeps them at the same cutoff; 0.3 is also
                // the largest q-value Skyline considers non-"unusually permissive"
                // (BlibBuild.SuggestedRange max = 0.3). 2.5.0 stays at 1%. The empty-results
                // test keeps 1.9.1 at 1% precisely to drive that zero-entry path.
                settings.CutoffScore = version == DiannHelpers.DIANN_191_VERSION && !_expectEmptyResults ? 0.3 : 0.01;
                settings.SetAdditionalSetting(DiannSearchEngine.THREADS, Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture));
            });

            // Leaving the search settings page makes sure DIA-NN is installed.
            // No DIA-NN registered anywhere: the download dialog, which leaves the user on the page when canceled.
            DiannHelpers.RegisteredDiannPathOverride = () => null;
            var downloadDlg = ShowDialog<DiannDownloadDlg>(() => dlg.ClickNextButton());
            OkDialog(downloadDlg, () => downloadDlg.DialogResult = DialogResult.Cancel);
            RunUI(() => AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.dda_search_settings_page, dlg.CurrentPage));

            // An existing DIA-NN install is "discovered" via the registry: the user is asked to reuse it or
            // download one; choose "Use Existing".
            DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
            var useExisting = ShowDialog<MultiButtonMsgDlg>(() => dlg.ClickNextButton());
            OkDialog(useExisting, () => useExisting.DialogResult = DialogResult.Yes);
            WaitForConditionUI(() => dlg.CurrentPage == PeptideSearchWizardDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                AssertEx.AreEqual(realDiannPath, DiannHelpers.DiannBinary);
                AssertEx.IsTrue(dlg.ClickNextButton());
                AssertEx.AreEqual(PeptideSearchWizardDlg.Pages.full_scan_settings_page, dlg.CurrentPage);
                dlg.FullScanSettingsControl.IsolationSchemeName = PropertiesResources.IsolationSchemeList_GetDefaults_Results_only;
            });

            // Run. The new document has no library of its own, so nothing asks whether to replace it.
            SkylineWindow.BeginInvoke(() => dlg.ClickNextButton());

            if (_expectEmptyResults)
            {
                // DIA-NN returned a zero-row lib.parquet. The run stops with a message that says so,
                // instead of handing the empty library to BlibBuild, which fails confusingly on it.
                var emptyMsg = WaitForOpenForm<MessageDlg>(300000);
                RunUI(() => AssertEx.AreEqual(string.Format(ModelResources.DiannSearchEngine_Run_DIA_NN_found_no_precursors_at_q_value__0_, 0.01),
                    emptyMsg.Message));
                OkDialog(emptyMsg, emptyMsg.OkDialog);
                WaitForConditionUI(() => dlg.RunSucceeded.HasValue);
                RunUI(() => AssertEx.IsFalse(dlg.RunSucceeded.Value));

                string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
                AssertEx.IsFalse(File.Exists(docBlibPath), @"no .blib should be built from empty results");
                AssertEx.IsFalse(File.Exists(BiblioSpecLiteSpec.GetRedundantName(docBlibPath)),
                    @"no .redundant.blib should be built from empty results");

                OkDialog(dlg, dlg.ClickCancelButton);
                return;
            }

            try
            {
                WaitForConditionUI(300000, () => dlg.RunSucceeded.HasValue, () => dlg.SearchControl.LogText); // DIA-NN can take a while
                RunUI(() => AssertEx.IsTrue(dlg.RunSucceeded.Value, dlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("DiannSearchControlLog.txt", dlg.SearchControl.LogText);
            }

            RunUI(() =>
            {
                // DIA-NN's console output is the run page's log file, and its library is the search result
                AssertEx.IsTrue(dlg.SearchControl.CanOpenLog);
                AssertEx.AreEqual(Path.Combine(documentDir, DiannHelpers.STDOUT_LOG_NAME), dlg.SearchControl.LogFilePath);
                AssertEx.IsTrue(File.Exists(Path.Combine(documentDir, DiannHelpers.OUTPUT_LIB_NAME)));
            });

            // DIA-NN searched the mzML conversion of a file it cannot read
            var searchFilePath = diaFilePaths[0];
            var convertedPath = Path.Combine(Path.GetDirectoryName(searchFilePath) ?? string.Empty,
                MsconvertDdaConverter.OUTPUT_SUBDIRECTORY, Path.GetFileNameWithoutExtension(searchFilePath) + @".mzML");
            bool isConverted = new DiannSearchEngine(documentDir).GetSearchFileNeedsConversion(new MsDataFilePath(searchFilePath), out _);
            AssertEx.AreEqual(_searchFileName.EndsWith(@".mzXML"), isConverted);
            AssertEx.AreEqual(isConverted, File.Exists(convertedPath), convertedPath);

            // The truncated mzML may not yield enough confident IDs to populate every target type, so the
            // counts are not checked: what matters is that the run goes through every step and BlibBuild
            // converts the DIA-NN parquet into the document's library.
            OkDialog(dlg, () => dlg.ClickNextButton()); // Finish
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                AssertEx.IsTrue(SkylineWindow.Document.Settings.HasDocumentLibrary);
                SkylineWindow.SaveDocument();
            });
            VerifyPeakBoundsFromLibrary(searchFilePath);
        }

        /// <summary>
        /// DIA-NN's peak boundaries in the library, which name the file DIA-NN searched, set the
        /// peaks of the replicate, which names the file the user chose.
        /// </summary>
        private void VerifyPeakBoundsFromLibrary(string searchFilePath)
        {
            var doc = SkylineWindow.Document;
            var replicateFile = doc.Settings.MeasuredResults.Chromatograms.Single().MSDataFilePaths.Single();
            AssertEx.AreEqual(Path.GetFileName(searchFilePath), replicateFile.GetFileName());
            var library = doc.Settings.PeptideSettings.Libraries.Libraries.Single();
            int peptidesWithBounds = 0;
            foreach (var peptide in doc.Peptides)
            {
                var bounds = library.GetExplicitPeakBounds(replicateFile, new[] { peptide.ModifiedTarget });
                if (bounds == null)
                    continue;
                peptidesWithBounds++;
                foreach (var nodeGroup in peptide.TransitionGroups)
                {
                    var chromInfo = nodeGroup.Results?[0].FirstOrDefault();
                    AssertEx.IsNotNull(chromInfo?.StartRetentionTime, peptide.ModifiedSequenceDisplay);
                    AssertEx.AreEqual(bounds.StartTime, chromInfo.StartRetentionTime.Value, PEAK_BOUND_TOLERANCE, peptide.ModifiedSequenceDisplay);
                    AssertEx.AreEqual(bounds.EndTime, chromInfo.EndRetentionTime.Value, PEAK_BOUND_TOLERANCE, peptide.ModifiedSequenceDisplay);
                }
            }
            AssertEx.IsTrue(peptidesWithBounds > 0, @"no library peak boundaries for " + replicateFile);
        }

        // Peak boundaries snap to the nearest chromatogram point, which can be a scan cycle away
        private const double PEAK_BOUND_TOLERANCE = 0.1;

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }

        /// <summary>
        /// A DIA-NN preset with values that differ from both the wizard's defaults and the built-in
        /// presets (e.g. enzyme "Trypsin/P" instead of "Trypsin"), so a value the wizard fails to apply shows.
        /// </summary>
        private static SearchSettingsPreset MakeCustomPreset()
        {
            // Built-in DIA-NN presets are tagged with SearchEngine.DIANN and offered with the other engines' presets
            var defaults = DiannHelpers.GetDefaultPresets().ToList();
            AssertEx.AreEqual(3, defaults.Count, @"expected 3 built-in DIA-NN presets");
            foreach (var dflt in defaults)
            {
                AssertEx.AreEqual(SearchEngine.DIANN, dflt.SearchEngine);
                AssertEx.IsTrue(new SearchSettingsPresetList().GetDefaults().Any(p => p.Name == dflt.Name),
                    $@"built-in preset '{dflt.Name}' missing from the search settings presets");
            }

            var carbamidomethyl = UniMod.GetModification(@"Carbamidomethyl (C)", true);
            var oxidationM = UniMod.GetModification(@"Oxidation (M)", true).ChangeVariable(true);
            var custom = new SearchSettingsPreset(
                @"DIA-NN unit-test preset",
                SearchEngine.DIANN,
                new MzTolerance(15, MzTolerance.Units.ppm),
                new MzTolerance(25, MzTolerance.Units.ppm),
                maxVariableMods: 2,
                fragmentIons: null,
                ms2Analyzer: null,
                cutoffScore: 0.05,
                additionalSettings: new Dictionary<string, string>
                {
                    { @"MinPepLen", @"6" },
                    { @"MaxPepLen", @"40" },
                    { @"MinPrecursorCharge", @"2" },
                    { @"MaxPrecursorCharge", @"5" },
                },
                enzymeName: @"Trypsin/P", // intentionally NOT EnzymeList[0] (Trypsin)
                maxMissedCleavages: 2,
                structuralModifications: new[] { carbamidomethyl, oxidationM },
                workflowType: SearchWorkflowType.dia,
                hasExplicitModifications: true);

            // XML round-trip: the user reported custom DIA-NN presets disappearing on
            // Skyline restart. Mirror what happens on Save()/Reload by writing the preset
            // to XML and reading it back, asserting every relevant field survives.
            var written = new StringWriter();
            using (var w = System.Xml.XmlWriter.Create(written, new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                w.WriteStartElement(@"search_workflow");
                custom.WriteXml(w);
                w.WriteEndElement();
            }
            SearchSettingsPreset deserialized;
            using (var r = System.Xml.XmlReader.Create(new StringReader(written.ToString())))
            {
                r.MoveToContent();
                deserialized = SearchSettingsPreset.Deserialize(r);
            }
            AssertEx.AreEqual(custom.Name, deserialized.Name);
            AssertEx.AreEqual(custom.SearchEngine, deserialized.SearchEngine);
            AssertEx.AreEqual(custom.PrecursorToleranceValue, deserialized.PrecursorToleranceValue);
            AssertEx.AreEqual(custom.FragmentToleranceValue, deserialized.FragmentToleranceValue);
            AssertEx.AreEqual(custom.CutoffScore, deserialized.CutoffScore);
            AssertEx.AreEqual(custom.EnzymeName, deserialized.EnzymeName);
            AssertEx.AreEqual(custom.MaxMissedCleavages, deserialized.MaxMissedCleavages);
            AssertEx.AreEqual(custom.Workflow, deserialized.Workflow);
            AssertEx.AreEqual(custom.HasExplicitModifications, deserialized.HasExplicitModifications);
            AssertEx.AreEqual(custom.StructuralModifications.Count, deserialized.StructuralModifications.Count);
            AssertEx.IsTrue(deserialized.AdditionalSettingsXml?.Contains(@"MinPepLen") == true,
                @"AdditionalSettingsXml did not survive round-trip");
            return custom;
        }

        private static void VerifyPresetApplied(RunPeptideSearchDlg dlg, double ms1Ppm, double ms2Ppm, double qValue,
            int minPepLen, int maxPepLen, int minCharge, int maxCharge)
        {
            var settings = dlg.SearchSettingsControl;
            AssertEx.AreEqual(ms1Ppm, settings.PrecursorTolerance.Value);
            AssertEx.AreEqual(MzTolerance.Units.ppm, settings.PrecursorTolerance.Unit);
            AssertEx.AreEqual(ms2Ppm, settings.FragmentTolerance.Value);
            AssertEx.AreEqual(MzTolerance.Units.ppm, settings.FragmentTolerance.Unit);
            AssertEx.AreEqual(qValue, settings.CutoffScore);
            var additional = settings.AdditionalSettings;
            AssertEx.AreEqual(minPepLen, additional[@"MinPepLen"].Value);
            AssertEx.AreEqual(maxPepLen, additional[@"MaxPepLen"].Value);
            AssertEx.AreEqual(minCharge, additional[@"MinPrecursorCharge"].Value);
            AssertEx.AreEqual(maxCharge, additional[@"MaxPrecursorCharge"].Value);
        }

        /// <summary>
        /// Write a representative DIA-NN .cfg file (the command-line argument list DIA-NN
        /// stores alongside its outputs) and verify SearchSettingsParamsFileParser maps it
        /// to a preset with the expected tolerances, enzyme, mods, and AdditionalSettings.
        /// </summary>
        private void VerifyDiannCfgParser()
        {
            // Variant 1: legacy flag names that older DIA-NN releases (≤1.9) emitted.
            VerifyDiannCfgParserOne(@"--ms1-accuracy 10", @"--ms2-accuracy 20");
            // Variant 2: 2.x flag names — same numbers, different spellings. Both must
            // round-trip identically since Skyline emits the new names but should still
            // import either.
            VerifyDiannCfgParserOne(@"--mass-acc-ms1 10", @"--mass-acc 20");
            // Multi-fasta cfgs should yield a clean single-path preset, not a path
            // containing an embedded U+001F that downstream code will choke on.
            VerifyDiannMultiFastaCfgParser();
        }

        private void VerifyDiannMultiFastaCfgParser()
        {
            var cfgPath = Path.Combine(Path.GetTempPath(), $@"diann-multifasta-{Guid.NewGuid():N}.cfg");
            File.WriteAllLines(cfgPath, new[]
            {
                @"--f ""C:\data\run1.raw""",
                @"--fasta ""C:\db\first.fasta""",
                @"--fasta ""C:\db\second.fasta""",
                @"--ms1-accuracy 10",
                @"--ms2-accuracy 20",
            });
            try
            {
                var preset = SearchSettingsParamsFileParser.ImportFromFile(cfgPath, @"DIA-NN-multifasta-test");
                AssertEx.AreEqual(@"C:\db\first.fasta", preset.FastaFilePath,
                    @"expected first --fasta path to be kept verbatim, no U+001F embedded");
                AssertEx.IsFalse(preset.FastaFilePath.Contains(''),
                    @"FastaFilePath must not contain unit separator");
            }
            finally
            {
                if (File.Exists(cfgPath))
                    File.Delete(cfgPath);
            }
        }

        private void VerifyDiannCfgParserOne(string ms1Flag, string ms2Flag)
        {
            var cfgPath = Path.Combine(Path.GetTempPath(), $@"diann-{Guid.NewGuid():N}.cfg");
            File.WriteAllLines(cfgPath, new[]
            {
                @"# Auto-generated by DIA-NN",
                @"--f ""C:\data\run1.raw""",
                @"--fasta ""C:\db\uniprot_human.fasta""",
                ms1Flag,
                ms2Flag,
                @"--qvalue 0.01",
                @"--threads 8",
                @"--min-pep-len 7",
                @"--max-pep-len 30",
                @"--min-pr-charge 2",
                @"--max-pr-charge 3",
                @"--cut K*,R*,!*P",
                @"--missed-cleavages 1",
                @"--met-excision",
                @"--fixed-mod UniMod:4,57.021464,C",
                @"--var-mod UniMod:35,15.994915,M",
            });
            try
            {
                var preset = SearchSettingsParamsFileParser.ImportFromFile(cfgPath, @"DIA-NN-import-test");
                AssertEx.AreEqual(SearchEngine.DIANN, preset.SearchEngine);
                AssertEx.AreEqual(10.0, preset.PrecursorToleranceValue);
                AssertEx.AreEqual(MzTolerance.Units.ppm, preset.PrecursorToleranceUnit);
                AssertEx.AreEqual(20.0, preset.FragmentToleranceValue);
                AssertEx.AreEqual(0.01, preset.CutoffScore);
                AssertEx.AreEqual(@"Trypsin", preset.EnzymeName);
                AssertEx.AreEqual(1, preset.MaxMissedCleavages);
                AssertEx.AreEqual(@"C:\db\uniprot_human.fasta", preset.FastaFilePath);
                AssertEx.AreEqual(SearchWorkflowType.dia, preset.Workflow);
                AssertEx.IsTrue(preset.HasExplicitModifications);
                AssertEx.AreEqual(2, preset.StructuralModifications.Count);
                AssertEx.IsTrue(preset.StructuralModifications.Any(m => m.UnimodId == 4 && !m.IsVariable),
                    @"expected fixed Carbamidomethyl (C)");
                AssertEx.IsTrue(preset.StructuralModifications.Any(m => m.UnimodId == 35 && m.IsVariable),
                    @"expected variable Oxidation (M)");
                AssertEx.Contains(preset.AdditionalSettingsXml, @"MinPepLen");
                AssertEx.Contains(preset.AdditionalSettingsXml, @"MaxPrecursorCharge");
            }
            finally
            {
                if (File.Exists(cfgPath))
                    File.Delete(cfgPath);
            }
        }
    }
}
