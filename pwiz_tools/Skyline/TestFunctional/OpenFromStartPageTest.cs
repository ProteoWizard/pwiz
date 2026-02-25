using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Startup;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class OpenFromStartPageTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestOpenFromStartPage()
        {
            TestFilesZip = @"TestFunctional\OpenFromStartPageTest.zip";
            RunFunctionalTest();
        }

        [TestMethod]
        public void TestOtherOpenFromStartPage()
        {
            TestFilesZip = @"TestFunctional\AS_Skyline_AdptRT_20260225.zip";
            RunFunctionalTest();
        }

        protected override bool ShowStartPage
        {
            get { return true; }
        }

        protected override void DoTest()
        {
            var startPage = WaitForOpenForm<StartPage>();
            var skyFile = Directory.EnumerateFiles(TestFilesDir.FullPath, "*.sky").First();
            RunUI(() => startPage.OpenFile(skyFile));
            WaitForOpenForm<SkylineWindow>();
            WaitForGraphs();
        }
    }
}
