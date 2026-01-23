using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ImportLargeTransitionListTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestImportLargeTransitionList()
        {
            TestFilesZip = @"TestFunctional\ImportLargeTransitionListTest.data";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("Document.sky")));
            RunDlg<ImportTransitionListColumnSelectDlg>(()=>SkylineWindow.ImportMassList(TestFilesDir.GetTestPath("TransitionList.csv")),
                dlg =>
                {
                    dlg.OkDialog();
                });
        }
    }
}
