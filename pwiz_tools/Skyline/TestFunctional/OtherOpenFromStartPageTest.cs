using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class OtherOpenFromStartPageTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOtherOpenFromStartPage()
        {
            TestFilesZip = @"TestFunctional\OpenFromStartPageTest.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            RunUI(() => startPage.OpenFile(TestFilesDir.GetTestPath("OpenFromStartPageTest.sky")));
            WaitForOpenForm<SkylineWindow>();
            WaitForGraphs();
        }

    }
}
