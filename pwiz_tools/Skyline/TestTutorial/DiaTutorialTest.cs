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
using Microsoft.VisualStudio.TestTools.UnitTesting;
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

        private const string DIA_START_CHECKPOINT = "DIABlank.sky";
        private const string DIA_SETUP_CHECKPOINT = "DIASetup.sky";
        private const string DIA_IMPORTED_CHECKPOINT = "DIAImported.sky";
        private const string DIA_TUTORIAL_CHECKPOINT = "DIATutorial.sky";

        /// <summary>
        /// Change to true to run full import of DIA data.  Also used
        /// to regenerate checkpoint files for non-full-import mode,
        /// when something changes in the test.
        /// </summary>
        private bool IsFullImportMode { get { return false; } }

        [TestMethod]
        public void TestDiaTutorial()
        {
            // Set true to look at tutorial screenshots.
            // IsPauseForScreenShots = true;

            LinkPdf = "https://skyline.gs.washington.edu/tutorials/DIA-2_6.pdf";

            TestFilesZipPaths = new[]
                {
                    // There is a small and a large version of the tutorial data.
                    // Large version contains 1 DDA and 2 DIA runs for 6 GB.
                    // Small version runs by default, skips some steps.
                    IsFullImportMode ? @"https://skyline.gs.washington.edu/tutorials/DIA.zip" :
                                       @"https://skyline.gs.washington.edu/tutorials/DIASmall.zip",
                    @"TestTutorial\DiaViews.zip"
                };
            RunFunctionalTest();
        }

        private string GetTestPath(string relativePath)
        {
            string folderTutorial = IsFullImportMode ? "Dia" : "DiaSmall" ; // Not L10N
            return TestFilesDirs[0].GetTestPath(Path.Combine(folderTutorial, relativePath));
        }

        protected override void DoTest()
        {
            // Clear all the settings lists that will be defined in this tutorial
            ClearSettingsLists();

            // Open the file
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_START_CHECKPOINT))); 
            WaitForDocumentLoaded();

            // Specify DIA acquisition scheme and machine settings
            var transitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettings.SelectedTab = TransitionSettingsUI.TABS.FullScan;
                transitionSettings.AcquisitionMethod = FullScanAcquisitionMethod.DIA;
                transitionSettings.PrecursorIsotopesCurrent = FullScanPrecursorIsotopes.Count;
                transitionSettings.PrecursorMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                transitionSettings.PrecursorRes = 35000;
                transitionSettings.PrecursorResMz = 200;
                transitionSettings.ProductMassAnalyzer = FullScanMassAnalyzerType.orbitrap;
                transitionSettings.ProductRes = 17500;
                transitionSettings.ProductResMz = 200;
            });
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan", 4);

            // Set up isolation scheme
            var isolationSchemeDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);

            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme form", 5);
            
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
            PauseForScreenShot<CalculateIsolationSchemeDlg>("Calculate Isolation Scheme form", 6);
            OkDialog(calculateIsolationDlg, calculateIsolationDlg.OkDialog);
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog Filled", 7);

            var isolationSchemeGraphDlg = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeDlg.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Graph of Isolation Scheme", 8);
            OkDialog(isolationSchemeGraphDlg, isolationSchemeGraphDlg.CloseButton);
            RunUI(() => isolationSchemeDlg.IsolationSchemeName = "500 to 900 by 20");
            OkDialog(isolationSchemeDlg, isolationSchemeDlg.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // Export isolation scheme
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form", 9);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(GetTestPath("DIA_tutorial_isolation_list.csv")));

            // Adjust modifications and filter
            var newPeptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => newPeptideSettings.AutoSelectMatchingPeptides = true);
            OkDialog(newPeptideSettings, newPeptideSettings.OkDialog);

            // Set up chromatogram retention time restriction
            var newTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => newTransitionSettings.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 5.0));
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Retention time filtering options", 16);

            // Adjust library transition ranking
            RunUI(() =>
            {
                newTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Library;
                newTransitionSettings.UseLibraryPick = true;
                newTransitionSettings.Filtered = true;
            });
            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab", 22);
            OkDialog(newTransitionSettings, newTransitionSettings.OkDialog);
            PauseForScreenShot<SequenceTreeForm>("Targets pane with precursors and best 5 transitions only", 23);

            // Build spectral library using Import Peptide Search
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT)));

            // "Build Spectral Library" page
            var importPeptideSearchDlg = ShowDialog<ImportPeptideSearchDlg>(() => SkylineWindow.ShowImportPeptideSearchDlg());
            RunUI(() =>
            {
                Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.spectra_page);
                importPeptideSearchDlg.BuildPepSearchLibControl.AddSearchFiles(new[] {GetTestPath("interact-20130311_DDA_Pit01.pep.xml")}); // Not L10N
                importPeptideSearchDlg.BuildPepSearchLibControl.WorkflowType = ImportPeptideSearchDlg.Workflow.dia;
            });
            PauseForScreenShot<BuildLibraryDlg>("Build Library form - input files", 10);

            const string prefixKeep = "DIA_Pit0";
            if (IsFullImportMode)
            {
                SrmDocument doc = SkylineWindow.Document;
                RunUI(() => Assert.IsTrue(importPeptideSearchDlg.ClickNextButton()));
                doc = WaitForDocumentChange(doc);

                // "Extract Chromatograms" page
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.chromatograms_page);
                    importPeptideSearchDlg.ImportResultsControl.FoundResultsFiles =
                        _importFiles.Select(f => new ImportPeptideSearch.FoundResultsFile(f, GetTestPath(f + ExtensionTestContext.ExtThermoRaw))).ToList();
                });
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importPeptideSearchDlg.ClickNextButtonNoCheck);
                RunUI(() =>
                {
                    string prefix = importResultsNameDlg.Prefix;
                    Assert.IsTrue(prefix.EndsWith(prefixKeep));
                    importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - prefixKeep.Length);
                    importResultsNameDlg.YesDialog();
                });
                WaitForClosedForm(importResultsNameDlg);

                // "Add Modifications" page
                RunUI(() =>
                {
                    const string modCarbamidomethyl = "Carbamidomethyl (C)";
                    const string modOxidation = "Oxidation (M)";
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.match_modifications_page);
                    // Define expected matched/unmatched modifications
                    var expectedMatched = new[] {modCarbamidomethyl, modOxidation};
                    // Verify matched/unmatched modifications
                    AssertEx.AreEqualDeep(expectedMatched, importPeptideSearchDlg.MatchModificationsControl.MatchedModifications.ToArray());
                    Assert.IsFalse(importPeptideSearchDlg.MatchModificationsControl.UnmatchedModifications.Any());
                    importPeptideSearchDlg.MatchModificationsControl.CheckedModifications = new[] { modCarbamidomethyl };
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });
                WaitForDocumentChange(doc);
                
                // "Configure Transition Settings" page
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.transition_settings_page);
                    importPeptideSearchDlg.TransitionSettingsControl.PrecursorCharges = new[] {1, 2, 3, 4};
                    importPeptideSearchDlg.TransitionSettingsControl.IonCharges = new[] {1, 2};
                    importPeptideSearchDlg.TransitionSettingsControl.IonTypes = new[] { IonType.y, IonType.b, IonType.precursor };
                    importPeptideSearchDlg.TransitionSettingsControl.ExclusionUseDIAWindow = true;
                    importPeptideSearchDlg.TransitionSettingsControl.IonCount = 5;
                    importPeptideSearchDlg.TransitionSettingsControl.IonMatchTolerance = 0.05;
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });

                // "Configure Full-Scan Settings" page
                RunUI(() =>
                {
                    Assert.IsTrue(importPeptideSearchDlg.CurrentPage == ImportPeptideSearchDlg.Pages.full_scan_settings_page);
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });

                // "Import FASTA" page
                RunUI(() =>
                {
                    importPeptideSearchDlg.ImportFastaControl.SetFastaContent(GetTestPath("pituitary_database.fasta"));
                    Assert.IsTrue(importPeptideSearchDlg.ClickNextButton());
                });

                WaitForClosedForm(importPeptideSearchDlg);
                WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 10 minutes

                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetTestPath(DIA_IMPORTED_CHECKPOINT));
                    SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT));
                });
            }
            else
            {
                OkDialog(importPeptideSearchDlg, importPeptideSearchDlg.CancelDialog);
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_IMPORTED_CHECKPOINT)));
            }
            WaitForDocumentLoaded();
            WaitForGraphs();

            RunUI(() =>
            {
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.Size = new Size(750, 788);
            });

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

            RunUI(() =>
            {
                SkylineWindow.CollapsePeptides();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Molecules, 6);
                var nodePepTree = SkylineWindow.SelectedNode as PeptideTreeNode;
                Assert.IsNotNull(nodePepTree);
                Assert.AreEqual("VLQAVLPPLPQVVCTYR", nodePepTree.DocNode.Peptide.Sequence);
                SkylineWindow.ShowSplitChromatogramGraph(true);
                SkylineWindow.AutoZoomBestPeak();
                var graphChrom = SkylineWindow.GetGraphChrom(prefixKeep + "1");
                var labelStrings = graphChrom.GetAnnotationLabelStrings().ToArray();
                Assert.IsTrue(labelStrings.Contains(string.Format("{0}\n+{1} ppm\n(idotp {2})", 75.4, 3, 0.59)),
                    string.Format("Missing expected label in {0}", string.Join("|", labelStrings)));
                SkylineWindow.Width = 1250;
            });

            RunDlg<ChromChartPropertyDlg>(SkylineWindow.ShowChromatogramProperties, dlg =>
            {
                dlg.FontSize = GraphFontSize.NORMAL;
                dlg.OkDialog();
            });

            RestoreViewOnScreen(27);
            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile", 26);

            RunUI(() =>
            {
                SkylineWindow.SelectedNode.Expand();
                var nodeTree = SkylineWindow.SelectedNode.Nodes[0].Nodes[0] as SrmTreeNode;
                Assert.IsNotNull(nodeTree);
                Assert.AreEqual((int) SequenceTree.StateImageId.no_peak, nodeTree.StateImageIndex);
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

        /// <summary>
        /// Clears all the relevant settings lists
        /// </summary>
        private static void ClearSettingsLists()
        {
            RunUI(() => {
                Settings.Default.PeakScoringModelList.Clear();
                Settings.Default.IsolationSchemeList.Clear();
                Settings.Default.HeavyModList.Clear();
                Settings.Default.StaticModList.Clear();
                Settings.Default.SpectralLibraryList.Clear();
            });
        }
    }
}
