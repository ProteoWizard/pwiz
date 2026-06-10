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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
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
        // heuristic) and asserts that DiannSearchDlg surfaces a friendly empty-results
        // message instead of handing the empty lib.parquet off to BlibBuild (which would
        // otherwise abort with a confusing "process cannot access the file" error).
        private bool _expectEmptyResults;

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)]
        public void TestDiannSearch()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            _versionUnderTest = DiannHelpers.DIANN_VERSION;
            RunFunctionalTest();
        }

        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME),
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
        /// the tiny test inputs, producing a zero-row lib.parquet. Without the
        /// DiannSearchDlg empty-results gate, BlibBuild would be invoked on the empty
        /// parquet, hit abort_current_library on the empty redundant.blib, fail to
        /// remove the file (unfinalized SQLite statements keep the handle open), and
        /// surface a confusing "process cannot access the file" IOException.
        /// </summary>
        [TestMethod,
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE),
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME),
         NoUnicodeTesting(@"DIA-NN 1.9.1 doesn't support UTF-8 paths properly.")]
        public void TestDiannSearchEmptyResults1_9_1()
        {
            TestFilesZip = @"TestFunctional\DiannSearchTest.zip";
            TestFilesZipSuffix = @"empty_1_9_1";
            _versionUnderTest = DiannHelpers.DIANN_191_VERSION;
            _expectEmptyResults = true;
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

            // 1.9.1 needs a relaxed BlibBuild cutoff: with only ~6 IDs total across the
            // tiny test mzMLs, DIA-NN's Global.Q.Value (cross-run FDR) lands around 0.11
            // even though per-run Q.Value is ~0.003. Default 0.95 score (≈ q-value 0.05)
            // would reject all of them. 2.x's lib output skips this path entirely so
            // leaving the cutoff alone for 2.5.0 keeps that test honest. The empty-results
            // test also leaves it alone because we want DIA-NN itself to produce zero
            // entries, not have Skyline's post-filter strip them.
            double savedCutoff = Settings.Default.LibraryResultCutOff;
            if (version == DiannHelpers.DIANN_191_VERSION && !_expectEmptyResults)
                RunUI(() => Settings.Default.LibraryResultCutOff = 0.5);
            try
            {
                RunSearchAgainstDiannVersionImpl(version, downloadInfo);
            }
            finally
            {
                RunUI(() => Settings.Default.LibraryResultCutOff = savedCutoff);
            }
        }

        private void RunSearchAgainstDiannVersionImpl(string version, FileDownloadInfo downloadInfo)
        {

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
            bool savedUseOriginalUrls = pwiz.Skyline.Program.UseOriginalURLs;
            try
            {
                if (version == DiannHelpers.DIANN_191_VERSION)
                    pwiz.Skyline.Program.UseOriginalURLs = true;
                AssertEx.IsTrue(SimpleFileDownloader.DownloadRequiredFiles(new[] { downloadInfo }, progress),
                    $@"failed to download DIA-NN {version}");
            }
            finally
            {
                pwiz.Skyline.Program.UseOriginalURLs = savedUseOriginalUrls;
            }
            string realDiannPath = downloadInfo.ToolPath;
            AssertEx.IsTrue(File.Exists(realDiannPath),
                $@"DIA-NN {version} not at expected path after download: {realDiannPath}");

            DiannSearchDlg searchDlg;
            try
            {
                // Point SearchToolList at a path that doesn't exist on disk so DiannBinary
                // resolves to a non-existent path (forcing EnsureDiannInstalled to prompt),
                // regardless of what's actually present in the default Tools directory.
                const string bogusPath = @"C:\__diann_not_installed__\diann.exe";
                RunUI(() =>
                {
                    if (Settings.Default.SearchToolList.ContainsKey(SearchToolType.DIANN))
                        Settings.Default.SearchToolList.Remove(
                            Settings.Default.SearchToolList[SearchToolType.DIANN]);
                    Settings.Default.SearchToolList.Add(new SearchTool(SearchToolType.DIANN,
                        bogusPath, string.Empty, Path.GetDirectoryName(bogusPath), false));
                });

                // Part A: no DIA-NN registered anywhere -> DiannDownloadDlg appears.
                DiannHelpers.RegisteredDiannPathOverride = () => null;
                var downloadDlg = ShowDialog<DiannDownloadDlg>(SkylineWindow.ShowDiannSearchDlg);
                OkDialog(downloadDlg, () => downloadDlg.DialogResult = DialogResult.Cancel);
                AssertEx.IsNull(FindOpenForm<DiannSearchDlg>());

                // Part B: an existing DIA-NN install is "discovered" via registry ->
                // MultiButtonMsgDlg asks the user to reuse it or download; choose "Use Existing".
                DiannHelpers.RegisteredDiannPathOverride = () => realDiannPath;
                var useExisting = ShowDialog<MultiButtonMsgDlg>(SkylineWindow.ShowDiannSearchDlg);
                OkDialog(useExisting, () => useExisting.DialogResult = DialogResult.Yes);
                searchDlg = WaitForOpenForm<DiannSearchDlg>();
                RunUI(() => AssertEx.AreEqual(realDiannPath, DiannHelpers.DiannBinary));

                // Exercise preset apply/save before the wizard walk. Catches regressions
                // like the silent-fallback Enzyme lookup and any field that ApplyPreset
                // forgets to map.
                VerifyPresetRoundTrip(searchDlg);
            }
            finally
            {
                DiannHelpers.RegisteredDiannPathOverride = null;
            }

            string fastaFilepath = TestFilesDir.GetTestPath("pan_human_library.fasta");
            // Single-file search exercises the no-MBR path. Multi-file coverage belongs
            // in a perf test; this functional test is intentionally minimal.
            string[] diaFilePaths =
            {
                TestFilesDir.GetTestPath("23aug2017_hela_serum_timecourse_wide_1c.mzML")
            };

            // Page 0: Data files - add wide window DIA file
            RunUI(() => AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.data_files_page));
            RunUI(() => searchDlg.DataFileResults.FoundResultsFiles = diaFilePaths
                .Select(p => new ImportPeptideSearch.FoundResultsFile(Path.GetFileName(p), p)).ToArray());

            // Page 1: FASTA
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.fasta_page);
                searchDlg.ImportFastaControl.SetFastaContent(fastaFilepath);
            });

            // Page 2: Modifications (defaults: Carbamidomethyl C fixed, Oxidation M variable)
            RunUI(searchDlg.NextPage);
            RunUI(() => AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.modifications_page));

            // Page 3: Search settings
            RunUI(searchDlg.NextPage);
            RunUI(() =>
            {
                AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.search_settings_page);
                searchDlg.Ms1Tolerance = 10;
                searchDlg.Ms2Tolerance = 20;
                // 1.9.1's --gen-spec-lib heuristic drops every confident ID from a tiny
                // input at strict 1% FDR. Loosening to 30% gives the heuristic enough
                // candidates that some pass through and the lib parquet gets populated;
                // 0.3 is also the largest q-value Skyline's BlibBuild grid considers
                // non-"unusually permissive" (BlibBuild.SuggestedRange max = 0.3), so the
                // permissive-threshold warning dialog doesn't fire. 2.5.0 stays at 1%.
                // The empty-results test keeps 1.9.1 at 1% precisely to drive that
                // zero-entry path.
                searchDlg.QValueThreshold = (version == DiannHelpers.DIANN_191_VERSION && !_expectEmptyResults) ? 0.3 : 0.01;
                searchDlg.Threads = Environment.ProcessorCount;
                searchDlg.DiannSearchConfig.AdditionalSettings[@"MinPrecursorCharge"].Value = 2;
                searchDlg.DiannSearchConfig.AdditionalSettings[@"MaxPrecursorCharge"].Value = 3;
            });

            // Page 4: Start search
            RunUI(searchDlg.NextPage);

            // Wait for search to complete (DIA-NN can take a while)
            try
            {
                bool? searchSucceeded = null;
                searchDlg.SearchControl.SearchFinished += success => searchSucceeded = success;
                WaitForConditionUI(300000, () => searchSucceeded.HasValue); // 5 minute timeout
                RunUI(() => AssertEx.IsTrue(searchSucceeded.Value, searchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("DiannSearchControlLog.txt", searchDlg.SearchControl.LogText);
            }

            // Verify spectral library (parquet) was created
            AssertEx.IsTrue(File.Exists(searchDlg.SearchControl.OutputSpecLibPath));

            // Delete any leftover .blib from a previous run so the existing-blib
            // overwrite prompt doesn't fire (that path is covered in a dedicated test).
            string docBlibPath = BiblioSpecLiteSpec.GetLibraryFileName(SkylineWindow.DocumentFilePath);
            if (File.Exists(docBlibPath))
                File.Delete(docBlibPath);

            if (_expectEmptyResults)
            {
                // DIA-NN returned a zero-row lib.parquet — the empty-results gate in
                // DiannSearchDlg.ImportDiannLibrary should surface a friendly message
                // and leave the wizard on run_page instead of opening
                // ImportPeptideSearchDlg (which would call BlibBuild and hit the
                // unfinalized-statement / file-locked failure mode).
                var emptyMsg = ShowDialog<MessageDlg>(searchDlg.NextPage);
                AssertEx.Contains(emptyMsg.Message, @"no precursors");
                OkDialog(emptyMsg, emptyMsg.OkDialog);
                AssertEx.IsNull(FindOpenForm<ImportPeptideSearchDlg>(),
                    @"ImportPeptideSearchDlg should not open when DIA-NN returns 0 precursors");
                RunUI(() => AssertEx.IsTrue(searchDlg.CurrentPage == DiannSearchDlg.Pages.run_page,
                    @"wizard should stay on run_page so user can go back to settings"));

                // Confirm the .blib is also clean — even though BlibBuild was never
                // invoked, the gate must not have left a partial file behind.
                AssertEx.IsFalse(File.Exists(docBlibPath),
                    @"no .blib should exist when empty-results gate fired");
                AssertEx.IsFalse(File.Exists(BiblioSpecLiteSpec.GetRedundantName(docBlibPath)),
                    @"no .redundant.blib should exist when empty-results gate fired");

                OkDialog(searchDlg, () => searchDlg.DialogResult = DialogResult.Cancel);
                return;
            }

            // Transition to Import Peptide Search wizard. DiannSearchDlg pre-loads the
            // DIA-NN -lib.parquet as the search input; the wizard opens on the spectra
            // page and BlibBuild converts the parquet to a .blib via DiaNNSpecLibReader.
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(searchDlg.NextPage);
            WaitForDocumentLoaded();
            // BlibBuild score-type lookup on the parquet runs async on the spectra page;
            // wait for it to finish before the Next button can validate.
            WaitForConditionUI(() => importPeptideSearchDlg.BuildPepSearchLibControl.Grid.ScoreTypesLoaded);

            // 1.9.1's lib parquet only contains entries from the relaxed --qvalue 0.3
            // search (1% FDR drops all entries due to 1.9.1's empirical-lib heuristic).
            // Match the BlibBuild per-file score_threshold so the entries survive the
            // filter. 0.3 sits at the top of Skyline's "non-permissive" q-value range,
            // so this also avoids the MultiButtonMsgDlg confirmation.
            if (version == DiannHelpers.DIANN_191_VERSION)
            {
                RunUI(() =>
                {
                    foreach (var f in importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files)
                        f.ScoreThreshold = 0.3;
                });
            }

            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

                // DiannSearchDlg pre-populated the chromatograms page with the searched
                // files, so all should appear in Found and Missing should be empty
                // (no second entry under a different filename casing/extension).
                var importResults = importPeptideSearchDlg.ImportResultsControl;
                var foundPaths = importResults.FoundResultsFiles.Select(f => f.Path).ToHashSet();
                AssertEx.AreEqual(diaFilePaths.Length, foundPaths.Count,
                    $@"expected {diaFilePaths.Length} found result files, got {foundPaths.Count}");
                foreach (var p in diaFilePaths)
                    AssertEx.IsTrue(foundPaths.Contains(p), $@"missing pre-populated file in Found list: {p}");
                AssertEx.IsFalse(importResults.MissingResultsFiles.Any(),
                    $@"unexpected entries in Missing list: {string.Join(@", ", importResults.MissingResultsFiles)}");

            });

            RunUI(() => AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton())); // 1 file → no rename dialog

            RunUI(() =>
            {
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                // DIA-NN default mods (Carbamidomethyl C, Oxidation M) are detected from
                // the library; add them all so library precursors match document targets.
                importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                importPeptideSearchDlg.FullScanSettingsControl.IsolationSchemeName =
                    PropertiesResources.IsolationSchemeList_GetDefaults_Results_only;
                AssertEx.IsTrue(importPeptideSearchDlg.ClickNextButton());
                AssertEx.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled = false;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = false;
            });

            // Finish wizard → AssociateProteinsDlg. The truncated mzML may not yield enough
            // confident IDs to populate every target type, so we don't assert specific counts
            // here — the value of this test is that the wizard advances cleanly through every
            // page and BlibBuild successfully converts the DIA-NN parquet to a .blib.
            var associateProteinsDlg = ShowDialog<AssociateProteinsDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
            WaitForConditionUI(() => associateProteinsDlg.DocumentFinalCalculated);
            using (new WaitDocumentChange(null, true))
            {
                OkDialog(associateProteinsDlg, associateProteinsDlg.OkDialog);
            }

            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void PrepareDocument(string documentFile)
        {
            RunUI(SkylineWindow.NewDocument);
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(documentFile)));
        }

        /// <summary>
        /// Apply a non-default preset to <paramref name="searchDlg"/> and assert every field
        /// the preset is supposed to control actually changed on the dialog. Then snapshot
        /// the dialog back into a preset and assert the values round-trip. Picks values
        /// that differ from both the dialog's startup defaults AND each other (e.g. enzyme
        /// "Trypsin/P" instead of "Trypsin") so silent-fallback bugs are visible.
        /// </summary>
        private void VerifyPresetRoundTrip(DiannSearchDlg searchDlg)
        {
            // Built-in DIA-NN presets must be registered against the SearchSettingsPresets
            // list (DiannSearchSettingsPresets) and tagged with SearchEngine.DIANN.
            var defaults = DiannHelpers.GetDefaultPresets().ToList();
            AssertEx.AreEqual(3, defaults.Count, @"expected 3 built-in DIA-NN presets");
            foreach (var dflt in defaults)
            {
                AssertEx.AreEqual(SearchEngine.DIANN, dflt.SearchEngine);
                AssertEx.IsTrue(Settings.Default.DiannSearchSettingsPresets.Any(p => p.Name == dflt.Name),
                    $@"built-in preset '{dflt.Name}' missing from DiannSearchSettingsPresets");
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

            RunUI(() =>
            {
                searchDlg.ApplyPreset(custom);
                AssertEx.AreEqual(15.0, searchDlg.Ms1Tolerance);
                AssertEx.AreEqual(25.0, searchDlg.Ms2Tolerance);
                AssertEx.AreEqual(0.05, searchDlg.QValueThreshold);
                AssertEx.AreEqual(@"Trypsin/P", searchDlg.ImportFastaControl.Enzyme?.Name,
                    @"ApplyPreset did not switch enzyme to 'Trypsin/P'");
                AssertEx.AreEqual(2, searchDlg.ImportFastaControl.MaxMissedCleavages);
                var fixedNames = searchDlg.FixedMods.Select(m => m.Name).ToList();
                var variableNames = searchDlg.VariableMods.Select(m => m.Name).ToList();
                AssertEx.IsTrue(fixedNames.Contains(@"Carbamidomethyl (C)"),
                    $@"expected fixed Carbamidomethyl (C), got [{string.Join(",", fixedNames)}]");
                AssertEx.IsTrue(variableNames.Contains(@"Oxidation (M)"),
                    $@"expected variable Oxidation (M), got [{string.Join(",", variableNames)}]");

                // Engine-specific bag landed in DiannSearchConfig.AdditionalSettings.
                var addl = searchDlg.DiannSearchConfig.AdditionalSettings;
                AssertEx.AreEqual(@"6", addl[@"MinPepLen"].Value.ToString());
                AssertEx.AreEqual(@"40", addl[@"MaxPepLen"].Value.ToString());
                AssertEx.AreEqual(@"2", addl[@"MinPrecursorCharge"].Value.ToString());
                AssertEx.AreEqual(@"5", addl[@"MaxPrecursorCharge"].Value.ToString());

                // Round-trip: snapshot the dialog into a new preset and assert the
                // values match what we just applied.
                var snapshot = searchDlg.BuildPresetFromCurrentSettings(@"snapshot");
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

                // Re-apply a built-in default and verify the values flip back. This catches
                // any state that ApplyPreset doesn't reset cleanly.
                var defaultPreset = defaults.First(p => p.Name == DiannHelpers.PRESET_ORBITRAP);
                searchDlg.ApplyPreset(defaultPreset);
                AssertEx.AreEqual(10.0, searchDlg.Ms1Tolerance);
                AssertEx.AreEqual(20.0, searchDlg.Ms2Tolerance);
                AssertEx.AreEqual(0.01, searchDlg.QValueThreshold);
                AssertEx.AreEqual(@"Trypsin", searchDlg.ImportFastaControl.Enzyme?.Name,
                    @"ApplyPreset(Orbitrap) did not restore enzyme to 'Trypsin'");
                AssertEx.AreEqual(1, searchDlg.ImportFastaControl.MaxMissedCleavages);
                AssertEx.AreEqual(@"2", searchDlg.DiannSearchConfig.AdditionalSettings[@"MinPrecursorCharge"].Value.ToString());
                AssertEx.AreEqual(@"3", searchDlg.DiannSearchConfig.AdditionalSettings[@"MaxPrecursorCharge"].Value.ToString());
            });

            // XML round-trip: the user reported custom DIA-NN presets disappearing on
            // Skyline restart. Mirror what happens on Save()/Reload by writing the preset
            // to XML and reading it back, asserting every relevant field survives.
            var written = new System.IO.StringWriter();
            using (var w = System.Xml.XmlWriter.Create(written, new System.Xml.XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                w.WriteStartElement(@"search_workflow");
                custom.WriteXml(w);
                w.WriteEndElement();
            }
            SearchSettingsPreset deserialized;
            using (var r = System.Xml.XmlReader.Create(new System.IO.StringReader(written.ToString())))
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
            var cfgPath = Path.Combine(Path.GetTempPath(), $@"diann-multifasta-{System.Guid.NewGuid():N}.cfg");
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
            var cfgPath = Path.Combine(Path.GetTempPath(), $@"diann-{System.Guid.NewGuid():N}.cfg");
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
