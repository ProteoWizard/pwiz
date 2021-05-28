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

using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
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
        private readonly string[] _importFiles =
            {
                "20130311_DIA_Pit01",
                "20130311_DIA_Pit02"
            };

        private const string DIA_IMPORTED_CHECKPOINT = "DIA-tutorial-imported.sky";
        private const string DIA_TUTORIAL_CHECKPOINT = "DIA-tutorial.sky";

        /// <summary>
        /// Change to true to run full import of DIA data.  Also used
        /// to regenerate checkpoint files for non-full-import mode,
        /// when something changes in the test.
        /// </summary>
        private bool IsFullImportMode { get { return IsRecordImported || IsCoverShotMode || IsPauseForScreenShots; } }

        private bool IsRecordImported
        {
            get { return false; }
        }

        [TestMethod]
        public void TestDiaTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "DIA";
            PauseStartPage = 29;

            LinkPdf = "https://skyline.ms/tutorials/DIA-20_2.pdf";

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
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan", 5);

            // Set up isolation scheme
            var isolationSchemeDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);

            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme form", 6);
            
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
            PauseForScreenShot<CalculateIsolationSchemeDlg>("Calculate Isolation Scheme form", 9);
            OkDialog(calculateIsolationDlg, calculateIsolationDlg.OkDialog);
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog Filled", 10);

            var isolationSchemeGraphDlg = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeDlg.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Graph of Isolation Scheme", 11);
            OkDialog(isolationSchemeGraphDlg, isolationSchemeGraphDlg.CloseButton);
            const string isolationSchemeName = "500 to 900 by 20";
            RunUI(() => isolationSchemeDlg.IsolationSchemeName = isolationSchemeName);
            OkDialog(isolationSchemeDlg, isolationSchemeDlg.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // Export isolation scheme
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            RunUI(() => exportIsolationDlg.InstrumentType = ExportInstrumentType.THERMO_Q_EXACTIVE);
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form", 12);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(GetTestPath("DIA_tutorial_isolation_list.csv")));

            // Change library filtering
            // TODO: Necessary? Adjust library transition ranking
            var newTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                newTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Library;
                newTransitionSettings.UseLibraryPick = true;
                newTransitionSettings.Filtered = true;
            });
            OkDialog(newTransitionSettings, newTransitionSettings.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT)));

            // "Build Spectral Library" page
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() => SkylineWindow.ShowImportPeptideSearchDlg());
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Spectral Library page", 15);
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] {GetTestPath("interact-20130311_DDA_Pit01.pep.xml")}); // Not L10N
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            PauseForScreenShot<ImportPeptideSearchDlg.SpectraPage>("Spectral Library page - input files", 17);

            using (new WaitDocumentChange(1, true))
            {
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            }

                // "Extract Chromatograms" page
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.chromatograms_page);
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
                PauseForScreenShot("Browse for Results Files form", 18);
                OkDialog(openDataFiles, openDataFiles.Open);

                PauseForScreenShot<ImportPeptideSearchDlg.ChromatogramsDiaPage>("Extract Chromatograms page", 19);

                var importResultsNameDlg =
                    ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
                RunUI(() =>
                {
                    string prefix = importResultsNameDlg.Prefix;
                    Assert.IsTrue(prefix.EndsWith(prefixKeep));
                    importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - prefixKeep.Length);
                });
                PauseForScreenShot("Import Results names form", 20);
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
            PauseForScreenShot<ImportPeptideSearchDlg.TransitionSettingsPage>("Transition Settings page", 21);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
            WaitForConditionUI(() => importPeptideSearchDlg.CurrentPage ==
                                     ImportPeptideSearchDlg.Pages.full_scan_settings_page);

                // "Configure Full-Scan Settings" page
            PauseForScreenShot<ImportPeptideSearchDlg.Ms2FullScanPage>("Full-Scan Settings page", 23);
            RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));

                // "Import FASTA" page
                RunUI(() =>
                {
                    importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("pituitary_database.fasta"));
                });
            PauseForScreenShot<ImportPeptideSearchDlg.FastaPage>("Fasta page", 24);
            var peptidesPerProteinDlg = ShowDialog<PeptidesPerProteinDlg>(() => importPeptideSearchDlg.ClickNextButton());

                WaitForCondition(() => peptidesPerProteinDlg.DocumentFinalCalculated);
            PauseForScreenShot("Peptides per protein form", 25);
                RunUI(() =>
                {
                    int proteinCount, peptideCount, precursorCount, transitionCount;
                    peptidesPerProteinDlg.NewTargetsAll(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateNewTargetCounts(proteinCount, peptideCount, precursorCount, transitionCount);
                    peptidesPerProteinDlg.NewTargetsFinal(out proteinCount, out peptideCount, out precursorCount, out transitionCount);
                ValidateNewTargetCounts(proteinCount, peptideCount, precursorCount, transitionCount);
                });
                OkDialog(peptidesPerProteinDlg, peptidesPerProteinDlg.OkDialog);
                WaitForClosedForm(importPeptideSearchDlg);
            WaitForConditionUI(() => SkylineWindow.DocumentUI.PeptideGroupCount == 6);
            RunUI(() => SkylineWindow.ExpandPrecursors());
            if (IsPauseForScreenShots)
            {
                var allChrom = WaitForOpenForm<AllChromatogramsGraph>();
                RunUI(() =>
                {
                    SkylineWindow.Size = new Size(1330, 700);
                    allChrom.Left = SkylineWindow.Right + 20;
                });

                PauseForScreenShot<SequenceTreeForm>("Targets view clipped - scrolled left and before fully imported", 26);
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

            if (IsRecordImported)
                PauseTest("COPY IMPORTED DOCUMENT");

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
                SkylineWindow.Width = 1250;
            });

            RestoreViewOnScreen(27);
            PauseForScreenShot<GraphChromatogram>("Skyline window", 27);

            RunUI(() =>
            {
                SkylineWindow.ShowMassErrorHistogramGraph();
                SkylineWindow.ChangeMassErrorTransition(TransitionMassError.all);
            });
            WaitForGraphs();
            PauseForScreenShot("Mass Errors: Histogram metafile", 28);
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
            PauseForScreenShot<GraphChromatogram>("Skyline window - with manual integration and ID times", 29);

            SelectNode(SrmDocument.Level.TransitionGroups, 1);  // 2nd peptide

            PauseForScreenShot<GraphChromatogram>("Skyline window", 30);

            SelectNode(SrmDocument.Level.Transitions, 22);

            RunUI(() =>
            {
                SkylineWindow.ShowProductTransitions();
                SkylineWindow.Height = 463;
            });

            PauseForScreenShot<GraphChromatogram>("Product ion chromatogram graph metafiles", 31);

            PauseTest("End of Automation");



            RunUI(() =>
            {
                SkylineWindow.CollapsePeptides();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 4); // 5th peptide
                var nodePepTree = SkylineWindow.SelectedNode as PeptideTreeNode;
                Assert.IsNotNull(nodePepTree);
                Assert.AreEqual("VLQAVLPPLPQVVCTYR", nodePepTree.DocNode.Peptide.Sequence);
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.AutoZoomBestPeak();
                var graphChrom = SkylineWindow.GetGraphChrom(prefixKeep + "1");
                var labelStrings = graphChrom.GetAnnotationLabelStrings().ToArray();
                Assert.IsTrue(labelStrings.Contains(string.Format("{0}\n+{1} ppm", 75.4, 2.7)),
                    string.Format("Missing expected label in {0}", string.Join("|", labelStrings)));
                SkylineWindow.Width = 1250;
            });

            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.FontSize = GraphFontSize.NORMAL;
                dlg.OkDialog();
            });

            RunUI(() => SkylineWindow.SelectedNode.Expand()); 

            RunUI(() =>
            {
                var nodeTree = SkylineWindow.SelectedNode.Nodes[0].Nodes[0] as SrmTreeNode;
                Assert.IsNotNull(nodeTree);
                var expectedImageId = IsFullImportMode ? SequenceTree.StateImageId.peak : SequenceTree.StateImageId.no_peak;
                Assert.AreEqual((int) expectedImageId, nodeTree.StateImageIndex);
            });

            PauseForScreenShot<SequenceTreeForm>("Targets view - ", 27);

            RunUI(() =>
            {
                SkylineWindow.SetIntegrateAll(true);
                var nodeTree = SkylineWindow.SelectedNode.Nodes[0].Nodes[0] as SrmTreeNode;
                Assert.IsNotNull(nodeTree);
                Assert.AreEqual((int) SequenceTree.StateImageId.peak, nodeTree.StateImageIndex);
                var nodeGroupTree = SkylineWindow.SelectedNode.Nodes[0] as TransitionGroupTreeNode;
                Assert.IsNotNull(nodeGroupTree);
                Assert.AreEqual(0.99, nodeGroupTree.DocNode.GetIsotopeDotProduct(0) ?? 0, 0.005);
                Assert.AreEqual(0.83, nodeGroupTree.DocNode.GetLibraryDotProduct(0) ?? 0, 0.005);
                SkylineWindow.ShowOtherRunPeptideIDTimes(true);
            });

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile - with ID lines", 28);

            RunUI(() =>
            {
                SkylineWindow.AutoZoomNone();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 1);
            });

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile - zoomed out and small peak", 30);

            RunUI(SkylineWindow.AutoZoomBestPeak);

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile - zoomed to peak", 31);
            if (IsFullImportMode)
            {
                ClickChromatogram(74.9, 1.775E+7, PaneKey.PRECURSORS);
                RestoreViewOnScreen(33);
                PauseForScreenShot<GraphFullScan>("Full Scan graph with precursors - zoom manually", 32);

                ClickChromatogram(74.8, 1.753E+6, PaneKey.PRODUCTS);
                PauseForScreenShot<GraphFullScan>("Full Scan graph showing y7", 33);

                ClickChromatogram(74.9, 9.64E+5, PaneKey.PRODUCTS);
                PauseForScreenShot<GraphFullScan>("Full Scan graph showing b3 - zoom manually", 34);

                ClickChromatogram(74.9, 1.25E+5, PaneKey.PRODUCTS);
                PauseForScreenShot<GraphFullScan>("Full Scan graph showing y3 - zoom manually", 34);
            }

            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 2);
                Assert.AreEqual("CNTDYSDCIHEAIK", ((PeptideTreeNode)SkylineWindow.SelectedNode).DocNode.Peptide.Sequence);
            });

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile - split between two precursors", 35);

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                });

                RestoreCoverViewOnScreen();
//                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                WaitForGraphs();
                ClickChromatogram("DIA_Pit01", 75.3468, 104968093, PaneKey.PRODUCTS);
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
                TakeCoverShot();
                OkDialog(isolationSchemeGraph, isolationSchemeGraph.CloseButton);
                OkDialog(isolationSchemeForm, isolationSchemeForm.OkDialog);
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);
                return;
            }

            RunUI(() =>
            {
                SkylineWindow.SelectedNode.Expand();
                SkylineWindow.SelectedPath = ((SrmTreeNode) SkylineWindow.SelectedNode.Nodes[0]).Path;
            });

            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile - double charged precursor", 36);

            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 3);
                Assert.AreEqual("ELVYETVR", ((PeptideTreeNode)SkylineWindow.SelectedNode).DocNode.Peptide.Sequence);
            });

            RunUI(() =>
            {
                SkylineWindow.SelectedNode.Expand();
                var nodeGroupTree = SkylineWindow.SelectedNode.Nodes[0] as TransitionGroupTreeNode;
                Assert.IsNotNull(nodeGroupTree);
                Assert.AreEqual(0.99, nodeGroupTree.DocNode.GetIsotopeDotProduct(0) ?? 0, 0.005);
                Assert.AreEqual(0.99, nodeGroupTree.DocNode.GetIsotopeDotProduct(0) ?? 0, 0.005);
            });

            RestoreViewOnScreen(38);
            PauseForScreenShot<GraphSpectrum>("Library Match view - zoom manually", 37);

            RestoreViewOnScreen(39);
            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile", 38);

            if (IsFullImportMode)
            {
                RestoreViewOnScreen(40);
                ClickChromatogram(41.9, 1.166E+8, PaneKey.PRECURSORS);
                PauseForScreenShot<GraphFullScan>("Full Scan graph showing precursor interference - zoom manually", 39);

                RunUI(() => SkylineWindow.GraphFullScan.ChangeScan(-12));
                CheckFullScanSelection(41.7, 1.532E+8, PaneKey.PRECURSORS);
                PauseForScreenShot<GraphFullScan>("Full Scan graph showing transition between interference and real peak - zoom manually", 39);
            }

            // Clear all the settings lists that were defined in this tutorial
            ClearSettingsLists();
        }

        private static void ValidateMassErrorStatistics(double mean, double stdev)
        {
            MassErrorHistogramGraphPane pane;
            Assert.IsTrue(SkylineWindow.GraphMassError.TryGetGraphPane(out pane));
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
