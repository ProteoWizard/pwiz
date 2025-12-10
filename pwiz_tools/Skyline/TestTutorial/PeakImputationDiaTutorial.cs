using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
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
                "https://proteome.gs.washington.edu/~nicksh/tutorials/PeakImputationDia.zip",
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
            WaitComplete(relativeAbundanceForm);
            PauseForScreenShot(SkylineWindow);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 0);
            WaitComplete(relativeAbundanceForm);
            PauseForScreenShot(relativeAbundanceForm);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            WaitComplete(relativeAbundanceForm);
            PauseForScreenShot(relativeAbundanceForm);
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow("EV-Enrich"));
            var foldChangeGrid = FindOpenForm<FoldChangeGrid>();
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
            var graphChromatogram = SkylineWindow.GetGraphChrom("Total04_HydN_12mz_42");
            RunUI(()=>SkylineWindow.ShowExemplaryPeak(true));
            WaitForConditionUI(() => graphChromatogram.ExemplaryPeak != null);
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
            WaitForGraphs();
            WaitForConditionUI(() => SkylineWindow.GraphChromatograms.All(chrom => chrom.ExemplaryPeak != null));
            PauseForScreenShot(SkylineWindow);
            RestoreFloatingWindows(windowPositions);
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(floatingWindow);


            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("Webinar26.sky"));
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.Molecules, 0);
                SkylineWindow.ShowRTReplicateGraph();
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
            rtReplicateGraphSummary = SkylineWindow.GraphRetentionTime;
            PauseForScreenShot(rtReplicateGraphSummary);
            RunDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                peptideSettingsUi.ImputeMissingPeaks = true;
                peptideSettingsUi.OkDialog();
            });
            WaitForGraphs();
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.AlignToRtPrediction = true);
            WaitForGraphs();
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.ShowRTRegressionGraphScoreToRun());
            var rtLinearRegressionGraphSummary = FormUtil.OpenForms.OfType<GraphSummary>()
                .Single(graph => graph.TryGetGraphPane(out RTLinearRegressionGraphPane _));
            RunUI(() =>
            {
                SkylineWindow.ChooseCalculator(new RtCalculatorOption.Library("Webinar26"));
                SkylineWindow.ShowRegressionMethod(RegressionMethodRT.loess);
            });
            WaitForGraphs();
            PauseForScreenShot(rtLinearRegressionGraphSummary);
        }

        private void WaitComplete(GraphSummary graphSummary)
        {
            WaitForGraphs();
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
    }
}
