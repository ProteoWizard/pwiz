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
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestTutorial
{
    [TestClass]
    public class PeakImputationDiaTutorial : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestPeakImputationDiaTutorial()
        {
            if (IsTranslationRequired)
            {
                return;
            }
            CoverShotName = "PeakImputationDia";
            TestFilesZipPaths = new[]
            {
                "https://skyline.ms/tutorials/PeakImputationDia.zip",
                @"TestTutorial\PeakImputationDiaViews.zip"
            };
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                if (!Program.SkylineOffscreen)
                {
                    var screen = Screen.FromControl(SkylineWindow);
                    var width = Math.Min(screen.Bounds.Width, 1680);
                    var height = Math.Min(screen.Bounds.Height, 1050);
                    SkylineWindow.Bounds = new Rectangle(screen.Bounds.Left + (screen.Bounds.Width - width) / 2,
                        screen.Bounds.Top + (screen.Bounds.Height - height) / 2, width, height);
                }
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("ExtracellularVesicalMagNet.sky"));
                var selectedNode = SkylineWindow.SelectedNode;
                Assert.IsInstanceOfType(selectedNode, typeof(PeptideTreeNode));
                Assert.AreEqual("FYNELTEILVR", GetSelectedPeptide());
            });
            WaitForDocumentLoaded();
            var relativeAbundanceForm = FindGraphSummaryByGraphType<SummaryRelativeAbundanceGraphPane>();
            Assert.IsNotNull(relativeAbundanceForm);
            RunUI(() =>
            {
                SkylineWindow.SetAreaProteinTargets(true);
                SkylineWindow.ShowSingleReplicate();
                SkylineWindow.ShowProductTransitions();
            });
            WaitForComplete(relativeAbundanceForm);
            PauseForScreenShot(SkylineWindow);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 0);
            WaitForComplete(relativeAbundanceForm);
            PauseForScreenShot(relativeAbundanceForm);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            WaitForComplete(relativeAbundanceForm);
            PauseForScreenShot(relativeAbundanceForm);
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow("EV-Enrich"));
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            PauseForScreenShot(foldChangeGrid);
            RunUI(()=>foldChangeGrid.DataboundGridControl.ChooseView("log2fold change"));
            WaitForConditionUI(() => foldChangeGrid.IsComplete);
            PauseForScreenShot(foldChangeGrid);
            RunUI(()=>foldChangeGrid.ShowVolcanoPlot());
            var foldChangeVolcanoPlot = FindOpenForm<FoldChangeVolcanoPlot>();
            var floatingWindow = ScreenshotManager.FindParent<FloatingWindow>(foldChangeGrid);
            Assert.IsNotNull(floatingWindow);
            Assert.AreSame(floatingWindow, ScreenshotManager.FindParent<FloatingWindow>(foldChangeVolcanoPlot));
            RunUI(()=>
            {
                floatingWindow.Width = 1024;
            });
            PauseForScreenShot(floatingWindow);
            RunUI(()=>foldChangeGrid.ShowChangeSettings());
            var editGroupComparisonDlg = FindOpenForm<EditGroupComparisonDlg>();
            RunUI(() =>
            {
                editGroupComparisonDlg.ShowAdvanced(true);
                editGroupComparisonDlg.GroupComparisonDef =
                    editGroupComparisonDlg.GroupComparisonDef.ChangeUseZeroForMissingPeaks(true);
            });
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(editGroupComparisonDlg);
            OkDialog(editGroupComparisonDlg, editGroupComparisonDlg.Close);
            PauseForScreenShot(floatingWindow);
            RunUI(()=>
            {
                foldChangeVolcanoPlot.Close();
                foldChangeGrid.ShowVolcanoPlot();
            });
            foldChangeVolcanoPlot = FindOpenForm<FoldChangeVolcanoPlot>();
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(floatingWindow);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            var windowPositions = HideFloatingWindows();
            Assert.AreEqual("FYNELTEILVR", CallUI(GetSelectedPeptide));
            var rtReplicateGraphSummary = SkylineWindow.GraphRetentionTime;
            Assert.IsNotNull(rtReplicateGraphSummary);
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.ShowExemplaryPeak(true));
            WaitForExemplaryPeaks();
            PauseForScreenShot(SkylineWindow);
            RunUI(() =>
            {
                var proteinNode = SkylineWindow.SequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                Assert.IsNotNull(proteinNode);
                var peptideNode = proteinNode.Nodes.OfType<PeptideTreeNode>()
                    .First(node => node.DocNode.Peptide.Sequence == "KADLVNR");
                SkylineWindow.SelectedPath = peptideNode.Path;
            });
            RunUI(()=>
            {
                SkylineWindow.SelectedResultsIndex = 4;
                SkylineWindow.AutoZoomBestPeak();
            });
            WaitForExemplaryPeaks();
            PauseForScreenShot(SkylineWindow);
            GraphSummary scoreToRunGraphSummary = null;
            RunUI(() =>
            {
                SkylineWindow.ShowRTRegressionGraphScoreToRun();
                scoreToRunGraphSummary = FindGraphSummaryByGraphType<RTLinearRegressionGraphPane>();
                Assert.IsNotNull(scoreToRunGraphSummary);
                SkylineWindow.ChooseCalculator(new RtCalculatorOption.Library("ExtracellularVesicalMagNet"));
                SkylineWindow.ShowRegressionMethod(RegressionMethodRT.loess);
            });
            WaitForComplete(scoreToRunGraphSummary);
            PauseForScreenShot(scoreToRunGraphSummary);
            RunUI(()=> SkylineWindow.ShowPlotType(PlotTypeRT.residuals));
            WaitForComplete(scoreToRunGraphSummary);
            PauseForScreenShot(scoreToRunGraphSummary);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 0);
            WaitForComplete(scoreToRunGraphSummary);
            PauseForScreenShot(scoreToRunGraphSummary);
            RunUI(() =>
            {
                var proteinNode = SkylineWindow.SequenceTree.GetNodeOfType<PeptideGroupTreeNode>();
                Assert.IsNotNull(proteinNode);
                var peptideNode = proteinNode.Nodes.OfType<PeptideTreeNode>()
                    .First(node => node.DocNode.Peptide.Sequence == "FYNELTEILVR");
                SkylineWindow.SelectedPath = peptideNode.Path;
            });
            RunUI(()=>rtReplicateGraphSummary.Activate());
            PauseForScreenShot(SkylineWindow);
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(()=>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUi.ImputeMissingPeaks = true;
                });
                PauseForScreenShot(peptideSettingsUi);
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            WaitForConditionUI(() => SkylineWindow.GraphChromatograms.All(chrom => chrom.ExemplaryPeak != null));
            PauseForScreenShot(SkylineWindow);
            RestoreFloatingWindows(windowPositions);
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(floatingWindow);
        }

        private void WaitForComplete(GraphSummary graphSummary)
        {
            //WaitForGraphs();
            WaitForConditionUI(() =>
            {
                var pane = graphSummary.GraphControl.GraphPane;
                if (pane is SummaryRelativeAbundanceGraphPane relativeAbundance)
                {
                    return relativeAbundance.IsComplete;
                }

                return true;
            });
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
