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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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
            // Set true to look at tutorial screenshots.
            //IsPauseForScreenShots = true;

            ForceMzml = true;   // 2-3x faster than raw files for this test.

            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/MethodRefine-1_4.pdf";

            // Set to use MzML for speed, especially during debugging.
            //Skyline.Program.NoVendorReaders = true;

            string supplementZip = (UseRawFiles ?
                @"https://skyline.gs.washington.edu/tutorials/MethodRefineSupplement.zip" : // Not L10N
                @"https://skyline.gs.washington.edu/tutorials/MethodRefineSupplementMzml.zip"); // Not L10N

            TestFilesZipPaths = new[] { supplementZip,
                UseRawFiles ?
                    @"https://skyline.gs.washington.edu/tutorials/MethodRefine.zip" : // Not L10N
                    @"https://skyline.gs.washington.edu/tutorials/MethodRefineMzml.zip", // Not L10N
                @"TestTutorial\MethodRefinementViews.zip",                     
            };
         
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Targeted Method Refinement

            var folderMethodRefine = UseRawFiles ? "MethodRefine" : "MethodRefineMzml"; // Not L10N

            // Results Data, p. 2
            var doc = SkylineWindow.Document;
            RunUI(() => SkylineWindow.OpenFile(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\WormUnrefined.sky"))); // Not L10N
            WaitForDocumentChangeLoaded(doc);
            RunUI(() =>
            {
                // Adjust font sizes for better screen shots
                Settings.Default.ChromatogramFontSize = 14;
                Settings.Default.SpectrumFontSize = 14;
                SkylineWindow.ChangeTextSize(TreeViewMS.LRG_TEXT_FACTOR);

                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.AutoZoomBestPeak();   // TODO: Mention this in the tutorial
                SkylineWindow.Size = new Size(1160, 660);

                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode.Text, "YLGAYLLATLGGNASPSAQDVLK"); // Not L10N
            });
            PauseForScreenShot("Main window", 2);

            // TODO: Update tutorial to view b-ions.
            RunUI(() => SkylineWindow.GraphSpectrumSettings.ShowBIons = true);

            // Unrefined Methods, p. 3
            {
                var exportDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportDlg.MethodType = ExportMethodType.Standard;
                    exportDlg.OptimizeType = ExportOptimize.NONE;
                    exportDlg.MaxTransitions = 59;
                });
                PauseForScreenShot("Export Transition List form", 3); // Not L10N
                OkDialog(exportDlg, () => exportDlg.OkDialog(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm"))); // Not L10N
            }

            for (int i = 1; i < 10; i++)
            {
                Assert.IsTrue(File.Exists(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm_000" + i + TextUtil.EXT_CSV))); // Not L10N
            }
            for (int i = 10; i < 40; i++)
            {
                Assert.IsTrue(File.Exists(TestFilesDirs[1].GetTestPath(folderMethodRefine + @"\worm_00" + i + TextUtil.EXT_CSV))); // Not L10N
            }

            // Importing Multiple Injection Data, p. 4

            Assert.IsTrue(SkylineWindow.Document.Settings.HasResults);
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveReplicates();
                Assert.AreEqual(manageResultsDlg.Chromatograms.ToArray().Length, 0);
                manageResultsDlg.OkDialog();
            });

            RunUI(() => SkylineWindow.SaveDocument());
            Assert.IsFalse(SkylineWindow.Document.Settings.HasResults);

            const string replicateName = "Unrefined"; // Not L10N
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddNewChecked = true;
                var namedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDirs[0].FullPath).ToArray();
                importResultsDlg.NamedPathSets =
                    new[] {new KeyValuePair<string, MsDataFileUri[]>(replicateName, namedPathSets[0].Value.Take(15).ToArray())};
                importResultsDlg.OkDialog();
            });
            WaitForOpenForm<AllChromatogramsGraph>();   // To make the AllChromatogramsGraph form accessible to the SkylineTester forms tab
            PauseForScreenShot<AllChromatogramsGraph>("Loading Chromatograms: Take screenshot at about 25% loaded...", 5);
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);  // 15 minutes

            Assert.IsTrue(SkylineWindow.Document.Settings.HasResults);
            Assert.AreEqual(15, SkylineWindow.Document.Settings.MeasuredResults.CachedFilePaths.ToArray().Length);

            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg =>
            {
                importResultsDlg.RadioAddExistingChecked = true;
                var namedPathSets = DataSourceUtil.GetDataSourcesInSubdirs(TestFilesDirs[0].FullPath).ToArray();
                importResultsDlg.NamedPathSets =
                    new[] { new KeyValuePair<string, MsDataFileUri[]>(replicateName, namedPathSets[0].Value.Skip(15).ToArray()) };
                importResultsDlg.OkDialog();
            });
            WaitForCondition(20*60*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);  // 15 minutes

            Assert.AreEqual(39, SkylineWindow.Document.Settings.MeasuredResults.CachedFilePaths.ToArray().Length);

            RunUI(SkylineWindow.AutoZoomNone);
            RestoreViewOnScreen(7);
            PauseForScreenShot("Chromatogram graph metafile", 7);

            // Simple Manual Refinement, p. 6
            int startingNodeCount = SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false);

            Assert.AreEqual("YLGAYLLATLGGNASPSAQDVLK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text); // Not L10N

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.EditDelete();
                SkylineWindow.ShowRTLinearRegressionGraph();
            });
            Assert.AreEqual(SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false), startingNodeCount - 1);
            Assert.AreEqual("VLEAGGLDCDMENANSVVDALK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text); // Not L10N
            PauseForScreenShot("Retention Times Regression plot metafile", 8);

            RunDlg<RegressionRTThresholdDlg>(SkylineWindow.ShowRegressionRTThresholdDlg, rtThresholdDlg =>
            {
                rtThresholdDlg.Threshold = 0.95;
                rtThresholdDlg.OkDialog();
            });
            WaitForConditionUI(() => SkylineWindow.RTGraphController.RegressionRefined != null);
            WaitForGraphs();
            PauseForScreenShot("Retention Times Regression plot metafile with 0.95 threshold", 9); // Not L10N

            TestRTResidualsSwitch();

            RunDlg<EditRTDlg>(SkylineWindow.CreateRegression, editRTDlg => editRTDlg.OkDialog());

            RunUI(() => SkylineWindow.ShowGraphRetentionTime(false));
            RunUI(SkylineWindow.AutoZoomNone);
            PauseForScreenShot("Chromatogram graph metafile zoomed out", 10); // Not L10N

            // Missing Data, p. 10
            RunUI(() =>
                {
                    SkylineWindow.RTGraphController.SelectPeptide(SkylineWindow.Document.GetPathTo(1, 163));
                    Assert.AreEqual("YLAEVASEDR", SkylineWindow.SequenceTree.SelectedNode.Text); // Not L10N
                });
            RestoreViewOnScreen(11);
            //  Restoring the view changes the selection
            RunUI(SkylineWindow.CollapsePeptides);
            FindNode("YLAEVASEDR");
            RunUI(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[153]);

            PauseForScreenShot("Targets view clipped from the main window", 11);

            RunUI(() =>
            {
                var nodePep = (PeptideDocNode)((SrmTreeNode)SkylineWindow.SequenceTree.SelectedNode).Model;
                Assert.AreEqual(null,
                                nodePep.GetPeakCountRatio(
                                    SkylineWindow.SequenceTree.GetDisplayResultsIndex(nodePep)));
                SkylineWindow.SequenceTree.SelectedPath = SkylineWindow.Document.GetPathTo(1, 157);
                Assert.AreEqual("VTVVDDQSVILK", SkylineWindow.SequenceTree.SelectedNode.Text);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                SkylineWindow.ActivateReplicate("Unrefined");
                SkylineWindow.AutoZoomNone();
            });
            PauseForScreenShot("Unrefined chromatogram graph page clipped from main window", 12); // Not L10N

//            foreach (var peptideDocNode in SkylineWindow.Document.Peptides)
//            {
//                var nodeGroup = ((TransitionGroupDocNode)peptideDocNode.Children[0]);
//                Console.WriteLine("{0} - {1}", peptideDocNode.Peptide.Sequence,
//                    nodeGroup.GetDisplayText(SkylineWindow.SequenceTree.GetDisplaySettings(peptideDocNode)));
//            }
//            Console.WriteLine("---------------------------------");

            RunUI(() =>
            {
                var graphChrom = SkylineWindow.GetGraphChrom("Unrefined"); // Not L10N
                Assert.AreEqual(2, graphChrom.Files.Count);
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0];
               
                // Picking Measurable Peptides and Transitions, p. 12
                SkylineWindow.ExpandPeptides();
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
            });

            RunUI(SkylineWindow.AutoZoomBestPeak);
            RestoreViewOnScreen(13);
            RunUI(() => SkylineWindow.ShowGraphSpectrum(false));
            PauseForScreenShot("Targets view clipped from the main window and chromatogram graph metafile", 13);

            RunUI(() => SkylineWindow.ShowGraphSpectrum(true));
            PauseForScreenShot("Library Match plot metafile", 14);

            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 0);
                SkylineWindow.SelectedNode.Expand();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            PauseForScreenShot("Targetes view clipped from the main window", 14); // Not L10N

            RunUI(() =>
            {
                double dotpExpect = Math.Round(Statistics.AngleToNormalizedContrastAngle(0.78), 2);  // 0.57
                AssertEx.Contains(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text,
                    dotpExpect.ToString(LocalizationHelper.CurrentCulture));
                SkylineWindow.EditDelete();

                dotpExpect = 0.34; // Math.Round(Statistics.AngleToNormalizedContrastAngle(0.633), 2);  // 0.44
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                AssertEx.Contains(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text,
                    dotpExpect.ToString(LocalizationHelper.CurrentCulture));
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
                    if (!(name == "y11" || name == "y13" || name == "y14")) // Not L10N
                        SkylineWindow.SequenceTree.SelectedNode = nodeTran;
                    SkylineWindow.SequenceTree.KeysOverride = Keys.Control;
                }
                SkylineWindow.SequenceTree.KeysOverride = Keys.None;
                SkylineWindow.EditDelete();
                for (int i = 0; i < 3; i++)
                    Assert.IsTrue(SkylineWindow.SequenceTree.Nodes[0].Nodes[i].Nodes[0].Nodes.Count == 3);
                SkylineWindow.AutoZoomNone();

                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 5);
                SkylineWindow.Size = new Size(722, 449);
            });

            RunUI(SkylineWindow.AutoZoomBestPeak);
            RestoreViewOnScreen(15);
            PauseForScreenShot("Targetes view clipped from main window and chromatogram graph metafile", 15);

            // Automated Refinement, p. 16
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.MaxTransitionPeakRank = 3;
                refineDlg.PreferLargerIons = true;
                refineDlg.RemoveMissingResults = true;
                refineDlg.RTRegressionThreshold = 0.95;
                refineDlg.DotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.95);    // Convert from original cos(angle) dot-product
                refineDlg.OkDialog();
            });
            WaitForCondition(() => SkylineWindow.Document.PeptideCount < 73);
//            foreach (var peptideDocNode in SkylineWindow.Document.Peptides)
//            {
//                var nodeGroup = ((TransitionGroupDocNode) peptideDocNode.Children[0]);
//                Console.WriteLine("{0} - {1}", peptideDocNode.Peptide.Sequence,
//                    nodeGroup.GetDisplayText(SkylineWindow.SequenceTree.GetDisplaySettings(peptideDocNode)));
//            }
            RunUI(() =>
            {
                Assert.AreEqual(72, SkylineWindow.Document.PeptideCount);
                Assert.AreEqual(216, SkylineWindow.Document.PeptideTransitionCount);
                SkylineWindow.CollapsePeptides();
                SkylineWindow.Undo();
            });
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.MaxTransitionPeakRank = 6;
                refineDlg.RemoveMissingResults = true;
                refineDlg.RTRegressionThreshold = 0.90;
                refineDlg.DotProductThreshold = Statistics.AngleToNormalizedContrastAngle(0.90);    // Convert from original cos(angle) dot-product
                refineDlg.OkDialog();
            });

            WaitForCondition(() => SkylineWindow.Document.PeptideCount < 120);
            RunUI(() =>
            {
                Assert.AreEqual(113, SkylineWindow.Document.PeptideCount);

                // Scheduling for Efficient Acquisition, p. 17 
                SkylineWindow.Undo();
            });

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, mResults =>
            {
                Assert.AreEqual(1, mResults.Chromatograms.Count());

                mResults.SelectedChromatograms =
                    SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Where(
                        set => Equals("Unrefined", set.Name)); // Not L10N

                mResults.RemoveReplicates();

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
                                                                          string.Empty)).ToArray();
            });
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg0.OkDialog);
            RunUI(importResultsNameDlg.NoDialog);
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes
            
            var docCurrent = SkylineWindow.Document;
            RunUI(SkylineWindow.RemoveMissingResults);
            WaitForDocumentChange(docCurrent);
            Assert.AreEqual(86, SkylineWindow.Document.PeptideCount);
            Assert.AreEqual(255, SkylineWindow.Document.PeptideTransitionCount);

            TestRTResidualsSwitch();

            // Measuring Retention Times, p. 17
            {
                var exportDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() => exportDlg.MaxTransitions = 130);
                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 18);
                OkDialog(exportDlg, () => exportDlg.OkDialog(TestFilesDirs[1].FullPath + "\\unscheduled")); // Not L10N
            }
            ///////////////////////

            // Reviewing Retention Time Runs, p. 18
            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
            });
            FindNode("FWEVISDEHGIQPDGTFK");

            RunUI(() => SkylineWindow.Size = new Size(1060, 550));
            RestoreViewOnScreen(19);
            PauseForScreenShot("Main window", 19); // Not L10N

            RunUI(() => SkylineWindow.ShowRTSchedulingGraph());
            WaitForCondition(() => SkylineWindow.GraphRetentionTime != null);

            PauseForScreenShot("Retention Times - Scheduling graph metafile", 19);

            RestoreViewOnScreen(20);

            // Creating a Scheduled Transition List, p. 20 
            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                        peptideSettingsUI.TimeWindow = 4;
                    });
                PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab", 21);
                OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog); // Not L10N
            }

            var exportMethodDlg1 = ShowDialog<ExportMethodDlg>(() =>
                SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
            RunUI(() =>
            {
                exportMethodDlg1.ExportStrategy = ExportStrategy.Single;
                exportMethodDlg1.MethodType = ExportMethodType.Scheduled;
            });
            // TODO: Update tutorial to mention the scheduling options dialog.
            PauseForScreenShot("Export Transition List form", 22); // Not L10N
            RunDlg<SchedulingOptionsDlg>(() =>
                exportMethodDlg1.OkDialog(TestFilesDirs[1].FullPath + "\\scheduled"), // Not L10N
                schedulingOptionsDlg => schedulingOptionsDlg.OkDialog());
            WaitForClosedForm(exportMethodDlg1);

            // Reviewing Multi-Replicate Data, p. 22
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });
            var importResultsDlg1 = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            RunDlg<OpenDataSourceDialog>(() => importResultsDlg1.NamedPathSets = importResultsDlg1.GetDataSourcePathsFile(null),
                openDataSourceDialog =>
                {
                    openDataSourceDialog.SelectAllFileType(ExtThermoRaw);
                    openDataSourceDialog.Open();
                });
            RunDlg<ImportResultsNameDlg>(importResultsDlg1.OkDialog, importResultsNameDlg0 =>
            {
                importResultsNameDlg0.Prefix = "Scheduled_"; // Not L10N
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
                SkylineWindow.CollapsePeptides();
                SkylineWindow.ShowChromatogramLegends(false);
                SkylineWindow.Size = new Size(1024, 768);
            });
            RestoreViewOnScreen(24);
            WaitForGraphs();
            PauseForScreenShot("Main window", 24); // Not L10N

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private static void TestRTResidualsSwitch()
        {
            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForGraphs();
            RTLinearRegressionGraphPane pane;
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.RTGraphController.GraphSummary.TryGetGraphPane(out pane));
                Assert.AreEqual(Resources.GraphData_GraphResiduals_Time_from_Regression,
                    pane.YAxis.Title.Text);
                SkylineWindow.ShowPlotType(PlotTypeRT.correlation);
            });
            WaitForGraphs();
            RunUI(() =>
            {
                Assert.IsTrue(SkylineWindow.RTGraphController.GraphSummary.TryGetGraphPane(out pane));
                Assert.AreEqual(Resources.RTLinearRegressionGraphPane_RTLinearRegressionGraphPane_Measured_Time,
                    pane.YAxis.Title.Text);
            });
        }
    }
}
