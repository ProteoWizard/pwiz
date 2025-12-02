using DigitalRune.Windows.Docking;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
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
                SkylineWindow.OpenFile(TestFilesDirs[0].GetTestPath("2025-DIA-Webinar-MagNet.sky"));
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(()=>documentGrid.ChooseView("Protein Areas"));
            WaitForDocumentLoaded();
            WaitForConditionUI(() => documentGrid.IsComplete);
            PauseForScreenShot(documentGrid);
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
            RunLongDlg<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI, peptideSettingsUi =>
            {
                RunUI(()=>
                {
                    peptideSettingsUi.SelectedTab = PeptideSettingsUI.TABS.Prediction;
                    peptideSettingsUi.ImputeMissingPeaks = true;
                });
                PauseForScreenShot(peptideSettingsUi);
            }, peptideSettingsUi=>peptideSettingsUi.OkDialog());
            WaitForConditionUI(() => foldChangeVolcanoPlot.IsComplete);
            PauseForScreenShot(floatingWindow);
        }
    }
}
