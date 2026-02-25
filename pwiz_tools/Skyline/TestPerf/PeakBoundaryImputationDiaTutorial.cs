/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PeakBoundaryImputationDiaTutorial : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakBoundaryImputationDiaTutorial()
        {
            if (IsTranslationRequired)
            {
                return;
            }
            CoverShotName = "PeakBoundaryImputation-DIA";
            TestFilesZipPaths = new[]
            {
                "https://skyline.ms/tutorials/PeakBoundaryImputation-DIA.zip",
                @"TestPerf\PeakBoundaryImputationDiaViews.zip"
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            const string pepOfInterest1 = "FYNELTEILVR";
            const string pepOfInterest2 = "KADLVNR";
            RunUI(() =>
            {
                if (!Program.SkylineOffscreen)
                {
                    var screenBounds = Screen.GetWorkingArea(SkylineWindow);
                    var width = Math.Min(screenBounds.Width, 1680);
                    var height = Math.Min(screenBounds.Height, 1050);
                    SkylineWindow.Bounds = new Rectangle(screenBounds.Left + (screenBounds.Width - width) / 2,
                        screenBounds.Top + (screenBounds.Height - height) / 2, width, height);
                }
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("ExtracellularVesicalMagNet.sky"));
                var selectedNode = SkylineWindow.SelectedNode;
                Assert.IsInstanceOfType(selectedNode, typeof(PeptideTreeNode));
                Assert.AreEqual(pepOfInterest1, GetSelectedPeptide());
            });
            WaitForDocumentLoaded();
            RunUI(() =>
            {
                SkylineWindow.SetAreaProteinTargets(true);
                SkylineWindow.ShowSingleReplicate();
                SkylineWindow.ShowProductTransitions();
            });
            WaitForRelativeAbundanceComplete();
            PauseForScreenShot(SkylineWindow, "Skyline main window");
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 0);
            WaitForRelativeAbundanceComplete();
            PauseForRelativeAbundanceGraphScreenShot("Peak abundance - EV enriched");
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            WaitForRelativeAbundanceComplete();
            PauseForRelativeAbundanceGraphScreenShot("Peak abundance - Total plasma");
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow("EV-Enrich"));
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            PauseForScreenShot(foldChangeGrid, "Group comparison grid");
            RunUI(()=>foldChangeGrid.DataboundGridControl.ChooseView("log2fold change"));
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            PauseForScreenShot(foldChangeGrid, "Group comparison with additional columns");
            RunUI(()=>foldChangeGrid.ShowVolcanoPlot());
            var foldChangeVolcanoPlot = FindOpenForm<FoldChangeVolcanoPlot>();
            var floatingWindow = ScreenshotManager.FindParent<FloatingWindow>(foldChangeGrid);
            Assert.IsNotNull(floatingWindow);
            Assert.AreSame(floatingWindow, ScreenshotManager.FindParent<FloatingWindow>(foldChangeVolcanoPlot));
            RunUI(()=>
            {
                floatingWindow.Width = 1024;
            });
            PauseForScreenShot(floatingWindow, "Group comparison with volcano plot");
            RunUI(()=>foldChangeGrid.ShowChangeSettings());
            var editGroupComparisonDlg = FindOpenForm<EditGroupComparisonDlg>();
            RunUI(() =>
            {
                editGroupComparisonDlg.ShowAdvanced(true);
                editGroupComparisonDlg.GroupComparisonDef =
                    editGroupComparisonDlg.GroupComparisonDef.ChangeUseZeroForMissingPeaks(true);
            });
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(editGroupComparisonDlg, "Edit group comparison dialog");
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.Close);
            PauseForScreenShot(floatingWindow, "Volcano plot with missing peaks as zero");
            RunUI(()=>
            {
                foldChangeVolcanoPlot.Close();
                foldChangeGrid.ShowVolcanoPlot();
            });
            foldChangeVolcanoPlot = FindOpenForm<FoldChangeVolcanoPlot>();
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForVolcanoPlotGraphScreenShot("Volcano plot zoomed out");
            RunUI(() => SkylineWindow.SelectedResultsIndex = 3);
            var windowPositions = HideFloatingWindows();
            Assert.AreEqual(pepOfInterest1, CallUI(GetSelectedPeptide));
            var rtReplicateGraphSummary = SkylineWindow.GraphRetentionTime;
            Assert.IsNotNull(rtReplicateGraphSummary);
            PauseForRetentionTimeGraphScreenShot("Retention times replicate comparison");
            RunUI(() => SkylineWindow.ShowExemplaryPeak(true));
            WaitForExemplaryPeaks();
            PauseForScreenShot(SkylineWindow, "Chromatograms with exemplary peak");
            SelectPeptide(pepOfInterest2);
            RunUI(()=>
            {
                SkylineWindow.SelectedResultsIndex = 4;
                SkylineWindow.AutoZoomBestPeak();
            });
            WaitForExemplaryPeaks();
            PauseForScreenShot(SkylineWindow, $"{pepOfInterest2} with misaligned imputation");
            WaitForGraphs();
            RunUI(() =>
            {
                // Reduce the number of updates to this graph by setting directly to settings
                // and then showing the graph.
                Settings.Default.RtCalculatorOption = new RtCalculatorOption.Library("ExtracellularVesicalMagNet");
                RTGraphController.RegressionMethod = RegressionMethodRT.loess;
                SkylineWindow.ShowRTRegressionGraphScoreToRun();
            });
            WaitForRTRegressionComplete();
            PauseForRetentionTimeGraphScreenShot("Score to run regression");
            RunUI(() => SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForRTRegressionComplete();
            PauseForRetentionTimeGraphScreenShot("Regression residuals plot");
            RunUI(() => SkylineWindow.SelectedResultsIndex = 0);
            WaitForRTRegressionComplete();
            PauseForRetentionTimeGraphScreenShot("Residuals for EV13 replicate");
            SelectPeptide(pepOfInterest1);
            RunUI(() => rtReplicateGraphSummary.Activate());
            PauseForScreenShot(SkylineWindow, $"{pepOfInterest1} peptide view");
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(() =>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUi.ImputeMissingPeaks = true;
                });
                PauseForScreenShot(peptideSettingsUi, "Peptide settings prediction tab");
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            WaitForConditionUI(() => SkylineWindow.GraphChromatograms.All(chrom => chrom.ExemplaryPeak != null));
            PauseForScreenShot(SkylineWindow, "Chromatograms with imputed boundaries");
            RestoreFloatingWindows(windowPositions);
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForVolcanoPlotGraphScreenShot("Volcano plot after imputation");

            if (IsCoverShotMode)
            {
                // Remove some of the peptide siblings of the selected node to
                // allow it to be closer to the protein highlighted in the volcano plot
                // and relative abundance plot views.
                TreeNode nodeProtTree = null;
                RunUI(() =>
                {
                    nodeProtTree = SkylineWindow.SequenceTree.SelectedNode.Parent;
                    DeleteNodes(nodeProtTree, 0, 6);
                    DeleteNodes(nodeProtTree, 4, 14);
                    DeleteNodes(nodeProtTree, 5, 5);
                    SkylineWindow.ShowChromatogramLegends(false);
                });
                // Restore a layout with the volcano plot alone in a floating window
                RestoreCoverViewOnScreen();
                // Reselect the peptide of interest - though the layout should have selected it
                SelectPeptide(pepOfInterest1);
                var volcanoPlot = FindOpenForm<FoldChangeVolcanoPlot>();
                Assert.IsNotNull(volcanoPlot);
                RunUI(() =>
                {
                    // Place it in the lower left corner of the Skyline window
                    // showing a line of pluses for the hidden proteins, and just
                    // above the Targets and Files tabs.
                    var volcanoFrame = volcanoPlot.Parent.Parent;
                    volcanoFrame.Location = SkylineWindow.SequenceTree.PointToScreen(
                        new Point(10, SkylineWindow.SequenceTree.Height - 4 - volcanoFrame.Height));
                    SkylineWindow.SequenceTree.TopNode = SkylineWindow.SequenceTree.SelectedNode.Parent.PrevNode;
                    SkylineWindow.SequenceTree.SetScrollPos(Orientation.Horizontal, 0);
                });
                // Wait for plots to finish updating before taking the cover shot
                WaitForConditionUI(() => volcanoPlot.IsComplete);
                WaitForRelativeAbundanceComplete();
                TakeCoverShot();
            }
        }

        private void DeleteNodes(TreeNode nodeProtTree, int start, int count)
        {
            SelectNodes(nodeProtTree, start, count);
            SkylineWindow.EditDelete();
        }

        private void WaitForRTRegressionComplete()
        {
            var scoreToRunGraphPane = GetScoreToRunGraphPane();
            Assert.IsNotNull(scoreToRunGraphPane);
            WaitForConditionUI(() => scoreToRunGraphPane.IsComplete);
        }

        public static RTLinearRegressionGraphPane GetScoreToRunGraphPane()
        {
            return FormUtil.OpenForms.OfType<GraphSummary>().Select(graphSummary => graphSummary.GraphControl.GraphPane)
                .OfType<RTLinearRegressionGraphPane>().FirstOrDefault(graphPane => !graphPane.RunToRun);
        }

        private void WaitForRelativeAbundanceComplete()
        {
            var summary = FindGraphSummaryByGraphType<SummaryRelativeAbundanceGraphPane>();
            Assert.IsNotNull(summary);
            WaitForPaneCondition<SummaryRelativeAbundanceGraphPane>(summary, pane => pane.IsComplete);
        }

        private static void SelectPeptide(string sequence)
        {
            RunUI(() =>
            {
                var proteinNode = SkylineWindow.SequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                Assert.IsNotNull(proteinNode);
                var peptideNode = proteinNode.Nodes.OfType<PeptideTreeNode>()
                    .First(node => node.DocNode.Peptide.Sequence == sequence);
                SkylineWindow.SelectedPath = peptideNode.Path;
            });
        }

        private void SelectNodes(TreeNode nodeProtTree, int start, int count)
        {
            SkylineWindow.SequenceTree.SelectedNode = nodeProtTree.Nodes[start];
            for (int i = start + 1; i < start + count; i++)
            {
                SkylineWindow.SequenceTree.SelectedNodes.Add((TreeNodeMS)nodeProtTree.Nodes[i]);
            }
        }

        private string GetSelectedPeptide()
        {
            return SkylineWindow.SequenceTree.GetNodeOfType<PeptideTreeNode>()?.DocNode.Peptide.Sequence;
        }

        private void WaitForExemplaryPeaks()
        {
            WaitForGraphs();
            WaitForConditionUI(() => SkylineWindow.GraphChromatograms.All(chrom => chrom.ExemplaryPeak != null));
        }
    }
}
