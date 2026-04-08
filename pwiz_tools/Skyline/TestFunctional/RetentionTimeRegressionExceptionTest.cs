using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RetentionTimeRegressionExceptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRetentionTimeRegressionException()
        {
            RunFunctionalTest();
            TestFilesZip = @"TestRetentionTimeRegressionException.zip";
        }

        protected override void DoTest()
        {
            RunDlg<MissingFileDlg>(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("OneReplicate.sky")),
                dlg => dlg.OkDialog());
            WaitForDocumentLoaded();
        }
    }
}
