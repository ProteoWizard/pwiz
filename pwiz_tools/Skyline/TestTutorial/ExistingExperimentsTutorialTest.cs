﻿/*
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
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.CommonMsData;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SampleType = pwiz.Skyline.Model.DocSettings.AbsoluteQuantification.SampleType;

namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Existing Quantitative Experiments.
    /// </summary>
    [TestClass]
    public class ExistingExperimentsTutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod,
         NoLeakTesting(TestExclusionReason.EXCESSIVE_TIME)] // Don't leak test this - it takes a long time to run even once
        public void TestExistingExperimentsTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
//            IsPauseForAuditLog = true;
            CoverShotName = "ExistingQuant";

            ForceMzml = false;

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/ExistingQuant-21_2.pdf";

            TestFilesZipPaths = new[]
                {
                    UseRawFilesOrFullData
                        ? "https://skyline.ms/tutorials/ExistingQuant.zip"
                        : "https://skyline.ms/tutorials/ExistingQuantMzml.zip",
                    @"TestTutorial\ExistingExperimentsViews.zip"
                };
            RunFunctionalTest();
        }

        protected override Bitmap ProcessCoverShot(Bitmap bmp)
        {
            using var excelBmp = new Bitmap(TestContext.GetProjectDirectory(@"TestTutorial\ExistingQuant_excel.png"));
            using var graph = Graphics.FromImage(base.ProcessCoverShot(bmp));
            graph.DrawImageUnscaled(excelBmp, bmp.Width - excelBmp.Width, bmp.Height - excelBmp.Height);
            return bmp;
        }

        // Not L10N
        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";
        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";

        private string GetTestPath(string relativePath)
        {
            var folderExistQuant = UseRawFilesOrFullData ? "ExistingQuant" : "ExistingQuantMzml"; // Not L10N
            return TestFilesDirs[0].GetTestPath(folderExistQuant + "\\" + relativePath);
        }

        protected override bool UseRawFiles
        {
            get { return !ForceMzml && ExtensionTestContext.CanImportThermoRaw && ExtensionTestContext.CanImportAbWiff; }
        }

        private bool UseRawFilesOrFullData
        {
            get { return UseRawFiles || IsFullData; }
        }

        protected override void DoTest()
        {
            if (!IsCoverShotMode)
                DoMrmerTest();
            DoStudy7Test();
        }

        private void DoMrmerTest()
        {
            if (!IsPauseForScreenShots)
                AllowInternetAccess = false;    // Keep the background proteome from getting updated from the web

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
                peptideSettingsUI.SetSelectedLibrary("Yeast_mini");
            });
            PauseForScreenShot<PeptideSettingsUI.LibraryTab>("Peptide Settings - Library tab");

            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Digest);
            RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg,
                buildBackgroundProteomeDlg =>
                {
                    buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast_mini"; // Not L10N
                    buildBackgroundProteomeDlg.BackgroundProteomePath = GetTestPath(@"MRMer\Yeast_MRMer_mini.protdb"); // Not L10N
                    buildBackgroundProteomeDlg.OkDialog();
                });
            PauseForScreenShot<PeptideSettingsUI.DigestionTab>("Peptide Settings - Digestion tab");

            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI, "Edit Isotope Modification form", 6);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI, "Edit Isotope Modification form", 7);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });
            var docBeforePeptideSettings = SkylineWindow.Document;
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            var docBeforeTrans = WaitForDocumentChangeLoaded(docBeforePeptideSettings);
            WaitForBackgroundProteomeLoaderCompleted();

            // Inserting a Transition List With Associated Proteins, p. 6
            // using (new DocChangeLogger("ShowInsertTransitionListDlg"))
            using (new CheckDocumentState(24, 44, 88, 296))
            {
                var importDialog = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
                RunUI(() => importDialog.Size = new Size(600, 300));
                PauseForScreenShot<InsertTransitionListDlg>("Insert Transition List form");
                var filePath = GetTestPath(@"MRMer\silac_1_to_4.xls"); // Not L10N
                string text1 = GetExcelFileText(filePath, "Fixed", 3, false); // Not L10N
                var colDlg = ShowDialog<ImportTransitionListColumnSelectDlg>(() => importDialog.TransitionListText = text1);
                WaitForConditionUI(() => colDlg.AssociateProteinsPreviewCompleted); // Wait for associate proteins to complete
                Assert.IsTrue(colDlg.checkBoxAssociateProteins.Checked);
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Insert Transition List column selection form");
                OkDialog(colDlg, colDlg.OkDialog);
            }

            Assert.IsTrue(SkylineWindow.Document.Children.All(c => c.Id is FastaSequence),
                string.Format("Found {0} proteins without FASTA information", SkylineWindow.Document.Children.Count(c => !(c.Id is FastaSequence))));

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                SkylineWindow.Size = new Size(1035, 511);
            });
            FocusDocument();
            PauseForScreenShot("Main window with transitions added");

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
            RestoreViewOnScreen(10);
            PauseForScreenShot("Main window with spectral library graph showing");

            // Importing Data, p. 10.
            RunUI(() => SkylineWindow.SaveDocument(GetTestPath(@"MRMer\MRMer.sky"))); // Not L10N
            ImportResultsFile("silac_1_to_4.mzXML"); // Not L10N
            FindNode("ETFP"); // Not L10N
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.Nodes[5];
            });
            FocusDocument();
            PauseForScreenShot("Main window with data imported");

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
            PauseForScreenShot("Main window");

            // Removing a Transition with Interference
            FindNode(string.Format("{0:F04}", 504.2664));   // I18N
            RunUI(() =>
            {
                var precursorNode = (TransitionGroupTreeNode) SkylineWindow.SelectedNode.Parent;
                VerifyPrecursorRatio(precursorNode, 0.31);

                SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                var selNode = (TransitionTreeNode) SkylineWindow.SelectedNode;
                VerifyTransitionRatio(selNode, "y4", 0.37);
                var nextNode = (TransitionTreeNode) SkylineWindow.SelectedNode.NextNode;
                VerifyTransitionRatio(nextNode, "y3", 0.59);
                SkylineWindow.SequenceTree.SelectedNode = nextNode;
                var heavyNode = (TransitionTreeNode) precursorNode.Parent.Nodes[1].Nodes[1];
                VerifyTransitionRatio(heavyNode, "y4");
                SkylineWindow.SequenceTree.SelectedNode = heavyNode;
                var heavyNextNode = (TransitionTreeNode) SkylineWindow.SelectedNode.NextNode;
                VerifyTransitionRatio(heavyNextNode, "y3");
                SkylineWindow.SequenceTree.SelectedNode = heavyNextNode;
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;

                SkylineWindow.MarkQuantitative(false);

                VerifyPrecursorRatio(precursorNode, 0.24);
            });

            // Adjusting Peak Boundaries to Exclude Interference, p. 15.
            RunUI(() => SkylineWindow.Undo());
            FindNode("ETFP");
            RunUI(() =>
            {
                var precursorNode = (TransitionGroupTreeNode) SkylineWindow.SelectedNode.Nodes[0];
                VerifyPrecursorRatio(precursorNode, 0.31);
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
            RestoreViewOnScreen(15);
            PauseForScreenShot("Main window");

            FindNode("YVDP");
            RunUI(() =>
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes[2].StateImageIndex
                    == (int)SequenceTree.StateImageId.peak_blank));
            RunUI(() => SkylineWindow.SaveDocument());
            PauseForAuditLog();
        }

        private void VerifyPrecursorRatio(TransitionGroupTreeNode precursorTreeNode, double ratioExpected)
        {
            var normalizedValueCalculator = new NormalizedValueCalculator(SkylineWindow.Document);
            var ratioActual = normalizedValueCalculator.GetTransitionGroupValue(
                normalizedValueCalculator.GetFirstRatioNormalizationMethod(), precursorTreeNode.PepNode,
                precursorTreeNode.DocNode, 0, precursorTreeNode.DocNode.Results[0][0]);
            Assert.AreEqual(ratioExpected, ratioActual.Value, 0.005);
        }

        private void VerifyTransitionRatio(TransitionTreeNode transitionTreeNode, string ionName, double? ratioExpected = null)
        {
            Assert.AreEqual(ionName, transitionTreeNode.DocNode.FragmentIonName);
            var normalizedValueCalculator = new NormalizedValueCalculator(SkylineWindow.Document);
            var ratioActual = normalizedValueCalculator.GetTransitionValue(
                normalizedValueCalculator.GetFirstRatioNormalizationMethod(), transitionTreeNode.PepNode,
                transitionTreeNode.TransitionGroupNode, transitionTreeNode.DocNode,
                0, transitionTreeNode.DocNode.Results[0][0]);

            if (ratioExpected.HasValue)
                Assert.AreEqual(ratioExpected.Value, ratioActual.Value, 0.005);
            else
                Assert.IsFalse(ratioActual.HasValue);
        }

        private void DoStudy7Test()
        {
            // Preparing a Document to Accept the Study 7 Transition List, p. 18
            RunUI(() => SkylineWindow.NewDocument());
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            if (IsCoverShotMode)
            {
                var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15, // Not L10N
                    RelativeRT.Matching, null, null, null);
                AddHeavyMod(modHeavyK, peptideSettingsUI1, "Edit Isotope Modification form", 6);
            }
            var mod13Cr = new StaticMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, false, null, LabelAtoms.C13,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(mod13Cr, peptideSettingsUI1, "Edit Isotope Modification form", 19);
            RunUI(() =>
                      {
                          peptideSettingsUI1.PickedHeavyMods = new[] { "Label:13C(6) (C-term R)", HEAVY_K };
                          peptideSettingsUI1.PickedLibraries = new string[0];
                          peptideSettingsUI1.SelectedBackgroundProteome = Resources.SettingsList_ELEMENT_NONE_None;
                          peptideSettingsUI1.OkDialog();
                      });

            // Pasting a Transition List into the Document, p. 19.
            string clipboardSaveText = string.Empty;
            RunUI(() =>
            {
                var filePath = GetTestPath(@"Study 7\Study7 transition list.xls");
                SetExcelFileClipboardText(filePath, "Simple", 6, false);
                clipboardSaveText = ClipboardEx.GetText();
            });
            // We expect this to fail due to instrument settings rather than format issues eg "The product m/z 1519.78 is out of range for the instrument settings, in the peptide sequence YEVQGEVFTKPQLWP. Check the Instrument tab in the Transition Settings."
            {
                var transitionSelectdgl = ShowDialog<ImportTransitionListColumnSelectDlg>(SkylineWindow.Paste);
                PauseForScreenShot<ImportTransitionListColumnSelectDlg>("Column list selection form");

                var messageDlg = ShowDialog<ImportTransitionListErrorDlg>(transitionSelectdgl.AcceptButton.PerformClick);
                AssertEx.AreComparableStrings(TextUtil.SpaceSeparate(Resources.MassListRowReader_CalcTransitionExplanations_The_product_m_z__0__is_out_of_range_for_the_instrument_settings__in_the_peptide_sequence__1_,
                        Resources.MassListRowReader_CalcPrecursorExplanations_Check_the_Instrument_tab_in_the_Transition_Settings),
                    messageDlg.ErrorList[0].ErrorMessage,
                    2);
                RunUI(() => messageDlg.Size = new Size(838, 192));
                PauseForScreenShot<ImportTransitionListErrorDlg>("Error message form (expected)");
                OkDialog(messageDlg, messageDlg.CancelButton.PerformClick); // Acknowledge the error but decline to proceed with import
                OkDialog(transitionSelectdgl, transitionSelectdgl.CancelDialog); // Cancel the import

                // Restore the clipboard text after pausing
                ClipboardEx.SetText(clipboardSaveText);
            }

            RunDlg<TransitionSettingsUI>(() => SkylineWindow.ShowTransitionSettingsUI(TransitionSettingsUI.TABS.Instrument), transitionSettingsUI =>
            {
                transitionSettingsUI.InstrumentMaxMz = 1800;
                transitionSettingsUI.OkDialog();
            });
            PasteTransitionListSkipColumnSelect();
            RunUI(SkylineWindow.CollapsePeptides);
            PauseForScreenShot<SequenceTreeForm>("Targets tree (selected from main window)", null,
                bmp => ClipTargets(bmp, 19));

            // Adjusting Modifications Manually, p. 19.
            AdjustModifications("AGLCQTFVYGGCR", true, 'V', 747.348);
            PauseForScreenShot<SequenceTreeForm>("Targets tree clipped from main window", null,
                bmp => ClipTargets(bmp, 21));

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
                openDataSourceDialog1.SelectAllFileType(UseRawFilesOrFullData ? ".wiff" : ".mzML"); // Force true wiff for FullData
            });
            if (UseRawFilesOrFullData)
            {
                var importResultsSamplesDlg = ShowDialog<ImportResultsSamplesDlg>(openDataSourceDialog1.Open);
                PauseForScreenShot<ImportResultsSamplesDlg>("Choose Samples form");

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
                PauseForScreenShot<ImportResultsNameDlg>("Import Results Common prefix form");

                OkDialog(importResultsNameDlg, importResultsNameDlg.YesDialog);
            }
            WaitForCondition(() =>
                SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RestoreViewOnScreen(26);

            // Inspecting and Adjusting Peak Integration, p. 24.
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowGraphRetentionTime(true);
                SkylineWindow.Size = new Size(1029, 659);
            });

            if (!IsPauseForScreenShots && !IsFullData)
            {
                TestApplyToAll();
                FindNode("YEVQGEVFTKPQLWP");
            }

            FocusDocument();
            JiggleSelection();
            PauseForScreenShot("Main window with peaks and retention times showing");
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
                    if ((sequence != null) && (sequence.StartsWith("YLA") || sequence.StartsWith("YEV"))) // Not L10N
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
                            string.Format("Expected peak_blank icon for the custom ion {0}, found {1}", nodePep.ModifiedTarget, imageIndex));
                    }
                }
            });
            PauseForScreenShot("Main window");

            // Data Inspection with Peak Areas View, p. 29.
            RestoreViewOnScreen(28);
            FindNode("SSDLVALSGGHTFGK"); // Not L10N
            RunUI(NormalizeGraphToHeavy);
            PauseForPeakAreaGraphScreenShot("Peak Areas graph metafile");
            FindNode((564.7746).ToString(LocalizationHelper.CurrentCulture) + "++"); // ESDTSYVSLK - Not L10N
            WaitForGraphs();
            PauseForPeakAreaGraphScreenShot("Peak Areas graph metafile");

            RunUI(SkylineWindow.ExpandPeptides);
            string hgflprLight = (363.7059).ToString(LocalizationHelper.CurrentCulture) + "++";  // HGFLPR - Not L10N
            FindNode(hgflprLight);
            WaitForGraphs();
            PauseForPeakAreaGraphScreenShot("Peak Areas graph metafile");

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.TOTAL));
            JiggleSelection();
            PauseForPeakAreaGraphScreenShot("Peak Areas graph normalized metafile");

            RunUI(() =>
            {
                SkylineWindow.ShowGraphPeakArea(false);
                SkylineWindow.ActivateReplicate("E_03");
                SkylineWindow.Size = new Size(757, 655);
            });
            PauseForChromGraphScreenShot("Chromatogram graph metafile with interference", "E_03");

            RunUI(() => SkylineWindow.ShowGraphPeakArea(true));
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowTotalTransitions();
                SkylineWindow.ShowCVValues(true);
                SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.document);
            });
            PauseForPeakAreaGraphScreenShot("Peak Areas Peptide Comparison graph metafile");

            float[] concentrations = { 0f, 60, 175, 513, 1500, 2760, 4980, 9060, 16500, 30000 };
            var documentGrid = ShowDialog<DocumentGridForm>(() => SkylineWindow.ShowDocumentGrid(true));
            var pathConcentration = PropertyPath.Parse("AnalyteConcentration");
            var pathSampleType = PropertyPath.Parse("SampleType");
            RunUI(() => documentGrid.ChooseView(Resources.SkylineViewContext_GetDocumentGridRowSources_Replicates));
            WaitForConditionUI(() => documentGrid.RowCount == (IsFullData ? concentrations.Length * 4 : 4) &&
                                     documentGrid.FindColumn(pathConcentration) != null &&
                                     documentGrid.FindColumn(pathSampleType) != null); // Let it initialize
            RunUI(() =>
            {
                // Parent is a DocPane and Parent.Parent is the floating window
                documentGrid.Parent.Parent.Size = new Size(585, 325);
                var documentGridView = documentGrid.DataGridView;
                var colConcentration = documentGrid.FindColumn(pathConcentration);
                var colStandardType = documentGrid.FindColumn(pathSampleType);

                if (IsFullData)
                {
                    for (int i = 0; i < concentrations.Length; i++)
                    {
                        for (int j = i * 4; j < (i + 1) * 4; j++)
                        {
                            double? concentration = concentrations[i];
                            SetCellValue(documentGridView, j, colConcentration.Index, concentration);
                            SetCellValue(documentGridView, j, colStandardType.Index,
                                concentration == 0 ? SampleType.BLANK : SampleType.STANDARD);
                        }
                    }
                }
                else
                {
                    SetCellValue(documentGridView, 0, colConcentration.Index, 0.0);
                    SetCellValue(documentGridView, 0, colStandardType.Index, SampleType.BLANK);
                    SetCellValue(documentGridView, 1, colConcentration.Index, 0.0);
                    SetCellValue(documentGridView, 1, colStandardType.Index, SampleType.BLANK);
                    SetCellValue(documentGridView, 2, colConcentration.Index, 175.0);
                    SetCellValue(documentGridView, 2, colStandardType.Index, SampleType.STANDARD);
                    SetCellValue(documentGridView, 3, colConcentration.Index, 175.0);
                    SetCellValue(documentGridView, 3, colStandardType.Index, SampleType.STANDARD);
                }
            });
            WaitForGraphs();
            PauseForScreenShot<DocumentGridForm>("Document grid filled (scrolled to the end)");
            RunUI(() => documentGrid.Close());
            
            FindNode("SSDLVALSGGHTFGK"); // Not L10N
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                var settings = SkylineWindow.DocumentUI.Settings;
                var valConcentration = ReplicateValue.GetAllReplicateValues(settings).Skip(1).First();
                SkylineWindow.GroupByReplicateValue(valConcentration);
                NormalizeGraphToHeavy();
            });
            PauseForPeakAreaGraphScreenShot("Peak Areas graph with CVs metafile");
            
            RunUI(() => SkylineWindow.ShowCVValues(false));
            RunUI(() => SkylineWindow.SaveDocument());
            PauseForPeakAreaGraphScreenShot("Peak Areas graph grouped by concentration metafile");
            PauseForAuditLog();
            // Further Exploration, p. 33.
            RunUI(() =>
            {
                SkylineWindow.OpenFile(GetTestPath(@"Study 7\Study II\Study 7ii (site 52).sky")); // Not L10N
                SkylineWindow.ShowPeakAreaPeptideGraph();
                SkylineWindow.ShowCVValues(true);
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RestoreViewOnScreen(38);
            PauseForPeakAreaGraphScreenShot("Peak Areas peptide comparison graph metafile");
            FindNode("LSEPAELTDAVK");
            RunUI(() =>
            {
                SkylineWindow.ShowGraphPeakArea(false);
                SkylineWindow.Width = 970;
                SkylineWindow.ShowRTReplicateGraph();
            });
            // JiggleSelection();
            PauseForRetentionTimeGraphScreenShot("Retention Times replicate graph metafile");

            FindNode("INDISHTQSVSAK");
            RunUI(SkylineWindow.ShowPeakAreaReplicateComparison);
            RunUIForScreenShot(() => SkylineWindow.GraphPeakArea.Parent.Parent.Width = 720);
            JiggleSelection();
            PauseForPeakAreaGraphScreenShot("Peak Areas normalized to heave graph metafile");

            if (IsCoverShotMode)
            {
                RunUI(() =>
                {
                    Settings.Default.ChromatogramFontSize = 14;
                    Settings.Default.AreaFontSize = 14;
                    SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);
                    SkylineWindow.AutoZoomBestPeak();
                });
                RestoreCoverViewOnScreen();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Parent);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.Nodes[0]);
                RunUI(() =>
                {
                    SkylineWindow.SelectedResultsIndex += 1;
                    SkylineWindow.SelectedResultsIndex -= 1;
                });
                RunUI(SkylineWindow.FocusDocument);
                TakeCoverShot();
                return;
            }

            RunUI(() => SkylineWindow.NormalizeAreaGraphTo(NormalizeOption.NONE));
            WaitForGraphs();
            JiggleSelection();
            PauseForPeakAreaGraphScreenShot("Peak Areas no normalization graph metafile");

            FindNode(hgflprLight);
            RunUI(() =>
            {
                SkylineWindow.ShowAllTransitions();
                NormalizeGraphToHeavy();
            });
            WaitForGraphs();
            PauseForPeakAreaGraphScreenShot("Area Ratio to Heavy graph showing interference metafile");

            RunUI(() => SkylineWindow.ShowGraphPeakArea(false));
            RunUI(() => SkylineWindow.ActivateReplicate("E_ 03"));
            WaitForGraphs();
            PauseForChromGraphScreenShot("Chromatogram graph metafile showing slight interference", "E_ 03");
        }

        private static void NormalizeGraphToHeavy()
        {
            SkylineWindow.AreaNormalizeOption = NormalizeOption.FromIsotopeLabelType(IsotopeLabelType.heavy);
            Settings.Default.AreaLogScale = false;
            SkylineWindow.UpdatePeakAreaGraph();
        }

        private void AdjustModifications(string peptideSeq, bool removeCTerminalMod, char aa13C, double expectedPrecursorMz)
        {
            FindNode(peptideSeq);
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            string sequence = string.Empty;
            bool modsContainLabel13C = false;
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                sequence = selNode.DocNode.Peptide.Sequence;
                if(removeCTerminalMod)
                    editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.Length - 1, string.Empty);

                // Only access Settings.Default on the UI thread
                modsContainLabel13C = Settings.Default.HeavyModList.Contains(mod => Equals(mod.Name, "Label:13C"));
            });
            if (modsContainLabel13C) // Not L10N
                RunUI(() => editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.IndexOf(aa13C), "Label:13C")); // Not L10N
            else
            {
                var editStaticModDlg = ShowDialog<EditStaticModDlg>(() => editPepModsDlg.AddNewModification(sequence.IndexOf(aa13C), IsotopeLabelType.heavy));
                RunUI(() => editStaticModDlg.Modification = new StaticMod("Label:13C", null, null, LabelAtoms.C13)); // Not L10N
                PauseForScreenShot<EditStaticModDlg.IsotopeModView>("Edit Isotope Modification form");

                OkDialog(editStaticModDlg, editStaticModDlg.OkDialog);
                // Make sure the right combo has the focus for the screen shot
                RunUI(() => editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.IndexOf(aa13C), "Label:13C")); // Not L10N
                PauseForScreenShot<EditPepModsDlg>("Edit Modifications form");
            }
            var doc = SkylineWindow.Document;
            OkDialog(editPepModsDlg, editPepModsDlg.OkDialog);
            WaitForDocumentChange(doc);
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                DocNode[] choices = selNode.GetChoices(true).ToArray();
                Assert.IsTrue(choices.Contains(node =>
                    ((TransitionGroupDocNode)node).PrecursorMz.Value.ToString(LocalizationHelper.CurrentCulture)
                        .Contains(expectedPrecursorMz.ToString(LocalizationHelper.CurrentCulture))));
                selNode.Pick(choices, false, false);
            });
        }

        private static void TestApplyToAll()
        {
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 568.7817, "A_01", false, false, 20.2587);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(20.24220, 20.25878, 20.09352, 20.09353));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 564.7746, "A_02", false, false, 18.34195);
                PeakMatcherTestUtil.VerifyPeaks(MakeVerificationDictionary(18.34, 18.34, 18.28, 18.28));
            });
            RunUI(() =>
            {
                PeakMatcherTestUtil.SelectAndApplyPeak("ESDTSYVSLK", 568.7817, "C_03", false, false, 18.0611);
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
