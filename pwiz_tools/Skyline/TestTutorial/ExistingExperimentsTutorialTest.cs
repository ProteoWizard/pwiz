/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Existing Quantitative Experiments.
    /// </summary>
    [TestClass]
    public class ExistingExperimentsTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestExistingExperimentsTutorial()
        {
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            ForceMzml = false;

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/ExistingQuant-1_4.pdf";

            TestFilesZipPaths = new[]
                {
                    UseRawFiles
                        ? "https://skyline.gs.washington.edu/tutorials/ExistingQuant.zip"
                        : "https://skyline.gs.washington.edu/tutorials/ExistingQuantMzml.zip",
                    @"TestTutorial\ExistingExperimentsViews.zip"
                };
            RunFunctionalTest();
        }

        // Not L10N
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";
        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";

        private string GetTestPath(string relativePath)
        {
            var folderExistQuant = UseRawFiles ? "ExistingQuant" : "ExistingQuantMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(folderExistQuant + "\\" + relativePath);
        }

        private bool IsFullData { get { return IsPauseForScreenShots || IsDemoMode; } }

        protected override void DoTest()
        {
            DoMrmerTest();
            DoStudy7Test();
        }

        private void DoMrmerTest()
        {
            // Preparing a Document to Accept a Transition List, p. 2
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            RunDlg<EditLibraryDlg>(editListUI.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibrarySpec =
                    new BiblioSpecLibSpec("Yeast_mini", GetTestPath(@"MRMer\Yeast_MRMer_min.blib")); // Not L10N
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);
            RunUI(() =>
            {
                peptideSettingsUI.PickedLibraries = new[] { "Yeast_mini" }; // Not L10N
                peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Library;
            });
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab", 3);

            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Digest);
            RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg,
                buildBackgroundProteomeDlg =>
                {
                    buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast_mini"; // Not L10N
                    buildBackgroundProteomeDlg.BackgroundProteomePath = GetTestPath(@"MRMer\Yeast_MRMer_mini.protdb"); // Not L10N
                    buildBackgroundProteomeDlg.OkDialog();
                });
            PauseForScreenShot<PeptideSettingsUI.DigestionTab>("Peptide Settings - Digestion tab", 4);

            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification form", 5);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI, "Edit Isotope Modification form", 6);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForCondition(
                60 * 1000 * (AllowInternetAccess ? 6 : 3), // Protein metadata lookup can take longer
                () =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0
                && SkylineWindow.Document.Settings.HasBackgroundProteome
                && BackgroundProteomeManager.DocumentHasLoadedBackgroundProteomeOrNone(SkylineWindow.Document,true) // wait for protein metadata
                && SkylineWindow.IsGraphSpectrumVisible);
            WaitForDocumentLoaded();

            // Inserting a Transition List With Associated Proteins, p. 6
            RunUI(() =>
            {
                var filePath = GetTestPath(@"MRMer\silac_1_to_4.xls"); // Not L10N
                SetExcelFileClipboardText(filePath, "Fixed", 3, false); // Not L10N
            });
            using (new CheckDocumentState(24, 44, 88, 296))
            {
                var pasteDlg = ShowDialog<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUI(() => pasteDlg.IsMolecule = false); // Make sure it's ready to accept peptides rather than small molecules
                RunUI(pasteDlg.PasteTransitions);
                PauseForScreenShot<PasteDlg.TransitionListTab>("Insert Transition List form", 8);
                OkDialog(pasteDlg, pasteDlg.OkDialog);
            }
            PauseForScreenShot("Main window with transitions added", 9);

            FindNode("LWDVAT");
            RunUI(() =>
            {
                Assert.IsTrue(((PeptideDocNode)((PeptideTreeNode)SkylineWindow.SelectedNode).Model).HasChildType(IsotopeLabelType.heavy));
                SkylineWindow.ExpandPrecursors();
            });
            FindNode(string.Format("{0:F03}", 854.4)); // I18N
            WaitForGraphs();
            // Unfortunately, label hiding may mean that the selected ion is not labeled
            Assert.IsTrue(SkylineWindow.GraphSpectrum.SelectedIonLabel != null
                              ? SkylineWindow.GraphSpectrum.SelectedIonLabel.Contains("y7") // Not L10N
                              : SkylineWindow.GraphSpectrum.IonLabels.Contains(ionLabel => ionLabel.Contains("y7"))); // Not L10N
            RunUI(() =>
            {
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowCharge2 = true;
            });
            PauseForScreenShot<GraphSpectrum>("Main window with spectral library graph showing", 10);

            // Importing Data, p. 10.
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(@"MRMer\MRMer.sky"))); // Not L10N
            ImportResultsFile("silac_1_to_4.mzXML"); // Not L10N
            FindNode("ETFP"); // Not L10N
            RunUI(SkylineWindow.AutoZoomBestPeak);
            PauseForScreenShot("Main window with data imported", 12);

            RunUI(() =>
            {
                var selNode = SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsTrue(selNode.Text.Contains("ETFPILVEEK")); // Not L10N
                Assert.IsTrue(((PeptideDocNode)((PeptideTreeNode)selNode).Model).HasResults);
                Assert.IsTrue(Equals(selNode.StateImageIndex, (int)SequenceTree.StateImageId.keep));
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.SelectedPath =
                  SkylineWindow.SequenceTree.GetNodePath(selNode.Nodes[0].Nodes[2] as TreeNodeMS);
                Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.StateImageIndex,
                    (int)SequenceTree.StateImageId.no_peak));
            });
            PauseForScreenShot("Main window", 13);

            // Removing a Transition with Interference, p. 13.
            FindNode(string.Format("{0:F04}", 504.2664));   // I18N
            RunUI(() =>
            {
                TransitionTreeNode selNode = (TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode;
                SkylineWindow.RemovePeak(SkylineWindow.SequenceTree.SelectedPath.GetPathTo((int)SrmDocument.Level.TransitionGroups),
                ((TransitionGroupTreeNode)selNode.Parent).DocNode, selNode.DocNode);
                Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.StateImageIndex,
                    (int)SequenceTree.StateImageId.no_peak) && SkylineWindow.SequenceTree.SelectedNode.Parent.Text.Contains((0.24).ToString(LocalizationHelper.CurrentCulture)));
            });

            // Adjusting Peak Boundaries to Exclude Interference, p. 14.
            RunUI(() => SkylineWindow.Undo());
            FindNode("ETFP");
            RunUI(() =>
            {
                var pathGroup =
                SkylineWindow.SequenceTree.GetNodePath((TreeNodeMS)SkylineWindow.SelectedNode.Nodes[0]);
                var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(pathGroup, null, graphChrom.NameSet,
                                                    graphChrom.ChromGroupInfos[0].FilePath,
                                                    new ScaledRetentionTime(29.8, 29.8),
                                                    new ScaledRetentionTime(30.4, 30.4),
                                                    PeakIdentification.FALSE,
                                                    PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
                foreach (TransitionTreeNode node in SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes)
                {
                    Assert.IsTrue(((TransitionDocNode)node.Model).HasResults);
                }
            });
            PauseForScreenShot("Main window", 14);

            FindNode("YVDP");
            RunUI(() =>
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes[2].StateImageIndex
                    == (int)SequenceTree.StateImageId.peak_blank));
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void DoStudy7Test()
        {
            // Preparing a Document to Accept the Study 7 Transition List, p. 15
            RunUI(() => SkylineWindow.NewDocument());
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var mod13Cr = new StaticMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, false, null, LabelAtoms.C13,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(mod13Cr, peptideSettingsUI1, "Edit Isotope Modification form", 17);
            RunUI(() =>
                      {
                          peptideSettingsUI1.PickedHeavyMods = new[] { "Label:13C(6) (C-term R)", HEAVY_K };
                          peptideSettingsUI1.PickedLibraries = new[] { "" };
                          peptideSettingsUI1.SelectedBackgroundProteome = Resources.SettingsList_ELEMENT_NONE_None;
                          peptideSettingsUI1.OkDialog();
                      });

            // Pasting a Transition List into the Document, p. 18.
            string clipboardSaveText = string.Empty;
            RunUI(() =>
            {
                var filePath = GetTestPath(@"Study 7\Study7 transition list.xls");
                SetExcelFileClipboardText(filePath, "Simple", 6, false);
                clipboardSaveText = ClipboardEx.GetText();
            });

            {
                var messageDlg = ShowDialog<ImportTransitionListErrorDlg>(SkylineWindow.Paste);
                PauseForScreenShot<ImportTransitionListErrorDlg>("Error message form (expected)", 18);
                OkDialog(messageDlg, messageDlg.CancelDialog);

                // Restore the clipboard text after pausing
                ClipboardEx.SetText(clipboardSaveText);
            }

            RunDlg<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.Instrument), transitionSettingsUI =>
            {
                transitionSettingsUI.InstrumentMaxMz = 1800;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.Paste();
                SkylineWindow.CollapsePeptides();
            });
            PauseForScreenShot("Targets tree (selected from main window)", 19);

            // Adjusting Modifications Manually, p. 19.
            AdjustModifications("AGLCQTFVYGGCR", true, 'V', 747.348);
            PauseForScreenShot("Target tree clipped from main window", 22);

            AdjustModifications("IVGGWECEK", true, 'V', 541.763);
            AdjustModifications("YEVQGEVFTKPQLWP", false, 'L', 913.974);
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(@"Study 7\Study7.sky")));

            // Importing Data from a Multiple Sample WIFF file, p. 23.
            var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                                                                        importResultsDlg1.GetDataSourcePathsFile(null));
            RunUI(() =>
            {
                openDataSourceDialog1.CurrentDirectory = new MsDataFilePath(GetTestPath("Study 7"));
                openDataSourceDialog1.SelectAllFileType(ExtAbWiff);
            });
            if (UseRawFiles)
            {
                var importResultsSamplesDlg = ShowDialog<ImportResultsSamplesDlg>(openDataSourceDialog1.Open);
                PauseForScreenShot<ImportResultsSamplesDlg>("Choose Samples form", 23);

                RunUI(() =>
                          {
                              if (IsFullData)
                              {
                                  importResultsSamplesDlg.CheckAll(true);
                                  importResultsSamplesDlg.ExcludeSample(0); // Blank
                                  importResultsSamplesDlg.ExcludeSample(25); // QC
                                  importResultsSamplesDlg.ExcludeSample(26);
                                  importResultsSamplesDlg.ExcludeSample(27);
                                  importResultsSamplesDlg.ExcludeSample(28);
                                  importResultsSamplesDlg.ExcludeSample(45); // A2
                                  importResultsSamplesDlg.ExcludeSample(46);
                                  importResultsSamplesDlg.ExcludeSample(47);
                                  importResultsSamplesDlg.ExcludeSample(48);
                                  importResultsSamplesDlg.ExcludeSample(49); // gradientwash
                                  importResultsSamplesDlg.ExcludeSample(50);
                                  importResultsSamplesDlg.ExcludeSample(51);
                                  importResultsSamplesDlg.ExcludeSample(52);
                                  importResultsSamplesDlg.ExcludeSample(53); // A3
                                  importResultsSamplesDlg.ExcludeSample(54);
                                  importResultsSamplesDlg.ExcludeSample(55);
                                  importResultsSamplesDlg.ExcludeSample(56);
                              }
                              else
                              {
                                  importResultsSamplesDlg.CheckAll(false);
                                  importResultsSamplesDlg.IncludeSample(1);
                                  importResultsSamplesDlg.IncludeSample(2);
                                  importResultsSamplesDlg.IncludeSample(11);
                                  importResultsSamplesDlg.IncludeSample(12);
                              }
                          });
                OkDialog(importResultsSamplesDlg, importResultsSamplesDlg.OkDialog);
            }
            else
            {
                RunUI(openDataSourceDialog1.Open);
            }

            {
                var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg1.OkDialog);
                PauseForScreenShot<ImportDocResultsDlg>("Import Results Common prefix form", 24);

                OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
            }
            WaitForCondition(() =>
                SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RestoreViewOnScreen(25);

            // Inspecting and Adjusting Peak Integration, p. 24.
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowGraphRetentionTime(true);
            });

            if (!IsPauseForScreenShots)
                TestApplyToAll();

            PauseForScreenShot<GraphSummary.RTGraphView>("Main window with peaks and retention times showing", 25);
            CheckReportCompatibility.CheckAll(SkylineWindow.Document);
            RunUI(SkylineWindow.EditDelete);
            FindNode("IVGGWECEK"); // Not L10N

            TransitionGroupTreeNode selNodeGroup = null;
            RunUI(() =>
            {
                selNodeGroup = (TransitionGroupTreeNode)SkylineWindow.SequenceTree.SelectedNode.Nodes[1];
                Assert.AreEqual(selNodeGroup.StateImageIndex, (int)SequenceTree.StateImageId.peak_blank);
            });
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.MZMatchTolerance = 0.065;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() =>
                {
                    Assert.AreEqual((int)SequenceTree.StateImageId.peak, selNodeGroup.StateImageIndex);
                    SkylineWindow.ToggleIntegrateAll();
                });
            RunUI(() =>
            {
                foreach (PeptideDocNode nodePep in SkylineWindow.Document.Molecules)
                {
                    string sequence = nodePep.Peptide.Sequence;
                    int imageIndex = PeptideTreeNode.GetPeakImageIndex(nodePep, SkylineWindow.SequenceTree);
                    if ((sequence != null) && sequence.StartsWith("YLA")) // Not L10N
                    {
                        Assert.AreEqual((int)SequenceTree.StateImageId.keep, imageIndex,
                            string.Format("Expected keep icon for the peptide {0}, found {1}", sequence, imageIndex));
                    }
                    else if (sequence != null)
                    {
                        Assert.AreEqual((int)SequenceTree.StateImageId.peak, imageIndex,
                            string.Format("Expected peak icon for the peptide {0}, found {1}", sequence, imageIndex));
                    }
                    else // Custom Ion
                    {
                        Assert.AreEqual((int)SequenceTree.StateImageId.peak_blank, imageIndex,
                            string.Format("Expected peak_blank icon for the custom ion {0}, found {1}", nodePep.RawTextId, imageIndex));
                    }
                }
            });
            PauseForScreenShot("Main window", 27);

            // Data Inspection with Peak Areas View, p. 27.
            FindNode("SSDLVALSGGHTFGK"); // Not L10N
            RunUI(NormalizeGraphToHeavy);
            RestoreViewOnScreen(28);
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph metafile", 28);

            FindNode((564.7746).ToString(LocalizationHelper.CurrentCulture) + "++"); // ESDTSYVSLK - Not L10N
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph metafile", 29);

            RunUI(SkylineWindow.ExpandPeptides);
            string hgflprLight = (363.7059).ToString(LocalizationHelper.CurrentCulture) + "++";  // HGFLPR - Not L10N
            FindNode(hgflprLight);
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph metafile", 30);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view));
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph normalized metafile", 31);

            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            RunUI(() => SkylineWindow.ActivateReplicate("E_03"));
            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile with interference", 31);

            RunUI(() => SkylineWindow.ShowGraphPeakArea(true));
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowTotalTransitions();
                SkylineWindow.ShowCVValues(true);
                SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.document);
            });
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas Peptide Comparison graph metafile", 32);

            // Annotating replicates with concentration values
            var chooseAnnotationsDlg = ShowDialog<DocumentSettingsDlg>(SkylineWindow.ShowDocumentSettingsDialog);
            var editListDlg = ShowDialog<EditListDlg<SettingsListBase<AnnotationDef>, AnnotationDef>>(chooseAnnotationsDlg.EditAnnotationList);
            RunUI(editListDlg.ResetList);
            var defineAnnotationDlg = ShowDialog<DefineAnnotationDlg>(editListDlg.AddItem);
            RunUI(() =>
            {
                defineAnnotationDlg.AnnotationName = "Concentration";
                defineAnnotationDlg.AnnotationType = AnnotationDef.AnnotationType.number;
                defineAnnotationDlg.AnnotationTargets = AnnotationDef.AnnotationTargetSet.Singleton(AnnotationDef.AnnotationTarget.replicate);
            });
            PauseForScreenShot<DefineAnnotationDlg>("Define Annotation form", 33);
            OkDialog(defineAnnotationDlg, defineAnnotationDlg.OkDialog);
            OkDialog(editListDlg, () => editListDlg.DialogResult = DialogResult.OK);
            RunUI(() => chooseAnnotationsDlg.AnnotationsCheckedListBox.SetItemChecked(0, true));
            PauseForScreenShot<DocumentSettingsDlg>("Annotation Settings form", 34);
            OkDialog(chooseAnnotationsDlg, chooseAnnotationsDlg.OkDialog);
            RunUI(() => SkylineWindow.ShowResultsGrid(true));
            RunUI(() =>
                      {
                          SkylineWindow.SelectedPath =
                              SkylineWindow.DocumentUI.GetPathTo((int) SrmDocument.Level.MoleculeGroups, 0);
                      });
            WaitForGraphs();
            WaitForConditionUI(() => FindOpenForm<LiveResultsGrid>().IsComplete);
            RunUI(() =>
            {
                Settings.Default.ResultsGridSynchSelection = false;
                DataGridView resultsGrid;
                resultsGrid = FindOpenForm<LiveResultsGrid>().DataGridView;
                var colConcentration =
// ReSharper disable LocalizableElement
                    resultsGrid.Columns.Cast<DataGridViewColumn>().First(col => "Concentration" == col.HeaderText); // Not L10N
// ReSharper restore LocalizableElement
                if (IsFullData)
                {
                    float[] concentrations = { 0f, 60, 175, 513, 1500, 2760, 4980, 9060, 16500, 30000 };
                    for (int i = 0; i < concentrations.Length; i++)
                    {
                        for (int j = i * 4; j < (i + 1) * 4; j++)
                            SetCellValue(resultsGrid, j, colConcentration.Index, concentrations[i]);
                    }
                }
                else
                {
                    SetCellValue(resultsGrid, 0, colConcentration.Index, 0f);
                    SetCellValue(resultsGrid, 1, colConcentration.Index, 0f);
                    SetCellValue(resultsGrid, 2, colConcentration.Index, 175f);
                    SetCellValue(resultsGrid, 3, colConcentration.Index, 175f);
                }
            });
            WaitForGraphs();
            PauseForScreenShot<LiveResultsGrid>("Results grid with annotations (scrolled to the end)", 35);
            
            FindNode("SSDLVALSGGHTFGK"); // Not L10N
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.GroupByReplicateAnnotation("Concentration");
                NormalizeGraphToHeavy();
            });
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph with CVs metafile", 36);
            
            RunUI(() => SkylineWindow.ShowCVValues(false));
            RunUI(() => SkylineWindow.SaveDocument());
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas graph grouped by concentration metafile", 36);

            // Further Exploration, p. 33.
            RunUI(() =>
            {
                SkylineWindow.OpenFile(GetTestPath(@"Study 7\Study II\Study 7ii (site 52).sky")); // Not L10N
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowCVValues(true);
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas peptide comparison graph metafile", 37);
            FindNode("LSEPAELTDAVK");
            RunUI(SkylineWindow.ShowRTReplicateGraph);
            PauseForScreenShot<GraphSummary.RTGraphView>("Retention Times replicate graph metafile", 38);

            FindNode("INDISHTQSVSAK");
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas normalized to heave graph metafile", 38);

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none));
            WaitForGraphs();
            PauseForScreenShot<GraphSummary.AreaGraphView>("Peak Areas no normalization graph metafile", 39);

            FindNode(hgflprLight);
            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                NormalizeGraphToHeavy();
            });
            WaitForGraphs();
            PauseForScreenShot<GraphSummary.AreaGraphView>("Area Ratio to Heavy graph showing interference metafile", 40);

            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            RunUI(() => SkylineWindow.ActivateReplicate("E_ 03"));
            WaitForGraphs();
            PauseForScreenShot<GraphChromatogram>("Chromatogram graph metafile showing slight interference", 40);            
        }

        private static void NormalizeGraphToHeavy()
        {
            AreaGraphController.AreaView = AreaNormalizeToView.area_ratio_view;
            Settings.Default.AreaLogScale = false;
            SkylineWindow.UpdatePeakAreaGraph();
        }

        private void AdjustModifications(string peptideSeq, bool removeCTerminalMod, char aa13C, double expectedPrecursorMz)
        {
            FindNode(peptideSeq);
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            string sequence = string.Empty;
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                sequence = selNode.DocNode.Peptide.Sequence;
                if(removeCTerminalMod)
                    editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.Length - 1, string.Empty);
            });
            if (Settings.Default.HeavyModList.Contains(mod => Equals(mod.Name, "Label:13C"))) // Not L10N
                RunUI(() => editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.IndexOf(aa13C), "Label:13C")); // Not L10N
            else
            {
                var editStaticModDlg = ShowDialog<EditStaticModDlg>(() => editPepModsDlg.AddNewModification(sequence.IndexOf(aa13C), IsotopeLabelType.heavy));
                RunUI(() => editStaticModDlg.Modification = new StaticMod("Label:13C", null, null, LabelAtoms.C13)); // Not L10N
                PauseForScreenShot<EditStaticModDlg.IsotopeModView>("Edit Isotope Modification form", 20);

                OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
                PauseForScreenShot<EditPepModsDlg>("Edit Modifications form", 21);
            }
            var doc = SkylineWindow.Document;
            RunUI(editPepModsDlg.OkDialog);
            WaitForDocumentChange(doc);
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                DocNode[] choices = selNode.GetChoices(true).ToArray();
                Assert.IsTrue(choices.Contains(node =>
                    ((TransitionGroupDocNode)node).PrecursorMz.ToString(LocalizationHelper.CurrentCulture)
                        .Contains(expectedPrecursorMz.ToString(LocalizationHelper.CurrentCulture))));
                selNode.Pick(choices, false, false);
            });
        }

        private void SetCellValue(DataGridView dataGridView, int rowIndex, int columnIndex, object value)
        {
            dataGridView.CurrentCell = dataGridView.Rows[rowIndex].Cells[columnIndex];
            dataGridView.BeginEdit(true);
            dataGridView.CurrentCell.Value = value;
            dataGridView.EndEdit();
        }

        private static void TestApplyToAll()
        {
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 568.7817, "A_01", false, 20.2587);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(20.24220, 20.25878, 20.09352, 20.09353));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 564.7746, "A_02", false, 21.4320);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(21.49805, 21.43202, 21.26673, 21.3659));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 568.7817, "C_03", false, 18.0611);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(18.12708, 18.12713, 18.06105, 18.06107));
            });

            // For each test, a peak was picked and applied - undo two actions per test
            for (int i = 0; i < 2 * 3; i++)
                RunUI(() => SkylineWindow.Undo());
        }

        private static Dictionary<string, double> MakeVerificationDictionary(params double[] expected)
        {
            Assert.AreEqual(4, expected.Length);
            return new Dictionary<string, double>
            {
                {"A_01", expected[0]}, {"A_02", expected[1]}, {"C_03", expected[2]}, {"C_04", expected[3]}
            };
        }
    }
}
