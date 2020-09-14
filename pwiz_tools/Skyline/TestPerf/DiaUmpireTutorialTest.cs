/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Results;
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
            public Dictionary<string, AbstractDdaSearchEngine.Setting> AdditionalSettings;
        }

        private class AnalysisValues
        {
            public bool IsWholeProteome;
            public bool KeepPrecursors;

            public int LibraryPeptideCount;
            public double IrtSlope;
            public double IrtIntercept;

            public string IrtFilterText;
            public int? MinPeptidesPerProtein;
            public bool RemoveDuplicates;
            public int[] TargetCounts;
            public int[] FinalTargetCounts;
            public string ScoringModelCoefficients;
            public PointF ChromatogramClickPoint;
            public double[][] MassErrorStats;

            public string FastaPathForSearch => "DDA_search\\nodecoys_3mixed_human_yeast_ecoli_20140403_iRT.fasta";
            public string FastaPath =>
                IsWholeProteome
                    ? "DDA_search\\nodecoys_3mixed_human_yeast_ecoli_20140403_iRT.fasta"
                    : "DIA\\target_protein_sequences.fasta";

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
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaTtofDiaUmpireTutorial()
        {
            //IsPauseForScreenShots = true;
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                ChromatogramClickPoint = new PointF(23.02F, 150.0F),
                LibraryPeptideCount = 18595,
                IrtSlope = 3.007,
                IrtIntercept = -67.325,

                TargetCounts = new[] { 13, 198, 262, 1572 },
                FinalTargetCounts = new[] { 9, 198, 262, 1572 },
                ScoringModelCoefficients = "0.0872|-0.4325|4.6642|-0.0719|-0.5895|0.9069|0.1453|-0.1057",
                MassErrorStats = new[]
                {
                    new[] {3.2, 3.8},
                    new[] {3.1, 3.1},
                    new[] {3.2, 4.4},
                },
            };

            TestTtofData();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaTtofDiaUmpireFullTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(23.02F, 150.0F),
                LibraryPeptideCount = 28802,
                IrtSlope = 3.005,
                IrtIntercept = -67.180,
                TargetCounts = new[] { 6860, 42396, 46842, 281052 },
                FinalTargetCounts = new[] { 2639, 28342, 31707, 190242 },
                ScoringModelCoefficients = "0.2110|-0.6707|3.7894|0.1967|-0.1875|0.6020|0.0608|-0.0468",
                MassErrorStats = new[]
                {
                    new[] {2.6, 5},
                    new[] {2.5, 4.6},
                    new[] {3.4, 4.9},
                    new[] {4.8, 4.6},
                    new[] {3.9, 4.9},
                    new[] {-0.2, 4.4},
                    new[] {1, 4.7},
                },
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
                InstrumentPreset = DiaUmpire.Config.InstrumentPreset.TripleTOF,
                AdditionalSettings = new Dictionary<string, AbstractDdaSearchEngine.Setting>
                {
                    {"MS1PPM", new AbstractDdaSearchEngine.Setting("MS1PPM", 10.0, 0, 1000)},
                    {"MS2PPM", new AbstractDdaSearchEngine.Setting("MS2PPM", 20.0, 0, 1000)},
                    {"SNThreshold", new AbstractDdaSearchEngine.Setting("SNThreshold", 1.0, 0, 1000)},
                    {"MS2SNThreshold", new AbstractDdaSearchEngine.Setting("MS2SNThreshold", 1.0, 0, 1000)},
                    //{"NoMissedScan", new AbstractDdaSearchEngine.Setting("NoMissedScan", 2, 0, 10)},
                    {"MaxCurveRTRange", new AbstractDdaSearchEngine.Setting("MaxCurveRTRange", 4.0, 0, 10)},
                    {"RTOverlapThreshold", new AbstractDdaSearchEngine.Setting("RTOverlapThreshold", 0.05, 0, 10)},
                    {"CorrThreshold", new AbstractDdaSearchEngine.Setting("CorrThreshold", 0.1, 0, 10)},
                }
            });

            RunTest();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaQeDiaUmpireTutorial()
        {
            RunPerfTests = true;
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IrtFilterText = "standard",
                ChromatogramClickPoint = new PointF(18.19f, 1.8e6f),
                LibraryPeptideCount = 10207,
                IrtSlope = 2.607,
                IrtIntercept = -46.029,
                TargetCounts = new[] { 13, 192, 226, 1356 },
                FinalTargetCounts = new[] { 9, 192, 226, 1356 },
                ScoringModelCoefficients = "0.4571|-0.6871|4.1075|1.5161|-0.1427|0.6322|0.0604|-0.0753",
                MassErrorStats = new[]
                {
                    new[] {1.7, 3.1},
                    new[] {1.3, 2.8},
                    new[] {2, 3.4},
                },
            };

            if (!IsCoverShotMode)
                TestQeData();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaQeDiaUmpireFullTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(18.19f, 1.8e6f),
                LibraryPeptideCount = 15118,
                IrtSlope = 2.600,
                IrtIntercept = -45.702,
                TargetCounts = new[] { 5472, 30448, 32751, 196506 },
                FinalTargetCounts = new[] { 1863, 18742, 20406, 122436 },
                ScoringModelCoefficients = "0.1768|-0.8335|3.5691|1.0667|-0.0698|0.7307|0.1776|-0.0533",
                MassErrorStats = new[]
                {
                    new[] {1.8, 3.9},
                    new[] {1.3, 3.8},
                    new[] {1.8, 4.1},
                    new[] {2, 3.8},
                    new[] {1.9, 4.1},
                    new[] {2, 3.8},
                    new[] {1.7, 4.1},
                },

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

            TestFilesPersistent = new[] { Path.Combine(RootName, "DDA_search"), Path.Combine(RootName, "DIA") };
        }

        private void RunTest()
        {
//            IsPauseForScreenShots = true;
//            RunPerfTests = true;
//            IsPauseForCoverShot = true;
            CoverShotName = "DIA-SWATH with DiaUmpire";

            RunFunctionalTest();

            Assert.IsFalse(IsRecordMode);   // Make sure this doesn't get committed as true
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
        private bool IsRecordMode { get { return IsRecordAuditLogForTutorials; } }

        private string ParseIrtProperties(string irtFormula, CultureInfo cultureInfo = null)
        {
            var decimalSeparator = (cultureInfo ?? CultureInfo.CurrentCulture).NumberFormat.NumberDecimalSeparator;
            var match = Regex.Match(irtFormula, $@"iRT = (?<slope>\d+{decimalSeparator}\d+) \* [^+-]+? (?<sign>[+-]) (?<intercept>\d+{decimalSeparator}\d+)");
            Assert.IsTrue(match.Success);
            string slope = match.Groups["slope"].Value, intercept = match.Groups["intercept"].Value, sign = match.Groups["sign"].Value;
            if (sign == "+") sign = string.Empty;
            return $"IrtSlope = {slope},\r\nIrtIntercept = {sign}{intercept},\r\n";
        }

        protected override void DoTest()
        {
            Assert.AreEqual("IrtSlope = 3.005,\r\nIrtIntercept = -67.173,\r\n", ParseIrtProperties("iRT = 3.005 * Measured RT - 67.173", CultureInfo.InvariantCulture));
            if (IsRecordMode)
                Console.WriteLine();

            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                d => d.ChangeSettings(SrmSettingsList.GetDefault())));
            RunUI(() => SkylineWindow.SetIntegrateAll(true));

            SrmDocument doc = SkylineWindow.Document;

            string documentBaseName = "DIA-" + InstrumentTypeName + "-tutorial";
            string documentFile = TestContext.GetTestPath(documentBaseName + SrmDocument.EXT);
            RunUI(() => SkylineWindow.SaveDocument(documentFile));

            // Launch the wizard
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(SkylineWindow.ShowImportPeptideSearchDlg);

            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library empty page", 3);

            // We're on the "Build Spectral Library" page of the wizard.
            // Add the test xml file to the search files list and try to 
            // build the document library.
            string diaDir = GetTestPath("DIA");
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
                importPeptideSearchDlg.BuildPepSearchLibControl.PerformDDASearch = true;
                importPeptideSearchDlg.BuildPepSearchLibControl.DdaSearchDataSources = searchFiles.Select(f => new MsDataFilePath(f)).ToArray();
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.BIOGNOSYS_11;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                importPeptideSearchDlg.BuildPepSearchLibControl.InputFileType = ImportPeptideSearchDlg.InputFile.dia_raw;
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.95, importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library populated page", 4);

            //WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);

            var removeSuffix = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton()); // now on remove prefix/suffix dialog
            PauseForScreenShot<ImportResultsNameDlg>("Import Peptide Search - Remove shared prefix/suffix page", 5);
            OkDialog(removeSuffix, () => removeSuffix.YesDialog()); // now on modifications
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

            PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Import Peptide Search - After adding modifications page", 6);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.TransitionSettingsControl.MinIonMz = 50;
                importPeptideSearchDlg.TransitionSettingsControl.MaxIonMz = 1500;
                importPeptideSearchDlg.TransitionSettingsControl.IonRangeFrom = TransitionFilter.StartFragmentFinder.MZ_PRECURSOR.Label;
                importPeptideSearchDlg.TransitionSettingsControl.IonRangeTo = TransitionFilter.EndFragmentFinder.IONS_3.Label;
                importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = true;
                importPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = new[]
                {
                    Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED
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
                Assert.AreEqual(0.05, importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition settings", 7);
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
            PauseForScreenShot("Isolation scheme", 8);

            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);
            PauseForScreenShot("Isolation scheme graph", 9);

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Import Peptide Search - Configure Full-Scan Settings page", 10);

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page before settings", 11);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastaPathForSearch);
                if (!_analysisValues.IsWholeProteome)
                    importPeptideSearchDlg.ImportFastaControl.FastaImportTargetsFile = fastaPathForImport;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = true;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page after settings", 12);

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());

                importPeptideSearchDlg.ConverterSettingsControl.InstrumentPreset = _instrumentValues.InstrumentPreset;
                importPeptideSearchDlg.ConverterSettingsControl.AdditionalSettings = _instrumentValues.AdditionalSettings;
            });
            PauseForScreenShot("Import Peptide Search - DiaUmpire settings page", 14);

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

            PauseForScreenShot("Import Peptide Search - DDA search progress page", 14);
            WaitForConditionUI(3600000, () => searchSucceeded.HasValue);
            Assert.IsTrue(searchSucceeded.Value);

            var addIrtDlg = ShowDialog<AddIrtPeptidesDlg>(() => importPeptideSearchDlg.ClickNextButton());//peptidesPerProteinDlg.OkDialog());
            RunUI(() =>
            {
                // Check values shown in the tutorial
                Assert.AreEqual(1, addIrtDlg.RunsConvertedCount);
                var row = addIrtDlg.GetRow(0);
                Assert.AreEqual(11, row.Cells[1].Value);

                var regressionLine = new RegressionLine(_analysisValues.IrtSlope, _analysisValues.IrtIntercept);
                if (!IsRecordMode)
                {
                    Assert.AreEqual(_analysisValues.LibraryPeptideCount, addIrtDlg.PeptidesCount);
                    Assert.AreEqual(regressionLine.DisplayEquation, row.Cells[2].Value);
                }
                else
                {
                    Console.WriteLine($@"LibraryPeptideCount = {addIrtDlg.PeptidesCount},");
                    Console.WriteLine(ParseIrtProperties(row.Cells[2].Value.ToString()));
                }

                Assert.AreEqual(1.0, double.Parse(row.Cells[3].Value.ToString()));
                Assert.AreEqual(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Success, row.Cells[4].Value);
            });
            PauseForScreenShot("Add iRT peptides form", 15);

            var irtGraph = ShowDialog<GraphRegression>(() => addIrtDlg.ShowRegression(0));
            PauseForScreenShot("iRT regression graph", 15);

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

            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(() => {}, 600000); //importPeptideSearchDlg.ClickNextButton(), 600000);
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateTargets(_analysisValues.TargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"TargetCounts");
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
                ValidateTargets(_analysisValues.FinalTargetCounts, proteinCount, peptideCount, precursorCount, transitionCount, @"FinalTargetCounts");
            });
            PauseForScreenShot("Import FASTA summary form", 16);
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);

            PauseForScreenShot<AllChromatogramsGraph>("Loading chromatograms window", 13, 30*1000); // 30 second timeout to avoid getting stuck
            WaitForDocumentChangeLoaded(doc, 15 * 60 * 1000); // 15 minutes

            var peakScoringModelDlg = WaitForOpenForm<EditPeakScoringModelDlg>();
            ValidateCoefficients(peakScoringModelDlg, _analysisValues.ScoringModelCoefficients);
            PauseForScreenShot("mProphet model form", 17);

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
            PauseForScreenShot("Manual review window layout with protein selected", 19);

            FindNode("TDINQALNR");
            WaitForGraphs();
            PauseForScreenShot("Manual review window layout with peptide selected", 20);

            FindNode("_HUMAN");
            WaitForGraphs();
            FindNode("TDINQALNR");
            RunUI(SkylineWindow.AutoZoomBestPeak);
            WaitForGraphs();
            PauseForScreenShot("Snip just one chromatogram pane", 21);

            ClickChromatogram(SkylineWindow.Document.MeasuredResults.Chromatograms[0].Name,
                _analysisValues.ChromatogramClickPoint.X,
                _analysisValues.ChromatogramClickPoint.Y);
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - zoomed", 21);
            
            RunUI(() => SkylineWindow.GraphFullScan.ZoomToSelection(false));
            WaitForGraphs();
            PauseForScreenShot<GraphFullScan>("Full-Scan graph window - unzoomed", 22);

            RunUI(SkylineWindow.GraphFullScan.Close);
            RunUI(SkylineWindow.ShowMassErrorHistogramGraph);
            WaitForGraphs();
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out MassErrorHistogramGraphPane massErrorPane));
            int massErrorStatsIndex = 0;
            if (IsRecordMode)
            {
                Console.WriteLine(@"MassErrorStats = new[]");
                Console.WriteLine(@"{");
            }
            ValidateMassErrors(massErrorPane, massErrorStatsIndex++);

            // CONSIDER: No way to specify mass error graph window in PauseForScreenShot or ShowDialog
            PauseForScreenShot("Mass errors histogram graph window", 23);

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
                Console.WriteLine(@"},");
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
            PauseForScreenShot("Retention time regression graph window - regression", 24);

            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForGraphs();
            PauseForScreenShot("Retention time regression graph window - residuals", 25);
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
                /*fcGrid = WaitForOpenForm<FoldChangeGrid>();
                fcGridControlFinal = fcGrid.DataboundGridControl;
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

        private void ApplyFormatting(VolcanoPlotFormattingDlg formattingDlg, string matchText, string rgbText)
        {
            RunUI(() =>
            {
                var bindingList = formattingDlg.GetCurrentBindingList();
                var color = RgbHexColor.ParseRgb(rgbText).Value;
                bindingList.Add(new MatchRgbHexColor("ProteinName: " + matchText, false, color, PointSymbol.Circle, PointSize.normal));
            });
        }

        private void ValidateTargets(int[] targetCounts, int proteinCount, int peptideCount, int precursorCount, int transitionCount, string propName)
        {
            if (IsRecordMode)
            {
                Console.WriteLine(@"{0} = new[] {{ {1}, {2}, {3}, {4} }},", propName, proteinCount, peptideCount, precursorCount, transitionCount);
                return;
            }

            var targetCountsActual = new[] {proteinCount, peptideCount, precursorCount, transitionCount};
            if (!ArrayUtil.EqualsDeep(targetCounts, targetCountsActual))
            {
                Assert.Fail("Expected target counts <{0}> do not match actual <{1}>.",
                    string.Join(", ", targetCounts),
                    string.Join(", ", targetCountsActual));
            }
        }

        private void ValidateCoefficients(EditPeakScoringModelDlg editDlgFromSrm, string expectedCoefficients)
        {
            string coefficients = string.Join(@"|", GetCoefficientStrings(editDlgFromSrm));
            if (IsRecordMode)
                Console.WriteLine(@"ScoringModelCoefficients = ""{0}"",", coefficients);  // Not L10N
            else
                AssertEx.AreEqualLines(expectedCoefficients, coefficients);
        }

        private void ValidateMassErrors(MassErrorHistogramGraphPane massErrorPane, int index)
        {
            double mean = massErrorPane.Mean, stdDev = massErrorPane.StdDev;
            if (IsRecordMode)
                Console.WriteLine(@"new[] {{{0:0.#}, {1:0.#}}},", mean, stdDev);  // Not L10N
            else
            {
                Assert.AreEqual(_analysisValues.MassErrorStats[index][0], mean, 0.05);
                Assert.AreEqual(_analysisValues.MassErrorStats[index][1], stdDev, 0.05);
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
