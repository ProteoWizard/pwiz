/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;
using System.IO;
using System.Linq;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.BiblioSpec;
using pwiz.Common.Chemistry;
using pwiz.Skyline.EditUI;
using System.Windows.Forms;
using pwiz.CommonMsData;
using pwiz.Skyline.Model;

namespace TestPerf
{
    /// <summary>
    /// Runs the Acquisition Comparison tutorial
    /// </summary>
    [TestClass]
    public class AcquisitionComparisonTutorialTest : AbstractFunctionalTestEx
    {
        private string ROOT_DIR;
        private string PRM_DATA_DIR;
        private string DIA_DATA_DIR;
        private string DDA_DATA_DIR;

        [TestMethod, NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestAcquisitionComparisonTutorial()
        {
            // Not yet translated
            if (IsTranslationRequired)
                return;
            // RunPerfTests = true;
            CoverShotName = "AcquisitionComparison";

            TestFilesZipPaths = new[]
            {
                // CONSIDER: Get raw files?
                @"http://skyline.ms/tutorials/AcquisitionComparisonMzml.zip",
                @"TestPerf\AcquisitionComparisonViews.zip",
            };
            InitPaths();
            TestFilesPersistent = new[] { ".mzML", ".tsv", ".speclib", "mqpar.xml", "msms.txt" };
            RunFunctionalTest();
        }

        private void InitPaths()
        {
            // Initialize paths specific to the Acquisition Comparison tutorial
            ROOT_DIR = "AcquisitionComparisonMzml";
            PRM_DATA_DIR = Path.Combine(ROOT_DIR, "1_PRM");
            DIA_DATA_DIR = Path.Combine(ROOT_DIR, "2_DIA");
            DDA_DATA_DIR = Path.Combine(ROOT_DIR, "3_DDA");
        }

        private string DataExt => DataSourceUtil.EXT_MZML;  // Update to support raw data

        private string GetTestPath(string relativePath)
        {
            if (!relativePath.StartsWith(ROOT_DIR))
                relativePath = Path.Combine(ROOT_DIR, relativePath);
            return TestFilesDirs.First().GetTestPath(relativePath);
        }

        private const string PROCAL_LIBRARY = "PROCAL";
        private const string PROCAL_KOINA_LIBRARY = "PROCAL-Koina";

        private bool IsConnected => IsRecordingScreenShots;

        protected override Bitmap ProcessCoverShot(Bitmap bmp)
        {
            using var graph = Graphics.FromImage(base.ProcessCoverShot(bmp));
            
            // Draw the figure 1 image from the tutorial in the lower right corner
            using var figure1 = Image.FromFile(Path.Combine(TutorialPath, "image-figure1.png"));
            int widthFigure1 = 509;
            int destX = bmp.Width - widthFigure1 - 15;
            int destY = bmp.Height - figure1.Height - 55;
            var rectFigure1 = new Rectangle(destX, destY, widthFigure1, figure1.Height);
            var rectSrc = new Rectangle(0, 0, widthFigure1, figure1.Height);
            graph.DrawImage(figure1, rectFigure1, rectSrc, GraphicsUnit.Pixel);
            
            // Draw a border around it to make it stand out a bit more
            using var whitePen = new Pen(Color.White);
            rectFigure1.X--;
            rectFigure1.Y--;
            rectFigure1.Width++;
            rectFigure1.Height++;
            graph.DrawRectangle(whitePen, rectFigure1);
            using var grayPen = new Pen(Color.Gray, 4);
            rectFigure1.Inflate(2, 2);
            rectFigure1.Width++;
            rectFigure1.Height++;
            graph.DrawRectangle(grayPen, rectFigure1);
            
            return bmp;
        }

        protected override void DoTest()
        {
            PrepareTargets();
            if (IsConnected)
                BuildKoinaLibrary();
            else
                SkipScreenshots(8);
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Tutorial_Libraries" + SrmDocument.EXT)));
            ProcessPrmData();
            if (IsCoverShotMode)
                return;
            ProcessDiaData();
            ProcessDdaData();
        }

        private void PrepareTargets()
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            var addLibUI = ShowDialog<EditLibraryDlg>(editListUI.AddItem);
            RunUI(() => addLibUI.LibrarySpec =
                new BiblioSpecLibSpec(PROCAL_LIBRARY, GetTestPath(Path.Combine(PRM_DATA_DIR, PROCAL_LIBRARY + BiblioSpecLiteSpec.EXT))));
            OkDialog(addLibUI, addLibUI.OkDialog);
            OkDialog(editListUI, editListUI.OkDialog);
            
            RunUI(() => peptideSettingsUI.SetLibraryChecked(0, true));
            
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab with PROCAL library");

            var doc = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChangeLoaded(doc);

            var viewLibDlg = ShowDialog<ViewLibraryDlg>(SkylineWindow.ViewSpectralLibraries);
            
            PauseForScreenShot<ViewLibraryDlg>("Spectral Library Explorer showing PROCAL library");
            
            OkDialog(viewLibDlg, viewLibDlg.Close);
            
            RunUI(() => SkylineWindow.ImportFastaFile(GetTestPath(Path.Combine(PRM_DATA_DIR, "PROCAL.FASTA"))));
            
            RunUIForScreenShot(() => SkylineWindow.Size = new Size(940, 635));
            RestoreViewOnScreen(4);
            PauseForScreenShot("Main window with PROCAL targets and Library Match");
            
            RunUI(() => SkylineWindow.ShowBIons(true));
            
            RunUI(SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Expand); // Expand the first peptide to see its transitions
            
            PauseForTargetsScreenShot("Top of Targets view showing first peptide expanded", true, 14);

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Filter;
                transitionSettingsUI.FragmentTypes = "y, b, p";
                transitionSettingsUI.RangeFrom = TransitionFilter.StartFragmentFinder.ION_2.Label;
                transitionSettingsUI.RangeTo = TransitionFilter.EndFragmentFinder.LAST_ION.Label;
                transitionSettingsUI.SpecialIons = Array.Empty<string>();
            });
            
            PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab");

            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Library;
                transitionSettingsUI.IonCount = transitionSettingsUI.MinIonCount = 6;
                transitionSettingsUI.Filtered = true;
            });

            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab");
            
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.Instrument;
                transitionSettingsUI.MinMz = 350;
            });

            PauseForScreenShot<TransitionSettingsUI.InstrumentTab>("Transition Settings - Instrument tab");
            
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUI.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettingsUI.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
            });
            
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan tab");
            
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            
            PauseForTargetsScreenShot("Targets view with added transitions", true, 14);
        }

        private void BuildKoinaLibrary()
        {
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettingsUI.ShowBuildLibraryDlg);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = PROCAL_KOINA_LIBRARY;
                buildLibraryDlg.LibraryPath = GetTestPath(Path.Combine(PRM_DATA_DIR, PROCAL_KOINA_LIBRARY + BiblioSpecLibSpec.EXT));
                buildLibraryDlg.LibraryPathSelectEnd();
            });
            var msgDlg = ShowDialog<AlertDlg>(() => buildLibraryDlg.Koina = true);
            var toolsOptionsUi = ShowDialog<ToolOptionsUI>(msgDlg.ClickYes);
            RunUI(() =>
            {
                toolsOptionsUi.NavigateToTab(ToolOptionsUI.TABS.Koina);
                toolsOptionsUi.KoinaIntensityModelCombo = KoinaIntensityModel.Models.First(m => m.Contains("Prosit") && m.Contains("HCD"));
                toolsOptionsUi.CECombo = 31;
                // iRT model should get selected by default
            });
            WaitForConditionUI(() => toolsOptionsUi.KoinaServerStatus == ToolOptionsUI.ServerStatus.AVAILABLE);
            
            PauseForScreenShot<ToolOptionsUI>("Koina options");
            
            OkDialog(toolsOptionsUi, toolsOptionsUi.OkDialog);

            RunUI(() => buildLibraryDlg.NCE = 31);
            
            PauseForScreenShot<BuildLibraryDlg>("Build Library form with Koina and NCE 31");
            
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            WaitForConditionUI(() =>
                peptideSettingsUI.PickedLibraries.Any(libName => Equals(libName, PROCAL_KOINA_LIBRARY)));
            
            PauseForScreenShot<PeptideSettingsUI>("Peptide Settings - Libraries tab with Koina library checked");

            var doc = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForDocumentChangeLoaded(doc);
            
            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(PROCAL_LIBRARY));
            WaitForGraphs();
            PauseForScreenShot<GraphSpectrum>("Library Match with PROCAL selected");

            RunUI(() => SkylineWindow.GraphSpectrum.SelectSpectrum(PROCAL_KOINA_LIBRARY));
            WaitForGraphs();
            PauseForScreenShot<GraphSpectrum>("Library Match with PROCAL-Koina selected");

            RunUI(() =>
            {
                Settings.Default.LibMatchMirror = true;
                SkylineWindow.GraphSpectrum.SelectSpectrum(PROCAL_LIBRARY);
                SkylineWindow.GraphSpectrum.SelectMirrorSpectrum(PROCAL_KOINA_LIBRARY);
            });
            WaitForGraphs();
            PauseForScreenShot<GraphSpectrum>("Library Match with mirror of PROCAL and PROCAL-Koina selected");

            RunUI(() => Settings.Default.Koina = true);
            RunUI(SkylineWindow.UpdateGraphPanes);

            if (IsCoverShotMode)
                return;

            WaitForConditionUI(() => SkylineWindow.GraphSpectrum.KoinaNCE == 31);
            RunUI(() => SkylineWindow.GraphSpectrum.KoinaNCE = 18);
            WaitForGraphs();
            PauseForScreenShot<GraphSpectrum>("Library Match with PROCAL v Koina mirror and 18 NCE");

            RunUI(() => SkylineWindow.GraphSpectrum.KoinaNCE = 39);
            RunUI(SkylineWindow.UpdateGraphPanes);
            WaitForGraphs();
            PauseForScreenShot<GraphSpectrum>("Library Match with PROCAL v Koina mirror and 39 NCE");

            RunUI(() =>
            {
                Settings.Default.Koina = false;
                Settings.Default.LibMatchMirror = false;
                SkylineWindow.UpdateGraphPanes();
            });
        }

        private void ProcessPrmData()
        {
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Tutorial_PRM" + SrmDocument.EXT)));

            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettingsUI.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUI.ProductMassAnalyzer = FullScanMassAnalyzerType.centroided;
                transitionSettingsUI.ProductRes = 10;
                transitionSettingsUI.RetentionTimeFilterType = RetentionTimeFilterType.none;
            });

            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan tab with PRM");

            var doc = SkylineWindow.Document;
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
            WaitForDocumentChange(doc);

            var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);

            PauseForScreenShot<ImportResultsDlg>("Import Results form for PRM data");

            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() =>
            {
                var dataFolder = Path.GetDirectoryName(GetTestPath(Path.Combine(PRM_DATA_DIR, "PRM_100fmol.mzML")));
                openDataSourceDialog.CurrentDirectory = new MsDataFilePath(dataFolder);
                openDataSourceDialog.SelectAllFileType(DataExt);
            });
            OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            var importResultsNameDlg = WaitForOpenForm<ImportResultsNameDlg>();
            var docPreLoad = SkylineWindow.Document;
            OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);
            WaitForDocumentChangeLoaded(docPreLoad, WAIT_TIME * 10);
            
            RunUI(() =>
            {
                SkylineWindow.Size = new Size(1470, 905);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.ShowPeakAreaLegend(false);
                SkylineWindow.ShowRTLegend(false);
            });
            
            RestoreViewOnScreen(11);
            RunUIForScreenShot(() => SkylineWindow.ActivateReplicate("PRM_100fmol"));
            RunUI(() => SkylineWindow.SynchronizeZooming(true));
            if (IsCoverShotMode)
            {
                TakeCoverShot();
                return;
            }
            PauseForScreenShot("Skyline main window with PRM data");
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void ProcessDiaData()
        {
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Tutorial_DIA" + SrmDocument.EXT)));
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() => SkylineWindow.ShowImportPeptideSearchDlg(ImportPeptideSearchDlg.Workflow.dia));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new []{GetTestPath(Path.Combine(DIA_DATA_DIR, "DIA_PROCAL.tsv.speclib"))});
            });
            WaitForConditionUI(() =>
                Equals(ScoreType.GenericQValue.ToString(), importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.FirstOrDefault()?.ScoreType?.ToString()));
            RunUI(() =>
            {
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.01, importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.First().ScoreThreshold);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("DIA Import Peptide Search - Build Spectral Library populated page");

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

            var importResults = importPeptideSearchDlg.ImportResultsControl as ImportResultsControl;
            Assert.IsNotNull(importResults);

            var dataFolder = Path.GetDirectoryName(GetTestPath(Path.Combine(DIA_DATA_DIR, "DIA_100fmol.mzML")));
            RunUI(() => importResults.UpdateResultsFiles(new[] { dataFolder }, true));

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.AreEqual(4, importResults.FoundResultsFiles.Count));

            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("DIA Import PeptideSearch - Extract chromatograms page with files");

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.match_modifications_page);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = false; // Can't do this with use results
                importPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = new[]
                {
                    Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED
                };
                importPeptideSearchDlg.TransitionSettingsControl.MinIonMz = 350;
                importPeptideSearchDlg.TransitionSettingsControl.IonRangeFrom =
                    TransitionFilter.StartFragmentFinder.ION_2.Label;
                importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 3;

                // Verify other values shown in the tutorial
                Assert.AreEqual(2000, importPeptideSearchDlg.TransitionSettingsControl.MaxIonMz);
                Assert.AreEqual(TransitionFilter.EndFragmentFinder.LAST_ION.Label, importPeptideSearchDlg.TransitionSettingsControl.IonRangeTo);
                Assert.AreEqual(6, importPeptideSearchDlg.TransitionSettingsControl.IonCount);
                Assert.AreEqual(0.05, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Value);
                Assert.AreEqual(MzTolerance.Units.mz, importPeptideSearchDlg.TransitionSettingsControl.IonMatchMzTolerance.Unit);
                // CONSIDER: Not that easy to validate 1, 2 in ion charges.
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("DIA Import Peptide Search - Transition settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            
            WaitForConditionUI(() =>
                importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                importPeptideSearchDlg.FullScanSettingsControl.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                // Verify other values shown in the tutorial
                Assert.AreEqual(FullScanMassAnalyzerType.centroided, importPeptideSearchDlg.FullScanSettingsControl.ProductMassAnalyzer);
                Assert.AreEqual(RetentionTimeFilterType.ms2_ids, importPeptideSearchDlg.FullScanSettingsControl.RetentionTimeFilterType);
                Assert.AreEqual(3, importPeptideSearchDlg.FullScanSettingsControl.Peaks);
                Assert.AreEqual(5, importPeptideSearchDlg.FullScanSettingsControl.TimeAroundPrediction);
                Assert.AreEqual(10, importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes);
                Assert.AreEqual(10, importPeptideSearchDlg.FullScanSettingsControl.ProductRes);
            });
            var isolationScheme =
                ShowDialog<EditIsolationSchemeDlg>(importPeptideSearchDlg.FullScanSettingsControl.AddIsolationScheme);
            RunUI(() =>
            {
                isolationScheme.IsolationSchemeName = "DIA_40windows";
                isolationScheme.UseResults = true;
            });
            OkDialog(isolationScheme, isolationScheme.OkDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("DIA Import Peptide Search - Full-Scan settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(Path.Combine(PRM_DATA_DIR, "PROCAL.fasta")));
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.DecoyGenerationEnabled);
                importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod =
                    Resources.DecoyGeneration_SHUFFLE_SEQUENCE_Shuffle_Sequence;
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
                Assert.IsFalse(importPeptideSearchDlg.ImportFastaControl.AutoTrain);
            });

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("DIA Import Peptide Search - Import FASTA page");

            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(2, proteinCount);
                Assert.AreEqual(80, peptideCount);
                Assert.AreEqual(90, precursorCount);
                Assert.AreEqual(780, transitionCount);
            });
            PauseForScreenShot<AssociateProteinsDlg>("DIA Import FASTA summary form");

            var docPreLoad = SkylineWindow.Document;
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            WaitForDocumentChangeLoaded(docPreLoad);
            
            RunUI(() =>
            {
                SkylineWindow.ArrangeGraphsTiled();
                // Select ISLGEHEGGGK++
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 5);
            });

            RunUIForScreenShot(() => SkylineWindow.ActivateReplicate("DIA_100fmol"));
            RunUI(() => SkylineWindow.SynchronizeZooming(true));
            FocusDocument();
            PauseForScreenShot("Skyline main window with DIA data");
            
            ShowPeptideIdTimesMenu("DIA_01fmol");
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void ProcessDdaData()
        {
            RunUI(() => SkylineWindow.NewDocument());
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath("Tutorial_DDA" + SrmDocument.EXT)));

            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() => SkylineWindow.ShowImportPeptideSearchDlg(ImportPeptideSearchDlg.Workflow.dda));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] { GetTestPath(Path.Combine(DDA_DATA_DIR, "msms.txt")) });
            });
            WaitForConditionUI(() =>
                Equals(ScoreType.MaxQuant.ToString(), importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.FirstOrDefault()?.ScoreType?.ToString()));
            RunUI(() =>
            {
                // Check default settings shown in the tutorial
                Assert.AreEqual(0.05, importPeptideSearchDlg.BuildPepSearchLibControl.Grid.Files.First().ScoreThreshold);
                Assert.IsFalse(importPeptideSearchDlg.BuildPepSearchLibControl.IncludeAmbiguousMatches);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("DDA Import Peptide Search - Build Spectral Library populated page");

            var messageDlg = ShowDialog<MessageDlg>(() => importPeptideSearchDlg.ClickNextButton());
            RunUI(() => AssertEx.Contains(messageDlg.Message, Resources.BiblioSpecLiteBuilder_AmbiguousMatches_The_library_built_successfully__Spectra_matching_the_following_peptides_had_multiple_ambiguous_peptide_matches_and_were_excluded_));
            OkDialog(messageDlg, messageDlg.OkDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);

            var importResults = importPeptideSearchDlg.ImportResultsControl as ImportResultsControl;
            Assert.IsNotNull(importResults);
            
            var dataFolder = Path.GetDirectoryName(GetTestPath(Path.Combine(DIA_DATA_DIR, "DDA_100fmol.mzML")));

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            RunUI(() => Assert.AreEqual(4, importResults.FoundResultsFiles.Count)); // Found because BlibBuild found them

            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("DDA Import PeptideSearch - Extract chromatograms page with files");

            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(() => importPeptideSearchDlg.ClickNextButton());
            OkDialog(importResultsNameDlg, importResultsNameDlg.NoDialog);

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.match_modifications_page);

            PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("DDA Import PeptideSearch - Modifications page");

            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
            RunUI(() =>
            {
                importPeptideSearchDlg.FullScanSettingsControl.PrecursorCharges = new[] { 2, 3 };

                // Verify other values shown in the tutorial
                Assert.AreEqual(FullScanPrecursorIsotopes.Count, importPeptideSearchDlg.FullScanSettingsControl.PrecursorIsotopesCurrent);
                Assert.AreEqual(3, importPeptideSearchDlg.FullScanSettingsControl.Peaks);
                Assert.AreEqual(FullScanMassAnalyzerType.centroided, importPeptideSearchDlg.FullScanSettingsControl.PrecursorMassAnalyzer);
                Assert.AreEqual(10, importPeptideSearchDlg.FullScanSettingsControl.PrecursorRes);
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("DIA Import Peptide Search - Transition settings");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.import_fasta_page);
                Assert.AreEqual("Trypsin [KR | P]", importPeptideSearchDlg.ImportFastaControl.Enzyme.GetKey());
                Assert.AreEqual(0, importPeptideSearchDlg.ImportFastaControl.MaxMissedCleavages);
                importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath(Path.Combine(PRM_DATA_DIR, "PROCAL.fasta")));
                Assert.IsTrue(importPeptideSearchDlg.ImportFastaControl.ContainsFastaContent);
            });

            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("DDA Import Peptide Search - Import FASTA page");

            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());
            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                Assert.AreEqual(1, proteinCount);
                Assert.AreEqual(40, peptideCount);
                Assert.AreEqual(44, precursorCount);
                Assert.AreEqual(132, transitionCount);
            });
            PauseForScreenShot<AssociateProteinsDlg>("DDA Import FASTA summary form");

            var docPreLoad = SkylineWindow.Document;
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            WaitForDocumentChangeLoaded(docPreLoad);

            RunUI(() =>
            {
                SkylineWindow.ArrangeGraphsTiled();
                // Select ISLGEHEGGGK+++
                SkylineWindow.SelectedPath = SkylineWindow.DocumentUI.GetPathTo((int)SrmDocument.Level.TransitionGroups, 7);
            });

            RunUIForScreenShot(() => SkylineWindow.ActivateReplicate("DDA_100fmol"));
            RunUI(() => SkylineWindow.SynchronizeZooming(true));
            FocusDocument();
            PauseForScreenShot("Skyline main window with DDA data");

            ShowPeptideIdTimesMenu("DDA_01fmol");
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void ShowPeptideIdTimesMenu(string replicateName)
        {

            if (!IsPauseForScreenShots)
                return;
            var graphChrom = SkylineWindow.GetGraphChrom(replicateName);
            var chromControl = graphChrom.GraphControl;
            ToolStripDropDown menuStrip = null, subMenuStrip = null;

            RunUI(() =>
            {
                chromControl.ContextMenuStrip.Show(graphChrom.PointToScreen(new Point(60, 20)));
                var peptideIdTimesMenuItem = chromControl.ContextMenuStrip.Items.OfType<ToolStripMenuItem>()
                    .First(i => Equals(i.Name, @"peptideIDTimesContextMenuItem"));
                peptideIdTimesMenuItem.ShowDropDown();
                peptideIdTimesMenuItem.DropDownItems.OfType<ToolStripMenuItem>()
                    .First(i => Equals(i.Name, @"idTimesMatchingContextMenuItem")).Select();

                menuStrip = chromControl.ContextMenuStrip;
                subMenuStrip = peptideIdTimesMenuItem.DropDown;
                menuStrip.Closing += DenyMenuClosing;
                subMenuStrip.Closing += DenyMenuClosing;
            });
            
            PauseForScreenShot("Chromatograms context menu above main window", null,
                bmp => ClipBitmap(bmp.CleanupBorder(), GetPeptideIdMenuRect(graphChrom, menuStrip, subMenuStrip)));

            RunUI(() =>
            {
                menuStrip.Closing -= DenyMenuClosing;
                subMenuStrip.Closing -= DenyMenuClosing;
                menuStrip.Close();
            });
        }

        private Rectangle GetPeptideIdMenuRect(GraphChromatogram graphChrom, ToolStripDropDown menuStrip, ToolStripDropDown subMenuStrip)
        {
            var rect = Rectangle.Union(menuStrip.Bounds, subMenuStrip.Bounds);
            var rectChrom = ScreenshotManager.GetDockedFormBounds(graphChrom);
            var rectSkyline = ScreenshotManager.GetFramedWindowBounds(SkylineWindow);
            return new Rectangle(rectChrom.Left - rectSkyline.Left, rect.Top - 10 - rectSkyline.Top, 
                rect.Width + rect.Left - rectChrom.Left + 10, rect.Height + 20);
        }
    }
}
