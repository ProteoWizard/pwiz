using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class RescoreTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestRescore()
        {
            TestFilesZip = @"TestFunctional\RescoreTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Rat_plasma.sky")));
            WaitForDocumentLoaded();
            RunLongDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageResultsDlg =>
            {
                RunDlg<RescoreResultsDlg>(manageResultsDlg.Rescore, rescoreResultsDlg=>rescoreResultsDlg.Rescore(false));
               
            }, _ => { });
            WaitForDocumentLoaded();
        }
    }
}
