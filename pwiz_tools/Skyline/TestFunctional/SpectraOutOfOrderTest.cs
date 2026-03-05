using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectraOutOfOrderTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectraOutOfOrder()
        {
            TestFilesZip = @"TestFunctional\SpectraOutOfOrderTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test.sky")));
            ActionUtil.RunAsync(() =>
            {
                Thread.Sleep(TimeSpan.FromMinutes(1));
                Console.Out.WriteLine(TextUtil.LineSeparate(HangDetection.GetAllThreadsCallstacks(Process.GetCurrentProcess().Id)));
            });
            ImportResultsFile(TestFilesDir.GetTestPath("FU2_2026_0223_RJ_10_box20.mzML"));
            
        }
    }
}
