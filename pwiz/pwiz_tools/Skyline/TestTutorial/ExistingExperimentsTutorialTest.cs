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

using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
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
            TestFilesZip = ExtensionTestContext.CanImportAbWiff
                               ? "https://skyline.gs.washington.edu/tutorials/ExistingQuant.zip"
                               : "https://skyline.gs.washington.edu/tutorials/ExistingQuantMzml.zip";
            RunFunctionalTest();
        }

        private const string HEAVY_R = "Label:13C(6)15N(4) (C-term R)";
        private const string HEAVY_K = "Label:13C(6)15N(2) (C-term K)";

        protected override void DoTest()
        {
            var folderExistQuant = ExtensionTestContext.CanImportAbWiff ? "ExistingQuant" : "ExistingQuantMzml";

            // Preparing a Document to Accept a Transition List, p. 2
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var editListUI =
                ShowDialog<EditListDlg<SettingsListBase<LibrarySpec>, LibrarySpec>>(peptideSettingsUI.EditLibraryList);
            RunDlg<EditLibraryDlg>(editListUI.AddItem, editLibraryDlg =>
            {
                editLibraryDlg.LibrarySpec =
                    new BiblioSpecLibSpec("Yeast_mini",
                        TestFilesDir.GetTestPath(folderExistQuant + @"\MRMer\Yeast_MRMer_min.blib"));
                editLibraryDlg.OkDialog();
            });
            OkDialog(editListUI, editListUI.OkDialog);
            RunUI(() => peptideSettingsUI.PickedLibraries = new[] { "Yeast_mini" });
            RunDlg<BuildBackgroundProteomeDlg>(peptideSettingsUI.ShowBuildBackgroundProteomeDlg,
                buildBackgroundProteomeDlg =>
                {
                    buildBackgroundProteomeDlg.BuildNew = false;
                    buildBackgroundProteomeDlg.BackgroundProteomeName = "Yeast_mini";
                    buildBackgroundProteomeDlg.BackgroundProteomePath =
                        TestFilesDir.GetTestPath(folderExistQuant + @"\MRMer\Yeast_MRMer_mini.protdb");
                    buildBackgroundProteomeDlg.OkDialog();
                });
            var modHeavyK = new StaticMod(HEAVY_K, "K", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyK, peptideSettingsUI);
            var modHeavyR = new StaticMod(HEAVY_R, "R", ModTerminus.C, false, null, LabelAtoms.C13 | LabelAtoms.N15,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(modHeavyR, peptideSettingsUI);
            RunUI(() => peptideSettingsUI.PickedHeavyMods = new[] { HEAVY_K, HEAVY_R });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);
            WaitForCondition(
                () =>
                SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries.Count > 0
                && SkylineWindow.Document.Settings.HasBackgroundProteome
                && SkylineWindow.Document.Settings.IsLoaded
                && SkylineWindow.GraphSpectrumVisible());

            // Inserting a Transition List With Associated Proteins, p. 6
            RunUI(() =>
            {
                var filePath = TestFilesDir.GetTestPath(folderExistQuant + @"\MRMer\silac_1_to_4.xls");
                SetExcelFileClipboardText(filePath, "Fixed", 3, false);
            });
            RunDlg<PasteDlg>(SkylineWindow.ShowPasteTransitionListDlg, pasteDlg =>
            {
                pasteDlg.PasteTransitions();
                pasteDlg.OkDialog();
            });
            AssertEx.IsDocumentState(SkylineWindow.Document, null, 24, 44, 88, 296);
            FindNode("LWDVAT");
            RunUI(() =>
            {
                Assert.IsTrue(((PeptideDocNode)((PeptideTreeNode)SkylineWindow.SelectedNode).Model).HasChildType(IsotopeLabelType.heavy));
                SkylineWindow.ExpandPrecursors();
            });
            FindNode(string.Format("{0:F03}",854.4));   // I18N
            WaitForGraphs();
            // Unfornunately, label hiding may mean that the selected ion is not labeled
            Assert.IsTrue(SkylineWindow.GraphSpectrum.SelectedIonLabel != null
                              ? SkylineWindow.GraphSpectrum.SelectedIonLabel.Contains("y7")
                              : SkylineWindow.GraphSpectrum.IonLabels.Contains(ionLabel => ionLabel.Contains("y7")));
            RunUI(() =>
            {
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.GraphSpectrumSettings.ShowCharge2 = true;
            });

            // Importing Data, p. 10.
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(folderExistQuant + @"\MRMer\MRMer.sky")));
            ImportResultsFile("silac_1_to_4.mzXML");
            FindNode("ETFP");
            RunUI(() =>
            {
                var selNode = SkylineWindow.SequenceTree.SelectedNode;
                Assert.IsTrue(selNode.Text.Contains("ETFPILVEEK"));
                Assert.IsTrue(((PeptideDocNode)((PeptideTreeNode)selNode).Model).HasResults);
                Assert.IsTrue(Equals(selNode.StateImageIndex, (int)SequenceTree.StateImageId.keep));
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowAllTransitions();
                SkylineWindow.SelectedPath =
                  SkylineWindow.SequenceTree.GetNodePath(selNode.Nodes[0].Nodes[2] as TreeNodeMS);
                Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.StateImageIndex, 
                    (int) SequenceTree.StateImageId.no_peak));
            });
            
            // Removing a Transition with Interference, p. 13.
            FindNode(string.Format("{0:F04}", 504.2664));   // I18N
            RunUI(() =>
            {
                TransitionTreeNode selNode = (TransitionTreeNode)SkylineWindow.SequenceTree.SelectedNode;
                SkylineWindow.RemovePeak(SkylineWindow.SequenceTree.SelectedPath.GetPathTo((int)SrmDocument.Level.TransitionGroups),
                ((TransitionGroupTreeNode)selNode.Parent).DocNode, selNode.DocNode);
                Assert.IsTrue(Equals(SkylineWindow.SequenceTree.SelectedNode.StateImageIndex,
                    (int)SequenceTree.StateImageId.no_peak) && SkylineWindow.SequenceTree.SelectedNode.Parent.Text.Contains((0.24).ToString(CultureInfo.CurrentCulture)));
            });

            // Adjusting Peak Boundaries to Exclude Interference, p. 14.
            RunUI(() => SkylineWindow.Undo());
            FindNode("ETFP");
            RunUI(() =>
            {  
                var pathGroup =
                SkylineWindow.SequenceTree.GetNodePath((TreeNodeMS) SkylineWindow.SelectedNode.Nodes[0]);
                var graphChrom = SkylineWindow.GraphChromatograms.ToList()[0];
                var listChanges = new List<ChangedPeakBoundsEventArgs>
                {
                    new ChangedPeakBoundsEventArgs(pathGroup, null, graphChrom.NameSet, 
                        graphChrom.ChromGroupInfos[0].FilePath, 29.8, 30.4, false, PeakBoundsChangeType.both)
                };
                graphChrom.SimulateChangedPeakBounds(listChanges);
                foreach(TransitionTreeNode node in SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes)
                {
                    Assert.IsTrue(((TransitionDocNode) node.Model).HasResults);
                }
            });
            FindNode("YVDP");
            RunUI(() => 
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Nodes[2].StateImageIndex 
                    == (int) SequenceTree.StateImageId.peak_blank));

            // Preparing a Document to Accept the Study 7 Transition List, p. 15
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.NewDocument();
            });
            var peptideSettingsUI1 = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            var mod13Cr = new StaticMod("Label:13C(6) (C-term R)", "R", ModTerminus.C, false, null, LabelAtoms.C13,
                                          RelativeRT.Matching, null, null, null);
            AddHeavyMod(mod13Cr, peptideSettingsUI1);
            RunUI(() =>
                      {
                          peptideSettingsUI1.PickedHeavyMods = new[] {"Label:13C(6) (C-term R)", HEAVY_K};
                          peptideSettingsUI1.PickedLibraries = new[] { "" };
                          peptideSettingsUI1.SelectedBackgroundProteome = "None";
                          peptideSettingsUI1.OkDialog();
                      });

            // Pasting a Transition List into the Document, p. 18.
            RunUI(() =>
            {
                var filePath = TestFilesDir.GetTestPath(folderExistQuant + @"\Study 7\Study7 transition list.xls");
                SetExcelFileClipboardText(filePath, "Simple", 6, false);
            });
            
            RunDlg<MessageDlg>(SkylineWindow.Paste, messageDlg => messageDlg.OkDialog());

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.InstrumentMaxMz = 1800;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() =>
            {
                SkylineWindow.Paste();
                SkylineWindow.CollapsePeptides();
            });
                                
            // Adjusting Modifications Manually, p. 19.
            AdjustModifications("AGLCQTFVYGGCR", true, 'V', 747.348);
            AdjustModifications("IVGGWECEK", true, 'V', 541.763);
            AdjustModifications("YEVQGEVFTKPQLWP", false, 'L', 913.974);
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath(folderExistQuant + @"\Study 7\Study7.sky")));

            // Importing Data from a Multiple Sample WIFF file, p. 23.
            var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            var openDataSourceDialog1 = ShowDialog<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets =
                                                                        importResultsDlg1.GetDataSourcePathsFile(null));
            RunUI(() =>
            {
               openDataSourceDialog1.CurrentDirectory = TestFilesDir.GetTestPath(folderExistQuant + @"\Study 7");
               openDataSourceDialog1.SelectAllFileType(ExtensionTestContext.ExtAbWiff);
            });
            if (ExtensionTestContext.CanImportAbWiff)
            {
                RunDlg<ImportResultsSamplesDlg>(openDataSourceDialog1.Open, importResultsSamplesDlg =>
                {
                    importResultsSamplesDlg.CheckAll(false);
                    importResultsSamplesDlg.IncludeSample(1);
                    importResultsSamplesDlg.IncludeSample(12);
                    importResultsSamplesDlg.OkDialog();
                });
            }
            else
            {
                RunUI(openDataSourceDialog1.Open);
            }
            RunDlg<ImportResultsNameDlg>(importResultsDlg1.OkDialog,
                importResultsNameDlg => importResultsNameDlg.YesDialog());
            WaitForCondition(() =>
                SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);

            // Inspecting and Adjusting Peak Integration, p. 24.
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                SkylineWindow.ShowGraphRetentionTime(true);
                SkylineWindow.EditDelete();
            });
            FindNode("IVGGWECEK");

            TransitionGroupTreeNode selNodeGroup = null;
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.Nodes[1];
                selNodeGroup = (TransitionGroupTreeNode) SkylineWindow.SequenceTree.SelectedNode;
                Assert.AreEqual(selNodeGroup.StateImageIndex, (int) SequenceTree.StateImageId.peak_blank);
            });
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUI =>
            {
                transitionSettingsUI.MZMatchTolerance = 0.065;
                transitionSettingsUI.OkDialog();
            });
            RunUI(() =>
            {
                Assert.AreEqual(selNodeGroup.StateImageIndex, (int)SequenceTree.StateImageId.peak);
                SkylineWindow.IntegrateAll();
                foreach(PeptideDocNode nodePep in SkylineWindow.Document.Peptides)
                {
                    Assert.AreEqual(PeptideTreeNode.GetPeakImageIndex(nodePep, SkylineWindow.SequenceTree),
                       (int) SequenceTree.StateImageId.peak);
                }
            });

            // Data Inspection with Peak Areas View, p. 27.
            FindNode("SSDLVALSGGHTFGK");
            RunUI(NormalizeGraphToHeavy);
            RunUI(SkylineWindow.ExpandPeptides);
            FindNode("HGFLPR");

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.Nodes[0];
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.area_percent_view);
                SkylineWindow.ShowRTPeptideGraph();
                SkylineWindow.ShowTotalTransitions();
                SkylineWindow.ShowCVValues();
                SkylineWindow.ShowPeptideOrder(SummaryPeptideOrder.document);
            });

            // Further Exploration, p. 33.
            RunUI(() =>
            {
                SkylineWindow.OpenFile(
                  TestFilesDir.GetTestPath(folderExistQuant + @"\Study 7\Study II\Study 7ii (site 52).sky"));
                SkylineWindow.ShowPeakAreaReplicateComparison();
                NormalizeGraphToHeavy();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
            });
            WaitForCondition(() => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);
            RunUI(() =>
            {
                SkylineWindow.ShowPeakAreaReplicateComparison();
                NormalizeGraphToHeavy();
                SkylineWindow.NormalizeAreaGraphTo(AreaNormalizeToView.none);
            });
            WaitForGraphs();
            FindNode("HGFLPR");
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.SelectedNode.Nodes[0];
                SkylineWindow.ShowAllTransitions();
                NormalizeGraphToHeavy();
            });
            WaitForGraphs();
        }

        private static void FindNode(string searchText)
        {
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findPeptideDlg =>
            {
                findPeptideDlg.SearchString = searchText;
                findPeptideDlg.FindNext();
                findPeptideDlg.Close();
            });
        }

        private static void NormalizeGraphToHeavy()
        {
            AreaGraphController.AreaView = AreaNormalizeToView.area_ratio_view;
            Settings.Default.AreaLogScale = false;
            SkylineWindow.UpdatePeakAreaGraph();
        }

        private static void AdjustModifications(string peptideSeq, bool removeCTerminalMod, char aa13C, double expectedPrecursorMz)
        {
            FindNode(peptideSeq);
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            string sequence = "";
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                sequence = selNode.DocNode.Peptide.Sequence;
                if(removeCTerminalMod)
                    editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.Length - 1, "");
            });
            if(Settings.Default.HeavyModList.Contains(mod => Equals(mod.Name, "Label:13C")))
                RunUI(() => editPepModsDlg.SelectModification(IsotopeLabelType.heavy, sequence.IndexOf(aa13C), "Label:13C"));
            else
            {
                RunDlg<EditStaticModDlg>(() => editPepModsDlg.AddNewModification(sequence.IndexOf(aa13C), IsotopeLabelType.heavy),
                    editStaticModDlg =>
                {
                    editStaticModDlg.Modification = new StaticMod("Label:13C", null, null, LabelAtoms.C13);
                    editStaticModDlg.OkDialog();
                });
            }
            var doc = SkylineWindow.Document;
            RunUI(editPepModsDlg.OkDialog);
            WaitForDocumentChange(doc);
            RunUI(() =>
            {
                PeptideTreeNode selNode = ((PeptideTreeNode)SkylineWindow.SequenceTree.SelectedNode);
                IEnumerable<DocNode> choices = selNode.GetChoices(true);
                Assert.IsTrue(choices.Contains(node =>
                    ((TransitionGroupDocNode)node).PrecursorMz.ToString(CultureInfo.CurrentCulture)
                        .Contains(expectedPrecursorMz.ToString(CultureInfo.CurrentCulture))));
                selNode.Pick(choices, false, false);
            });
        }
    }
}
