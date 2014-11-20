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

using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
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

            // Build spectral library
            var peptideSettings = ShowDialog<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI());
            RunUI(() => peptideSettings.SelectedTab = PeptideSettingsUI.TABS.Library);
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(peptideSettings.ShowBuildLibraryDlg);
            PauseForScreenShot<BuildLibraryDlg>("Build Library form", 3);
            RunUI(() =>
            {
                buildLibraryDlg.LibraryName = "Dia_Tutorial_Library"; // Not L10N
                buildLibraryDlg.LibraryAuthority = "proteome.gs.washington.edu"; // Not L10N
                buildLibraryDlg.LibraryPath = GetTestPath("Dia_Tutorial_Library.blib");
                buildLibraryDlg.OkWizardPage();
            });
            PauseForScreenShot<BuildLibraryDlg>("Build Library form - input files", 4);
            RunUI(() =>
            {
                var ddaFiles = new[]
                {
                    "interact-20130311_DDA_Pit01.pep.xml", // Not L10N
                };
                var ddaFilesFull = ddaFiles.Select(GetTestPath);
                buildLibraryDlg.AddInputFiles(ddaFilesFull);
            });
            if (IsFullImportMode)
            {
                RunUI(buildLibraryDlg.OkWizardPage);

                WaitForConditionUI(() => peptideSettings.AvailableLibraries.Length > 0);
                RunUI(() =>
                {
                    peptideSettings.PickedLibraries = peptideSettings.AvailableLibraries;
                    Assert.AreEqual(peptideSettings.PickedLibraries.Length, 1);
                    Assert.AreEqual(peptideSettings.PickedLibraries[0], "Dia_Tutorial_Library"); // Not L10N
                });
                PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings form", 6);
                OkDialog(peptideSettings, peptideSettings.OkDialog);
                WaitForConditionUI(() => SkylineWindow.Document.Settings.PeptideSettings.Libraries.IsLoaded);
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetTestPath(DIA_SETUP_CHECKPOINT));
                    SkylineWindow.SaveDocument(GetTestPath(DIA_START_CHECKPOINT));
                });
            }
            else
            {
                OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);
                OkDialog(peptideSettings, peptideSettings.OkDialog);
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_SETUP_CHECKPOINT)));
                var peptideSettingsLib = ShowDialog<PeptideSettingsUI>(() => SkylineWindow.ShowPeptideSettingsUI());

                PauseForScreenShot("Peptide Settings - Library tab", 6);
                OkDialog(peptideSettingsLib, peptideSettingsLib.OkDialog);
            }
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
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Transition Settings - Full-Scan", 8);

            // Set up isolation scheme
            var isolationSchemeDlg = ShowDialog<EditIsolationSchemeDlg>(transitionSettings.AddIsolationScheme);

            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme form", 9);
            
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
            PauseForScreenShot<CalculateIsolationSchemeDlg>("Calculate Isolation Scheme form", 10);
            OkDialog(calculateIsolationDlg, calculateIsolationDlg.OkDialog);
            PauseForScreenShot<EditIsolationSchemeDlg>("Edit Isolation Scheme Dialog Filled", 11);

            var isolationSchemeGraphDlg = ShowDialog<DiaIsolationWindowsGraphForm>(isolationSchemeDlg.OpenGraph);
            PauseForScreenShot<DiaIsolationWindowsGraphForm>("Graph of Isolation Scheme", 12);
            OkDialog(isolationSchemeGraphDlg, isolationSchemeGraphDlg.CloseButton);
            RunUI(() => isolationSchemeDlg.IsolationSchemeName = "DIA tutorial isolation"); // Not L10N
            OkDialog(isolationSchemeDlg, isolationSchemeDlg.OkDialog);
            OkDialog(transitionSettings, transitionSettings.OkDialog);
            
            // Export isolation scheme
            var exportIsolationDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.IsolationList));
            PauseForScreenShot<ExportMethodDlg>("Export Isolation List form", 13);
            OkDialog(exportIsolationDlg, () => exportIsolationDlg.OkDialog(GetTestPath("DIA_tutorial_isolation_list.csv")));
            
            // Set up chromatogram retention time restriction
            var newTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => newTransitionSettings.SetRetentionTimeFilter(RetentionTimeFilterType.ms2_ids, 5.0));
            PauseForScreenShot<TransitionSettingsUI.FullScanTab>("Retention time filtering options", 15);
            OkDialog(newTransitionSettings, newTransitionSettings.OkDialog);

            // Adjust modifications and filter
            var newPeptideSettings = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() =>
            {
                newPeptideSettings.AutoSelectMatchingPeptides = true;
                newPeptideSettings.SelectedTab = PeptideSettingsUI.TABS.Modifications;
            });
            var editModificationsDlg = ShowDialog<EditListDlg<SettingsListBase<StaticMod>, StaticMod>>(newPeptideSettings.EditStaticMods);
            var addModificationDlg = ShowDialog<EditStaticModDlg>(editModificationsDlg.AddItem);
            const string carbamidoMod = StaticModList.DEFAULT_NAME;
            RunUI(() => addModificationDlg.SetModification(carbamidoMod)); // Not L10N
            PauseForScreenShot<EditStaticModDlg>("Add fixed modification", 17);
            OkDialog(addModificationDlg, addModificationDlg.OkDialog);
            OkDialog(editModificationsDlg, editModificationsDlg.OkDialog);
            RunUI(() =>
            {
                newPeptideSettings.PickedStaticMods = new[] {carbamidoMod};
            });
            PauseForScreenShot<PeptideSettingsUI.ModificationsTab>("Edit Structural Modification form", 18);
            OkDialog(newPeptideSettings, newPeptideSettings.OkDialog);

            var filterTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                filterTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Filter;
                filterTransitionSettings.PrecursorCharges = "1, 2, 3, 4"; // Not L10N
                filterTransitionSettings.ProductCharges = "1, 2"; // Not L10N
                filterTransitionSettings.FragmentTypes = "y, b, p"; // Not L10N
                filterTransitionSettings.SetAutoSelect = true;
                filterTransitionSettings.SetDIAExclusionWindow = true;
            });
            PauseForScreenShot<TransitionSettingsUI.FilterTab>("Transition Settings - Filter tab", 19);
            OkDialog(filterTransitionSettings, filterTransitionSettings.OkDialog);

            // Import .fasta file with 30 peptides
            RunUI(() =>
            {
                SkylineWindow.ImportFastaFile(GetTestPath("pituitary_database.fasta"));
                SkylineWindow.ExpandPrecursors();
                SkylineWindow.Size = new Size(750, 788);
            });
            RestoreViewOnScreen(21);
            PauseForScreenShot("Targets pane with large transition lists", 21);

            // Adjust library transition ranking
            var libraryTransitionSettings = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                libraryTransitionSettings.SelectedTab = TransitionSettingsUI.TABS.Library;
                libraryTransitionSettings.UseLibraryPick = true;
                libraryTransitionSettings.IonCount = 5;
                libraryTransitionSettings.Filtered = true;
                libraryTransitionSettings.IonMatchTolerance = 0.05;
            });
            PauseForScreenShot<TransitionSettingsUI.LibraryTab>("Transition Settings - Library tab", 22);
            OkDialog(libraryTransitionSettings, libraryTransitionSettings.OkDialog);
            PauseForScreenShot("Targets pane with precursors and best 5 transitions only", 23);

            // Generate decoys
            var decoysDlg = ShowDialog<GenerateDecoysDlg>(SkylineWindow.ShowGenerateDecoysDlg);
            PauseForScreenShot<GenerateDecoysDlg>("Add Decoy Peptides form", 24);
            RunUI(() =>
            {
                decoysDlg.NumDecoys = 26;
                Assert.AreEqual(decoysDlg.DecoysMethod, DecoyGeneration.SHUFFLE_SEQUENCE);
            });
            OkDialog(decoysDlg, decoysDlg.OkDialog);
            RunUI(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.SelectedNode.PrevNode.Nodes[6]);
            PauseForScreenShot("Targets pane with decoys added", 25);

            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT)));

            // Import mass spec data
            const string prefixKeep = "DIA_Pit0";
            if (IsFullImportMode)
            {
                // Import the raw data
                var importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(() =>
                {
                    importResultsDlg.RadioAddNewChecked = true;
                    var path = new KeyValuePair<string, MsDataFileUri[]>[2];
                    for (int i = 0; i < 2; ++i)
                    {
                        path[i] = new KeyValuePair<string, MsDataFileUri[]>(_importFiles[i],
                                                new[] { MsDataFileUri.Parse(GetTestPath(_importFiles[i] + ExtensionTestContext.ExtThermoRaw)) });
                    }

                    importResultsDlg.NamedPathSets = path;
                });
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg.OkDialog);
                PauseForScreenShot<ImportResultsNameDlg>("Import Results - Common prefix", 26);
                RunUI(() =>
                {
                    string prefix = importResultsNameDlg.Prefix;
                    Assert.IsTrue(prefix.EndsWith(prefixKeep));
                    importResultsNameDlg.Prefix = prefix.Substring(0, prefix.Length - prefixKeep.Length);
                    importResultsNameDlg.YesDialog();
                });
                WaitForClosedForm(importResultsNameDlg);
                WaitForClosedForm(importResultsDlg);
                WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 10 minutes
                RunUI(() =>
                {
                    SkylineWindow.SaveDocument(GetTestPath(DIA_IMPORTED_CHECKPOINT));
                    SkylineWindow.SaveDocument(GetTestPath(DIA_TUTORIAL_CHECKPOINT));
                });
            }
            else
            {
                RunUI(() => SkylineWindow.OpenFile(GetTestPath(DIA_IMPORTED_CHECKPOINT)));
            }
            WaitForDocumentLoaded();
            WaitForGraphs();

            RunUI(() =>
            {
                SkylineWindow.CollapsePeptides();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Peptides, 6);
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
                dlg.FontSize = 12;
                dlg.OkDialog();
            });

            RestoreViewOnScreen(27);
            PauseForScreenShot("Chromatogram graph metafile", 27);

            RunUI(() =>
            {
                SkylineWindow.SelectedNode.Expand();
                var nodeTree = SkylineWindow.SelectedNode.Nodes[0].Nodes[0] as SrmTreeNode;
                Assert.IsNotNull(nodeTree);
                Assert.AreEqual((int) SequenceTree.StateImageId.no_peak, nodeTree.StateImageIndex);
            });

            PauseForScreenShot("Targets view - ", 28);

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

            PauseForScreenShot("Chromatogram graph metafile - with ID lines", 29);

            RunUI(() =>
            {
                SkylineWindow.AutoZoomNone();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Peptides, 1);
            });

            PauseForScreenShot("Chromatogram graph metafile - zoomed out and small peak", 30);

            RunUI(SkylineWindow.AutoZoomBestPeak);

            PauseForScreenShot("Chromatogram graph metafile - zoomed to peak", 32);
            if (IsFullImportMode)
            {
                ClickChromatogram(74.9, 1.775E+7, PaneKey.PRECURSORS);
                RestoreViewOnScreen(33);
                PauseForScreenShot("Full Scan graph with precursors - zoom manually");

                ClickChromatogram(74.8, 1.753E+6, PaneKey.PRODUCTS);
                PauseForScreenShot("Full Scan graph showing y7");

                ClickChromatogram(74.9, 9.64E+5, PaneKey.PRODUCTS);
                PauseForScreenShot("Full Scan graph showing b3 - zoom manually");

                ClickChromatogram(74.9, 1.25E+5, PaneKey.PRODUCTS);
                PauseForScreenShot("Full Scan graph showing y3 - zoom manually");
            }

            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Peptides, 2);
                Assert.AreEqual("CNTDYSDCIHEAIK", ((PeptideTreeNode)SkylineWindow.SelectedNode).DocNode.Peptide.Sequence);
            });

            PauseForScreenShot("Chromatogram graph metafile - split between two precursors", 36);

            RunUI(() =>
            {
                SkylineWindow.SelectedNode.Expand();
                SkylineWindow.SelectedPath = ((SrmTreeNode) SkylineWindow.SelectedNode.Nodes[0]).Path;
            });

            PauseForScreenShot("Chromatogram graph metafile - double charged precursor", 37);

            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Peptides, 3);
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
            PauseForScreenShot("Library Match view - zoom manually", 38);

            RestoreViewOnScreen(39);
            PauseForScreenShot("Chromatogram graph metafile", 39);

            if (IsFullImportMode)
            {
                RestoreViewOnScreen(40);
                ClickChromatogram(41.9, 1.166E+8, PaneKey.PRECURSORS);
                PauseForScreenShot("Full Scan graph showing precursor interference - zoom manually", 40);

                RunUI(() => SkylineWindow.GraphFullScan.ChangeScan(-12));
                CheckFullScanSelection(41.7, 1.532E+8, PaneKey.PRECURSORS);
                PauseForScreenShot("Full Scan graph showing transition between interference and real peak - zoom manually", 40);
            }

            // Clear all the settings lists that were defined in this tutorial
            ClearSettingsLists();
        }

        /// <summary>
        /// Clears all the relevant settings lists
        /// </summary>
        private static void ClearSettingsLists()
        {
            Settings.Default.PeakScoringModelList.Clear();
            Settings.Default.IsolationSchemeList.Clear();
            Settings.Default.HeavyModList.Clear();
            Settings.Default.StaticModList.Clear();
            Settings.Default.SpectralLibraryList.Clear();
        }
    }
}
