using System;
using System.Linq;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.GroupComparison;
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
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("PeakImputationDemo.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView("Peptide Areas"));
            WaitForDocumentLoaded();
            WaitForConditionUI(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
            RunUI(()=>SkylineWindow.ShowGroupComparisonWindow("EV-Enrich-Peptide"));
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
            RunUI(() =>
            {
                var biggestFoldChange = foldChangeVolcanoPlot.CurveList
                    .SelectMany(curve => Enumerable.Range(0, curve.NPts).Select(i => curve.Points[i]))
                    .Where(pointPair => pointPair.Tag is FoldChangeRow).OrderByDescending(pointPair => pointPair.X)
                    .First();
                var foldChangeRow = (FoldChangeRow)biggestFoldChange.Tag;
                foldChangeRow.Peptide.LinkValueOnClick(null, EventArgs.Empty);
            });
            PauseForScreenShot(foldChangeVolcanoPlot, processShot:ClipControl(foldChangeVolcanoPlot));
            var rtReplicateGraphSummary = ShowDialog<GraphSummary>(SkylineWindow.ShowRTReplicateGraph);
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            var windowPositions = HideFloatingWindows();
            PauseForScreenShot(SkylineWindow);
            RunUI(()=>SkylineWindow.ShowExemplaryPeak(true));
            var graphChromatogram = SkylineWindow.GetGraphChrom("Total04_HydN_12mz_42");
            WaitForConditionUI(() => graphChromatogram.ExemplaryPeak != null);
            PauseForScreenShot(graphChromatogram);

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
            WaitForConditionUI(() => graphChromatogram.ExemplaryPeak != null);
            PauseForScreenShot(graphChromatogram);
            RestoreFloatingWindows(windowPositions);
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.AlignToRtPrediction = true);
            WaitForGraphs();
            windowPositions = HideFloatingWindows();
            PauseForScreenShot(graphChromatogram);
            RestoreFloatingWindows(windowPositions);
            PauseForScreenShot(rtReplicateGraphSummary);
        }
    }
}
