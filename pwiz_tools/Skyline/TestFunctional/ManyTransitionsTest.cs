using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ManyTransitionsTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestManyTransitions()
        {
            TestFilesZip = @"TestFunctional/ManyTransitionsTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("mb_0000.sky")));
            WaitForDocumentLoaded();
            WaitForGraphs();
            RunUI(()=>SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(2, 0));
            WaitForGraphs();
            RunUI(() => SkylineWindow.SelectedPath = SkylineWindow.Document.GetPathTo(3, 0));
            WaitForGraphs();
        }
    }
}
