using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Controls.GroupComparison;
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
        }
    }
}
