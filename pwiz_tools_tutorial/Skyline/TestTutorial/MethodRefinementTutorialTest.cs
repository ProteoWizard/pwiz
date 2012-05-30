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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;


namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Refinement
    /// </summary>
    [TestClass]
    public class MethodRefinementTutorialTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMethodRefinementTutorial()
        {
            string supplementZip = (ExtensionTestContext.CanImportThermoRaw ?
                @"https://skyline.gs.washington.edu/tutorials/MethodRefineSupplement.zip" :
                @"https://skyline.gs.washington.edu/tutorials/MethodRefineSupplementMzml.zip");

            TestFilesZipPaths = new[] { supplementZip, ExtensionTestContext.CanImportThermoRaw ?
                    @"https://skyline.gs.washington.edu/tutorials/MethodRefine.zip" :
                    @"https://skyline.gs.washington.edu/tutorials/MethodRefineMzml.zip"
                 };

         
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Targeted Method Refinement

            var folderMethodRefine = ExtensionTestContext.CanImportThermoRaw ? "MethodRefine" : "MethodRefineMzml";

            // Results Data, p. 2
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\WormUnrefined.sky"));
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];

                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode.Text, "YLGAYLLATLGGNASPSAQDVLK");
            });

            // Unrefined Methods, p. 3
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportDlg =>
            {
                exportDlg.ExportStrategy = ExportStrategy.Buckets;
                exportDlg.MethodType = ExportMethodType.Standard;
                exportDlg.OptimizeType = ExportOptimize.NONE;
                exportDlg.MaxTransitions = 59;
                exportDlg.OkDialog(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm"));
            });

            for (int i = 1; i < 10; i++)
            {
                Assert.IsTrue(File.Exists(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm_000" + i + ".csv")));
            }
            for (int i = 10; i < 40; i++)
            {
                Assert.IsTrue(File.Exists(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm_00" + i + ".csv")));
            }

            // Importing Multiple Injection Data, p. 4

            Assert.IsTrue(SkylineWindow.Document.Settings.HasResults);
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.Remove();
                Assert.AreEqual(manageResultsDlg.Chromatograms.ToArray().Length, 0);
                manageResultsDlg.OkDialog();
            });

            RunUI(() => SkylineWindow.SaveDocument());
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);
            
            const string replicateName = "Unrefined";
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var namedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDirs[0].FullPath).ToArray();
                importResultsDlg.NamedPathSets =
                    new[] {new KeyValuePair<string, string[]>(replicateName, namedPathSets[0].Value.Take(15).ToArray())};
                importResultsDlg.OkDialog();
            });
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);  // 15 minutes

            Assert.IsTrue(SkylineWindow.Document.Settings.HasResults);
            Assert.AreEqual(15, SkylineWindow.Document.Settings.MeasuredResults.CachedFilePaths.ToArray().Length);

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddExistingChecked = true;
                var namedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDirs[0].FullPath).ToArray();
                importResultsDlg.NamedPathSets =
                    new[] { new KeyValuePair<string, string[]>(replicateName, namedPathSets[0].Value.Skip(15).ToArray()) };
                importResultsDlg.OkDialog();
            });
            WaitForCondition(20*60*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);  // 15 minutes

            Assert.AreEqual(39, SkylineWindow.Document.Settings.MeasuredResults.CachedFilePaths.ToArray().Length);
           
            // Simple Manual Refinement, p. 6
            int startingNodeCount = SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false);

            Assert.AreEqual("YLGAYLLATLGGNASPSAQDVLK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text);

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.EditDelete();
                SkylineWindow.ShowRTLinearRegressionGraph();
            });
            Assert.AreEqual(SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false), startingNodeCount - 1);
            Assert.AreEqual("VLEAGGLDCDMENANSVVDALK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text);

            RunDlg<ShowRTThresholdDlg>(SkylineWindow.ShowRTThresholdDlg, rtThresholdDlg =>
            {
                rtThresholdDlg.Threshold = 0.95;
                rtThresholdDlg.OkDialog();
            });
            WaitForConditionUI(() => SkylineWindow.RTGraphController.RegressionRefined != null);

            RunDlg<EditRTDlg>(SkylineWindow.CreateRegression, editRTDlg => editRTDlg.OkDialog());

            // Missing Data, p. 10
            RunUI(() =>
            {
                SkylineWindow.RTGraphController.SelectPeptide(SkylineWindow.Document.GetPathTo(1, 163));
                Assert.AreEqual("YLAEVASEDR", SkylineWindow.SequenceTree.SelectedNode.Text);
                var nodePep = (PeptideDocNode)((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode).Model;
                Assert.AreEqual(null,
                                nodePep.GetPeakCountRatio(
                                    SkylineWindow.SequenceTree.GetDisplayResultsIndex(nodePep)));
                SkylineWindow.RTGraphController.GraphSummary.DocumentUIContainer.FocusDocument();
                SkylineWindow.SequenceTree.SelectedPath = SkylineWindow.Document.GetPathTo(1, 157);
                Assert.AreEqual("VTVVDDQSVILK", SkylineWindow.SequenceTree.SelectedNode.Text);
            });
            WaitForGraphs();
//            foreach (var peptideDocNode in SkylineWindow.Document.Peptides)
//            {
//                var nodeGroup = ((TransitionGroupDocNode)peptideDocNode.Children[0]);
//                Console.WriteLine("{0} - {1}", peptideDocNode.Peptide.Sequence,
//                    nodeGroup.GetDisplayText(SkylineWindow.SequenceTree.GetDisplaySettings(peptideDocNode)));
//            }
//            Console.WriteLine("---------------------------------");
            RunUI(() =>
            {
                var graphChrom = SkylineWindow.GetGraphChrom("Unrefined");
                Assert.AreEqual(2, graphChrom.Files.Count);
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                SkylineWindow.RTGraphController.GraphSummary.Close();
               
                // Picking Measurable Peptides and Transitions, p. 12
                SkylineWindow.ExpandPeptides();

                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text
                    .Contains((0.78).ToString(CultureInfo.CurrentCulture)));
                SkylineWindow.EditDelete();

                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                Assert.IsTrue(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text
                    .Contains((0.63).ToString(CultureInfo.CurrentCulture)));
                SkylineWindow.EditDelete();

                PeptideTreeNode nodePep;
                for (int i = 0; i < 2; i++)
                {
                    nodePep = (PeptideTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[i];
                    nodePep.ExpandAll();
                    foreach (TransitionTreeNode nodeTran in nodePep.Nodes[0].Nodes)
                    {
                        TransitionDocNode nodeTranDoc = (TransitionDocNode) nodeTran.Model;
                        Assert.AreEqual((int) SequenceTree.StateImageId.peak,
                                        TransitionTreeNode.GetPeakImageIndex(nodeTranDoc,
                                                                             (PeptideDocNode) nodePep.Model,
                                                                             SkylineWindow.SequenceTree));
                        var resultsIndex = SkylineWindow.SequenceTree.GetDisplayResultsIndex(nodePep);
                        var rank = nodeTranDoc.GetPeakRank(resultsIndex);
                        if (rank == null || rank > 3)
                            SkylineWindow.SequenceTree.SelectedNode = nodeTran;
                        SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                    }
                }
                nodePep = (PeptideTreeNode) SkylineWindow.SequenceTree.Nodes[0].Nodes[2];
                nodePep.ExpandAll();
                foreach (TransitionTreeNode nodeTran in nodePep.Nodes[0].Nodes)
                {
                    TransitionDocNode nodeTranDoc = (TransitionDocNode) nodeTran.Model;
                    Assert.AreEqual((int) SequenceTree.StateImageId.peak,
                                    TransitionTreeNode.GetPeakImageIndex(nodeTranDoc,
                                                                         (PeptideDocNode) nodePep.Model,
                                                                         SkylineWindow.SequenceTree));
                    var name = ((TransitionDocNode) nodeTran.Model).FragmentIonName;
                    if (!(name == "y11" || name == "y13" || name == "y14"))
                        SkylineWindow.SequenceTree.SelectedNode = nodeTran;
                    SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                }
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.EditDelete();
                for (int i = 0; i < 3; i++)
                    Assert.IsTrue(SkylineWindow.SequenceTree.Nodes[0].Nodes[i].Nodes[0].Nodes.Count == 3);
                SkylineWindow.AutoZoomNone();
            });

            // Automated Refinement, p. 16
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.MaxTransitionPeakRank = 3;
                refineDlg.PreferLargerIons = true;
                refineDlg.RemoveMissingResults = true;
                refineDlg.RTRegressionThreshold = 0.95;
                refineDlg.DotProductThreshold = 0.95;
                refineDlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.PeptideCount < 75);
//            foreach (var peptideDocNode in SkylineWindow.Document.Peptides)
//            {
//                var nodeGroup = ((TransitionGroupDocNode) peptideDocNode.Children[0]);
//                Console.WriteLine("{0} - {1}", peptideDocNode.Peptide.Sequence,
//                    nodeGroup.GetDisplayText(SkylineWindow.SequenceTree.GetDisplaySettings(peptideDocNode)));
//            }
            RunUI(() =>
            {
                // TODO change back to 70 and 210
                Assert.AreEqual(65, SkylineWindow.Document.PeptideCount);
                Assert.AreEqual(195, SkylineWindow.Document.TransitionCount);
                SkylineWindow.CollapsePeptides();
                SkylineWindow.Undo();
            });
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.MaxTransitionPeakRank = 6;
                refineDlg.RemoveMissingResults = true;
                refineDlg.RTRegressionThreshold = 0.90;
                refineDlg.DotProductThreshold = 0.90;
                refineDlg.OkDialog();
            });

            WaitForCondition(() => SkylineWindow.Document.PeptideCount < 120);
            RunUI(() =>
            {
                //Todo: Was previously 110
                Assert.AreEqual(105, SkylineWindow.Document.PeptideCount);

                // Scheduling for Efficient Acquisition, p. 17 
                SkylineWindow.Undo();
            });

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, mResults =>
            {
                Assert.AreEqual(1, mResults.Chromatograms.Count());

                mResults.SelectedChromatograms =
                    SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Where(
                        set => Equals("Unrefined", set.Name));

                mResults.Remove();

                Assert.AreEqual(0, mResults.Chromatograms.Count());
                mResults.OkDialog();
            });

            var importResultsDlg0 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
                importResultsDlg0.RadioCreateMultipleMultiChecked = true;
                importResultsDlg0.NamedPathSets =
                    DataSourceUtil.GetDataSourcesInSubdirs(Path.Combine(TestFilesDirs[1].FullPath,
                                                                          Path.GetFileName(TestFilesDirs[1].FullPath) ??
                                                                          "")).ToArray();
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg0.OkDialog);
            RunUI(importResultsNameDlg.NoDialog);
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes
            
            var docCurrent = SkylineWindow.Document;
            RunUI(SkylineWindow.RemoveMissingResults);
            WaitForDocumentChange(docCurrent);
            Assert.AreEqual(86, SkylineWindow.Document.PeptideCount);
            Assert.AreEqual(255, SkylineWindow.Document.TransitionCount);

            // Measuring Retention Times, p. 17
            RunDlg<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List), exportMethodDlg =>
            {
                exportMethodDlg.MaxTransitions = 130;
                exportMethodDlg.OkDialog(TestFilesDirs[1].FullPath + "\\unscheduled");
            });
            ///////////////////////

            // Reviewing Retention Time Runs, p. 18
            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ShowRTSchedulingGraph();
            });
            WaitForCondition(() => SkylineWindow.GraphRetentionTime != null);
           
            // Creating a Scheduled Transition List, p. 20 
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUI =>
            {
                peptideSettingsUI.TimeWindow = 4;
                peptideSettingsUI.OkDialog();
            });
            var exportMethodDlg1 = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
            {
                exportMethodDlg1.MethodType = ExportMethodType.Scheduled;
            });
            // TODO: Update tutorial to mention the scheduling options dialog.
            RunDlg<SchedulingOptionsDlg>(() =>
                exportMethodDlg1.OkDialog(TestFilesDirs[1].FullPath + "\\scheduled"),
                schedulingOptionsDlg => schedulingOptionsDlg.OkDialog());
            WaitForClosedForm(exportMethodDlg1);

            // Reviewing Multi-Replicate Data, p. 22
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAll();
                manageResultsDlg.OkDialog();
            });
            var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunDlg<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets = importResultsDlg1.GetDataSourcePathsFile(null),
                openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectAllFileType(ExtensionTestContext.ExtThermoRaw);
                        openDataSourceDialog.Open();
                });
            RunDlg<ImportResultsNameDlg>(importResultsDlg1.OkDialog, importResultsNameDlg0 =>
            {
                importResultsNameDlg0.Prefix = "Scheduled_";
                importResultsNameDlg0.YesDialog();
            });
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes
            Assert.AreEqual(5, SkylineWindow.GraphChromatograms.Count(graphChrom => !graphChrom.IsHidden));
            RunUI(() =>
            {
                SkylineWindow.RemoveMissingResults();
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.ShowGraphRetentionTime(false);
            });
            WaitForCondition(() => SkylineWindow.GraphRetentionTime.IsHidden);
            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.ShowRTReplicateGraph();
                SkylineWindow.ShowPeakAreaReplicateComparison();
            });
            WaitForGraphs();
        }
    }
}
