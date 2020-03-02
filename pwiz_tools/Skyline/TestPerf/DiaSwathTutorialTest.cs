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
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Skyline;
using pwiz.Skyline.Alerts;
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
using pwiz.Skyline.Model.Lib;
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
    /// Verify DIA/SWATH tutorial operation
    /// </summary>
    [TestClass]
    public class DiaSwathTutorialTest : AbstractFunctionalTestEx
    {
        private InstrumentSpecificValues _instrumentValues;
        private AnalysisValues _analysisValues;

        private class InstrumentSpecificValues
        {
            public string InstrumentTypeName;
            public string[] DiaFiles;
            public int LibraryPeptideCount;
            public double IrtSlope;
            public double IrtIntercept;
            public bool HasAmbiguousMatches;
            public string IsolationSchemeName;
            public string IsolationSchemeFile;
            public char IsolationSchemeFileSeparator;
        }

        private class AnalysisValues
        {
            public bool IsWholeProteome;
            public bool KeepPrecursors;

            public string IrtFilterText;
            public int? MinPeptidesPerProtein;
            public bool RemoveDuplicates;
            public int[] TargetCounts;
            public int[] FinalTargetCounts;
            public string ScoringModelCoefficients;
            public PointF ChromatogramClickPoint;
            public double[][] MassErrorStats;
            public int[] DiffPeptideCounts;
            public int UnpolishedProteins;
            public int? PolishedProteins;

            public string FastaPath =>
                IsWholeProteome
                    ? "DDA_search\\napedro_3mixed_human_yeast_ecoli_20140403_iRT_reverse.fasta"
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
        public void TestDiaTtofTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IrtFilterText = "standard",
                TargetCounts = new[] { 13, 272, 292, 1751 },
                FinalTargetCounts = new[] { 12, 272, 292, 1751 },
                ScoringModelCoefficients = "0.9138|-0.7889|3.5694|-1.2988|-0.6596|0.8953|-0.3182|-0.0554",
                ChromatogramClickPoint = new PointF(34.18F, 108.0F),
                MassErrorStats = new[]
                {
                    new []{2.9, 4.5},
                    new []{2.7, 4.3},
                    new []{3.8, 4.5},
                    new []{5.7, 3.7},
                    new []{4.6, 4.3},
                    new []{-0.4, 3.7},
                    new []{1.1, 3.9},
                },
                DiffPeptideCounts = new[] { 143, 44, 31, 57 },
                UnpolishedProteins = 9
            };

            TestTtofData();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaTtofFullSearchTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(34.18F, 108.0F),
                TargetCounts = new[] { 4908, 38022, 39353, 235938 },
                FinalTargetCounts = new[] { 2471, 27054, 27987, 167767 },
                ScoringModelCoefficients = "0.4698|-0.8032|3.8888|-0.1753|-0.4008|0.6675|0.0205|-0.0714",
                MassErrorStats = new[]
                {
                    new[] {2.7, 4.6},
                    new[] {2.4, 4.4},
                    new[] {3.9, 4.3},
                    new[] {5.1, 4.2},
                    new[] {4.4, 4.2},
                    new[] {-0.4, 4},
                    new[] {1, 4.1},
                },
                DiffPeptideCounts = new[] { 13129, 8174, 2781, 2163 },
                UnpolishedProteins = 2187,
                PolishedProteins = 2465,
            };

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
                LibraryPeptideCount = 18600,
                IrtSlope = 2.9257,
                IrtIntercept = -68.8503,
                HasAmbiguousMatches = true,
                IsolationSchemeName = "ETH TTOF (64 variable)",
                IsolationSchemeFile = "64_variable_windows.csv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_CSV,
            });

            RunTest();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaQeTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IrtFilterText = "standard",
                TargetCounts = new[] { 13, 274, 334, 2002 },
                FinalTargetCounts = new[] { 12, 274, 334, 2002 },
                ScoringModelCoefficients = "0.3987|-0.8643|2.9375|0.5705|-0.0418|1.0304|0.0522|-0.0849",
                ChromatogramClickPoint = new PointF(31.98F, 285741.3F),
                MassErrorStats = new[]
                {
                    new[] {2.1, 3},
                    new[] {1.5, 3.3},
                    new[] {2, 3.5},
                    new[] {2.2, 2.6},
                    new[] {2.3, 3.2},
                    new[] {2.5, 2.3},
                    new[] {2.1, 3.1},
                },
                DiffPeptideCounts = new[] { 145, 50, 31, 53 },
                UnpolishedProteins = 7
            };

            TestQeData();
        }

        [TestMethod]
        [Timeout(int.MaxValue)] // These can take a long time
        public void TestDiaQeFullSearchTutorial()
        {
            _analysisValues = new AnalysisValues
            {
                KeepPrecursors = false,
                IsWholeProteome = true,
                IrtFilterText = "iRT",
                MinPeptidesPerProtein = 2,
                RemoveDuplicates = true,
                ChromatogramClickPoint = new PointF(32.05F, 268334.7F),
                TargetCounts = new[] { 4065, 32056, 34970, 209527 },
                FinalTargetCounts = new[] { 2081, 22732, 24775, 148419 },
                ScoringModelCoefficients = "0.3042|-0.9019|3.3988|0.8467|-0.0433|0.8814|-0.0152|-0.0720",
                MassErrorStats = new[]
                {
                    new[] {2.3, 3.5},
                    new[] {1.8, 3.5},
                    new[] {2.4, 3.5},
                    new[] {2.5, 3.5},
                    new[] {2.5, 3.6},
                    new[] {2.5, 3.5},
                    new[] {2.3, 3.5},
                },
                DiffPeptideCounts = new[] { 10148, 6287, 2175, 1675 },
                UnpolishedProteins = 1264,
                PolishedProteins = 2036
            };

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
                LibraryPeptideCount = 15855,
                IrtSlope = 2.624,
                IrtIntercept = -48.003,
                HasAmbiguousMatches = false,
                IsolationSchemeName = "ETH QE (18 variable)",
                IsolationSchemeFile = "QE_DIA_18var.tsv",
                IsolationSchemeFileSeparator = TextUtil.SEPARATOR_TSV,
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

            RunFunctionalTest();

            Assert.IsFalse(IsRecordMode);   // Make sure this doesn't get committed as true
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        private string GetTestPath(string path)
        {
            return TestFilesDirs[0].GetTestPath(Path.Combine(RootName, path));
        }

        /// <summary>
        /// Change to true to write coefficient arrays
        /// </summary>
        private bool IsRecordMode { get { return false; } }

        protected override void DoTest()
        {
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
            string[] searchFiles =
            {
                GetTestPath("DDA_search\\interact.pep.xml"),
            };
            foreach (var searchFile in searchFiles)
                Assert.IsTrue(File.Exists(searchFile), string.Format("File {0} does not exist.", searchFile));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(searchFiles);
                importPeptideSearchDlg.BuildPepSearchLibControl.IrtStandards = IrtStandard.BIOGNOSYS_11;
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.95, importPeptideSearchDlg.BuildPepSearchLibControl.CutOffScore);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Import Peptide Search - Build Spectral Library populated page", 4);

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            var addIrtDlg = ShowDialog<AddIrtPeptidesDlg>(() => importPeptideSearchDlg.ClickNextButton());
            RunUI(() =>
            {
                // Check values shown in the tutorial
                Assert.AreEqual(1, addIrtDlg.RunsConvertedCount);
                var row = addIrtDlg.GetRow(0);
                Assert.AreEqual(11, row.Cells[1].Value);
                Assert.AreEqual(_instrumentValues.LibraryPeptideCount, addIrtDlg.PeptidesCount);
                var regressionLine = new RegressionLine(_instrumentValues.IrtSlope, _instrumentValues.IrtIntercept);
                Assert.AreEqual(regressionLine.DisplayEquation, row.Cells[2].Value);
                Assert.AreEqual(1.0, double.Parse(row.Cells[3].Value.ToString()));
                Assert.AreEqual(Resources.AddIrtPeptidesDlg_AddIrtPeptidesDlg_Success, row.Cells[4].Value);
            });
            PauseForScreenShot("Add iRT peptides form", 5);

            var irtGraph = ShowDialog<GraphRegression>(() => addIrtDlg.ShowRegression(0));
            PauseForScreenShot("iRT regression graph", 5);

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
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

            string docLibPath = BiblioSpecLiteSpec.GetLibraryFileName(documentFile);
            string redundantDocLibPath = BiblioSpecLiteSpec.GetRedundantName(docLibPath);
            Assert.IsTrue(File.Exists(docLibPath) && File.Exists(redundantDocLibPath));
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.HasDocumentLibrary);

            var importResults = importPeptideSearchDlg.ImportResultsControl as ImportResultsDIAControl;
            Assert.IsNotNull(importResults);
            string diaDir = GetTestPath("DIA");
            var openDataFiles = ShowDialog<OpenDataSourceDialog>(() => importResults.Browse(diaDir));
            RunUI(() =>
            {
                openDataFiles.SelectAllFileType(ExtensionTestContext.ExtMzml);
                foreach (var selectedFile in openDataFiles.SelectedFiles)
                    Assert.IsTrue(DiaFiles.Contains(selectedFile));
            });
            PauseForScreenShot("Results files form", 6);
            OkDialog(openDataFiles, openDataFiles.Open);

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() =>
            {
                foreach (var resultFileName in importResults.FoundResultsFiles)
                    Assert.IsTrue(DiaFiles.Contains(Path.GetFileName(resultFileName.Path)));
            });
            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("Extract chromatograms page with files", 7);

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
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
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition settings", 8);
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
                Assert.AreEqual(RetentionTimeFilterType.scheduling_windows, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
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
            PauseForScreenShot("Isolation scheme", 9);

            var isolationGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationScheme.OpenGraph);
            PauseForScreenShot("Isolation scheme graph", 10);

            OkDialog(isolationGraph, isolationGraph.CloseButton);
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Import Peptide Search - Configure Full-Scan Settings page", 11);

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            string fastPath = GetTestPath(_analysisValues.FastaPath);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(fastPath);
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                importPeptideSearchDlg.ImportFastaControl.AutoTrain = true;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Import Peptide Search - Import FASTA page", 12);

            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(() => importPeptideSearchDlg.ClickNextButton());
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
            PauseForScreenShot("Import FASTA summary form", 13);

            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            PauseForScreenShot<AllChromatogramsGraph>("Loading chromatograms window", 13, 30*1000); // 30 second timeout to avoid getting stuck
            WaitForDocumentChangeLoaded(doc, 8 * 60 * 1000); // 10 minutes

            var peakScoringModelDlg = WaitForOpenForm<EditPeakScoringModelDlg>();
            ValidateCoefficients(peakScoringModelDlg, _analysisValues.ScoringModelCoefficients);
            PauseForScreenShot("mProphet model form", 14);

            OkDialog(peakScoringModelDlg, peakScoringModelDlg.OkDialog);

            // Setup annotations
            var documentSettingsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);

            AddReplicateAnnotation(documentSettingsDlg, "Condition", AnnotationDef.AnnotationType.value_list,
                new[] { "A", "B" }, 16);

            AddReplicateAnnotation(documentSettingsDlg, "BioReplicate");

            RunUI(() =>
            {
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true);
                documentSettingsDlg.AnnotationsCheckedListBox.SetItemChecked(1, true);
            });

            OkDialog(documentSettingsDlg, documentSettingsDlg.OkDialog);

            // Annotate replicates in Document Grid: Replicates
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            RunUI(() =>
            {
                documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates);
                FormEx.GetParentForm(documentGrid).Size = new Size(591, 283);
            });
            WaitForConditionUI(() => documentGrid.IsComplete); // Let it initialize

            RunUI(() =>
            {
                var pathCondition = PropertyPath.Root.Property(AnnotationDef.GetColumnName("Condition"));
                var columnSubjectId = documentGrid.FindColumn(pathCondition);
                var gridView = documentGrid.DataGridView;
                gridView.CurrentCell = gridView.Rows[0].Cells[columnSubjectId.Index];
            });

            var replicateAnnotations = new[]
            {
                new[] {"A", "1"},
                new[] {"B", "1"},
                new[] {"A", "2"},
                new[] {"B", "2"},
                new[] {"A", "3"},
                new[] {"B", "3"}
            };
            SetClipboardText(TextUtil.LineSeparate(replicateAnnotations.Select(TextUtil.ToEscapedTSV)));

            RunUI(() => documentGrid.DataGridView.SendPaste());

            PauseForScreenShot<DocumentGridForm>("Document Grid - filled", 17);

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

            FindNode("LPQVEGTGGDVQPSQDLVR");
            WaitForGraphs();
            PauseForScreenShot("Manual review window layout with peptide selected", 20);

            FindNode("_HUMAN");
            WaitForGraphs();
            FindNode("LPQVEGTGGDVQPSQDLVR");
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

            var editGroupComparisonDlg = ShowDialog<EditGroupComparisonDlg>(SkylineWindow.AddGroupComparison);
            const string groupComparisonName = @"By Condition";
            RunUI(() =>
            {
                editGroupComparisonDlg.TextBoxName.Text = groupComparisonName;
                editGroupComparisonDlg.ComboControlAnnotation.SelectedItem = @"Condition";
            });
            WaitForConditionUI(() => editGroupComparisonDlg.ComboControlValue.Items.Count > 0);
            RunUI(() =>
            {
                editGroupComparisonDlg.ComboControlValue.SelectedItem = "A";
                editGroupComparisonDlg.ComboCaseValue.SelectedItem = "B";
                editGroupComparisonDlg.ComboIdentityAnnotation.SelectedItem = "BioReplicate";   // Irrelevant
                editGroupComparisonDlg.ShowAdvanced(true);
                editGroupComparisonDlg.TextBoxQValueCutoff.Text = (0.01).ToString(CultureInfo.CurrentCulture);
            });
            PauseForScreenShot("Group comparison", 26);

            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.OkDialog);
            var fcResultProperty = PropertyPath.Root.Property("FoldChangeResult");
            var proteinProperty = PropertyPath.Root.Property("Protein");
            RunUI(() => SkylineWindow.ShowGroupComparisonWindow(groupComparisonName));
            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>();
                var fcGridControl = fcGrid.DataboundGridControl;
                WaitForConditionUI(() => fcGridControl.IsComplete && fcGridControl.FindColumn(fcResultProperty) != null && fcGridControl.RowCount > 11);
                RunUI(() =>
                {
                    var foldChangeResultColumn = fcGridControl.FindColumn(fcResultProperty);
                    fcGridControl.DataGridView.AutoResizeColumn(foldChangeResultColumn.Index);
                    var proteinNameColumn = fcGridControl.FindColumn(proteinProperty);
                    fcGridControl.DataGridView.AutoResizeColumn(proteinNameColumn.Index);
                    fcGridControl.DataGridView.FirstDisplayedScrollingRowIndex = 11;  // Scroll past iRT peptides
                });
                WaitForConditionUI(() => 0 != fcGridControl.RowCount, "0 != foldChangeGrid.DataboundGridControl.RowCount");
                WaitForConditionUI(() => fcGridControl.IsComplete, "foldChangeGrid.DataboundGridControl.IsComplete");
                PauseForScreenShot<FoldChangeGrid>("By Condition grid", 27);

                var volcanoPlot = ShowDialog<FoldChangeVolcanoPlot>(fcGrid.ShowVolcanoPlot);
                RestoreViewOnScreenNoSelChange(27);
                fcGrid = WaitForOpenForm<FoldChangeGrid>();
                WaitForConditionUI(() => fcGrid.DataboundGridControl.IsComplete && fcGrid.DataboundGridControl.RowCount > 11);
                RunUI(() => fcGrid.DataboundGridControl.DataGridView.FirstDisplayedScrollingRowIndex = 11); // Re-apply scrolling
                PauseForScreenShot<FoldChangeVolcanoPlot>("By Condition:Volcano Plot - unformatted", 27);
                volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();    // May have changed with RestoreViewsOnScreen
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 5);
                WaitForConditionUI(() => volcanoPlot.CurveList[4].Points.Count > SkylineWindow.Document.MoleculeCount/4);
                RunUI(() =>
                {
                    int actualPoints = volcanoPlot.CurveList[4].Points.Count;
                    if (IsRecordMode)
                        Console.Write(@"DiffPeptideCounts = new[] { " + actualPoints);
                    else
                        Assert.AreEqual(_analysisValues.DiffPeptideCounts[0], actualPoints);
                });
                var formattingDlg = ShowDialog<VolcanoPlotFormattingDlg>(volcanoPlot.ShowFormattingDialog);
                ApplyFormatting(formattingDlg, "ECOLI", "128, 0, 255");
                var createExprDlg = ShowDialog<CreateMatchExpressionDlg>(() =>
                {
                    var bindingList = formattingDlg.GetCurrentBindingList();
                    formattingDlg.ClickCreateExpression(bindingList.Count - 1);
                });
                PauseForScreenShot("Create Expression form", 28);
                OkDialog(createExprDlg, createExprDlg.OkDialog);

                ApplyFormatting(formattingDlg, "YEAS", "255, 128, 0");
                ApplyFormatting(formattingDlg, "HUMAN", "0, 128, 0");
                PauseForScreenShot("Volcano plot formatting form", 29);

                OkDialog(formattingDlg, formattingDlg.OkDialog);
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 8 &&
                                         volcanoPlot.CurveList[7].Points.Count == 11); // iRTs
                for (int i = 1; i < 4; i++)
                {
                    RunUI(() =>
                    {
                        int actualPoints = volcanoPlot.CurveList[7 - i].Points.Count;
                        if (IsRecordMode)
                            Console.Write(@", " + actualPoints);
                        else
                            Assert.AreEqual(_analysisValues.DiffPeptideCounts[i], actualPoints);
                    });
                }
                if (IsRecordMode)
                    Console.WriteLine(@" },");
                PauseForScreenShot<FoldChangeVolcanoPlot>("By Condition:Volcano Plot - fully formatted", 29);
            }

            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>(); // May have changed with RestoreViewsOnScreen
                RunUI(fcGrid.ShowGraph);
                RestoreViewOnScreenNoSelChange(30);
            }

            {
                var fcGrid = WaitForOpenForm<FoldChangeGrid>(); // May have changed with RestoreViewsOnScreen
                var fcGridControl = fcGrid.DataboundGridControl;
                WaitForConditionUI(() => fcGridControl.IsComplete && fcGridControl.FindColumn(proteinProperty) != null);
                var quickFilterForm = ShowDialog<QuickFilterForm>(() =>
                {
                    var proteinNameColumn = fcGridControl.FindColumn(proteinProperty);
                    fcGridControl.QuickFilter(proteinNameColumn);
                });
                RunUI(() =>
                {
                    quickFilterForm.SetFilterOperation(0, FilterOperations.OP_NOT_CONTAINS);
                    quickFilterForm.SetFilterOperand(0, _analysisValues.IrtFilterText);
                });
                OkDialog(quickFilterForm, quickFilterForm.OkDialog);

                var volcanoPlot = WaitForOpenForm<FoldChangeVolcanoPlot>();    // May have changed with RestoreViewsOnScreen
                WaitForConditionUI(() => volcanoPlot.CurveList.Count == 8 && 
                                         volcanoPlot.CurveList[7].Points.Count == 0); // No iRTs
                if (!IsRecordMode)
                {
                    for (int i = 1; i < 4; i++)
                        RunUI(() => Assert.AreEqual(_analysisValues.DiffPeptideCounts[i], volcanoPlot.CurveList[7 - i].Points.Count));
                }
                var barGraph = WaitForOpenForm<FoldChangeBarGraph>();
                const int volcanoBarDelta = 11 - 1; // iRTs - selected peptide
                if (!IsRecordMode)
                    WaitForBarGraphPoints(barGraph, _analysisValues.DiffPeptideCounts[0] - volcanoBarDelta);

                SortByFoldChange(fcGridControl, fcResultProperty);
                PauseForScreenShot<FoldChangeBarGraph>("By Condition:Bar Graph - peptides", 30);

                var changeGroupComparisonSettings = ShowDialog<EditGroupComparisonDlg>(fcGrid.ShowChangeSettings);
                RunUI(() => changeGroupComparisonSettings.RadioScopePerProtein.Checked = true);

                int targetProteinCount = SkylineWindow.Document.MoleculeGroupCount - 2; // minus iRTs and decoys
                int unpolishedCount = _analysisValues.UnpolishedProteins;
                if (!IsRecordMode)
                    WaitForBarGraphPoints(barGraph, unpolishedCount);
                else
                {
                    WaitForBarGraphPoints(barGraph, targetProteinCount, 1);
                    unpolishedCount = GetBarCount(barGraph);
                    Console.WriteLine(@"UnpolishedProteins = {0},", unpolishedCount);
                }

                RunUI(() => changeGroupComparisonSettings.ComboSummaryMethod.SelectedItem =
                    SummarizationMethod.MEDIANPOLISH);

                if (!IsRecordMode)
                    WaitForBarGraphPoints(barGraph, _analysisValues.PolishedProteins ?? targetProteinCount);
                else
                {
                    WaitForBarGraphPoints(barGraph, targetProteinCount, unpolishedCount);
                    if (GetBarCount(barGraph) != targetProteinCount)
                        Console.WriteLine(@"PolishedProteins = {0},", GetBarCount(barGraph));
                }
                fcGrid = WaitForOpenForm<FoldChangeGrid>();
                var fcGridControlFinal = fcGrid.DataboundGridControl;
                SortByFoldChange(fcGridControlFinal, fcResultProperty);  // Re-apply the sort, in case it was lost in restoring views
                PauseForScreenShot<FoldChangeBarGraph>("By Condition:Graph - proteins", 31);
            }
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