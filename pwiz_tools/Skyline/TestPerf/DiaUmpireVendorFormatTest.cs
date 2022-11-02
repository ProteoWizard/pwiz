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
using pwiz.Skyline;
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

        [TestMethod]
        public void TestDiaUmpireWiffFile()
        {
            // do not run full filesets for nightly tests
            if (Program.SkylineOffscreen)
                return;

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
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

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
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = searchFiles.Select(f => new MsDataFilePath(f)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.CIRT_SHORT;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                importPeptideSearchDlg.BuildPepSearchLibControl.InputFileType = ImportPeptideSearchDlg.InputFile.dia_raw;
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.95, importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
            });

            //WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);

            WaitForDocumentLoaded();

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page));

            var editStructModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editStructModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(OXIDATION_M, true); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editStructModListUI, editStructModListUI.OkDialog);

            RunUI(() => importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true));

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = false;
                importPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = new[]
                {
                    /*Adduct.SINGLY_PROTONATED, */Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED
                };
                // Default is to have precursors
                if (_instrumentValues.KeepPrecursors)
                {
                    AssertEx.AreEqualDeep(new[] {IonType.y, IonType.b, IonType.precursor},
                        importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes);
                }
                else
                {
                    importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes = new[]
                    {
                        IonType.y, IonType.b    // Removes precursor
                    };
                }
                // Verify other values shown in the tutorial
                Assert.AreEqual(6, importPeptideSearchDlg.TransitionSettingsControl.IonCount);
                Assert.AreEqual(6, importPeptideSearchDlg.TransitionSettingsControl.MinIonCount);
                Assert.AreEqual(0.05, importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                if (_instrumentValues.KeepPrecursors)
                    importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes = 20;
                importPeptideSearchDlg.FullScanSettingsControl.ProductRes = 20;

                Assert.AreEqual(importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent, FullScanPrecursorIsotopes.None);
                Assert.AreEqual(FullScanMassAnalyzerType.centroided, importPeptideSearchDlg.FullScanSettingsControl.ProductMassAnalyzer);
                //Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundPrediction);
            });

            var isolationScheme =
                ShowDialog<EditIsolationSchemeDlg>(importPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);
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

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPathForSearch);
                importPeptideSearchDlg.ImportFastaControl.FastaImportTargetsFile = fastaPathForImport;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = true;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.ConverterSettingsControl.InstrumentPreset = _instrumentValues.InstrumentPreset;
                importPeptideSearchDlg.ConverterSettingsControl.EstimateBackground = true;
                importPeptideSearchDlg.ConverterSettingsControl.AdditionalSettings = _instrumentValues.AdditionalSettings;
            });

            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                              ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = _instrumentValues.PrecursorTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = _instrumentValues.FragmentTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";
            });

            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.SearchControl.OnSearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            WaitForConditionUI(120 * 600000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            RunUI(() => importPeptideSearchDlg.ClickCancelButton());
        }
    }
}
