using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class ChromatogramIndexOutOfRangeTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestChromatogramIndexOutOfRange()
        {
            TestFilesZip = "https://proteome.gs.washington.edu/~nicksh/test/ChromatogramIndexOutOfRangeTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("D_2026_0224_RJ_5xFADvWT_isoOnly_features_final_forNick.sky")));
            WaitForDocumentLoaded();
            int resultsCount = SkylineWindow.Document.MeasuredResults?.Chromatograms.Count ?? 0;
            ImportResultsFile(TestFilesDir.GetTestPath("D_2026_0224_RJ_29_57.mzML"));
            Assert.IsNotNull(SkylineWindow.Document.MeasuredResults);
            Assert.AreEqual(resultsCount + 1, SkylineWindow.Document.MeasuredResults.Chromatograms.Count);
        }
    }
}
