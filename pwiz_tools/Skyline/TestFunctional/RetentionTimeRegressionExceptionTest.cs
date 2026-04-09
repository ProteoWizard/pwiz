using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RetentionTimeRegressionExceptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRetentionTimeRegressionException()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeRegressionExceptionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("RtRegressionBugSmall.sky"));
                Settings.Default.RTCalculatorName = "TTOF_64w_iRT-C18";
            });
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.ShowRTRegressionGraphScoreToRun());
            WaitForGraphs();
        }
    }
}
