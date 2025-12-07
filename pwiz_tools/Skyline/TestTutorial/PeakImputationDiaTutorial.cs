using System.Globalization;
using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.EditUI;
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
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("ExtracellularVesicalMagNet.sky"));
                SkylineWindow.SelectedResultsIndex = 3;
            });
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.ShowPeakAreaRelativeAbundanceGraph());
            var relativeAbundanceForm = FindGraphSummaryByGraphType<SummaryRelativeAbundanceGraphPane>();
            Assert.IsNotNull(relativeAbundanceForm);
            RunUI(() =>
            {
                SkylineWindow.SetAreaProteinTargets(true);
                SkylineWindow.ShowSingleReplicate();
            });
            WaitForGraphs();
            PauseForScreenShot(relativeAbundanceForm);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 0);
            WaitForGraphs();
            PauseForScreenShot(relativeAbundanceForm);
            RunLongDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findNodeDlg =>
            {
                RunUI(() => {
                    findNodeDlg.SearchString = "CD9_HUMAN";
                });
                PauseForScreenShot(findNodeDlg);
                RunUI(findNodeDlg.FindNext);
            }, findNodeDlg=>findNodeDlg.Close());
            WaitForGraphs();
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
            var rtReplicateGraphSummary = ShowNestedDlg<GraphSummary>(SkylineWindow.ShowRTReplicateGraph);
            PauseForScreenShot(rtReplicateGraphSummary);
            RunDlg<FindNodeDlg>(SkylineWindow.ShowFindNodeDlg, findNodeDlg =>
            {
                findNodeDlg.SearchString = 441.24.ToString("F04", CultureInfo.CurrentCulture);
                findNodeDlg.FindNext();
                findNodeDlg.Close();
            });
            PauseForScreenShot(rtReplicateGraphSummary);
            RunUI(()=>SkylineWindow.SelectedResultsIndex = 3);
            var windowPositions = HideFloatingWindows();
            PauseForScreenShot(SkylineWindow);
            var graphChromatogram = SkylineWindow.GetGraphChrom("Total04_HydN_12mz_42");
            PauseForScreenShot(graphChromatogram);
            RunUI(()=>SkylineWindow.ShowExemplaryPeak(true));
            WaitForConditionUI(() => graphChromatogram.ExemplaryPeak != null);
            PauseForScreenShot(graphChromatogram);
            RunUI(() =>
            {
                SkylineWindow.ShowProductTransitions();
            });
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
