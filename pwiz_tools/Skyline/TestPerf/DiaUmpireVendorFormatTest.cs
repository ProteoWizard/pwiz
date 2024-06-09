/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Verify DIA-Umpire can read vendor formats smoothly.
    /// </summary>
    [TestClass]
    public class DiaUmpireVendorFormatTest : AbstractFunctionalTestEx
    {
        private InstrumentSpecificValues _instrumentValues;

        private class InstrumentSpecificValues
        {
            public string InstrumentTypeName;
            public string[] TestZipFiles;
            public string[] DiaFiles;
            public string IsolationSchemeName;
            public MzTolerance PrecursorTolerance;
            public MzTolerance FragmentTolerance;
            public DiaUmpire.Config.InstrumentPreset InstrumentPreset;

            // This may be necessary in the future if the default settings change but we don't want the test results to change.
            public Dictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings;

            public bool KeepPrecursors;

            public string FastaPathForSearch => "DDA_search\\nodecoys_3mixed_human_yeast_ecoli_20140403_iRT.fasta";
            public string FastaPath => "DIA\\target_protein_sequences.fasta";

        }

        private string[] DiaFiles
        {
            get { return _instrumentValues.DiaFiles; }
        }
        private string InstrumentTypeName
        {
            get { return _instrumentValues.InstrumentTypeName; }
        }
        private string RootName { get; set; }

        [TestMethod, 
         NoParallelTesting(TestExclusionReason.VENDOR_FILE_LOCKING), // Reader wants exclusive read access to raw data?
         NoUnicodeTesting(TestExclusionReason.MZ5_UNICODE_ISSUES),
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME)] // Do not run full filesets for nightly tests
        public void TestDiaUmpireWiffFile()
        {

            _instrumentValues = new InstrumentSpecificValues
            {
                InstrumentTypeName = "TTOF",
                TestZipFiles = new[] { "PerfImportResultsAbDiaVsMz5.zip" },
                DiaFiles = new[]
                {
                    @"AB\5600_DIA\Hoofnagle_10xDil_SWATH_01.wiff"
                },
                IsolationSchemeName = "Hoofnagle",
                PrecursorTolerance = new MzTolerance(30, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(40, MzTolerance.Units.ppm),
                InstrumentPreset = DiaUmpire.Config.InstrumentPreset.TripleTOF,

                KeepPrecursors = false,
                //IrtFilterText = "iRT",
            };

            _instrumentValues.AdditionalSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>
            {
                //{ "MinMSIntensity", new AbstractDdaSearchEngine.Setting("MinMSIntensity", 20) },
                //{ "MinMSMSIntensity", new AbstractDdaSearchEngine.Setting("MinMSMSIntensity", 20) },
                { "StartRT", new AbstractDdaSearchEngine.Setting("StartRT", 20) },
                { "EndRT", new AbstractDdaSearchEngine.Setting("EndRT", 22) }
            };

            RootName = "DIA-" + InstrumentTypeName;

            TestFilesZipPaths = _instrumentValues.TestZipFiles.Select(o => GetPerfTestDataURL(o)).Concat(new [] { $"http://skyline.ms/tutorials/{RootName}.zip" }).ToArray();
            TestFilesPersistent = new[]
            {
                @"AB\5600_DIA",
                Path.Combine(RootName, "DDA_search"),
                Path.Combine(RootName, "DIA")
            };

            RunTest();
        }

        private void RunTest()
        {
            // RunPerfTests = true; // Uncomment this to force perf tests to run under Test Explorer

            RunFunctionalTest();
        }

        private const string OXIDATION_M = "Oxidation (M)";

        private string GetVendorFileTestPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(path);
        }
        private string GetFastaTestPath(string path)
        {
            return TestFilesDirs[1].GetTestPath(Path.Combine(RootName, path));
        }

        protected override void DoTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                d => d.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            string documentBaseName = "DIA-" + InstrumentTypeName + "-format-test";
            string documentFile = TestContext.GetTestResultsPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            // Launch the wizard
            var runPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            string[] searchFiles = DiaFiles.Select(p => GetVendorFileTestPath(p)).ToArray();
            foreach (var searchFile in searchFiles)
            {
                // delete -diaumpire files so they get regenerated instead of reused
                var searchFileDir = Path.GetDirectoryName(searchFile) ?? string.Empty;
                foreach (var diaumpireFile in Directory.GetFiles(searchFileDir, "*-diaumpire.*"))
                    FileEx.SafeDelete(diaumpireFile);
                Assert.IsTrue(File.Exists(searchFile), string.Format("File {0} does not exist.", searchFile));
            }

            string fastaPathForImport = GetFastaTestPath(_instrumentValues.FastaPath);
            Assert.IsTrue(File.Exists(fastaPathForImport));

            string fastaPathForSearch = GetFastaTestPath(_instrumentValues.FastaPathForSearch);
            Assert.IsTrue(File.Exists(fastaPathForSearch));

            RunUI(() =>
            {
                Assert.IsTrue(runPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                runPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                runPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = searchFiles.Select(f => new MsDataFilePath(f)).ToArray();
                runPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.CIRT_SHORT;
                runPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                runPeptideSearchDlg.BuildPepSearchLibControl.InputFileType = ImportPeptideSearchDlg.InputFile.dia_raw;
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.95, runPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore);
                Assert.IsFalse(runPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
                Assert.IsTrue(runPeptideSearchDlg.ClickNextButton());
            });

            //WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);

            WaitForDocumentLoaded();

            RunUI(() => Assert.IsTrue(runPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page));

            var editStructModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(runPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editStructModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(OXIDATION_M); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editStructModListUI, editStructModListUI.OkDialog);

            RunUI(() => runPeptideSearchDlg.MatchModificationsControl.ChangeAll(true));

            RunUI(() => Assert.IsTrue(runPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => runPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                runPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = false;
                runPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = new[]
                {
                    /*Adduct.SINGLY_PROTONATED, */Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED
                };
                // Default is to have precursors
                if (_instrumentValues.KeepPrecursors)
                {
                    AssertEx.AreEqualDeep(new[] {IonType.y, IonType.b, IonType.precursor},
                        runPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes);
                }
                else
                {
                    runPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes = new[]
                    {
                        IonType.y, IonType.b    // Removes precursor
                    };
                }
                // Verify other values shown in the tutorial
                Assert.AreEqual(6, runPeptideSearchDlg.TransitionSettingsControl.IonCount);
                Assert.AreEqual(6, runPeptideSearchDlg.TransitionSettingsControl.MinIonCount);
                Assert.AreEqual(0.05, runPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            RunUI(() => Assert.IsTrue(runPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(runPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                if (_instrumentValues.KeepPrecursors)
                    runPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20;
                runPeptideSearchDlg.FullScanSettingsControl.ProductRes = 20;

                Assert.AreEqual(runPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.None);
                Assert.AreEqual(FullScanMassAnalyzerType.centroided, runPeptideSearchDlg.FullScanSettingsControl.ProductMassAnalyzer);
                //Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, runPeptideSearchDlg.FullScanSettingsControl.TimeAroundPrediction);
            });

            var isolationScheme =
                ShowDialog<EditIsolationSchemeDlg>(runPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);
            RunUI(() =>
            {
                isolationScheme.IsolationSchemeName = _instrumentValues.IsolationSchemeName;
                isolationScheme.UseResults = false;
            });
            RunDlg<OpenDataSourceDialog>(isolationScheme.ImportRanges, importRangesDlg =>
            {
                importRangesDlg.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(searchFiles[0]));
                importRangesDlg.SelectFile(Path.GetFileName(DiaFiles[0]));
                importRangesDlg.Open();
            });

            WaitForConditionUI(() => 28 == isolationScheme.GetIsolationWindows().Count);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            WaitForConditionUI(() => runPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(runPeptideSearchDlg.ClickNextButton()));

            RunUI(() =>
            {
                Assert.IsTrue(runPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", runPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, runPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                runPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPathForSearch);
                runPeptideSearchDlg.ImportFastaControl.FastaImportTargetsFile = fastaPathForImport;
                Assert.IsTrue(runPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                runPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                runPeptideSearchDlg.ImportFastaControl.AutoTrain = true;
                Assert.IsTrue(runPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });

            RunUI(() =>
            {
                Assert.IsTrue(runPeptideSearchDlg.ClickNextButton());

                runPeptideSearchDlg.ConverterSettingsControl.InstrumentPreset = _instrumentValues.InstrumentPreset;
                runPeptideSearchDlg.ConverterSettingsControl.EstimateBackground = true;
                runPeptideSearchDlg.ConverterSettingsControl.AdditionalSettings = _instrumentValues.AdditionalSettings;
            });

            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(runPeptideSearchDlg.ClickNextButton());

                Assert.IsTrue(runPeptideSearchDlg.CurrentPage ==
                              ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                runPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = _instrumentValues.PrecursorTolerance;
                runPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = _instrumentValues.FragmentTolerance;
                runPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";
            });

            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(runPeptideSearchDlg.ClickNextButton());

                runPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                runPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            WaitForConditionUI(120 * 600000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            RunUI(() => runPeptideSearchDlg.ClickCancelButton());
        }
    }
}
