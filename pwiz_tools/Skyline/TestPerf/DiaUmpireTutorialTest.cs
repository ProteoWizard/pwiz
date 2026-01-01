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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.Irt;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Verify DIA/SWATH tutorial operation via DIA-Umpire
    /// </summary>
    [TestClass]
    public class DiaUmpireTutorialTest : AbstractFunctionalTestEx
    {
        private InstrumentSpecificValues _instrumentValues;
        private AnalysisValues _analysisValues;
        private ExpectedValues _expectedValues;
        private string _expectedValuesFilePath;

        private class InstrumentSpecificValues
        {
            public string InstrumentTypeName;
            public string[] DiaFiles;
            public bool HasAmbiguousMatches;
            public string IsolationSchemeName;
            public string IsolationSchemeFile;
            public char IsolationSchemeFileSeparator;
            public MzTolerance PrecursorTolerance;
            public MzTolerance FragmentTolerance;
            public DiaUmpire.Config.InstrumentPreset InstrumentPreset;

            // This may be necessary in the future if the default settings change but we don't want the tutorial results to change.
            //public Dictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings;
        }

        private class AnalysisValues
        {
            /// <summary>
            /// If true, all DiaFiles will be processed and searched with the full FASTA (FastaPathForSearch).
            /// If false, only first 2 DiaFiles will be processed and searched with targets-only FASTA (FastaPath).
            /// </summary>
            public bool IsWholeProteome;
            public bool KeepPrecursors;

            public string IrtFilterText;
            public int? MinPeptidesPerProtein;
            public bool RemoveDuplicates;
            public PointF ChromatogramClickPoint;

            public string FastaPathForSearch => "DDA_search\\nodecoys_3mixed_human_yeast_ecoli_20140403_iRT.fasta";
            public string FastaPath =>
                IsWholeProteome
                    ? "DDA_search\\nodecoys_3mixed_human_yeast_ecoli_20140403_iRT.fasta"
                    : "DIA\\target_protein_sequences.fasta";

        }

        private class ExpectedValues
        {
            public int LibraryPeptideCount;
            public double IrtSlope;
            public double IrtIntercept;
            public double[][] MassErrorStats;
            public int[] FinalTargetCounts;
            public double?[] ScoringModelCoefficients;
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

        private Image _searchLogImage;

        protected override Bitmap ProcessCoverShot(Bitmap bmp)
        {
            var graph = Graphics.FromImage(base.ProcessCoverShot(bmp));
            graph.DrawImageUnscaled(_searchLogImage, bmp.Width - _searchLogImage.Width - 10, bmp.Height - _searchLogImage.Height - 30);
            return bmp;
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiaTtofDiaUmpireTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;
            ReadExpectedValues("TestDiaTtofDiaUmpireTutorial");
            //IsPauseForScreenShots = true;
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                ChromatogramClickPoint = new PointF(23.02F, 150.0F),
            };

            TestTtofData();
        }

        [TestMethod, 
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE), 
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME)]
        public void TestDiaTtofDiaUmpireTutorialFullFileset()
        {
            ReadExpectedValues("TestDiaTtofDiaUmpireTutorialFullFileset");
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(23.02F, 150.0F),
            };

            if (!IsCoverShotMode)
                TestTtofData();
        }

        private void TestTtofData()
        {
            SetInstrumentType(new InstrumentSpecificValues
            {
                InstrumentTypeName = "TTOF",
                DiaFiles = new[]
                {
                    "collinsb_I180316_001_SW-A.mzML",
                    "collinsb_I180316_002_SW-B.mzML",
                    "collinsb_I180316_003_SW-A.mzML",
                    "collinsb_I180316_004_SW-B.mzML",
                    "collinsb_I180316_005_SW-A.mzML",
                    "collinsb_I180316_006_SW-B.mzML",
                },
                HasAmbiguousMatches = false,
                IsolationSchemeName = "ETH TTOF (64 variable)",
                IsolationSchemeFile = "64_variable_windows.csv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_CSV,
                PrecursorTolerance = new MzTolerance(30, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(40, MzTolerance.Units.ppm),
                InstrumentPreset = DiaUmpire.Config.InstrumentPreset.TripleTOF
            });

            RunTest();
        }

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestDiaQeDiaUmpireTutorialExtra()
        {
            ReadExpectedValues("TestDiaQeDiaUmpireTutorialExtra");
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IrtFilterText = "standard",
                ChromatogramClickPoint = new PointF(18.13f, 5.51e5f),
            };

            if (!IsCoverShotMode)
                TestQeData();
        }

        [TestMethod, 
         NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE), 
         NoNightlyTesting(TestExclusionReason.EXCESSIVE_TIME)] // do not run full filesets for nightly tests
        public void TestDiaQeDiaUmpireTutorialFullFileset()
        {
            ReadExpectedValues("TestDiaQeDiaUmpireTutorialFullFileset");
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(18.13f, 5.51e5f),
            };

            if (!IsCoverShotMode)
                TestQeData();
        }
       
        private void TestQeData()
        {
            SetInstrumentType(new InstrumentSpecificValues
            {
                InstrumentTypeName = "QE",
                DiaFiles = new[]
                {
                    "collinsb_X1803_171-A.mzML",
                    "collinsb_X1803_172-B.mzML",
                    "collinsb_X1803_173-A.mzML",
                    "collinsb_X1803_174-B.mzML",
                    "collinsb_X1803_175-A.mzML",
                    "collinsb_X1803_176-B.mzML",
                },
                HasAmbiguousMatches = false,
                IsolationSchemeName = "ETH QE (18 variable)",
                IsolationSchemeFile = "QE_DIA_18var.tsv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
                PrecursorTolerance = new MzTolerance(10, MzTolerance.Units.ppm),
                FragmentTolerance = new MzTolerance(20, MzTolerance.Units.ppm),
                InstrumentPreset = DiaUmpire.Config.InstrumentPreset.QExactive
            });

            RunTest();
        }

        private void ReadExpectedValues(string name)
        {
            _expectedValuesFilePath = Path.Combine(ExtensionTestContext.GetProjectDirectory(
                @"TestPerf\DiaUmpireTutorialTest.data"), name + ".json");
            if (File.Exists(_expectedValuesFilePath))
            {
                using var streamReader = File.OpenText(_expectedValuesFilePath);
                using var jsonReader = new JsonTextReader(streamReader);
                _expectedValues = JsonSerializer.Create().Deserialize<ExpectedValues>(jsonReader);
            }
            else
            {
                Assert.IsTrue(IsRecordMode, "Expected values file {0} does not exist", _expectedValuesFilePath);
                _expectedValues = new ExpectedValues();
            }
        }

        // disable audit log comparison for FullFileset tests
        public override bool AuditLogCompareLogs => !TestContext.TestName.EndsWith("FullFileset");

        private void SetInstrumentType(InstrumentSpecificValues instrumentValues)
        {
            _instrumentValues = instrumentValues;

            RootName = "DIA-" + InstrumentTypeName;
            // LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/{0}-20_1.pdf";
            LinkPdf = string.Format("file:///C:/proj/branches/work/pwiz_tools/Skyline/Documentation/Tutorials/{0}-20_1.pdf", RootName);

            TestFilesZipPaths = new[]
            {
                string.Format(@"http://skyline.ms/tutorials/{0}.zip", RootName),
                string.Format(@"TestPerf\DiaSwath{0}Views.zip", InstrumentTypeName)
            };

            TestFilesPersistent = new[] { Path.Combine(RootName, "DDA_search"), Path.Combine(RootName, "DIA") + '\\' };
        }

        private void RunTest()
        {
//            IsPauseForScreenShots = true;
//            RunPerfTests = true;
//            IsPauseForCoverShot = true;
            CoverShotName = "DIA-Umpire-" + InstrumentTypeName;

            RunFunctionalTest();
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        private PropertyPath _resultProperty = PropertyPath.Root.Property("FoldChangeResult");
        private PropertyPath _proteinProperty = PropertyPath.Root.Property("Protein");
        private const string OXIDATION_M = "Oxidation (M)";

        private string GetTestPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(Path.Combine(RootName, path));
        }

        /// <summary>
        /// Change to true to write coefficient arrays.
        /// </summary>
        protected override bool IsRecordMode => false;

        protected override void DoTest()
        {
            Assert.IsNotNull(_expectedValues);

            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                d => d.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            SrmDocument doc = SkylineWindow.Document;

            string documentBaseName = "DIA-" + InstrumentTypeName + "-tutorial";
            string documentFile = GetTestPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowRunPeptideSearchDlg);

            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library empty page");

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            string diaDir = GetTestPath("DIA\\");

            // when in regular test mode, delete -diaumpire files so they get regenerated instead of reused
            // (in IsRecordMode, keep these files around so that repeated tests on each language run faster)
            /* TODO: how can this code work if we aren't running DiaUmpire in the persistent directory? */
            if (!IsRecordMode)
            {
                var diaumpireFiles = Directory.GetFiles(diaDir, "*-diaumpire.*");
                var filesToRegenerate = diaumpireFiles.Skip(1); // regenerate all but 1 file in order to test file reusability
                foreach (var file in filesToRegenerate)
                    FileEx.SafeDelete(file);
            }

            string[] searchFiles = DiaFiles.Select(p => Path.Combine(diaDir, p)).Take(_analysisValues.IsWholeProteome ? DiaFiles.Length : 2).ToArray();
            foreach (var searchFile in searchFiles)
                Assert.IsTrue(File.Exists(searchFile), string.Format("File {0} does not exist.", searchFile));

            string fastaPathForImport = GetTestPath(_analysisValues.FastaPath);
            Assert.IsTrue(File.Exists(fastaPathForImport));

            string fastaPathForSearch = GetTestPath(_analysisValues.FastaPathForSearch);
            Assert.IsTrue(File.Exists(fastaPathForSearch));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = searchFiles.Select(f => new MsDataFilePath(f)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.BIOGNOSYS_11;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                // Check default settings shown in the tutorial
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library populated page");

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
            });

            // TODO: Put this back with tutorial text that explains it
            // PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("Import Peptide Search - Extract chromatograms page");

            // With 2 sources, we get the remove prefix/suffix dialog; accept default behavior
            if (searchFiles.Length > 1)
            {
                var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton()); // now on remove prefix/suffix dialog
                PauseForScreenShot<ImportResultsNameDlg>("Import Peptide Search - Remove shared prefix/suffix page");
                OkDialog(removeSuffix, () => removeSuffix.YesDialog()); // now on modifications
                WaitForDocumentLoaded();
            }
            else
                RunUI(() => importPeptideSearchDlg.ClickNextButton());

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page));

            var editStructModListUI =
                ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(importPeptideSearchDlg.MatchModificationsControl.ClickAddStructuralModification);
            RunDlg<EditStaticModDlg>(editStructModListUI.AddItem, editModDlg =>
            {
                editModDlg.SetModification(OXIDATION_M); // Not L10N
                editModDlg.OkDialog();
            });
            OkDialog(editStructModListUI, editStructModListUI.OkDialog);

            RunUI(() => importPeptideSearchDlg.MatchModificationsControl.ChangeAll(true));

            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - After adding modifications page");
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
                if (_analysisValues.KeepPrecursors)
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
                Assert.AreEqual(0.05, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Value);
                Assert.AreEqual(MzTolerance.Units.mz, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Unit);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            // We're on the "Configure Full-Scan Settings" page of the wizard.
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                if (_analysisValues.KeepPrecursors)
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
                importRangesDlg.CurrentDirectory = new MsDataFilePath(diaDir);
                importRangesDlg.SelectFile(DiaFiles[0]);
                importRangesDlg.Open();
            });
            string schemePath = Path.Combine("DIA", _instrumentValues.IsolationSchemeFile);
            var schemeLines = File.ReadAllLines(GetTestPath(schemePath));
            string[][] windowFields = schemeLines.Select(l =>
                TextUtil.ParseDsvFields(l, _instrumentValues.IsolationSchemeFileSeparator)).ToArray();
            WaitForConditionUI(() => isolationScheme.GetIsolationWindows().Count == schemeLines.Length);

            RunUI(() =>
            {
                Assert.IsTrue(isolationScheme.SpecifyMargin);
                int schemeRow = 0;
                foreach (var isolationWindow in isolationScheme.GetIsolationWindows())
                {
                    var fields = windowFields[schemeRow++];
                    Assert.AreEqual(double.Parse(fields[0], CultureInfo.InvariantCulture), isolationWindow.MethodStart, 0.01);
                    Assert.AreEqual(double.Parse(fields[1], CultureInfo.InvariantCulture), isolationWindow.MethodEnd, 0.01);
                    Assert.AreEqual(double.Parse(fields[2], CultureInfo.InvariantCulture), isolationWindow.StartMargin ?? 0, 0.01);
                }
            });
            PauseForScreenShot<EditIsolationSchemeDlg>("Isolation scheme");

            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Isolation scheme graph");

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Import Peptide Search - Configure Full-Scan Settings page");

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page before settings");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPathForSearch);
                importPeptideSearchDlg.ImportFastaControl.ScrollFastaTextToEnd();   // So that the FASTA file name is visible
                if (!_analysisValues.IsWholeProteome)
                {
                    importPeptideSearchDlg.ImportFastaControl.FastaImportTargetsFile = fastaPathForImport;
                    importPeptideSearchDlg.ImportFastaControl.ScrollFastaTargetsToEnd();
                }
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = true;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page after settings");

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.ConverterSettingsControl.UseDiaUmpire = true;
                importPeptideSearchDlg.ConverterSettingsControl.InstrumentPreset = _instrumentValues.InstrumentPreset;
                importPeptideSearchDlg.ConverterSettingsControl.EstimateBackground = true;
                //importPeptideSearchDlg.ConverterSettingsControl.AdditionalSettings = _instrumentValues.AdditionalSettings;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.ConverterSettingsPage>("Import Peptide Search - DiaUmpire settings page");

            bool? searchSucceeded = null;
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                Assert.IsTrue(importPeptideSearchDlg.CurrentPage ==
                              ImportPeptideSearchDlg.Pages.dda_search_settings_page);
                importPeptideSearchDlg.SearchSettingsControl.PrecursorTolerance = _instrumentValues.PrecursorTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentTolerance = _instrumentValues.FragmentTolerance;
                importPeptideSearchDlg.SearchSettingsControl.FragmentIons = "b, y";
                importPeptideSearchDlg.SearchSettingsControl.CutoffScore = 0.05;
                Assert.AreEqual(PropertyNames.CutoffScore_PERCOLATOR_QVALUE, importPeptideSearchDlg.SearchSettingsControl.CutoffLabel);
                Assert.AreEqual(0.05, importPeptideSearchDlg.SearchSettingsControl.CutoffScore);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.DDASearchSettingsPage>("Import Peptide Search - DDA search settings");

            IDictionary<string, object> diaUmpireParameters = null;
            SearchSettingsControl.DdaSearchSettings searchSettings = null;
            if (IsRecordMode)
            {
                RunUI(() =>
                {
                    diaUmpireParameters = importPeptideSearchDlg.ConverterSettingsControl.GetDiaUmpireConverter().DiaUmpireConfig.Parameters;
                    searchSettings = importPeptideSearchDlg.SearchSettingsControl.SearchSettings;
                });
            }

            if (IsCoverShotMode)
            {
                // Resize the form before running or the output will not appear scrolled to the end
                RunUI(() => importPeptideSearchDlg.Size = new Size(404, 578));  // minimum height
            }

            RunUI(() =>
            {
                // Run the search
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.SearchControl.SearchFinished += (success) => searchSucceeded = success;
                importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches = true;
            });

            try
            {
                WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.dda_search_page);
                WaitForConditionUI(WAIT_TIME * 4, () => importPeptideSearchDlg.SearchControl.PercentComplete > 17);
                PauseForScreenShot<ImportPeptideSearchDlg.DDASearchPage>("Import Peptide Search - DDA search progress page");

                WaitForConditionUI(120 * 600000, () => searchSucceeded.HasValue, () => importPeptideSearchDlg.SearchControl.LogText);
                RunUI(() => Assert.IsTrue(searchSucceeded.Value, importPeptideSearchDlg.SearchControl.LogText));
            }
            finally
            {
                File.WriteAllText("SearchControlLog.txt", importPeptideSearchDlg.SearchControl.LogText);
            }

            if (IsCoverShotMode)
            {
                ScreenshotManager.ActivateScreenshotForm(importPeptideSearchDlg);
                _searchLogImage = ScreenshotManager.TakeShot(importPeptideSearchDlg);
                Assert.IsNotNull(_searchLogImage);
            }

            var addIrtDlg = ShowDialog<AddIrtPeptidesDlg>(() => importPeptideSearchDlg.ClickNextButton(), 30 * 60000);//peptidesPerProteinDlg.OkDialog());
            RunUI(() =>
            {
                // Check values shown in the tutorial
                Assert.AreEqual(1, addIrtDlg.RunsConvertedCount);
                var row = addIrtDlg.GetRow(0);
                Assert.AreEqual(11, row.Cells[1].Value);

                var regressionLine = addIrtDlg.GetRegressionRefined(0);
                Assert.IsNotNull(regressionLine);
                var actualSlope = Math.Round(regressionLine.Slope, 3);
                var actualIntercept = Math.Round(regressionLine.Intercept, 3);
                if (!IsRecordMode)
                {
                    Assert.AreEqual(_expectedValues.LibraryPeptideCount, addIrtDlg.PeptidesCount);
                    Assert.AreEqual(_expectedValues.IrtSlope, actualSlope);
                    Assert.AreEqual(_expectedValues.IrtIntercept, actualIntercept);
                }
                else
                {
                    _expectedValues.LibraryPeptideCount = addIrtDlg.PeptidesCount;
                    _expectedValues.IrtSlope = actualSlope;
                    _expectedValues.IrtIntercept = actualIntercept;
                }

                Assert.AreEqual(1.0, double.Parse(row.Cells[3].Value.ToString()));
                Assert.AreEqual(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Success, row.Cells[4].Value);
            });
            PauseForScreenShot<AddIrtPeptidesDlg>("Add iRT peptides form");

            var irtGraph = ShowDialog<GraphRegression>(() => addIrtDlg.ShowRegression(0));
            PauseForScreenShot<GraphRegression>("iRT regression graph");

            OkDialog(irtGraph, irtGraph.CloseDialog);
            var recalibrateMessage = ShowDialog<MultiButtonMsgDlg>(addIrtDlg.OkDialog);
            RunUI(() => Assert.AreEqual(TextUtil.LineSeparate(Resources.LibraryGridViewDriver_AddToLibrary_Do_you_want_to_recalibrate_the_iRT_standard_values_relative_to_the_peptides_being_added_,
                Resources.LibraryGridViewDriver_AddToLibrary_This_can_improve_retention_time_alignment_under_stable_chromatographic_conditions_), recalibrateMessage.Message));

            if (!_instrumentValues.HasAmbiguousMatches)
            {
                OkDialog(recalibrateMessage, recalibrateMessage.ClickNo);
            }
            else
            {
                var ambiguousDlg = ShowDialog<MessageDlg>(recalibrateMessage.ClickNo);
                RunUI(() => AssertEx.Contains(ambiguousDlg.Message,
                    Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));
                OkDialog(ambiguousDlg, ambiguousDlg.OkDialog);
            }
            OkDialog(addIrtDlg, addIrtDlg.OkDialog);

            var peptidesPerProteinDlg = WaitForOpenForm<AssociateProteinsDlg>(600000);
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                //int proteinCount, peptideCount, precursorCount, transitionCount;
                //peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                //ValidateTargets(ref _analysisValues.TargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"TargetCounts");
                if (_analysisValues.RemoveDuplicates)
                    peptidesPerProteinDlg.RemoveDuplicatePeptides = true;
                if (_analysisValues.MinPeptidesPerProtein.HasValue)
                    peptidesPerProteinDlg.MinPeptides = _analysisValues.MinPeptidesPerProtein.Value;
            });
            WaitForConditionUI(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateTargets(ref _expectedValues.FinalTargetCounts, proteinCount, peptideCount, precursorCount, transitionCount);
            });
            PauseForScreenShot<AssociateProteinsDlg>("Import FASTA summary form");
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);

            if (!PauseForAllChromatogramsGraphScreenShot("Loading chromatograms window", 40, @"00:00:22", 39f, 1e4f,
                    new Dictionary<string, int>
                    {
                        { "collinsb_I180316_001", 40 },
                        { "collinsb_I180316_002", 41 }
                    }))
            {
                CleanUpPersistentDir(diaDir);
                return;
            }
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes

            var peakScoringModelDlg = WaitForOpenForm<EditPeakScoringModelDlg>();
            ValidateCoefficients(peakScoringModelDlg, ref _expectedValues.ScoringModelCoefficients);
            PauseForScreenShot<EditPeakScoringModelDlg>("mProphet model form");

            OkDialog(peakScoringModelDlg, peakScoringModelDlg.OkDialog);

            RunUI(() => SkylineWindow.ShowDocumentGrid(false));

            // Arrange windows for manual inspection
            var arrangeGraphsDlg = ShowDialog<ArrangeGraphsGroupedDlg>(SkylineWindow.ArrangeGraphsGrouped);
            RunUI(() =>
            {
                arrangeGraphsDlg.Groups = 2;
                arrangeGraphsDlg.GroupType = GroupGraphsType.distributed;
                arrangeGraphsDlg.GroupOrder = GroupGraphsOrder.Document;
                arrangeGraphsDlg.DisplayType = DisplayGraphsType.Row;
            });

            OkDialog(arrangeGraphsDlg, arrangeGraphsDlg.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument());

            if (IsPauseForScreenShots)
            {
                RestoreViewOnScreenNoSelChange(17);
                SelectNode(SrmDocument.Level.MoleculeGroups, 0);
                Rectangle rectFrame = Rectangle.Empty;
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(900, 900);
                    SkylineWindow.ForceOnScreen();  // Avoid this shifting the window under the floating window later
                });
                Thread.Sleep(200);  // Give layout time to adjust
                RunUI(() =>
                {
                    var chromPane1 = SkylineWindow.GetGraphChrom(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0].Name);
                    var chromPane2 = SkylineWindow.GetGraphChrom(SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[1].Name);
                    var rtGraphFrame = FindFloatingWindow(SkylineWindow.GraphRetentionTime);
                    var chromTopLeft = chromPane1.PointToScreen(new Point(0, 0));
                    var chromTopRight = chromPane2.PointToScreen(new Point(chromPane2.Width, 0));
                    rtGraphFrame.Location = chromPane1.PointToScreen(new Point(((chromTopRight.X - chromTopLeft.X) - rtGraphFrame.Width) / 2, 50));
                    rectFrame = rtGraphFrame.Bounds;
                    rtGraphFrame.Activate();    // TODO: Want the graph activated but a screenshot of screen
                });
                PauseForScreenShot<ScreenForm>("Docking floating image with cursor", null, bmp =>
                    DrawLArrowCursorOnBitmap(ClipDockingRect(bmp, rectFrame), 0.5, 0.155));
                BeginDragDisplay(SkylineWindow.GraphRetentionTime, 0.62, 0.11);
                PauseForScreenShot<ScreenForm>("Docking image cursor on upper dock indicator", null, bmp =>
                    ClipDockingRect(bmp, rectFrame));
                EndDragDisplay();
            }

            const string proteinNameToSelect = "sp|P63284|CLPB_ECOLI";
            if (Equals(proteinNameToSelect, SkylineWindow.Document.MoleculeGroups.Skip(1).First().Name))
                SelectNode(SrmDocument.Level.MoleculeGroups, 1);
            else
                FindNode(proteinNameToSelect);

            RunUI(() =>
            {
                Assert.AreEqual(proteinNameToSelect, SkylineWindow.SelectedNode.Text);

                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.Size = new Size(1226, 900);
            });
            RestoreViewOnScreenNoSelChange(18);
            WaitForGraphs();
            RunUIForScreenShot(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.SelectedNode);
            PauseForScreenShot("Manual review window layout with protein selected");

            FindNode("TDINQALNR");
            WaitForGraphs();
            PauseForScreenShot("Manual review window layout with peptide selected");

            FindNode("_HUMAN");
            WaitForGraphs();
            FindNode("TDINQALNR");
            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();
            PauseForChromGraphScreenShot("Snip just one chromatogram pane", "1_SW-A");

            ClickChromatogram(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name,
                _analysisValues.ChromatogramClickPoint.X,
                _analysisValues.ChromatogramClickPoint.Y);
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - zoomed");
            
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            WaitForGraphs();
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - unzoomed");

            RunUI(SkylineWindow.GraphFullScan.Close);
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph);
            WaitForGraphs();
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out MassErrorHistogramGraphPane massErrorPane));
            int massErrorStatsIndex = 0;
            ValidateMassErrors(massErrorPane, massErrorStatsIndex++);

            // CONSIDER: No way to specify mass error graph window in PauseForScreenShot or ShowDialog
            PauseForMassErrorGraphScreenShot("Mass errors histogram graph window");

            // Review single replicates
            RunUI(SkylineWindow.ShowSingleReplicate);
            foreach (var chromatogramSet in SkylineWindow.Document.MeasuredResults.Chromatograms)
            {
                RunUI(() => SkylineWindow.ActivateReplicate(chromatogramSet.Name));
                WaitForGraphs();
                ValidateMassErrors(massErrorPane, massErrorStatsIndex++);
            }

            if (IsRecordMode)
            {
                PrintAnalysisSettingsAndResultSummary(diaUmpireParameters, searchSettings, _analysisValues);
            }

            RunUI(() =>
            {
                SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.decoys);
                SkylineWindow.ShowAverageReplicates();
            });
            WaitForGraphs();
            RunUI(() => SkylineWindow.ShowPointsTypeMassError(PointsTypeMassError.targets));    // CONSIDER: 1% FDR
            RunUI(() => SkylineWindow.ShowGraphMassError(false));

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForGraphs();
            RestoreViewOnScreenNoSelChange(24);
            PauseForRetentionTimeGraphScreenShot("Retention time regression graph window - regression");

            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForGraphs();
            PauseForRetentionTimeGraphScreenShot("Retention time regression graph window - residuals");
            RunUI(() => SkylineWindow.ShowGraphRetentionTime(false, GraphTypeSummary.score_to_run_regression));

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });

                RestoreCoverViewOnScreen();
                
                // No fold-change with only 2 runs
                // CONSIDER: Could remove the fold-change floating window from the cover.view
                var fcGrid = WaitForOpenForm<FoldChangeGrid>();
                RunUI(FindFloatingWindow(fcGrid).Close);

                /* fcGridControlFinal = fcGrid.DataboundGridControl;
                FilterIrtProtein(fcGridControlFinal);
                changeGroupComparisonSettings = ShowDialog<EditGroupComparisonDlg>(fcGrid.ShowChangeSettings);
                RunUI(() => changeGroupComparisonSettings.RadioScopePerPeptide.Checked = true);
                OkDialog(changeGroupComparisonSettings, changeGroupComparisonSettings.Close);

                RunUI(() =>
                {
                    var fcFloatingWindow = fcGrid.Parent.Parent;
                    fcFloatingWindow.Left = SkylineWindow.Left + 8;
                    fcFloatingWindow.Top = SkylineWindow.Bottom - fcFloatingWindow.Height - 8;
                });*/
                TakeCoverShot();
            }

            // Cleanup output files in persistent dir
            // (in IsRecordMode, keep these files around so that repeated tests on each language run faster)
            if (!IsRecordMode)
            {
                CleanUpPersistentDir(diaDir);
            }

            if (IsRecordMode)
            {
                Assert.IsNotNull(_expectedValues);
                Assert.IsNotNull(_expectedValuesFilePath);
                using var streamWriter = new StreamWriter(_expectedValuesFilePath);
                using var jsonTextWriter = new JsonTextWriter(streamWriter);
                JsonSerializer.Create(new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                }).Serialize(jsonTextWriter, _expectedValues);
            }
        }

        private static void CleanUpPersistentDir(string diaDir)
        {
            foreach (var file in Directory.GetFiles(diaDir, "*-diaumpire.*"))
                FileEx.SafeDelete(file);
        }

        private static Bitmap ClipDockingRect(Bitmap bmp, Rectangle rectFrame)
        {
            const int hBorder = 20;
            const int topBorder = 100;
            const int bottomBorder = 10;
            var rectClip = new Rectangle(rectFrame.X - hBorder, rectFrame.Y - topBorder, rectFrame.Width + hBorder * 2,
                rectFrame.Height + topBorder + bottomBorder);
            bmp = ClipBitmap(bmp, rectClip);
            return bmp;
        }

        private void PrintAnalysisSettingsAndResultSummary(IDictionary<string, object> diaUmpireParameters, SearchSettingsControl.DdaSearchSettings searchSettings, AnalysisValues analysisValues)
        {
            var interestingParameters = new List<string>();
            foreach (var key in "MS1PPM MS2PPM SN MS2SN DeltaApex CorrThreshold BoostComplementaryIon EstimateBG".Split())
                interestingParameters.Add(diaUmpireParameters[key].ToString());
            interestingParameters.Add(searchSettings.PrecursorTolerance.Value.ToString(CultureInfo.InvariantCulture));
            interestingParameters.Add(searchSettings.FragmentTolerance.Value.ToString(CultureInfo.InvariantCulture));
            interestingParameters.Add(_expectedValues.LibraryPeptideCount.ToString());
            for (int i = 0; i < 4; ++i)
                interestingParameters.Add(_expectedValues.FinalTargetCounts[i].ToString());
            Console.WriteLine(string.Join("\t", interestingParameters));
        }

        private void FilterIrtProtein(DataboundGridControl fcGridControl)
        {
            WaitForConditionUI(() => fcGridControl.IsComplete && fcGridControl.FindColumn(_proteinProperty) != null);
            var quickFilterForm = ShowDialog<QuickFilterForm>(() =>
            {
                var proteinNameColumn = fcGridControl.FindColumn(_proteinProperty);
                fcGridControl.QuickFilter(proteinNameColumn);
            });
            RunUI(() =>
            {                
                quickFilterForm.SetFilterOperation(0, FilterOperations.OP_NOT_CONTAINS);
                quickFilterForm.SetFilterOperand(0, _analysisValues.IrtFilterText);
            });
            OkDialog(quickFilterForm, quickFilterForm.OkDialog);
        }

        private void RestoreViewOnScreenNoSelChange(int pageName)
        {
            if (!Program.SkylineOffscreen)
            {
                RunUI(() =>
                {
                    var selectedPath = SkylineWindow.SelectedPath;
                    RestoreViewOnScreen(pageName);
                    SkylineWindow.SelectedPath = selectedPath;
                });
            }
        }

        private void ValidateTargets(ref int[] targetCounts, int proteinCount, int peptideCount, int precursorCount, int transitionCount)
        {
            var targetCountsActual = new[] { proteinCount, peptideCount, precursorCount, transitionCount };
            if (IsRecordMode)
            {
                targetCounts = targetCountsActual;
                return;
            }

            if (!ArrayUtil.EqualsDeep(targetCounts, targetCountsActual))
            {
                Assert.Fail("Expected target counts <{0}> do not match actual <{1}>.",
                    string.Join(", ", targetCounts),
                    string.Join(", ", targetCountsActual));
            }
        }

        private void ValidateCoefficients(EditPeakScoringModelDlg editDlgFromSrm, ref double?[] expectedCoefficients)
        {
            var actualCoefficients = editDlgFromSrm.PeakCalculatorsGrid.Items
                .Select(item => item.Weight.HasValue ? Math.Round(item.Weight.Value, 4) : (double?)null).ToArray();
            if (IsRecordMode)
                expectedCoefficients = actualCoefficients;
            else
                AssertEx.AreEqual(string.Join("|", expectedCoefficients), string.Join("|", actualCoefficients));
        }

        private void ValidateMassErrors(MassErrorHistogramGraphPane massErrorPane, int index)
        {
            double mean = massErrorPane.Mean, stdDev = massErrorPane.StdDev;
            if (IsRecordMode)
            {
                _expectedValues.MassErrorStats ??= Array.Empty<double[]>();
                while (_expectedValues.MassErrorStats.Length <= index)
                {
                    _expectedValues.MassErrorStats =
                        _expectedValues.MassErrorStats.Append(Array.Empty<double>()).ToArray();
                }

                _expectedValues.MassErrorStats[index] = new[] { mean, stdDev };
            }
            else
            {
                Assert.AreEqual(_expectedValues.MassErrorStats[index][0], mean, 0.05);
                Assert.AreEqual(_expectedValues.MassErrorStats[index][1], stdDev, 0.05);
            }
        }

        private void WaitForBarGraphPoints(FoldChangeBarGraph barGraph, int barCount, int? lowerBoundCount = null)
        {
            WaitForConditionUI(() => barGraph.ZedGraphControl.GraphPane.CurveList.Count == 1);
            if (!lowerBoundCount.HasValue)
            {
                WaitForConditionUI(() => barCount == GetBarCount(barGraph),
                    () => string.Format("Expecting {0} bars, actual {1} bars", barCount, GetBarCount(barGraph)));
            }
            else
            {
                WaitForConditionUI(() => lowerBoundCount.Value < GetBarCount(barGraph) && GetBarCount(barGraph) < barCount,
                    () => string.Format("Expecting < {0} bars, actual {1} bars", barCount, GetBarCount(barGraph)));
            }
        }

        private int GetBarCount(FoldChangeBarGraph barGraph)
        {
            return barGraph.ZedGraphControl.GraphPane.CurveList[0].Points.Count;
        }

        private static void SortByFoldChange(DataboundGridControl fcGridControl, PropertyPath fcResultProperty)
        {
            RunUI(() =>
            {
                var fcResultColumn = fcGridControl.FindColumn(fcResultProperty);
                fcGridControl.SetSortDirection(fcGridControl.GetPropertyDescriptor(fcResultColumn),
                    ListSortDirection.Ascending);
            });
        }
    }
}
