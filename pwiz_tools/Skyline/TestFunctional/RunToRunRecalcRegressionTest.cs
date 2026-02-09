using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RunToRunRecalcRegressionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRunToRunRecalcRegression()
        {
            TestFilesZip = @"TestFunctional\RunToRunRecalcRegressionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RecalcRegressionTest.sky"));
                SkylineWindow.ShowRTRegressionGraphRunToRun();
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
            RunUI(()=>{
                SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo((int)SrmDocument.Level.TransitionGroups, 0);
                SkylineWindow.SelectedResultsIndex = 0;
                SkylineWindow.RemovePeak();
            });
            WaitForDocumentLoaded();
            WaitForGraphs();
        }
    }
}
