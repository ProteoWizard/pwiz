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
using ZedGraph;


namespace pwiz.SkylineTestTutorial
{
    /// <summary>
    /// Testing the tutorial for Skyline Targeted Method Refinement
    /// </summary>
    [TestClass]
    public class MethodRefinementTutorialTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestMethodRefinementTutorial()
        {
            // Set true to look at tutorial screenshots.
//            IsPauseForScreenShots = true;
//            IsCoverShotMode = true;
            CoverShotName = "MethodRefine";

            // Multi-file import has problems with mzML on this test
            ForceMzml = true; // (Settings.Default.ImportResultsSimultaneousFiles == 0);   // 2-3x faster than raw files for this test.

            LinkPdf = "https://skyline.ms/_webdav/home/software/Skyline/%40files/tutorials/MethodRefine-20_1.pdf";

            // Set to use MzML for speed, especially during debugging.
            //Skyline.Program.NoVendorReaders = true;

            string supplementZip = (UseRawFiles ?
                @"https://skyline.ms/tutorials/MethodRefineSupplement.zip" : // Not L10N
                @"https://skyline.ms/tutorials/MethodRefineSupplementMzml.zip"); // Not L10N

            TestFilesZipPaths = new[] { supplementZip,
                UseRawFiles ?
                    @"https://skyline.ms/tutorials/MethodRefine.zip" : // Not L10N
                    @"https://skyline.ms/tutorials/MethodRefineMzml.zip", // Not L10N
                @"TestTutorial\MethodRefinementViews.zip",                     
            };
         
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Skyline Targeted Method Refinement

            var folderMethodRefine = UseRawFiles ? "MethodRefine" : "MethodRefineMzml"; // Not L10N

            // Results Data, p. 3
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
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.GraphSpectrumSettings.ShowBIons = true;
                SkylineWindow.Size = new Size(1266, 736);

                Assert.AreEqual(SkylineWindow.SequenceTree.SelectedNode.Text, "YLGAYLLATLGGNASPSAQDVLK"); // Not L10N
            });
            PauseForScreenShot("Main window", 3);

            // Unrefined Methods, p. 4
            {
                var exportDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() =>
                {
                    exportDlg.ExportStrategy = ExportStrategy.Buckets;
                    exportDlg.MethodType = ExportMethodType.Standard;
                    exportDlg.OptimizeType = ExportOptimize.NONE;
                    exportDlg.MaxTransitions = 59;
                });
                PauseForScreenShot("Export Transition List form", 5); // Not L10N
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
            PauseForScreenShot<AllChromatogramsGraph>("Loading Chromatograms: Take screenshot at about 25% loaded...", 7);
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
            RestoreViewOnScreen(8);
            PauseForScreenShot("Chromatogram graph metafile", 8);

            if (IsCoverShotMode)
            {
                RestoreCoverViewOnScreen();
                RunUI(SkylineWindow.AutoZoomBestPeak);
                // Change and restore selection to ensure all graphs are updated
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.PrevNode);
                WaitForGraphs();
                RunUI(() => SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SelectedNode.NextNode);
                TakeCoverShot();
                return;
            }

            // Simple Manual Refinement, p. 6
            int startingNodeCount = SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false);

            Assert.AreEqual("YLGAYLLATLGGNASPSAQDVLK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text); // Not L10N

            RunUI(() =>
            {
                SkylineWindow.SequenceTree.SelectedNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.EditDelete();
                SkylineWindow.ShowRTRegressionGraphScoreToRun();
            });
            WaitForRegression();
            Assert.AreEqual(SkylineWindow.SequenceTree.Nodes[0].GetNodeCount(false), startingNodeCount - 1);
            Assert.AreEqual("VLEAGGLDCDMENANSVVDALK", SkylineWindow.SequenceTree.Nodes[0].Nodes[0].Text); // Not L10N
            RestoreViewOnScreen(9);
            PauseForScreenShot("Retention Times Regression plot metafile", 9);

            RunDlg<RegressionRTThresholdDlg>(SkylineWindow.ShowRegressionRTThresholdDlg, rtThresholdDlg =>
            {
                rtThresholdDlg.Threshold = 0.95;
                rtThresholdDlg.OkDialog();
            });
            WaitForRegression();
            PauseForScreenShot("Retention Times Regression plot metafile with 0.95 threshold", 9); // Not L10N

            TestRTResidualsSwitch();

            RunDlg<EditRTDlg>(SkylineWindow.CreateRegression, editRTDlg =>
            {
                Assert.AreEqual(146, editRTDlg.PeptideCount);
                Assert.AreEqual(15.7, editRTDlg.Regression.TimeWindow, 0.05);
                editRTDlg.OkDialog();
            });

            RunUI(() => SkylineWindow.ShowGraphRetentionTime(false));
            RunUI(SkylineWindow.AutoZoomNone);
            PauseForScreenShot("Chromatogram graph metafile zoomed out", 10); // Not L10N

            // Missing Data, p. 10
            RunUI(() =>
                {
                    SkylineWindow.RTGraphController.SelectPeptide(SkylineWindow.Document.GetPathTo(1, 163));
                    Assert.AreEqual("YLAEVASEDR", SkylineWindow.SequenceTree.SelectedNode.Text); // Not L10N
                });
            RestoreViewOnScreen(12);
            //  Restoring the view changes the selection
            RunUI(SkylineWindow.CollapsePeptides);
            FindNode("YLAEVASEDR");
            RunUI(() => SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.Nodes[0].Nodes[153]);

            PauseForScreenShot("Targets view clipped from the main window", 12);

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
            RestoreViewOnScreen(13);
            PauseForScreenShot("Unrefined chromatogram graph page clipped from main window", 13); // Not L10N

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
            PauseForScreenShot("Targets view clipped from the main window AND chromatogram graph metafile", 14);
            RestoreViewOnScreen(16);
            PauseForScreenShot("Library Match plot metafile", 16);

            RunUI(() =>
            {
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int) SrmDocument.Level.Transitions, 0);
                SkylineWindow.SelectedNode.Expand();
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
            });

            PauseForScreenShot("Targets view clipped from the main window", 16); // Not L10N

            RunUI(() =>
            {
                double dotpExpect = Math.Round(Statistics.AngleToNormalizedContrastAngle(0.78), 2);  // 0.57
                AssertEx.Contains(SkylineWindow.SequenceTree.SelectedNode.Nodes[0].Text,
                    dotpExpect.ToString(LocalizationHelper.CurrentCulture));
                SkylineWindow.EditDelete();

                dotpExpect = 0.53; // Math.Round(Statistics.AngleToNormalizedContrastAngle(0.633), 2);  // 0.44
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
            RestoreViewOnScreen(17);
            PauseForScreenShot("Targets view clipped from main window and chromatogram graph metafile", 17);

            // Automated Refinement, p. 16
            var refineDlgLoose = ShowDialog<RefineDlg>(SkylineWindow.ShowRefineDlg);
            RunUI(() =>
            {
                refineDlgLoose.SelectedTab = RefineDlg.TABS.Results;
                refineDlgLoose.MaxTransitionPeakRank = 3;
                refineDlgLoose.PreferLargerIons = true;
                refineDlgLoose.RemoveMissingResults = true;
                refineDlgLoose.RTRegressionThreshold = 0.95;
                refineDlgLoose.DotProductThreshold = 0.8;
            });
            PauseForForm(typeof(RefineDlg.ResultsTab));
            OkDialog(refineDlgLoose, refineDlgLoose.OkDialog);

            const int expectedRefinedPeptideCount = 80;
            WaitForCondition(() => SkylineWindow.Document.PeptideCount <= expectedRefinedPeptideCount);
//            foreach (var peptideDocNode in SkylineWindow.Document.Peptides)
//            {
//                var nodeGroup = ((TransitionGroupDocNode) peptideDocNode.Children[0]);
//                Console.WriteLine("{0} - {1}", peptideDocNode.Peptide.Sequence,
//                    nodeGroup.GetDisplayText(SkylineWindow.SequenceTree.GetDisplaySettings(peptideDocNode)));
//            }
            RunUI(() =>
            {
                Assert.AreEqual(expectedRefinedPeptideCount, SkylineWindow.Document.PeptideCount);
                Assert.AreEqual(240, SkylineWindow.Document.PeptideTransitionCount);
                SkylineWindow.CollapsePeptides();
                SkylineWindow.Undo();
            });
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlgLoose.SelectedTab = RefineDlg.TABS.Results;
                refineDlg.MaxTransitionPeakRank = 6;
                refineDlg.RemoveMissingResults = true;
                refineDlg.RTRegressionThreshold = 0.90;
                refineDlg.DotProductThreshold = 0.712;
                refineDlg.OkDialog();
            });
            const int expectedPeptideCount = 127;
            WaitForCondition(() => SkylineWindow.Document.PeptideCount <= expectedPeptideCount);
            RunUI(() =>
            {
                Assert.AreEqual(expectedPeptideCount, SkylineWindow.Document.PeptideCount);

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

            RunUI(()=>SkylineWindow.SaveDocument());

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
            // This test fails regularly on certain test machines - is it a filesystem problem?
            WaitForConditionUI(() => importResultsDlg0.NamedPathSets != null,
                    "Failure in DataSourceUtil.GetDataSourcesInSubdirs - no filesets found");
            WaitForConditionUI(() => importResultsDlg0.NamedPathSets.Length == 2,
                    "Failure in DataSourceUtil.GetDataSourcesInSubdirs - expected 2 filesets");
            WaitForConditionUI(() => importResultsDlg0.NamedPathSets[0].Value.Length == 2,
                    "Failure in DataSourceUtil.GetDataSourcesInSubdirs - expected 2 files in fileset 0");
            WaitForConditionUI(() => importResultsDlg0.NamedPathSets[1].Value.Length == 2,
                    "Failure in DataSourceUtil.GetDataSourcesInSubdirs - expected 2 files in fileset 1");
            var importResultsNameDlg = ShowDialog<ImportResultsNameDlg>(importResultsDlg0.OkDialog);
            RunUI(importResultsNameDlg.NoDialog);
            WaitForCondition(15*60*1000, () => SkylineWindow.Document.Settings.HasResults && SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 15 minutes
            
            var docCurrent = SkylineWindow.Document;
            RunUI(SkylineWindow.RemoveMissingResults);
            WaitForDocumentChange(docCurrent);
            Assert.AreEqual(86, SkylineWindow.Document.PeptideCount);
            Assert.AreEqual(255, SkylineWindow.Document.PeptideTransitionCount);

            RunUI(SkylineWindow.ShowRTRegressionGraphScoreToRun);
            WaitForGraphs();
            WaitForRegression();

            TestRTResidualsSwitch();

            // Measuring Retention Times, p. 20
            {
                var exportDlg = ShowDialog<ExportMethodDlg>(() => SkylineWindow.ShowExportMethodDialog(ExportFileType.List));
                RunUI(() => exportDlg.MaxTransitions = 130);
                PauseForScreenShot<ExportMethodDlg.TransitionListView>("Export Transition List form", 20);
                OkDialog(exportDlg, () => exportDlg.OkDialog(TestFilesDirs[1].FullPath + "\\unscheduled")); // Not L10N
            }
            ///////////////////////

            // Reviewing Retention Time Runs, p. 21
            RunUI(() =>
            {
                SkylineWindow.ShowGraphSpectrum(false);
                SkylineWindow.ArrangeGraphsTiled();
                SkylineWindow.AutoZoomNone();
                SkylineWindow.AutoZoomBestPeak();
            });
            FindNode("FWEVISDEHGIQPDGTFK");

            RunUI(() => SkylineWindow.Size = new Size(1060, 550));
            RestoreViewOnScreen(21);
            PauseForScreenShot("Main window", 21); // Not L10N

            RTScheduleGraphPane pane = null;
            RunUI(() =>
            {
                SkylineWindow.ShowRTSchedulingGraph();
                WaitForCondition(() => SkylineWindow.GraphRetentionTime != null);
                Assert.IsTrue(SkylineWindow.GraphRetentionTime.TryGetGraphPane(out pane));
            });
            WaitForConditionUI(() => pane.CurveList.Count == 3);
            RunUI(() =>
            {
                Assert.AreEqual(33, GetMaxPoint(pane.CurveList[0]));
                Assert.AreEqual(57, GetMaxPoint(pane.CurveList[1]));
                Assert.AreEqual(93, GetMaxPoint(pane.CurveList[2]));
            });

            PauseForScreenShot("Retention Times - Scheduling graph metafile", 22);

            RestoreViewOnScreen(22);

            // Creating a Scheduled Transition List, p. 22 
            {
                var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
                RunUI(() =>
                    {
                        peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                        peptideSettingsUI.TimeWindow = 4;
                    });
                PauseForScreenShot<PeptideSettingsUI.PredictionTab>("Peptide Settings - Prediction tab", 23);
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
            PauseForScreenShot("Export Transition List form", 24); // Not L10N

            RunDlg<ExportMethodScheduleGraph>(exportMethodDlg1.ShowSchedulingGraph, dlg =>
            {
                WaitForCondition(() => dlg.GraphControl.GraphPane.CurveList.Count > 0);
                Assert.AreEqual(48, GetMaxPoint(dlg.GraphControl.GraphPane.CurveList[0]));
                dlg.Close();
            });

            RunDlg<SchedulingOptionsDlg>(() =>
                exportMethodDlg1.OkDialog(TestFilesDirs[1].FullPath + "\\scheduled"), // Not L10N
                schedulingOptionsDlg => schedulingOptionsDlg.OkDialog());
            WaitForClosedForm(exportMethodDlg1);

            // Reviewing Multi-Replicate Data, p. 25
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                manageResultsDlg.RemoveAllReplicates();
                manageResultsDlg.OkDialog();
            });
            RunUI(()=>SkylineWindow.SaveDocument());
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
            RestoreViewOnScreen(26);
            WaitForGraphs();
            PauseForScreenShot("Main window", 26); // Not L10N

            // Show the RefineDlg.ConsistencyTab for localization text review
            var refineDlgConsistency = ShowDialog<RefineDlg>(SkylineWindow.ShowRefineDlg);
            RunUI(() => refineDlgConsistency.SelectedTab = RefineDlg.TABS.Consistency);
            PauseForForm(typeof(RefineDlg.ConsistencyTab));
            OkDialog(refineDlgConsistency, refineDlgConsistency.OkDialog);

            RunUI(() => SkylineWindow.SaveDocument());
            RunUI(SkylineWindow.NewDocument);
        }

        private static int GetMaxPoint(CurveItem curve)
        {
            var points = curve.Points;
            var maxTransitions = -1;
            for (var i = 0; i < points.Count; i++)
            {
                var transitions = (int)points[i].Y;
                if (transitions > maxTransitions)
                    maxTransitions = transitions;
            }

            return maxTransitions;
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
