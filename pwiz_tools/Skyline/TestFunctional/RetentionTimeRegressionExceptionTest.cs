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
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("OneReplicate.sky"));
                Settings.Default.RTCalculatorName = "Native_Human_Glycopeptides_GradOpt";
            });
            WaitForDocumentLoaded();
            RunUI(()=>SkylineWindow.ShowRTRegressionGraphScoreToRun());
            WaitForGraphs();
        }
    }
}
