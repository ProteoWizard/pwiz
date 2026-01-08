/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.FileUI.PeptideSearch;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class DiaTutorialTest : AbstractFunctionalTestEx
    {
        private const string DIA_IMPORTED_CHECKPOINT = "DIA-tutorial-imported.sky";
        private const string DIA_TUTORIAL_CHECKPOINT = "DIA-tutorial.sky";

        /// <summary>
        /// Change to true to run full import of DIA data.  Also used
        /// to regenerate checkpoint files for non-full-import mode,
        /// when something changes in the test.
        /// </summary>
        private bool IsFullImportMode { get { return IsRecordMode || IsCoverShotMode || IsPauseForScreenShots || IsAutoScreenShotMode; } }

        protected override bool IsRecordMode => false;

        [TestMethod]
        public void TestDiaTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;
            // IsCoverShotMode = true;
            CoverShotName = "DIA";
            // PauseStartPage = 36;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/DIA-22_2.pdf";

            TestFilesZipPaths = new[]
                {
                    // There is a small and a large version of the tutorial data.
                    // Large version contains 1 DDA and 2 DIA runs for 6 GB.
                    // Small version runs by default, skips some steps.
                IsFullImportMode
                    ? @"https://skyline.ms/tutorials/DIA-20_2.zip"
                    : @"https://skyline.ms/tutorials/DIALibrary-20_2.zip",
                    @"TestTutorial\DiaViews.zip"
                };

            TestFilesPersistent = new[] { "20130311_DIA_Pit01", "20130311_DIA_Pit02" };

            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderTutorial = IsFullImportMode ? "DIA-20_2" : "DIALibrary-20_2" ; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderTutorial, relativePath));
        }

        protected override void DoTest()
        {
            // Clear all the settings lists that will be defined in this tutorial
            ClearSettingsLists();

            // Specify DIA acquisition scheme and machine settings
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                transitionSettings.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettings.PrecursorMassAnalyzer = FullScanMassAnalyzerType.centroided;
                transitionSettings.PrecursorRes = 20;
                transitionSettings.ProductMassAnalyzer = FullScanMassAnalyzerType.centroided;
                transitionSettings.ProductRes = 20;
            });
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan");

            // Set up isolation scheme
            var isolationSchemeDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);

            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme form");
            
            RunUI(() =>
            {
                isolationSchemeDlg.UseResults = false;
            });
            
            var calculateIsolationDlg = ShowDialog<CalculateIsolationSchemeDlg>(isolationSchemeDlg.Calculate);
            RunUI(() =>
            {
                calculateIsolationDlg.WindowWidth = 20;
                calculateIsolationDlg.Start = 500;
                calculateIsolationDlg.End = 900;
                calculateIsolationDlg.OptimizeWindowPlacement = true;
            });
            PauseForScreenShot<CalculateIsolationSchemeDlg>("Calculate Isolation Scheme form");
            OkDialog(calculateIsolationDlg, calculateIsolationDlg.OkDialog);
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog Filled");

            var isolationSchemeGraphDlg = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeDlg.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Graph of Isolation Scheme");
            OkDialog(isolationSchemeGraphDlg, isolationSchemeGraphDlg.CloseButton);
            const string isolationSchemeName = "500 to 900 by 20";
            RunUI(() => isolationSchemeDlg.IsolationSchemeName = isolationSchemeName);
            OkDialog(isolationSchemeDlg, isolationSchemeDlg.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // Export isolation scheme
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() => exportIsolationDlg.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE);
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form");
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(GetTestPath("DIA_tutorial_isolation_list.csv")));

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT)));

            // "Build Spectral Library" page
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() => SkylineWindow.ShowImportPeptideSearchDlg());
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Spectral Library page");
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] {GetTestPath("interact-20130311_DDA_Pit01.pep.xml")}); // Not L10N
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            WaitForConditionUI(() => importPeptideSearchDlg.IsNextButtonEnabled);
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Spectral Library page - input files");

            using (new WaitDocumentChange(1, true))
            {
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            }

                // "Extract Chromatograms" page
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.chromatograms_page);
            TryWaitForOpenForm(typeof(ImportPeptideSearchDlg.ChromatogramsDiaPage));    // So that SkylineTester - Forms tab pauses
            var importResults = importPeptideSearchDlg.ImportResultsControl as ImportResultsDIAControl;
            Assert.IsNotNull(importResults);
            const string prefixKeep = "Pit0";
            if (!IsFullImportMode)
            {
                var noFilesMessage = ShowDialog<MultiButtonMsgDlg>(() => importPeptideSearchDlg.ClickNextButton());
                OkDialog(noFilesMessage, noFilesMessage.OkDialog);
            }
            else
            {
                string baseDir = TestFilesPersistent != null
                    ? TestFilesDirs[0].PersistentFilesDir
                    : TestFilesDirs[0].FullPath;
                string diaDir = Path.Combine(baseDir, "DIA-20_2");
                var openDataFiles = ShowDialog<OpenDataSourceDialog>(() => importResults.Browse(diaDir));
                RunUI(() => openDataFiles.SelectAllFileType(ExtensionTestContext.ExtThermoRaw));
                PauseForScreenShot<OpenDataSourceDialog>("Browse for Results Files form");
                OkDialog(openDataFiles, openDataFiles.Open);

                PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("Extract Chromatograms page");

                var importResultsNameDlg =
                    ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
                RunUI(() =>
                {
                    string prefix = importResultsNameDlg.Prefix;
                    Assert.IsTrue(prefix.EndsWith(prefixKeep));
                    importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - prefixKeep.Length);
                });
                PauseForScreenShot<ImportResultsNameDlg>("Import Results names form");
                OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
            }

            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.match_modifications_page);

                // "Add Modifications" page
                RunUI(() =>
                {
                    const string modOxidation = "Oxidation (M)";
                    // Define expected matched/unmatched modifications
                var expectedMatched = new[] {modOxidation};
                    // Verify matched/unmatched modifications
                    AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToArray());
                    Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());
                });
            // PauseForScreenShot<ImportPeptideSearchDlg.MatchModsPage>("Modifications page", 19);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
                WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                         ImportPeptideSearchDlg.Pages.transition_settings_page);

                // "Configure Transition Settings" page
            // RunUI(() =>
            // {
                // Allowing charge 4 precursors introduces GWCLESSQCQDLTTESNLLECIR which requires an involved
                // explanation, because interference shifts its precursor signal out of the extraction range
                // importPeptideSearchDlg.TransitionSettingsControl.PeptidePrecursorCharges = Adduct.ProtonatedFromCharges(2, 3, 4);
                // These settings are now just the defaults for DIA
                // importPeptideSearchDlg.TransitionSettingsControl.PeptideIonCharges = Adduct.ProtonatedFromCharges(1, 2);
                // importPeptideSearchDlg.TransitionSettingsControl.PeptideIonTypes = new[] { IonType.y, IonType.b, IonType.precursor };
                // importPeptideSearchDlg.TransitionSettingsControl.IonRangeFrom = TransitionFilter.StartFragmentFinder.ION_3.Label;
                // importPeptideSearchDlg.TransitionSettingsControl.IonRangeTo = TransitionFilter.EndFragmentFinder.LAST_ION.Label;
                // importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = true;
                // importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = 0.05;
                // importPeptideSearchDlg.TransitionSettingsControl.IonCount = 6;
                // importPeptideSearchDlg.TransitionSettingsControl.MinIonCount = 6;
            // });
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition Settings page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.full_scan_settings_page);

                // "Configure Full-Scan Settings" page
            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Full-Scan Settings page");
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

                // "Import FASTA" page
                RunUI(() =>
                {
                    importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("pituitary_database.fasta"));
                    importPeptideSearchDlg.ImportFastaControl.DecoyGenerationMethod = string.Empty;
                });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Fasta page");
            var peptidesPerProteinDlg = ShowDialog<AssociateProteinsDlg>(() => importPeptideSearchDlg.ClickNextButton());

            WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            PauseForScreenShot<AssociateProteinsDlg>("Peptides per protein form");
            RunUI(() =>
            {
                int proteinCount, peptideCount, precursorCount, transitionCount;
                //peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                //ValidateNewTargetCounts(proteinCount, peptideCount, precursorCount, transitionCount);
                peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateNewTargetCounts(proteinCount, peptideCount, precursorCount, transitionCount);
            });
            OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
            WaitForClosedForm(importPeptideSearchDlg);
            WaitForConditionUI(() => SkylineWindow.DocumentUI.PeptideGroupCount == 6);
            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                if(IsPauseForScreenShots)
                    SkylineWindow.SequenceTree.SetScrollPos(Orientation.Horizontal, 0);
            });

            if (IsPauseForScreenShots)
            {
                var allChrom = WaitForOpenForm<AllChromatogramsGraph>();
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(1330, 700);
                    allChrom.Left = SkylineWindow.Right + 20;
                });

                PauseForTargetsScreenShot("Targets view clipped - scrolled left and before fully imported");
            }
            WaitForDocumentLoaded(10 * 60 * 1000);    // 10 minutes

            RunUI(() => SkylineWindow.SaveDocument());

            if (IsFullImportMode)
            {
                RunUI(() => SkylineWindow.SaveDocument(GetTestPath(DIA_IMPORTED_CHECKPOINT)));
            }
            else
            {
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_IMPORTED_CHECKPOINT)));
            }

            if (IsRecordMode)
                PauseForManualTutorialStep("COPY IMPORTED DOCUMENT");

            // Generate decoys
            //            var decoysDlg = ShowDialog<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg);
            //            PauseForScreenShot<GenerateDecoysDlg>("Add Decoy Peptides form", 24);
            //            RunUI(() =>
            //            {
            //                decoysDlg.NumDecoys = 26;
            //                Assert.AreEqual(decoysDlg.DecoysMethod, DecoyGeneration.SHUFFLE_SEQUENCE);
            //            });
            //            OkDialog(decoysDlg, decoysDlg.OkDialog);
            //            RunUI(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.SelectedNode.PrevNode.Nodes[6]);
            //            PauseForScreenShot<SequenceTreeForm>("Targets pane with decoys added", 25);

            SelectNode(SrmDocument.Level.Molecules, 0); // 1st peptide
            RunUI(() =>
            {
                SkylineWindow.CollapsePeptides();
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ArrangeGraphs(DisplayGraphsType.Row);
                // CONSIDER: SkylineWindow should probably have a function for this
                Settings.Default.PeakAreaDotpDisplay = DotProductDisplayOption.label.ToString();
                SkylineWindow.Width = 1250;
            });

            RestoreViewOnScreen(27);
            WaitForDocumentLoaded();    // Retention time alignment before screenshot
            PauseForScreenShot("Skyline window");

            RunUI(() =>
            {
                SkylineWindow.ShowMassErrorHistogramGraph();
                SkylineWindow.ChangeMassErrorTransition(TransitionMassError.all);
            });
            WaitForGraphs();
            PauseForGraphScreenShot("Mass Errors: Histogram metafile", FindGraphSummaryByGraphType<MassErrorHistogramGraphPane>());
            RunUI(() =>
            {
                ValidateMassErrorStatistics(2.7, 3.3);
                SkylineWindow.ChangeMassErrorDisplayType(DisplayTypeMassError.precursors);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                ValidateMassErrorStatistics(2.9, 2.1);
                SkylineWindow.ShowGraphMassError(false, GraphTypeSummary.histogram);
            });

            ChangePeakBounds("Pit01", 45.3, 46.3);
            ChangePeakBounds("Pit02", 45.4, 46.4);

            RunUI(() => SkylineWindow.ShowOtherRunPeptideIDTimes(true));
            // Jitter selection to update graphs
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Parent);
            WaitForGraphs();
            RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
            WaitForGraphs();
            SetPeakAreasMaxes(6 * Math.Pow(10, 9), 7 * Math.Pow(10, 8));
            PauseForScreenShot("Skyline window - with manual integration and ID times");

            RunUI(() =>
            {
                SkylineWindow.ShowChromatogramLegends(true);
                SkylineWindow.Height = 735;
            });
            RestoreViewOnScreen(31);
            SelectNode(SrmDocument.Level.Molecules, 1);  // 2nd peptide
            PauseForGraphScreenShot("Chromatogram graph metafile", SkylineWindow.GetGraphChrom("Pit01"));

            RestoreViewOnScreen(27);
            RunUI(() =>
            {
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.Height = 700;
            });
            SelectNode(SrmDocument.Level.TransitionGroups, 1);  // 2nd peptide - first precursor
            WaitForGraphs();
            SetPeakAreasMaxes(1 * Math.Pow(10, 9), 6 * Math.Pow(10, 7));
            PauseForScreenShot("Skyline window");

            SelectNode(SrmDocument.Level.Transitions, 22);

            RunUI(() =>
            {
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.Height = 463;
            });

            PauseForGraphScreenShot("Product ion chromatogram graph metafile 1", SkylineWindow.GetGraphChrom("Pit01"));
            PauseForGraphScreenShot("Product ion chromatogram graph metafile 2", SkylineWindow.GetGraphChrom("Pit02"));

            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.Height = 700;
            });
            RestoreViewOnScreen(34);
            FindNode("NYGLLYCFR");
            ChangePeakBounds("Pit01", 65.36, 66.7);
            ChangePeakBounds("Pit02", 64.89, 66.2);

            Func<Bitmap, Bitmap> clipChromAndPeakAreas = bmp => 
                ClipSkylineWindowShotWithForms(bmp, new DockableForm[]
                {
                    SkylineWindow.GetGraphChrom("Pit01"),
                    SkylineWindow.GetGraphChrom("Pit02"),
                    FindGraphSummaryByGraphType<AreaReplicateGraphPane>()
                });

            PauseForScreenShot("Chromatograms and peak areas", null, clipChromAndPeakAreas);

            SelectNode(SrmDocument.Level.TransitionGroups, 13);

            PauseForScreenShot("Chromatograms and peak areas", null, clipChromAndPeakAreas);

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 12;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.ShowChromatogramLegends(true);
                });

                RestoreCoverViewOnScreen();
                ClickChromatogram("Pit02", 44.1353, 926456.5, PaneKey.PRODUCTS);
                TreeNode selectedNode = null;
                RunUI(() => selectedNode = SkylineWindow.SequenceTree.SelectedNode);
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = selectedNode);
                var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
                RunUI(() =>
                {
                    transitionSettingsUI.Top = SkylineWindow.Top;
                    transitionSettingsUI.Left = SkylineWindow.Left - transitionSettingsUI.Width - 20;
                });
                var isolationSchemeForm = ShowDialog<EditIsolationSchemeDlg>(transitionSettingsUI.EditCurrentIsolationScheme);
                RunUI(() =>
                {
                    isolationSchemeForm.Top = SkylineWindow.Bottom - isolationSchemeForm.Height - 34;
                    isolationSchemeForm.Left = SkylineWindow.Left + 40;
                    isolationSchemeForm.Width = 400;
                });
                var isolationSchemeGraph = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeForm.OpenGraph);
                RunUI(() =>
                {
                    isolationSchemeGraph.Height -= 58;
                    isolationSchemeGraph.Top = SkylineWindow.Bottom - isolationSchemeGraph.Height;
                    isolationSchemeGraph.Left = SkylineWindow.Left;
                    isolationSchemeGraph.Width = 480;
                });
                TakeCoverShot(isolationSchemeGraph);
                OkDialog(isolationSchemeGraph, isolationSchemeGraph.CloseButton);
                OkDialog(isolationSchemeForm, isolationSchemeForm.OkDialog);
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                return;
            }

            if (IsFullImportMode)
            {
                RestoreViewOnScreen(36);
                ClickChromatogram("Pit01", 70.19, 169.5E+06, PaneKey.PRECURSORS);

                PauseForGraphScreenShot("Full-Scan MS1 spectrum metafile", SkylineWindow.GraphFullScan);
                ClickChromatogram("Pit01", 70.79, 4.9E+05, PaneKey.PRODUCTS);

                PauseForGraphScreenShot("Full-Scan MS/MS spectrum y10 metafile", SkylineWindow.GraphFullScan);

                ClickChromatogram("Pit01", 70.79, 3.25E+05, PaneKey.PRODUCTS);

                PauseForGraphScreenShot("Full-Scan MS/MS spectrum y10++ metafile", SkylineWindow.GraphFullScan);

                FindNode("K.ELVYETVR.V [73, 80]");
                WaitForGraphs();
                ClickChromatogram("Pit02", 41.67, 4.076E+07, PaneKey.PRECURSORS);
                WaitForGraphs();
                RunUI(() => AssertEx.Contains(SkylineWindow.GraphFullScan.TitleText, "Pit02", (41.67).ToString(CultureInfo.CurrentCulture)));
                RunUI(() =>
                {
                    var graphControl = SkylineWindow.GraphFullScan.ZedGraphControl;
                    var scale = graphControl.GraphPane.XAxis.Scale;
                    scale.Min = 504;
                    scale.Max = 506;
                    graphControl.Invalidate();
                    SkylineWindow.GraphFullScan.Parent.Parent.Height -= 15;    // Not quite as tall to fit 3 into one page
                });
                PauseForGraphScreenShot("Full-Scan MS1 spectrum metafile (1/3)", SkylineWindow.GraphFullScan);

                MoveNextScan(41.68);
                PauseForGraphScreenShot("Full-Scan MS1 spectrum metafile (2/3)", SkylineWindow.GraphFullScan);

                MoveNextScan(41.7);
                PauseForGraphScreenShot("Full-Scan MS1 spectrum metafile (3/3)", SkylineWindow.GraphFullScan);
            }

            // Clear all the settings lists that were defined in this tutorial
            ClearSettingsLists();
        }

        private static void SetPeakAreasMaxes(double precursorMax, double productMax)
        {
            RunUI(() =>
            {
                var graphControl = SkylineWindow.GraphPeakArea.GraphControl;
                var panes = graphControl.MasterPane.PaneList;
                panes[0].YAxis.Scale.Max = precursorMax;
                panes[1].YAxis.Scale.Max = productMax;
                graphControl.Invalidate();
            });
        }

        private static void MoveNextScan(double rt)
        {
            string titleText = null;
            RunUI(() =>
            {
                titleText = SkylineWindow.GraphFullScan.TitleText;
                SkylineWindow.GraphFullScan.ChangeScan(1);
            });
            WaitForConditionUI(() => SkylineWindow.GraphFullScan.TitleText != titleText);
            RunUI(() => AssertEx.Contains(SkylineWindow.GraphFullScan.TitleText, "Pit02",
                rt.ToString(CultureInfo.CurrentCulture)));
        }

        private static void ValidateMassErrorStatistics(double mean, double stdev)
        {
            MassErrorHistogramGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
            WaitForConditionUI(() => !double.IsNaN(pane.Mean) && !double.IsNaN(pane.StdDev));
            Assert.AreEqual(mean, pane.Mean, 0.05);
            Assert.AreEqual(stdev, pane.StdDev, 0.05);
        }

        private static void ValidateNewTargetCounts(int proteinCount, int peptideCount, int precursorCount, int transitionCount)
        {
            Assert.AreEqual(6, proteinCount);
            Assert.AreEqual(21, peptideCount);
            Assert.AreEqual(25, precursorCount);
            Assert.AreEqual(225, transitionCount);
        }

        /// <summary>
        /// Clears all the relevant settings lists
        /// </summary>
        private static void ClearSettingsLists()
        {
            RunUI(() => {
                Settings.Default.PeakScoringModelList.Clear();
                Settings.Default.IsolationSchemeList.Clear();
                Settings.Default.IsolationSchemeList.AddDefaults();
                Settings.Default.HeavyModList.Clear();
                Settings.Default.StaticModList.Clear();
                Settings.Default.SpectralLibraryList.Clear();
            });
        }
    }
}
